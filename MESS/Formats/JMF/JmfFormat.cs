using MESS.Common;
using MESS.Mapping;
using MESS.Mathematics.Spatial;

namespace MESS.Formats.JMF
{
    /// <summary>
    /// JMF is J.A.C.K.'s map format. Similar to the RMF format,
    /// it also supports logical and visual groups, camera's and paths.
    /// Unlike RMF, JMF supports objects being part of multiple visual groups.
    /// </summary>
    public class JmfFormat
    {
        private static string[] SerializedAttributeNames { get; } = new[] { "spawnflags", "origin", "angles", "scale", "targetname", "target", "skyname", "model", "model", "texture", "model", "model", "script" };


        public static Map Load(Stream stream)
        {
            var map = new JmfMap();

            var jhmfFileSignature = stream.ReadFixedLengthString(4);
            if (jhmfFileSignature != "JHMF")
                throw new InvalidDataException($"Expected 'JHMF' magic string, but found '{jhmfFileSignature}'.");

            var fileVersion = stream.ReadInt();
            if (fileVersion < 121 || fileVersion > 122)
                throw new NotSupportedException($"Only JMF file versions 121 and 122 are supported.");

            // Recent export paths:
            var exportPathCount = stream.ReadInt();
            for (int i = 0; i < exportPathCount; i++)
                map.RecentExportPaths.Add(stream.ReadLengthPrefixedString() ?? "");

            if (fileVersion >= 122)
            {
                // 2D view background images:
                map.FrontViewBackgroundImage = ReadBackgroundImageSettings(stream);
                map.SideViewBackgroundImage = ReadBackgroundImageSettings(stream);
                map.TopViewBackgroundImage = ReadBackgroundImageSettings(stream);
            }

            // Groups can be nested:
            var groupCount = stream.ReadInt();
            var parentGroupIDs = new Dictionary<int, int>();
            for (int i = 0; i < groupCount; i++)
            {
                (var group, var parentGroupID) = ReadGroup(stream);
                if (parentGroupID != 0)
                    parentGroupIDs[group.ID] = parentGroupID;

                map.Groups.Add(group);
            }
            var groups = map.Groups.ToDictionary(group => group.ID, group => group);
            foreach (var kv in parentGroupIDs)
                groups[kv.Value].AddObject(groups[kv.Key]);

            // Objects can be part of multiple VIS groups:
            var visGroupCount = stream.ReadInt();
            for (int i = 0; i < visGroupCount; i++)
                map.VisGroups.Add(ReadVisGroup(stream));
            var visGroups = map.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

            var cordonMin = ReadVector3D(stream);
            var cordonMax = ReadVector3D(stream);
            map.CordonArea = new BoundingBox(cordonMin, cordonMax);

            var cameraCount = stream.ReadInt();
            for (int i = 0; i < cameraCount; i++)
            {
                (var camera, var isSelected) = ReadCamera(stream);
                map.Cameras.Add(camera);

                if (isSelected)
                    map.ActiveCameraIndex = i;
            }

            var pathCount = stream.ReadInt();
            for (int i = 0; i < pathCount; i++)
                map.EntityPaths.Add(ReadEntityPath(stream));

            try
            {
                while (true)
                {
                    var entity = ReadEntity(stream, groups, visGroups);
                    if (entity.ClassName == Entities.Worldspawn)
                        map.Worldspawn = entity;
                    else
                        map.Entities.Add(entity);
                }
            }
            catch (EndOfStreamException)
            {
                // End of file, no more entities.
            }

            return map;
        }


        public static void Save(Map map, string path, JmfFileSaveSettings? settings = null)
        {
            using (var file = File.Create(path))
                Save(map, file, settings);
        }

