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
                _binding = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasBinding));
            }
        }

        private bool _isModified;
        public bool IsModified
        {
            get => _isModified;
            set { _isModified = value; RaisePropertyChanged(); }
        }


        // Derived properties:
        public bool HasBinding => Binding != null;


        // Read-only:
        public TextureInfo TextureInfo { get; }


        public TextureInfoVM(TextureInfo textureInfo)
        {
            TextureInfo = textureInfo;
        }
    }
}
