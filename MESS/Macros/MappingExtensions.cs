using MESS.Common;
using MESS.Formats;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using System.Globalization;

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
                map.AddEntities(entityPath.GenerateEntities());
            map.EntityPaths.Clear();
        }


        /// <summary>
        /// Returns true if this brush is fully covered with the 'ORIGIN' texture. Origin brushes are used to specify the origin of certain brush entities.
        /// </summary>
        public static bool IsOriginBrush(this Brush brush) => brush?.Faces.All(face => face.TextureName?.ToUpper() == Textures.Origin) == true;

        /// <summary>
        /// For point entities, this returns the 'origin' attribute, or null if that attribute is missing.
        /// For brush entities, this returns the center of the first 'ORIGIN'-textured brush, or null if there is no such brush.
        /// </summary>
        public static Vector3D? GetOrigin(this Entity entity)
        {
            if (entity.IsPointBased)
            {
                return entity.Properties.ContainsKey(Attributes.Origin) ? entity.Origin : null;
            }
            else
            {
                return entity.Brushes.FirstOrDefault(IsOriginBrush)?.BoundingBox.Center;
            }
        }


        /// <summary>
        /// Creates a copy of this brush, adjusted by the given offset. Ignores editor format-specific data, VIS-group and group relationships.
        /// </summary>
        public static Brush Copy(this Brush brush, Vector3D? offset = null)
            => brush.Copy(new Transform(1, new Vector3D(1, 1, 1), Matrix3x3.Identity, offset ?? new Vector3D()));

        /// <summary>
        /// Creates a copy of this brush, adjusted by the given transform. Ignores editor format-specific data, VIS-group and group relationships.
        /// </summary>
        public static Brush Copy(this Brush brush, Transform transform)
        {
            return new Brush(brush.Faces.Select(CopyFace)) { Color = brush.Color };

            Face CopyFace(Face face)
            {
                var faceCopy = face.Copy();
                ApplyTransform(faceCopy, transform);
                return faceCopy;
            }
        }

        /// <summary>
        /// Creates a copy of this entity, with its position adjusted by the given offset.
        /// No other transformations are applied, and no expressions are evaluated.
        /// Ignores editor format-specific data, VIS-group and group relationships.
        /// </summary>
        public static Entity Copy(this Entity entity, Vector3D offset)
        {
            var transform = new Transform(1, new Vector3D(1, 1, 1), Matrix3x3.Identity, offset);
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(transform)));

            foreach (var kv in entity.Properties)
                copy.Properties[kv.Key] = kv.Value;

            if (entity.IsPointBased)
                copy.Origin = entity.Origin + offset;

            return copy;
        }

        /// <summary>
        /// Creates a copy of this entity. The brushwork will be rotated, translated and scaled using the given transform.
        /// Ignores editor format-specific data, VIS-group and group relationships.
        /// </summary>
        public static Entity Copy(this Entity entity, Transform transform)
        {
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(transform)));
            foreach (var kv in entity.Properties)
                copy.Properties[kv.Key] = kv.Value;

            return copy;
        }


        public static void ApplyTransform(this Brush brush, Transform transform)
        {
            foreach (var face in brush.Faces)
                ApplyTransform(face, transform);
        }

        private static void ApplyTransform(Face face, Transform transform)
        {
            var oldPlanePoints = face.PlanePoints;
            var oldTextureRightAxis = face.TextureRightAxis;
            var oldTextureDownAxis = face.TextureDownAxis;
            var oldTextureScale = face.TextureScale;

            // If an odd number of axis has a negative scale, then we need to flip faces around (otherwise we'd get an 'inside-out' or 'inverted' brush):
            var flipFace = transform.GeometryScale.X < 0;
            if (transform.GeometryScale.Y < 0)
                flipFace = !flipFace;
            if (transform.GeometryScale.Z < 0)
                flipFace = !flipFace;

            var newVertices = face.Vertices.Select(transform.Apply).ToArray();
            var newPlanePoints = face.PlanePoints.Select(transform.Apply).ToArray();
            if (flipFace)
            {
                Array.Reverse(newVertices);
                Array.Reverse(newPlanePoints);
            }
            face.Vertices.Clear();
            face.Vertices.AddRange(newVertices);
            face.PlanePoints = newPlanePoints;

            var newTextureRightAxis = transform.Rotation * (face.TextureRightAxis * (1f / transform.GeometryScale));
            var newTextureDownAxis = transform.Rotation * (face.TextureDownAxis * (1f / transform.GeometryScale));
            var rightScaleFactor = newTextureRightAxis.Length();
            var downScaleFactor = newTextureDownAxis.Length();

            face.TextureRightAxis = newTextureRightAxis.Normalized();
            face.TextureDownAxis = newTextureDownAxis.Normalized();
            face.TextureScale = new Vector2D(face.TextureScale.X / rightScaleFactor, face.TextureScale.Y / downScaleFactor);

            // Apply 'texture lock' while moving:
            var oldTextureCoordinates = GetTextureCoordinates(oldPlanePoints[0], oldTextureDownAxis, oldTextureRightAxis, face.TextureShift, oldTextureScale);
            var newTextureCoordinates = GetTextureCoordinates(face.PlanePoints[flipFace ? face.PlanePoints.Length - 1 : 0], face.TextureDownAxis, face.TextureRightAxis, face.TextureShift, face.TextureScale);
            face.TextureShift = (oldTextureCoordinates + face.TextureShift) - newTextureCoordinates;
        }


        /// <summary>
        /// Updates the angles, scale and origin attributes (if present) by applying the given transform to them.
        /// </summary>
        public static void UpdateTransformProperties(this IDictionary<string, object?> properties, Transform transform, bool invertedPitch = false)
        {
            if (properties.GetAngles(Attributes.Angles) is Angles angles)
                properties.SetAngles(Attributes.Angles, (transform.Rotation * angles.ToMatrix(invertedPitch)).ToAngles(invertedPitch));

            if (properties.GetDouble(Attributes.Scale) is double scale)
                properties.SetDouble(Attributes.Scale, scale * transform.Scale);

            if (properties.GetVector3D(Attributes.Origin) is Vector3D origin)
                properties.SetVector3D(Attributes.Origin, transform.Apply(origin));
        }

        /// <summary>
        /// Searches for <see cref="Attributes.SpawnflagN"/> attributes and uses them to update the special <see cref="Attributes.Spawnflags"/> attribute.
        /// The <see cref="Attributes.SpawnflagN"/> attributes are removed afterwards.
        /// This makes it possible to control spawn flags with MScript - which, due to how editors handle the spawnflags attribute,
        /// would otherwise not be possible.
        /// </summary>
        public static void UpdateSpawnFlags(this IDictionary<string, object?> properties)
        {
            var spawnFlags = properties.GetInteger(Attributes.Spawnflags) ?? 0;
            for (int i = 0; i < 32; i++)
            {
                var flagName = string.Format(CultureInfo.InvariantCulture, Attributes.SpawnflagN, i);
                if (!properties.TryGetValue(flagName, out var value))
                    continue;

                var isChecked = Interpreter.IsTrue(value) && !(value is double d && d == 0);
                if (isChecked)
                    spawnFlags |= (1 << i);
                else
                    spawnFlags = spawnFlags & ~(1 << i);

                properties.Remove(flagName);
            }

            if (properties.ContainsKey(Attributes.Spawnflags) || spawnFlags != 0)
                properties.SetInteger(Attributes.Spawnflags, spawnFlags);
        }


        public static Vector3D GetAnchorPoint(this Entity entity, TemplateAreaAnchor anchor)
        {
            if (anchor == TemplateAreaAnchor.OriginBrush && entity.GetOrigin() is Vector3D origin)
                return origin;

            switch (anchor)
            {
                // NOTE: The bottom anchor point is our fallback for when there's no origin brush:
                default:
                case TemplateAreaAnchor.Bottom: return new Vector3D(entity.BoundingBox.Center.X, entity.BoundingBox.Center.Y, entity.BoundingBox.Min.Z);
                case TemplateAreaAnchor.Center: return entity.BoundingBox.Center;
                case TemplateAreaAnchor.Top: return new Vector3D(entity.BoundingBox.Center.X, entity.BoundingBox.Center.Y, entity.BoundingBox.Max.Z);
            }
        }


        public static Vector2D GetTextureCoordinates(this Face face, Vector3D point)
            => GetTextureCoordinates(point, face.TextureDownAxis, face.TextureRightAxis, face.TextureShift, face.TextureScale);

        private static Vector2D GetTextureCoordinates(Vector3D point, Vector3D textureDownAxis, Vector3D textureRightAxis, Vector2D textureShift, Vector2D textureScale)
        {
            var texturePlaneNormal = textureDownAxis.CrossProduct(textureRightAxis);
            var projectedPoint = point - (point.DotProduct(texturePlaneNormal) * texturePlaneNormal);

            var x = projectedPoint.DotProduct(textureRightAxis);
            var y = projectedPoint.DotProduct(textureDownAxis);
            return new Vector2D(
                (x / textureScale.X) + textureShift.X,
                (y / textureScale.Y) + textureShift.Y);
        }
    }
}
