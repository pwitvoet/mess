using MLib.Texturing.Hotspotting;
using System.Collections.Generic;

namespace HotspotMaker.Hotspot
{
    public class HotspotRectangleVM
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public bool AllowRotation { get; set; }
        public bool AllowHorizontalMirroring { get; set; }  // TODO: Internally this is a None|Horizontal|Vertical enum, so it doesn't technically support horizontal + vertical (which is a 180 degree rotation)!
        public bool AllowVerticalMirroring { get; set; }

        public HotspotLayout HorizontalLayout { get; set; }
        public HotspotLayout VerticalLayout { get; set; }
        public double? SnapWidth { get; set; }
        public double? SnapHeight { get; set; }

        public double SelectionWeight { get; set; }
        public bool IsTopConcave { get; set; }
        public bool IsRightConcave { get; set; }
        public bool IsBottomConcave { get; set; }
        public bool IsLeftConcave { get; set; }

        public List<string> Labels { get; } = new();


        public string DisplayName => $"Rectangle ({X}, {Y}), {Width} x {Height}";


        public HotspotRectangleVM()
        {
        }

        public HotspotRectangleVM(HotspotRectangle rectangle)
        {
            X = rectangle.Rectangle.X;
            Y = rectangle.Rectangle.Y;
            Width = rectangle.Rectangle.Width;
            Height = rectangle.Rectangle.Height;

            AllowRotation = rectangle.AllowRotation;
            AllowHorizontalMirroring = rectangle.AllowedMirroring == MLib.Texturing.Hotspotting.Mirrorings.Horizontal;
            AllowVerticalMirroring = rectangle.AllowedMirroring == MLib.Texturing.Hotspotting.Mirrorings.Vertical;

            HorizontalLayout = rectangle.HorizontalLayout;
            VerticalLayout = rectangle.VerticalLayout;
            SnapWidth = rectangle.SnapWidth;
            SnapHeight = rectangle.SnapHeight;

            SelectionWeight = rectangle.SelectionWeight;
            IsTopConcave = rectangle.ConcaveEdges.HasFlag(MLib.Texturing.Hotspotting.ConcaveEdges.Top);
            IsRightConcave = rectangle.ConcaveEdges.HasFlag(MLib.Texturing.Hotspotting.ConcaveEdges.Right);
            IsBottomConcave = rectangle.ConcaveEdges.HasFlag(MLib.Texturing.Hotspotting.ConcaveEdges.Bottom);
            IsLeftConcave = rectangle.ConcaveEdges.HasFlag(MLib.Texturing.Hotspotting.ConcaveEdges.Left);

            Labels.AddRange(rectangle.Labels);
        }
    }
}
