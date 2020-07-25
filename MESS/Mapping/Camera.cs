using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    /// <summary>
    /// Cameras are used by the editor.
    /// </summary>
    public class Camera
    {
        public Vector3D EyePosition { get; set; }
        public Vector3D LookAtPosition { get; set; }
    }
}
