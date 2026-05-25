using System.Text.RegularExpressions;

namespace MLib.Texturing.Hotspotting
{
    /// <summary>
    /// This class maps texture names to hotspot data.
    /// </summary>
    public class HotspotDataCollection
    {
        private Dictionary<string, HotspotRectangleSet> HotspotRectangleSets { get; } = new();

        private Dictionary<string, HotspotBinding> ExactHotspotBindings { get; } = new();
        private List<(Regex, HotspotBinding)> WildcardHotspotBindings { get; } = new();


        // TODO: Warn about names that are already taken!
        public void AddHotspotFileData(HotspotFileData hotspotFileData)
        {
            foreach (var rectangleSet in hotspotFileData.RectangleSets)
            {
                // TODO: Issue warning if a name is already taken!
                HotspotRectangleSets[rectangleSet.Name] = rectangleSet;
            }

            foreach (var binding in hotspotFileData.Bindings)
            {
                if (!HasWildcards(binding.TextureNamePattern))
                {
                    // TODO: Issue warning if a name is already taken!
                    ExactHotspotBindings[binding.TextureNamePattern] = binding;
                }
                else
                {
                    WildcardHotspotBindings.Add((MakeNamePatternRegex(binding.TextureNamePattern), binding));
                }
            }
        }

        public HotspotData? GetHotspotDataForTexture(string textureName)
        {
            var binding = GetHotspotBindingForTexture(textureName, out var nameMatch);
            if (binding == null)
                return null;

            if (!HotspotRectangleSets.TryGetValue(binding.HotspotName, out var rectangleSet))
                return null;


            var fallbackTextureName = binding.FallbackTextureNamePattern;
            if (fallbackTextureName != null && ContainsPlaceholders(binding.TextureNamePattern) && nameMatch != null)
            {
                var replacementValues = nameMatch.Groups.Cast<Group>()
                    .Skip(1)
                    .Select(group => group.Value)
                    .ToArray();

                fallbackTextureName = ReplacePlaceholders(fallbackTextureName, replacementValues);
            }
            return new HotspotData(rectangleSet, binding, fallbackTextureName);
        }


        private HotspotBinding? GetHotspotBindingForTexture(string textureName, out Match? nameMatch)
        {
            nameMatch = null;

            textureName = textureName.ToLowerInvariant();
            if (ExactHotspotBindings.TryGetValue(textureName, out var exactHotspotBinding))
                return exactHotspotBinding;

            foreach ((var regex, var hotspotBinding) in WildcardHotspotBindings)
            {
                var match = regex.Match(textureName);
                if (match.Success)
                {
                    nameMatch = match;
                    return hotspotBinding;
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
