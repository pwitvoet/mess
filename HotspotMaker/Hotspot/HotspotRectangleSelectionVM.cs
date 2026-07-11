using HotspotMaker.History;
using HotspotMaker.Util;
using MLib.Texturing.Hotspotting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace HotspotMaker.Hotspot
{
    public class HotspotRectangleSelectionVM : ChangeTrackingVM
    {
        public event Action<HotspotRectangleVM[], HotspotRectangleVM[]>? SelectionChanged;
        protected void RaiseSelectionChanged(HotspotRectangleVM[] deselected, HotspotRectangleVM[] selected)
            => SelectionChanged?.Invoke(deselected, selected);


        // Bindable properties:
        private ObservableCollection<HotspotRectangleVM> _rectangles = new();
        public IEnumerable<HotspotRectangleVM> Rectangles => _rectangles;


        // Multi-selection property editing:
        public MultiValue<double> X { get; }
        public MultiValue<double> Y { get; }
        public MultiValue<double> Width { get; }
        public MultiValue<double> Height { get; }

        public MultiValue<bool> AllowRotation { get; }
        public MultiValue<bool> AllowHorizontalMirroring { get; }
        public MultiValue<bool> AllowVerticalMirroring { get; }

        public MultiValue<HotspotLayout> HorizontalLayout { get; }
        public MultiValue<HotspotLayout> VerticalLayout { get; }
        public NullableMultiValue<double?> SnapWidth { get; }
        public NullableMultiValue<double?> SnapHeight { get; }

        public MultiValue<double> SelectionWeight { get; }

        public MultiValue<bool> IsTopConcave { get; }
        public MultiValue<bool> IsRightConcave { get; }
        public MultiValue<bool> IsBottomConcave { get; }
        public MultiValue<bool> IsLeftConcave { get; }

        public NullableMultiValue<string[]> Labels { get; }


        // Derived properties:
        public bool IsEmpty => _rectangles.Count == 0;

        public bool IsSingleSelection => _rectangles.Count == 1;

        public bool IsMultiSelection => _rectangles.Count > 1;

        public HotspotRectangleVM? SingleRectangle => _rectangles.Count == 1 ? _rectangles[0] : null;


        private bool SuppressSelectionChangedEvents { get; set; }


        public HotspotRectangleSelectionVM(UndoSystem undoSystem)
            : base(undoSystem)
        {
            _rectangles.CollectionChanged += Rectangles_CollectionChanged;

            X = new MultiValue<double>(value => SetMultiPropertyOngoing(value, r => r.X, (r, v) => r.X = v, nameof(X)));
            Y = new MultiValue<double>(value => SetMultiPropertyOngoing(value, r => r.Y, (r, v) => r.Y = v, nameof(Y)));
            Width = new MultiValue<double>(value => SetMultiPropertyOngoing(value, r => r.Width, (r, v) => r.Width = v, nameof(Width)));
            Height = new MultiValue<double>(value => SetMultiPropertyOngoing(value, r => r.Height, (r, v) => r.Height = v, nameof(Height)));

            AllowRotation = new MultiValue<bool>(value => SetMultiProperty(value, r => r.AllowRotation, (r, v) => r.AllowRotation = v));
            AllowHorizontalMirroring = new MultiValue<bool>(value => SetMultiProperty(value, r => r.AllowHorizontalMirroring, (r, v) => r.AllowHorizontalMirroring = v));
            AllowVerticalMirroring = new MultiValue<bool>(value => SetMultiProperty(value, r => r.AllowVerticalMirroring, (r, v) => r.AllowVerticalMirroring = v));

            HorizontalLayout = new MultiValue<HotspotLayout>(value => SetMultiProperty(value, r => r.HorizontalLayout, (r, v) => r.HorizontalLayout = v));
            VerticalLayout = new MultiValue<HotspotLayout>(value => SetMultiProperty(value, r => r.VerticalLayout, (r, v) => r.VerticalLayout = v));
            SnapWidth = new NullableMultiValue<double?>(value => SetMultiPropertyOngoing(value, r => r.SnapWidth, (r, v) => r.SnapWidth = v, nameof(SnapWidth)));
            SnapHeight = new NullableMultiValue<double?>(value => SetMultiPropertyOngoing(value, r => r.SnapHeight, (r, v) => r.SnapHeight = v, nameof(SnapHeight)));

            SelectionWeight = new MultiValue<double>(value => SetMultiPropertyOngoing(value, r => r.SelectionWeight, (r, v) => r.SelectionWeight = v, nameof(SelectionWeight)));

            IsTopConcave = new MultiValue<bool>(value => SetMultiProperty(value, r => r.IsTopConcave, (r, v) => r.IsTopConcave = v));
            IsRightConcave = new MultiValue<bool>(value => SetMultiProperty(value, r => r.IsRightConcave, (r, v) => r.IsRightConcave = v));
            IsBottomConcave = new MultiValue<bool>(value => SetMultiProperty(value, r => r.IsBottomConcave, (r, v) => r.IsBottomConcave = v));
            IsLeftConcave = new MultiValue<bool>(value => SetMultiProperty(value, r => r.IsLeftConcave, (r, v) => r.IsLeftConcave = v));

            Labels = new NullableMultiValue<string[]>(value => SetMultiPropertyOngoing(value, r => r.Labels, (r, v) => r.Labels = v, nameof(Labels)));
        }

        public void Clear()
        {
            _rectangles.Clear();
        }

        public void Add(HotspotRectangleVM rectangleVM)
        {
            _rectangles.Add(rectangleVM);
        }

        public void Add(IEnumerable<HotspotRectangleVM> rectangleVMs)
        {
            var rectangleVMsArray = rectangleVMs.ToArray();

            try
            {
                SuppressSelectionChangedEvents = true;

                foreach (var rectangleVM in rectangleVMsArray)
                    _rectangles.Add(rectangleVM);
            }
            finally
            {
                SuppressSelectionChangedEvents = false;

                RaiseSelectionChanged(Array.Empty<HotspotRectangleVM>(), rectangleVMsArray);
            }
        }

        public bool IsSelected(HotspotRectangleVM rectangleVM)
        {
            return Rectangles.Contains(rectangleVM);
        }


        private void Rectangles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateMultiProperties();

            var deselectedRectangles = e.OldItems?.OfType<HotspotRectangleVM>().ToArray() ?? Array.Empty<HotspotRectangleVM>();
            foreach (var rectangleVM in deselectedRectangles)
                rectangleVM.PropertyChanged -= RectangleVM_PropertyChanged;

            var selectedRectangles = e.NewItems?.OfType<HotspotRectangleVM>().ToArray() ?? Array.Empty<HotspotRectangleVM>();
            foreach (var rectangleVM in selectedRectangles)
                rectangleVM.PropertyChanged += RectangleVM_PropertyChanged;

            if (!SuppressSelectionChangedEvents)
            {
                RaiseSelectionChanged(deselectedRectangles, selectedRectangles);
            }

            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(IsSingleSelection));
            RaisePropertyChanged(nameof(IsMultiSelection));
            RaisePropertyChanged(nameof(SingleRectangle));
        }

        private void RectangleVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(HotspotRectangleVM.X): UpdateX(); break;
                case nameof(HotspotRectangleVM.Y): UpdateY(); break;
                case nameof(HotspotRectangleVM.Width): UpdateWidth(); break;
                case nameof(HotspotRectangleVM.Height): UpdateHeight(); break;

                case nameof(HotspotRectangleVM.AllowRotation): UpdateAllowRotation(); break;
                case nameof(HotspotRectangleVM.AllowHorizontalMirroring): UpdateAllowHorizontalMirroring(); break;
                case nameof(HotspotRectangleVM.AllowVerticalMirroring): UpdateAllowVerticalMirroring(); break;

                case nameof(HotspotRectangleVM.HorizontalLayout): UpdateHorizontalLayout(); break;
                case nameof(HotspotRectangleVM.VerticalLayout): UpdateVerticalLayout(); break;
                case nameof(HotspotRectangleVM.SnapWidth): UpdateSnapWidth(); break;
                case nameof(HotspotRectangleVM.SnapHeight): UpdateSnapHeight(); break;

                case nameof(HotspotRectangleVM.SelectionWeight): UpdateSelectionWeight(); break;

                case nameof(HotspotRectangleVM.IsTopConcave): UpdateIsTopConcave(); break;
                case nameof(HotspotRectangleVM.IsRightConcave): UpdateIsRightConcave(); break;
                case nameof(HotspotRectangleVM.IsBottomConcave): UpdateIsBottomConcave(); break;
                case nameof(HotspotRectangleVM.IsLeftConcave): UpdateIsLeftConcave(); break;

                case nameof(HotspotRectangleVM.Labels): UpdateLabels(); break;
            }
        }


        // TODO: This should mark all affected rectangles as modified! Maybe give undoable actions a unique ID, and give each object a last-modified-by-id property?
        private void SetMultiProperty<TValue>(TValue newValue, Func<HotspotRectangleVM, TValue> getValue, Action<HotspotRectangleVM, TValue> setValue)
            where TValue : struct
        {
            var selectedRectangles = Rectangles.ToArray();
            var originalValues = selectedRectangles.Select(getValue).ToArray();

            PerformUndoableAction(
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        selectedRectangles[i].WithoutUndo(() => setValue(selectedRectangles[i], newValue));
                },
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        selectedRectangles[i].WithoutUndo(() => setValue(selectedRectangles[i], originalValues[i]));
                });
        }

        // TODO: Same as above - mark all affected rectangles as modified!
        private void SetMultiPropertyOngoing<TValue>(TValue newValue, Func<HotspotRectangleVM, TValue> getValue, Action<HotspotRectangleVM, TValue> setValue, string propertyName)
        {
            var selectedRectangles = Rectangles.ToArray();
            var originalValues = selectedRectangles.Select(getValue).ToArray();

            PerformUndoableActionOngoing(
                propertyName,
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        selectedRectangles[i].WithoutUndo(() => setValue(selectedRectangles[i], newValue));
                },
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        selectedRectangles[i].WithoutUndo(() => setValue(selectedRectangles[i], originalValues[i]));
                });
        }

        // TODO: Same as above - mark all affected rectangles as modified!
        private void SetMultiPropertyOngoing<TValue>(TValue? newValue, Func<HotspotRectangleVM, TValue?> getValue, Action<HotspotRectangleVM, TValue?> setValue, string propertyName)
            where TValue : struct
        {
            var selectedRectangles = Rectangles.ToArray();
            var originalValues = selectedRectangles.Select(getValue).ToArray();

            PerformUndoableActionOngoing(
                propertyName,
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        selectedRectangles[i].WithoutUndo(() => setValue(selectedRectangles[i], newValue));
                },
                () =>
                {
                    for (int i = 0; i < selectedRectangles.Length; i++)
                        selectedRectangles[i].WithoutUndo(() => setValue(selectedRectangles[i], originalValues[i]));
                });
        }

        private void UpdateMultiProperties()
        {
            UpdateX();
            UpdateY();
            UpdateWidth();
            UpdateHeight();

            UpdateAllowRotation();
            UpdateAllowHorizontalMirroring();
            UpdateAllowVerticalMirroring();

            UpdateHorizontalLayout();
            UpdateVerticalLayout();
            UpdateSnapWidth();
            UpdateSnapHeight();

            UpdateSelectionWeight();

            UpdateIsTopConcave();
            UpdateIsRightConcave();
            UpdateIsBottomConcave();
            UpdateIsLeftConcave();

            UpdateLabels();
        }

        private void UpdateMultiProperty<TValue>(MultiValue<TValue> multiValue, Func<HotspotRectangleVM, TValue> getValue)
            where TValue : struct
        {
            if (_rectangles.Count == 0)
            {
                // NOTE: No value actually, but the UI shouldn't display anything, so this should be OK.
                multiValue.SetMultiValue();
            }
            else if (_rectangles.Count == 1)
            {
                multiValue.SetSingleValue(getValue(_rectangles[0]));
            }
            else
            {
                var firstValue = getValue(_rectangles[0]);
                var comparer = EqualityComparer<TValue>.Default;
                var hasMultipleValues = _rectangles.Any(rectangleVM => !comparer.Equals(getValue(rectangleVM), firstValue));

                if (hasMultipleValues)
                    multiValue.SetMultiValue();
                else
                    multiValue.SetSingleValue(firstValue);
            }
        }

        private void UpdateMultiProperty<TValue>(NullableMultiValue<TValue> multiValue, Func<HotspotRectangleVM, TValue> getValue)
        {
            if (_rectangles.Count == 0)
            {
                // NOTE: No value actually, but the UI shouldn't display anything, so this should be OK.
                multiValue.SetMultiValue();
            }
            else if (_rectangles.Count == 1)
            {
                multiValue.SetSingleValue(getValue(_rectangles[0]));
            }
            else
            {
                var firstValue = getValue(_rectangles[0]);
                var comparer = EqualityComparer<TValue>.Default;
                var hasMultipleValues = _rectangles.Any(rectangleVM => !comparer.Equals(getValue(rectangleVM), firstValue));

                if (hasMultipleValues)
                    multiValue.SetMultiValue();
                else
                    multiValue.SetSingleValue(firstValue);
            }
        }

        private void UpdateX() => UpdateMultiProperty(X, r => r.X);
        private void UpdateY() => UpdateMultiProperty(Y, r => r.Y);
        private void UpdateWidth() => UpdateMultiProperty(Width, r => r.Width);
        private void UpdateHeight() => UpdateMultiProperty(Height, r => r.Height);

        private void UpdateAllowRotation() => UpdateMultiProperty(AllowRotation, r => r.AllowRotation);
        private void UpdateAllowHorizontalMirroring() => UpdateMultiProperty(AllowHorizontalMirroring, r => r.AllowHorizontalMirroring);
        private void UpdateAllowVerticalMirroring() => UpdateMultiProperty(AllowVerticalMirroring, r => r.AllowVerticalMirroring);

        private void UpdateHorizontalLayout() => UpdateMultiProperty(HorizontalLayout, r => r.HorizontalLayout);
        private void UpdateVerticalLayout() => UpdateMultiProperty(VerticalLayout, r => r.VerticalLayout);
        private void UpdateSnapWidth() => UpdateMultiProperty(SnapWidth, r => r.SnapWidth);
        private void UpdateSnapHeight() => UpdateMultiProperty(SnapHeight, r => r.SnapHeight);

        private void UpdateSelectionWeight() => UpdateMultiProperty(SelectionWeight, r => r.SelectionWeight);

        private void UpdateIsTopConcave() => UpdateMultiProperty(IsTopConcave, r => r.IsTopConcave);
        private void UpdateIsRightConcave() => UpdateMultiProperty(IsRightConcave, r => r.IsRightConcave);
        private void UpdateIsBottomConcave() => UpdateMultiProperty(IsBottomConcave, r => r.IsBottomConcave);
        private void UpdateIsLeftConcave() => UpdateMultiProperty(IsLeftConcave, r => r.IsLeftConcave);

        private void UpdateLabels() => UpdateMultiProperty(Labels, r => r.Labels);
    }
}
