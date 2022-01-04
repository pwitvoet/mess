using MESS.Common;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MESS.Formats
{
    /// <summary>
    /// JMF is J.A.C.K.'s map format. Similar to the RMF format,
    /// it also supports logical and visual groups, camera's and paths.
    /// Unlike RMF, JMF supports objects being part of multiple visual groups.
    /// </summary>
    public class JmfFormat
    {
        public static Map Load(Stream stream)
        {
            var map = new Map();

            var jhmfMagicString = stream.ReadString(4);
            if (jhmfMagicString != "JHMF")
                throw new InvalidDataException($"Expected 'JHMF' magic string, but found '{jhmfMagicString}'.");

            var unknown1 = stream.ReadBytes(4);

            // Recent export paths (not relevant for MESS):
            var exportPathCount = stream.ReadInt();
            for (int i = 0; i < exportPathCount; i++)
                stream.ReadLengthPrefixedString();

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
                groups[kv.Key].Group = groups[kv.Value];

            // Objects can be part of multiple VIS groups (not relevant for MESS, and not supported):
            var visGroupCount = stream.ReadInt();
            for (int i = 0; i < visGroupCount; i++)
                map.VisGroups.Add(ReadVisGroup(stream));
            var visGroups = map.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

            var cordonMin = ReadVector3D(stream);
            var cordonMax = ReadVector3D(stream);

            var cameraCount = stream.ReadInt();
            for (int i = 0; i < cameraCount; i++)
                map.Cameras.Add(ReadCamera(stream));

            var pathCount = stream.ReadInt();
            for (int i = 0; i < pathCount; i++)
                map.EntityPaths.Add(ReadEntityPath(stream));

            try
            {
                while (true)
                    map.Entities.Add(ReadEntity(stream, groups, visGroups));
            }
            catch (EndOfStreamException)
            {
                // End of file, no more entities.
            }

            return map;
        }

        public static void Save(Map map, Stream stream) => throw new NotSupportedException();


        private static (Group group, int parentGroupID) ReadGroup(Stream stream)
        {
            var group = new Group();
            group.ID = stream.ReadInt();
            var parentGroupID = stream.ReadInt();
            var flags = (Flags)stream.ReadInt();
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
            visGroup.IsVisible = stream.ReadBytes(1)[0] != 0;
            return visGroup;
        }

        private static Camera ReadCamera(Stream stream)
        {
            var camera = new Camera();
            camera.EyePosition = ReadVector3D(stream);
            camera.LookAtPosition = ReadVector3D(stream);
            var isSelected = ((Flags)stream.ReadInt()).HasFlag(Flags.Selected);
            var color = ReadColor(stream);
            return camera;
        }

        private static EntityPath ReadEntityPath(Stream stream)
        {
            var path = new EntityPath();
            path.ClassName = stream.ReadLengthPrefixedString();
            path.Name = stream.ReadLengthPrefixedString();
            path.Type = (PathType)stream.ReadInt();
            var unknown1 = stream.ReadBytes(4);
            var color = ReadColor(stream);

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

            var flags = (Flags)stream.ReadInt();
            var color = ReadColor(stream);

            var propertyCount = stream.ReadInt();
            for (int i = 0; i < propertyCount; i++)
            {
                var key = stream.ReadLengthPrefixedString();
                var value = stream.ReadLengthPrefixedString();
                node.Properties[key] = value;
            }

            return node;
        }

        private static Entity ReadEntity(Stream stream, IDictionary<int, Group> groups, IDictionary<int, VisGroup> visGroups)
        {
            var className = stream.ReadLengthPrefixedString();
            var origin = ReadVector3D(stream);
            var flags = (Flags)stream.ReadInt();    // NOTE: Editor state (hidden, selected), not entity spawnflags.
            var groupID = stream.ReadInt();
            var rootGroupID = stream.ReadInt();
            var color = ReadColor(stream);

            // NOTE: Each entity contains a list of 'special' attribute names, but they're not relevant for MESS:
            for (int i = 0; i < 13; i++)
                stream.ReadLengthPrefixedString();

            // Many 'special' attributes are stored here, but they're only included in a MAP file export if they
            // also occur in the properties dictionary, so MESS can ignore these:
            var spawnflags = stream.ReadInt();
            var angles = ReadAngles(stream);
            var rendering = (Rendering)stream.ReadInt();

            var rawFxColor = stream.ReadBytes(4);
            var fxColor = new Color(rawFxColor[3], rawFxColor[2], rawFxColor[1], rawFxColor[0]);

            var renderMode = stream.ReadInt();
            var renderFX = stream.ReadInt();
            var body = stream.ReadShort();
            var skin = stream.ReadShort();
            var sequence = stream.ReadInt();
            var framerate = stream.ReadFloat();
            var scale = stream.ReadFloat();
            var radius = stream.ReadFloat();
            var unknown1 = stream.ReadBytes(28);

            var propertyCount = stream.ReadInt();
            var properties = Enumerable.Range(0, propertyCount)
                .Select(i => Tuple.Create(stream.ReadLengthPrefixedString(), stream.ReadLengthPrefixedString()))
                .ToArray();

            var visGroupCount = stream.ReadInt();
            var visGroupIDs = Enumerable.Range(0, visGroupCount)
                .Select(i => stream.ReadInt())
                .ToArray();

            var brushCount = stream.ReadInt();
            var brushes = Enumerable.Range(0, brushCount)
                .Select(i => ReadBrush(stream, groups, visGroups))
                .ToArray();

            var entity = new Entity(brushes);
            entity.ClassName = className;
            entity.Origin = origin;
            entity.Color = color;

            entity.Group = (groupID != 0 && groups.TryGetValue(groupID, out var group)) ? group : null;
            //entity.VisGroup;  // TODO: MESS does not support objects being part of multiple VIS groups!

            foreach (var property in properties)
                entity.Properties[property.Item1] = property.Item2;

            return entity;
        }

        private static Brush ReadBrush(Stream stream, IDictionary<int, Group> groups, IDictionary<int, VisGroup> visGroups)
        {
            var curvesCount = stream.ReadInt();
            var flags = (Flags)stream.ReadInt();
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

            var brush = new Brush(faces);
            brush.Color = color;
            brush.Group = (groupID != 0 && groups.TryGetValue(groupID, out var group)) ? group : null;
            //brush.VisGroup;  // TODO: MESS does not support objects being part of multiple VIS groups!

            return brush;
        }

        private static Face ReadFace(Stream stream)
        {
            var face = new Face();

            var renderFlags = stream.ReadInt();
            var vertexCount = stream.ReadInt();
            face.TextureRightAxis = ReadVector3D(stream);
            var textureShiftX = stream.ReadFloat();
            face.TextureDownAxis = ReadVector3D(stream);
            var textureShiftY = stream.ReadFloat();
            face.TextureShift = new Vector2D(textureShiftX, textureShiftY);
            face.TextureScale = new Vector2D(stream.ReadFloat(), stream.ReadFloat());
            face.TextureAngle = stream.ReadFloat();
            var unknown1 = stream.ReadInt();
            var unknown2 = stream.ReadBytes(16);
            face.TextureName = stream.ReadString(64);

            var unknown3 = stream.ReadFloat();
            var unknown4 = stream.ReadFloat();
            var unknown5 = stream.ReadFloat();
            var unknown6 = stream.ReadFloat();
            var unknown7 = stream.ReadInt();

            for (int i = 0; i < vertexCount; i++)
            {
                var position = ReadVector3D(stream);

                // TODO: Normal?
                var vertexUnknown1 = stream.ReadFloat();
                var vertexUnknown2 = stream.ReadFloat();
                var vertexUnknown3 = stream.ReadFloat();

                face.Vertices.Add(position);
            }

            face.PlanePoints = face.Vertices.Take(3).Reverse().ToArray();

            return face;
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
    }


    enum Rendering : int
    {
        Glow =                  0x00000025, // HL render modes 3 (glow) and 5 (additive)
        Normal =                0x00000100, // HL render mode 0 (normal)
        Translucent =           0x00040065, // HL render modes 1 (color) and 2 (texture)
        TransparentColorKey =   0x00400165, // HL render mode 4 (solid)
    }

    [Flags]
    enum Flags
    {
        None =          0x00,
        PointBased =    0x01,   // Or maybe 'HasOrigin'?
        Selected =      0x02,
        Hidden =        0x08,
        RenderMode =    0x10,   // Any other render mode than 'normal'
    }
}
