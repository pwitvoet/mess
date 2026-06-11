using MLib.Texturing;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HotspotMaker.Hotspot
{
    public class TextureInfoVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        // Bindable properties:
        public string Name => TextureInfo.Name;

        private HotspotBindingVM? _binding;
        public HotspotBindingVM? Binding
        {
            get => _binding;
            set
            {
                if (_binding != null)
                    _binding.PropertyChanged -= Binding_PropertyChanged;

                _binding = value;

                if (_binding != null)
                    _binding.PropertyChanged += Binding_PropertyChanged;

                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasBinding));
                RaisePropertyChanged(nameof(IsModified));
            }
        }


        // Derived properties:
        public bool HasBinding => Binding != null;

        // TODO: Also check if the referenced hotspot rectangle set has been modified!
        public bool IsModified => Binding != OriginalBinding || (Binding != null && Binding.IsModified);


        // Read-only:
        public TextureInfo TextureInfo { get; }

        public HotspotBindingVM? OriginalBinding { get; }


        public TextureInfoVM(TextureInfo textureInfo, HotspotBindingVM? binding)
        {
            TextureInfo = textureInfo;

            OriginalBinding = binding;
            Binding = binding;
        }


        private void Binding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HotspotBindingVM.IsModified))
                RaisePropertyChanged(nameof(IsModified));
        }
    }
}
