namespace MESS.Macros.Texturing
{
    /// <summary>
    /// This class maps texture names to collections of hotspot rectangles.
    /// </summary>
    public class HotspotData
    {
        private Dictionary<string, HotspotRectangle[]> _hotspotRects = new();


        public void SetHotspotRectanglesForTexture(string textureName, HotspotRectangle[] hotspotRects)
            => _hotspotRects[textureName.ToLowerInvariant()] = hotspotRects;

        public HotspotRectangle[] GetHotspotRectanglesForTexture(string textureName)
            => _hotspotRects.TryGetValue(textureName.ToLowerInvariant(), out var hotspotRects) ? hotspotRects : Array.Empty<HotspotRectangle>();
    }
}
