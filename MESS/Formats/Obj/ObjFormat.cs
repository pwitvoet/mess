using MESS.Logging;
using MESS.Macros;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Globalization;
using System.Text;

namespace MESS.Formats.Obj
{
    /// <summary>
    /// OBJ is a file format that stores 3D geometry.
    /// It cannot store entity data, so it's only useful for exporting map geometry.
    /// </summary>
    public static class ObjFormat
    {
        public static void Export(Map map, Stream stream, string filePath, ObjFileSaveSettings? settings = null, ILogger? logger = null)
        {
            using (var objSaver = new ObjSaver(stream, filePath, settings, logger))
                objSaver.ExportMap(map);
        }


        class ObjSaver : IDisposable
        {
            private const string TextureNamePlaceholder = "texturename";

            private const string LayerNamePlaceholder = "layername";
            private const string LayerIdPlaceholder = "layerid";
            private const string GroupIdPlaceholder = "groupid";
            private const string EntityIdPlaceholder = "entityid";
            private const string BrushIdPlaceholder = "brushid";
            private const string EntityPropertyPlaceholderPrefix = "entity.";


            public TextWriter Writer { get; }
            public string FilePath { get; }
            public ObjFileSaveSettings Settings { get; }
            public ILogger Logger { get; }

            private string TexturePathFormat { get; }
            private string ObjectNameFormat { get; }

            private HashSet<string> SkipTextures { get; } = new();
            private string FloatFormat { get; }
            private float Scale { get; }

            private Dictionary<Vector3D, int> Vertices = new();
            private Dictionary<Vector3D, int> Normals = new();
            private Dictionary<Vector2D, int> UvCoordinates = new();


            public ObjSaver(Stream stream, string filePath, ObjFileSaveSettings? settings, ILogger? logger)
            {
                Writer = new StreamWriter(stream, new UTF8Encoding(false));
                FilePath = filePath;
                Settings = settings ?? new ObjFileSaveSettings();
                Logger = logger ?? new MultiLogger();

                TexturePathFormat = Settings.TexturePathFormat ?? $"{{{TextureNamePlaceholder}}}.png";
                ObjectNameFormat = Settings.ObjectNameFormat ?? GetDefaultObjectNameFormat(Settings.ObjectGrouping);

                foreach (var skipTexture in Settings.SkipTextures)
                    SkipTextures.Add(skipTexture.ToLowerInvariant());

                FloatFormat = "r";
                Scale = Settings.Scale <= 0 ? 1f : Settings.Scale;
            }

            public void Dispose()
            {
                Writer.Dispose();
            }

