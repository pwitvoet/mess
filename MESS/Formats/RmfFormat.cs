using MESS.Mapping;
using MESS.Spatial;
using System;
using System.IO;
using System.Linq;

namespace MESS.Formats
{
    /// <summary>
    /// The binary RMF file format (Rich Map Format) not only stores entities and brushes,
    /// but also supports logical and visual groups, camera's and paths.
    /// </summary>
    public static class RmfFormat
    {
        public static Map Load(Stream stream)
        {
            var map = new Map();

            var version = stream.ReadFloat();
            var rmfMagicString = stream.ReadString(3);
            if (rmfMagicString != "RMF")
                throw new InvalidDataException($"Expected 'RMF' magic string, but found '{rmfMagicString}'.");

            var visGroupCount = stream.ReadInt();
            for (int i = 0; i < visGroupCount; i++)
                map.VisGroups.Add(ReadVisGroup(stream));
            var visGroupIdLookup = map.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

            var cMapWorld = stream.ReadNString();
            stream.ReadBytes(7);

            var objectCount = stream.ReadInt();
            for (int i = 0; i < objectCount; i++)
            {
                (var mapObject, var visGroupID) = ReadObject(stream);
                if (visGroupID > 0 && visGroupIdLookup.TryGetValue(visGroupID, out var visGroup))
                    mapObject.VisGroup = visGroup;

                AddObject(mapObject);
            }

            var worldspawnClassname = stream.ReadNString();
            stream.ReadBytes(4);    // ?
            map.Properties["spawnflags"] = stream.ReadInt().ToString();  // 'worldspawn' flags, unused.

            var worldspawnPropertyCount = stream.ReadInt();
            for (int i = 0; i < worldspawnPropertyCount; i++)
                map.Properties[stream.ReadNString()] = stream.ReadNString();
            stream.ReadBytes(12);   // ?

            var pathCount = stream.ReadInt();
            for (int i = 0; i < pathCount; i++)
                map.Paths.Add(ReadPath(stream));

            var docInfoMagicString = stream.ReadString(8);
            if (docInfoMagicString != "DOCINFO")
                throw new InvalidDataException($"Expected 'DOCINFO' magic string, but found '{docInfoMagicString}'.");

            var cameraVersion = stream.ReadFloat();
            map.ActiveCameraIndex = stream.ReadInt();
            var cameraCount = stream.ReadInt();
            for (int i = 0; i < cameraCount; i++)
                map.Cameras.Add(ReadCamera(stream));

            return map;

            void AddObject(MapObject mapObject)
            {
                switch (mapObject)
                {
                    case Entity entity:
                        map.Entities.Add(entity);
                        break;

                    case Brush brush:
                        map.WorldGeometry.Add(brush);
                        break;

                    case Group group:
                        map.Groups.Add(group);
                        foreach (var childObject in group.Objects)
                            AddObject(childObject);
                        break;
                }
            }
        }

        // TODO: Save will have to generate vertices from brushes that came from a .map file!
        public static void Save(Map map, Stream stream) => throw new NotImplementedException();


        private static VisGroup ReadVisGroup(Stream stream)
        {
            var name = stream.ReadString(128);
            var color = ReadColor(stream);
            stream.ReadByte();      // Padding byte?
            var id = stream.ReadInt();
            var isVisible = stream.ReadByte() == 1;
            stream.ReadBytes(3);    // More padding bytes?

            return new VisGroup {
                Name = name,
                Color = color,
                ID = id,
                IsVisible = isVisible,
            };
        }

        private static (MapObject mapObject, int visGroupID) ReadObject(Stream stream)
        {
            var typeName = stream.ReadNString();
            switch (typeName)
            {
                case "CMapSolid": return ReadBrush(stream);
                case "CMapEntity": return ReadEntity(stream);
                case "CMapGroup": return ReadGroup(stream);
                default: throw new InvalidDataException($"Unknown object type '{typeName}'.");
            }
        }

