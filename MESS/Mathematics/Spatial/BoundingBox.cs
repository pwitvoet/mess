using System;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Mathematics.Spatial
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

                max.X = Math.Max(max.X, point.X);
                max.Y = Math.Max(max.Y, point.Y);
                max.Z = Math.Max(max.Z, point.Z);
            }
            return new BoundingBox(min, max);
        }

        public static BoundingBox FromBoundingBoxes(IEnumerable<BoundingBox> boundingBoxes) => FromPoints(boundingBoxes.Select(bb => bb.Min).Concat(boundingBoxes.Select(bb => bb.Max)));


        public Vector3D Min;
        public Vector3D Max;

        public Vector3D Center => (Min + Max) / 2;


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

        public bool Touches(BoundingBox other)
        {
            return Min.X < other.Max.X && Max.X > other.Min.X &&
                Min.Y < other.Max.Y && Max.Y > other.Min.Y &&
                Min.Z < other.Max.Z && Max.Z > other.Min.Z;
        }
    }
}
