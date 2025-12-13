namespace Verbex.Repositories
{
    /// <summary>
    /// In-memory SQLite index repository.
    /// Data is stored in memory and lost when the repository is closed unless explicitly flushed to disk.
    /// </summary>
    public class MemoryIndexRepository : SqliteIndexRepository
    {
        /// <summary>
        /// Creates a new in-memory index repository.
        /// </summary>
        public MemoryIndexRepository() : base(null)
        {
        }
    }
}
