using MESS.Mathematics.Spatial;

namespace MESS.Mathematics
{
    public class Transform
    {
        public static Transform Identity => new Transform(1, new Vector3D(1, 1, 1), Matrix3x3.Identity, new Vector3D());


        public float Scale;
        public Vector3D GeometryScale;
        public Matrix3x3 Rotation;
        public Vector3D Offset;

        public Transform(float scale, Vector3D geometryScale, Matrix3x3 rotation, Vector3D offset)
        {
            Scale = scale;
            GeometryScale = geometryScale;
            Rotation = rotation;
            Offset = offset;
        }


        public override string ToString() => $"(scale: {Scale}, geometry scale: {GeometryScale}, rotation: {Rotation}, offset: {Offset})";
    }
}
