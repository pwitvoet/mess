using Avalonia.Platform.Storage;
using HotspotMaker.Controls;
using HotspotMaker.Hotspot;
using MLib.Texturing.Hotspotting;
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


        // Bindable properties:
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
                if (_hotspotProject != null)
                    _hotspotProject.PropertyChanged -= HotspotProject_PropertyChanged;

                _hotspotProject = value;

                if (_hotspotProject != null)
                    _hotspotProject.PropertyChanged += HotspotProject_PropertyChanged;

                UpdateWindowTitle(value);
                RaisePropertyChanged(nameof(HasOpenProject));
                RaisePropertyChanged();
            }
        }


        // Derived properties:
        public bool HasOpenProject => HotspotProject != null;

        public bool IsUndoAvailable => HotspotProject?.IsUndoAvailable == true;

        public bool IsRedoAvailable => HotspotProject?.IsRedoAvailable == true;


        private IStorageProvider StorageProvider { get; }


        public MainWindowVM(IStorageProvider storageProvider)
        {
            StorageProvider = storageProvider;

            UpdateWindowTitle(null);
        }


        // Commands:
        public async Task OpenWadFile()
        {
            if (HotspotProject != null)
            {
                await CloseCurrentProject();
                if (HotspotProject != null)
                    return;
            }

            try
            {
                // TODO: Remember the previously opened file(s), and open the most recent folder (SuggestedStartLocation)!
                var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
                // TODO: Improve error message!
                await MessageBox.Show("Error", $"Failed to open project: {ex.GetType().Name}: {ex.Message}.", MessageBoxButtons.Ok);
            }
        }

        public async Task SaveCurrentProject()
        {
            try
            {
                if (HotspotProject == null)
                    return;

                var hotspotFileData = HotspotProject.CreateHotspotFileData();
                HotspotFileWriter.Save(HotspotProject.HotspotFilePath, hotspotFileData);

                HotspotProject.MarkAsUnmodified();
            }
            catch (Exception ex)
            {
                // TODO: Improve error message!
                await MessageBox.Show("Error", $"Failed to save project: {ex.GetType().Name}: {ex.Message}.", MessageBoxButtons.Ok);
            }
        }

        public async Task CloseCurrentProject()
        {
            if (HotspotProject == null)
                return;

            if (HotspotProject.IsModified)
            {
                var confirmation = await MessageBox.Show("Unsaved changes", "You have unsaved changes. Are you sure you want to close the project without saving?", MessageBoxButtons.OkCancel);
                if (confirmation != true)
                    return;
            }

            // TODO: This does not erase/reset the editor VM state or the view!
            HotspotProject = null;
        }

        public async Task ExitProgram()
        {
            if (HotspotProject != null)
            {
                await CloseCurrentProject();
                if (HotspotProject != null)
                    return;
            }

            Environment.Exit(0);
        }

        public void UndoLastAction()
            => HotspotProject?.UndoLastAction();

        public void RedoLastAction()
            => HotspotProject?.RedoLastAction();


        private void HotspotProject_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HotspotProjectVM.IsUndoAvailable))
                RaisePropertyChanged(nameof(IsUndoAvailable));
            else if (e.PropertyName == nameof(HotspotProjectVM.IsRedoAvailable))
                RaisePropertyChanged(nameof(IsRedoAvailable));
            else if (e.PropertyName == nameof(HotspotProjectVM.IsModified))
                UpdateWindowTitle(HotspotProject);
        }

        private void UpdateWindowTitle(HotspotProjectVM? hotspotProject)
        {
            if (hotspotProject == null)
            {
                WindowTitle = DefaultWindowTitle;
            }
            else
            {
                WindowTitle = $"{DefaultWindowTitle} - {hotspotProject.HotspotFilePath}{(hotspotProject.IsModified ? " *" : "")}";
            }
        }
    }
}
