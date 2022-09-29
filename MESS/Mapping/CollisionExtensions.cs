using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    public static class CollisionExtensions
    {
        public static bool Touches(this Brush brush, Brush otherBrush)
        {
            if (!brush.BoundingBox.Touches(otherBrush.BoundingBox))
                return false;

            // Two brushes are touching if no plane exists that separates them (hyperplane separation theorem):
            if (brush.Faces.Any(face => AllPointsOutside(face.Plane, otherBrush)) || otherBrush.Faces.Any(face => AllPointsOutside(face.Plane, brush)))
                return false;

            return true;

            bool AllPointsOutside(Plane plane, Brush brsh)
            {
                foreach (var bface in brsh.Faces)
                    if (bface.Vertices.Any(vertex => plane.Normal.DotProduct(vertex) < plane.Distance))
                        return false;

                return true;
            }
        }

        public static bool Touches(this Brush brush, Entity entity)
        {
            if (entity.IsPointBased)
                return brush.Contains(entity);
            else
                return entity.Brushes.Any(entityBrush => brush.Touches(entityBrush));
        }

        public static bool Touches(this Entity entity, Brush brush) => brush.Touches(entity);

        public static bool Touches(this Entity entity, Entity otherEntity)
        {
            if (!entity.IsPointBased)
                return entity.Brushes.Any(brush => brush.Touches(otherEntity));
            else if (!otherEntity.IsPointBased)
                return otherEntity.Brushes.Any(brush => brush.Touches(entity));
            else
                return entity.Origin == otherEntity.Origin;   // TODO: Delta comparison?
        }


        public static bool Contains(this BoundingBox boundingBox, Brush brush) => boundingBox.Contains(brush.BoundingBox);

        public static bool Contains(this BoundingBox boundingBox, Entity entity) => entity.IsPointBased ? boundingBox.Contains(entity.Origin) : boundingBox.Contains(entity.BoundingBox);

        public static bool Contains(this Brush brush, Brush otherBrush)
        {
            if (!brush.BoundingBox.Contains(otherBrush.BoundingBox))
                return false;

            // TODO: This does a lot of duplicate work, because each face contains its own vertex copies!
            foreach (var face in otherBrush.Faces)
                foreach (var vertex in face.Vertices)
                    if (!brush.Contains(vertex))
                        return false;

            return true;
        }

        public static bool Contains(this Brush brush, Entity entity)
        {
            if (entity.IsPointBased)
                return brush.Contains(entity.Origin);
            else
                return entity.Brushes.All(entityBrush => brush.Contains(entityBrush));
        }

        public static bool Contains(this Brush brush, Vector3D point, float epsilon = 0)
        {
            // A point lies inside a brush if it's on the inside of every face plane:
            foreach (var face in brush.Faces)
            {
                var plane = face.Plane;
                if (plane.Normal.DotProduct(point) > plane.Distance + epsilon)
                    return false;
            }

            return true;
        }

        // NOTE: Checking whether an entity - which can consist of multiple brushes - contains a brush is difficult:
        //       the other object may not be fully contained by any of the entity's brushes, but still be fully contained by several brushes together!
    }
}
