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

            await EnsureSchemaAsync().ConfigureAwait(false);

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

        private async Task EnsureSchemaAsync()
        {
            await _Database.ExecuteQueryAsync(GetCreateTableQuery(), true).ConfigureAwait(false);
            await _Database.ExecuteQueryAsync(GetCreateDetailTableQuery(), true).ConfigureAwait(false);
            await EnsureColumnsAsync("request_history", GetRequestHistoryColumns()).ConfigureAwait(false);
            await EnsureColumnsAsync("request_history_detail", GetRequestHistoryDetailColumns()).ConfigureAwait(false);

            foreach (string indexQuery in GetCreateIndexQueries())
            {
                try
                {
                    await _Database.ExecuteQueryAsync(indexQuery, true).ConfigureAwait(false);
                }
                catch (Exception e) when (CanIgnoreIndexException(e))
                {
                    _Logging.Debug(_Header + "ignoring index creation warning: " + e.Message);
                }
            }
        }

        private string GetCreateTableQuery()
        {
            return _Database.Settings.Type switch
            {
                DatabaseTypeEnum.Postgresql => @"
CREATE TABLE IF NOT EXISTS request_history (
    id VARCHAR(64) PRIMARY KEY,
    tenant_id VARCHAR(64),
    user_id VARCHAR(64),
    credential_id VARCHAR(64),
    principal_type VARCHAR(32),
    principal_name VARCHAR(256),
    request_type VARCHAR(64) NOT NULL,
    http_method VARCHAR(16) NOT NULL,
    route_template TEXT NOT NULL,
    request_url TEXT NOT NULL,
    source_ip VARCHAR(128),
    index_id VARCHAR(64),
    document_id VARCHAR(128),
    status_code INTEGER NOT NULL,
    success BOOLEAN NOT NULL DEFAULT FALSE,
    request_content_type VARCHAR(256),
    response_content_type VARCHAR(256),
    request_size_bytes BIGINT NOT NULL DEFAULT 0,
    response_size_bytes BIGINT NOT NULL DEFAULT 0,
    request_body_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    response_body_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    is_binary_response BOOLEAN NOT NULL DEFAULT FALSE,
    duration_ms DOUBLE PRECISION NOT NULL DEFAULT 0,
    detail_path TEXT NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL,
    last_update_utc TIMESTAMPTZ NOT NULL
);",
                DatabaseTypeEnum.Mysql => @"
CREATE TABLE IF NOT EXISTS request_history (
    id VARCHAR(64) PRIMARY KEY,
    tenant_id VARCHAR(64) NULL,
    user_id VARCHAR(64) NULL,
    credential_id VARCHAR(64) NULL,
    principal_type VARCHAR(32) NULL,
    principal_name VARCHAR(256) NULL,
    request_type VARCHAR(64) NOT NULL,
    http_method VARCHAR(16) NOT NULL,
    route_template TEXT NOT NULL,
    request_url TEXT NOT NULL,
    source_ip VARCHAR(128) NULL,
    index_id VARCHAR(64) NULL,
    document_id VARCHAR(128) NULL,
    status_code INT NOT NULL,
    success BOOLEAN NOT NULL DEFAULT FALSE,
    request_content_type VARCHAR(256) NULL,
    response_content_type VARCHAR(256) NULL,
    request_size_bytes BIGINT NOT NULL DEFAULT 0,
    response_size_bytes BIGINT NOT NULL DEFAULT 0,
    request_body_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    response_body_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    is_binary_response BOOLEAN NOT NULL DEFAULT FALSE,
    duration_ms DOUBLE NOT NULL DEFAULT 0,
    detail_path TEXT NOT NULL,
    created_utc DATETIME(6) NOT NULL,
    last_update_utc DATETIME(6) NOT NULL
);",
                DatabaseTypeEnum.SqlServer => @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='request_history' AND xtype='U')
CREATE TABLE request_history (
    id NVARCHAR(64) PRIMARY KEY,
    tenant_id NVARCHAR(64) NULL,
    user_id NVARCHAR(64) NULL,
    credential_id NVARCHAR(64) NULL,
    principal_type NVARCHAR(32) NULL,
    principal_name NVARCHAR(256) NULL,
    request_type NVARCHAR(64) NOT NULL,
    http_method NVARCHAR(16) NOT NULL,
    route_template NVARCHAR(MAX) NOT NULL,
    request_url NVARCHAR(MAX) NOT NULL,
    source_ip NVARCHAR(128) NULL,
    index_id NVARCHAR(64) NULL,
    document_id NVARCHAR(128) NULL,
    status_code INT NOT NULL,
    success BIT NOT NULL DEFAULT 0,
    request_content_type NVARCHAR(256) NULL,
    response_content_type NVARCHAR(256) NULL,
    request_size_bytes BIGINT NOT NULL DEFAULT 0,
    response_size_bytes BIGINT NOT NULL DEFAULT 0,
    request_body_truncated BIT NOT NULL DEFAULT 0,
    response_body_truncated BIT NOT NULL DEFAULT 0,
    is_binary_response BIT NOT NULL DEFAULT 0,
    duration_ms FLOAT NOT NULL DEFAULT 0,
    detail_path NVARCHAR(MAX) NOT NULL,
    created_utc DATETIME2 NOT NULL,
    last_update_utc DATETIME2 NOT NULL
);",
                _ => @"
CREATE TABLE IF NOT EXISTS request_history (
    id TEXT PRIMARY KEY,
    tenant_id TEXT,
    user_id TEXT,
    credential_id TEXT,
    principal_type TEXT,
    principal_name TEXT,
    request_type TEXT NOT NULL,
    http_method TEXT NOT NULL,
    route_template TEXT NOT NULL,
    request_url TEXT NOT NULL,
    source_ip TEXT,
    index_id TEXT,
    document_id TEXT,
    status_code INTEGER NOT NULL,
    success INTEGER NOT NULL DEFAULT 0,
    request_content_type TEXT,
    response_content_type TEXT,
    request_size_bytes INTEGER NOT NULL DEFAULT 0,
    response_size_bytes INTEGER NOT NULL DEFAULT 0,
    request_body_truncated INTEGER NOT NULL DEFAULT 0,
    response_body_truncated INTEGER NOT NULL DEFAULT 0,
    is_binary_response INTEGER NOT NULL DEFAULT 0,
    duration_ms REAL NOT NULL DEFAULT 0,
    detail_path TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);"
            };
        }

        private string GetCreateDetailTableQuery()
        {
            return _Database.Settings.Type switch
            {
                DatabaseTypeEnum.Postgresql => @"
CREATE TABLE IF NOT EXISTS request_history_detail (
    id VARCHAR(64) PRIMARY KEY,
    route_parameters TEXT,
    query_parameters TEXT,
    request_headers TEXT,
    request_body TEXT,
    request_body_bytes BIGINT,
    request_body_truncated BOOLEAN,
    response_headers TEXT,
    response_body TEXT,
    response_body_bytes BIGINT,
    response_body_truncated BOOLEAN,
    response_filename VARCHAR(512),
    notes TEXT,
    created_utc TIMESTAMPTZ NOT NULL,
    last_update_utc TIMESTAMPTZ NOT NULL
);",
                DatabaseTypeEnum.Mysql => @"
CREATE TABLE IF NOT EXISTS request_history_detail (
    id VARCHAR(64) PRIMARY KEY,
    route_parameters TEXT NULL,
    query_parameters TEXT NULL,
    request_headers TEXT NULL,
    request_body LONGTEXT NULL,
    request_body_bytes BIGINT NULL,
    request_body_truncated BOOLEAN NULL,
    response_headers TEXT NULL,
    response_body LONGTEXT NULL,
    response_body_bytes BIGINT NULL,
    response_body_truncated BOOLEAN NULL,
    response_filename VARCHAR(512) NULL,
    notes TEXT NULL,
    created_utc DATETIME(6) NOT NULL,
    last_update_utc DATETIME(6) NOT NULL
);",
                DatabaseTypeEnum.SqlServer => @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='request_history_detail' AND xtype='U')
CREATE TABLE request_history_detail (
    id NVARCHAR(64) PRIMARY KEY,
    route_parameters NVARCHAR(MAX) NULL,
    query_parameters NVARCHAR(MAX) NULL,
    request_headers NVARCHAR(MAX) NULL,
    request_body NVARCHAR(MAX) NULL,
    request_body_bytes BIGINT NULL,
    request_body_truncated BIT NULL,
    response_headers NVARCHAR(MAX) NULL,
    response_body NVARCHAR(MAX) NULL,
    response_body_bytes BIGINT NULL,
    response_body_truncated BIT NULL,
    response_filename NVARCHAR(512) NULL,
    notes NVARCHAR(MAX) NULL,
    created_utc DATETIME2 NOT NULL,
    last_update_utc DATETIME2 NOT NULL
);",
                _ => @"
CREATE TABLE IF NOT EXISTS request_history_detail (
    id TEXT PRIMARY KEY,
    route_parameters TEXT,
    query_parameters TEXT,
    request_headers TEXT,
    request_body TEXT,
    request_body_bytes INTEGER,
    request_body_truncated INTEGER,
    response_headers TEXT,
    response_body TEXT,
    response_body_bytes INTEGER,
    response_body_truncated INTEGER,
    response_filename TEXT,
    notes TEXT,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);"
            };
        }

        private List<string> GetCreateIndexQueries()
        {
            if (_Database.Settings.Type == DatabaseTypeEnum.SqlServer)
            {
                return new List<string>
                {
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_created_utc ON request_history(created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_tenant_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_tenant_created_utc ON request_history(tenant_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_user_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_user_created_utc ON request_history(user_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_credential_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_credential_created_utc ON request_history(credential_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_index_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_index_created_utc ON request_history(index_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_method_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_method_created_utc ON request_history(http_method, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_status_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_status_created_utc ON request_history(status_code, created_utc);"
                };
            }

            if (_Database.Settings.Type == DatabaseTypeEnum.Mysql)
            {
                return new List<string>
                {
                    "CREATE INDEX idx_request_history_created_utc ON request_history(created_utc);",
                    "CREATE INDEX idx_request_history_tenant_created_utc ON request_history(tenant_id, created_utc);",
                    "CREATE INDEX idx_request_history_user_created_utc ON request_history(user_id, created_utc);",
                    "CREATE INDEX idx_request_history_credential_created_utc ON request_history(credential_id, created_utc);",
                    "CREATE INDEX idx_request_history_index_created_utc ON request_history(index_id, created_utc);",
                    "CREATE INDEX idx_request_history_method_created_utc ON request_history(http_method, created_utc);",
                    "CREATE INDEX idx_request_history_status_created_utc ON request_history(status_code, created_utc);"
                };
            }

            return new List<string>
            {
                "CREATE INDEX IF NOT EXISTS idx_request_history_created_utc ON request_history(created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_tenant_created_utc ON request_history(tenant_id, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_user_created_utc ON request_history(user_id, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_credential_created_utc ON request_history(credential_id, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_index_created_utc ON request_history(index_id, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_method_created_utc ON request_history(http_method, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_status_created_utc ON request_history(status_code, created_utc);"
            };
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

        private async Task EnsureColumnsAsync(string tableName, List<RequestHistoryColumnDefinition> columns)
        {
            HashSet<string> existingColumns = await GetExistingColumnsAsync(tableName).ConfigureAwait(false);

            foreach (RequestHistoryColumnDefinition column in columns)
            {
                if (existingColumns.Contains(column.Name)) continue;

                try
                {
                    await _Database.ExecuteQueryAsync(GetAddColumnQuery(tableName, column), true).ConfigureAwait(false);
                    existingColumns.Add(column.Name);
                }
                catch (Exception e) when (CanIgnoreColumnMigrationException(e))
                {
                    _Logging.Debug(_Header + "ignoring column migration warning for " + tableName + "." + column.Name + ": " + e.Message);
                    existingColumns.Add(column.Name);
                }
            }
        }

        private async Task<HashSet<string>> GetExistingColumnsAsync(string tableName)
        {
            string schemaName = String.IsNullOrWhiteSpace(_Database.Settings.Schema)
                ? "public"
                : _Database.Settings.Schema;

            string query = _Database.Settings.Type switch
            {
                DatabaseTypeEnum.Postgresql => $"SELECT column_name FROM information_schema.columns WHERE table_schema = '{EscapeSql(schemaName)}' AND table_name = '{EscapeSql(tableName)}';",
                DatabaseTypeEnum.Mysql => $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{EscapeSql(tableName)}';",
                DatabaseTypeEnum.SqlServer => $"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('{EscapeSql(tableName)}');",
                _ => $"SELECT name FROM pragma_table_info('{EscapeSql(tableName)}');"
            };

            DataTable result = await _Database.ExecuteQueryAsync(query, false).ConfigureAwait(false);
            HashSet<string> columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in result.Rows)
            {
                string? columnName = GetColumnName(row);
                if (!String.IsNullOrWhiteSpace(columnName)) columns.Add(columnName);
            }

            return columns;
        }

        private string GetAddColumnQuery(string tableName, RequestHistoryColumnDefinition column)
        {
            return _Database.Settings.Type switch
            {
                DatabaseTypeEnum.Postgresql => $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {column.Name} {column.PostgresqlDefinition};",
                DatabaseTypeEnum.Mysql => $"ALTER TABLE {tableName} ADD COLUMN {column.Name} {column.MysqlDefinition};",
                DatabaseTypeEnum.SqlServer => $"ALTER TABLE {tableName} ADD {column.Name} {column.SqlServerDefinition};",
                _ => $"ALTER TABLE {tableName} ADD COLUMN {column.Name} {column.SqliteDefinition};"
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

        private static string? GetColumnName(DataRow row)
        {
            if (row.Table.Columns.Contains("column_name")) return ToStringValue(row["column_name"]);
            if (row.Table.Columns.Contains("COLUMN_NAME")) return ToStringValue(row["COLUMN_NAME"]);
            if (row.Table.Columns.Contains("name")) return ToStringValue(row["name"]);
            return null;
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

        private List<RequestHistoryColumnDefinition> GetRequestHistoryColumns()
        {
            return new List<RequestHistoryColumnDefinition>
            {
                new RequestHistoryColumnDefinition("tenant_id", "VARCHAR(64)", "VARCHAR(64) NULL", "NVARCHAR(64) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("user_id", "VARCHAR(64)", "VARCHAR(64) NULL", "NVARCHAR(64) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("credential_id", "VARCHAR(64)", "VARCHAR(64) NULL", "NVARCHAR(64) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("principal_type", "VARCHAR(32)", "VARCHAR(32) NULL", "NVARCHAR(32) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("principal_name", "VARCHAR(256)", "VARCHAR(256) NULL", "NVARCHAR(256) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("request_type", "VARCHAR(64)", "VARCHAR(64) NULL", "NVARCHAR(64) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("http_method", "VARCHAR(16)", "VARCHAR(16) NULL", "NVARCHAR(16) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("route_template", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("request_url", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("source_ip", "VARCHAR(128)", "VARCHAR(128) NULL", "NVARCHAR(128) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("index_id", "VARCHAR(64)", "VARCHAR(64) NULL", "NVARCHAR(64) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("document_id", "VARCHAR(128)", "VARCHAR(128) NULL", "NVARCHAR(128) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("status_code", "INTEGER", "INT NULL", "INT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("success", "BOOLEAN", "BOOLEAN NULL", "BIT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("request_content_type", "VARCHAR(256)", "VARCHAR(256) NULL", "NVARCHAR(256) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("response_content_type", "VARCHAR(256)", "VARCHAR(256) NULL", "NVARCHAR(256) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("request_size_bytes", "BIGINT", "BIGINT NULL", "BIGINT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("response_size_bytes", "BIGINT", "BIGINT NULL", "BIGINT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("request_body_truncated", "BOOLEAN", "BOOLEAN NULL", "BIT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("response_body_truncated", "BOOLEAN", "BOOLEAN NULL", "BIT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("is_binary_response", "BOOLEAN", "BOOLEAN NULL", "BIT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("duration_ms", "DOUBLE PRECISION", "DOUBLE NULL", "FLOAT NULL", "REAL"),
                new RequestHistoryColumnDefinition("detail_path", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("created_utc", "TIMESTAMPTZ", "DATETIME(6) NULL", "DATETIME2 NULL", "TEXT"),
                new RequestHistoryColumnDefinition("last_update_utc", "TIMESTAMPTZ", "DATETIME(6) NULL", "DATETIME2 NULL", "TEXT")
            };
        }

        private List<RequestHistoryColumnDefinition> GetRequestHistoryDetailColumns()
        {
            return new List<RequestHistoryColumnDefinition>
            {
                new RequestHistoryColumnDefinition("route_parameters", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("query_parameters", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("request_headers", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("request_body", "TEXT", "LONGTEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("request_body_bytes", "BIGINT", "BIGINT NULL", "BIGINT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("request_body_truncated", "BOOLEAN", "BOOLEAN NULL", "BIT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("response_headers", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("response_body", "TEXT", "LONGTEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("response_body_bytes", "BIGINT", "BIGINT NULL", "BIGINT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("response_body_truncated", "BOOLEAN", "BOOLEAN NULL", "BIT NULL", "INTEGER"),
                new RequestHistoryColumnDefinition("response_filename", "VARCHAR(512)", "VARCHAR(512) NULL", "NVARCHAR(512) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("notes", "TEXT", "TEXT NULL", "NVARCHAR(MAX) NULL", "TEXT"),
                new RequestHistoryColumnDefinition("created_utc", "TIMESTAMPTZ", "DATETIME(6) NULL", "DATETIME2 NULL", "TEXT"),
                new RequestHistoryColumnDefinition("last_update_utc", "TIMESTAMPTZ", "DATETIME(6) NULL", "DATETIME2 NULL", "TEXT")
            };
        }

        private bool CanIgnoreIndexException(Exception e)
        {
            string message = e.Message ?? String.Empty;
            return message.Contains("Duplicate key name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("There is already an object named", StringComparison.OrdinalIgnoreCase)
                || message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanIgnoreColumnMigrationException(Exception e)
        {
            string message = e.Message ?? String.Empty;
            return message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("column names in each table must be unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
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

        private sealed class RequestHistoryColumnDefinition
        {
            public string Name { get; }
            public string PostgresqlDefinition { get; }
            public string MysqlDefinition { get; }
            public string SqlServerDefinition { get; }
            public string SqliteDefinition { get; }

            public RequestHistoryColumnDefinition(string name, string postgresqlDefinition, string mysqlDefinition, string sqlServerDefinition, string sqliteDefinition)
            {
                Name = name;
                PostgresqlDefinition = postgresqlDefinition;
                MysqlDefinition = mysqlDefinition;
                SqlServerDefinition = sqlServerDefinition;
                SqliteDefinition = sqliteDefinition;
            }
        }
    }
}
