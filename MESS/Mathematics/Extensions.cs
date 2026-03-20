using MESS.Mathematics.Spatial;

namespace MESS.Mathematics
{
    static class Extensions
    {
        public static double ToRadians(this double degrees) => degrees / 180.0 * Math.PI;

        public static double ToDegrees(this double radians) => radians / Math.PI * 180.0;


        /// <summary>
        /// Turns Euler angles into a rotation matrix. The angles are assumed to be in degrees.
        /// </summary>
        public static Matrix3x3 ToMatrix(this Angles eulerAngles, bool invertedPitch = false)
        {
            var x = eulerAngles.Roll.ToRadians();
            var y = eulerAngles.Pitch.ToRadians();
            var z = eulerAngles.Yaw.ToRadians();

            if (invertedPitch)
                y = -y;

            return new Matrix3x3(
                Math.Cos(y) * Math.Cos(z),
                Math.Sin(x) * Math.Sin(y) * Math.Cos(z) - Math.Cos(x) * Math.Sin(z),
                Math.Cos(x) * Math.Sin(y) * Math.Cos(z) + Math.Sin(x) * Math.Sin(z),

                Math.Cos(y) * Math.Sin(z),
                Math.Sin(x) * Math.Sin(y) * Math.Sin(z) + Math.Cos(x) * Math.Cos(z),
                Math.Cos(x) * Math.Sin(y) * Math.Sin(z) - Math.Sin(x) * Math.Cos(z),

                -Math.Sin(y),
                Math.Sin(x) * Math.Cos(y),
                Math.Cos(x) * Math.Cos(y)
            );
        }

        /// <summary>
        /// Turns a rotation matrix into Euler angles. Note that there are multiple possible angles,
        /// and that this approach can suffer from Gimbal lock.
        /// </summary>
        public static Angles ToAngles(this Matrix3x3 matrix, bool invertedPitch = false)
        {
            // NOTE: There are always multiple solutions!
            var m = matrix;
            if (m.r31 != 1) // TODO: With some epsilon!
            {
                var y = -Math.Asin(m.r31);
                var x = Math.Atan2(m.r32 / Math.Cos(y), m.r33 / Math.Cos(y));
                var z = Math.Atan2(m.r21 / Math.Cos(y), m.r11 / Math.Cos(y));
                return new Angles(x.ToDegrees(), invertedPitch ? -y.ToDegrees() : y.ToDegrees(), z.ToDegrees());

                // Alternate solution:
                //var y2 = Math.PI - y;
                //var x2 = Math.Atan2(m.r32 / Math.Cos(y2), m.r33 / Math.Cos(y2));
                //var z2 = Math.Atan2(m.r21 / Math.Cos(y2), m.r11 / Math.Cos(y2));
                //return new Angles(y2.ToDegrees(), z2.ToDegrees(), x2.ToDegrees());
            }
            else
            {
                // Gimbal lock:
                var z = 0.0;
                if (m.r31 == -1)
                {
                    var y = Math.PI / 2;
                    var x = z + Math.Atan2(m.r12, m.r13);
                    return new Angles(x.ToDegrees(), invertedPitch ? -y.ToDegrees() : y.ToDegrees(), z.ToDegrees());
                }
                else
                {
                    var y = -Math.PI / 2;
                    var x = -z + Math.Atan2(-m.r12, -m.r13);
                    return new Angles(x.ToDegrees(), invertedPitch ? -y.ToDegrees() : y.ToDegrees(), z.ToDegrees());
                }
            }
        }


        /// <summary>
        /// Rotates this vector around the given axis. The angle is in radians.
        /// </summary>
        public static Vector3D RotateAroundAxis(this Vector3D vector, Vector3D axis, double angle)
        {
            return (vector * Math.Cos(angle)) +
                (axis.CrossProduct(vector) * Math.Sin(angle)) +
                (axis * axis.DotProduct(vector) * (1 - Math.Cos(angle)));
        }
    }
}
