namespace Verbex.Database
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies the type of database backend to use for storage.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// SQLite database (file-based or in-memory).
        /// </summary>
        Sqlite,

        /// <summary>
        /// PostgreSQL database server.
        /// </summary>
        Postgresql,

        /// <summary>
        /// MySQL database server.
        /// </summary>
        Mysql,

        /// <summary>
        /// Microsoft SQL Server database.
        /// </summary>
        SqlServer
    }
}
