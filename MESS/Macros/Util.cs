﻿namespace MESS.Macros
{
    public class Util
    {
        /// <summary>
        /// Parses a list of comma-separated items. A double comma (,,) acts as an escape sequence.
        /// </summary>
        public static IEnumerable<string> ParseCommaSeparatedList(string input)
        {
            var startIndex = 0;
            var searchIndex = 0;
            while (searchIndex < input.Length)
            {
                var nextCommaIndex = input.IndexOf(',', searchIndex);
                if (nextCommaIndex == -1)
                    break;

                if (nextCommaIndex + 1 < input.Length && input[nextCommaIndex + 1] == ',')
                {
                    searchIndex = nextCommaIndex + 2;
                }
                else
                {
                    yield return input.Substring(startIndex, nextCommaIndex - startIndex).Trim().Replace(",,", ",");
                    startIndex = searchIndex = nextCommaIndex + 1;
                }
            }
            if (startIndex < input.Length)
                yield return input.Substring(startIndex).Trim().Replace(",,", ",");
        }

        /// <summary>
        /// Parses a list of comma-separated items, with optional weights (a colon followed by a number).
        /// </summary>
        public static IEnumerable<(string name, double weight)> ParseCommaSeparatedWeightedList(string input, double defaultWeight = 1)
        {
            foreach (var part in ParseCommaSeparatedList(input))
            {
                var colonIndex = part.LastIndexOf(':');
                var name = part;
                if (colonIndex != -1 && double.TryParse(part.Substring(colonIndex + 1), out var weight))
                {
                    name = part.Substring(0, colonIndex).Trim();
                }
                else
                {
                    weight = defaultWeight;
                }

                yield return (name, weight);
            }
        }
    }
}
