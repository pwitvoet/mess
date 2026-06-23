using Avalonia.Controls;
using Avalonia.Input;
using HotspotMaker.Util.UI;

namespace HotspotMaker.Hotspot;

public partial class HotspotProjectView : UserControl
{
    public HotspotProjectView()
    {
        InitializeComponent();
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        EditorView.HandleKeyDown(e);
    }


    private void TextBox_LostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            FocusTracking.ReportFocusLoss(textBox, TextBox.TextProperty);
    }
}