            public void ExportMap(Map map)
            {
                // Create the mtl file:
                if (Settings.MtlFilePath != "")
                {
                    var mtlFilePath = Settings.MtlFilePath ?? Path.ChangeExtension(FilePath, ".mtl");
                    if (!Path.IsPathRooted(mtlFilePath))
                        mtlFilePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", mtlFilePath);

                    var mtlDirectory = Path.GetDirectoryName(mtlFilePath);
                    if (!string.IsNullOrEmpty(mtlDirectory))
                        Directory.CreateDirectory(mtlDirectory);

                    using (var mtlFile = File.Create(mtlFilePath))
                    using (var mtlWriter = new StreamWriter(mtlFile, new UTF8Encoding(false)))
                    {
                        var textureNames = map.WorldGeometry
                            .Concat(map.Entities.SelectMany(entity => entity.Brushes))
                            .SelectMany(brush => brush.Faces)
                            .Select(face => face.TextureName)
                            .Distinct()
                            .Where(textureName => !SkipTextures.Contains(textureName.ToLowerInvariant()))
                            .ToArray();

                        foreach (var textureName in textureNames)
                        {
                            mtlWriter.WriteLine();
                            mtlWriter.WriteLine($"newmtl {textureName}");

                            var texturePath = TexturePathFormat.Replace($"{{{TextureNamePlaceholder}}}", textureName);
                            mtlWriter.WriteLine($"map_Kd {texturePath}");
                        }
                    }

                    var relativeMtlPath = Path.GetRelativePath(Path.GetDirectoryName(FilePath) ?? "", mtlFilePath);
                    Writer.WriteLine($"mtllib {relativeMtlPath}");
                }


                // Prepare data (note that .obj file indices start at 1, not at 0):
                foreach (var brush in map.WorldGeometry.Concat(map.Entities.SelectMany(entity => entity.Brushes)))
                {
                    foreach (var face in brush.Faces)
                    {
                        if (SkipFace(face))
                            continue;

                        foreach (var vertex in face.Vertices)
                        {
                            if (!Vertices.ContainsKey(vertex))
                                Vertices[vertex] = Vertices.Count + 1;

                            var uv = face.GetTextureCoordinates(vertex);
                            if (!UvCoordinates.ContainsKey(uv))
                                UvCoordinates[uv] = UvCoordinates.Count + 1;
                        }

                        if (!Normals.ContainsKey(face.Plane.Normal))
                            Normals[face.Plane.Normal] = Normals.Count + 1;
                    }
                }


                // Write vertices, UV coordinates and normals:
                Writer.WriteLine();
                Writer.WriteLine("# Vertices");
                foreach (var kv in Vertices.OrderBy(kv => kv.Value))
                {
                    var vertex = ToTargetCoordinateSystem(kv.Key);
                    Writer.WriteLine($"v {FormatFloat(vertex.X * Scale)} {FormatFloat(vertex.Y * Scale)} {FormatFloat(vertex.Z * Scale)}");
                }

                Writer.WriteLine();
                Writer.WriteLine("# UV coordinates");
                foreach (var kv in UvCoordinates.OrderBy(kv => kv.Value))
                {
                    // TODO: This must be divided by texture dimensions to get normalized UV coordinates!
                    var uv = kv.Key;
                    Writer.WriteLine($"vt {FormatFloat(uv.X)} {FormatFloat(uv.Y)}");
                }

                Writer.WriteLine();
                Writer.WriteLine("# Normals");
                foreach (var kv in Normals.OrderBy(kv => kv.Value))
                {
                    var normal = ToTargetCoordinateSystem(kv.Key);
                    Writer.WriteLine($"vn {FormatFloat(kv.Key.X)} {FormatFloat(kv.Key.Y)} {FormatFloat(kv.Key.Z)}");
                }


                // TODO: Skip objects that do not have any visible faces!
                // Write objects and their faces:
                Writer.WriteLine();
                Writer.WriteLine("# Objects");
                switch (Settings.ObjectGrouping)
                {
                    case ObjObjectGrouping.Map:
                    {
                        Writer.WriteLine();
                        WriteObjectName(entity: map.Worldspawn);
                        foreach (var brush in map.WorldGeometry.Concat(map.Entities.SelectMany(entity => entity.Brushes)))
                            WriteBrush(brush);
                        break;
                    }

                    case ObjObjectGrouping.Layer:
                    {
                        foreach (var visGroup in map.VisGroups)
                        {
                            var brushes = GetBrushes(visGroup.Objects);
                            if (HasVisibleFaces(brushes))
                            {
                                Writer.WriteLine();
                                WriteObjectName(layer: visGroup);
                                foreach (var brush in brushes)
                                    WriteBrush(brush);
                            }
                        }

                        var ungroupedBrushes = map.WorldGeometry
                            .Where(brush => !brush.VisGroups.Any())
                            .Concat(map.Entities
                                .Where(entity => !entity.VisGroups.Any())
                                .SelectMany(entity => entity.Brushes));
                        if (HasVisibleFaces(ungroupedBrushes))
                        {
                            Writer.WriteLine();
                            Writer.WriteLine("o no_layer");
                            foreach (var brush in ungroupedBrushes)
                                WriteBrush(brush);
                        }
                        break;
                    }

                    case ObjObjectGrouping.Group:
                    {
                        foreach (var group in map.Groups.Where(group => group.Group is null))
                        {
                            var brushes = GetBrushes(group.Objects);
                            if (HasVisibleFaces(brushes))
                            {
                                Writer.WriteLine();
                                WriteObjectName(group: group);
                                foreach (var brush in GetBrushes(group.Objects))
                                    WriteBrush(brush);
                            }
                        }

                        var ungroupedBrushes = map.WorldGeometry
                            .Where(brush => brush.Group is null)
                            .Concat(map.Entities
                                .Where(entity => entity.Group is null)
                                .SelectMany(entity => entity.Brushes))
                            .ToArray();
                        if (HasVisibleFaces(ungroupedBrushes))
                        {
                            Writer.WriteLine();
                            Writer.WriteLine("o no_group");
                            foreach (var brush in ungroupedBrushes)
                                WriteBrush(brush);
                        }
                        break;
                    }

                    case ObjObjectGrouping.Entity:
                    {
                        var entityID = 0;
                        foreach (var entity in map.Entities.Prepend(map.Worldspawn))
                        {
                            if (HasVisibleFaces(entity.Brushes))
                            {
                                Writer.WriteLine();
                                WriteObjectName(entity: entity, entityID: entityID);
                                foreach (var brush in entity.Brushes)
                                    WriteBrush(brush);
                            }

                            entityID += 1;
                        }
                        break;
                    }

                    default:
                    case ObjObjectGrouping.Brush:
                    {
                        var entityID = 0;
                        foreach (var entity in map.Entities.Prepend(map.Worldspawn))
                        {
                            var brushID = 0;
                            foreach (var brush in entity.Brushes)
                            {
                                if (HasVisibleFaces(brush))
                                {
                                    Writer.WriteLine();
                                    WriteObjectName(entity: entity, entityID: entityID, brushID: brushID);
                                    WriteBrush(brush);
                                }
                                brushID += 1;
                            }

                            entityID += 1;
                        }
                        break;
                    }
                }
            }


