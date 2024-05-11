using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Text;

namespace MESS.Formats.JMF
{
    /// <summary>
    /// JMF is J.A.C.K.'s map format. Similar to the RMF format,
    /// it also supports logical and visual groups, camera's and paths.
    /// Unlike RMF, JMF supports objects being part of multiple visual groups.
    /// </summary>
    public class JmfFormat
    {
        // TODO: This list depends on fgd settings, and may not always be the same for every entity!
        private static string[] SerializedAttributeNames { get; } = new[] { "spawnflags", "origin", "angles", "scale", "targetname", "target", "skyname", "model", "model", "texture", "model", "model", "script" };


        public static Map Load(Stream stream, FileLoadSettings? settings = null, ILogger? logger = null)
            => new JmfLoader(stream, settings, logger).LoadMap();

        public static void Save(Map map, Stream stream, JmfFileSaveSettings? settings = null, ILogger? logger = null)
            => new JmfSaver(stream, settings, logger).SaveMap(map);


        class JmfLoader : IOContext<FileLoadSettings>
        {
            public JmfLoader(Stream stream, FileLoadSettings? settings, ILogger? logger)
                : base(stream, settings ?? new FileLoadSettings(), logger)
            {
            }

            public Map LoadMap()
            {
                var map = new JmfMap();
                map.VisGroupAssignment = VisGroupAssignment.PerObject;
                map.HasColorInformation = true;

                var jhmfFileSignature = Stream.ReadFixedLengthString(4);
                if (jhmfFileSignature != "JHMF")
                    throw new InvalidDataException($"Expected 'JHMF' magic string, but found '{jhmfFileSignature}'.");

                var fileVersion = Stream.ReadInt();
                if (fileVersion < 121 || fileVersion > 122)
                    throw new NotSupportedException($"Only JMF file versions 121 and 122 are supported.");

                // Recent export paths:
                var exportPathCount = Stream.ReadInt();
                for (int i = 0; i < exportPathCount; i++)
                    map.RecentExportPaths.Add(Stream.ReadLengthPrefixedString() ?? "");

                if (fileVersion >= 122)
                {
                    // 2D view background images:
                    map.FrontViewBackgroundImage = ReadBackgroundImageSettings();
                    map.SideViewBackgroundImage = ReadBackgroundImageSettings();
                    map.TopViewBackgroundImage = ReadBackgroundImageSettings();
                }

                // Groups can be nested:
                var groupCount = Stream.ReadInt();
                var parentGroupIDs = new Dictionary<int, int>();
                for (int i = 0; i < groupCount; i++)
                {
                    (var group, var parentGroupID) = ReadGroup();
                    if (parentGroupID != 0)
                        parentGroupIDs[group.ID] = parentGroupID;

                    map.AddGroup(group);
                }
                var groups = map.Groups.ToDictionary(group => group.ID, group => group);
                foreach (var kv in parentGroupIDs)
                    groups[kv.Value].AddObject(groups[kv.Key]);

                // Objects can be part of multiple VIS groups:
                var visGroupCount = Stream.ReadInt();
                for (int i = 0; i < visGroupCount; i++)
                    map.AddVisGroup(ReadVisGroup());
                var visGroups = map.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

                var cordonMin = ReadVector3D();
                var cordonMax = ReadVector3D();
                map.CordonArea = new BoundingBox(cordonMin, cordonMax);

                var cameraCount = Stream.ReadInt();
                for (int i = 0; i < cameraCount; i++)
                {
                    (var camera, var isSelected) = ReadCamera();
                    map.Cameras.Add(camera);

                    if (isSelected)
                        map.ActiveCameraIndex = i;
                }

                var pathCount = Stream.ReadInt();
                for (int i = 0; i < pathCount; i++)
                    map.EntityPaths.Add(ReadEntityPath());

                try
                {
                    while (true)
                    {
                        var entity = ReadEntity(groups, visGroups);
                        if (entity.ClassName == Entities.Worldspawn)
                            map.Worldspawn = entity;
                        else
                            map.AddEntity(entity);
                    }
                }
                catch (EndOfStreamException)
                {
                    // End of file, no more entities.
                }

                return map;
            }


            private JmfBackgroundImageSettings ReadBackgroundImageSettings()
            {
                var settings = new JmfBackgroundImageSettings();

                settings.ImagePath = Stream.ReadLengthPrefixedString() ?? "";
                settings.Scale = Stream.ReadDouble();
                settings.Luminance = Stream.ReadInt();
                settings.Filtering = (ImageFiltering)Stream.ReadInt();
                settings.InvertColors = Stream.ReadInt() == 1;
                settings.OffsetX = Stream.ReadInt();
                settings.OffsetY = Stream.ReadInt();
                settings.UnknownData = Stream.ReadBytes(4);

                return settings;
            }

            private (Group group, int parentGroupID) ReadGroup()
            {
                var group = new Group();
                group.ID = Stream.ReadInt();
                var parentGroupID = Stream.ReadInt();

                var flags = (JmfMapObjectFlags)Stream.ReadInt();
                group.IsSelected = flags.HasFlag(JmfMapObjectFlags.Selected);
                group.IsHidden = flags.HasFlag(JmfMapObjectFlags.Hidden);

                var count = Stream.ReadInt();
                group.Color = ReadColor();

                return (group, parentGroupID);
            }

