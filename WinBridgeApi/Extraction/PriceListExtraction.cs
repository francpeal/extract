using System.Globalization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Data.SqlClient;

namespace WinBridgeApi.Extraction;

public static class PriceListExtraction
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;
    private const int CommandTimeoutSeconds = 25;
    private const int RequestTimeoutSeconds = 30;

    private const string Sql = """
        WITH provided_query AS
        (
            SELECT NUM_ITEM AS codigo,
                   LTRIM(RTRIM(des_item)) AS nombre,
                   'true' AS activo,
                   NULL AS updated_at
            FROM D_TABLAS
            WHERE CDG_TAB = 'PRC'
        ),
        normalized AS
        (
            SELECT CONVERT(nvarchar(4000), codigo) AS price_list_code,
                   nombre,
                   CONVERT(bit, 1) AS active,
                   COUNT_BIG(*) OVER
                       (PARTITION BY CONVERT(nvarchar(4000), codigo)) AS key_count
            FROM provided_query
        )
        SELECT TOP (@take)
               price_list_code,
               nombre,
               active,
               key_count
        FROM normalized
        WHERE @afterCode IS NULL OR price_list_code > @afterCode
        ORDER BY price_list_code;
        """;

    public static IEndpointConventionBuilder MapPriceListExtraction(
        this WebApplication app,
        string connectionString)
    {
        return app.MapGet(
                "/api/v1/extract/price-lists",
                async (int? limit, string? cursor, string? updatedSince, HttpContext context) =>
                {
                    if (!string.IsNullOrWhiteSpace(updatedSince))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "incremental_not_supported",
                                message = "Price-list extraction currently supports full snapshots only.",
                                retryable = false,
                                requestId = context.TraceIdentifier
                            }
                        });
                    }

                    var effectiveLimit = limit ?? DefaultLimit;
                    if (effectiveLimit is < 1 or > MaxLimit)
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "invalid_limit",
                                message = $"limit must be between 1 and {MaxLimit}.",
                                retryable = false,
                                requestId = context.TraceIdentifier
                            }
                        });
                    }

                    if (!OpaqueCursor.TryDecode("price-lists", cursor, out var afterCode))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "invalid_cursor",
                                message = "cursor is invalid.",
                                retryable = false,
                                requestId = context.TraceIdentifier
                            }
                        });
                    }

                    var timeoutToken = context.Features
                        .Get<IHttpRequestTimeoutFeature>()?.RequestTimeoutToken
                        ?? CancellationToken.None;
                    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        context.RequestAborted,
                        timeoutToken);

                    try
                    {
                        var items = await ReadPageAsync(
                            connectionString,
                            effectiveLimit + 1,
                            afterCode,
                            cancellation.Token);
                        var hasMore = items.Count > effectiveLimit;
                        if (hasMore)
                        {
                            items.RemoveAt(items.Count - 1);
                        }

                        var nextCursor = hasMore
                            ? OpaqueCursor.Encode("price-lists", items[^1].PriceListCode)
                            : null;
                        return Results.Ok(new ExtractionPage<PriceListDto>(
                            items,
                            nextCursor,
                            DateTimeOffset.UtcNow.ToString("O")));
                    }
                    catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
                    {
                        app.Logger.LogError("Price-list extraction timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Price-list extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        app.Logger.LogWarning("Price-list extraction request was aborted.");
                        return Results.StatusCode(499);
                    }
                    catch (SqlException exception) when (exception.Number == -2)
                    {
                        app.Logger.LogError(exception, "Price-list SQL command timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Price-list extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (SqlException exception)
                    {
                        app.Logger.LogError(exception, "Price-list extraction dependency failed.");
                        return ErrorResult(
                            StatusCodes.Status503ServiceUnavailable,
                            "dependency_unavailable",
                            "The data source is temporarily unavailable.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (InvalidOperationException exception)
                    {
                        app.Logger.LogError(exception, "Price-list source contract validation failed.");
                        return ErrorResult(
                            StatusCodes.Status500InternalServerError,
                            "source_contract_violation",
                            "Price-list data does not satisfy the extraction contract.",
                            false,
                            context.TraceIdentifier);
                    }
                })
            .WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));
    }

    private static async Task<List<PriceListDto>> ReadPageAsync(
        string connectionString,
        int take,
        string? afterCode,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(Sql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        command.Parameters.Add("@take", System.Data.SqlDbType.Int).Value = take;
        command.Parameters.Add("@afterCode", System.Data.SqlDbType.NVarChar, 4000).Value =
            afterCode is null ? DBNull.Value : afterCode;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<PriceListDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = RequiredString(reader, 0, "NUM_ITEM");
            if (code.Length > 3 || reader.GetInt64(3) != 1)
            {
                throw new InvalidOperationException(
                    "NUM_ITEM must be unique, non-empty, and no longer than 3 characters.");
            }
            items.Add(new PriceListDto(
                code,
                RequiredString(reader, 1, "des_item"),
                reader.GetBoolean(2),
                null));
        }
        return items;
    }

    private static string RequiredString(SqlDataReader reader, int ordinal, string sourceField)
    {
        var value = reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"{sourceField} is required by the price-list contract.");
        }
        return value;
    }

    private static IResult ErrorResult(
        int status,
        string code,
        string message,
        bool retryable,
        string requestId)
    {
        return Results.Json(
            new { error = new { code, message, retryable, requestId } },
            statusCode: status);
    }
}

public sealed record PriceListDto(
    string PriceListCode,
    string Name,
    bool Active,
    DateTimeOffset? SourceUpdatedAt);
