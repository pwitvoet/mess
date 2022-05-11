using MESS.Mathematics.Spatial;
using System;

namespace MESS.Mathematics
{
    static class Extensions
    {
        public static float ToRadians(this float degrees) => (float)(degrees / 180.0 * Math.PI);

        public static float ToDegrees(this float radians) => (float)(radians / Math.PI * 180.0);


        /// <summary>
        /// Turns Euler angles into a rotation matrix. The angles are assumed to be in degrees.
        /// </summary>
        public static Matrix3x3 ToMatrix(this Angles eulerAngles)
        {
            var x = eulerAngles.Roll.ToRadians();
            var y = -eulerAngles.Pitch.ToRadians();
            var z = eulerAngles.Yaw.ToRadians();

            return new Matrix3x3(
                (float)(Math.Cos(y) * Math.Cos(z)),
                (float)(Math.Sin(x) * Math.Sin(y) * Math.Cos(z) - Math.Cos(x) * Math.Sin(z)),
                (float)(Math.Cos(x) * Math.Sin(y) * Math.Cos(z) + Math.Sin(x) * Math.Sin(z)),

                (float)(Math.Cos(y) * Math.Sin(z)),
                (float)(Math.Sin(x) * Math.Sin(y) * Math.Sin(z) + Math.Cos(x) * Math.Cos(z)),
                (float)(Math.Cos(x) * Math.Sin(y) * Math.Sin(z) - Math.Sin(x) * Math.Cos(z)),

                (float)(-Math.Sin(y)),
                (float)(Math.Sin(x) * Math.Cos(y)),
                (float)(Math.Cos(x) * Math.Cos(y))
            );
        }

        /// <summary>
        /// Turns a rotation matrix into Euler angles. Note that there are multiple possible angles,
        /// and that this approach can suffer from Gimbal lock.
        /// </summary>
        public static Angles ToAngles(this Matrix3x3 matrix)
        {
            // NOTE: There are always multiple solutions!
            var m = matrix;
            if (m.r31 != 1) // TODO: With some epsilon!
            {
                var y = (float)-Math.Asin(m.r31);
                var x = (float)Math.Atan2(m.r32 / Math.Cos(y), m.r33 / Math.Cos(y));
                var z = (float)Math.Atan2(m.r21 / Math.Cos(y), m.r11 / Math.Cos(y));
                return new Angles(x.ToDegrees(), -y.ToDegrees(), z.ToDegrees());

                // Alternate solution:
                //var y2 = (float)Math.PI - y;
                //var x2 = (float)Math.Atan2(m.r32 / Math.Cos(y2), m.r33 / Math.Cos(y2));
                //var z2 = (float)Math.Atan2(m.r21 / Math.Cos(y2), m.r11 / Math.Cos(y2));
                //return new Angles(y2.ToDegrees(), z2.ToDegrees(), x2.ToDegrees());
            }
            else
            {
                // Gimbal lock:
                var z = 0f;
                if (m.r31 == -1)
                {
                    var y = (float)Math.PI / 2;
                    var x = z + (float)Math.Atan2(m.r12, m.r13);
                    return new Angles(x.ToDegrees(), -y.ToDegrees(), z.ToDegrees());
                }
                else
                {
                    var y = (float)-Math.PI / 2;
                    var x = -z + (float)Math.Atan2(-m.r12, -m.r13);
                    return new Angles(x.ToDegrees(), -y.ToDegrees(), z.ToDegrees());
                }
            }
        }
    }
}
