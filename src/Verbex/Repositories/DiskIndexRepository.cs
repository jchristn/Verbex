namespace Verbex.Repositories
{
    using System;
    using System.IO;

    /// <summary>
    /// Disk-based SQLite index repository.
    /// Data is persisted immediately to the specified database file.
    /// </summary>
    public class DiskIndexRepository : SqliteIndexRepository
    {
        /// <summary>
        /// Creates a new disk-based index repository.
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file.</param>
        /// <exception cref="ArgumentNullException">If databasePath is null or empty.</exception>
        public DiskIndexRepository(string databasePath) : base(databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentNullException(nameof(databasePath), "Database path cannot be null or empty.");
            }
        }

        /// <summary>
        /// Creates a new disk-based index repository with automatic directory creation.
        /// </summary>
        /// <param name="directory">Directory to store the database.</param>
        /// <param name="databaseFilename">Database filename (default: index.db).</param>
        /// <returns>New DiskIndexRepository instance.</returns>
        /// <exception cref="ArgumentNullException">If directory is null or empty.</exception>
        public static DiskIndexRepository Create(string directory, string databaseFilename = "index.db")
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentNullException(nameof(directory), "Directory cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(databaseFilename))
            {
                databaseFilename = "index.db";
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string databasePath = Path.Combine(directory, databaseFilename);
            return new DiskIndexRepository(databasePath);
        }

        /// <summary>
        /// Gets the default storage directory for indices.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <returns>Default directory path.</returns>
        public static string GetDefaultStorageDirectory(string indexName)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".vbx", "indices", indexName);
        }
    }
}
