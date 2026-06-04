namespace HotspotMaker.Util.UI
{
    /// <summary>
    /// Allows view models to react to focus loss of UI elements that are bound to a specific property in that view model.
    /// </summary>
    public interface IFocusTrackingVM
    {
        void FocustLost(string propertyName);
    }
}
