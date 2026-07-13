using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Data.SqlClient;

namespace WinBridgeApi.Extraction;

public static class PriceExtraction
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;
    private const int CommandTimeoutSeconds = 25;
    private const int RequestTimeoutSeconds = 30;

    private const string Sql = """
        WITH provided_query AS
        (
            SELECT CDG_PROD AS cod_articulo,
                   CDG_LPRC AS cod_lista,
                   PRE_DOL AS pre_dol,
                   PRE_SOL AS pre_sol,
                   min_dol,
                   min_sol,
                   MAX_DOL AS max_dol,
                   MAX_SOL AS max_sol,
                   POR_DCT1 AS por_dct1,
                   POR_DCT2 AS por_dct2,
                   POR_DCT3 AS por_dct3,
                   NULL AS updated_at
            FROM M_PRECIO
        ),
        normalized AS
        (
            SELECT CONVERT(nvarchar(4000), cod_articulo) AS article_code,
                   CONVERT(nvarchar(4000), cod_lista) AS price_list_code,
                   pre_dol,
                   pre_sol,
                   min_dol,
                   min_sol,
                   max_dol,
                   max_sol,
                   por_dct1,
                   por_dct2,
                   por_dct3,
                   COUNT_BIG(*) OVER
                       (PARTITION BY
                           CONVERT(nvarchar(4000), cod_articulo),
                           CONVERT(nvarchar(4000), cod_lista)) AS key_count
            FROM provided_query
        )
        SELECT TOP (@take)
               article_code,
               price_list_code,
               pre_dol,
               pre_sol,
               min_dol,
               min_sol,
               max_dol,
               max_sol,
               por_dct1,
               por_dct2,
               por_dct3,
               key_count
        FROM normalized
        WHERE @afterArticle IS NULL
           OR article_code > @afterArticle
           OR (article_code = @afterArticle AND price_list_code > @afterList)
        ORDER BY article_code, price_list_code;
        """;

    public static IEndpointConventionBuilder MapPriceExtraction(
        this WebApplication app,
        string connectionString)
    {
        return app.MapGet(
                "/api/v1/extract/prices",
                async (int? limit, string? cursor, string? updatedSince, HttpContext context) =>
                {
                    if (!string.IsNullOrWhiteSpace(updatedSince))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "incremental_not_supported",
                                message = "Price extraction currently supports full snapshots only.",
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

                    if (!TryDecodeCursor(cursor, out var afterArticle, out var afterList))
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
                            afterList,
                            cancellation.Token);
                        var hasMore = items.Count > effectiveLimit;
                        if (hasMore)
                        {
                            items.RemoveAt(items.Count - 1);
                        }

                        var nextCursor = hasMore ? EncodeCursor(items[^1]) : null;
                        return Results.Ok(new ExtractionPage<PriceDto>(
                            items,
                            nextCursor,
                            DateTimeOffset.UtcNow.ToString("O")));
                    }
                    catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
                    {
                        app.Logger.LogError("Price extraction timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Price extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        app.Logger.LogWarning("Price extraction request was aborted.");
                        return Results.StatusCode(499);
                    }
                    catch (SqlException exception) when (exception.Number == -2)
                    {
                        app.Logger.LogError(exception, "Price SQL command timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Price extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (SqlException exception)
                    {
                        app.Logger.LogError(exception, "Price extraction dependency failed.");
                        return ErrorResult(
                            StatusCodes.Status503ServiceUnavailable,
                            "dependency_unavailable",
                            "The data source is temporarily unavailable.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (InvalidOperationException exception)
                    {
                        app.Logger.LogError(exception, "Price source contract validation failed.");
                        return ErrorResult(
                            StatusCodes.Status500InternalServerError,
                            "source_contract_violation",
                            "Price data does not satisfy the extraction contract.",
                            false,
                            context.TraceIdentifier);
                    }
                })
            .WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));
    }

    private static bool TryDecodeCursor(
        string? cursor,
        out string? articleCode,
        out string? priceListCode)
    {
        articleCode = null;
        priceListCode = null;
        if (!OpaqueCursor.TryDecode("prices", cursor, out var payload))
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
            priceListCode = values[1];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string EncodeCursor(PriceDto item)
    {
        var payload = JsonSerializer.Serialize(new[] { item.ArticleCode, item.PriceListCode });
        return OpaqueCursor.Encode("prices", payload);
    }

    private static async Task<List<PriceDto>> ReadPageAsync(
        string connectionString,
        int take,
        string? afterArticle,
        string? afterList,
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
        command.Parameters.Add("@afterList", System.Data.SqlDbType.NVarChar, 4000).Value =
            afterList is null ? DBNull.Value : afterList;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<PriceDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var articleCode = RequiredString(reader, 0, "CDG_PROD");
            var priceListCode = RequiredString(reader, 1, "CDG_LPRC");
            if (articleCode.Length > 20
                || priceListCode.Length > 3
                || reader.GetInt64(11) != 1)
            {
                throw new InvalidOperationException(
                    "The price key must be unique and fit the destination code lengths.");
            }
            items.Add(new PriceDto(
                articleCode,
                priceListCode,
                RequiredDecimal(reader, 2, "PRE_DOL"),
                RequiredDecimal(reader, 3, "PRE_SOL"),
                OptionalDecimal(reader, 4),
                OptionalDecimal(reader, 5),
                OptionalDecimal(reader, 6),
                OptionalDecimal(reader, 7),
                OptionalDecimal(reader, 8),
                OptionalDecimal(reader, 9),
                OptionalDecimal(reader, 10),
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
            throw new InvalidOperationException($"{sourceField} is required by the price contract.");
        }
        return value;
    }

    private static decimal RequiredDecimal(SqlDataReader reader, int ordinal, string sourceField)
    {
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"{sourceField} is required by the price contract.");
        }
        try
        {
            return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException($"{sourceField} must be decimal.", exception);
        }
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
            throw new InvalidOperationException("Optional price value must be decimal.", exception);
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

public sealed record PriceDto(
    string ArticleCode,
    string PriceListCode,
    decimal PriceUsd,
    decimal PricePen,
    decimal? MinimumUsd,
    decimal? MinimumPen,
    decimal? MaximumUsd,
    decimal? MaximumPen,
    decimal? Discount1,
    decimal? Discount2,
    decimal? Discount3,
    DateTimeOffset? SourceUpdatedAt);
