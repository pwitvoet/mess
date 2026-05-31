using Avalonia.Controls;

namespace HotspotMaker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowVM(StorageProvider);
        }
    }
}