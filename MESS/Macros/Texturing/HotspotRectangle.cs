using MESS.Mathematics.Spatial;

namespace MESS.Macros.Texturing
{
    public enum HotspotLayout
    {
        /// <summary>
        /// Default behavior, the rectangle will be stretched to fit a face.
        /// </summary>
        Fit,

        /// <summary>
        /// If the rectangle is larger than the face, then this behaves the same as <see cref="Fit"/>. Else, it behaves as <see cref="Tile"/>.
        /// </summary>
        Clip,

        /// <summary>
        /// The rectangle will not be stretched.
        /// </summary>
        Tile,
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

        public HotspotLayout HorizontalLayout { get; }
        public HotspotLayout VerticalLayout { get; }

        public double SelectionWeight { get; }
        public ConcaveEdges ConcaveEdges { get; }

        public HashSet<string> Labels { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);


        // Derived:
        public bool IsTiling => HorizontalLayout == HotspotLayout.Tile || VerticalLayout == HotspotLayout.Tile;


        public HotspotRectangle(
            Rectangle rectangle,
            bool allowRotation,
            Mirrorings allowedMirroring,
            HotspotLayout horizontalLayout,
            HotspotLayout verticalLayout,
            double selectionWeight,
            ConcaveEdges concaveEdges,
            IEnumerable<string>? labels)
        {
            Rectangle = rectangle;

            AllowRotation = allowRotation;
            AllowedMirroring = allowedMirroring;

            HorizontalLayout = horizontalLayout;
            VerticalLayout = verticalLayout;

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
