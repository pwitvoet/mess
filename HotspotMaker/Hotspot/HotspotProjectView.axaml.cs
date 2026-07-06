using Avalonia.Controls;
using Avalonia.Input;
using HotspotMaker.Controls;
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
        switch (sender)
        {
            case TextBox textBox:
                FocusTracking.ReportFocusLoss(textBox, TextBox.TextProperty);
                break;

            case LabelsTextBox labelsTextBox:
                FocusTracking.ReportFocusLoss(labelsTextBox, LabelsTextBox.LabelsProperty);
                break;
        }
    }
}