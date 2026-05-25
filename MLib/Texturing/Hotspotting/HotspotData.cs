namespace MLib.Texturing.Hotspotting
{
    /// <summary>
    /// Hotspot data for a specific face.
    /// </summary>
    public class HotspotData
    {
        public IReadOnlyList<HotspotRectangle> HotspotRectangles => RectangleSet.Rectangles;

        public string? FallbackTextureName { get; }
        public double FallbackScoreThreshold => Binding.FallbackScoreThreshold;

        public IReadOnlySet<string> Labels => Binding.Labels;


        private HotspotRectangleSet RectangleSet { get; }
        private HotspotBinding Binding { get; }


        public HotspotData(HotspotRectangleSet rectangleSet, HotspotBinding binding, string? fallbackTextureName)
        {
            RectangleSet = rectangleSet;
            Binding = binding;

            FallbackTextureName = fallbackTextureName;
        }
    }
}
