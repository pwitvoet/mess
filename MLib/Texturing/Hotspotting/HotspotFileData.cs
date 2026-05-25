namespace MLib.Texturing.Hotspotting
{
    /// <summary>
    /// A .hotspot file contains a list of hotspot rectangle sets, and a list of bindings, which map texture names to rectangle sets.
    /// </summary>
    public class HotspotFileData
    {
        public HotspotRectangleSet[] RectangleSets { get; }
        public HotspotBinding[] Bindings { get; }


        public HotspotFileData(IEnumerable<HotspotRectangleSet> rectangleSets, IEnumerable<HotspotBinding> bindings)
        {
            RectangleSets = rectangleSets.ToArray();
            Bindings = bindings.ToArray();
        }
    }
}
