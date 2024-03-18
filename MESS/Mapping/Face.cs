using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    /// <summary>
    /// Faces are defined by a number of vertices (RMF and JMF formats) and by the plane they're on (MAP and RMF formats).
    /// This plane is defined by 3 points, in clockwise order, as seen from the outside of the brush.
    /// If vertices are not stored, they can be derived by finding the intersections with neighboring planes.
    /// 
    /// Texture mapping is done by projecting the face onto a texture plane, which is defined by a right and down vector,
    /// and by scaling and shifting the coordinates. The texture angle is only used by the editor.
    /// </summary>
    public class Face
    {
        public List<Vector3D> Vertices { get; } = new();
        public Vector3D[] PlanePoints { get; set; } = Array.Empty<Vector3D>();
        public Plane Plane { get; set; }

        public string TextureName { get; set; } = "";
        public Vector3D TextureRightAxis { get; set; }
        public Vector3D TextureDownAxis { get; set; }
        public Vector2D TextureShift { get; set; }
        public float TextureAngle { get; set; }
        public Vector2D TextureScale { get; set; }
    }
}