            private VisGroup ReadVisGroup()
            {
                var visGroup = new VisGroup();
                visGroup.Name = Stream.ReadLengthPrefixedString();
                visGroup.ID = Stream.ReadInt();
                visGroup.Color = ReadColor();
                visGroup.IsVisible = Stream.ReadSingleByte() == 1;
                return visGroup;
            }

            private (Camera camera, bool isSelected) ReadCamera()
            {
                var camera = new Camera();
                camera.EyePosition = ReadVector3D();
                camera.LookAtPosition = ReadVector3D();
                var isSelected = ((JmfMapObjectFlags)Stream.ReadInt()).HasFlag(JmfMapObjectFlags.Selected);
                camera.Color = ReadColor();
                return (camera, isSelected);
            }

            private EntityPath ReadEntityPath()
            {
                var path = new JmfEntityPath();
                path.ClassName = Stream.ReadLengthPrefixedString() ?? "";
                path.Name = Stream.ReadLengthPrefixedString() ?? "";
                path.Type = (PathType)Stream.ReadInt();
                path.UnknownData = Stream.ReadBytes(4);
                path.Color = ReadColor();

                var nodeCount = Stream.ReadInt();
                for (int i = 0; i < nodeCount; i++)
                    path.Nodes.Add(ReadPathNode());

                return path;
            }

            private EntityPathNode ReadPathNode()
            {
                var node = new EntityPathNode();
                node.NameOverride = Stream.ReadLengthPrefixedString();

                var fireOnTarget = Stream.ReadLengthPrefixedString();
                if (!string.IsNullOrEmpty(fireOnTarget))
                    node.Properties[Attributes.Message] = fireOnTarget;

                node.Position = ReadVector3D();

                var angles = ReadAngles();
                if (angles.Pitch != 0 || angles.Yaw != 0 || angles.Roll != 0)
                    node.Properties[Attributes.Angles] = FormattableString.Invariant($"{angles.Pitch} {angles.Yaw} {angles.Roll}");

                var flags = (JmfMapObjectFlags)Stream.ReadInt();
                node.IsSelected = flags.HasFlag(JmfMapObjectFlags.Selected);    // NOTE: Path nodes cannot be hidden in JACK, but this behavior may change.
                node.Color = ReadColor();

                var propertyCount = Stream.ReadInt();
                for (int i = 0; i < propertyCount; i++)
                {
                    var key = Stream.ReadLengthPrefixedString();
                    var value = Stream.ReadLengthPrefixedString();
                    node.Properties[key ?? ""] = value ?? "";
                }

                return node;
            }

