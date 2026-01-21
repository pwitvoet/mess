using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public enum TilingMode
    {
        None,
        Horizontal,
        Vertical,
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


        public HotspotRectangle(Rectangle rectangle, bool allowRotation, bool allowMirroring, bool isAlternate, TilingMode tilingMode, float selectionWeight)
        {
            Rectangle = rectangle;

            AllowRotation = allowRotation;
            AllowMirroring = allowMirroring;
            IsAlternate = isAlternate;

            TilingMode = tilingMode;
            SelectionWeight = selectionWeight;
        }
    }
}
