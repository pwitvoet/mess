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
        public TilingMode TilingMode { get; }
        public double SelectionWeight { get; }
        public ConcaveEdges ConcaveEdges { get; }

        public HashSet<string> Labels { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);


        public HotspotRectangle(
            Rectangle rectangle,
            bool allowRotation,
            Mirrorings allowedMirroring,
            TilingMode tilingMode,
            double selectionWeight,
            ConcaveEdges concaveEdges,
            IEnumerable<string>? labels)
        {
            Rectangle = rectangle;

            AllowRotation = allowRotation;
            AllowedMirroring = allowedMirroring;
            TilingMode = tilingMode;
            SelectionWeight = selectionWeight;
            ConcaveEdges = concaveEdges;

            if (labels != null)
            {
                foreach (var label in labels)
                    Labels.Add(label);
            }
        }
    }
}