        public static void Save(Map map, Stream stream, JmfFileSaveSettings? settings = null)
        {
            if (settings == null)
                settings = new JmfFileSaveSettings();


            stream.WriteFixedLengthString("JHMF");
            stream.WriteInt((int)settings.FileVersion);

            // Export paths:
            var jmfMap = map as JmfMap;
            var recentExportPaths = jmfMap?.RecentExportPaths ?? new List<string>();
            stream.WriteInt(recentExportPaths.Count);
            foreach (var exportPath in recentExportPaths)
                stream.WriteLengthPrefixedString(exportPath);

            if (settings.FileVersion >= JmfFileVersion.V122)
            {
                // 2D view background images:
                WriteBackgroundImageSettings(stream, jmfMap?.FrontViewBackgroundImage);
                WriteBackgroundImageSettings(stream, jmfMap?.SideViewBackgroundImage);
                WriteBackgroundImageSettings(stream, jmfMap?.TopViewBackgroundImage);
            }

            // Groups:
            stream.WriteInt(map.Groups.Count);
            foreach (var group in map.Groups)
                WriteGroup(stream, group);

            // VIS groups:
            stream.WriteInt(map.VisGroups.Count);
            foreach (var visGroup in map.VisGroups)
                WriteVisGroup(stream, visGroup);

            // Cordon:
            WriteVector3D(stream, map.CordonArea?.Min ?? new Vector3D());
            WriteVector3D(stream, map.CordonArea?.Max ?? new Vector3D());

            // Cameras:
            stream.WriteInt(map.Cameras.Count);
            for (int i = 0; i < map.Cameras.Count; i++)
                WriteCamera(stream, map.Cameras[i], map.ActiveCameraIndex == i);

            // Paths:
            stream.WriteInt(map.EntityPaths.Count);
            foreach (var entityPath in map.EntityPaths)
                WriteEntityPath(stream, entityPath);

            // Entities (and their brushes):
            WriteEntity(stream, map.Worldspawn);
            foreach (var entity in map.Entities)
                WriteEntity(stream, entity);
        }


        private static JmfBackgroundImageSettings ReadBackgroundImageSettings(Stream stream)
        {
            var settings = new JmfBackgroundImageSettings();

            settings.ImagePath = stream.ReadLengthPrefixedString() ?? "";
            settings.Scale = stream.ReadDouble();
            settings.Luminance = stream.ReadInt();
            settings.Filtering = (ImageFiltering)stream.ReadInt();
            settings.InvertColors = stream.ReadInt() == 1;
            settings.OffsetX = stream.ReadInt();
            settings.OffsetY = stream.ReadInt();
            settings.UnknownData = stream.ReadBytes(4);

            return settings;
        }

        private static (Group group, int parentGroupID) ReadGroup(Stream stream)
        {
            var group = new Group();
            group.ID = stream.ReadInt();
            var parentGroupID = stream.ReadInt();

            var flags = (JmfMapObjectFlags)stream.ReadInt();
            group.IsSelected = flags.HasFlag(JmfMapObjectFlags.Selected);
            group.IsHidden = flags.HasFlag(JmfMapObjectFlags.Hidden);

            var count = stream.ReadInt();
            group.Color = ReadColor(stream);

            return (group, parentGroupID);
        }

        private static VisGroup ReadVisGroup(Stream stream)
        {
            var visGroup = new VisGroup();
            visGroup.Name = stream.ReadLengthPrefixedString();
            visGroup.ID = stream.ReadInt();
            visGroup.Color = ReadColor(stream);
            visGroup.IsVisible = stream.ReadSingleByte() == 1;
            return visGroup;
        }

        private static (Camera camera, bool isSelected) ReadCamera(Stream stream)
        {
            var camera = new Camera();
            camera.EyePosition = ReadVector3D(stream);
            camera.LookAtPosition = ReadVector3D(stream);
            var isSelected = ((JmfMapObjectFlags)stream.ReadInt()).HasFlag(JmfMapObjectFlags.Selected);
            camera.Color = ReadColor(stream);
            return (camera, isSelected);
        }

