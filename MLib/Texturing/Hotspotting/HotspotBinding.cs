namespace MLib.Texturing.Hotspotting
{
    /// <summary>
    /// This links one or more textures to a hotspot rectangle set.
    /// Optionally, a fallback texture can be provided, which will be used if none of the rectangles achieve a sufficiently high score.
    /// The texture name pattern can contain wildcards (*), and the fallback texture name pattern can contain matching placeholders ({0}, {1], etc.).
    /// </summary>
    public class HotspotBinding
    {
        public string TextureNamePattern { get; }
        public string HotspotName { get; }

        public string? FallbackTextureNamePattern { get; }
        public double FallbackScoreThreshold { get; }

        public HashSet<string> Labels { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        // TODO: Add a label function: a default function that can select labels for specific faces, for example 'wall', 'floor' or 'ceiling' based on the orientation of a face.


        public HotspotBinding(string textureNamePattern, string hotspotName, string? fallbackTextureNamePattern, double fallbackScoreThreshold, IEnumerable<string>? labels)
        {
            TextureNamePattern = textureNamePattern;
            HotspotName = hotspotName;

            FallbackTextureNamePattern = fallbackTextureNamePattern;
            FallbackScoreThreshold = fallbackScoreThreshold;

            if (labels != null)
            {
                foreach (var label in labels)
                    Labels.Add(label);
            }
        }
    }
}