            private Entity ReadEntity(IDictionary<int, Group> groups, IDictionary<int, VisGroup> visGroups)
            {
                var entity = new JmfEntity();

                entity.ClassName = Stream.ReadLengthPrefixedString() ?? "";
                entity.JmfOrigin = ReadVector3D();

                entity.JmfFlags = (JmfMapObjectFlags)Stream.ReadInt();
                entity.IsSelected = entity.JmfFlags.HasFlag(JmfMapObjectFlags.Selected);
                entity.IsHidden = entity.JmfFlags.HasFlag(JmfMapObjectFlags.Hidden);

                var groupID = Stream.ReadInt();
                if (groupID != 0 && groups.TryGetValue(groupID, out var group) && group != null)
                    group.AddObject(entity);

                var rootGroupID = Stream.ReadInt();
                entity.Color = ReadColor();

                // NOTE: Each entity contains a list of 'special' attribute names, which are almost always the same and don't seem to serve any purpose:
                for (int i = 0; i < 13; i++)
                    entity.SpecialAttributeNames.Add(Stream.ReadLengthPrefixedString());

                // This is internal editor state that can mostly be derived from entity properties,
                // but sometimes different properties are used depending on the type of entity,
                // and some data depends on fgd settings that are not replicated in the entity properties.
                // Still, for normal operations, this can be ignored because only the data in entity.Properties
                // is exported to a .map file:
                entity.JmfSpawnflags = Stream.ReadInt();
                entity.JmfAngles = ReadAngles();
                entity.JmfRendering = (JmfRenderMode)Stream.ReadInt();

                entity.JmfFxColor = ReadColor();
                entity.JmfRenderMode = Stream.ReadInt();
                entity.JmfRenderFX = Stream.ReadInt();
                entity.JmfBody = Stream.ReadShort();
                entity.JmfSkin = Stream.ReadShort();
                entity.JmfSequence = Stream.ReadInt();
                entity.JmfFramerate = Stream.ReadFloat();
                entity.JmfScale = Stream.ReadFloat();
                entity.JmfRadius = Stream.ReadFloat();
                entity.UnknownData = Stream.ReadBytes(28);

                var propertyCount = Stream.ReadInt();
                for (int i = 0; i < propertyCount; i++)
                {
                    var key = Stream.ReadLengthPrefixedString();
                    var value = Stream.ReadLengthPrefixedString();
                    entity.JmfProperties.Add(new KeyValuePair<string?, string?>(key, value));

                    key = key ?? "";
                    value = value ?? "";
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

                var visGroupCount = Stream.ReadInt();
                for (int i = 0; i < visGroupCount; i++)
                {
                    var visGroupID = Stream.ReadInt();
                    if (visGroups.TryGetValue(visGroupID, out var visGroup))
                        visGroup.AddObject(entity);
                }

                var brushCount = Stream.ReadInt();
                for (int i = 0; i < brushCount; i++)
                    entity.AddBrush(ReadBrush(groups, visGroups));

                if (entity.IsPointBased)
                    entity.Origin = entity.JmfOrigin;

                return entity;
            }

            private Brush ReadBrush(IDictionary<int, Group> groups, IDictionary<int, VisGroup> visGroups)
            {
                var isPatch = Stream.ReadInt() == 1;
                var flags = (JmfMapObjectFlags)Stream.ReadInt();
                var groupID = Stream.ReadInt();
                var rootGroupID = Stream.ReadInt();
                var color = ReadColor();

                var visGroupCount = Stream.ReadInt();
                var visGroupIDs = Enumerable.Range(0, visGroupCount)
                    .Select(i => Stream.ReadInt())
                    .ToArray();

                var faceCount = Stream.ReadInt();
                var faces = Enumerable.Range(0, faceCount)
                    .Select(i => ReadFace())
                    .ToArray();

                var brush = new JmfBrush(faces);
                brush.Color = color;
                if (isPatch)
                    brush.Patch = ReadPatch();


                if (groupID != 0 && groups.TryGetValue(groupID, out var group) && group != null)
                    group.AddObject(brush);

                foreach (var visGroupID in visGroupIDs)
                {
                    if (visGroups.TryGetValue(visGroupID, out var visGroup))
                        visGroup.AddObject(brush);
                }

                brush.JmfFlags = flags;
                brush.IsSelected = flags.HasFlag(JmfMapObjectFlags.Selected);
                brush.IsHidden = flags.HasFlag(JmfMapObjectFlags.Hidden);

                return brush;
            }

            private Face ReadFace()
            {
                var face = new JmfFace();

                face.RenderFlags = Stream.ReadInt();
                var vertexCount = Stream.ReadInt();
                face.TextureRightAxis = ReadVector3D();
                var textureShiftX = Stream.ReadFloat();
                face.TextureDownAxis = ReadVector3D();
                var textureShiftY = Stream.ReadFloat();
                face.TextureShift = new Vector2D(textureShiftX, textureShiftY);
                face.TextureScale = new Vector2D(Stream.ReadFloat(), Stream.ReadFloat());
                face.TextureAngle = Stream.ReadFloat();
                face.TextureAlignment = (JmfTextureAlignment)Stream.ReadInt();
                face.UnknownData = Stream.ReadBytes(12);
                face.Contents = (JmfSurfaceContents)Stream.ReadInt();
                face.TextureName = Stream.ReadFixedLengthString(64);

                var planeNormal = ReadVector3D();
                var planeDistance = Stream.ReadFloat();
                face.Plane = new Plane(planeNormal, planeDistance);
                face.AxisAlignment = (JmfAxisAlignment)Stream.ReadInt();

                for (int i = 0; i < vertexCount; i++)
                {
                    face.Vertices.Add(ReadVector3D());
                    face.VertexUVCoordinates.Add(new Vector2D(Stream.ReadFloat(), Stream.ReadFloat()));
                    face.VertexSelectionState.Add((JmfVertexSelection)Stream.ReadInt());
                }

                // Jmf files store vertices in counter-clockwise order:
                face.Vertices.Reverse();
                face.VertexUVCoordinates.Reverse();
                face.VertexSelectionState.Reverse();

                face.PlanePoints = face.Vertices.Take(3).ToArray();

                return face;
            }

            private JmfPatch ReadPatch()
            {
                var columns = Stream.ReadInt();
                var rows = Stream.ReadInt();

                var patch = new JmfPatch(columns, rows);

                patch.TextureRightAxis = ReadVector3D();
                var textureShiftX = Stream.ReadFloat();
                patch.TextureDownAxis = ReadVector3D();
                var textureShiftY = Stream.ReadFloat();
                patch.TextureShift = new Vector2D(textureShiftX, textureShiftY);
                patch.TextureScale = new Vector2D(Stream.ReadFloat(), Stream.ReadFloat());
                patch.TextureAngle = Stream.ReadFloat();
                patch.TextureAlignment = (JmfTextureAlignment)Stream.ReadInt();
                patch.UnknownData = Stream.ReadBytes(12);

                patch.Contents = (JmfSurfaceContents)Stream.ReadInt();
                patch.TextureName = Stream.ReadFixedLengthString(64);
                patch.UnknownData2 = Stream.ReadBytes(4);


                for (int column = 0; column < 32; column++)
                {
                    for (int row = 0; row < 32; row++)
                    {
                        var controlPoint = ReadPatchControlPoint();
                        if (column < columns && row < rows)
                            patch.ControlPoints[column, row] = controlPoint;
                    }
                }

                return patch;
            }

            private JmfPatchControlPoint ReadPatchControlPoint()
            {
                var controlPoint = new JmfPatchControlPoint();
                controlPoint.Position = ReadVector3D();
                controlPoint.Normal = ReadVector3D();
                controlPoint.UV = new Vector2D(Stream.ReadFloat(), Stream.ReadFloat());
                controlPoint.IsSelected = Stream.ReadInt() == 1;
                return controlPoint;
            }


            private Vector3D ReadVector3D() => new Vector3D(Stream.ReadFloat(), Stream.ReadFloat(), Stream.ReadFloat());

            private Color ReadColor()
            {
                var color = Stream.ReadBytes(4);    // RGBA
                return new Color(color[0], color[1], color[2], color[3]);
            }

            private Angles ReadAngles()
            {
                var pitch = Stream.ReadFloat();
                var yaw = Stream.ReadFloat();
                var roll = Stream.ReadFloat();
                return new Angles(roll, pitch, yaw);
            }
        }

