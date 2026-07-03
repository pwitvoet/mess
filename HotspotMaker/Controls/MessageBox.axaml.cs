using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace HotspotMaker.Controls
{
    [Flags]
    public enum MessageBoxButtons
    {
        None =      0x00,

        Ok =        0x01,
        Cancel =    0x02,

        OkCancel = Ok | Cancel,
    }


    public partial class MessageBox : Window
    {
        public static async Task<bool?> Show(string title, string message, MessageBoxButtons buttons = MessageBoxButtons.OkCancel)
        {
            var dialog = new MessageBox(title, message, buttons);

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null)
                return null;

            dialog.Position = new PixelPoint(
                mainWindow.Position.X + (int)((mainWindow.Width - dialog.Width) / 2),
                mainWindow.Position.Y + (int)((mainWindow.Height - dialog.Height) / 2));

            await dialog.ShowDialog(mainWindow);
            return dialog.Result;
        }


        private bool? Result { get; set; }

        public MessageBox(string title, string message, MessageBoxButtons buttons)
        {
            InitializeComponent();

            Title = title;
            MessageTextBlock.Text = message;

            OkButton.IsVisible = buttons.HasFlag(MessageBoxButtons.Ok);
            CancelButton.IsVisible = buttons.HasFlag(MessageBoxButtons.Cancel);
        }


        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Enter && OkButton.IsVisible)
            {
                Result = true;
                Close();
            }
            else if (e.Key == Key.Escape && CancelButton.IsVisible)
            {
                Result = false;
                Close();
            }
        }


        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}