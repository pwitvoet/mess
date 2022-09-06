using MESS.Common;
using MESS.Formats;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using System;
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
        public static bool IsOriginBrush(this Brush brush) => brush?.Faces.All(face => face.TextureName?.ToUpper() == Textures.Origin) == true;

        /// <summary>
        /// For point entities, this returns the 'origin' attribute, or null if that attribute is missing.
        /// For brush entities, this returns the center of the first 'ORIGIN'-textured brush, or null if there is no such brush.
        /// </summary>
        public static Vector3D? GetOrigin(this Entity entity)
        {
            if (entity.IsPointBased)
            {
                return entity.Properties.ContainsKey(Attributes.Origin) ? (Vector3D?)entity.Origin : null;
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
            => brush.Copy(new Transform(1, new Vector3D(1, 1, 1), Matrix3x3.Identity, offset));

        /// <summary>
        /// Creates a copy of this brush, adjusted by the given transform. Ignores VIS-group and group relationships.
        /// </summary>
        public static Brush Copy(this Brush brush, Transform transform)
        {
            return new Brush(brush.Faces.Select(CopyFace)) { Color = brush.Color };

            Face CopyFace(Face face)
            {
                var copy = new Face();

                // If an odd number of axis has a negative scale, then we need to flip faces around (otherwise we'd get an 'inside-out' or 'inverted' brush):
                var flipFace = transform.GeometryScale.X < 0;
                if (transform.GeometryScale.Y < 0)
                    flipFace = !flipFace;
                if (transform.GeometryScale.Z < 0)
                    flipFace = !flipFace;

                if (flipFace)
                {
                    copy.Vertices.AddRange(face.Vertices.Select(transform.Apply).Reverse());
                    copy.PlanePoints = face.PlanePoints.Select(transform.Apply).Reverse().ToArray();
                }
                else
                {
                    copy.Vertices.AddRange(face.Vertices.Select(transform.Apply));
                    copy.PlanePoints = face.PlanePoints.Select(transform.Apply).ToArray();
                }

                var newTextureRightAxis = transform.Rotation * (face.TextureRightAxis * (1f / transform.GeometryScale));
                var newTextureDownAxis = transform.Rotation * (face.TextureDownAxis * (1f / transform.GeometryScale));
                var rightScaleFactor = newTextureRightAxis.Length();
                var downScaleFactor = newTextureDownAxis.Length();

                copy.TextureName = face.TextureName;
                copy.TextureRightAxis = newTextureRightAxis.Normalized();
                copy.TextureDownAxis = newTextureDownAxis.Normalized();
                copy.TextureAngle = face.TextureAngle;
                copy.TextureScale = new Vector2D(face.TextureScale.X / rightScaleFactor, face.TextureScale.Y / downScaleFactor);

                // Apply 'texture lock' while moving:
                var oldTextureCoordinates = GetTextureCoordinates(face.PlanePoints[0], face.TextureDownAxis, face.TextureRightAxis, face.TextureScale);
                var newTextureCoordinates = GetTextureCoordinates(copy.PlanePoints[flipFace ? copy.PlanePoints.Length - 1 : 0], copy.TextureDownAxis, copy.TextureRightAxis, copy.TextureScale);
                copy.TextureShift = (oldTextureCoordinates + face.TextureShift) - newTextureCoordinates;

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
        /// Ignores VIS-group and group relationships.
        /// </summary>
        public static Entity Copy(this Entity entity, Transform transform)
        {
            var copy = new Entity(entity.Brushes.Select(brush => brush.Copy(transform)));
            foreach (var kv in entity.Properties)
                copy.Properties[kv.Key] = kv.Value;

            return copy;
        }


        /// <summary>
        /// Updates the angles, scale and origin attributes (if present) by applying the given transform to them.
        /// </summary>
        public static void UpdateTransformProperties(this IDictionary<string, string> properties, Transform transform, bool invertedPitch = false)
        {
            if (properties.GetAnglesProperty(Attributes.Angles) is Angles angles)
                properties.SetAnglesProperty(Attributes.Angles, (transform.Rotation * angles.ToMatrix(invertedPitch)).ToAngles(invertedPitch));

            if (properties.GetNumericProperty(Attributes.Scale) is double scale)
                properties.SetNumericProperty(Attributes.Scale, scale * transform.Scale);

            if (properties.GetVector3DProperty(Attributes.Origin) is Vector3D origin)
                properties.SetVector3DProperty(Attributes.Origin, transform.Apply(origin));
        }

        /// <summary>
        /// Searches for 'spawnflag{N}' attributes and uses them to update the special 'spawnflags' attribute.
        /// The 'spawnflag{N}' attributes are removed afterwards.
        /// This makes it possible to control spawn flags with MScript - which, due to how editors handle the spawnflags attribute,
        /// would otherwise not be possible.
        /// </summary>
        public static void UpdateSpawnFlags(this IDictionary<string, string> properties)
        {
            var spawnFlags = properties.GetIntegerProperty(Attributes.Spawnflags) ?? 0;
            for (int i = 0; i < 32; i++)
            {
                var flagName = FormattableString.Invariant($"spawnflag{i}");
                if (!properties.TryGetValue(flagName, out var stringValue))
                    continue;

                var value = PropertyExtensions.ParseProperty(stringValue);
                var isChecked = Interpreter.IsTrue(value) && !(value is double d && d == 0);
                if (isChecked)
                    spawnFlags |= (1 << i);
                else
                    spawnFlags = spawnFlags & ~(1 << i);

                properties.Remove(flagName);
            }

            if (properties.ContainsKey(Attributes.Spawnflags) || spawnFlags != 0)
                properties.SetIntegerProperty(Attributes.Spawnflags, spawnFlags);
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
