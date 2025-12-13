namespace Verbex
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// Represents a filter for searching documents by their custom metadata properties and labels.
    /// Multiple filters are combined with AND logic (all filters must match).
    /// Label matching is case-insensitive. Tag (custom metadata) matching is exact.
    /// </summary>
    public class MetadataFilter
    {
        private readonly Dictionary<string, object> _Filters;
        private readonly HashSet<string> _Labels;

        /// <summary>
        /// Initializes a new instance of the MetadataFilter class with no filters.
        /// </summary>
        public MetadataFilter()
        {
            _Filters = new Dictionary<string, object>();
            _Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the MetadataFilter class with a single key-value filter.
        /// </summary>
        /// <param name="key">The metadata key to filter on</param>
        /// <param name="value">The value to match</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or whitespace</exception>
        public MetadataFilter(string key, object value)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty or whitespace", nameof(key));
            }

            _Filters = new Dictionary<string, object>
            {
                { key, value }
            };
            _Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the MetadataFilter class with multiple filters.
        /// </summary>
        /// <param name="filters">Dictionary of metadata key-value pairs to filter on</param>
        /// <exception cref="ArgumentNullException">Thrown when filters is null</exception>
        public MetadataFilter(Dictionary<string, object> filters)
        {
            ArgumentNullException.ThrowIfNull(filters);
            _Filters = new Dictionary<string, object>(filters);
            _Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the MetadataFilter class with labels only.
        /// </summary>
        /// <param name="labels">Collection of labels to filter on (case-insensitive)</param>
        /// <exception cref="ArgumentNullException">Thrown when labels is null</exception>
        public MetadataFilter(IEnumerable<string> labels)
        {
            ArgumentNullException.ThrowIfNull(labels);
            _Filters = new Dictionary<string, object>();
            _Labels = new HashSet<string>(labels.Where(l => !string.IsNullOrWhiteSpace(l)), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the MetadataFilter class with both tags and labels.
        /// </summary>
        /// <param name="filters">Dictionary of metadata key-value pairs to filter on</param>
        /// <param name="labels">Collection of labels to filter on (case-insensitive)</param>
        /// <exception cref="ArgumentNullException">Thrown when filters or labels is null</exception>
        public MetadataFilter(Dictionary<string, object>? filters, IEnumerable<string>? labels)
        {
            _Filters = filters != null ? new Dictionary<string, object>(filters) : new Dictionary<string, object>();
            _Labels = labels != null
                ? new HashSet<string>(labels.Where(l => !string.IsNullOrWhiteSpace(l)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the count of tag filters in this MetadataFilter.
        /// </summary>
        public int Count
        {
            get { return _Filters.Count; }
        }

        /// <summary>
        /// Gets the count of label filters in this MetadataFilter.
        /// </summary>
        public int LabelCount
        {
            get { return _Labels.Count; }
        }

        /// <summary>
        /// Gets the total count of all filters (tags + labels) in this MetadataFilter.
        /// </summary>
        public int TotalCount
        {
            get { return _Filters.Count + _Labels.Count; }
        }

        /// <summary>
        /// Gets whether this MetadataFilter has no filters (will match all documents).
        /// Returns true only if both tag filters and label filters are empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return _Filters.Count == 0 && _Labels.Count == 0; }
        }

        /// <summary>
        /// Gets a read-only dictionary of the tag filter key-value pairs.
        /// </summary>
        public IReadOnlyDictionary<string, object> Filters
        {
            get { return _Filters; }
        }

        /// <summary>
        /// Gets a read-only collection of the label filters.
        /// Label matching is case-insensitive.
        /// </summary>
        public IReadOnlyCollection<string> Labels
        {
            get { return _Labels; }
        }

        /// <summary>
        /// Adds a filter for the specified key and value.
        /// If a filter for the key already exists, it will be replaced.
        /// </summary>
        /// <param name="key">The metadata key to filter on</param>
        /// <param name="value">The value to match</param>
        /// <returns>This MetadataFilter instance for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or whitespace</exception>
        public MetadataFilter Add(string key, object value)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty or whitespace", nameof(key));
            }

            _Filters[key] = value;
            return this;
        }

        /// <summary>
        /// Removes the filter for the specified key.
        /// </summary>
        /// <param name="key">The metadata key to remove</param>
        /// <returns>True if the filter was removed, false if the key was not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        public bool Remove(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return _Filters.Remove(key);
        }

        /// <summary>
        /// Clears all tag filters from this MetadataFilter.
        /// Does not affect label filters.
        /// </summary>
        public void Clear()
        {
            _Filters.Clear();
        }

        /// <summary>
        /// Clears all filters (both tags and labels) from this MetadataFilter.
        /// </summary>
        public void ClearAll()
        {
            _Filters.Clear();
            _Labels.Clear();
        }

        /// <summary>
        /// Adds a label to the filter. Label matching is case-insensitive.
        /// </summary>
        /// <param name="label">The label to filter on</param>
        /// <returns>This MetadataFilter instance for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when label is null</exception>
        /// <exception cref="ArgumentException">Thrown when label is empty or whitespace</exception>
        public MetadataFilter AddLabel(string label)
        {
            ArgumentNullException.ThrowIfNull(label);

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Label cannot be empty or whitespace", nameof(label));
            }

            _Labels.Add(label.Trim());
            return this;
        }

        /// <summary>
        /// Adds multiple labels to the filter. Label matching is case-insensitive.
        /// Empty or whitespace labels are ignored.
        /// </summary>
        /// <param name="labels">The labels to filter on</param>
        /// <returns>This MetadataFilter instance for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when labels is null</exception>
        public MetadataFilter AddLabels(IEnumerable<string> labels)
        {
            ArgumentNullException.ThrowIfNull(labels);

            foreach (string label in labels)
            {
                if (!string.IsNullOrWhiteSpace(label))
                {
                    _Labels.Add(label.Trim());
                }
            }

            return this;
        }

        /// <summary>
        /// Removes a label from the filter.
        /// </summary>
        /// <param name="label">The label to remove</param>
        /// <returns>True if the label was removed, false if not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when label is null</exception>
        public bool RemoveLabel(string label)
        {
            ArgumentNullException.ThrowIfNull(label);
            return _Labels.Remove(label);
        }

        /// <summary>
        /// Clears all label filters from this MetadataFilter.
        /// Does not affect tag filters.
        /// </summary>
        public void ClearLabels()
        {
            _Labels.Clear();
        }

        /// <summary>
        /// Checks whether this filter contains the specified label.
        /// Comparison is case-insensitive.
        /// </summary>
        /// <param name="label">The label to check for</param>
        /// <returns>True if the filter contains the label, false otherwise</returns>
        public bool HasLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            return _Labels.Contains(label);
        }

        /// <summary>
        /// Checks whether the specified document metadata matches all filters.
        /// All filter conditions must be satisfied (AND logic).
        /// Both tag filters and label filters must match.
        /// </summary>
        /// <param name="metadata">The document metadata to check</param>
        /// <returns>True if all filters match or if there are no filters, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when metadata is null</exception>
        public bool Matches(DocumentMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            if (_Filters.Count == 0 && _Labels.Count == 0)
            {
                return true;
            }

            foreach (string label in _Labels)
            {
                if (!metadata.HasLabel(label))
                {
                    return false;
                }
            }

            foreach (KeyValuePair<string, object> filter in _Filters)
            {
                if (!metadata.CustomMetadata.TryGetValue(filter.Key, out object? metadataValue))
                {
                    return false;
                }

                if (!ValuesMatch(metadataValue, filter.Value))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two values for equality, handling JsonElement values from JSON deserialization.
        /// </summary>
        private static bool ValuesMatch(object? metadataValue, object? filterValue)
        {
            if (metadataValue == null && filterValue == null)
            {
                return true;
            }

            if (metadataValue == null || filterValue == null)
            {
                return false;
            }

            // Handle JsonElement from JSON deserialization
            if (filterValue is JsonElement filterElement)
            {
                filterValue = GetValueFromJsonElement(filterElement);
            }

            if (metadataValue is JsonElement metadataElement)
            {
                metadataValue = GetValueFromJsonElement(metadataElement);
            }

            // Compare string values case-sensitively
            if (metadataValue is string metadataStr && filterValue is string filterStr)
            {
                return string.Equals(metadataStr, filterStr, StringComparison.Ordinal);
            }

            return object.Equals(metadataValue, filterValue);
        }

        /// <summary>
        /// Extracts the actual value from a JsonElement.
        /// </summary>
        private static object? GetValueFromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long longValue))
                    {
                        return longValue;
                    }
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.ToString();
            }
        }

        /// <summary>
        /// Creates a new MetadataFilter with the specified key-value pair.
        /// Convenience factory method for creating single-filter instances.
        /// </summary>
        /// <param name="key">The metadata key to filter on</param>
        /// <param name="value">The value to match</param>
        /// <returns>A new MetadataFilter instance</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or whitespace</exception>
        public static MetadataFilter Create(string key, object value)
        {
            return new MetadataFilter(key, value);
        }

        /// <summary>
        /// Creates a new MetadataFilter from the specified dictionary.
        /// Convenience factory method for creating multi-filter instances.
        /// </summary>
        /// <param name="filters">Dictionary of metadata key-value pairs to filter on</param>
        /// <returns>A new MetadataFilter instance</returns>
        /// <exception cref="ArgumentNullException">Thrown when filters is null</exception>
        public static MetadataFilter Create(Dictionary<string, object> filters)
        {
            return new MetadataFilter(filters);
        }

        /// <summary>
        /// Creates a new MetadataFilter with the specified labels.
        /// Convenience factory method for creating label-only filter instances.
        /// </summary>
        /// <param name="labels">Collection of labels to filter on (case-insensitive)</param>
        /// <returns>A new MetadataFilter instance</returns>
        /// <exception cref="ArgumentNullException">Thrown when labels is null</exception>
        public static MetadataFilter CreateWithLabels(IEnumerable<string> labels)
        {
            return new MetadataFilter(labels);
        }

        /// <summary>
        /// Creates a new MetadataFilter with both tags and labels.
        /// Convenience factory method for creating combined filter instances.
        /// </summary>
        /// <param name="filters">Dictionary of metadata key-value pairs to filter on, or null</param>
        /// <param name="labels">Collection of labels to filter on (case-insensitive), or null</param>
        /// <returns>A new MetadataFilter instance</returns>
        public static MetadataFilter Create(Dictionary<string, object>? filters, IEnumerable<string>? labels)
        {
            return new MetadataFilter(filters, labels);
        }
    }
}
