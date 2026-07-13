using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Data.SqlClient;

namespace WinBridgeApi.Extraction;

public static class WarehouseStockExtraction
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;
    private const int CommandTimeoutSeconds = 25;
    private const int RequestTimeoutSeconds = 30;

    private const string Sql = """
        WITH provided_query AS
        (
            SELECT CDG_PROD AS cod_articulo,
                   CDG_AREA AS cod_almacen,
                   STK_INIC AS stock_inicial,
                   STK_ING AS stock_ingresos,
                   STK_SAL AS stock_salidas,
                   STK_ACT AS stock_actual,
                   NULL AS updated_at
            FROM M_STOCK
        ),
        normalized AS
        (
            SELECT CONVERT(nvarchar(4000), cod_articulo) AS article_code,
                   CONVERT(nvarchar(4000), cod_almacen) AS warehouse_code,
                   stock_inicial,
                   stock_ingresos,
                   stock_salidas,
                   stock_actual,
                   COUNT_BIG(*) OVER
                       (PARTITION BY
                           CONVERT(nvarchar(4000), cod_articulo),
                           CONVERT(nvarchar(4000), cod_almacen)) AS key_count
            FROM provided_query
        )
        SELECT TOP (@take)
               article_code,
               warehouse_code,
               stock_inicial,
               stock_ingresos,
               stock_salidas,
               stock_actual,
               key_count
        FROM normalized
        WHERE @afterArticle IS NULL
           OR article_code > @afterArticle
           OR (article_code = @afterArticle AND warehouse_code > @afterWarehouse)
        ORDER BY article_code, warehouse_code;
        """;

    public static IEndpointConventionBuilder MapWarehouseStockExtraction(
        this WebApplication app,
        string connectionString)
    {
        return app.MapGet(
                "/api/v1/extract/warehouse-stock",
                async (int? limit, string? cursor, string? updatedSince, HttpContext context) =>
                {
                    if (!string.IsNullOrWhiteSpace(updatedSince))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "incremental_not_supported",
                                message = "Warehouse stock extraction currently supports full snapshots only.",
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

                    if (!TryDecodeCursor(cursor, out var afterArticle, out var afterWarehouse))
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
                            afterArticle,
                            afterWarehouse,
                            cancellation.Token);
                        var hasMore = items.Count > effectiveLimit;
                        if (hasMore)
                        {
                            items.RemoveAt(items.Count - 1);
                        }

                        var nextCursor = hasMore ? EncodeCursor(items[^1]) : null;
                        return Results.Ok(new ExtractionPage<WarehouseStockDto>(
                            items,
                            nextCursor,
                            DateTimeOffset.UtcNow.ToString("O")));
                    }
                    catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
                    {
                        app.Logger.LogError("Warehouse stock extraction timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Warehouse stock extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        app.Logger.LogWarning("Warehouse stock extraction request was aborted.");
                        return Results.StatusCode(499);
                    }
                    catch (SqlException exception) when (exception.Number == -2)
                    {
                        app.Logger.LogError(exception, "Warehouse stock SQL command timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Warehouse stock extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (SqlException exception)
                    {
                        app.Logger.LogError(exception, "Warehouse stock extraction dependency failed.");
                        return ErrorResult(
                            StatusCodes.Status503ServiceUnavailable,
                            "dependency_unavailable",
                            "The data source is temporarily unavailable.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (InvalidOperationException exception)
                    {
                        app.Logger.LogError(exception, "Warehouse stock source contract validation failed.");
                        return ErrorResult(
                            StatusCodes.Status500InternalServerError,
                            "source_contract_violation",
                            "Warehouse stock data does not satisfy the extraction contract.",
                            false,
                            context.TraceIdentifier);
                    }
                })
            .WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));
    }

    private static bool TryDecodeCursor(
        string? cursor,
        out string? articleCode,
        out string? warehouseCode)
    {
        articleCode = null;
        warehouseCode = null;
        if (!OpaqueCursor.TryDecode("warehouse-stock", cursor, out var payload))
        {
            return false;
        }
        if (payload is null)
        {
            return true;
        }
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(payload);
            if (values is not { Length: 2 }
                || string.IsNullOrWhiteSpace(values[0])
                || string.IsNullOrWhiteSpace(values[1]))
            {
                return false;
            }
            articleCode = values[0];
            warehouseCode = values[1];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string EncodeCursor(WarehouseStockDto item)
    {
        var payload = JsonSerializer.Serialize(new[] { item.ArticleCode, item.WarehouseCode });
        return OpaqueCursor.Encode("warehouse-stock", payload);
    }

    private static async Task<List<WarehouseStockDto>> ReadPageAsync(
        string connectionString,
        int take,
        string? afterArticle,
        string? afterWarehouse,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(Sql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        command.Parameters.Add("@take", System.Data.SqlDbType.Int).Value = take;
        command.Parameters.Add("@afterArticle", System.Data.SqlDbType.NVarChar, 4000).Value =
            afterArticle is null ? DBNull.Value : afterArticle;
        command.Parameters.Add("@afterWarehouse", System.Data.SqlDbType.NVarChar, 4000).Value =
            afterWarehouse is null ? DBNull.Value : afterWarehouse;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<WarehouseStockDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var articleCode = RequiredString(reader, 0, "CDG_PROD");
            var warehouseCode = RequiredString(reader, 1, "CDG_AREA");
            if (articleCode.Length > 20
                || warehouseCode.Length > 10
                || reader.GetInt64(6) != 1)
            {
                throw new InvalidOperationException(
                    "The warehouse stock key must be unique and fit the destination code lengths.");
            }
            items.Add(new WarehouseStockDto(
                articleCode,
                warehouseCode,
                OptionalDecimal(reader, 2),
                OptionalDecimal(reader, 3),
                OptionalDecimal(reader, 4),
                OptionalDecimal(reader, 5),
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
            throw new InvalidOperationException(
                $"{sourceField} is required by the warehouse stock contract.");
        }
        return value;
    }

    private static decimal? OptionalDecimal(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }
        try
        {
            return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException("Warehouse stock value must be decimal.", exception);
        }
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

public sealed record WarehouseStockDto(
    string ArticleCode,
    string WarehouseCode,
    decimal? OpeningStock,
    decimal? IncomingStock,
    decimal? OutgoingStock,
    decimal? CurrentStock,
    DateTimeOffset? SourceUpdatedAt);
