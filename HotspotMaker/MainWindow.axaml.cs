using Avalonia.Controls;
using Avalonia.Input;

namespace HotspotMaker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowVM(StorageProvider);
        }


        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // To improve the user experience, the editor view needs to handle certain keys regardless of whether it has focus:
            ProjectView.HandleKeyDown(e);
        }
    }
}