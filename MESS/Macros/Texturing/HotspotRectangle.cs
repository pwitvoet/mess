using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public enum TilingMode
    {
        None,
        Horizontal,
        Vertical,
    }

    [Flags]
    public enum ConcaveEdges
    {
        None =      0,

        Top =       1,
        Right =     2,
        Bottom =    4,
        Left =      8,
    }


    public class HotspotRectangle
    {
        public Rectangle Rectangle { get; }

        public bool AllowRotation { get; }
        public bool AllowMirroring { get; }
        public bool IsAlternate { get; }

        // Additions:
        public TilingMode TilingMode { get; }
        public float SelectionWeight { get; }
        public ConcaveEdges ConcaveEdges { get; }


        public HotspotRectangle(Rectangle rectangle, bool allowRotation, bool allowMirroring, bool isAlternate, TilingMode tilingMode, float selectionWeight, ConcaveEdges concaveEdges)
        {
            Rectangle = rectangle;

            AllowRotation = allowRotation;
            AllowMirroring = allowMirroring;
            IsAlternate = isAlternate;

            TilingMode = tilingMode;
            SelectionWeight = selectionWeight;
            ConcaveEdges = concaveEdges;
        }
    }
}
