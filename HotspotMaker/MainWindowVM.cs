using Avalonia;
using Avalonia.Platform.Storage;
using HotspotMaker.Hotspot;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HotspotMaker
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        private const string DefaultWindowTitle = "HotspotMaker";


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        private string _windowTitle = DefaultWindowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; RaisePropertyChanged(); }
        }

        private HotspotProjectVM? _hotspotProject;
        public HotspotProjectVM? HotspotProject
        {
            get => _hotspotProject;
            set
            {
                _hotspotProject = value;
                UpdateWindowTitle(value);
                RaisePropertyChanged();
            }
        }


        private IStorageProvider StorageProvider { get; }


        public MainWindowVM(IStorageProvider storageProvider)
        {
            StorageProvider = storageProvider;
            UpdateWindowTitle(null);
        }


        // Commands:
        public async Task OpenWadFile()
        {
            try
            {
                // TODO: Remember the previously opened file(s), and open the most recent folder (SuggestedStartLocation)!
                var selectedFiles = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Open .wad file",
                    FileTypeFilter = [new FilePickerFileType("Wad file") { Patterns = ["*.wad"] }],
                    AllowMultiple = false,
                });

                if (selectedFiles.Any())
                {
                    var wadFilePath = selectedFiles.First().Path.LocalPath;
                    var hotspotFilePath = wadFilePath + ".hotspot";
                    HotspotProject = HotspotProjectVM.Load((string)wadFilePath, (string)hotspotFilePath);
                }
            }
            catch (Exception ex)
            {
                // TODO: Show error message!
            }
        }

        public void SaveCurrentProject()
        {
            // TODO!
        }

        public void CloseCurrentProject()
        {
            // TODO: Check if current project has unsaved changes!
        }

        public void ExitProgram()
        {
            // TODO: Check if current project has unsaved changes!
            Environment.Exit(0);
        }


        private void UpdateWindowTitle(HotspotProjectVM? hotspotProject)
        {
            if (hotspotProject == null)
            {
                WindowTitle = DefaultWindowTitle;
            }
            else
            {
                WindowTitle = $"{DefaultWindowTitle} - {hotspotProject.HotspotFilePath}";
            }
        }
    }
}
