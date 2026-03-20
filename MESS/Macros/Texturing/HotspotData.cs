namespace MESS.Macros.Texturing
{
    public class HotspotData
    {
        public HotspotRectangle[] HotspotRectangles { get; }

        public string? FallbackTextureName { get; }
        public double FallbackScoreThreshold { get; }


        public HotspotData(HotspotRectangle[] hotspotRectangles, string? fallbackTextureName = null, double fallbackScoreThreshold = 0)
        {
            HotspotRectangles = hotspotRectangles;

            FallbackTextureName = fallbackTextureName;
            FallbackScoreThreshold = fallbackScoreThreshold;
        }
    }
}
