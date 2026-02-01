namespace Verbex.Sdk.DTO.Responses
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of a restore operation.
    /// </summary>
    public class RestoreResult
    {
        /// <summary>
        /// Gets or sets whether the restore was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the restored index identifier.
        /// </summary>
        [JsonPropertyName("indexId")]
        public string IndexId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the result message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any warnings from the restore operation.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
