namespace Verbex.Server.Classes
{
    /// <summary>
    /// Specifies the ordering for enumeration results.
    /// </summary>
    public enum EnumerationOrderEnum
    {
        /// <summary>
        /// Order by creation timestamp ascending (oldest first).
        /// </summary>
        CreatedAscending = 0,

        /// <summary>
        /// Order by creation timestamp descending (newest first).
        /// </summary>
        CreatedDescending = 1
    }
}
