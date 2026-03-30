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
    public enum Mirrorings
    {
        None =          0,
        Horizontal =    1,
        Vertical =      2,
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
        public Mirrorings AllowedMirroring { get; }
        public bool IsAlternate { get; }

        // Additions:
        public TilingMode TilingMode { get; }
        public double SelectionWeight { get; }
        public ConcaveEdges ConcaveEdges { get; }


        public HotspotRectangle(
            Rectangle rectangle,
            bool allowRotation,
            Mirrorings allowedMirroring,
            bool isAlternate,
            TilingMode tilingMode,
            double selectionWeight,
            ConcaveEdges concaveEdges)
        {
            Rectangle = rectangle;

            AllowRotation = allowRotation;
            AllowedMirroring = allowedMirroring;
            IsAlternate = isAlternate;

            TilingMode = tilingMode;
            SelectionWeight = selectionWeight;
            ConcaveEdges = concaveEdges;
        }
    }
}
