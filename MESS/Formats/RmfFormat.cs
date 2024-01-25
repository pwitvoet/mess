using MESS.Common;
using MESS.Formats.RMF;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
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

            var fileVersion = ReadFileVersion(stream);
            var rmfFileSignature = stream.ReadFixedLengthString(3);
            if (rmfFileSignature != "RMF")
                throw new InvalidDataException($"Expected 'RMF' magic string, but found '{rmfFileSignature}'.");

            var visGroupCount = stream.ReadInt();
            for (int i = 0; i < visGroupCount; i++)
                map.VisGroups.Add(ReadVisGroup(stream));
            var visGroupIdLookup = map.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

            var cMapWorld = stream.ReadNString();
            if (cMapWorld != "CMapWorld")
                throw new InvalidDataException($"Expected a CMapWorld object, but found '{cMapWorld}'.");

            stream.ReadInt();   // VIS group ID, always 0
            map.Worldspawn.Color = ReadColor(stream);

            var objectCount = stream.ReadInt();
            for (int i = 0; i < objectCount; i++)
            {
                (var mapObject, var visGroupID) = ReadMapObject(stream);
                if (visGroupID > 0 && visGroupIdLookup.TryGetValue(visGroupID, out var visGroup) && visGroup != null)
                    visGroup.AddObject(mapObject);

                AddObject(mapObject);

                if (mapObject is IRmfIndexedObject indexedObject)
                    indexedObject.RmfIndex = i;
            }

            ReadEntityData(stream, map.Worldspawn);
            var unknown1 = stream.ReadBytes(12);

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


        public static void Save(Map map, string path, RmfFileSaveSettings? settings = null)
        {
            using (var file = File.Create(path))
                Save(map, file, settings);
        }

        public static void Save(Map map, Stream stream, RmfFileSaveSettings? settings = null)
        {
            if (settings == null)
                settings = new RmfFileSaveSettings();


            WriteFileVersion(stream, settings.FileVersion);
            stream.WriteFixedLengthString("RMF");

            stream.WriteInt(map.VisGroups.Count);
            foreach (var visGroup in map.VisGroups)
                WriteVisGroup(stream, visGroup);


            stream.WriteNString("CMapWorld");
            stream.WriteInt(0);     // VIS group ID
            WriteColor(stream, map.Worldspawn.Color);

            var topLevelObjects = new List<MapObject>();
            topLevelObjects.AddRange(map.WorldGeometry.Where(brush => brush.Group == null));
            topLevelObjects.AddRange(map.Entities.Where(entity => entity.Group == null));
            topLevelObjects.AddRange(map.Groups.Where(group => group.Group == null));
            topLevelObjects = topLevelObjects.OrderBy(mapObject => mapObject is IRmfIndexedObject indexedObject ? indexedObject.RmfIndex : int.MaxValue).ToList();

            stream.WriteInt(topLevelObjects.Count);
            foreach (var mapObject in topLevelObjects)
                WriteMapObject(stream, mapObject);

            // worldspawn & map properties:
            WriteEntityData(stream, map.Worldspawn);
            stream.WriteBytes(new byte[12]);    // Unknown

            // Paths:
            stream.WriteInt(map.EntityPaths.Count);
            foreach (var entityPath in map.EntityPaths)
                WritePath(stream, entityPath);

            // Cameras:
            stream.WriteFixedLengthString("DOCINFO\0");
            stream.WriteFloat(0.2f);
            stream.WriteInt(map.ActiveCameraIndex ?? -1);
            stream.WriteInt(map.Cameras.Count);
            foreach (var camera in map.Cameras)
                WriteCamera(stream, camera);
        }


        private static RmfFileVersion ReadFileVersion(Stream stream)
        {
            var version = stream.ReadFloat();
            switch (version)
            {
                case 2.2f: return RmfFileVersion.V2_2;
                default: throw new NotSupportedException($"Rmf file version {version} is not supported.");
            }
        }

        private static VisGroup ReadVisGroup(Stream stream)
        {
            var name = stream.ReadFixedLengthString(128);
            var color = ReadColor(stream);
            stream.ReadByte();      // Padding?
            var id = stream.ReadInt();
            var isVisible = stream.ReadSingleByte() == 1;
            stream.ReadBytes(3);    // More padding?

            return new VisGroup {
                Name = name,
                Color = color,
                ID = id,
                IsVisible = isVisible,
            };
        }

        private static (MapObject mapObject, int visGroupID) ReadMapObject(Stream stream)
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
            var childObjectCount = stream.ReadInt();
            if (childObjectCount != 0)
                throw new InvalidDataException($"Expected 0 child objects for brush, but found {childObjectCount}.");

            var faces = new Face[stream.ReadInt()];
            for (int i = 0; i < faces.Length; i++)
                faces[i] = ReadFace(stream);

            return (new RmfBrush(faces) { Color = color }, visGroupID);
        }

        private static Face ReadFace(Stream stream)
        {
            var face = new RmfFace();

            face.TextureName = stream.ReadFixedLengthString(256);
            face.UnknownData1 = stream.ReadInt();

            face.TextureRightAxis = ReadVector3D(stream);
            var shiftX = stream.ReadFloat();
            face.TextureDownAxis = ReadVector3D(stream);
            var shiftY = stream.ReadFloat();
            face.TextureShift = new Vector2D(shiftX, shiftY);

            face.TextureAngle = stream.ReadFloat();
            face.TextureScale = new Vector2D(stream.ReadFloat(), stream.ReadFloat());

            face.UnknownData2 = stream.ReadBytes(16);

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
                if (cMapSolid != "CMapSolid")
                    throw new InvalidDataException($"Expected a CMapSolid child object for entity, but found a '{cMapSolid}'.");

                brushes[i] = ReadBrush(stream).brush;
            }

            var entity = new RmfEntity(brushes);
            entity.Color = color;

            ReadEntityData(stream, entity);

            entity.UnknownData1 = stream.ReadBytes(12);
            entity.EntityType = (RmfEntityType)stream.ReadShort();

            var origin = ReadVector3D(stream);
            if (entity.EntityType == RmfEntityType.PointBased)
                entity.Origin = origin;

            entity.UnknownData2 = stream.ReadInt();

            return (entity, visGroupID);
        }

        private static void ReadEntityData(Stream stream, Entity entity)
        {
            entity.ClassName = stream.ReadNString();
            var unknown1 = stream.ReadInt();
            entity.Spawnflags = stream.ReadInt();

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
                entity.Properties[stream.ReadNString()] = stream.ReadNString();
        }

        private static (Group group, int visGroupID) ReadGroup(Stream stream)
        {
            var group = new RmfGroup();

            var visGroupID = stream.ReadInt();
            group.Color = ReadColor(stream);

            var objectCount = stream.ReadInt();
            for (int i = 0; i < objectCount; i++)
                group.AddObject(ReadMapObject(stream).mapObject);

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
            entityPath.Name = stream.ReadFixedLengthString(128);
            entityPath.ClassName = stream.ReadFixedLengthString(128);
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
            pathNode.NameOverride = stream.ReadFixedLengthString(128);

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


        private static void WriteFileVersion(Stream stream, RmfFileVersion version)
        {
            switch (version)
            {
                case RmfFileVersion.V2_2: stream.WriteFloat(2.2f); break;
                default: throw new NotSupportedException($"Rmf file version {version} is not supported.");
            }
        }

        private static void WriteVisGroup(Stream stream, VisGroup visGroup)
        {
            stream.WriteFixedLengthString(visGroup.Name ?? "", 128);
            WriteColor(stream, visGroup.Color);
            stream.WriteByte(0);            // Padding?
            stream.WriteInt(visGroup.ID);
            stream.WriteByte((byte)(visGroup.IsVisible ? 1 : 0));
            stream.WriteBytes(new byte[3]); // More padding?
        }

        private static void WriteMapObject(Stream stream, MapObject mapObject)
        {
            switch (mapObject)
            {
                case Entity entity: WriteEntity(stream, entity); break;
                case Brush brush: WriteBrush(stream, brush); break;
                case Group group: WriteGroup(stream, group); break;
                default: throw new NotSupportedException($"Map objects of type '{mapObject?.GetType().Name}' cannot be saved to an RMF file.");
            }
        }

        private static void WriteBrush(Stream stream, Brush brush)
        {
            stream.WriteNString("CMapSolid");
            stream.WriteInt(brush.VisGroups.FirstOrDefault()?.ID ?? 0);
            WriteColor(stream, brush.Color);
            stream.WriteInt(0);     // Child object count

            stream.WriteInt(brush.Faces.Count);
            foreach (var face in brush.Faces)
                WriteFace(stream, face);
        }

        private static void WriteFace(Stream stream, Face face)
        {
            var rmfFace = face as RmfFace;

            stream.WriteFixedLengthString(face.TextureName, 256);
            stream.WriteInt(rmfFace?.UnknownData1 ?? 0);

            WriteVector3D(stream, face.TextureRightAxis);
            stream.WriteFloat(face.TextureShift.X);
            WriteVector3D(stream, face.TextureDownAxis);
            stream.WriteFloat(face.TextureShift.Y);

            stream.WriteFloat(face.TextureAngle);
            stream.WriteFloat(face.TextureScale.X);
            stream.WriteFloat(face.TextureScale.Y);

            stream.WriteBytes(rmfFace?.UnknownData2?.Length == 16 ? rmfFace.UnknownData2 : new byte[16]);

            stream.WriteInt(face.Vertices.Count);
            foreach (var vertex in face.Vertices)
                WriteVector3D(stream, vertex);

            var planePoints = face.PlanePoints;
            if (planePoints.Length < 3)
                planePoints = face.Vertices.Take(3).ToArray();
            for (int i = 0; i < 3; i++)
                WriteVector3D(stream, planePoints[i]);
        }

        private static void WriteEntity(Stream stream, Entity entity)
        {
            var rmfEntity = entity as RmfEntity;

            stream.WriteNString("CMapEntity");
            stream.WriteInt(entity.VisGroups.FirstOrDefault()?.ID ?? 0);
            WriteColor(stream, entity.Color);

            stream.WriteInt(entity.Brushes.Count);
            foreach (var brush in entity.Brushes)
                WriteBrush(stream, brush);

            WriteEntityData(stream, entity);

            stream.WriteBytes(rmfEntity?.UnknownData1?.Length == 12 ? rmfEntity.UnknownData1 : new byte[12]);   // Unknown

            stream.WriteShort((short)(rmfEntity?.EntityType ?? (entity.IsPointBased ? RmfEntityType.PointBased : RmfEntityType.BrushBased)));
            WriteVector3D(stream, entity.Origin);

            stream.WriteInt(rmfEntity?.UnknownData2 ?? 0);  // Unknown
        }

        private static void WriteEntityData(Stream stream, Entity entity)
        {
            stream.WriteNString(entity.ClassName);
            stream.WriteBytes(new byte[4]);             // Unknown
            stream.WriteInt(entity.Spawnflags);

            // NOTE: This behavior matches Hammer, but J.A.C.K. will include some of these property names, which can cause interoperability problems between J.A.C.K. and Hammer.
            var properties = entity.Properties
                .Where(property => (property.Key != Attributes.Classname || entity.ClassName == Entities.Worldspawn) && property.Key != Attributes.Spawnflags && property.Key != Attributes.Origin)
                .ToDictionary(property => property.Key, property => property.Value);

            stream.WriteInt(properties.Count);
            foreach (var property in properties)
            {
                stream.WriteNString(property.Key);
                stream.WriteNString(property.Value);
            }
        }

        private static void WriteGroup(Stream stream, Group group)
        {
            stream.WriteNString("CMapGroup");
            stream.WriteInt(group.VisGroups.FirstOrDefault()?.ID ?? 0);
            WriteColor(stream, group.Color);

            stream.WriteInt(group.Objects.Count);
            foreach (var mapObject in group.Objects)
                WriteMapObject(stream, mapObject);
        }

        private static void WriteColor(Stream stream, Color color)
        {
            var rgb = new byte[] { color.R, color.G, color.B };
            stream.WriteBytes(rgb);
        }

        private static void WriteVector3D(Stream stream, Vector3D vector)
        {
            stream.WriteFloat(vector.X);
            stream.WriteFloat(vector.Y);
            stream.WriteFloat(vector.Z);
        }

        private static void WritePath(Stream stream, EntityPath entityPath)
        {
            stream.WriteFixedLengthString(entityPath.Name, 128);
            stream.WriteFixedLengthString(entityPath.ClassName, 128);
            stream.WriteInt((int)entityPath.Type);

            stream.WriteInt(entityPath.Nodes.Count);
            foreach (var pathNode in entityPath.Nodes)
                WritePathNode(stream, pathNode);
        }

        private static void WritePathNode(Stream stream, EntityPathNode pathNode)
        {
            WriteVector3D(stream, pathNode.Position);
            stream.WriteInt(pathNode.Index);
            stream.WriteFixedLengthString(pathNode.NameOverride ?? "", 128);

            stream.WriteInt(pathNode.Properties.Count);
            foreach (var property in pathNode.Properties)
            {
                stream.WriteNString(property.Key);
                stream.WriteNString(property.Value);
            }
        }

        private static void WriteCamera(Stream stream, Camera camera)
        {
            WriteVector3D(stream, camera.EyePosition);
            WriteVector3D(stream, camera.LookAtPosition);
        }
    }


    public class RmfFileSaveSettings : FileSaveSettings
    {
        public RmfFileVersion FileVersion { get; set; } = RmfFileVersion.V2_2;
    }

    public enum RmfFileVersion
    {
        /// <summary>
        /// Version 2.2.
        /// </summary>
        V2_2,
    }
}
