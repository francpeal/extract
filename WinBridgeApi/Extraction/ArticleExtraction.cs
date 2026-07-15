using System.Globalization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Data.SqlClient;

namespace WinBridgeApi.Extraction;

public static class ArticleExtraction
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;
    private const int CommandTimeoutSeconds = 25;
    private const int RequestTimeoutSeconds = 30;

    private const string Sql = """
        WITH provided_query AS
        (
            SELECT ArtCod AS codigo,
                   LTRIM(RTRIM(ArtNombre)) AS descripcion,
                   'true' AS activo,
                   NULL AS updated_at,
                   SUBSTRING(
                       ArtLineaDesc,
                       CHARINDEX('-', ArtLineaDesc) + 1,
                       LEN(ArtLineaDesc)) AS marca,
                   ArtCodInt AS cod_alterno
            FROM VW_Articulo
        ),
        normalized AS
        (
            SELECT CONVERT(nvarchar(4000), codigo) AS article_code,
                   descripcion,
                   CONVERT(bit, 1) AS active,
                   marca,
                   CONVERT(nvarchar(4000), cod_alterno) AS alternate_code,
                   COUNT_BIG(*) OVER
                       (PARTITION BY CONVERT(nvarchar(4000), codigo)) AS key_count
            FROM provided_query
        )
        SELECT TOP (@take)
               article_code,
               descripcion,
               active,
               marca,
               alternate_code,
               key_count
        FROM normalized
        WHERE key_count = 1
          AND (@afterCode IS NULL OR article_code > @afterCode)
        ORDER BY article_code;
        """;

    public static IEndpointConventionBuilder MapArticleExtraction(
        this WebApplication app,
        string connectionString)
    {
        return app.MapGet(
                "/api/v1/extract/articles",
                async (int? limit, string? cursor, string? updatedSince, HttpContext context) =>
                {
                    if (!string.IsNullOrWhiteSpace(updatedSince))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "incremental_not_supported",
                                message = "Article extraction currently supports full snapshots only.",
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

                    if (!OpaqueCursor.TryDecode("articles", cursor, out var afterCode))
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
                            ? OpaqueCursor.Encode("articles", items[^1].ArticleCode)
                            : null;
                        return Results.Ok(new ExtractionPage<ArticleDto>(
                            items,
                            nextCursor,
                            DateTimeOffset.UtcNow.ToString("O")));
                    }
                    catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
                    {
                        app.Logger.LogError("Article extraction timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Article extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        app.Logger.LogWarning("Article extraction request was aborted.");
                        return Results.StatusCode(499);
                    }
                    catch (SqlException exception) when (exception.Number == -2)
                    {
                        app.Logger.LogError(exception, "Article SQL command timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Article extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (SqlException exception)
                    {
                        app.Logger.LogError(exception, "Article extraction dependency failed.");
                        return ErrorResult(
                            StatusCodes.Status503ServiceUnavailable,
                            "dependency_unavailable",
                            "The data source is temporarily unavailable.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (InvalidOperationException exception)
                    {
                        app.Logger.LogError(exception, "Article source contract validation failed.");
                        return ErrorResult(
                            StatusCodes.Status500InternalServerError,
                            "source_contract_violation",
                            "Article data does not satisfy the extraction contract.",
                            false,
                            context.TraceIdentifier);
                    }
                })
            .WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));
    }

    private static async Task<List<ArticleDto>> ReadPageAsync(
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
        var items = new List<ArticleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = RequiredString(reader, 0, "ArtCod");
            if (code.Length > 20 || reader.GetInt64(5) != 1)
            {
                throw new InvalidOperationException(
                    "ArtCod must be unique, non-empty, and no longer than 20 characters.");
            }

            var brand = OptionalString(reader, 3, trim: false);
            var alternateCode = OptionalString(reader, 4, trim: false);
            ValidateOptionalLength(brand, 120, "ArtLineaDesc/marca");
            ValidateOptionalLength(alternateCode, 100, "ArtCodInt");
            items.Add(new ArticleDto(
                code,
                OptionalString(reader, 1, trim: false),
                reader.GetBoolean(2),
                brand,
                alternateCode,
                null));
        }
        return items;
    }

    private static string RequiredString(SqlDataReader reader, int ordinal, string sourceField)
    {
        var value = OptionalString(reader, ordinal, trim: true);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"{sourceField} is required by the article contract.");
        }
        return value;
    }

    private static string? OptionalString(SqlDataReader reader, int ordinal, bool trim)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }
        var value = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        return trim ? value?.Trim() : value;
    }

    private static void ValidateOptionalLength(string? value, int maxLength, string sourceField)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"{sourceField} exceeds the destination limit of {maxLength} characters.");
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

public sealed record ArticleDto(
    string ArticleCode,
    string? Description,
    bool Active,
    string? Brand,
    string? AlternateCode,
    DateTimeOffset? SourceUpdatedAt);
