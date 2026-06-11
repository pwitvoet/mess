using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HotspotMaker.History;
using MLib.Texturing;
using MLib.Texturing.Hotspotting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HotspotMaker.Hotspot
{
    public class HotspotProjectVM : ChangeTrackingVM
    {
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

            HotspotFileData hotspotFileData;
            try
            {
                if (File.Exists(hotspotFilePath))
                {
                    hotspotFileData = HotspotFileParser.Load(hotspotFilePath);
                }
                else
                {
                    hotspotFileData = new HotspotFileData(Array.Empty<HotspotRectangleSet>(), Array.Empty<HotspotBinding>());
                }
            }
            catch (Exception ex)
            {
                // TODO: Wrap this in an exception that explains that the hotspot loading part failed!
                throw;
            }

            return new HotspotProjectVM(wadFile, hotspotFileData, hotspotFilePath);
        }


        // Bindable properties:
        private TextureInfoVM? _selectedTextureInfo;
        public TextureInfoVM? SelectedTextureInfo
        {
            get => _selectedTextureInfo;
            set
            {
                _selectedTextureInfo = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasSelectedTextureInfo));

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
            set
            {
                _selectedHotspotRectangleSet = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasSelectedHotspotRectangleSet));
            }
        }

        private HotspotRectangleVM? _selectedHotspotRectangle;
        public HotspotRectangleVM? SelectedHotspotRectangle
        {
            get => _selectedHotspotRectangle;
            set
            {
                _selectedHotspotRectangle = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasSelectedHotspotRectangle));
            }
        }

        public ObservableCollection<HotspotBindingVM> HotspotBindings { get; } = new();

        public ObservableCollection<HotspotRectangleSetVM> HotspotRectangleSets { get; } = new();


        // Derived properties:
        public string WadFilePath => WadFile.FilePath;

        public TextureInfoVM[] TextureInfos { get; }

        public bool HasSelectedTextureInfo => SelectedTextureInfo != null;

        public bool HasSelectedHotspotBinding => SelectedHotspotBinding != null;

        public bool HasSelectedHotspotRectangleSet => SelectedHotspotRectangleSet != null;

        public bool HasSelectedHotspotRectangle => SelectedHotspotRectangle != null;

        public bool IsUndoAvailable => UndoSystem.IsUndoAvailable;

        public bool IsRedoAvailable => UndoSystem.IsRedoAvailable;


        // Read-only:
        public string HotspotFilePath { get; }


        // Internal state:
        private WadFile WadFile { get; }
        private Dictionary<string, HotspotBindingVM> ExactBindings { get; } = new Dictionary<string, HotspotBindingVM>(StringComparer.InvariantCultureIgnoreCase);
        private List<(Regex, HotspotBindingVM)> WildcardHotspotBindings { get; } = new();


        public HotspotProjectVM(WadFile wadFile, HotspotFileData hotspotFileData, string hotspotFilePath)
            : base(new UndoSystem())
        {
            WadFile = wadFile;
            HotspotFilePath = hotspotFilePath;

            foreach (var binding in hotspotFileData.Bindings)
            {
                var bindingVM = new HotspotBindingVM(binding, UndoSystem);
                HotspotBindings.Add(bindingVM);

                // Register binding lookup:
                var regex = HotspotDataCollection.GetTextureNamePatternRegex(bindingVM.TextureNamePattern);
                if (regex == null)
                {
                    // TODO: Handle duplicate names!
                    ExactBindings[bindingVM.TextureNamePattern] = bindingVM;
                }
                else
                {
                    WildcardHotspotBindings.Add((regex, bindingVM));
                }
            }

            foreach (var rectangleSet in hotspotFileData.RectangleSets)
                HotspotRectangleSets.Add(new HotspotRectangleSetVM(rectangleSet, UndoSystem));

            // Initialize texture infos:
            TextureInfos = wadFile.TextureInfos
                .Select(textureInfo => new TextureInfoVM(textureInfo, GetBindingForTexture(textureInfo.Name)))
                .OrderBy(entry => entry.Name)
                .ToArray();

            UndoSystem.OnActionDone += UndoSystem_OnActionDone;
            UndoSystem.OnActionUndone += UndoSystem_OnActionUndone;
            UndoSystem.OnActionRedone += UndoSystem_OnActionRedone;
        }

        public void UndoLastAction()
            => UndoSystem.UndoLastAction();

        public void RedoLastAction()
            => UndoSystem.RedoLastAction();


        private void UndoSystem_OnActionDone()
        {
            RaisePropertyChanged(nameof(IsUndoAvailable));
            RaisePropertyChanged(nameof(IsRedoAvailable));
        }

        private void UndoSystem_OnActionUndone()
        {
            RaisePropertyChanged(nameof(IsUndoAvailable));
            RaisePropertyChanged(nameof(IsRedoAvailable));
        }

        private void UndoSystem_OnActionRedone()
        {
            RaisePropertyChanged(nameof(IsUndoAvailable));
            RaisePropertyChanged(nameof(IsRedoAvailable));
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
                SelectedHotspotRectangleSet = GetHotspotRectangleSet(textureItem.Binding?.HotspotName);
            }
        }

        private HotspotRectangleSetVM? GetHotspotRectangleSet(string? hotspotName)
            => hotspotName != null ? HotspotRectangleSets.FirstOrDefault(rectangleSet => string.Equals(rectangleSet.Name, hotspotName, StringComparison.InvariantCultureIgnoreCase)) : null;

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
