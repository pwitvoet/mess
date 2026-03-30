namespace MESS.Macros.Texturing
{
    public class HotspotData
    {
        public HotspotRectangle[] HotspotRectangles { get; }

        public string? FallbackTextureName { get; }
        public double FallbackScoreThreshold { get; }

        public HashSet<string> Labels { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);


        public HotspotData(HotspotRectangle[] hotspotRectangles, string? fallbackTextureName, double fallbackScoreThreshold, IEnumerable<string>? labels)
        {
            HotspotRectangles = hotspotRectangles;

            FallbackTextureName = fallbackTextureName;
            FallbackScoreThreshold = fallbackScoreThreshold;

            if (labels != null)
            {
                foreach (var label in labels)
                    Labels.Add(label);
            }
        }
    }
}
