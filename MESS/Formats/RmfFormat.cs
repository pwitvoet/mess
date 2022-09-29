using MESS.Common;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Globalization;
using System.Text;

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
            var nextGroupID = 1;

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
            map.Properties[Attributes.Spawnflags] = stream.ReadInt().ToString(CultureInfo.InvariantCulture);    // 'worldspawn' flags, unused.

            var worldspawnPropertyCount = stream.ReadInt();
            for (int i = 0; i < worldspawnPropertyCount; i++)
                map.Properties[stream.ReadNString()] = stream.ReadNString();
            stream.ReadBytes(12);   // ?

            var pathCount = stream.ReadInt();
            for (int i = 0; i < pathCount; i++)
                map.EntityPaths.Add(ReadPath(stream));

            var docInfoBuffer = new byte[8];
            if (stream.Read(docInfoBuffer, 0, docInfoBuffer.Length) == docInfoBuffer.Length)
            {
                var docInfoMagicString = Encoding.ASCII.GetString(docInfoBuffer);
                if (docInfoMagicString != "DOCINFO\0")
                    throw new InvalidDataException($"Expected 'DOCINFO' magic string, but found '{docInfoMagicString}'.");

                var cameraVersion = stream.ReadFloat();
                map.ActiveCameraIndex = stream.ReadInt();
                var cameraCount = stream.ReadInt();
                for (int i = 0; i < cameraCount; i++)
                    map.Cameras.Add(ReadCamera(stream));
            }

            return map;

            void AddObject(MapObject mapObject)
            {
                switch (mapObject)
                {
                    case Entity entity:
                        map.Entities.Add(entity);
                        break;

                    case Brush brush:
                        map.AddBrush(brush);
                        break;

                    case Group group:
                        group.ID = nextGroupID++;
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
            var visGroupID = stream.ReadInt();
            var color = ReadColor(stream);
            stream.ReadBytes(4);    // ?

            var faces = new Face[stream.ReadInt()];
            for (int i = 0; i < faces.Length; i++)
                faces[i] = ReadFace(stream);

            return (new Brush(faces) { Color = color }, visGroupID);
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
            var visGroupID = stream.ReadInt();
            var color = ReadColor(stream);

            var brushCount = stream.ReadInt();
            var brushes = new Brush[brushCount];
            for (int i = 0; i < brushCount; i++)
            {
                var cMapSolid = stream.ReadNString();
                brushes[i] = ReadBrush(stream).brush;
            }

            var entity = new Entity(brushes);
            entity.Color = color;

            entity.ClassName = stream.ReadNString();
            stream.ReadBytes(4);    // ?
            entity.Flags = stream.ReadInt();

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
                entity.Properties[stream.ReadNString()] = stream.ReadNString();

            stream.ReadBytes(14);   // ?

            if (entity.IsPointBased)
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

        private static EntityPath ReadPath(Stream stream)
        {
            var entityPath = new EntityPath();
            entityPath.Name = stream.ReadString(128);
            entityPath.ClassName = stream.ReadString(128);
            entityPath.Type = (PathType)stream.ReadInt();

            var nodeCount = stream.ReadInt();
            for (int i = 0; i < nodeCount; i++)
                entityPath.Nodes.Add(ReadPathNode(stream));

            return entityPath;
        }

        private static EntityPathNode ReadPathNode(Stream stream)
        {
            var pathNode = new EntityPathNode();
            pathNode.Position = ReadVector3D(stream);
            pathNode.Index = stream.ReadInt();
            pathNode.NameOverride = stream.ReadString(128);

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
                pathNode.Properties[stream.ReadNString()] = stream.ReadNString();

            return pathNode;
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
