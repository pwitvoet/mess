using Avalonia.Media;
using HotspotMaker.History;
using HotspotMaker.Hotspot;
using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HotspotMaker.Editor
{
    public class HotspotEditorVM : ChangeTrackingVM
    {
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

                _rectangleSet = value;

                if (_rectangleSet != null)
                {
                    foreach (var rectangleVM in _rectangleSet.Rectangles)
                        rectangleVM.PropertyChanged += Rectangle_PropertyChanged;

                    _rectangleSet.Rectangles.CollectionChanged += Rectangles_CollectionChanged;
                }

                RaisePropertyChanged();
            }
        }

        private HotspotRectangleVM? _selectedRectangle;
        public HotspotRectangleVM? SelectedRectangle
        {
            get => _selectedRectangle;
            set { _selectedRectangle = value; RaisePropertyChanged(); }
        }


        public HotspotEditorVM(UndoSystem undoSystem)
            : base(undoSystem)
        {
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
        }

        private void Rectangle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is HotspotRectangleVM rectangleVM)
                RaiseRectanglePropertyChanged(rectangleVM, e.PropertyName);
        }
    }
}