        class JmfSaver : IOContext<JmfFileSaveSettings>
        {
            private Dictionary<int, int> _visGroupIDMapping = new Dictionary<int, int>();
            private VisGroupAssignment _visGroupAssignment;
            private bool _hasColorInformation;
            private Random _random = new Random();


            public JmfSaver(Stream stream, JmfFileSaveSettings? settings, ILogger? logger)
                : base(stream, settings ?? new JmfFileSaveSettings(), logger)
            {
            }

            public void SaveMap(Map map)
            {
                _visGroupAssignment = map.VisGroupAssignment;
                _hasColorInformation = map.HasColorInformation;

                Stream.WriteFixedLengthString("JHMF");
                Stream.WriteInt((int)Settings.FileVersion);

                // Export paths:
                var jmfMap = map as JmfMap;
                var recentExportPaths = jmfMap?.RecentExportPaths ?? new List<string>();
                Stream.WriteInt(recentExportPaths.Count);
                foreach (var exportPath in recentExportPaths)
                    Stream.WriteLengthPrefixedString(exportPath);

                if (Settings.FileVersion >= JmfFileVersion.V122)
                {
                    // 2D view background images:
                    WriteBackgroundImageSettings(jmfMap?.FrontViewBackgroundImage);
                    WriteBackgroundImageSettings(jmfMap?.SideViewBackgroundImage);
                    WriteBackgroundImageSettings(jmfMap?.TopViewBackgroundImage);
                }

                // Groups:
                Stream.WriteInt(map.Groups.Count);
                foreach (var group in map.Groups)
                    WriteGroup(group);

                // NOTE: Apparently JACK uses VISgroup IDs as indexes, because any ID higher than map.VisGroups.Count will cause it to crash.
                //       So we'll have to map IDs to a safe range (starting at 1):
                for (int i = 0; i < map.VisGroups.Count; i++)
                    _visGroupIDMapping[map.VisGroups[i].ID] = i + 1;

                // VIS groups:
                Stream.WriteInt(map.VisGroups.Count);
                foreach (var visGroup in map.VisGroups)
                    WriteVisGroup(visGroup);

                // Cordon:
                WriteVector3D(map.CordonArea?.Min ?? new Vector3D());
                WriteVector3D(map.CordonArea?.Max ?? new Vector3D());

                // Cameras:
                Stream.WriteInt(map.Cameras.Count);
                for (int i = 0; i < map.Cameras.Count; i++)
                    WriteCamera(map.Cameras[i], map.ActiveCameraIndex == i);

                // Paths:
                Stream.WriteInt(map.EntityPaths.Count);
                foreach (var entityPath in map.EntityPaths)
                    WriteEntityPath(entityPath);

                // Entities (and their brushes):
                WriteEntity(map.Worldspawn);
                foreach (var entity in map.Entities)
                    WriteEntity(entity);
            }


            private void WriteBackgroundImageSettings(JmfBackgroundImageSettings? settings)
            {
                Stream.WriteLengthPrefixedString(settings?.ImagePath ?? "");
                Stream.WriteDouble(settings?.Scale ?? 1.0);
                Stream.WriteInt(settings?.Luminance ?? 255);
                Stream.WriteInt((int)(settings?.Filtering ?? ImageFiltering.Linear));
                Stream.WriteInt(settings?.InvertColors == true ? 1 : 0);
                Stream.WriteInt(settings?.OffsetX ?? 0);
                Stream.WriteInt(settings?.OffsetY ?? 0);
                Stream.WriteBytes(GetFixedLengthByteArray(settings?.UnknownData, 4));
            }

            private void WriteGroup(Group group)
            {
                Stream.WriteInt(group.ID);
                Stream.WriteInt(group.Group?.ID ?? 0);
                Stream.WriteInt((int)GetJmfMapObjectFlags(group));
                Stream.WriteInt(group.Objects.Count);
                WriteColor(_hasColorInformation ? group.Color : GenerateRandomColor());
            }

