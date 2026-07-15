using System.Globalization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Data.SqlClient;

namespace WinBridgeApi.Extraction;

public static class CustomerExtraction
{
    private static readonly TimeSpan LimaUtcOffset = TimeSpan.FromHours(-5);
    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;
    private const int CommandTimeoutSeconds = 25;
    private const int RequestTimeoutSeconds = 30;

    private const string Sql = """
        WITH provided_query AS
        (
            SELECT ruc_cli AS cod_cliente,
                   LTRIM(RTRIM(des_cli)) AS nombre,
                   LTRIM(RTRIM(des_cli)) AS razon_social,
                   cdg_alt AS ruc,
                   CONVERT(bit, 1) AS estado,
                   ISNULL(ing_cli, DATETIMEFROMPARTS(2000, 1, 1, 8, 0, 0, 0)) AS created_at,
                   NULL AS updated_at,
                   EMA_CLI AS email,
                   TEL_CLI AS telefono,
                   FAX_CLI AS celular,
                   REP_CLI AS representante,
                   CDG_VEND AS cod_vendedor_asig
            FROM m_client
            WHERE ISNULL(cdg_alt, '') <> ''
        ),
        normalized AS
        (
            SELECT CONVERT(nvarchar(4000), cod_cliente) AS customer_code,
                   nombre,
                   razon_social,
                   CONVERT(nvarchar(4000), ruc) AS tax_id,
                   estado,
                   created_at,
                   email,
                   telefono,
                   celular,
                   representante,
                   cod_vendedor_asig,
                   COUNT_BIG(*) OVER
                       (PARTITION BY CONVERT(nvarchar(4000), cod_cliente)) AS key_count,
                   COUNT_BIG(*) OVER
                       (PARTITION BY CONVERT(nvarchar(4000), ruc)) AS tax_id_count
            FROM provided_query
        )
        SELECT TOP (@take)
               customer_code,
               nombre,
               razon_social,
               tax_id,
               estado,
               created_at,
               email,
               telefono,
               celular,
               representante,
               cod_vendedor_asig,
               key_count
        FROM normalized
        WHERE key_count = 1
          AND tax_id_count = 1
          AND (@afterCode IS NULL OR customer_code > @afterCode)
        ORDER BY customer_code;
        """;

    public static IEndpointConventionBuilder MapCustomerExtraction(
        this WebApplication app,
        string connectionString)
    {
        return app.MapGet(
                "/api/v1/extract/customers",
                async (int? limit, string? cursor, string? updatedSince, HttpContext context) =>
                {
                    if (!string.IsNullOrWhiteSpace(updatedSince))
                    {
                        return Results.BadRequest(new
                        {
                            error = new
                            {
                                code = "incremental_not_supported",
                                message = "Customer extraction currently supports full snapshots only.",
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

                    if (!OpaqueCursor.TryDecode("customers", cursor, out var afterCode))
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
                            ? OpaqueCursor.Encode("customers", items[^1].CustomerCode)
                            : null;
                        return Results.Ok(new ExtractionPage<CustomerDto>(
                            items,
                            nextCursor,
                            DateTimeOffset.UtcNow.ToString("O")));
                    }
                    catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
                    {
                        app.Logger.LogError("Customer extraction timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Customer extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        app.Logger.LogWarning("Customer extraction request was aborted.");
                        return Results.StatusCode(499);
                    }
                    catch (SqlException exception) when (exception.Number == -2)
                    {
                        app.Logger.LogError(exception, "Customer SQL command timed out.");
                        return ErrorResult(
                            StatusCodes.Status504GatewayTimeout,
                            "timeout",
                            "Customer extraction timed out.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (SqlException exception)
                    {
                        app.Logger.LogError(exception, "Customer extraction dependency failed.");
                        return ErrorResult(
                            StatusCodes.Status503ServiceUnavailable,
                            "dependency_unavailable",
                            "The data source is temporarily unavailable.",
                            true,
                            context.TraceIdentifier);
                    }
                    catch (InvalidOperationException exception)
                    {
                        app.Logger.LogError(exception, "Customer source contract validation failed.");
                        return ErrorResult(
                            StatusCodes.Status500InternalServerError,
                            "source_contract_violation",
                            "Customer data does not satisfy the extraction contract.",
                            false,
                            context.TraceIdentifier);
                    }
                })
            .WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds));
    }

    private static async Task<List<CustomerDto>> ReadPageAsync(
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
        var items = new List<CustomerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var customerCode = RequiredString(reader, 0, "ruc_cli");
            if (customerCode.Length > 50 || reader.GetInt64(11) != 1)
            {
                throw new InvalidOperationException(
                    "ruc_cli must be unique, non-empty, and no longer than 50 characters.");
            }

            items.Add(new CustomerDto(
                customerCode,
                RequiredString(reader, 1, "des_cli"),
                RequiredString(reader, 2, "des_cli"),
                RequiredString(reader, 3, "cdg_alt"),
                reader.GetBoolean(4),
                OptionalString(reader, 6),
                OptionalString(reader, 7),
                OptionalString(reader, 8),
                OptionalString(reader, 9),
                OptionalString(reader, 10),
                RequiredLimaTimestamp(reader, 5, "ing_cli"),
                null));
        }
        return items;
    }

    private static string RequiredString(SqlDataReader reader, int ordinal, string sourceField)
    {
        var value = OptionalString(reader, ordinal)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"{sourceField} is required by the customer contract.");
        }
        return value;
    }

    private static string? OptionalString(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim();
    }

    private static DateTimeOffset RequiredLimaTimestamp(
        SqlDataReader reader,
        int ordinal,
        string sourceField)
    {
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"{sourceField} is required by the customer contract.");
        }

        var value = reader.GetValue(ordinal);
        var timestamp = value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(
                DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified),
                LimaUtcOffset),
            _ => throw new InvalidOperationException(
                $"{sourceField} must be a SQL date/time value.")
        };
        return timestamp.ToUniversalTime();
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

public sealed record CustomerDto(
    string CustomerCode,
    string Name,
    string LegalName,
    string TaxId,
    bool Active,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Representative,
    string? AssignedSellerCode,
    DateTimeOffset SourceCreatedAt,
    DateTimeOffset? SourceUpdatedAt);
