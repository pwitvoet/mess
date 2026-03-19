namespace MESS.Macros.Texturing
{
    /// <summary>
    /// This class maps texture names to hotspot data.
    /// </summary>
    public class HotspotDataCollection
    {
        private Dictionary<string, HotspotData> _hotspotRects = new();


        public void SetHotspotDataForTexture(string textureName, HotspotData hotspotData)
            => _hotspotRects[textureName.ToLowerInvariant()] = hotspotData;

        public HotspotData? GetHotspotDataForTexture(string textureName)
            => _hotspotRects.TryGetValue(textureName.ToLowerInvariant(), out var hotspotData) ? hotspotData : null;
    }
}
