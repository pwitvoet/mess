namespace MLib.Texturing.Hotspotting
{
    /// <summary>
    /// A set of hotspot rectangles. A single set can be used for multiple textures.
    /// </summary>
    public class HotspotRectangleSet
    {
        public string Name { get; }
        public HotspotRectangle[] Rectangles { get; }


        public HotspotRectangleSet(string name, IEnumerable<HotspotRectangle> rectangles)
        {
            Name = name;
            Rectangles = rectangles.ToArray();
        }
    }
}