            private void WriteVisGroup(VisGroup visGroup)
            {
                Stream.WriteLengthPrefixedString(visGroup.Name);
                Stream.WriteInt(_visGroupIDMapping[visGroup.ID]);
                WriteColor(_hasColorInformation ? visGroup.Color : GenerateRandomColor());
                Stream.WriteByte((byte)(visGroup.IsVisible ? 1 : 0));
            }

            private void WriteCamera(Camera camera, bool isSelected)
            {
                WriteVector3D(camera.EyePosition);
                WriteVector3D(camera.LookAtPosition);

                Stream.WriteInt((int)(isSelected ? JmfMapObjectFlags.Selected : JmfMapObjectFlags.None));
                WriteColor(_hasColorInformation ? camera.Color : GenerateRandomColor());
            }

            private void WriteEntityPath(EntityPath entityPath)
            {
                var logDescription = $"Path of type '{entityPath.ClassName}'";
                var jmfEntityPath = entityPath as JmfEntityPath;

                var className = Validation.ValidateValue(entityPath.ClassName, null, Settings, Logger, logDescription);
                Stream.WriteLengthPrefixedString(className);

                var name = Validation.ValidateValue(entityPath.Name, null, Settings, Logger, logDescription);
                Stream.WriteLengthPrefixedString(name);

                Stream.WriteInt((int)entityPath.Type);

                Stream.WriteBytes(GetFixedLengthByteArray(jmfEntityPath?.UnknownData, 4));
                WriteColor(entityPath.Color);

                Stream.WriteInt(entityPath.Nodes.Count);
                foreach (var pathNode in entityPath.Nodes)
                    WritePathNode(pathNode, entityPath.ClassName);
            }

            private void WritePathNode(EntityPathNode pathNode, string className)
            {
                var logDescription = $"Path node of type '{className}' (position: {pathNode.Position})";

                var nameOverride = Validation.ValidateValue(pathNode.NameOverride, null, Settings, Logger, logDescription);
                Stream.WriteLengthPrefixedString(nameOverride);

                if (pathNode.Properties.TryGetValue(Attributes.Message, out var fireOnTarget))
                    fireOnTarget = Validation.ValidateValue(fireOnTarget, null, Settings, Logger, logDescription);
                Stream.WriteLengthPrefixedString(fireOnTarget);

                WriteVector3D(pathNode.Position);
                WriteAngles(pathNode.Properties.GetAngles(Attributes.Angles) ?? new Angles());

                Stream.WriteInt((int)(pathNode.IsSelected ? JmfMapObjectFlags.Selected : JmfMapObjectFlags.None));
                WriteColor(pathNode.Color);

                Stream.WriteInt(pathNode.Properties.Count);
                foreach (var property in pathNode.Properties)
                {
                    var key = Validation.ValidateKey(property.Key ?? "", null, Settings, Logger, logDescription);
                    Stream.WriteLengthPrefixedString(key);

                    var value = Validation.ValidateValue(property.Value ?? "", null, Settings, Logger, logDescription);
                    Stream.WriteLengthPrefixedString(value);
                }
            }

