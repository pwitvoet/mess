using MESS.Mathematics.Spatial;

namespace MESS.Mathematics
{
    public class Transform
    {
        public static Transform Identity => new Transform(1, Matrix3x3.Identity, new Vector3D());


        public float Scale;
        public Matrix3x3 Rotation;
        public Vector3D Offset;

        public Transform(float scale, Matrix3x3 rotation, Vector3D offset)
        {
            Scale = scale;
            Rotation = rotation;
            Offset = offset;
        }


        public override string ToString() => $"(scale: {Scale}, rotation: {Rotation}, offset: {Offset})";
    }
}
