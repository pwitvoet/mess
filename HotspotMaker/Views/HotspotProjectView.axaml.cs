using Avalonia.Controls;
using Avalonia.Input;
using HotspotMaker.Util.UI;

namespace HotspotMaker.Views;

public partial class HotspotProjectView : UserControl
{
    public HotspotProjectView()
    {
        InitializeComponent();
    }

    private void TextBox_LostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            FocusTracking.ReportFocusLoss(textBox, TextBox.TextProperty);
    }
}