        private static EntityPath ReadEntityPath(Stream stream)
        {
            var path = new JmfEntityPath();
            path.ClassName = stream.ReadLengthPrefixedString() ?? "";
            path.Name = stream.ReadLengthPrefixedString() ?? "";
            path.Type = (PathType)stream.ReadInt();
            path.UnknownData = stream.ReadBytes(4);
            path.Color = ReadColor(stream);

            var nodeCount = stream.ReadInt();
            for (int i = 0; i < nodeCount; i++)
                path.Nodes.Add(ReadPathNode(stream));

            return path;
        }

        private static EntityPathNode ReadPathNode(Stream stream)
        {
            var node = new EntityPathNode();
            node.NameOverride = stream.ReadLengthPrefixedString();

            var fireOnTarget = stream.ReadLengthPrefixedString();
            if (!string.IsNullOrEmpty(fireOnTarget))
                node.Properties[Attributes.Message] = fireOnTarget;

            node.Position = ReadVector3D(stream);

            var angles = ReadAngles(stream);
            if (angles.Pitch != 0 || angles.Yaw != 0 || angles.Roll != 0)
                node.Properties[Attributes.Angles] = FormattableString.Invariant($"{angles.Pitch} {angles.Yaw} {angles.Roll}");

            var flags = (JmfMapObjectFlags)stream.ReadInt();
            node.IsSelected = flags.HasFlag(JmfMapObjectFlags.Selected);    // NOTE: Path nodes cannot be hidden in JACK, but this behavior may change. TEST THIS BY GENERATING A JMF!!!
            node.Color = ReadColor(stream);

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
            {
                var key = stream.ReadLengthPrefixedString();
                var value = stream.ReadLengthPrefixedString();
                node.Properties[key ?? ""] = value ?? "";
            }

            return node;
        }

        private static Entity ReadEntity(Stream stream, IDictionary<int, Group> groups, IDictionary<int, VisGroup> visGroups)
        {
            var entity = new JmfEntity();

            entity.ClassName = stream.ReadLengthPrefixedString() ?? "";
            entity.JmfOrigin = ReadVector3D(stream);

            entity.JmfFlags = (JmfMapObjectFlags)stream.ReadInt();
            entity.IsSelected = entity.JmfFlags.HasFlag(JmfMapObjectFlags.Selected);
            entity.IsHidden = entity.JmfFlags.HasFlag(JmfMapObjectFlags.Hidden);

            var groupID = stream.ReadInt();
            if (groupID != 0 && groups.TryGetValue(groupID, out var group) && group != null)
                group.AddObject(entity);

            var rootGroupID = stream.ReadInt();
            entity.Color = ReadColor(stream);

            // NOTE: Each entity contains a list of 'special' attribute names, which are always the same and don't seem to serve any purpose:
            for (int i = 0; i < 13; i++)
                entity.SpecialAttributeNames.Add(stream.ReadLengthPrefixedString());

            // This is internal editor state that can mostly be derived from entity properties,
            // but sometimes different properties are used depending on the type of entity,
            // and some data depends on fgd settings that are not replicated in the entity properties.
            // Still, for normal operations, this can be ignored because only the data in entity.Properties
            // is exported to a .map file:
            entity.JmfSpawnflags = stream.ReadInt();
            entity.JmfAngles = ReadAngles(stream);
            entity.JmfRendering = (JmfRenderMode)stream.ReadInt();

            entity.JmfFxColor = ReadColor(stream);
            entity.JmfRenderMode = stream.ReadInt();
            entity.JmfRenderFX = stream.ReadInt();
            entity.JmfBody = stream.ReadShort();
            entity.JmfSkin = stream.ReadShort();
            entity.JmfSequence = stream.ReadInt();
            entity.JmfFramerate = stream.ReadFloat();
            entity.JmfScale = stream.ReadFloat();
            entity.JmfRadius = stream.ReadFloat();
            entity.UnknownData = stream.ReadBytes(28);

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
            {
                var key = stream.ReadLengthPrefixedString();
                var value = stream.ReadLengthPrefixedString();
                entity.JmfProperties.Add(new KeyValuePair<string?, string?>(key, value));
                entity.Properties[key ?? ""] = value ?? "";
                //entity.Properties[stream.ReadLengthPrefixedString() ?? ""] = stream.ReadLengthPrefixedString() ?? "";
            }

            var visGroupCount = stream.ReadInt();
            for (int i = 0; i < visGroupCount; i++)
            {
                var visGroupID = stream.ReadInt();
                if (visGroups.TryGetValue(visGroupID, out var visGroup))
                    visGroup.AddObject(entity);
            }

            var brushCount = stream.ReadInt();
            for (int i = 0; i < brushCount; i++)
                entity.AddBrush(ReadBrush(stream, groups, visGroups));

            if (entity.IsPointBased)
                entity.Origin = entity.JmfOrigin;

            return entity;
        }

