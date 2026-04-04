using System.Text.RegularExpressions;

namespace MESS.Macros.Texturing
{
    /// <summary>
    /// This class maps texture names to hotspot data.
    /// </summary>
    public class HotspotDataCollection
    {
        private Dictionary<string, HotspotData> _hotspotRects = new();
        private List<(Regex, HotspotData)> _wildcardHotspotRects = new();


        public void SetHotspotDataForTexture(string namePattern, HotspotData hotspotData)
        {
            if (HasWildcards(namePattern))
                _wildcardHotspotRects.Add((MakeNamePatternRegex(namePattern), hotspotData));
            else
                _hotspotRects[namePattern.ToLowerInvariant()] = hotspotData;
        }

        public HotspotData? GetHotspotDataForTexture(string textureName)
        {
            textureName = textureName.ToLowerInvariant();

            if (_hotspotRects.TryGetValue(textureName, out var hotspotData))
                return hotspotData;

            foreach ((var regex, var wildcardHotspotData) in _wildcardHotspotRects)
            {
                var match = regex.Match(textureName);
                if (match.Success)
                {
                    if (wildcardHotspotData.FallbackTextureName != null && ContainsPlaceholders(wildcardHotspotData.FallbackTextureName))
                    {
                        var replacementValues = match.Groups.Cast<Group>()
                            .Skip(1)
                            .Select(group => group.Value)
                            .ToArray();

                        return new HotspotData(
                            wildcardHotspotData.HotspotRectangles,
                            ReplacePlaceholders(wildcardHotspotData.FallbackTextureName, replacementValues),
                            wildcardHotspotData.FallbackScoreThreshold,
                            wildcardHotspotData.Labels);
                    }
                    else
                    {
                        return wildcardHotspotData;
                    }
                }
            }

            return null;
        }


        private static bool HasWildcards(string namePattern)
            => Regex.IsMatch(namePattern, @"(?<!\\)\*");    // Matches * but not \*

        private static Regex MakeNamePatternRegex(string namePattern)
        {
            var regex = Regex.Replace(namePattern, @"\\\*|\*|\\|[^\*\\]*", match =>
            {
                switch (match.Value)
                {
                    case @"*": return "(.*?)";                  // A wildcard can be anything (including empty)
                    case @"\*": return Regex.Escape("*");       // A literal * must be escaped (\*)
                    default: return Regex.Escape(match.Value);  // There are no other special characters
                }
            });
            return new Regex("^" + regex + "$");
        }

        private static bool ContainsPlaceholders(string fallbackTextureName)
            => Regex.IsMatch(fallbackTextureName, @"\{\d+\}");

        public static string ReplacePlaceholders(string value, string[] replacementValues)
        {
            return Regex.Replace(value, @"\{(\d+)\}", match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < 0 || index >= replacementValues.Length)
                    return "";
                else
                    return replacementValues[index];
            });
        }
    }
}
