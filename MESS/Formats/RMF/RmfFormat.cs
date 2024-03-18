using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Text;

namespace MESS.Formats.RMF
{
    /// <summary>
    /// The binary RMF file format (Rich Map Format) not only stores entities and brushes,
    /// but also supports logical and visual groups, camera's and paths.
    /// </summary>
    public static class RmfFormat
    {
        public static Map Load(Stream stream, RmfFileLoadSettings? settings = null, ILogger? logger = null)
            => new RmfLoader(stream, settings, logger).LoadMap();

        public static void Save(Map map, Stream stream, RmfFileSaveSettings? settings = null, ILogger? logger = null)
            => new RmfSaver(stream, settings, logger).SaveMap(map);


        class RmfLoader : IOContext<RmfFileLoadSettings>
        {
            private RmfFileVersion FileVersion { get; set; }


            public RmfLoader(Stream stream, RmfFileLoadSettings? settings, ILogger? logger)
                : base(stream, settings ?? new RmfFileLoadSettings(), logger)
            {
            }

            public Map LoadMap()
            {
                var map = new Map();
                var nextGroupID = 1;

                FileVersion = ReadFileVersion();
                var rmfFileSignature = Stream.ReadFixedLengthString(3);
                if (rmfFileSignature != "RMF")
                    throw new InvalidDataException($"Expected 'RMF' magic string, but found '{rmfFileSignature}'.");

                var visGroupCount = Stream.ReadInt();
                for (int i = 0; i < visGroupCount; i++)
                    map.VisGroups.Add(ReadVisGroup());
                var visGroupIdLookup = map.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

                var cMapWorld = Stream.ReadNString();
                if (cMapWorld != "CMapWorld")
                    throw new InvalidDataException($"Expected a CMapWorld object, but found '{cMapWorld}'.");

                Stream.ReadInt();   // VIS group ID, always 0
                map.Worldspawn.Color = ReadColor();

                var objectCount = Stream.ReadInt();
                for (int i = 0; i < objectCount; i++)
                {
                    (var mapObject, var visGroupID) = ReadMapObject();
                    if (visGroupID > 0 && visGroupIdLookup.TryGetValue(visGroupID, out var visGroup) && visGroup != null)
                        visGroup.AddObject(mapObject);

                    AddObject(mapObject);

                    if (mapObject is IRmfIndexedObject indexedObject)
                        indexedObject.RmfIndex = i;
                }

                ReadEntityData(map.Worldspawn);
                var unknown1 = Stream.ReadBytes(12);

                var pathCount = Stream.ReadInt();
                for (int i = 0; i < pathCount; i++)
                    map.EntityPaths.Add(ReadPath());

                var docInfoBuffer = new byte[8];
                if (Stream.Read(docInfoBuffer, 0, docInfoBuffer.Length) == docInfoBuffer.Length)
                {
                    var docInfoMagicString = Encoding.ASCII.GetString(docInfoBuffer);
                    if (docInfoMagicString != "DOCINFO\0")
                        throw new InvalidDataException($"Expected 'DOCINFO' magic string, but found '{docInfoMagicString}'.");

                    var cameraVersion = Stream.ReadFloat();
                    map.ActiveCameraIndex = Stream.ReadInt();
                    var cameraCount = Stream.ReadInt();
                    for (int i = 0; i < cameraCount; i++)
                        map.Cameras.Add(ReadCamera());
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


            private RmfFileVersion ReadFileVersion()
            {
                var version = Stream.ReadFloat();
                switch (version)
                {
                    case 1.6f: return RmfFileVersion.V1_6;
                    case 1.8f: return RmfFileVersion.V1_8;
                    case 2.2f: return RmfFileVersion.V2_2;
                    default: throw new NotSupportedException($"Rmf file version {version} is not supported.");
                }
            }

            private VisGroup ReadVisGroup()
            {
                var name = Stream.ReadFixedLengthString(128);
                var color = ReadColor();
                Stream.ReadByte();      // Unknown (padding?)
                var id = Stream.ReadInt();
                var isVisible = Stream.ReadSingleByte() == 1;
                Stream.ReadBytes(3);    // Unknown (padding?)

                return new VisGroup {
                    Name = name,
                    Color = color,
                    ID = id,
                    IsVisible = isVisible,
                };
            }

            private (MapObject mapObject, int visGroupID) ReadMapObject()
            {
                var typeName = Stream.ReadNString();
                switch (typeName)
                {
                    case "CMapSolid": return ReadBrush();
                    case "CMapEntity": return ReadEntity();
                    case "CMapGroup": return ReadGroup();
                    default: throw new InvalidDataException($"Unknown object type '{typeName}'.");
                }
            }

            private (Brush brush, int visGroupID) ReadBrush()
            {
                var visGroupID = Stream.ReadInt();
                var color = ReadColor();
                var childObjectCount = Stream.ReadInt();
                if (childObjectCount != 0)
                    throw new InvalidDataException($"Expected 0 child objects for brush, but found {childObjectCount}.");

                var faces = new Face[Stream.ReadInt()];
                for (int i = 0; i < faces.Length; i++)
                    faces[i] = ReadFace();

                return (new RmfBrush(faces) { Color = color }, visGroupID);
            }

            private Face ReadFace()
            {
                var face = new RmfFace();

                face.TextureName = Stream.ReadFixedLengthString(GetTextureNameLength(FileVersion));

                var hasUVAxis = HasUVAxis(FileVersion);
                if (hasUVAxis)
                    face.TextureRightAxis = ReadVector3D();

                var shiftX = Stream.ReadFloat();

                if (hasUVAxis)
                    face.TextureDownAxis = ReadVector3D();

                var shiftY = Stream.ReadFloat();
                face.TextureShift = new Vector2D(shiftX, shiftY);

                face.TextureAngle = Stream.ReadFloat();
                face.TextureScale = new Vector2D(Stream.ReadFloat(), Stream.ReadFloat());

                var unknownData1Length = FileVersion >= RmfFileVersion.V1_8 ? 16 : 4;
                face.UnknownData1 = Stream.ReadBytes(unknownData1Length);

                var vertexCount = Stream.ReadInt();
                for (int i = 0; i < vertexCount; i++)
                    face.Vertices.Add(ReadVector3D());

                face.PlanePoints = Enumerable.Range(0, 3)
                    .Select(i => ReadVector3D())
                    .ToArray();
                face.Plane = Plane.FromPoints(face.PlanePoints);

                if (!hasUVAxis)
                {
                    var normalX = Math.Abs(face.Plane.Normal.X);
                    var normalY = Math.Abs(face.Plane.Normal.Y);
                    var normalZ = Math.Abs(face.Plane.Normal.Z);
                    if (normalZ >= normalX && normalZ >= normalY)
                    {
                        face.TextureRightAxis = new Vector3D(1, 0, 0);
                        face.TextureDownAxis = new Vector3D(0, -1, 0);
                    }
                    else if (normalX >= normalY)
                    {
                        face.TextureRightAxis = new Vector3D(0, 1, 0);
                        face.TextureDownAxis = new Vector3D(0, 0, -1);
                    }
                    else
                    {
                        face.TextureRightAxis = new Vector3D(1, 0, 0);
                        face.TextureDownAxis = new Vector3D(0, 0, -1);
                    }
                }

                return face;
            }

            private (Entity entity, int visGroupID) ReadEntity()
            {
                var visGroupID = Stream.ReadInt();
                var color = ReadColor();

                var brushCount = Stream.ReadInt();
                var brushes = new Brush[brushCount];
                for (int i = 0; i < brushCount; i++)
                {
                    var cMapSolid = Stream.ReadNString();
                    if (cMapSolid != "CMapSolid")
                        throw new InvalidDataException($"Expected a CMapSolid child object for entity, but found a '{cMapSolid}'.");

                    brushes[i] = ReadBrush().brush;
                }

                var entity = new RmfEntity(brushes);
                entity.Color = color;

                ReadEntityData(entity);

                entity.UnknownData1 = Stream.ReadBytes(12);
                entity.EntityType = (RmfEntityType)Stream.ReadShort();

                var origin = ReadVector3D();
                if (entity.EntityType == RmfEntityType.PointBased)
                    entity.Origin = origin;

                entity.UnknownData2 = Stream.ReadInt();

                return (entity, visGroupID);
            }

            private void ReadEntityData(Entity entity)
            {
                entity.ClassName = Stream.ReadNString();
                var unknown1 = Stream.ReadInt();
                entity.Spawnflags = Stream.ReadInt();

                var propertyCount = Stream.ReadInt();
                for (int i = 0; i < propertyCount; i++)
                {
                    var key = Stream.ReadNString();
                    var value = Stream.ReadNString();

                    if (key == Attributes.Spawnflags)
                    {
                        var isNumeric = int.TryParse(value, out var numericValue);
                        if (!isNumeric)
                            numericValue = 0;

                        if (!isNumeric || numericValue != entity.Spawnflags)
                        {
                            if (Settings.SpawnflagsPropertyHandling == RmfSpawnflagsPropertyHandling.UseField)
                            {
                                Logger.Warning($"Entity of type '{entity.ClassName}' contains a 'spawnflags' key, which will be ignored (spawnflags: {entity.Spawnflags}, key value: {value}).");
                            }
                            else if (Settings.SpawnflagsPropertyHandling == RmfSpawnflagsPropertyHandling.UseProperty)
                            {
                                // TODO: Handle duplicate spawnflags properties?
                                Logger.Warning($"Entity of type '{entity.ClassName}' contains a 'spawnflags' key, which will override the spawnflags field (spawnflags: {entity.Spawnflags}, key value: {value}).");
                                entity.Spawnflags = numericValue;
                            }
                            else if (Settings.SpawnflagsPropertyHandling == RmfSpawnflagsPropertyHandling.Fail)
                            {
                                var errorMessage = $"Entity of type '{entity.ClassName}' contains a 'spawnflags' key (spawnflags: {entity.Spawnflags}, key value: {value}).";
                                Logger.Error(errorMessage);
                                throw new MapLoadException(errorMessage);
                            }
                        }

                        continue;
                    }

                    if (entity.Properties.ContainsKey(key))
                    {
                        if (Settings.DuplicateKeyHandling == DuplicateKeyHandling.UseFirst)
                        {
                            Logger.Warning($"Entity of type '{entity.ClassName}' contains duplicate key '{key}', using first value: '{entity.Properties[key]}'.");
                            continue;
                        }
                        else if (Settings.DuplicateKeyHandling == DuplicateKeyHandling.UseLast)
                        {
                            Logger.Warning($"Entity of type '{entity.ClassName}' contains duplicate key '{key}', using last value: '{value}'.");
                        }
                        else if (Settings.DuplicateKeyHandling == DuplicateKeyHandling.Fail)
                        {
                            var errorMessage = $"Entity of type '{entity.ClassName}' contains duplicate key '{key}' (first value: '{entity.Properties[key]}', duplicate value: '{value}'.";
                            Logger.Error(errorMessage);
                            throw new DuplicateKeyException(errorMessage);
                        }
                    }

                    entity.Properties[key] = value;
                }
            }

            private (Group group, int visGroupID) ReadGroup()
            {
                var group = new RmfGroup();

                var visGroupID = Stream.ReadInt();
                group.Color = ReadColor();

                var objectCount = Stream.ReadInt();
                for (int i = 0; i < objectCount; i++)
                    group.AddObject(ReadMapObject().mapObject);

                return (group, visGroupID);
            }

            private Color ReadColor()
            {
                var colors = Stream.ReadBytes(3);
                if (FileVersion >= RmfFileVersion.V2_2)
                    return new Color(colors[0], colors[1], colors[2]);
                else
                    return new Color(colors[2], colors[1], colors[0]);
            }

            private Vector3D ReadVector3D() => new Vector3D(Stream.ReadFloat(), Stream.ReadFloat(), Stream.ReadFloat());

            private EntityPath ReadPath()
            {
                var entityPath = new EntityPath();
                entityPath.Name = Stream.ReadFixedLengthString(128);
                entityPath.ClassName = Stream.ReadFixedLengthString(128);
                entityPath.Type = (PathType)Stream.ReadInt();

                var nodeCount = Stream.ReadInt();
                for (int i = 0; i < nodeCount; i++)
                    entityPath.Nodes.Add(ReadPathNode());

                return entityPath;
            }

            private EntityPathNode ReadPathNode()
            {
                var pathNode = new RmfEntityPathNode();
                pathNode.Position = ReadVector3D();
                pathNode.CreationOrder = Stream.ReadInt();
                pathNode.NameOverride = Stream.ReadFixedLengthString(128);

                var propertyCount = Stream.ReadInt();
                for (int i = 0; i < propertyCount; i++)
                    pathNode.Properties[Stream.ReadNString()] = Stream.ReadNString();

                return pathNode;
            }

            private Camera ReadCamera()
            {
                return new Camera {
                    EyePosition = ReadVector3D(),
                    LookAtPosition = ReadVector3D(),
                };
            }
        }

        class RmfSaver : IOContext<RmfFileSaveSettings>
        {
            public RmfSaver(Stream stream, RmfFileSaveSettings? settings, ILogger? logger)
                : base(stream, settings ?? new RmfFileSaveSettings(), logger)
            {
            }

            public void SaveMap(Map map)
            {
                WriteFileVersion(Settings.FileVersion);
                Stream.WriteFixedLengthString("RMF");

                Stream.WriteInt(map.VisGroups.Count);
                foreach (var visGroup in map.VisGroups)
                    WriteVisGroup(visGroup);


                Stream.WriteNString("CMapWorld");
                Stream.WriteInt(0);     // VIS group ID
                WriteColor(map.Worldspawn.Color);

                var topLevelObjects = new List<MapObject>();
                topLevelObjects.AddRange(map.WorldGeometry.Where(brush => brush.Group == null));
                topLevelObjects.AddRange(map.Entities.Where(entity => entity.Group == null));
                topLevelObjects.AddRange(map.Groups.Where(group => group.Group == null));
                topLevelObjects = topLevelObjects.OrderBy(mapObject => mapObject is IRmfIndexedObject indexedObject ? indexedObject.RmfIndex : int.MaxValue).ToList();

                Stream.WriteInt(topLevelObjects.Count);
                foreach (var mapObject in topLevelObjects)
                    WriteMapObject(mapObject);

                // worldspawn & map properties:
                WriteEntityData(map.Worldspawn);
                Stream.WriteBytes(new byte[12]);    // Unknown

                // Paths:
                Stream.WriteInt(map.EntityPaths.Count);
                foreach (var entityPath in map.EntityPaths)
                    WritePath(entityPath);

                // Cameras:
                Stream.WriteFixedLengthString("DOCINFO\0");
                Stream.WriteFloat(0.2f);
                Stream.WriteInt(map.ActiveCameraIndex ?? -1);
                Stream.WriteInt(map.Cameras.Count);
                foreach (var camera in map.Cameras)
                    WriteCamera(camera);
            }


            private void WriteFileVersion(RmfFileVersion version)
            {
                switch (version)
                {
                    case RmfFileVersion.V2_2: Stream.WriteFloat(2.2f); break;
                    default: throw new NotSupportedException($"Rmf file version {version} is not supported.");
                }
            }

            private void WriteVisGroup(VisGroup visGroup)
            {
                ValidateAndWriteFixedLengthString($"VIS group '{visGroup.Name}'", visGroup.Name ?? "", 128);
                WriteColor(visGroup.Color);
                Stream.WriteByte(0);            // Unknown (padding?)
                Stream.WriteInt(visGroup.ID);
                Stream.WriteByte((byte)(visGroup.IsVisible ? 1 : 0));
                Stream.WriteBytes(new byte[3]); // Unknown (padding?)
            }

            private void WriteMapObject(MapObject mapObject)
            {
                switch (mapObject)
                {
                    case Entity entity: WriteEntity(entity); break;
                    case Brush brush: WriteBrush(brush); break;
                    case Group group: WriteGroup(group); break;
                    default: throw new NotSupportedException($"Map objects of type '{mapObject?.GetType().Name}' cannot be saved to an RMF file.");
                }
            }

            private void WriteBrush(Brush brush)
            {
                Stream.WriteNString("CMapSolid");
                ValidateAndWriteVisGroupID("Brush", brush);
                WriteColor(brush.Color);
                Stream.WriteInt(0);     // Child object count

                Stream.WriteInt(brush.Faces.Count);
                foreach (var face in brush.Faces)
                    WriteFace(face);
            }

            private void WriteFace(Face face)
            {
                var rmfFace = face as RmfFace;

                WriteTextureName(face.TextureName);

                var hasUVAxis = HasUVAxis(Settings.FileVersion);
                if (hasUVAxis)
                    WriteVector3D(face.TextureRightAxis);

                Stream.WriteFloat(face.TextureShift.X);

                if (hasUVAxis)
                    WriteVector3D(face.TextureDownAxis);

                Stream.WriteFloat(face.TextureShift.Y);

                Stream.WriteFloat(face.TextureAngle);
                Stream.WriteFloat(face.TextureScale.X);
                Stream.WriteFloat(face.TextureScale.Y);

                var unknownData1Length = Settings.FileVersion >= RmfFileVersion.V1_8 ? 16 : 4;
                Stream.WriteBytes(rmfFace?.UnknownData1?.Length == unknownData1Length ? rmfFace.UnknownData1 : new byte[unknownData1Length]);

                Stream.WriteInt(face.Vertices.Count);
                foreach (var vertex in face.Vertices)
                    WriteVector3D(vertex);

                var planePoints = face.PlanePoints;
                if (planePoints.Length < 3)
                    planePoints = face.Vertices.Take(3).ToArray();
                for (int i = 0; i < 3; i++)
                    WriteVector3D(planePoints[i]);
            }

            private void WriteTextureName(string textureName)
            {
                var maxLength = GetTextureNameLength(Settings.FileVersion);
                textureName = Validation.ValidateTextureName(textureName, maxLength, Settings, Logger, Encoding.ASCII);
                Stream.WriteFixedLengthString(textureName, maxLength);
            }

            private void WriteEntity(Entity entity)
            {
                var rmfEntity = entity as RmfEntity;

                Stream.WriteNString("CMapEntity");
                ValidateAndWriteVisGroupID($"Entity of type '{entity.ClassName}'", entity);
                WriteColor(entity.Color);

                Stream.WriteInt(entity.Brushes.Count);
                foreach (var brush in entity.Brushes)
                    WriteBrush(brush);

                WriteEntityData(entity);

                Stream.WriteBytes(rmfEntity?.UnknownData1?.Length == 12 ? rmfEntity.UnknownData1 : new byte[12]);   // Unknown

                Stream.WriteShort((short)(rmfEntity?.EntityType ?? (entity.IsPointBased ? RmfEntityType.PointBased : RmfEntityType.BrushBased)));
                WriteVector3D(entity.Origin);

                Stream.WriteInt(rmfEntity?.UnknownData2 ?? 0);  // Unknown
            }

            private void WriteEntityData(Entity entity)
            {
                var logDescription = $"Entity of type '{entity.ClassName}'";

                var className = Validation.ValidateValue(entity.ClassName, 255, Settings, Logger, logDescription, mustBeNullTerminated: true);
                Stream.WriteNString(className ?? "", truncate: true);

                Stream.WriteBytes(new byte[4]);             // Unknown
                Stream.WriteInt(entity.Spawnflags);

                // NOTE: This behavior matches Hammer, but J.A.C.K. will include some of these property names, which can cause interoperability problems between J.A.C.K. and Hammer.
                var properties = entity.Properties
                    .Where(property => property.Key != Attributes.Classname && property.Key != Attributes.Spawnflags && property.Key != Attributes.Origin)
                    .ToDictionary(property => property.Key, property => property.Value);

                Stream.WriteInt(properties.Count);
                foreach (var property in properties)
                {
                    var key = Validation.ValidateKey(property.Key, 255, Settings, Logger, logDescription, mustBeNullTerminated: true);
                    Stream.WriteNString(key ?? "", truncate: true);

                    var value = Validation.ValidateValue(property.Value, 255, Settings, Logger, logDescription, mustBeNullTerminated: true);
                    Stream.WriteNString(value ?? "", truncate: true);
                }
            }

            private void WriteGroup(Group group)
            {
                Stream.WriteNString("CMapGroup");
                ValidateAndWriteVisGroupID("Group", group);
                WriteColor(group.Color);

                Stream.WriteInt(group.Objects.Count);
                foreach (var mapObject in group.Objects)
                    WriteMapObject(mapObject);
            }

            private void WriteColor(Color color)
            {
                var rgb = Settings.FileVersion >= RmfFileVersion.V2_2 ? new byte[] { color.R, color.G, color.B } : new byte[] { color.B, color.G, color.R };
                Stream.WriteBytes(rgb);
            }

            private void WriteVector3D(Vector3D vector)
            {
                Stream.WriteFloat(vector.X);
                Stream.WriteFloat(vector.Y);
                Stream.WriteFloat(vector.Z);
            }

            private void WritePath(EntityPath entityPath)
            {
                var logDescription = $"Path of type '{entityPath.ClassName}' (name: '{entityPath.Name}')";

                ValidateAndWriteFixedLengthString(logDescription, entityPath.Name, 128);
                ValidateAndWriteFixedLengthString(logDescription, entityPath.ClassName, 128);
                Stream.WriteInt((int)entityPath.Type);

                Stream.WriteInt(entityPath.Nodes.Count);
                for (int i = 0; i < entityPath.Nodes.Count; i++)
                    WritePathNode(entityPath.Nodes[i], i, entityPath.ClassName);
            }

            private void WritePathNode(EntityPathNode pathNode, int index, string className)
            {
                var logDescription = $"Path node of type '{className}' (position: {pathNode.Position})";

                var rmfPathNode = pathNode as RmfEntityPathNode;
                WriteVector3D(pathNode.Position);
                Stream.WriteInt(rmfPathNode?.CreationOrder ?? index + 1);
                ValidateAndWriteFixedLengthString(logDescription, pathNode.NameOverride ?? "", 128);

                Stream.WriteInt(pathNode.Properties.Count);
                foreach (var property in pathNode.Properties)
                {
                    var key = Validation.ValidateKey(property.Key, 255, Settings, Logger, logDescription, mustBeNullTerminated: true);
                    Stream.WriteNString(key, truncate: true);

                    var value = Validation.ValidateValue(property.Value, 255, Settings, Logger, logDescription, mustBeNullTerminated: true);
                    Stream.WriteNString(value, truncate: true);
                }
            }

            private void WriteCamera(Camera camera)
            {
                WriteVector3D(camera.EyePosition);
                WriteVector3D(camera.LookAtPosition);
            }


            private void ValidateAndWriteVisGroupID(string objectDescription, MapObject mapObject)
            {
                var visGroupID = mapObject.VisGroups.FirstOrDefault()?.ID ?? 0;
                if (mapObject.VisGroups.Count > 1)
                {
                    if (Settings.TooManyVisGroupsHandling == TooManyVisGroupsHandling.UseFirst)
                    {
                        Logger.Warning($"{objectDescription} is part of {mapObject.VisGroups.Count} VIS groups, using first VIS group.");
                    }
                    else if (Settings.TooManyVisGroupsHandling == TooManyVisGroupsHandling.UseLast)
                    {
                        Logger.Warning($"{objectDescription} is part of {mapObject.VisGroups.Count} VIS groups, using last VIS group.");
                        visGroupID = mapObject.VisGroups.LastOrDefault()?.ID ?? 0;
                    }
                    else if (Settings.TooManyVisGroupsHandling == TooManyVisGroupsHandling.Fail)
                    {
                        var errorMessage = $"{objectDescription} is part of {mapObject.VisGroups.Count} VIS groups.";
                        Logger.Error(errorMessage);
                        throw new MapSaveException(errorMessage);
                    }
                }

                Stream.WriteInt(visGroupID);
            }

            private void ValidateAndWriteFixedLengthString(string objectDescription, string value, int maxLength)
            {
                var rawValue = Encoding.ASCII.GetBytes(value);

                if (rawValue.Length > maxLength)
                {
                    if (Settings.KeyValueTooLongHandling == ValueTooLongHandling.Truncate)
                    {
                        Logger.Warning($"{objectDescription} contains a value that is too long: '{value}', truncating value.");
                    }
                    else if (Settings.KeyValueTooLongHandling == ValueTooLongHandling.Fail)
                    {
                        var errorMessage = $"{objectDescription} contains a value that is too long: '{value}'.";
                        Logger.Error(errorMessage);
                        throw new MapSaveException(errorMessage);
                    }
                }

                Stream.WriteFixedLengthString(value, maxLength);
            }
        }

        private static int GetTextureNameLength(RmfFileVersion fileVersion) => fileVersion > RmfFileVersion.V1_6 ? 260 : 40;

        private static bool HasUVAxis(RmfFileVersion fileVersion) => fileVersion >= RmfFileVersion.V2_2;

        private static bool IsValidNStringSize(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            var isNullTerminated = bytes.Length > 0 && bytes[bytes.Length - 1] == 0;

            return (isNullTerminated ? bytes.Length : bytes.Length + 1) <= 255;
        }
    }


    public enum RmfFileVersion
    {
        /// <summary>
        /// Version 1.6. Used by Worldcraft 1.5b.
        /// </summary>
        V1_6,

        /// <summary>
        /// Version 1.8. Used by Worldcraft 1.6 to 2.1.
        /// This version increases the maximum length of texture names.
        /// </summary>
        V1_8,

        /// <summary>
        /// Version 2.2. Used by Worldcraft 3.3 and Hammer 3.4 and 3.5.
        /// This version adds support for texture UV axis.
        /// </summary>
        V2_2,
    }
}
