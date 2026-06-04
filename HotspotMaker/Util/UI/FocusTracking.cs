using Avalonia;
using Avalonia.Data;

namespace HotspotMaker.Util.UI
{
    public static class FocusTracking
    {
        // TODO: Using reflection is brittle -- so try to find a better solution for this!
        public static void ReportFocusLoss(AvaloniaObject uiElement, AvaloniaProperty property)
        {
            var binding = BindingOperations.GetBindingExpressionBase(uiElement, property);
            var leafNode = binding?.GetValue("LeafNode");
            var source = leafNode?.GetValue("Source");
            var propertyName = leafNode?.GetValue("PropertyName") as string;

            if (source is IFocusTrackingVM changeTrackingVM && propertyName != null)
                changeTrackingVM.FocustLost(propertyName);
        }
    }
}
