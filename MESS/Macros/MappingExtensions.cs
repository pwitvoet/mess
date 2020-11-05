using MESS.Formats;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
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
        /// Expands all entity paths in the given map into actual entities, then clears the list of entity paths.
        /// This method modifies the given map.
        /// </summary>
        public static void ExpandPaths(this Map map)
        {
            foreach (var entityPath in map.EntityPaths)
                map.Entities.AddRange(entityPath.GenerateEntities());
            map.EntityPaths.Clear();
        }


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
        /// Creates a copy of this brush, adjusted by the given offset. Ignores VIS-group and group relationships.
        /// </summary>
        public static Brush Copy(this Brush brush, Vector3D offset)
            => brush.Copy(new Transform(1, Matrix3x3.Identity, offset));

        /// <summary>
        /// Creates a copy of this brush, adjusted by the given transform. Ignores VIS-group and group relationships.
        /// </summary>
        public static Brush Copy(this Brush brush, Transform transform)
        {
            return new Brush(brush.Faces.Select(CopyFace)) { Color = brush.Color };

            Face CopyFace(Face face)
            {
                var copy = new Face();

                copy.Vertices.AddRange(face.Vertices.Select(vertex => ApplyTransform(vertex, transform)));
                copy.PlanePoints = face.PlanePoints.Select(point => ApplyTransform(point, transform)).ToArray();

                copy.TextureName = face.TextureName;
                copy.TextureRightAxis = transform.Rotation * face.TextureRightAxis;
                copy.TextureDownAxis = transform.Rotation * face.TextureDownAxis;
                copy.TextureAngle = face.TextureAngle;
                copy.TextureScale = transform.Scale * face.TextureScale;

                // Apply 'texture lock' while moving:
                var oldTextureCoordinates = GetTextureCoordinates(face.PlanePoints[0], face.TextureDownAxis, face.TextureRightAxis, face.TextureScale) + face.TextureShift;
                var newTextureCoordinates = GetTextureCoordinates(copy.PlanePoints[0], copy.TextureDownAxis, copy.TextureRightAxis, copy.TextureScale);
                copy.TextureShift = oldTextureCoordinates - newTextureCoordinates;

                return copy;
            }
        }

        /// <summary>
        /// Creates a copy of this entity, with its position adjusted by the given offset.
        /// No other transformations are applied, and no expressions are evaluated.
        /// Ignores VIS-group and group relationships.
        /// </summary>
        public static Entity Copy(this Entity entity, Vector3D offset)
        {
            var transform = new Transform(1, Matrix3x3.Identity, offset);
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(transform)));

            foreach (var kv in entity.Properties)
                copy.Properties[kv.Key] = kv.Value;

            if (entity.IsPointBased)
                copy.Origin = entity.Origin + offset;

            return copy;
        }

        // TODO: If 'angles' and 'scale' are missing, that could cause issues...? But what if we always insert them,
        //       could that also be problematic in different situations?
        /// <summary>
        /// Creates a copy of this entity. The copy is scaled, rotated and translated,
        /// and by default any expressions in its properties will be evaluated.
        /// If it contains 'angles' and 'scale' properties, then these will be updated according to how the entity has been transformed.
        /// Ignores VIS-group and group relationships.
        /// </summary>
        public static Entity Copy(this Entity entity, InstantiationContext context, bool applyTransform = true, bool evaluateExpressions = true)
        {
            var transform = applyTransform ? context.Transform : Transform.Identity;
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(transform)));

            if (evaluateExpressions)
            {
                foreach (var kv in entity.Properties)
                    copy.Properties[context.EvaluateInterpolatedString(kv.Key)] = context.EvaluateInterpolatedString(kv.Value);

                UpdateSpawnFlags(copy);
            }
            else
            {
                foreach (var kv in entity.Properties)
                    copy.Properties[kv.Key] = kv.Value;
            }

            if (applyTransform)
            {
                // TODO: Also check whether maybe the angles/scale keys do exist, but contain invalid values!
                if (copy.Angles is Angles angles)
                    copy.Angles = (angles.ToMatrix() * context.Transform.Rotation).ToAngles();

                if (copy.Scale is double scale)
                    copy.Scale = scale * context.Transform.Scale;

                if (copy.IsPointBased)
                    copy.Origin = ApplyTransform(copy.Origin, context.Transform);
            }

            return copy;
        }


        private static Vector3D ApplyTransform(Vector3D point, Transform transform)
            => transform.Offset + (transform.Rotation * (point * transform.Scale));

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

        /// <summary>
        /// Searches for 'spawnflag{N}' attributes and uses them to update the special 'spawnflags' attribute.
        /// The 'spawnflag{N}' attributes are removed afterwards.
        /// This makes it possible to control spawn flags with MScript - which, due to how editors handle the spawnflags attribute,
        /// would otherwise not be possible.
        /// </summary>
        private static void UpdateSpawnFlags(Entity entity)
        {
            var spawnFlags = entity.Flags;
            for (int i = 0; i < 32; i++)
            {
                var flagName = $"spawnflag{i + 1}";
                if (entity.Properties.TryGetValue(flagName, out var value) && int.TryParse(value, out var flagValue))
                {
                    if (flagValue != 0)
                        spawnFlags |= (1 << i);
                    else
                        spawnFlags = spawnFlags & ~(1 << i);

                    entity.Properties.Remove(flagName);
                }
            }
            entity.Flags = spawnFlags;
        }
    }
}