            private IEnumerable<Brush> GetBrushes(IEnumerable<MapObject> mapObjects)
            {
                foreach (var mapObject in mapObjects)
                {
                    switch (mapObject)
                    {
                        case Group group:
                            foreach (var brush in GetBrushes(group.Objects))
                                yield return brush;
                            break;

                        case Entity entity:
                            foreach (var brush in entity.Brushes)
                                yield return brush;
                            break;

                        case Brush brush:
                            yield return brush;
                            break;
                    }
                }
            }

            private bool HasVisibleFaces(Brush brush)
                => brush.Faces.Any(face => !SkipFace(face));

            private bool HasVisibleFaces(IEnumerable<Brush> brushes)
                => brushes.Any(HasVisibleFaces);

            private bool SkipFace(Face face)
                => SkipTextures.Contains(face.TextureName.ToLowerInvariant());

            private void WriteObjectName(VisGroup? layer = null, Group? group = null, Entity? entity = null, int? entityID = null, int? brushID = null)
            {
                var objectName = System.Text.RegularExpressions.Regex.Replace(ObjectNameFormat, @"\{([^\}]+)\}", match =>
                {
                    var placeholder = match.Groups[1].Value;
                    switch (placeholder)
                    {
                        case LayerNamePlaceholder: return layer?.Name ?? "";
                        case LayerIdPlaceholder: return layer?.ID.ToString() ?? "";
                        case GroupIdPlaceholder: return group?.ID.ToString() ?? "";
                        case EntityIdPlaceholder: return entityID?.ToString() ?? "";
                        case BrushIdPlaceholder: return brushID?.ToString() ?? "";
                    }

                    if (placeholder.StartsWith(EntityPropertyPlaceholderPrefix) && entity is not null)
                    {
                        var key = placeholder.Substring(EntityPropertyPlaceholderPrefix.Length);
                        return entity.Properties.TryGetValue(key, out var value) ? value : "";
                    }

                    return match.Value;
                });

                // TODO: Does the .obj file format impose any restrictions on object names? I'm going to assume spaces can cause trouble:
                objectName = objectName.Replace(" ", "_");

                Writer.WriteLine($"o {objectName}");
            }

            private void WriteBrush(Brush brush)
            {
                foreach (var face in brush.Faces)
                {
                    if (SkipFace(face))
                        continue;

                    Writer.WriteLine($"usemtl {face.TextureName}");
                    Writer.Write($"f");
                    foreach (var vertex in face.Vertices)
                    {
                        var vertexID = Vertices[vertex];
                        var uvID = UvCoordinates[face.GetTextureCoordinates(vertex)];
                        var normalID = Normals[face.Plane.Normal];
                        Writer.Write($" {vertexID}/{uvID}/{normalID}");
                    }
                    Writer.WriteLine();
                }
            }


            private string GetDefaultObjectNameFormat(ObjObjectGrouping grouping)
            {
                switch (grouping)
                {
                    case ObjObjectGrouping.Map: return "map";
                    case ObjObjectGrouping.Layer: return $"layer_{{{LayerNamePlaceholder}}}";
                    case ObjObjectGrouping.Group: return $"group_{{{GroupIdPlaceholder}}}";
                    case ObjObjectGrouping.Entity: return $"entity_{{{EntityIdPlaceholder}}}";

                    default:
                    case ObjObjectGrouping.Brush: return $"entity_{{{EntityIdPlaceholder}}}_brush_{{{BrushIdPlaceholder}}}";
                }
            }

            private Vector3D ToTargetCoordinateSystem(Vector3D point)
            {
                // Both .obj and .map files are right-handed, but .map files are z-up, while .obj files are y-up:
                var upY = Settings.UpAxis == ObjUpAxis.Y;

                return new Vector3D(
                    point.X,
                    upY ? point.Z : point.Y,
                    upY ? -point.Y : point.Z);
            }

            private string FormatFloat(float f)
                => f.ToString(FloatFormat, CultureInfo.InvariantCulture).Replace('E', 'e');
        }
    }
}
