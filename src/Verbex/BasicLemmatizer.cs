namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Basic lemmatizer implementation using simple suffix rules for common English word forms.
    /// This is a simplified lemmatizer suitable for basic text processing needs.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class BasicLemmatizer : ILemmatizer
    {
        private static readonly Dictionary<string, string> _IrregularVerbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "was", "be" }, { "were", "be" }, { "been", "be" }, { "am", "be" }, { "is", "be" }, { "are", "be" },
            { "had", "have" }, { "has", "have" },
            { "did", "do" }, { "does", "do" }, { "done", "do" },
            { "went", "go" }, { "gone", "go" }, { "goes", "go" },
            { "came", "come" }, { "comes", "come" },
            { "saw", "see" }, { "seen", "see" }, { "sees", "see" },
            { "took", "take" }, { "taken", "take" }, { "takes", "take" },
            { "got", "get" }, { "gotten", "get" }, { "gets", "get" },
            { "made", "make" }, { "makes", "make" },
            { "said", "say" }, { "says", "say" },
            { "gave", "give" }, { "given", "give" }, { "gives", "give" },
            { "knew", "know" }, { "known", "know" }, { "knows", "know" },
            { "thought", "think" }, { "thinks", "think" },
            { "found", "find" }, { "finds", "find" },
            { "told", "tell" }, { "tells", "tell" },
            { "left", "leave" }, { "leaves", "leave" },
            { "felt", "feel" }, { "feels", "feel" },
            { "brought", "bring" }, { "brings", "bring" },
            { "bought", "buy" }, { "buys", "buy" },
            { "caught", "catch" }, { "catches", "catch" },
            { "taught", "teach" }, { "teaches", "teach" },
            { "fought", "fight" }, { "fights", "fight" },
            { "sought", "seek" }, { "seeks", "seek" },
            { "ran", "run" }, { "runs", "run" },
            { "won", "win" }, { "wins", "win" },
            { "began", "begin" }, { "begun", "begin" }, { "begins", "begin" },
            { "wrote", "write" }, { "written", "write" }, { "writes", "write" },
            { "read", "read" }, { "reads", "read" }, // read/read are the same
            { "drove", "drive" }, { "driven", "drive" }, { "drives", "drive" },
            { "spoke", "speak" }, { "spoken", "speak" }, { "speaks", "speak" },
            { "broke", "break" }, { "broken", "break" }, { "breaks", "break" },
            { "chose", "choose" }, { "chosen", "choose" }, { "chooses", "choose" },
            { "wore", "wear" }, { "worn", "wear" }, { "wears", "wear" },
            { "drew", "draw" }, { "drawn", "draw" }, { "draws", "draw" },
            { "flew", "fly" }, { "flown", "fly" }, { "flies", "fly" },
            { "grew", "grow" }, { "grown", "grow" }, { "grows", "grow" },
            { "threw", "throw" }, { "thrown", "throw" }, { "throws", "throw" }
        };

        private static readonly Dictionary<string, string> _IrregularNouns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "children", "child" },
            { "feet", "foot" },
            { "teeth", "tooth" },
            { "geese", "goose" },
            { "mice", "mouse" },
            { "men", "man" },
            { "women", "woman" },
            { "people", "person" },
            { "oxen", "ox" },
            { "deer", "deer" },
            { "sheep", "sheep" },
            { "fish", "fish" }
        };

        /// <summary>
        /// Reduces a word to its base form using simple suffix rules and irregular word mappings.
        /// </summary>
        /// <param name="word">The word to lemmatize. Cannot be null or empty.</param>
        /// <returns>The lemmatized form of the word.</returns>
        /// <exception cref="ArgumentNullException">Thrown when word is null.</exception>
        /// <exception cref="ArgumentException">Thrown when word is empty or whitespace.</exception>
        public string Lemmatize(string word)
        {
            ArgumentNullException.ThrowIfNull(word);
            if (string.IsNullOrWhiteSpace(word))
                throw new ArgumentException("Word cannot be empty or whitespace.", nameof(word));

            string trimmedWord = word.Trim().ToLowerInvariant();

            // Check irregular verbs first
            if (_IrregularVerbs.TryGetValue(trimmedWord, out string? irregularVerb))
            {
                return irregularVerb;
            }

            // Check irregular nouns
            if (_IrregularNouns.TryGetValue(trimmedWord, out string? irregularNoun))
            {
                return irregularNoun;
            }

            // Apply suffix rules
            return ApplySuffixRules(trimmedWord);
        }

        private static string ApplySuffixRules(string word)
        {
            if (word.Length <= 2)
                return word;

            // Plural nouns
            if (word.EndsWith("ies") && word.Length > 3)
                return word.Substring(0, word.Length - 3) + "y";

            if (word.EndsWith("ves") && word.Length > 3)
                return word.Substring(0, word.Length - 3) + "f";

            if (word.EndsWith("s") && !word.EndsWith("ss") && !word.EndsWith("us") && word.Length > 1)
                return word.Substring(0, word.Length - 1);

            // Past tense verbs
            if (word.EndsWith("ied") && word.Length > 3)
                return word.Substring(0, word.Length - 3) + "y";

            if (word.EndsWith("ed"))
            {
                if (word.Length > 3)
                {
                    string base1 = word.Substring(0, word.Length - 2);
                    // Handle doubled consonants (e.g., "stopped" -> "stop")
                    if (base1.Length > 1 && base1[base1.Length - 1] == base1[base1.Length - 2] &&
                        IsConsonant(base1[base1.Length - 1]))
                    {
                        return base1.Substring(0, base1.Length - 1);
                    }
                    return base1;
                }
                return word;
            }

            // Present tense verbs
            if (word.EndsWith("ing"))
            {
                if (word.Length > 4)
                {
                    string base1 = word.Substring(0, word.Length - 3);
                    // Handle doubled consonants (e.g., "running" -> "run")
                    if (base1.Length > 1 && base1[base1.Length - 1] == base1[base1.Length - 2] &&
                        IsConsonant(base1[base1.Length - 1]))
                    {
                        return base1.Substring(0, base1.Length - 1);
                    }
                    // Handle silent e (e.g., "making" -> "make")
                    if (base1.Length > 1 && IsConsonant(base1[base1.Length - 1]) && IsVowel(base1[base1.Length - 2]))
                    {
                        return base1 + "e";
                    }
                    return base1;
                }
                return word;
            }

            // Comparative and superlative adjectives
            if (word.EndsWith("est") && word.Length > 4)
                return word.Substring(0, word.Length - 3);

            if (word.EndsWith("er") && word.Length > 3)
                return word.Substring(0, word.Length - 2);

            // Adverbs
            if (word.EndsWith("ly") && word.Length > 3)
                return word.Substring(0, word.Length - 2);

            return word;
        }

        private static bool IsVowel(char c)
        {
            return "aeiou".Contains(c);
        }

        private static bool IsConsonant(char c)
        {
            return char.IsLetter(c) && !"aeiou".Contains(c);
        }
    }
}