            private void WriteEntity(Entity entity)
            {
                var jmfEntity = entity as JmfEntity;

                Stream.WriteLengthPrefixedString(entity.ClassName);
                WriteVector3D(jmfEntity?.JmfOrigin ?? entity.Origin);
                Stream.WriteInt((int)(jmfEntity?.JmfFlags ?? GetJmfMapObjectFlags(entity)));
                Stream.WriteInt(entity.Group?.ID ?? 0);
                Stream.WriteInt(entity.Group == null ? 0 : GetRootGroupID(entity.Group));
                WriteColor(_hasColorInformation ? entity.Color : new Color(220, 30, 220));  // NOTE: JACK appears to ignore this - it'll use .fgd color information for entities instead.

                foreach (var attributeName in jmfEntity?.SpecialAttributeNames.ToArray() ?? SerializedAttributeNames)
                    Stream.WriteLengthPrefixedString(attributeName);

                Stream.WriteInt(jmfEntity?.JmfSpawnflags ?? entity.Spawnflags);
                WriteAngles(jmfEntity?.JmfAngles ?? entity.Angles ?? new Angles());
                Stream.WriteInt((int)(jmfEntity?.JmfRendering ?? GetJmfRenderMode(entity)));

                var renderColor = GetColorProperty(Attributes.RenderColor, new Color(255, 255, 255));
                renderColor.A = (byte)Math.Clamp(GetIntProperty(Attributes.RenderAmount, 255), 0, 255);
                WriteColor(jmfEntity?.JmfFxColor ?? renderColor);

                Stream.WriteInt(jmfEntity?.JmfRenderMode ?? GetIntProperty(Attributes.Rendermode, 0));
                Stream.WriteInt(jmfEntity?.JmfRenderFX ?? GetIntProperty(Attributes.RenderFX, 0));
                Stream.WriteShort((short)(jmfEntity?.JmfBody ?? GetIntProperty(Attributes.Body, 0)));
                Stream.WriteShort((short)(jmfEntity?.JmfSkin ?? GetIntProperty(Attributes.Skin, 0)));
                Stream.WriteInt(jmfEntity?.JmfSequence ?? GetIntProperty(Attributes.Sequence, 0));
                Stream.WriteFloat(jmfEntity?.JmfFramerate ?? GetFloatProperty(Attributes.Framerate, 10));
                Stream.WriteFloat(jmfEntity?.JmfScale ?? GetFloatProperty(Attributes.Scale, 1));
                Stream.WriteFloat(jmfEntity?.JmfRadius ?? GetFloatProperty(Attributes.Radius, 0));
                Stream.WriteBytes(GetFixedLengthByteArray(jmfEntity?.UnknownData, 28));

                var serializedProperties = entity.Properties
                    .Where(kv => kv.Key != Attributes.Classname && kv.Key != Attributes.Origin)
                    .Cast<KeyValuePair<string?, string?>>()
                    .ToArray();
                if (jmfEntity != null)
                    serializedProperties = jmfEntity.JmfProperties.ToArray();

                var logDescription = $"Entity of type '{entity.ClassName}'";

                Stream.WriteInt(serializedProperties.Length);
                foreach (var property in serializedProperties)
                {
                    var key = Validation.ValidateKey(property.Key ?? "", null, Settings, Logger, logDescription);
                    Stream.WriteLengthPrefixedString(key);

                    var value = Validation.ValidateValue(property.Value ?? "", null, Settings, Logger, logDescription);
                    Stream.WriteLengthPrefixedString(value);
                }

                var visGroupIDs = GetVisGroupIDs(entity);
                Stream.WriteInt(visGroupIDs.Length);
                foreach (var visGroupID in visGroupIDs)
                    Stream.WriteInt(visGroupID);

                Stream.WriteInt(entity.Brushes.Count);
                foreach (var brush in entity.Brushes)
                    WriteBrush(brush);


                Color GetColorProperty(string name, Color defaultColor)
                {
                    if (entity.Properties.TryGetValue(name, out var rawColor))
                    {
                        var parts = rawColor.Split();
                        if (parts.Length >= 3 && byte.TryParse(parts[0], out var r) && byte.TryParse(parts[1], out var g) && byte.TryParse(parts[2], out var b))
                            return new Color(r, g, b);
                    }

                    return defaultColor;
                }

                int GetIntProperty(string name, int defaultValue)
                    => entity.Properties.TryGetValue(name, out var rawValue) && int.TryParse(rawValue, out var value) ? value : defaultValue;

                float GetFloatProperty(string name, float defaultValue)
                    => entity.Properties.TryGetValue(name, out var rawValue) && float.TryParse(rawValue, out var value) ? value : defaultValue;
            }

            private void WriteBrush(Brush brush)
            {
                var jmfBrush = brush as JmfBrush;
                var patch = jmfBrush?.Patch;

                Stream.WriteInt(patch != null ? 1 : 0);
                Stream.WriteInt((int)(jmfBrush?.JmfFlags ?? GetJmfMapObjectFlags(brush)));
                Stream.WriteInt(brush.Group?.ID ?? 0);
                Stream.WriteInt(brush.Group == null ? 0 : GetRootGroupID(brush.Group));
                WriteColor(_hasColorInformation ? brush.Color : GenerateRandomColor());

                var visGroupIDs = GetVisGroupIDs(brush);
                Stream.WriteInt(visGroupIDs.Length);
                foreach (var visGroupID in visGroupIDs)
                    Stream.WriteInt(visGroupID);

                Stream.WriteInt(brush.Faces.Count);
                foreach (var face in brush.Faces)
                    WriteFace(face);

                if (patch != null)
                    WritePatch(patch);
            }