        private static (Brush brush, int visGroupID) ReadBrush(Stream stream)
        {
            var brush = new Brush();

            var visGroupID = stream.ReadInt();
            brush.Color = ReadColor(stream);
            stream.ReadBytes(4);    // ?

            var faceCount = stream.ReadInt();
            for (int i = 0; i < faceCount; i++)
                brush.Faces.Add(ReadFace(stream));

            return (brush, visGroupID);
        }

        private static Face ReadFace(Stream stream)
        {
            var face = new Face();

            face.TextureName = stream.ReadString(256);
            stream.ReadFloat(); // ?

            face.TextureRightAxis = ReadVector3D(stream);
            var shiftX = stream.ReadFloat();
            face.TextureDownAxis = ReadVector3D(stream);
            var shiftY = stream.ReadFloat();
            face.TextureShift = new Vector2D(shiftX, shiftY);

            face.TextureAngle = stream.ReadFloat();
            face.TextureScale = new Vector2D(stream.ReadFloat(), stream.ReadFloat());

            stream.ReadBytes(16);   // ?
            var vertexCount = stream.ReadInt();
            for (int i = 0; i < vertexCount; i++)
                face.Vertices.Add(ReadVector3D(stream));

            face.PlanePoints = Enumerable.Range(0, 3)
                .Select(i => ReadVector3D(stream))
                .ToArray();

            return face;
        }

        private static (Entity entity, int visGroupID) ReadEntity(Stream stream)
        {
            var entity = new Entity();

            var visGroupID = stream.ReadInt();
            entity.Color = ReadColor(stream);

            var brushCount = stream.ReadInt();
            for (int i = 0; i < brushCount; i++)
            {
                var cMapSolid = stream.ReadNString();
                entity.Brushes.Add(ReadBrush(stream).brush);
            }

            entity.ClassName = stream.ReadNString();
            stream.ReadBytes(4);    // ?
            entity.Flags = stream.ReadInt();

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
                entity[stream.ReadNString()] = stream.ReadNString();

            stream.ReadBytes(14);   // ?

            if (brushCount == 0)
                entity.Origin = ReadVector3D(stream);
            else
                ReadVector3D(stream);

            stream.ReadBytes(4);    // ?

            return (entity, visGroupID);
        }

        private static (Group group, int visGroupID) ReadGroup(Stream stream)
        {
            var group = new Group();

            var visGroupID = stream.ReadInt();
            group.Color = ReadColor(stream);

            var objectCount = stream.ReadInt();
            for (int i = 0; i < objectCount; i++)
                ReadObject(stream).mapObject.Group = group;

            return (group, visGroupID);
        }

        private static Color ReadColor(Stream stream)
        {
            var colors = stream.ReadBytes(3);
            return new Color(colors[0], colors[1], colors[2]);
        }

        private static Vector3D ReadVector3D(Stream stream) => new Vector3D(stream.ReadFloat(), stream.ReadFloat(), stream.ReadFloat());

        private static Mapping.Path ReadPath(Stream stream)
        {
            var path = new Mapping.Path();
            path.Name = stream.ReadString(128);
            path.ClassName = stream.ReadString(128);
            path.Type = (PathType)stream.ReadInt();

            var cornerCount = stream.ReadInt();
            for (int i = 0; i < cornerCount; i++)
                path.Corners.Add(ReadCorner(stream));

            return path;
        }

        private static Corner ReadCorner(Stream stream)
        {
            var corner = new Corner();
            corner.Position = ReadVector3D(stream);
            corner.Index = stream.ReadInt();
            corner.NameOverride = stream.ReadString(128);

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
                corner.Properties[stream.ReadNString()] = stream.ReadNString();

            return corner;
        }

        private static Camera ReadCamera(Stream stream)
        {
            return new Camera {
                EyePosition = ReadVector3D(stream),
                LookAtPosition = ReadVector3D(stream),
            };
        }
    }
}
