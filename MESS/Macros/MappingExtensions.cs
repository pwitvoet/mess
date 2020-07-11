using MESS.Mapping;
using MESS.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Macros
{
    static class MappingExtensions
    {
        /// <summary>
        /// Selects all entities with the specified class name.
        /// </summary>
        public static IEnumerable<Entity> GetEntitiesWithClassName(this Map map, string className) => map.Entities.Where(entity => entity.ClassName == className);


        /// <summary>
        /// Returns true if this brush is fully covered with the 'ORIGIN' texture. Origin brushes are used to specify the origin of certain brush entities.
        /// </summary>
        public static bool IsOriginBrush(this Brush brush) => brush?.Faces.All(face => face.TextureName?.ToUpper() == "ORIGIN") == true;

        /// <summary>
        /// For point entities, this returns the 'origin' attribute, or null if that attribute is missing.
        /// For brush entities, this returns the center of the first 'ORIGIN'-textured brush, or null if there is no such brush.
        /// </summary>
        public static Vector3D? GetOrigin(this Entity entity)
        {
            if (entity.IsPointBased)
            {
                return entity.Properties.ContainsKey("origin") ? (Vector3D?)entity.Origin : null;
            }
            else
            {
                return entity.Brushes.FirstOrDefault(IsOriginBrush)?.BoundingBox.Center;
            }
        }

        /// <summary>
        /// Creates a copy of this brush. Ignores VIS-group and group relationships.
        /// </summary>
        public static Brush Copy(this Brush brush, Vector3D offset)
        {
            return new Brush(brush.Faces.Select(CopyFace)) { Color = brush.Color };

            Face CopyFace(Face face)
            {
                var copy = new Face();

                copy.Vertices.AddRange(face.Vertices.Select(vertex => vertex + offset));
                copy.PlanePoints = face.PlanePoints.Select(point => point + offset).ToArray();

                copy.TextureName = face.TextureName;
                copy.TextureRightAxis = face.TextureRightAxis;
                copy.TextureDownAxis = face.TextureDownAxis;
                copy.TextureAngle = face.TextureAngle;
                copy.TextureScale = face.TextureScale;

                // Apply 'texture lock' while moving:
                var oldTextureCoordinates = GetTextureCoordinates(face.PlanePoints[0], face.TextureDownAxis, face.TextureRightAxis, face.TextureScale) + face.TextureShift;
                var newTextureCoordinates = GetTextureCoordinates(copy.PlanePoints[0], copy.TextureDownAxis, copy.TextureRightAxis, copy.TextureScale);
                copy.TextureShift = oldTextureCoordinates - newTextureCoordinates;

                return copy;
            }
        }

        /// <summary>
        /// Creates a copy of this entity. Ignores VIS-group and group relationships.
        /// </summary>
        public static Entity Copy(this Entity entity, Vector3D offset)
        {
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(offset)));

            foreach (var kv in entity.Properties)
                copy.Properties[kv.Key] = kv.Value;

            if (entity.IsPointBased)
                copy.Origin = entity.Origin + offset;

            return copy;
        }

        /// <summary>
        /// Creates a copy of this entity. Also evaluates expressions in property keys and values. Ignores VIS-group and group relationships.
        /// </summary>
        public static Entity CopyWithEvaluation(this Entity entity, Vector3D offset, ExpansionContext context)
        {
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(offset)));

            foreach (var kv in entity.Properties)
                copy.Properties[context.Evaluate(kv.Key)] = context.Evaluate(kv.Value);

            if (entity.IsPointBased)
                copy.Origin = entity.Origin + offset;

            return copy;
        }


        private static Vector2D GetTextureCoordinates(Vector3D point, Vector3D textureDownAxis, Vector3D textureRightAxis, Vector2D textureScale)
        {
            var texturePlaneNormal = textureDownAxis.CrossProduct(textureRightAxis);
            var projectedPoint = point - (point.DotProduct(texturePlaneNormal) * texturePlaneNormal);

            var x = projectedPoint.DotProduct(textureRightAxis);
            var y = projectedPoint.DotProduct(textureDownAxis);
            return new Vector2D(
                x / textureScale.X,
                y / textureScale.Y);
        }
    }
}
