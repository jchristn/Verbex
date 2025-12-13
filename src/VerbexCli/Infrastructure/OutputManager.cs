namespace VerbexCli.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

    /// <summary>
    /// Manages output formatting and colored console output for the CLI
    /// </summary>
    public static class OutputManager
    {
        private static bool _ColorEnabled = true;
        private static bool _VerboseEnabled = false;
        private static bool _QuietEnabled = false;
        private static OutputFormat _DefaultFormat = OutputFormat.Table;

        /// <summary>
        /// Gets or sets whether colored output is enabled
        /// </summary>
        public static bool ColorEnabled
        {
            get => _ColorEnabled && !Console.IsOutputRedirected;
            set => _ColorEnabled = value;
        }

        /// <summary>
        /// Gets or sets whether verbose output is enabled
        /// </summary>
        public static bool VerboseEnabled
        {
            get => _VerboseEnabled;
            set => _VerboseEnabled = value;
        }

        /// <summary>
        /// Gets or sets whether quiet output is enabled
        /// </summary>
        public static bool QuietEnabled
        {
            get => _QuietEnabled;
            set => _QuietEnabled = value;
        }

        /// <summary>
        /// Gets or sets the default output format
        /// </summary>
        public static OutputFormat DefaultFormat
        {
            get => _DefaultFormat;
            set => _DefaultFormat = value;
        }

        /// <summary>
        /// Writes a success message in green
        /// </summary>
        /// <param name="message">Message to write</param>
        public static void WriteSuccess(string message)
        {
            if (QuietEnabled) return;

            if (ColorEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(message);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Writes an error message in red to stderr
        /// </summary>
        /// <param name="message">Error message to write</param>
        public static void WriteError(string message)
        {
            if (ColorEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(message);
                Console.ResetColor();
            }
            else
            {
                Console.Error.WriteLine(message);
            }
        }

        /// <summary>
        /// Writes a warning message in yellow
        /// </summary>
        /// <param name="message">Warning message to write</param>
        public static void WriteWarning(string message)
        {
            if (QuietEnabled) return;

            if (ColorEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(message);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Writes an info message in blue
        /// </summary>
        /// <param name="message">Info message to write</param>
        public static void WriteInfo(string message)
        {
            if (QuietEnabled) return;

            if (ColorEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(message);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Writes a verbose message (only if verbose is enabled)
        /// </summary>
        /// <param name="message">Verbose message to write</param>
        public static void WriteVerbose(string message)
        {
            if (!VerboseEnabled || QuietEnabled) return;

            if (ColorEnabled)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"VERBOSE: {message}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"VERBOSE: {message}");
            }
        }

        /// <summary>
        /// Writes a debug message (only if debug is enabled via environment variable)
        /// </summary>
        /// <param name="message">Debug message to write</param>
        public static void WriteDebug(string message)
        {
            if (Environment.GetEnvironmentVariable("VBX_DEBUG") != "1" || QuietEnabled) return;

            if (ColorEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"DEBUG: {message}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"DEBUG: {message}");
            }
        }

        /// <summary>
        /// Writes a normal message
        /// </summary>
        /// <param name="message">Message to write</param>
        public static void WriteLine(string message)
        {
            if (QuietEnabled) return;
            Console.WriteLine(message);
        }

        /// <summary>
        /// Writes data in the specified format
        /// </summary>
        /// <param name="data">Data to write</param>
        /// <param name="format">Output format</param>
        public static void WriteData(object data, OutputFormat? format = null)
        {
            OutputFormat outputFormat = format ?? DefaultFormat;

            switch (outputFormat)
            {
                case OutputFormat.Json:
                    WriteJson(data);
                    break;
                case OutputFormat.Csv:
                    WriteCsv(data);
                    break;
                case OutputFormat.Yaml:
                    WriteYaml(data);
                    break;
                case OutputFormat.Table:
                default:
                    WriteTable(data);
                    break;
            }
        }

        /// <summary>
        /// Writes data as JSON
        /// </summary>
        /// <param name="data">Data to write</param>
        private static void WriteJson(object data)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(data, options);
            Console.WriteLine(json);
        }

        /// <summary>
        /// Writes data as CSV
        /// </summary>
        /// <param name="data">Data to write</param>
        private static void WriteCsv(object data)
        {
            if (data is IEnumerable<object> collection)
            {
                IEnumerable<object> items = collection.ToList();
                if (!items.Any()) return;

                // Get properties from first item
                object? firstItem = items.FirstOrDefault();
                if (firstItem == null) return;

                Type itemType = firstItem.GetType();
                System.Reflection.PropertyInfo[] properties = itemType.GetProperties();

                // Write header
                Console.WriteLine(string.Join(",", properties.Select(p => QuoteCsvValue(p.Name))));

                // Write data rows
                foreach (object item in items)
                {
                    string[] values = properties.Select(p => QuoteCsvValue(p.GetValue(item)?.ToString() ?? "")).ToArray();
                    Console.WriteLine(string.Join(",", values));
                }
            }
            else
            {
                // Single object - write as key-value pairs
                Type objectType = data.GetType();
                System.Reflection.PropertyInfo[] properties = objectType.GetProperties();

                Console.WriteLine("Property,Value");
                foreach (System.Reflection.PropertyInfo property in properties)
                {
                    string value = property.GetValue(data)?.ToString() ?? "";
                    Console.WriteLine($"{QuoteCsvValue(property.Name)},{QuoteCsvValue(value)}");
                }
            }
        }

        /// <summary>
        /// Writes data as YAML
        /// </summary>
        /// <param name="data">Data to write</param>
        private static void WriteYaml(object data)
        {
            // Simple YAML-like output with depth protection
            WriteYamlObject(data, 0, new HashSet<object>(), 10);
        }

        /// <summary>
        /// Writes data as a formatted table
        /// </summary>
        /// <param name="data">Data to write</param>
        private static void WriteTable(object data)
        {
            if (data is IEnumerable<object> collection)
            {
                WriteTableCollection(collection);
            }
            else
            {
                WriteTableObject(data);
            }
        }

        /// <summary>
        /// Writes a collection as a table
        /// </summary>
        /// <param name="collection">Collection to write</param>
        private static void WriteTableCollection(IEnumerable<object> collection)
        {
            List<object> items = collection.ToList();
            if (!items.Any())
            {
                WriteLine("No data to display.");
                return;
            }

            object? firstItem = items.FirstOrDefault();
            if (firstItem == null) return;

            Type itemType = firstItem.GetType();
            System.Reflection.PropertyInfo[] properties = itemType.GetProperties();

            // Calculate column widths
            Dictionary<string, int> columnWidths = new Dictionary<string, int>();
            foreach (System.Reflection.PropertyInfo property in properties)
            {
                int maxWidth = property.Name.Length;
                foreach (object item in items)
                {
                    string value = property.GetValue(item)?.ToString() ?? "";
                    maxWidth = Math.Max(maxWidth, value.Length);
                }
                columnWidths[property.Name] = Math.Min(maxWidth, 50); // Cap at 50 characters
            }

            // Write header
            StringBuilder headerBuilder = new StringBuilder();
            foreach (System.Reflection.PropertyInfo property in properties)
            {
                string header = property.Name.PadRight(columnWidths[property.Name]);
                headerBuilder.Append(header);
                headerBuilder.Append("  ");
            }
            WriteLine(headerBuilder.ToString().TrimEnd());

            // Write separator
            StringBuilder separatorBuilder = new StringBuilder();
            foreach (System.Reflection.PropertyInfo property in properties)
            {
                separatorBuilder.Append(new string('-', columnWidths[property.Name]));
                separatorBuilder.Append("  ");
            }
            WriteLine(separatorBuilder.ToString().TrimEnd());

            // Write data rows
            foreach (object item in items)
            {
                StringBuilder rowBuilder = new StringBuilder();
                foreach (System.Reflection.PropertyInfo property in properties)
                {
                    string value = property.GetValue(item)?.ToString() ?? "";
                    if (value.Length > columnWidths[property.Name])
                    {
                        value = value.Substring(0, columnWidths[property.Name] - 3) + "...";
                    }
                    value = value.PadRight(columnWidths[property.Name]);
                    rowBuilder.Append(value);
                    rowBuilder.Append("  ");
                }
                WriteLine(rowBuilder.ToString().TrimEnd());
            }
        }

        /// <summary>
        /// Writes a single object as a table
        /// </summary>
        /// <param name="obj">Object to write</param>
        private static void WriteTableObject(object obj)
        {
            Type objectType = obj.GetType();
            System.Reflection.PropertyInfo[] properties = objectType.GetProperties();

            int maxPropertyNameLength = properties.Max(p => p.Name.Length);

            foreach (System.Reflection.PropertyInfo property in properties)
            {
                string name = property.Name.PadRight(maxPropertyNameLength);
                object? propertyValue = property.GetValue(obj);
                string value = FormatPropertyValue(propertyValue);
                WriteLine($"{name}: {value}");
            }
        }

        /// <summary>
        /// Formats a property value for display, handling arrays and complex objects
        /// </summary>
        /// <param name="value">The property value to format</param>
        /// <returns>Formatted string representation</returns>
        private static string FormatPropertyValue(object? value)
        {
            if (value == null)
                return "";

            // Handle arrays and collections
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                List<string> items = new List<string>();
                foreach (object item in enumerable)
                {
                    items.Add(item?.ToString() ?? "");
                    if (items.Count > 10) // Limit to prevent excessive output
                    {
                        items.Add("...");
                        break;
                    }
                }
                return $"[{string.Join(", ", items)}]";
            }

            return value.ToString() ?? "";
        }

        /// <summary>
        /// Recursively writes an object as YAML
        /// </summary>
        /// <param name="obj">Object to write</param>
        /// <param name="indent">Current indentation level</param>
        /// <param name="visited">Set of visited objects to prevent circular references</param>
        /// <param name="maxDepth">Maximum recursion depth</param>
        private static void WriteYamlObject(object obj, int indent, HashSet<object> visited, int maxDepth)
        {
            // Prevent infinite recursion
            if (maxDepth <= 0)
            {
                Console.WriteLine($"{new string(' ', indent)}...");
                return;
            }

            // Check for circular references
            if (obj != null && !obj.GetType().IsPrimitive && !(obj is string) && visited.Contains(obj))
            {
                Console.WriteLine($"{new string(' ', indent)}[circular reference]");
                return;
            }

            string indentString = new string(' ', indent);

            if (obj is IEnumerable<object> collection && !(obj is string))
            {
                List<object> items = collection.ToList();
                if (!items.Any())
                {
                    Console.WriteLine($"{indentString}[]");
                    return;
                }

                HashSet<object> newVisited = new HashSet<object>(visited);
                if (obj != null && !obj.GetType().IsPrimitive && !(obj is string))
                    newVisited.Add(obj);

                foreach (object item in items)
                {
                    if (item == null)
                    {
                        Console.WriteLine($"{indentString}- null");
                    }
                    else if (item is string || item.GetType().IsPrimitive || item.GetType().IsEnum)
                    {
                        string yamlValue = FormatYamlValue(item.ToString() ?? "");
                        Console.WriteLine($"{indentString}- {yamlValue}");
                    }
                    else
                    {
                        Console.WriteLine($"{indentString}-");
                        WriteYamlObject(item, indent + 2, newVisited, maxDepth - 1);
                    }
                }
            }
            else
            {
                Type objectType = obj?.GetType() ?? typeof(object);

                // Skip built-in types that are problematic
                if (IsBuiltInType(objectType))
                {
                    Console.WriteLine($"{indentString}{FormatYamlValue(obj?.ToString() ?? "")}");
                    return;
                }

                HashSet<object> newVisited = new HashSet<object>(visited);
                if (obj != null && !obj.GetType().IsPrimitive && !(obj is string))
                    newVisited.Add(obj);

                System.Reflection.PropertyInfo[] properties = objectType.GetProperties()
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .ToArray();

                foreach (System.Reflection.PropertyInfo property in properties)
                {
                    try
                    {
                        object? value = property.GetValue(obj);
                        if (value == null)
                        {
                            Console.WriteLine($"{indentString}{property.Name}: null");
                        }
                        else if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
                        {
                            string yamlValue = FormatYamlValue(value.ToString() ?? "");
                            Console.WriteLine($"{indentString}{property.Name}: {yamlValue}");
                        }
                        else if (value is IEnumerable<object> nestedCollection && !(value is string))
                        {
                            Console.WriteLine($"{indentString}{property.Name}:");
                            WriteYamlObject(value, indent + 2, newVisited, maxDepth - 1);
                        }
                        else
                        {
                            Console.WriteLine($"{indentString}{property.Name}:");
                            WriteYamlObject(value, indent + 2, newVisited, maxDepth - 1);
                        }
                    }
                    catch
                    {
                        // Skip properties that throw exceptions when accessed
                        Console.WriteLine($"{indentString}{property.Name}: [error accessing property]");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a type is a built-in type that should be treated as a primitive
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <returns>True if built-in type</returns>
        private static bool IsBuiltInType(Type type)
        {
            return type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid) ||
                   type == typeof(decimal) ||
                   type.IsValueType;
        }

        /// <summary>
        /// Formats a value for YAML output, handling special characters and quoting
        /// </summary>
        /// <param name="value">Value to format</param>
        /// <returns>Formatted YAML value</returns>
        private static string FormatYamlValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Quote strings that contain special YAML characters or look like numbers/booleans
            if (value.Contains(":") || value.Contains("#") || value.Contains("'") || value.Contains("\"") ||
                value.Contains("\n") || value.Contains("\r") || value.Contains("\t") ||
                value.StartsWith(" ") || value.EndsWith(" ") ||
                bool.TryParse(value, out _) ||
                double.TryParse(value, out _))
            {
                return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
            }

            return value;
        }

        /// <summary>
        /// Quotes a value for CSV output if needed
        /// </summary>
        /// <param name="value">Value to quote</param>
        /// <returns>Quoted value</returns>
        private static string QuoteCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}