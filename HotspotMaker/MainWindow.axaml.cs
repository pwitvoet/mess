using Avalonia.Controls;
using Avalonia.Input;

namespace HotspotMaker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowVM(StorageProvider, Clipboard);
        }


        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // To improve the user experience, the editor view needs to handle certain keys regardless of whether it has focus.

            // NOTE: This hack ensures that our key-down handling won't gobble up certain keys when writing in a TextBox:
            if (e.Source is not TextBox)
                ProjectView.HandleKeyDown(e);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is MainWindowVM mainWindowVM && mainWindowVM.HotspotProject?.IsModified == true)
            {
                // Let the VM warn the user about unsaved changes:
                e.Cancel = true;
                mainWindowVM.ExitProgram();
            }
        }
    }
}