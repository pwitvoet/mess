namespace MESS.Macros.Texturing
{
    public class HotspotData
    {
        public HotspotRectangle[] HotspotRectangles { get; }

        public string? FallbackTextureName { get; }
        public float FallbackScoreThreshold { get; }


        public HotspotData(HotspotRectangle[] hotspotRectangles, string? fallbackTextureName = null, float fallbackScoreThreshold = 0f)
        {
            HotspotRectangles = hotspotRectangles;

            FallbackTextureName = fallbackTextureName;
            FallbackScoreThreshold = fallbackScoreThreshold;
        }
    }
}
