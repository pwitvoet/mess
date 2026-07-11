using Avalonia;
using HotspotMaker.History;
using MLib.Mathematics.Spatial;
using MLib.Texturing.Hotspotting;
using System;
using System.Linq;

namespace HotspotMaker.Hotspot
{
    public class HotspotRectangleVM : ChangeTrackingVM
    {
        public static HotspotLayout[] AvailableHotspotLayouts { get; } = [HotspotLayout.Fit, HotspotLayout.Clip, HotspotLayout.Tile];


        private double _x;
        public double X
        {
            get => _x;
            set => SetPropertyOngoing(v => _x = v, _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetPropertyOngoing(v => _y = v, _y, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetPropertyOngoing(v => _width = v, _width, value);
        }

        private double _height;
        public double Height
        {
            get => _height;
            set => SetPropertyOngoing(v => _height = v, _height, value);
        }

        private bool _allowRotation;
        public bool AllowRotation
        {
            get => _allowRotation;
            set => SetProperty(v => _allowRotation = v, _allowRotation, value);
        }

        // TODO: Internally this is a None|Horizontal|Vertical enum, so it doesn't technically support horizontal + vertical (which is a 180 degree rotation)!
        private bool _allowHorizontalMirroring;
        public bool AllowHorizontalMirroring
        {
            get => _allowHorizontalMirroring;
            set => SetProperty(v => _allowHorizontalMirroring = v, _allowHorizontalMirroring, value);
        }

        private bool _allowVerticalMirroring;
        public bool AllowVerticalMirroring
        {
            get => _allowVerticalMirroring;
            set => SetProperty(v => _allowVerticalMirroring = v, _allowVerticalMirroring, value);
        }

        private HotspotLayout _horizontalLayout;
        public HotspotLayout HorizontalLayout
        {
            get => _horizontalLayout;
            set => SetProperty(v => _horizontalLayout = v, _horizontalLayout, value);
        }

        private HotspotLayout _verticalLayout;
        public HotspotLayout VerticalLayout
        {
            get => _verticalLayout;
            set => SetProperty(v => _verticalLayout = v, _verticalLayout, value);
        }

        private double? _snapWidth;
        public double? SnapWidth
        {
            get => _snapWidth;
            set => SetPropertyOngoing(v => _snapWidth = v, _snapWidth, value);
        }

        private double? _snapHeight;
        public double? SnapHeight
        {
            get => _snapHeight;
            set => SetPropertyOngoing(v => _snapHeight = v, _snapHeight, value);
        }

        private double _selectionWeight;
        public double SelectionWeight
        {
            get => _selectionWeight;
            set => SetPropertyOngoing(v => _selectionWeight = v, _selectionWeight, value);
        }

        private bool _isTopConcave;
        public bool IsTopConcave
        {
            get => _isTopConcave;
            set => SetProperty(v => _isTopConcave = v, _isTopConcave, value);
        }

        private bool _isRightConcave;
        public bool IsRightConcave
        {
            get => _isRightConcave;
            set => SetProperty(v => _isRightConcave = v, _isRightConcave, value);
        }

        private bool _isBottomConcave;
        public bool IsBottomConcave
        {
            get => _isBottomConcave;
            set => SetProperty(v => _isBottomConcave = v, _isBottomConcave, value);
        }

        private bool _isLeftConcave;
        public bool IsLeftConcave
        {
            get => _isLeftConcave;
            set => SetProperty(v => _isLeftConcave = v, _isLeftConcave, value);
        }

        private string[] _labels = Array.Empty<string>();
        public string[] Labels
        {
            get => _labels;
            set => SetPropertyOngoing(v => _labels = v, _labels, value);
        }


        public string DisplayName => $"Rectangle ({X}, {Y}), {Width} x {Height}";


        public HotspotRectangleVM(UndoSystem undoSystem)
            : base(undoSystem)
        {
        }

        public HotspotRectangleVM(HotspotRectangle rectangle, UndoSystem undoSystem)
            : base(undoSystem)
        {
            WithoutChangeTracking(() =>
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

                Labels= rectangle.Labels.ToArray();
            });
        }

        public HotspotRectangle CreateHotspotRectangle()
        {
            var mirrorings = Mirrorings.None;
            if (AllowHorizontalMirroring) mirrorings |= Mirrorings.Horizontal;
            if (AllowVerticalMirroring) mirrorings |= Mirrorings.Vertical;

            var concaveEdges = ConcaveEdges.None;
            if (IsTopConcave) concaveEdges |= ConcaveEdges.Top;
            if (IsRightConcave) concaveEdges |= ConcaveEdges.Right;
            if (IsBottomConcave) concaveEdges |= ConcaveEdges.Bottom;
            if (IsLeftConcave) concaveEdges |= ConcaveEdges.Left;

            return new HotspotRectangle(
                new Rectangle(X, Y, Width, Height),
                AllowRotation,
                mirrorings,
                HorizontalLayout,
                VerticalLayout,
                SnapWidth,
                SnapHeight,
                SelectionWeight,
                concaveEdges,
                Labels);
        }


        public void WithoutUndo(Action action)
            => WithoutChangeTracking(action);

        // TODO: Selecting another rectangle should stop any ungoing actions such as this one! -- The editor should manage that, not the individual rectangle VMs!
        public void MoveWithUndo(Vector offset)
        {
            var oldPosition = new Point(X, Y);
            var newPosition = new Point(X + offset.X, Y + offset.Y);

            PerformUndoableActionOngoing(
                "Move",
                () =>
                {
                    WithoutChangeTracking(() =>
                    {
                        X = newPosition.X;
                        Y = newPosition.Y;
                    });
                },
                () =>
                {
                    WithoutChangeTracking(() =>
                    {
                        X = oldPosition.X;
                        Y = oldPosition.Y;
                    });
                });
        }

        public void SetDimensionsWithoutUndo(double x, double y, double width, double height)
        {
            WithoutChangeTracking(() =>
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            });
        }
    }
}
