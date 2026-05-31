using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MLib.Texturing;
using MLib.Texturing.Hotspotting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HotspotMaker.Hotspot
{
    public class HotspotProjectVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        // Bindable properties:
        private TextureInfoVM? _selectedTextureInfo;
        public TextureInfoVM? SelectedTextureInfo
        {
            get => _selectedTextureInfo;
            set
            {
                _selectedTextureInfo = value;
                RaisePropertyChanged();

                OnSelectedTextureUpdate(value);
            }
        }

        private Bitmap? _selectedTextureImage;
        public Bitmap? SelectedTextureImage
        {
            get => _selectedTextureImage;
            set { _selectedTextureImage = value; RaisePropertyChanged(); }
        }

        private HotspotBindingVM? _selectedHotspotBinding;
        public HotspotBindingVM? SelectedHotspotBinding
        {
            get => _selectedHotspotBinding;
            set
            {
                _selectedHotspotBinding = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasSelectedHotspotBinding));
            }
        }

        private HotspotRectangleSetVM? _selectedHotspotRectangleSet;
        public HotspotRectangleSetVM? SelectedHotspotRectangleSet
        {
            get => _selectedHotspotRectangleSet;
            set { _selectedHotspotRectangleSet = value; RaisePropertyChanged(); }
        }

        private HotspotRectangleVM? _selectedHotspotRectangle;
        public HotspotRectangleVM? SelectedHotspotRectangle
        {
            get => _selectedHotspotRectangle;
            set { _selectedHotspotRectangle = value; RaisePropertyChanged(); }
        }

        // Derived properties:
        public string WadFilePath => WadFile.FilePath;

        public string HotspotFilePath => HotspotFile.FilePath;

        public TextureInfoVM[] TextureInfos { get; }

        public List<HotspotRectangleSetVM> HotspotRectangleSets => HotspotFile.HotspotRectangleSets;

        public bool HasSelectedHotspotBinding => SelectedHotspotBinding != null;


        // Internal state:
        private WadFile WadFile { get; }
        private HotspotFile HotspotFile { get; }

        private Dictionary<string, HotspotBindingVM> ExactBindings { get; } = new Dictionary<string, HotspotBindingVM>(StringComparer.InvariantCultureIgnoreCase);
        private List<(Regex, HotspotBindingVM)> WildcardHotspotBindings { get; } = new();


        public HotspotProjectVM(WadFile wadFile, HotspotFile hotspotFile)
        {
            WadFile = wadFile;
            HotspotFile = hotspotFile;

            // Initialize binding lookups:
            foreach (var binding in hotspotFile.HotspotBindings)
            {
                var regex = HotspotDataCollection.GetTextureNamePatternRegex(binding.TextureNamePattern);
                if (regex == null)
                {
                    // TODO: Handle duplicate names!
                    ExactBindings[binding.TextureNamePattern] = binding;
                }
                else
                {
                    WildcardHotspotBindings.Add((regex, binding));
                }
            }

            // Initialize texture infos:
            TextureInfos = wadFile.TextureInfos
                .Select(textureInfo => new TextureInfoVM(textureInfo) { Binding = GetBindingForTexture(textureInfo.Name) })
                .OrderBy(entry => entry.Name)
                .ToArray();
        }


        // TODO: Improve error reporting!
        public static HotspotProjectVM Load(string wadFilePath, string hotspotFilePath)
        {
            WadFile wadFile;
            try
            {
                wadFile = WadFile.Load(wadFilePath);
            }
            catch (Exception ex)
            {
                // TODO: Wrap this in an exception that explains that the wad loading part failed!
                throw;
            }

            HotspotFile hotspotFile;
            try
            {
                if (File.Exists(hotspotFilePath))
                {
                    hotspotFile = HotspotFile.Load(hotspotFilePath);
                }
                else
                {
                    hotspotFile = new HotspotFile(hotspotFilePath, Array.Empty<HotspotRectangleSetVM>(), Array.Empty<HotspotBindingVM>());
                }
            }
            catch (Exception ex)
            {
                // TODO: Wrap this in an exception that explains that the hotspot loading part failed!
                throw;
            }

            return new HotspotProjectVM(wadFile, hotspotFile);
        }


        private void OnSelectedTextureUpdate(TextureInfoVM? textureItem)
        {
            if (textureItem == null)
            {
                SelectedTextureImage = null;
                SelectedHotspotBinding = null;
                SelectedHotspotRectangleSet = null;
            }
            else
            {
                var texture = WadFile.LoadTexture(textureItem.TextureInfo);
                SelectedTextureImage = CreateBitmapFromTexture(texture);
                SelectedHotspotBinding = textureItem.Binding;
                SelectedHotspotRectangleSet = textureItem.Binding != null ? HotspotFile.HotspotRectangleSets.FirstOrDefault(rectangleSet => rectangleSet.Name == textureItem.Binding.HotspotName) : null;
            }
        }

        private Bitmap CreateBitmapFromTexture(Texture texture)
        {
            var bitmap = new WriteableBitmap(new PixelSize(texture.Width, texture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var buffer = bitmap.Lock())
            {
                var isTransparent = texture.Name.StartsWith('{');

                for (int y = 0; y < texture.Height; y++)
                {
                    var row = new byte[buffer.RowBytes];
                    for (int x = 0; x < texture.Width; x++)
                    {
                        var index = texture.ImageData[y * texture.Width + x];
                        var color = texture.Palette[index];
                        row[x * 4] = color.R;
                        row[x * 4 + 1] = color.G;
                        row[x * 4 + 2] = color.B;
                        row[x * 4 + 3] = (byte)(isTransparent && index == 255 ? 0 : 255);
                    }
                    Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, buffer.RowBytes);
                }
            }
            return bitmap;
        }

        private HotspotBindingVM? GetBindingForTexture(string textureName)
        {
            if (ExactBindings.TryGetValue(textureName, out var exactBinding))
                return exactBinding;

            foreach ((var regex, var binding) in WildcardHotspotBindings)
            {
                if (regex.IsMatch(textureName))
                    return binding;
            }

            return null;
        }
    }
}
