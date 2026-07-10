using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: basic console logs with timestamps ---
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.SingleLine = true;
});

// --- Kestrel: HTTP only, fixed address/port. No HTTPS, no HSTS. ---
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // http://0.0.0.0:5000
});

var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException("Connection string 'SqlServer' is not configured in appsettings.json.");

var logger = app.Logger;

const int CommandTimeoutSeconds = 25; // leaves headroom under the 30s request budget
const int RequestTimeoutSeconds = 30;

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

    return await ExecuteQueryAsync(sql, null, connectionString, logger, ctx.RequestAborted);
});

app.MapPost("/query", async (QueryRequest? request, HttpContext ctx) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Sql))
    {
        return Results.BadRequest(new { error = "Missing 'sql' field in request body." });
    }

    return await ExecuteQueryAsync(request.Sql, request.Params, connectionString, logger, ctx.RequestAborted);
});

logger.LogInformation("WinBridgeApi starting on http://0.0.0.0:5000");

app.Run();

static async Task<IResult> ExecuteQueryAsync(
    string sql,
    Dictionary<string, object?>? parameters,
    string connectionString,
    ILogger logger,
    CancellationToken requestAborted)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    timeoutCts.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

    try
    {
        logger.LogInformation("Executing query: {Sql}", sql);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(timeoutCts.Token);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                var paramName = key.StartsWith('@') ? key : $"@{key}";
                command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
            }
        }

        await using var reader = await command.ExecuteReaderAsync(timeoutCts.Token);

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

        while (await reader.ReadAsync(timeoutCts.Token))
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
    catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
    {
        logger.LogWarning("Request was aborted by the client.");
        return Results.StatusCode(499); // non-standard "client closed request" code
    }
    catch (OperationCanceledException)
    {
        logger.LogError("Query timed out after {Timeout}s.", RequestTimeoutSeconds);
        return Results.Json(
            new { error = $"Query timed out after {RequestTimeoutSeconds} seconds." },
            statusCode: StatusCodes.Status504GatewayTimeout);
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

record QueryRequest(string Sql, Dictionary<string, object?>? Params);
