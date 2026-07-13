using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.Timeouts;
using System.Text.Json;
using WinBridgeApi.Extraction;

const int CommandTimeoutSeconds = 25;
const int RequestTimeoutSeconds = 30;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: basic console logs with timestamps ---
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.SingleLine = true;
});

// Uses WindowsServiceLifetime and Event Log only when launched by the Service
// Control Manager. Console execution remains available for diagnostics.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WinBridgeApi";
});
builder.Services.AddRequestTimeouts();

// --- Kestrel: HTTP only on loopback. SSH is the only remote entry point. ---
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000);
});

var app = builder.Build();
app.UseRequestTimeouts();

var connectionString = app.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException("Connection string 'SqlServer' is not configured in appsettings.json.");

var logger = app.Logger;

// Intentionally no UseHttpsRedirection() and no UseHsts(): plain HTTP only,
// TLS is terminated by the external SSH tunnel.

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        timestamp = DateTime.UtcNow.ToString("O")
    });
});

// TEMPORARY / TEST-ONLY endpoint: executes arbitrary SQL passed via query string.
// Remove before exposing this API beyond controlled testing.
app.MapGet("/query", async (string? sql, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(sql))
    {
        return Results.BadRequest(new { error = "Missing 'sql' query parameter." });
    }

    var requestTimeout = ctx.Features.Get<IHttpRequestTimeoutFeature>()?.RequestTimeoutToken
        ?? CancellationToken.None;
    return await ExecuteQueryAsync(
        sql, null, connectionString, logger, ctx.RequestAborted, requestTimeout);
}).WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));

app.MapPost("/query", async (QueryRequest? request, HttpContext ctx) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Sql))
    {
        return Results.BadRequest(new { error = "Missing 'sql' field in request body." });
    }

    var requestTimeout = ctx.Features.Get<IHttpRequestTimeoutFeature>()?.RequestTimeoutToken
        ?? CancellationToken.None;
    return await ExecuteQueryAsync(
        request.Sql, request.Params, connectionString, logger, ctx.RequestAborted, requestTimeout);
}).WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));

app.MapCustomerExtraction(connectionString);
app.MapPriceListExtraction(connectionString);
app.MapArticleExtraction(connectionString);
app.MapWarehouseExtraction(connectionString);
app.MapPriceExtraction(connectionString);
app.MapWarehouseStockExtraction(connectionString);

logger.LogInformation("WinBridgeApi starting on http://localhost:5000");

app.Run();

static async Task<IResult> ExecuteQueryAsync(
    string sql,
    Dictionary<string, JsonElement>? parameters,
    string connectionString,
    ILogger logger,
    CancellationToken requestAborted,
    CancellationToken requestTimeout)
{
    try
    {
        logger.LogInformation("Executing SQL query ({Length} characters).", sql.Length);

        var normalizedParameters = new List<KeyValuePair<string, object>>();
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Query parameter names cannot be empty.");
                }

                var paramName = key.StartsWith('@') ? key : $"@{key}";
                normalizedParameters.Add(new(paramName, ConvertJsonParameter(value)));
            }
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(requestAborted);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        foreach (var (name, value) in normalizedParameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await using var reader = await command.ExecuteReaderAsync(requestAborted);

        // Non-query statements (UPDATE/INSERT/DELETE) have no result columns.
        if (reader.FieldCount == 0)
        {
            var rowsAffected = reader.RecordsAffected;
            logger.LogInformation("Query affected {RowsAffected} row(s).", rowsAffected);
            return Results.Ok(new { rowsAffected });
        }

        var columnNames = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        var results = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(requestAborted))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[columnNames[i]] = value is DBNull ? null : value;
            }
            results.Add(row);
        }

        logger.LogInformation("Query returned {Count} row(s).", results.Count);

        return Results.Ok(results);
    }
    catch (OperationCanceledException) when (requestTimeout.IsCancellationRequested)
    {
        logger.LogError("Request timed out after {Timeout}s.", RequestTimeoutSeconds);
        return CreateTimeoutResult();
    }
    catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
    {
        logger.LogWarning("Request was aborted by the client.");
        return Results.StatusCode(499); // non-standard "client closed request" code
    }
    catch (OperationCanceledException)
    {
        logger.LogError("Query operation was canceled.");
        return CreateTimeoutResult();
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Invalid query parameter.");
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (SqlException ex) when (ex.Number == -2)
    {
        logger.LogError(ex, "SQL command timed out after {Timeout}s.", CommandTimeoutSeconds);
        return CreateTimeoutResult();
    }
    catch (SqlException ex)
    {
        logger.LogError(ex, "SQL error executing query.");
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error executing query.");
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
}

static IResult CreateTimeoutResult()
{
    return Results.Json(
        new { error = $"Request timed out after {RequestTimeoutSeconds} seconds." },
        statusCode: StatusCodes.Status504GatewayTimeout);
}

static object ConvertJsonParameter(JsonElement value)
{
    return value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => DBNull.Value,
        JsonValueKind.String => value.GetString() is { } stringValue ? stringValue : DBNull.Value,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
        JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
        JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
        JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
        JsonValueKind.Number => throw new ArgumentException("Numeric parameter is outside the supported range."),
        _ => throw new ArgumentException(
            "Query parameters must be scalar JSON values: string, number, boolean, or null.")
    };
}

record QueryRequest(string Sql, Dictionary<string, JsonElement>? Params);
