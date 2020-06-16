using System;
using System.Collections.Generic;

namespace MESS.Spatial
{
    public struct BoundingBox
    {
        public static BoundingBox FromPoints(IEnumerable<Vector3D> points)
        {
            var min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);
            foreach (var point in points)
            {
                min.X = Math.Min(min.X, point.X);
                min.Y = Math.Min(min.Y, point.Y);
                min.Z = Math.Min(min.Z, point.Z);

                max.X = Math.Min(max.X, point.X);
                max.Y = Math.Min(max.Y, point.Y);
                max.Z = Math.Min(max.Z, point.Z);
            }
            return new BoundingBox(min, max);
        }


        public Vector3D Min;
        public Vector3D Max;

        public BoundingBox(Vector3D min, Vector3D max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(Vector3D point)
        {
            return point.X >= Min.X && point.Y >= Min.Y && point.Z >= Min.Z &&
                   point.X <= Max.X && point.Y <= Max.Y && point.Z <= Max.Z;
        }

        public bool Contains(BoundingBox other) => Contains(other.Min) && Contains(other.Max);
    }
}
