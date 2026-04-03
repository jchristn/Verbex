namespace Verbex.Server.Services
{
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database;
    using Verbex.Server.Classes;

    public class RequestHistoryService : IDisposable
    {
        private readonly RequestHistorySettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private readonly string _Header = "[RequestHistoryService] ";
        private Timer? _CleanupTimer;
        private bool _Initialized = false;

        public bool Enabled => _Initialized && _Settings.Enabled;

        public RequestHistoryService(RequestHistorySettings settings, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        public async Task InitializeAsync()
        {
            if (!_Settings.Enabled)
            {
                _Initialized = false;
                return;
            }

            _CleanupTimer = new Timer(
                async _ => await SafeCleanupAsync().ConfigureAwait(false),
                null,
                TimeSpan.FromMinutes(_Settings.CleanupIntervalMinutes),
                TimeSpan.FromMinutes(_Settings.CleanupIntervalMinutes));

            _Initialized = true;
        }

        public async Task RecordAsync(RequestHistoryEntry entry, RequestHistoryDetail detail, CancellationToken token = default)
        {
            if (!Enabled) return;
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (detail == null) throw new ArgumentNullException(nameof(detail));

            entry.CreatedUtc = entry.CreatedUtc == default ? DateTime.UtcNow : entry.CreatedUtc.ToUniversalTime();
            entry.LastUpdateUtc = entry.CreatedUtc;
            detail.Id = entry.Id;
            await _Database.ExecuteQueriesAsync(
                new[]
                {
                    BuildInsertQuery(entry),
                    BuildInsertDetailQuery(entry, detail)
                },
                true,
                token).ConfigureAwait(false);
        }

        public async Task<(List<RequestHistoryEntry> Entries, long TotalCount)> SearchAsync(RequestHistoryQuery query, CancellationToken token = default)
        {
            if (!Enabled) return (new List<RequestHistoryEntry>(), 0);

            query ??= new RequestHistoryQuery();
            string whereClause = BuildWhereClause(query);

            DataTable countTable = await _Database.ExecuteQueryAsync(
                $"SELECT COUNT(*) FROM request_history{whereClause};",
                false,
                token).ConfigureAwait(false);

            long totalCount = countTable.Rows.Count > 0 ? ToInt64(countTable.Rows[0][0]) : 0;

            string searchQuery = $@"
SELECT id, tenant_id, user_id, credential_id, principal_type, principal_name, request_type, http_method, route_template,
       request_url, source_ip, index_id, document_id, status_code, success, request_content_type, response_content_type,
       request_size_bytes, response_size_bytes, request_body_truncated, response_body_truncated, is_binary_response,
       duration_ms, created_utc, last_update_utc
FROM request_history
{whereClause}
ORDER BY created_utc DESC
{BuildPaginationClause(query.PageSize, query.Skip)};";

            DataTable result = await _Database.ExecuteQueryAsync(searchQuery, false, token).ConfigureAwait(false);
            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            foreach (DataRow row in result.Rows) entries.Add(MapEntry(row));
            return (entries, totalCount);
        }

        public async Task<RequestHistoryEntry?> GetEntryAsync(string id, CancellationToken token = default)
        {
            if (!Enabled || String.IsNullOrWhiteSpace(id)) return null;

            string query = _Database.Settings.Type == DatabaseTypeEnum.SqlServer
                ? $@"
SELECT TOP 1 id, tenant_id, user_id, credential_id, principal_type, principal_name, request_type, http_method, route_template,
       request_url, source_ip, index_id, document_id, status_code, success, request_content_type, response_content_type,
       request_size_bytes, response_size_bytes, request_body_truncated, response_body_truncated, is_binary_response,
       duration_ms, created_utc, last_update_utc
FROM request_history
WHERE id = '{EscapeSql(id)}';"
                : $@"
SELECT id, tenant_id, user_id, credential_id, principal_type, principal_name, request_type, http_method, route_template,
       request_url, source_ip, index_id, document_id, status_code, success, request_content_type, response_content_type,
       request_size_bytes, response_size_bytes, request_body_truncated, response_body_truncated, is_binary_response,
       duration_ms, created_utc, last_update_utc
FROM request_history
WHERE id = '{EscapeSql(id)}'
LIMIT 1;";

            DataTable result = await _Database.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count < 1 ? null : MapEntry(result.Rows[0]);
        }

        public async Task<RequestHistoryDetail?> GetDetailAsync(string id, CancellationToken token = default)
        {
            if (!Enabled || String.IsNullOrWhiteSpace(id)) return null;

            string query = _Database.Settings.Type == DatabaseTypeEnum.SqlServer
                ? $@"
SELECT TOP 1 id, route_parameters, query_parameters, request_headers, request_body, request_body_bytes,
       request_body_truncated, response_headers, response_body, response_body_bytes, response_body_truncated,
       response_filename, notes
FROM request_history_detail
WHERE id = '{EscapeSql(id)}';"
                : $@"
SELECT id, route_parameters, query_parameters, request_headers, request_body, request_body_bytes,
       request_body_truncated, response_headers, response_body, response_body_bytes, response_body_truncated,
       response_filename, notes
FROM request_history_detail
WHERE id = '{EscapeSql(id)}'
LIMIT 1;";

            DataTable result = await _Database.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result.Rows.Count < 1 ? null : MapDetail(result.Rows[0]);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            RequestHistoryEntry? entry = await GetEntryAsync(id, token).ConfigureAwait(false);
            if (entry == null) return false;

            await _Database.ExecuteQueriesAsync(
                new[]
                {
                    $"DELETE FROM request_history_detail WHERE id = '{EscapeSql(id)}';",
                    $"DELETE FROM request_history WHERE id = '{EscapeSql(id)}';"
                },
                true,
                token).ConfigureAwait(false);
            return true;
        }

        public async Task<RequestHistoryBulkDeleteResult> BulkDeleteAsync(RequestHistoryQuery query, CancellationToken token = default)
        {
            if (!Enabled) return new RequestHistoryBulkDeleteResult();

            query ??= new RequestHistoryQuery();
            string whereClause = BuildWhereClause(query);
            DataTable result = await _Database.ExecuteQueryAsync(
                $"SELECT id FROM request_history{whereClause};",
                false,
                token).ConfigureAwait(false);

            RequestHistoryBulkDeleteResult deleteResult = new RequestHistoryBulkDeleteResult();
            foreach (DataRow row in result.Rows)
            {
                string id = ToStringValue(row["id"]) ?? String.Empty;
                if (!String.IsNullOrEmpty(id)) deleteResult.DeletedIds.Add(id);
            }

            if (deleteResult.DeletedIds.Count > 0)
            {
                List<string> queries = new List<string>();
                queries.AddRange(BuildDeleteDetailQueries(deleteResult.DeletedIds));
                queries.Add($"DELETE FROM request_history{whereClause};");
                await _Database.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
            }

            deleteResult.DeletedCount = deleteResult.DeletedIds.Count;
            return deleteResult;
        }

        public async Task<RequestHistorySummary> GetSummaryAsync(RequestHistoryQuery query, CancellationToken token = default)
        {
            RequestHistorySummary summary = new RequestHistorySummary();
            if (!Enabled) return summary;

            query ??= new RequestHistoryQuery();
            query.FromUtc ??= DateTime.UtcNow.AddHours(-24);
            query.ToUtc ??= DateTime.UtcNow;

            string whereClause = BuildWhereClause(query);
            DataTable result = await _Database.ExecuteQueryAsync($@"
SELECT created_utc, success, duration_ms
FROM request_history
{whereClause}
ORDER BY created_utc ASC;", false, token).ConfigureAwait(false);

            Dictionary<DateTime, RequestHistorySummaryBucket> buckets = new Dictionary<DateTime, RequestHistorySummaryBucket>();
            double durationTotal = 0;

            foreach (DataRow row in result.Rows)
            {
                DateTime createdUtc = ToDateTime(row["created_utc"]);
                DateTime bucketStart = FloorToBucket(createdUtc, query.BucketMinutes);

                if (!buckets.TryGetValue(bucketStart, out RequestHistorySummaryBucket? bucket))
                {
                    bucket = new RequestHistorySummaryBucket
                    {
                        BucketStartUtc = bucketStart,
                        BucketEndUtc = bucketStart.AddMinutes(query.BucketMinutes)
                    };
                    buckets[bucketStart] = bucket;
                }

                bool success = ToBoolean(row["success"]);
                double duration = ToDouble(row["duration_ms"]);

                bucket.TotalCount++;
                bucket.AverageDurationMs += duration;
                summary.TotalCount++;
                durationTotal += duration;

                if (success)
                {
                    bucket.SuccessCount++;
                    summary.SuccessCount++;
                }
                else
                {
                    bucket.FailureCount++;
                    summary.FailureCount++;
                }
            }

            if (summary.TotalCount > 0)
            {
                summary.AverageDurationMs = durationTotal / summary.TotalCount;
            }

            if (query.FromUtc.HasValue && query.ToUtc.HasValue)
            {
                DateTime cursor = FloorToBucket(query.FromUtc.Value.ToUniversalTime(), query.BucketMinutes);
                DateTime maxBucket = FloorToBucket(query.ToUtc.Value.ToUniversalTime(), query.BucketMinutes);

                while (cursor <= maxBucket)
                {
                    if (!buckets.ContainsKey(cursor))
                    {
                        buckets[cursor] = new RequestHistorySummaryBucket
                        {
                            BucketStartUtc = cursor,
                            BucketEndUtc = cursor.AddMinutes(query.BucketMinutes)
                        };
                    }

                    cursor = cursor.AddMinutes(query.BucketMinutes);
                }
            }

            summary.Buckets = buckets.Values
                .OrderBy(x => x.BucketStartUtc)
                .Select(x =>
                {
                    if (x.TotalCount > 0) x.AverageDurationMs /= x.TotalCount;
                    return x;
                })
                .ToList();

            return summary;
        }

        public async Task CleanupExpiredAsync(CancellationToken token = default)
        {
            if (!Enabled) return;

            string cutoff = FormatDateTimeValue(DateTime.UtcNow.AddDays(-_Settings.RetentionDays));
            DataTable result = await _Database.ExecuteQueryAsync(
                $"SELECT id FROM request_history WHERE created_utc < '{EscapeSql(cutoff)}';",
                false,
                token).ConfigureAwait(false);

            List<string> expiredIds = new List<string>();
            foreach (DataRow row in result.Rows)
            {
                string id = ToStringValue(row["id"]) ?? String.Empty;
                if (!String.IsNullOrWhiteSpace(id)) expiredIds.Add(id);
            }

            if (expiredIds.Count > 0)
            {
                List<string> queries = new List<string>();
                queries.AddRange(BuildDeleteDetailQueries(expiredIds));
                queries.Add($"DELETE FROM request_history WHERE created_utc < '{EscapeSql(cutoff)}';");
                await _Database.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _CleanupTimer?.Dispose();
        }

        private string BuildInsertQuery(RequestHistoryEntry entry)
        {
            return $@"
INSERT INTO request_history (
    id, tenant_id, user_id, credential_id, principal_type, principal_name, request_type, http_method, route_template,
    request_url, source_ip, index_id, document_id, status_code, success, request_content_type, response_content_type,
    request_size_bytes, response_size_bytes, request_body_truncated, response_body_truncated, is_binary_response,
    duration_ms, detail_path, created_utc, last_update_utc
) VALUES (
    '{EscapeSql(entry.Id)}',
    {NullableSql(entry.TenantId)},
    {NullableSql(entry.UserId)},
    {NullableSql(entry.CredentialId)},
    {NullableSql(entry.PrincipalType)},
    {NullableSql(entry.PrincipalName)},
    '{EscapeSql(entry.RequestType)}',
    '{EscapeSql(entry.HttpMethod)}',
    '{EscapeSql(entry.RouteTemplate)}',
    '{EscapeSql(entry.RequestUrl)}',
    {NullableSql(entry.SourceIp)},
    {NullableSql(entry.IndexId)},
    {NullableSql(entry.DocumentId)},
    {entry.StatusCode},
    {BooleanSql(entry.Success)},
    {NullableSql(entry.RequestContentType)},
    {NullableSql(entry.ResponseContentType)},
    {entry.RequestSizeBytes},
    {entry.ResponseSizeBytes},
    {BooleanSql(entry.RequestBodyTruncated)},
    {BooleanSql(entry.ResponseBodyTruncated)},
    {BooleanSql(entry.IsBinaryResponse)},
    {entry.DurationMs.ToString(CultureInfo.InvariantCulture)},
    '',
    '{EscapeSql(FormatDateTimeValue(entry.CreatedUtc))}',
    '{EscapeSql(FormatDateTimeValue(entry.LastUpdateUtc))}'
);";
        }

        private string BuildInsertDetailQuery(RequestHistoryEntry entry, RequestHistoryDetail detail)
        {
            return $@"
INSERT INTO request_history_detail (
    id, route_parameters, query_parameters, request_headers, request_body, request_body_bytes, request_body_truncated,
    response_headers, response_body, response_body_bytes, response_body_truncated, response_filename, notes,
    created_utc, last_update_utc
) VALUES (
    '{EscapeSql(detail.Id)}',
    {NullableSql(SerializeDictionary(detail.RouteParameters))},
    {NullableSql(SerializeDictionary(detail.QueryParameters))},
    {NullableSql(SerializeDictionary(detail.RequestHeaders))},
    {NullableSql(detail.RequestBody)},
    {detail.RequestBodyBytes},
    {BooleanSql(detail.RequestBodyTruncated)},
    {NullableSql(SerializeDictionary(detail.ResponseHeaders))},
    {NullableSql(detail.ResponseBody)},
    {detail.ResponseBodyBytes},
    {BooleanSql(detail.ResponseBodyTruncated)},
    {NullableSql(detail.ResponseFilename)},
    {NullableSql(detail.Notes)},
    '{EscapeSql(FormatDateTimeValue(entry.CreatedUtc))}',
    '{EscapeSql(FormatDateTimeValue(entry.LastUpdateUtc))}'
);";
        }

        private string BuildWhereClause(RequestHistoryQuery query)
        {
            List<string> clauses = new List<string>();

            if (!String.IsNullOrWhiteSpace(query.TenantId)) clauses.Add($"tenant_id = '{EscapeSql(query.TenantId)}'");
            if (!String.IsNullOrWhiteSpace(query.UserId)) clauses.Add($"user_id = '{EscapeSql(query.UserId)}'");
            if (!String.IsNullOrWhiteSpace(query.CredentialId)) clauses.Add($"credential_id = '{EscapeSql(query.CredentialId)}'");
            if (!String.IsNullOrWhiteSpace(query.IndexId)) clauses.Add($"index_id = '{EscapeSql(query.IndexId)}'");
            if (!String.IsNullOrWhiteSpace(query.HttpMethod)) clauses.Add($"http_method = '{EscapeSql(query.HttpMethod.ToUpperInvariant())}'");

            if (!String.IsNullOrWhiteSpace(query.Route))
            {
                string route = EscapeSql(query.Route.ToLowerInvariant());
                clauses.Add($"LOWER(route_template) LIKE '%{route}%'");
            }

            if (!String.IsNullOrWhiteSpace(query.SourceIp))
            {
                string sourceIp = EscapeSql(query.SourceIp.ToLowerInvariant());
                clauses.Add($"LOWER(source_ip) LIKE '%{sourceIp}%'");
            }

            if (!String.IsNullOrWhiteSpace(query.Principal))
            {
                string principal = EscapeSql(query.Principal.ToLowerInvariant());
                clauses.Add($"LOWER(principal_name) LIKE '%{principal}%'");
            }

            if (query.StatusCode.HasValue) clauses.Add($"status_code = {query.StatusCode.Value}");
            if (query.FromUtc.HasValue) clauses.Add($"created_utc >= '{EscapeSql(FormatDateTimeValue(query.FromUtc.Value))}'");
            if (query.ToUtc.HasValue) clauses.Add($"created_utc <= '{EscapeSql(FormatDateTimeValue(query.ToUtc.Value))}'");

            return clauses.Count > 0 ? " WHERE " + String.Join(" AND ", clauses) : String.Empty;
        }

        private string BuildPaginationClause(int limit, int offset)
        {
            return _Database.Settings.Type == DatabaseTypeEnum.SqlServer
                ? $"OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY"
                : $"LIMIT {limit} OFFSET {offset}";
        }

        private RequestHistoryEntry MapEntry(DataRow row)
        {
            return new RequestHistoryEntry
            {
                Id = ToStringValue(row["id"]) ?? String.Empty,
                TenantId = ToStringValue(row["tenant_id"]),
                UserId = ToStringValue(row["user_id"]),
                CredentialId = ToStringValue(row["credential_id"]),
                PrincipalType = ToStringValue(row["principal_type"]),
                PrincipalName = ToStringValue(row["principal_name"]),
                RequestType = ToStringValue(row["request_type"]) ?? RequestTypeEnum.Unknown.ToString(),
                HttpMethod = ToStringValue(row["http_method"]) ?? "GET",
                RouteTemplate = ToStringValue(row["route_template"]) ?? "/",
                RequestUrl = ToStringValue(row["request_url"]) ?? "/",
                SourceIp = ToStringValue(row["source_ip"]),
                IndexId = ToStringValue(row["index_id"]),
                DocumentId = ToStringValue(row["document_id"]),
                StatusCode = ToInt32(row["status_code"]),
                Success = ToBoolean(row["success"]),
                RequestContentType = ToStringValue(row["request_content_type"]),
                ResponseContentType = ToStringValue(row["response_content_type"]),
                RequestSizeBytes = ToInt64(row["request_size_bytes"]),
                ResponseSizeBytes = ToInt64(row["response_size_bytes"]),
                RequestBodyTruncated = ToBoolean(row["request_body_truncated"]),
                ResponseBodyTruncated = ToBoolean(row["response_body_truncated"]),
                IsBinaryResponse = ToBoolean(row["is_binary_response"]),
                DurationMs = ToDouble(row["duration_ms"]),
                CreatedUtc = ToDateTime(row["created_utc"]),
                LastUpdateUtc = ToDateTime(row["last_update_utc"])
            };
        }

        private RequestHistoryDetail MapDetail(DataRow row)
        {
            return new RequestHistoryDetail
            {
                Id = ToStringValue(row["id"]) ?? String.Empty,
                RouteParameters = DeserializeDictionary(ToStringValue(row["route_parameters"])),
                QueryParameters = DeserializeDictionary(ToStringValue(row["query_parameters"])),
                RequestHeaders = DeserializeDictionary(ToStringValue(row["request_headers"])),
                RequestBody = ToStringValue(row["request_body"]),
                RequestBodyBytes = ToInt64(row["request_body_bytes"]),
                RequestBodyTruncated = ToBoolean(row["request_body_truncated"]),
                ResponseHeaders = DeserializeDictionary(ToStringValue(row["response_headers"])),
                ResponseBody = ToStringValue(row["response_body"]),
                ResponseBodyBytes = ToInt64(row["response_body_bytes"]),
                ResponseBodyTruncated = ToBoolean(row["response_body_truncated"]),
                ResponseFilename = ToStringValue(row["response_filename"]),
                Notes = ToStringValue(row["notes"])
            };
        }

        private List<string> BuildDeleteDetailQueries(List<string> ids)
        {
            List<string> queries = new List<string>();

            for (int i = 0; i < ids.Count; i += 250)
            {
                List<string> batch = ids
                    .Skip(i)
                    .Take(250)
                    .Where(id => !String.IsNullOrWhiteSpace(id))
                    .Select(id => $"'{EscapeSql(id)}'")
                    .ToList();

                if (batch.Count > 0)
                {
                    queries.Add($"DELETE FROM request_history_detail WHERE id IN ({String.Join(", ", batch)});");
                }
            }

            return queries;
        }

        private string? SerializeDictionary(Dictionary<string, string>? values)
        {
            if (values == null) return null;
            return JsonSerializer.Serialize(values, _JsonOptions);
        }

        private Dictionary<string, string> DeserializeDictionary(string? json)
        {
            if (String.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _JsonOptions)
                    ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private static string EscapeSql(string value) => value.Replace("'", "''");

        private static string NullableSql(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? "NULL" : $"'{EscapeSql(value)}'";
        }

        private string BooleanSql(bool value)
        {
            return _Database.Settings.Type == DatabaseTypeEnum.Postgresql ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");
        }

        private string FormatDateTimeValue(DateTime value)
        {
            DateTime utc = value.ToUniversalTime();

            return _Database.Settings.Type switch
            {
                DatabaseTypeEnum.Mysql => utc.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
                DatabaseTypeEnum.SqlServer => utc.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                _ => utc.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private static string? ToStringValue(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int ToInt32(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static long ToInt64(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static bool ToBoolean(object value)
        {
            if (value == null || value == DBNull.Value) return false;
            if (value is bool boolValue) return boolValue;

            string normalized = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "0";
            return normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime ToDateTime(object value)
        {
            if (value == null || value == DBNull.Value) return DateTime.UtcNow;
            if (value is DateTime dateTime) return dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();

            string raw = Convert.ToString(value, CultureInfo.InvariantCulture) ?? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static DateTime FloorToBucket(DateTime value, int bucketMinutes)
        {
            value = value.ToUniversalTime();
            long bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
            long flooredTicks = value.Ticks - (value.Ticks % bucketTicks);
            return new DateTime(flooredTicks, DateTimeKind.Utc);
        }

        private async Task SafeCleanupAsync()
        {
            try
            {
                await CleanupExpiredAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "cleanup failed: " + e.Message);
            }
        }
    }
}