            private void WriteFace(Face face)
            {
                var jmfFace = face as JmfFace;

                Stream.WriteInt(jmfFace?.RenderFlags ?? 0);
                Stream.WriteInt(face.Vertices.Count);

                WriteVector3D(face.TextureRightAxis);
                Stream.WriteFloat(face.TextureShift.X);
                WriteVector3D(face.TextureDownAxis);
                Stream.WriteFloat(face.TextureShift.Y);
                Stream.WriteFloat(face.TextureScale.X);
                Stream.WriteFloat(face.TextureScale.Y);
                Stream.WriteFloat(face.TextureAngle);

                Stream.WriteInt((int)(jmfFace?.TextureAlignment ?? GetTextureAlignment()));
                Stream.Write(GetFixedLengthByteArray(jmfFace?.UnknownData, 12));
                Stream.WriteInt((int)(jmfFace?.Contents ?? JmfSurfaceContents.None));
                WriteTextureName(face.TextureName, 64);

                WriteVector3D(face.Plane.Normal);
                Stream.WriteFloat(face.Plane.Distance);
                Stream.WriteInt((int)(jmfFace?.AxisAlignment ?? GetAxisAlignment()));

                // Vertices are stored in counter-clockwise order:
                for (int i = face.Vertices.Count - 1; i >= 0; i--)
                {
                    WriteVector3D(face.Vertices[i]);
                    if (jmfFace != null)
                    {
                        Stream.WriteFloat(jmfFace.VertexUVCoordinates[i].X);
                        Stream.WriteFloat(jmfFace.VertexUVCoordinates[i].Y);
                        Stream.WriteInt((int)jmfFace.VertexSelectionState[i]);
                    }
                    else
                    {
                        Stream.WriteFloat(0f);
                        Stream.WriteFloat(0f);
                        Stream.WriteInt(0);
                    }
                }


                JmfTextureAlignment GetTextureAlignment()
                {
                    var textureAlignment = JmfTextureAlignment.None;

                    // TODO: Use an epsilon here!
                    var textureNormal = face.TextureDownAxis.CrossProduct(face.TextureRightAxis);
                    if (textureNormal == face.Plane.Normal)
                        textureAlignment |= JmfTextureAlignment.Face;

                    // TODO: Check whether the face normal is also closest to the projection axis!
                    if (face.TextureRightAxis == new Vector3D(0, 1, 0) && face.TextureDownAxis == new Vector3D(0, 0, -1) ||   // Projected along X axis
                        face.TextureRightAxis == new Vector3D(1, 0, 0) && face.TextureDownAxis == new Vector3D(0, 0, -1) ||   // Projected along Y axis
                        face.TextureRightAxis == new Vector3D(1, 0, 0) && face.TextureDownAxis == new Vector3D(0, -1, 0))     // Projected alogn Z axis
                        textureAlignment |= JmfTextureAlignment.World;

                    return textureAlignment;
                }

                JmfAxisAlignment GetAxisAlignment()
                {
                    var isZeroX = face.Plane.Normal.X == 0;
                    var isZeroY = face.Plane.Normal.Y == 0;
                    var isZeroZ = face.Plane.Normal.Z == 0;

                    if (!isZeroX && isZeroY && isZeroZ)
                        return JmfAxisAlignment.XAligned;
                    else if (isZeroX && !isZeroY && isZeroZ)
                        return JmfAxisAlignment.YAligned;
                    else if (isZeroX && isZeroY && !isZeroZ)
                        return JmfAxisAlignment.ZAligned;
                    else
                        return JmfAxisAlignment.Unaligned;
                }
            }

            private void WritePatch(JmfPatch patch)
            {
                Stream.WriteInt(patch.Columns);
                Stream.WriteInt(patch.Rows);

                WriteVector3D(patch.TextureRightAxis);
                Stream.WriteFloat(patch.TextureShift.X);
                WriteVector3D(patch.TextureDownAxis);
                Stream.WriteFloat(patch.TextureShift.Y);
                Stream.WriteFloat(patch.TextureScale.X);
                Stream.WriteFloat(patch.TextureScale.Y);
                Stream.WriteFloat(patch.TextureAngle);
                Stream.WriteInt((int)patch.TextureAlignment);
                Stream.WriteBytes(GetFixedLengthByteArray(patch.UnknownData, 12));

                Stream.WriteInt((int)patch.Contents);
                WriteTextureName(patch.TextureName, 64);
                Stream.WriteBytes(GetFixedLengthByteArray(patch.UnknownData2, 4));

                var emptyControlPoint = new JmfPatchControlPoint();
                for (int column = 0; column < 32; column++)
                {
                    for (int row = 0; row < 32; row++)
                    {
                        var controlPoint = column < patch.Columns && row < patch.Rows ? patch.ControlPoints[column, row] : emptyControlPoint;
                        WritePatchControlPoint(controlPoint);
                    }
                }
            }

            private void WritePatchControlPoint(JmfPatchControlPoint controlPoint)
            {
                WriteVector3D(controlPoint.Position);
                WriteVector3D(controlPoint.Normal);
                Stream.WriteFloat(controlPoint.UV.X);
                Stream.WriteFloat(controlPoint.UV.Y);
                Stream.WriteInt(controlPoint.IsSelected ? 1 : 0);
            }

            private void WriteTextureName(string textureName, int maxLength)
            {
                textureName = Validation.ValidateTextureName(textureName, maxLength, Settings, Logger, Encoding.ASCII);
                Stream.WriteFixedLengthString(textureName, maxLength);
            }


            private void WriteVector3D(Vector3D vector)
            {
                Stream.WriteFloat(vector.X);
                Stream.WriteFloat(vector.Y);
                Stream.WriteFloat(vector.Z);
            }

            private void WriteColor(Color color)
            {
                var rgba = new byte[] { color.R, color.G, color.B, color.A };
                Stream.WriteBytes(rgba);
            }

            private void WriteAngles(Angles angles)
            {
                Stream.WriteFloat(angles.Pitch);
                Stream.WriteFloat(angles.Yaw);
                Stream.WriteFloat(angles.Roll);
            }


            private int[] GetVisGroupIDs(MapObject mapObject)
            {
                switch (_visGroupAssignment)
                {
                    case VisGroupAssignment.PerGroup:
                        return (mapObject.TopLevelGroup ?? mapObject).VisGroups
                            .Select(visGroup => _visGroupIDMapping[visGroup.ID])
                            .ToArray();

                    case VisGroupAssignment.PerObject:
                        return mapObject.VisGroups
                            .Select(visGroup => _visGroupIDMapping[visGroup.ID])
                            .ToArray();

                    default:
                        throw new NotImplementedException($"Unknown VIS group assignment approach: {_visGroupAssignment}.");
                }
            }

