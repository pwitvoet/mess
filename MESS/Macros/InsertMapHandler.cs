using MESS.Mapping;
using MESS.Spatial;
using System;
using System.Linq;

namespace MESS.Macros
{
    // TODO: Support for special property names, such as '{#ID}', which will be given a unique ID! Useful for isolating template instances from each other,
    //       and when you don't care about referencing instance entities from elsewhere. This means you don't need to give your macro_insert_map entities
    //       a unique name.

    /// <summary>
    /// Inserst the content of a map into another map, after first expanding macros and substituting placeholders in the to-be-inserted map.
    /// The properties of the 'macro_insert_map' are used for placeholder substitution.
    /// <para>
    /// For example, if the 'macro_insert_map' has a property 'name'='fire1', then an entity with targetname '{name}_01' ends up with a targetname of 'fire1_01'.
    /// </para>
    /// </summary>
    class InsertMapHandler : IMacroEntityHandler
    {
        public string EntityName => "macro_insert_map";


        public void Process(Entity entity, Map map, string workingDirectory, MacroExpander expander)
        {
            var insertedMapPath = GetNormalizedPath(entity["map"], workingDirectory);
            if (string.IsNullOrEmpty(insertedMapPath))
                return;

            // NOTE: This gives us an expanded copy of a map template, so we're free to modify brushes and entities without worrying about destroying a template:
            var insertedMap = expander.ExpandMacros(insertedMapPath, entity.Properties);

            var offset = entity.Origin;
            var scale = GetScale(entity);
            var angles = GetAngles(entity);

            // Insert brushes and entities, adjusting position, orientation and scale accordingly:
            foreach (var insertedBrush in insertedMap.WorldGeometry)
            {
                ApplyTransformations(insertedBrush, offset, scale, angles);
                map.WorldGeometry.Add(insertedBrush);
            }
            foreach (var insertedEntity in insertedMap.Entities)
            {
                ApplyTransformations(insertedEntity, offset, scale, angles);
                map.Entities.Add(insertedEntity);
            }

            // NOTE: Paths don't need to be added, because the macro expander has already translated them into entities.
        }


        private static string GetNormalizedPath(string path, string workingDirectory)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (!System.IO.Path.IsPathRooted(path))
                path = System.IO.Path.Combine(workingDirectory, path);

            return System.IO.Path.GetFullPath(path);
        }

        private static float GetScale(Entity entity) => float.TryParse(entity["scale"], out var scale) ? scale : 1f;

        private static Vector3D GetAngles(Entity entity)
        {
            var rawAngles = entity["angles"];
            if (string.IsNullOrEmpty(rawAngles))
                return new Vector3D();

            var angles = rawAngles.Split()
                .Select(part => float.TryParse(part, out var value) ? value : (float?)null)
                .ToArray();
            if (angles.Length != 3 || angles.Any(value => value == null))
                return new Vector3D();

            return new Vector3D(angles[0].Value, angles[1].Value, angles[2].Value);
        }

        private static void ApplyTransformations(Brush brush, Vector3D offset, float scale, Vector3D angles)
        {
            foreach (var face in brush.Faces)
            {
                // Determine the current texture coordinates - it doesn't matter which point on the face plane we pick, as long as we later compare against the same point:
                var oldTextureCoordinates = GetTextureCoordinates(face.PlanePoints[0], face.TextureDownAxis, face.TextureRightAxis, face.TextureScale);
                oldTextureCoordinates += face.TextureShift;

                // Transform all vertices and plane control points:
                for (int i = 0; i < face.Vertices.Count; i++)
                    face.Vertices[i] = Transform(face.Vertices[i], offset, scale, angles);

                for (int i = 0; i < face.PlanePoints.Length; i++)
                    face.PlanePoints[i] = Transform(face.PlanePoints[i], offset, scale, angles);

                // Rotate the texture plane and adjust its scale:
                face.TextureRightAxis = Rotate(face.TextureRightAxis, angles);
                face.TextureDownAxis = Rotate(face.TextureDownAxis, angles);
                face.TextureScale *= scale;

                // Update texture shift, so the point on the face we picked earlier maps to the same texture coordinates as before:
                var newTextureCoordinates = GetTextureCoordinates(face.PlanePoints[0], face.TextureDownAxis, face.TextureRightAxis, face.TextureScale);
                face.TextureShift = oldTextureCoordinates - newTextureCoordinates;
            }
        }

        private static void ApplyTransformations(Entity entity, Vector3D offset, float scale, Vector3D angles)
        {
            if (entity.IsPointBased)
            {
                entity.Origin = Transform(entity.Origin, offset, scale, angles);
            }
            else
            {
                foreach (var brush in entity.Brushes)
                    ApplyTransformations(brush, offset, scale, angles);
            }
        }

        private static Vector3D Transform(Vector3D point, Vector3D offset, float scale, Vector3D angles)
        {
            point *= scale;
            point = Rotate(point, angles);
            point += offset;

            return point;
        }

        private static Vector3D Rotate(Vector3D point, Vector3D angles)
        {
            var pitch = angles.X / 180f * Math.PI;
            var yaw = angles.Y / 180f * Math.PI;
            var roll = angles.Z / 180f * Math.PI;

            var cosPitch = Math.Cos(pitch);
            var cosYaw = Math.Cos(yaw);
            var cosRoll = Math.Cos(roll);
            var sinPitch = Math.Sin(pitch);
            var sinYaw = Math.Sin(yaw);
            var sinRoll = Math.Sin(roll);

            return new Vector3D(
                (float)((point.X * cosYaw * cosPitch) + (point.Y * (cosYaw * sinPitch * sinRoll - sinYaw * cosRoll)) + (point.Z * (cosYaw * sinPitch * cosRoll + sinYaw * sinRoll))),
                (float)((point.X * sinYaw * cosPitch) + (point.Y * (sinYaw * sinPitch * sinRoll + cosYaw * cosRoll)) + (point.Z * (sinYaw * sinPitch * cosRoll - cosYaw * sinRoll))),
                (float)((point.X * -sinPitch) + (point.Y * cosPitch * sinRoll) + (point.Z * cosPitch * cosRoll)));
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
