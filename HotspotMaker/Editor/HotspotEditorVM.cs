using Avalonia;
using Avalonia.Media;
using HotspotMaker.History;
using HotspotMaker.Hotspot;
using MLib.Mathematics.Spatial;
using MLib.Texturing.Hotspotting;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace HotspotMaker.Editor
{
    public class HotspotEditorVM : ChangeTrackingVM
    {
        public event Action? RectanglesChanged;
        protected void RaiseRectanglesChanged()
            => RectanglesChanged?.Invoke();

        public event Action<HotspotRectangleVM, string?>? RectanglePropertyChanged;
        protected void RaiseRectanglePropertyChanged(HotspotRectangleVM sender, string? propertyName)
            => RectanglePropertyChanged?.Invoke(sender, propertyName);


        // Bindable properties:
        private IImage? _textureImage;
        public IImage? TextureImage
        {
            get => _textureImage;
            set { _textureImage = value; RaisePropertyChanged(); }
        }

        private HotspotRectangleSetVM? _rectangleSet;
        public HotspotRectangleSetVM? RectangleSet
        {
            get => _rectangleSet;
            set
            {
                if (_rectangleSet != null)
                {
                    foreach (var rectangleVM in _rectangleSet.Rectangles)
                        rectangleVM.PropertyChanged -= Rectangle_PropertyChanged;

                    _rectangleSet.Rectangles.CollectionChanged -= Rectangles_CollectionChanged;
                }

                if (_rectangleSet != null)
                    Selection.Clear();

                _rectangleSet = value;

                if (_rectangleSet != null)
                {
                    foreach (var rectangleVM in _rectangleSet.Rectangles)
                        rectangleVM.PropertyChanged += Rectangle_PropertyChanged;

                    _rectangleSet.Rectangles.CollectionChanged += Rectangles_CollectionChanged;
                }

                RaisePropertyChanged();
                RaiseRectanglesChanged();
            }
        }

        public HotspotRectangleSelectionVM Selection { get; }


        // Internal state:
        private Point CurrentOperationStartCoordinate { get; set; }
        private HotspotRectangleVM[] CurrentOperationRectangles { get; set; } = Array.Empty<HotspotRectangleVM>();
        private Point[] CurrentOperationOriginalPositions { get; set; } = Array.Empty<Point>();


        public HotspotEditorVM(UndoSystem undoSystem, HotspotRectangleSelectionVM selection)
            : base(undoSystem)
        {
            Selection = selection;
            Selection.SelectionChanged += Selection_SelectionChanged;
        }

        public HotspotRectangleVM[] GetRectanglesAtPoint(Point point)
            => RectangleSet?.Rectangles.Where(rectangleVM => IsTouching(rectangleVM, point)).ToArray() ?? [];

        public HotspotRectangleVM[] GetRectanglesInArea(Rect rect)
            => RectangleSet?.Rectangles.Where(rectangleVM => IsTouching(rectangleVM, rect)).ToArray() ?? [];


        public void SetSelection(HotspotRectangleVM rectangle)
        {
            Selection.Clear();
            Selection.Add(rectangle);
        }

        public void SetSelection(IEnumerable<HotspotRectangleVM> rectangles)
        {
            // Materialize immediately - the caller is likely to use the current selection as basis, and clearing it before enumerating would cause trouble:
            var rectanglesArray = rectangles.ToArray();

            Selection.Clear();
            Selection.Add(rectanglesArray);
        }

        public void ClearSelection()
        {
            Selection.Clear();
        }


        public HotspotRectangleVM[]? AddRectanglesWithOffset(HotspotRectangle[] rectangles, Point offset)
        {
            var rectangleSet = RectangleSet;
            if (rectangleSet == null)
                return null;

            var rectangleVMs = rectangles
                .Select(rectangle => new HotspotRectangleVM(rectangle, UndoSystem))
                .ToArray();

            foreach (var rectangleVM in rectangleVMs)
            {
                rectangleVM.X += offset.X;
                rectangleVM.Y += offset.Y;
            }

            PerformUndoableAction(
                () =>
                {
                    foreach (var rectangleVM in rectangleVMs)
                        rectangleSet.Rectangles.Add(rectangleVM);
                },
                () =>
                {
                    foreach (var rectangleVM in rectangleVMs)
                        rectangleSet.Rectangles.Remove(rectangleVM);
                });

            return rectangleVMs;
        }


        public void StartDuplicateRectanglesOperation(Point startTextureCoordinate, double gridSize, bool snapToGrid)
        {
            var rectangleSet = RectangleSet;
            if (rectangleSet == null)
                return;

            var duplicatedRectangles = Selection.Rectangles
                .Select(rectangleVM => new HotspotRectangleVM(rectangleVM.CreateHotspotRectangle(), UndoSystem))
                .ToArray();
            if (!duplicatedRectangles.Any())
                return;

            PerformUndoableActionOngoing(
                "DuplicateRectangles",
                () =>
                {
                    foreach (var rectangleVM in duplicatedRectangles)
                        rectangleSet.Rectangles.Add(rectangleVM);
                },
                () =>
                {
                    foreach (var rectangleVM in duplicatedRectangles)
                        rectangleSet.Rectangles.Remove(rectangleVM);
                });

            CurrentOperationStartCoordinate = startTextureCoordinate;
            CurrentOperationRectangles = duplicatedRectangles;
            CurrentOperationOriginalPositions = duplicatedRectangles
                .Select(rectangleVM => new Point(rectangleVM.X, rectangleVM.Y))
                .ToArray();


            // Also select the new rectangles:
            SetSelection(duplicatedRectangles);
        }

        public void UpdateDuplicateRectanglesOperation(Point currentTextureCoordinate, double gridSize, bool snapToGrid)
        {
            var offset = currentTextureCoordinate - CurrentOperationStartCoordinate;
            if (snapToGrid)
                offset = GetSnappedCoordinate(offset, gridSize, snapToGrid);

            // NOTE: We're updating the selected rectangles in-place, so undoing and redoing this action will restore them with their latest positions.
            for (int i = 0; i < CurrentOperationRectangles.Length; i++)
            {
                var rectangleVM = CurrentOperationRectangles[i];
                var originalPosition = CurrentOperationOriginalPositions[i];

                rectangleVM.SetDimensionsWithoutUndo(originalPosition.X + offset.X, originalPosition.Y + offset.Y, rectangleVM.Width, rectangleVM.Height);
            }
        }

        public void StartMoveRectanglesOperation(Point startTextureCoordinate, double gridSize, bool snapToGrid)
        {
            if (RectangleSet == null)
                return;

            // NOTE: No undoable action yet, because there has been no actual movement yet.

            CurrentOperationStartCoordinate = startTextureCoordinate;
            CurrentOperationRectangles = Selection.Rectangles.ToArray();
            CurrentOperationOriginalPositions = Selection.Rectangles
                .Select(rectangleVM => new Point(rectangleVM.X, rectangleVM.Y))
                .ToArray();
        }

        public void UpdateMoveRectanglesOperation(Point currentTextureCoordinate, double gridSize, bool snapToGrid)
        {
            var offset = currentTextureCoordinate - CurrentOperationStartCoordinate;
            if (snapToGrid)
                offset = GetSnappedCoordinate(offset, gridSize, snapToGrid);

            var selectedRectangles = CurrentOperationRectangles;
            var originalPositions = CurrentOperationOriginalPositions;

            PerformUndoableActionOngoing(
                "MoveRectangles",
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                    {
                        var rectangleVM = selectedRectangles[i];
                        var originalPosition = originalPositions[i];

                        rectangleVM.SetDimensionsWithoutUndo(originalPosition.X + offset.X, originalPosition.Y + offset.Y, rectangleVM.Width, rectangleVM.Height);
                    }
                },
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                    {
                        var rectangleVM = selectedRectangles[i];
                        var originalPosition = originalPositions[i];

                        rectangleVM.SetDimensionsWithoutUndo(originalPosition.X, originalPosition.Y, rectangleVM.Width, rectangleVM.Height);
                    }
                });
        }

        public void MoveSelectedRectangles(Vector offset)
        {
            if (RectangleSet == null)
                return;

            var selectedRectangles = Selection.Rectangles.ToArray();
            var originalPositions = selectedRectangles
                .Select(rectangleVM => new Point(rectangleVM.X, rectangleVM.Y))
                .ToArray();

            PerformUndoableActionOngoing(
                "MoveRectanglesByOffset",
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                    {
                        var rectangleVM = selectedRectangles[i];
                        var originalPosition = originalPositions[i];

                        rectangleVM.SetDimensionsWithoutUndo(originalPosition.X + offset.X, originalPosition.Y + offset.Y, rectangleVM.Width, rectangleVM.Height);
                    }
                },
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                    {
                        var rectangleVM = selectedRectangles[i];
                        var originalPosition = originalPositions[i];

                        rectangleVM.SetDimensionsWithoutUndo(originalPosition.X, originalPosition.Y, rectangleVM.Width, rectangleVM.Height);
                    }
                });
        }

        public void StartCreateRectangleOperation(Point startTextureCoordinate, double gridSize, bool snapToGrid)
        {
            var rectangleSet = RectangleSet;
            if (rectangleSet == null)
                return;

            var snappedCoordinate = GetSnappedCoordinate(startTextureCoordinate, gridSize, snapToGrid);
            var newRectangle = new HotspotRectangle(
                new Rectangle(snappedCoordinate.X, snappedCoordinate.Y, gridSize, gridSize),
                false,
                Mirrorings.None,
                HotspotLayout.Fit,
                HotspotLayout.Fit,
                null,
                null,
                1,
                ConcaveEdges.None,
                Array.Empty<string>());
            var newRectangleVM = new HotspotRectangleVM(newRectangle, UndoSystem);

            PerformUndoableActionOngoing(
                "CreateRectangle",
                () => rectangleSet.Rectangles.Add(newRectangleVM),
                () => rectangleSet.Rectangles.Remove(newRectangleVM));

            CurrentOperationStartCoordinate = startTextureCoordinate;
            CurrentOperationRectangles = [newRectangleVM];


            // Also select the new rectangle:
            SetSelection(newRectangleVM);
        }

        public void UpdateCreateRectangleOperation(Point currentTextureCoordinate, double gridSize, bool snapToGrid)
        {
            var rectangleVM = CurrentOperationRectangles.FirstOrDefault();
            if (rectangleVM == null)
                return;

            var minX = Math.Min(CurrentOperationStartCoordinate.X, currentTextureCoordinate.X);
            var minY = Math.Min(CurrentOperationStartCoordinate.Y, currentTextureCoordinate.Y);
            var maxX = Math.Max(CurrentOperationStartCoordinate.X, currentTextureCoordinate.X);
            var maxY = Math.Max(CurrentOperationStartCoordinate.Y, currentTextureCoordinate.Y);

            if (snapToGrid)
            {
                // Always making the grid cell that the cursor is over (and the cell that the cursor started at)
                // part of the new rectangle results in a more intuitive editing experience:
                var halfGridSize = gridSize / 2;
                minX -= halfGridSize;
                maxX += halfGridSize;
                minY -= halfGridSize;
                maxY += halfGridSize;
            }

            var snappedTopLeft = GetSnappedCoordinate(new Point(minX, minY), gridSize, snapToGrid);
            var snappedBottomRight = GetSnappedCoordinate(new Point(maxX, maxY), gridSize, snapToGrid);

            // We don't need to update the undoable action here because we're modifying the newly created element:
            var minSize = snapToGrid ? gridSize : 1;
            rectangleVM.SetDimensionsWithoutUndo(
                snappedTopLeft.X,
                snappedTopLeft.Y,
                Math.Max(minSize, snappedBottomRight.X - snappedTopLeft.X),
                Math.Max(minSize, snappedBottomRight.Y - snappedTopLeft.Y));
        }

        public void FinalizeCurrentOperation()
        {
            StopOngoingAction();

            CurrentOperationStartCoordinate = new Point();
            CurrentOperationRectangles = Array.Empty<HotspotRectangleVM>();
        }

        public void DeleteSelectedRectangles()
        {
            StopOngoingAction();

            var rectangleSet = RectangleSet;
            if (rectangleSet == null)
                return;

            var selectedRectangles = Selection.Rectangles.ToArray();
            var originalIndices = selectedRectangles
                .Select(rectangleSet.Rectangles.IndexOf)
                .ToArray();

            PerformUndoableAction(
                () =>
                {
                    foreach (var rectangleVM in selectedRectangles)
                        rectangleSet.Rectangles.Remove(rectangleVM);
                },
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        rectangleSet.Rectangles.Insert(originalIndices[i], selectedRectangles[i]);
                });
        }


        private Point GetSnappedCoordinate(Point startTextureCoordinate, double gridSize, bool snapToGrid)
        {
            if (snapToGrid)
                return new Point(Math.Round(startTextureCoordinate.X / gridSize) * gridSize, Math.Round(startTextureCoordinate.Y / gridSize) * gridSize);
            else
                return new Point(Math.Round(startTextureCoordinate.X), Math.Round(startTextureCoordinate.Y));
        }


        private void Rectangles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (HotspotRectangleVM rectangleVM in e.NewItems)
                    rectangleVM.PropertyChanged += Rectangle_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (HotspotRectangleVM rectangleVM in e.OldItems)
                    rectangleVM.PropertyChanged -= Rectangle_PropertyChanged;
            }

            RaiseRectanglesChanged();
        }

        private void Rectangle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is HotspotRectangleVM rectangleVM)
                RaiseRectanglePropertyChanged(rectangleVM, e.PropertyName);
        }

        private void Selection_SelectionChanged(HotspotRectangleVM[] deselected, HotspotRectangleVM[] selected)
        {
            StopOngoingAction();
        }


        private static bool IsTouching(HotspotRectangleVM rectangleVM, Point point)
            => point.X >= rectangleVM.X && point.X <= rectangleVM.X + rectangleVM.Width &&
            point.Y >= rectangleVM.Y && point.Y <= rectangleVM.Y + rectangleVM.Height;

        private static bool IsTouching(HotspotRectangleVM rectangleVM, Rect rect)
            => rect.Right >= rectangleVM.X && rect.Left <= rectangleVM.X + rectangleVM.Width &&
            rect.Bottom >= rectangleVM.Y && rect.Top <= rectangleVM.Y + rectangleVM.Height;
    }
}