        private static Brush ReadBrush(Stream stream, IDictionary<int, Group> groups, IDictionary<int, VisGroup> visGroups)
        {
            var isPatch = stream.ReadInt() == 1;
            var flags = (JmfMapObjectFlags)stream.ReadInt();
            var groupID = stream.ReadInt();
            var rootGroupID = stream.ReadInt();
            var color = ReadColor(stream);

            var visGroupCount = stream.ReadInt();
            var visGroupIDs = Enumerable.Range(0, visGroupCount)
                .Select(i => stream.ReadInt())
                .ToArray();

            var faceCount = stream.ReadInt();
            var faces = Enumerable.Range(0, faceCount)
                .Select(i => ReadFace(stream))
                .ToArray();

            var brush = new JmfBrush(faces);
            brush.Color = color;
            if (isPatch)
                brush.Patch = ReadPatch(stream);


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

        private static Face ReadFace(Stream stream)
        {
            var face = new JmfFace();

            face.RenderFlags = stream.ReadInt();
            var vertexCount = stream.ReadInt();
            face.TextureRightAxis = ReadVector3D(stream);
            var textureShiftX = stream.ReadFloat();
            face.TextureDownAxis = ReadVector3D(stream);
            var textureShiftY = stream.ReadFloat();
            face.TextureShift = new Vector2D(textureShiftX, textureShiftY);
            face.TextureScale = new Vector2D(stream.ReadFloat(), stream.ReadFloat());
            face.TextureAngle = stream.ReadFloat();
            face.TextureAlignment = (JmfTextureAlignment)stream.ReadInt();
            face.UnknownData = stream.ReadBytes(12);
            face.Contents = (JmfSurfaceContents)stream.ReadInt();
            face.TextureName = stream.ReadFixedLengthString(64);

            var planeNormal = ReadVector3D(stream);
            var planeDistance = stream.ReadFloat();
            face.Plane = new Plane(planeNormal, planeDistance);
            face.AxisAlignment = (JmfAxisAlignment)stream.ReadInt();

            for (int i = 0; i < vertexCount; i++)
            {
                face.Vertices.Add(ReadVector3D(stream));
                face.VertexUVCoordinates.Add(new Vector2D(stream.ReadFloat(), stream.ReadFloat()));
                face.VertexSelectionState.Add((JmfVertexSelection)stream.ReadInt());
            }

            face.PlanePoints = face.Vertices.Take(3).Reverse().ToArray();

            return face;
        }

        private static JmfPatch ReadPatch(Stream stream)
        {
            var columns = stream.ReadInt();
            var rows = stream.ReadInt();

            var patch = new JmfPatch(columns, rows);

            patch.TextureRightAxis = ReadVector3D(stream);
            var textureShiftX = stream.ReadFloat();
            patch.TextureDownAxis = ReadVector3D(stream);
            var textureShiftY = stream.ReadFloat();
            patch.TextureShift = new Vector2D(textureShiftX, textureShiftY);
            patch.TextureScale = new Vector2D(stream.ReadFloat(), stream.ReadFloat());
            patch.TextureAngle = stream.ReadFloat();
            patch.TextureAlignment = (JmfTextureAlignment)stream.ReadInt();
            patch.UnknownData = stream.ReadBytes(12);

            patch.Contents = (JmfSurfaceContents)stream.ReadInt();
            patch.TextureName = stream.ReadFixedLengthString(64);
            patch.UnknownData2 = stream.ReadBytes(4);


            for (int column = 0; column < 32; column++)
            {
                for (int row = 0; row < 32; row++)
                {
                    var controlPoint = ReadPatchControlPoint(stream);
                    if (column < columns && row < rows)
                        patch.ControlPoints[column, row] = controlPoint;
                }
            }

            return patch;
        }

        private static JmfPatchControlPoint ReadPatchControlPoint(Stream stream)
        {
            var controlPoint = new JmfPatchControlPoint();
            controlPoint.Position = ReadVector3D(stream);
            controlPoint.Normal = ReadVector3D(stream);
            controlPoint.UV = new Vector2D(stream.ReadFloat(), stream.ReadFloat());
            controlPoint.IsSelected = stream.ReadInt() == 1;
            return controlPoint;
        }


        private static void WriteBackgroundImageSettings(Stream stream, JmfBackgroundImageSettings? settings)
        {
            stream.WriteLengthPrefixedString(settings?.ImagePath ?? "");
            stream.WriteDouble(settings?.Scale ?? 1.0);
            stream.WriteInt(settings?.Luminance ?? 255);
            stream.WriteInt((int)(settings?.Filtering ?? ImageFiltering.Linear));
            stream.WriteInt(settings?.InvertColors == true ? 1 : 0);
            stream.WriteInt(settings?.OffsetX ?? 0);
            stream.WriteInt(settings?.OffsetY ?? 0);
            stream.WriteBytes(GetFixedLengthByteArray(settings?.UnknownData, 4));
        }

        private static void WriteGroup(Stream stream, Group group)
        {
            stream.WriteInt(group.ID);
            stream.WriteInt(group.Group?.ID ?? 0);
            stream.WriteInt((int)GetJmfMapObjectFlags(group));
            stream.WriteInt(group.Objects.Count);
            WriteColor(stream, group.Color);
        }

        private static void WriteVisGroup(Stream stream, VisGroup visGroup)
        {
            stream.WriteLengthPrefixedString(visGroup.Name);
            stream.WriteInt(visGroup.ID);
            WriteColor(stream, visGroup.Color);
            stream.WriteByte((byte)(visGroup.IsVisible ? 1 : 0));
        }

        private static void WriteCamera(Stream stream, Camera camera, bool isSelected)
        {
            WriteVector3D(stream, camera.EyePosition);
            WriteVector3D(stream, camera.LookAtPosition);

            stream.WriteInt((int)(isSelected ? JmfMapObjectFlags.Selected : JmfMapObjectFlags.None));
            WriteColor(stream, camera.Color);
        }

        private static void WriteEntityPath(Stream stream, EntityPath entityPath)
        {
            var jmfEntityPath = entityPath as JmfEntityPath;

            stream.WriteLengthPrefixedString(entityPath.ClassName);
            stream.WriteLengthPrefixedString(entityPath.Name);
            stream.WriteInt((int)entityPath.Type);

            stream.WriteBytes(GetFixedLengthByteArray(jmfEntityPath?.UnknownData, 4));
            WriteColor(stream, entityPath.Color);

            stream.WriteInt(entityPath.Nodes.Count);
            foreach (var pathNode in entityPath.Nodes)
                WritePathNode(stream, pathNode);
        }

        private static void WritePathNode(Stream stream, EntityPathNode pathNode)
        {
            stream.WriteLengthPrefixedString(pathNode.NameOverride);
            stream.WriteLengthPrefixedString(pathNode.Properties.TryGetValue(Attributes.Message, out var fireOnTarget) ? fireOnTarget : null);

            WriteVector3D(stream, pathNode.Position);
            WriteAngles(stream, pathNode.Properties.GetAngles(Attributes.Angles) ?? new Angles());

            stream.WriteInt((int)(pathNode.IsSelected ? JmfMapObjectFlags.Selected : JmfMapObjectFlags.None));
            WriteColor(stream, pathNode.Color);

            stream.WriteInt(pathNode.Properties.Count);
            foreach (var property in pathNode.Properties)
            {
                stream.WriteLengthPrefixedString(property.Key);
                stream.WriteLengthPrefixedString(property.Value);
            }
        }

        private static void WriteEntity(Stream stream, Entity entity)
        {
            var jmfEntity = entity as JmfEntity;

            stream.WriteLengthPrefixedString(entity.ClassName);
            WriteVector3D(stream, jmfEntity?.JmfOrigin ?? entity.Origin);
            stream.WriteInt((int)(jmfEntity?.JmfFlags ?? GetJmfMapObjectFlags(entity)));
            stream.WriteInt(entity.Group?.ID ?? 0);
            stream.WriteInt(entity.Group == null ? 0 : GetRootGroupID(entity.Group));
            WriteColor(stream, entity.Color);

            // NOTE: I'm not sure what the purpose is of saving these attribute names, but it's consistent with what J.A.C.K. does...
            foreach (var attributeName in jmfEntity?.SpecialAttributeNames.ToArray() ?? SerializedAttributeNames)
                stream.WriteLengthPrefixedString(attributeName);

            stream.WriteInt(jmfEntity?.JmfSpawnflags ?? entity.Spawnflags);
            WriteAngles(stream, jmfEntity?.JmfAngles ?? entity.Angles ?? new Angles());
            stream.WriteInt((int)(jmfEntity?.JmfRendering ?? GetJmfRenderMode(entity)));

            var renderColor = GetColorProperty(Attributes.RenderColor, new Color(255, 255, 255));
            renderColor.A = (byte)Math.Clamp(GetIntProperty(Attributes.RenderAmount, 255), 0, 255);
            WriteColor(stream, jmfEntity?.JmfFxColor ?? renderColor);

            stream.WriteInt(jmfEntity?.JmfRenderMode ?? GetIntProperty(Attributes.Rendermode, 0));
            stream.WriteInt(jmfEntity?.JmfRenderFX ?? GetIntProperty(Attributes.RenderFX, 0));
            stream.WriteShort((short)(jmfEntity?.JmfBody ?? GetIntProperty(Attributes.Body, 0)));
            stream.WriteShort((short)(jmfEntity?.JmfSkin ?? GetIntProperty(Attributes.Skin, 0)));
            stream.WriteInt(jmfEntity?.JmfSequence ?? GetIntProperty(Attributes.Sequence, 0));
            stream.WriteFloat(jmfEntity?.JmfFramerate ?? GetFloatProperty(Attributes.Framerate, 10));
            stream.WriteFloat(jmfEntity?.JmfScale ?? GetFloatProperty(Attributes.Scale, 1));
            stream.WriteFloat(jmfEntity?.JmfRadius ?? GetFloatProperty(Attributes.Radius, 0));
            stream.WriteBytes(GetFixedLengthByteArray(jmfEntity?.UnknownData, 28));

            var serializedProperties = entity.Properties
                .Where(kv => kv.Key != Attributes.Classname && kv.Key != Attributes.Origin)
                .ToArray();
            if (jmfEntity != null) serializedProperties = jmfEntity.JmfProperties.ToArray();
            stream.WriteInt(serializedProperties.Length);
            foreach (var property in serializedProperties)
            {
                if (property.Key == Attributes.Classname)
                    continue;

                stream.WriteLengthPrefixedString(property.Key);
                stream.WriteLengthPrefixedString(property.Value);
            }

            stream.WriteInt(entity.VisGroups.Count);
            foreach (var visGroup in entity.VisGroups)
                stream.WriteInt(visGroup.ID);

            stream.WriteInt(entity.Brushes.Count);
            foreach (var brush in entity.Brushes)
                WriteBrush(stream, brush);


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

        private static void WriteBrush(Stream stream, Brush brush)
        {
            var jmfBrush = brush as JmfBrush;
            var patch = jmfBrush?.Patch;

            stream.WriteInt(patch != null ? 1 : 0);
            stream.WriteInt((int)(jmfBrush?.JmfFlags ?? GetJmfMapObjectFlags(brush)));
            stream.WriteInt(brush.Group?.ID ?? 0);
            stream.WriteInt(brush.Group == null ? 0 : GetRootGroupID(brush.Group));
            WriteColor(stream, brush.Color);

            stream.WriteInt(brush.VisGroups.Count);
            foreach (var visGroup in brush.VisGroups)
                stream.WriteInt(visGroup.ID);

            stream.WriteInt(brush.Faces.Count);
            foreach (var face in brush.Faces)
                WriteFace(stream, face);

            if (patch != null)
                WritePatch(stream, patch);
        }

        private static void WriteFace(Stream stream, Face face)
        {
            var jmfFace = face as JmfFace;

            stream.WriteInt(jmfFace?.RenderFlags ?? 0);
            stream.WriteInt(face.Vertices.Count);

            WriteVector3D(stream, face.TextureRightAxis);
            stream.WriteFloat(face.TextureShift.X);
            WriteVector3D(stream, face.TextureDownAxis);
            stream.WriteFloat(face.TextureShift.Y);
            stream.WriteFloat(face.TextureScale.X);
            stream.WriteFloat(face.TextureScale.Y);
            stream.WriteFloat(face.TextureAngle);

            stream.WriteInt((int)(jmfFace?.TextureAlignment ?? GetTextureAlignment()));
            stream.Write(GetFixedLengthByteArray(jmfFace?.UnknownData, 12));
            stream.WriteInt((int)(jmfFace?.Contents ?? JmfSurfaceContents.None));
            stream.WriteFixedLengthString(face.TextureName, 64);

            WriteVector3D(stream, face.Plane.Normal);
            stream.WriteFloat(face.Plane.Distance);
            stream.WriteInt((int)(jmfFace?.AxisAlignment ?? GetAxisAlignment()));

            for (int i = 0; i < face.Vertices.Count; i++)
            {
                WriteVector3D(stream, face.Vertices[i]);
                if (jmfFace != null)
                {
                    stream.WriteFloat(jmfFace.VertexUVCoordinates[i].X);
                    stream.WriteFloat(jmfFace.VertexUVCoordinates[i].Y);
                    stream.WriteInt((int)jmfFace.VertexSelectionState[i]);
                }
                else
                {
                    stream.WriteFloat(0f);
                    stream.WriteFloat(0f);
                    stream.WriteInt(0);
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
                if ((face.TextureRightAxis == new Vector3D(0, 1, 0) && face.TextureDownAxis == new Vector3D(0, 0, -1)) ||   // Projected along X axis
                    (face.TextureRightAxis == new Vector3D(1, 0, 0) && face.TextureDownAxis == new Vector3D(0, 0, -1)) ||   // Projected along Y axis
                    (face.TextureRightAxis == new Vector3D(1, 0, 0) && face.TextureDownAxis == new Vector3D(0, -1, 0)))     // Projected alogn Z axis
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

        private static void WritePatch(Stream stream, JmfPatch patch)
        {
            stream.WriteInt(patch.Columns);
            stream.WriteInt(patch.Rows);

            WriteVector3D(stream, patch.TextureRightAxis);
            stream.WriteFloat(patch.TextureShift.X);
            WriteVector3D(stream, patch.TextureDownAxis);
            stream.WriteFloat(patch.TextureShift.Y);
            stream.WriteFloat(patch.TextureScale.X);
            stream.WriteFloat(patch.TextureScale.Y);
            stream.WriteFloat(patch.TextureAngle);
            stream.WriteInt((int)patch.TextureAlignment);
            stream.WriteBytes(GetFixedLengthByteArray(patch.UnknownData, 12));

            stream.WriteInt((int)patch.Contents);
            stream.WriteFixedLengthString(patch.TextureName, 64);
            stream.WriteBytes(GetFixedLengthByteArray(patch.UnknownData2, 4));

            var emptyControlPoint = new JmfPatchControlPoint();
            for (int column = 0; column < 32; column++)
            {
                for (int row = 0; row < 32; row++)
                {
                    var controlPoint = column < patch.Columns && row < patch.Rows ? patch.ControlPoints[column, row] : emptyControlPoint;
                    WritePatchControlPoint(stream, controlPoint);
                }
            }
        }

        private static void WritePatchControlPoint(Stream stream, JmfPatchControlPoint controlPoint)
        {
            WriteVector3D(stream, controlPoint.Position);
            WriteVector3D(stream, controlPoint.Normal);
            stream.WriteFloat(controlPoint.UV.X);
            stream.WriteFloat(controlPoint.UV.Y);
            stream.WriteInt(controlPoint.IsSelected ? 1 : 0);
        }


        private static Vector3D ReadVector3D(Stream stream)
        {
            return new Vector3D(stream.ReadFloat(), stream.ReadFloat(), stream.ReadFloat());
        }

        private static Color ReadColor(Stream stream)
        {
            var color = stream.ReadBytes(4);    // RGBA
            return new Color(color[0], color[1], color[2], color[3]);
        }

        private static Angles ReadAngles(Stream stream)
        {
            var pitch = stream.ReadFloat();
            var yaw = stream.ReadFloat();
            var roll = stream.ReadFloat();
            return new Angles(roll, pitch, yaw);
        }


        private static void WriteVector3D(Stream stream, Vector3D vector)
        {
            stream.WriteFloat(vector.X);
            stream.WriteFloat(vector.Y);
            stream.WriteFloat(vector.Z);
        }

        private static void WriteColor(Stream stream, Color color)
        {
            var rgba = new byte[] { color.R, color.G, color.B, color.A };
            stream.WriteBytes(rgba);
        }

        private static void WriteAngles(Stream stream, Angles angles)
        {
            stream.WriteFloat(angles.Pitch);
            stream.WriteFloat(angles.Yaw);
            stream.WriteFloat(angles.Roll);
        }


        private static int GetRootGroupID(Group group) => group.Group == null ? group.ID : GetRootGroupID(group.Group);

        private static JmfMapObjectFlags GetJmfMapObjectFlags(Group group)
        {
            var flags = JmfMapObjectFlags.None;

            if (group.IsSelected)
                flags |= JmfMapObjectFlags.Selected;

            if (group.IsHidden)
                flags |= JmfMapObjectFlags.Hidden;

            return flags;
        }

        private static JmfMapObjectFlags GetJmfMapObjectFlags(Entity entity)
        {
            var flags = JmfMapObjectFlags.None;

            if (!entity.Brushes.Any())
                flags |= JmfMapObjectFlags.PointBased;

            if (entity.IsSelected)
                flags |= JmfMapObjectFlags.Selected;

            if (entity.IsHidden)
                flags |= JmfMapObjectFlags.Hidden;

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

        private static JmfMapObjectFlags GetJmfMapObjectFlags(Brush brush)
        {
            var flags = JmfMapObjectFlags.None;

            if (brush.IsSelected)
                flags |= JmfMapObjectFlags.Selected;

            if (brush.IsHidden)
                flags |= JmfMapObjectFlags.Hidden;

            return flags;
        }

        private static JmfRenderMode GetJmfRenderMode(Entity entity)
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

        private static byte[] GetFixedLengthByteArray(byte[]? data, int length)
        {
            if (data == null)
                return new byte[length];
            else if (data.Length == length)
                return data;

            var buffer = new byte[length];
            Array.Copy(data, buffer, Math.Min(data.Length, length));
            return buffer;
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

    public class JmfFileSaveSettings : FileSaveSettings
    {
        public JmfFileVersion FileVersion { get; set; } = JmfFileVersion.V121;
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
