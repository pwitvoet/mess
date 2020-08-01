using MESS.Mathematics.Spatial;

namespace MESS.Mathematics
{
    public struct Matrix3x3
    {
        public static Matrix3x3 Identity => new Matrix3x3(1, 0, 0,    0, 1, 0,    0, 0, 1);


        public float r11, r12, r13;
        public float r21, r22, r23;
        public float r31, r32, r33;

        public Matrix3x3(float r11, float r12, float r13,
                         float r21, float r22, float r23,
                         float r31, float r32, float r33)
        {
            this.r11 = r11;
            this.r12 = r12;
            this.r13 = r13;

            this.r21 = r21;
            this.r22 = r22;
            this.r23 = r23;

            this.r31 = r31;
            this.r32 = r32;
            this.r33 = r33;
        }

        public override string ToString() => $"{r11}, {r12}, {r13}\n{r21}, {r22}, {r23}\n{r31}, {r32}, {r33}";


        public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
        {
            return new Matrix3x3(
                a.r11 * b.r11 + a.r12 * b.r21 + a.r13 * b.r31,
                a.r11 * b.r12 + a.r12 * b.r22 + a.r13 * b.r32,
                a.r11 * b.r13 + a.r12 * b.r23 + a.r13 * b.r33,

                a.r21 * b.r11 + a.r22 * b.r21 + a.r23 * b.r31,
                a.r21 * b.r12 + a.r22 * b.r22 + a.r23 * b.r32,
                a.r21 * b.r13 + a.r22 * b.r23 + a.r23 * b.r33,

                a.r31 * b.r11 + a.r32 * b.r21 + a.r33 * b.r31,
                a.r31 * b.r12 + a.r32 * b.r22 + a.r33 * b.r32,
                a.r31 * b.r13 + a.r32 * b.r23 + a.r33 * b.r33
            );
        }

        public static Vector3D operator *(Matrix3x3 a, Vector3D b)
        {
            return new Vector3D(
                a.r11 * b.X + a.r12 * b.Y + a.r13 * b.Z,
                a.r21 * b.X + a.r22 * b.Y + a.r23 * b.Z,
                a.r31 * b.X + a.r32 * b.Y + a.r33 * b.Z
            );
        }
    }
}