            private int GetRootGroupID(Group group) => group.Group == null ? group.ID : GetRootGroupID(group.Group);

            private JmfMapObjectFlags GetJmfMapObjectFlags(Group group)
            {
                var flags = JmfMapObjectFlags.None;

                if (group.IsSelected)
                    flags |= JmfMapObjectFlags.Selected;

                if ((_visGroupAssignment == VisGroupAssignment.PerGroup && group.VisGroups.Any(visGroup => !visGroup.IsVisible)) ||
                    (_visGroupAssignment == VisGroupAssignment.PerObject && group.IsHidden))
                {
                    flags |= JmfMapObjectFlags.Hidden;
                }

                return flags;
            }

            private JmfMapObjectFlags GetJmfMapObjectFlags(Entity entity)
            {
                var flags = JmfMapObjectFlags.None;

                if (!entity.Brushes.Any())
                    flags |= JmfMapObjectFlags.PointBased;

                if (entity.IsSelected)
                    flags |= JmfMapObjectFlags.Selected;

                if ((_visGroupAssignment == VisGroupAssignment.PerGroup && entity.VisGroups.Any(visGroup => !visGroup.IsVisible)) ||
                    (_visGroupAssignment == VisGroupAssignment.PerObject && entity.IsHidden))
                {
                    flags |= JmfMapObjectFlags.Hidden;
                }

                if (entity.Properties.TryGetValue(Attributes.Rendermode, out var rawRenderMode) && int.TryParse(rawRenderMode, out var renderMode) && renderMode != 0)
                    flags |= JmfMapObjectFlags.RenderMode;

                if (entity.ClassName == Entities.Worldspawn)
                    flags |= JmfMapObjectFlags.IsWorld;

                if (entity.ClassName.StartsWith("weapon") || entity.ClassName.StartsWith("item_"))
                    flags |= JmfMapObjectFlags.WeaponOrItem;

                if (entity.ClassName.StartsWith("path_"))
                    flags |= JmfMapObjectFlags.PathEntity;

                // TODO: Unknown1 flag!

                return flags;
            }

            private JmfMapObjectFlags GetJmfMapObjectFlags(Brush brush)
            {
                var flags = JmfMapObjectFlags.None;

                if (brush.IsSelected)
                    flags |= JmfMapObjectFlags.Selected;

                if ((_visGroupAssignment == VisGroupAssignment.PerGroup && brush.VisGroups.Any(visGroup => !visGroup.IsVisible)) ||
                    (_visGroupAssignment == VisGroupAssignment.PerObject && brush.IsHidden))
                {
                    flags |= JmfMapObjectFlags.Hidden;
                }

                return flags;
            }

            private JmfRenderMode GetJmfRenderMode(Entity entity)
            {
                if (!entity.Properties.TryGetValue(Attributes.Rendermode, out var rawRenderMode) || !int.TryParse(rawRenderMode, out var renderMode))
                    renderMode = 0;

                switch (renderMode)
                {
                    case 0:     // Normal
                    default:
                        return JmfRenderMode.Normal;

                    case 1:     // Color
                    case 2:     // Texture
                        return JmfRenderMode.Translucent;

                    case 3:     // Glow
                    case 5:     // Additive
                        return JmfRenderMode.Glow;

                    case 4:     // Solid
                        return JmfRenderMode.TransparentColorKey;
                }
            }

            private byte[] GetFixedLengthByteArray(byte[]? data, int length)
            {
                if (data == null)
                    return new byte[length];
                else if (data.Length == length)
                    return data;

                var buffer = new byte[length];
                Array.Copy(data, buffer, Math.Min(data.Length, length));
                return buffer;
            }

            private Color GenerateRandomColor()
            {
                var red = (byte)0;
                var green = (byte)_random.Next(100, 256);
                var blue = (byte)_random.Next(100, 256);
                return new Color(red, green, blue);
            }
        }
    }


    public enum JmfRenderMode : int
    {
        Glow =                  0x00000025, // HL render modes 3 (glow) and 5 (additive)
        Normal =                0x00000100, // HL render mode 0 (normal)
        Translucent =           0x00040065, // HL render modes 1 (color) and 2 (texture)
        TransparentColorKey =   0x00400165, // HL render mode 4 (solid)
    }

    [Flags]
    public enum JmfMapObjectFlags
    {
        None =          0x0000,
        PointBased =    0x0001,     // Or maybe 'HasOrigin'?
        Selected =      0x0002,

        Hidden =        0x0008,
        RenderMode =    0x0010,     // Any other render mode than 'normal'
        IsWorld =       0x0020,     // The special "worldspawn" entity
        WeaponOrItem =  0x0040,     // Any entity whose name starts with "weapon" or "item_"
        PathEntity =    0x0080,     // Any entity whose name starts with "path_"

        Unknown1 =      0x8000,     // TODO: This appears to be related to models, and may have been introduced in a newer JACK version?
    }

    public enum JmfFileVersion
    {
        V121 = 121,

        /// <summary>
        /// Version 122 adds support for 2D view background images.
        /// </summary>
        V122 = 122,
    }
}
