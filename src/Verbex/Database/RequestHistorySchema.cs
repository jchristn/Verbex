namespace Verbex.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    /// <summary>
    /// Shared request history schema definitions used by core database drivers.
    /// </summary>
    internal static class RequestHistorySchema
    {
        internal static readonly IReadOnlyList<RequestHistoryColumnDefinition> RequestHistoryColumns = new List<RequestHistoryColumnDefinition>
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

        internal static readonly IReadOnlyList<RequestHistoryColumnDefinition> RequestHistoryDetailColumns = new List<RequestHistoryColumnDefinition>
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

        internal static string GetCreateTableQuery(DatabaseTypeEnum type)
        {
            return type switch
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

        internal static string GetCreateDetailTableQuery(DatabaseTypeEnum type)
        {
            return type switch
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

        internal static IReadOnlyList<string> GetCreateIndexQueries(DatabaseTypeEnum type)
        {
            if (type == DatabaseTypeEnum.SqlServer)
            {
                return new List<string>
                {
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_created_utc ON request_history(created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_tenant_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_tenant_created_utc ON request_history(tenant_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_user_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_user_created_utc ON request_history(user_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_credential_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_credential_created_utc ON request_history(credential_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_index_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_index_created_utc ON request_history(index_id, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_method_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_method_created_utc ON request_history(http_method, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_status_created_utc' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_status_created_utc ON request_history(status_code, created_utc);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_summary' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_summary ON request_history(created_utc) INCLUDE (success, duration_ms);",
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_tenant_summary' AND object_id = OBJECT_ID('request_history')) CREATE INDEX idx_request_history_tenant_summary ON request_history(tenant_id, created_utc) INCLUDE (success, duration_ms);"
                };
            }

            if (type == DatabaseTypeEnum.Mysql)
            {
                return new List<string>
                {
                    "CREATE INDEX idx_request_history_created_utc ON request_history(created_utc);",
                    "CREATE INDEX idx_request_history_tenant_created_utc ON request_history(tenant_id, created_utc);",
                    "CREATE INDEX idx_request_history_user_created_utc ON request_history(user_id, created_utc);",
                    "CREATE INDEX idx_request_history_credential_created_utc ON request_history(credential_id, created_utc);",
                    "CREATE INDEX idx_request_history_index_created_utc ON request_history(index_id, created_utc);",
                    "CREATE INDEX idx_request_history_method_created_utc ON request_history(http_method, created_utc);",
                    "CREATE INDEX idx_request_history_status_created_utc ON request_history(status_code, created_utc);",
                    "CREATE INDEX idx_request_history_summary ON request_history(created_utc, success, duration_ms);",
                    "CREATE INDEX idx_request_history_tenant_summary ON request_history(tenant_id, created_utc, success, duration_ms);"
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
                "CREATE INDEX IF NOT EXISTS idx_request_history_status_created_utc ON request_history(status_code, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_summary ON request_history(created_utc, success, duration_ms);",
                "CREATE INDEX IF NOT EXISTS idx_request_history_tenant_summary ON request_history(tenant_id, created_utc, success, duration_ms);"
            };
        }

        internal static string GetExistingColumnsQuery(DatabaseSettings settings, string tableName)
        {
            string schemaName = String.IsNullOrWhiteSpace(settings.Schema)
                ? "public"
                : settings.Schema;

            return settings.Type switch
            {
                DatabaseTypeEnum.Postgresql => $"SELECT column_name FROM information_schema.columns WHERE table_schema = '{EscapeSql(schemaName)}' AND table_name = '{EscapeSql(tableName)}';",
                DatabaseTypeEnum.Mysql => $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{EscapeSql(tableName)}';",
                DatabaseTypeEnum.SqlServer => $"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('{EscapeSql(tableName)}');",
                _ => $"SELECT name FROM pragma_table_info('{EscapeSql(tableName)}');"
            };
        }

        internal static string GetAddColumnQuery(DatabaseTypeEnum type, string tableName, RequestHistoryColumnDefinition column)
        {
            return type switch
            {
                DatabaseTypeEnum.Postgresql => $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {column.Name} {column.PostgresqlDefinition};",
                DatabaseTypeEnum.Mysql => $"ALTER TABLE {tableName} ADD COLUMN {column.Name} {column.MysqlDefinition};",
                DatabaseTypeEnum.SqlServer => $"ALTER TABLE {tableName} ADD {column.Name} {column.SqlServerDefinition};",
                _ => $"ALTER TABLE {tableName} ADD COLUMN {column.Name} {column.SqliteDefinition};"
            };
        }

        internal static string? GetColumnName(DataRow row)
        {
            if (row.Table.Columns.Contains("column_name")) return Convert.ToString(row["column_name"]);
            if (row.Table.Columns.Contains("COLUMN_NAME")) return Convert.ToString(row["COLUMN_NAME"]);
            if (row.Table.Columns.Contains("name")) return Convert.ToString(row["name"]);
            return null;
        }

        internal static bool CanIgnoreDuplicateIndexException(Exception e)
        {
            string message = e.Message ?? String.Empty;
            return message.Contains("Duplicate key name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("There is already an object named", StringComparison.OrdinalIgnoreCase)
                || message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool CanIgnoreDuplicateColumnException(Exception e)
        {
            string message = e.Message ?? String.Empty;
            return message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("column names in each table must be unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeSql(string value)
        {
            return value.Replace("'", "''");
        }

        internal sealed class RequestHistoryColumnDefinition
        {
            internal string Name { get; }
            internal string PostgresqlDefinition { get; }
            internal string MysqlDefinition { get; }
            internal string SqlServerDefinition { get; }
            internal string SqliteDefinition { get; }

            internal RequestHistoryColumnDefinition(string name, string postgresqlDefinition, string mysqlDefinition, string sqlServerDefinition, string sqliteDefinition)
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
