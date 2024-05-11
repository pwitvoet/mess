using MESS.Common;
using MESS.Formats.MAP.Trenchbroom;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MESS.Formats.MAP
{
    /// <summary>
    /// The text-based MAP file format only stores entities and brushes.
    /// The special 'worldspawn' entity contains all map properties and world geometry.
    /// </summary>
    public static class MapFormat
    {
        public static Map Load(Stream stream, MapFileLoadSettings? settings = null, ILogger? logger = null)
        {
            using (var mapLoader = new MapLoader(stream, settings, logger))
                return mapLoader.LoadMap();
        }

        public static void Save(Map map, Stream stream, MapFileSaveSettings? settings = null, ILogger? logger = null)
        {
            using (var mapSaver = new MapSaver(stream, settings, logger))
                mapSaver.SaveMap(map);
        }


        class MapLoader : IDisposable
        {
            public ReadingContext Context { get; }
            public MapFileLoadSettings Settings { get; }
            public ILogger Logger { get; }


            public MapLoader(Stream stream, MapFileLoadSettings? settings, ILogger? logger)
            {
                Context = new ReadingContext(stream);
                Settings = settings ?? new MapFileLoadSettings();
                Logger = logger ?? new MultiLogger();
            }

            public void Dispose()
            {
                Context.Dispose();
            }

            public Map LoadMap()
            {
                var line = Context.Peek();
                if (Settings.TrenchbroomGroupHandling == TrenchbroomGroupHandling.ConvertToGroup && line?.Trim().StartsWith("// Game:") == true)
                    return LoadTrenchbroomMapFormat();
                else
                    return LoadDefaultMapFormat();
            }


            private Map LoadDefaultMapFormat()
            {
                var map = new Map();
                map.VisGroupAssignment = VisGroupAssignment.PerGroup;
                map.HasColorInformation = false;

                var entityNumber = 0;
                while (!Context.EndOfStream)
                {
                    try
                    {
                        var line = Context.ReadLine();
                        if (line is null)
                            break;

                        if (!line.Trim().StartsWith("{"))
                            continue;

                        var entity = ReadEntity();
                        if (entity == null)
                            break;

                        if (entity.ClassName == Entities.Worldspawn)
                        {
                            foreach (var kv in entity.Properties)
                                map.Properties[kv.Key] = kv.Value;

                            map.AddBrushes(entity.Brushes);
                        }
                        else
                        {
                            map.AddEntity(entity);
                        }

                        entityNumber += 1;
                    }
                    catch (Exception ex)
                    {
                        ex.Data["EntityNumber"] = entityNumber;
                        ex.Data["LineNumber"] = Context.CurrentLineNumber;
                        throw new InvalidDataException($"Failed to parse entity #{map.Entities.Count}.", ex);
                    }
                }

                return map;
            }

            private Map LoadTrenchbroomMapFormat()
            {
                var map = new TBMap();
                map.VisGroupAssignment = VisGroupAssignment.PerGroup;
                map.HasColorInformation = false;

                var entityNumber = 0;
                while (!Context.EndOfStream)
                {
                    try
                    {
                        var line = Context.ReadLine();
                        if (line is null)
                            break;

                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("//"))
                        {
                            // Read game and format information:
                            if (map.Game == null)
                            {
                                var gameMatch = Regex.Match(trimmedLine, @"^//\s*Game:\s*(?<game>.*)$");
                                if (gameMatch.Success)
                                    map.Game = gameMatch.Groups["game"].Value;
                            }

                            if (map.Format == null)
                            {
                                var formatMatch = Regex.Match(trimmedLine, @"^//\s*Format:\s*(?<format>.*)$");
                                if (formatMatch.Success)
                                    map.Format = formatMatch.Groups["format"].Value;
                            }
                        }
                        else if (trimmedLine.StartsWith("{"))
                        {
                            var entity = ReadTBEntity();
                            if (entity == null)
                                break;

                            if (entity.ClassName == Entities.Worldspawn)
                            {
                                foreach (var kv in entity.Properties)
                                    map.Properties[kv.Key] = kv.Value;

                                map.AddBrushes(entity.Brushes);
                            }
                            else
                            {
                                map.AddEntity(entity);
                            }

                            entityNumber += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Data["EntityNumber"] = entityNumber;
                        ex.Data["LineNumber"] = Context.CurrentLineNumber;
                        throw new InvalidDataException($"Failed to parse entity #{map.Entities.Count}.", ex);
                    }
                }

                TBUtil.ConvertFromTBGroups(map);

                return map;
            }

            private Entity ReadEntity()
            {
                var entity = new Entity();
                ReadEntityContent(entity);
                return entity;
            }

            private TBEntity ReadTBEntity()
            {
                var entity = new TBEntity();
                ReadEntityContent(entity);

                var protectedProperties = entity.Properties.GetString(TBUtil.TB.ProtectedProperties);
                if (protectedProperties != null)
                {
                    entity.ProtectedProperties = protectedProperties;
                    entity.Properties.Remove(TBUtil.TB.ProtectedProperties);
                }

                return entity;
            }

            private void ReadEntityContent(Entity entity)
            {
                var properties = new List<KeyValuePair<string, string>>();
                var brushes = new List<Brush>();
                while (true)
                {
                    var line = Context.ReadLine()?.Trim();
                    if (line is null)
                        throw new InvalidDataException($"Expected key-value pair, brush or end of entity, but found end of file.");

                    if (line.StartsWith("//"))
                    {
                        continue;
                    }
                    else if (line.StartsWith("\""))
                    {
                        var parts = line.Split('"');
                        if (parts.Length > 5)
                            throw new InvalidDataException($"Found content after key-value pair: '{line}' (double quotes are not allowed in entity key-value pairs).");

                        properties.Add(KeyValuePair.Create(parts[1], parts[3]));
                    }
                    else if (line.StartsWith("{"))
                    {
                        try
                        {
                            brushes.Add(ReadBrush());
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidDataException($"Failed to parse brush #{brushes.Count}.", ex);
                        }
                    }
                    else if (line.StartsWith("}"))
                    {
                        break;
                    }
                    else
                    {
                        throw new InvalidDataException($"Expected key-value pair, brush or end of entity, but found '{line}'.");
                    }
                }

                if (brushes.Any())
                    entity.AddBrushes(brushes);

                foreach (var property in properties)
                {
                    if (entity.Properties.ContainsKey(property.Key))
                    {
                        var classname = properties.FirstOrDefault(kv => kv.Key == Attributes.Classname).Value;  // TODO: What does this do if no match is found?!

                        if (Settings.DuplicateKeyHandling == DuplicateKeyHandling.UseFirst)
                        {
                            Logger.Warning($"Entity of type '{classname}' contains duplicate key '{property.Key}', using first value: '{entity.Properties[property.Key]}'.");
                            continue;
                        }
                        else if (Settings.DuplicateKeyHandling == DuplicateKeyHandling.UseLast)
                        {
                            Logger.Warning($"Entity of type '{classname}' contains duplicate key '{property.Key}', using last value: '{property.Value}'.");
                        }
                        else if (Settings.DuplicateKeyHandling == DuplicateKeyHandling.Fail)
                        {
                            var errorMessage = $"Entity of type '{classname}' contains duplicate key '{property.Key}' (first value: '{entity.Properties[property.Key]}', duplicate value: '{property.Value}'.";
                            Logger.Error(errorMessage);
                            throw new DuplicateKeyException(errorMessage);
                        }
                    }

                    entity.Properties[property.Key] = property.Value;
                }
            }

            private Brush ReadBrush()
            {
                var faces = new List<Face>();
                while (true)
                {
                    var line = Context.ReadLine()?.Trim();
                    if (line is null)
                        throw new InvalidDataException($"Expected face or end of brush, but found end of file.");

                    if (line.StartsWith("//"))
                        continue;
                    else if (line.StartsWith("("))
                        faces.Add(ReadFace(line));
                    else if (line.StartsWith("}"))
                        break;
                    else
                        throw new InvalidDataException($"Expected face or end of brush, but found '{line}'.");
                }
                return new Brush(faces);
            }

            private Face ReadFace(string line)
            {
                var parts = line.Split();
                if (parts.Length < 31)
                    throw new InvalidDataException($"Unexpected face format: '{line}'.");

                return new Face
                {
                    PlanePoints = new[] {
                    new Vector3D(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])),
                    new Vector3D(ParseFloat(parts[6]), ParseFloat(parts[7]), ParseFloat(parts[8])),
                    new Vector3D(ParseFloat(parts[11]), ParseFloat(parts[12]), ParseFloat(parts[13])),
                },
                    TextureName = parts[15],
                    TextureRightAxis = new Vector3D(ParseFloat(parts[17]), ParseFloat(parts[18]), ParseFloat(parts[19])),
                    TextureDownAxis = new Vector3D(ParseFloat(parts[23]), ParseFloat(parts[24]), ParseFloat(parts[25])),
                    TextureShift = new Vector2D(ParseFloat(parts[20]), ParseFloat(parts[26])),
                    TextureAngle = ParseFloat(parts[28]),
                    TextureScale = new Vector2D(ParseFloat(parts[29]), ParseFloat(parts[30])),
                };
            }


            private static float ParseFloat(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }


        class MapSaver : IDisposable
        {
            public TextWriter Writer { get; }
            public MapFileSaveSettings Settings { get; }
            public ILogger Logger { get; }

            private string FloatFormat { get; }


            public MapSaver(Stream stream, MapFileSaveSettings? settings, ILogger? logger)
            {
                Writer = new StreamWriter(stream, new UTF8Encoding(false));
                Settings = settings ?? new MapFileSaveSettings();
                Logger = logger ?? new MultiLogger();

                if (Settings.DecimalsCount == null)
                    FloatFormat = "r";
                else
                    FloatFormat = "0." + new string('#', Math.Clamp(Settings.DecimalsCount.Value, 0, 100));
            }

            public void Dispose()
            {
                Writer.Dispose();
            }

            public void SaveMap(Map map)
            {
                switch (Settings.FileVariant)
                {
                    case MapFileVariant.Valve220: SaveDefaultMapFormat(map); break;
                    case MapFileVariant.TrenchbroomValve220: SaveTrenchbroomMapFormat(map); break;

                    default: throw new NotSupportedException($"Map file variant {Settings.FileVariant} is not supported.");
                }
            }


            private void SaveDefaultMapFormat(Map map)
            {
                var worldspawn = new Entity(map.WorldGeometry);
                worldspawn.ClassName = Entities.Worldspawn;
                foreach (var kv in map.Properties)
                    worldspawn.Properties[kv.Key] = kv.Value;

                worldspawn.Properties["mapversion"] = "220";

                WriteEntity(worldspawn);
                foreach (var entity in map.Entities)
                    WriteEntity(entity);

                foreach (var path in map.EntityPaths)
                {
                    foreach (var entity in path.GenerateEntities())
                        WriteEntity(entity);
                }
            }

            private void SaveTrenchbroomMapFormat(Map map)
            {
                if (map.Groups.Any() || map.VisGroups.Any())
                {
                    map = TBUtil.CopyMap(map);
                    TBUtil.ConvertToTBGroups(map, Settings.TooManyVisGroupsHandling, OnVisGroupFailure);
                }


                var worldspawn = new Entity(map.WorldGeometry);
                worldspawn.ClassName = Entities.Worldspawn;
                foreach (var kv in map.Properties)
                    worldspawn.Properties[kv.Key] = kv.Value;

                worldspawn.Properties["mapversion"] = "220";

                Writer.WriteLine($"// Game: {Settings.TrenchbroomGameName ?? (map as TBMap)?.Game}");
                Writer.WriteLine("// Format: Valve");

                var entityNumber = 0;
                Writer.WriteLine($"// entity {entityNumber++}");
                WriteEntity(worldspawn);
                foreach (var entity in map.Entities)
                {
                    Writer.WriteLine($"// entity {entityNumber++}");
                    WriteEntity(entity);
                }

                foreach (var path in map.EntityPaths)
                {
                    foreach (var entity in path.GenerateEntities())
                    {
                        Writer.WriteLine($"// entity {entityNumber++}");
                        WriteEntity(entity);
                    }
                }
            }

            private void WriteEntity(Entity entity)
            {
                Writer.WriteLine("{");

                var logDescription = $"Entity of type '{entity.ClassName}'";
                foreach (var property in entity.Properties)
                {
                    var key = Validation.ValidateKey(property.Key, null, Settings, Logger, logDescription);
                    var value = Validation.ValidateValue(property.Value, null, Settings, Logger, logDescription);

                    Writer.WriteLine(FormattableString.Invariant($"\"{key}\" \"{value}\""));
                }

                if (Settings.FileVariant == MapFileVariant.TrenchbroomValve220)
                {
                    var brushNumber = 0;
                    foreach (var brush in entity.Brushes)
                    {
                        Writer.WriteLine($"// brush {brushNumber++}");
                        WriteBrush(brush);
                    }
                }
                else
                {
                    foreach (var brush in entity.Brushes)
                        WriteBrush(brush);
                }

                Writer.WriteLine("}");
            }

            private void WriteBrush(Brush brush)
            {
                Writer.WriteLine("{");

                foreach (var face in brush.Faces)
                    WriteFace(face);

                Writer.WriteLine("}");
            }

            private void WriteFace(Face face)
            {
                var textureName = Validation.ValidateTextureName(face.TextureName, null, Settings, Logger);

                foreach (var point in face.PlanePoints)
                    Writer.Write($"( {FormatFloat(point.X)} {FormatFloat(point.Y)} {FormatFloat(point.Z)} ) ");

                Writer.Write(textureName);
                Writer.Write(" ");
                Writer.Write($"[ {FormatFloat(face.TextureRightAxis.X)} {FormatFloat(face.TextureRightAxis.Y)} {FormatFloat(face.TextureRightAxis.Z)} {FormatFloat(face.TextureShift.X)} ] ");
                Writer.Write($"[ {FormatFloat(face.TextureDownAxis.X)} {FormatFloat(face.TextureDownAxis.Y)} {FormatFloat(face.TextureDownAxis.Z)} {FormatFloat(face.TextureShift.Y)} ] ");
                Writer.WriteLine($"{FormatFloat(face.TextureAngle)} {FormatFloat(face.TextureScale.X)} {FormatFloat(face.TextureScale.Y)} ");
            }


            private string FormatFloat(float f) => f.ToString(FloatFormat, CultureInfo.InvariantCulture).Replace('E', 'e');

            private void OnVisGroupFailure(MapObject mapObject)
            {
                var objectDescription = mapObject switch
                {
                    Brush => "Brush",
                    Entity entity => $"Entity of type '{entity.ClassName}'",
                    Mapping.Group => "Group",
                    _ => "Unknown object",
                };

                var errorMessage = $"{objectDescription} is part of {mapObject.VisGroups.Count} VIS groups.";
                Logger.Error(errorMessage);
                throw new MapSaveException(errorMessage);
            }
        }


        class ReadingContext : IDisposable
        {
            public int CurrentLineNumber { get; private set; }
            public bool EndOfStream => _nextLine == null && _reader.EndOfStream;


            private StreamReader _reader;
            private string? _nextLine;


            public ReadingContext(Stream stream)
            {
                _reader = new StreamReader(stream, Encoding.UTF8);
            }

            public void Dispose() => _reader.Dispose();

            public string? ReadLine()
            {
                if (_nextLine != null)
                {
                    var line = _nextLine;
                    CurrentLineNumber += 1;
                    _nextLine = null;
                    return line;
                }
                else
                {
                    var line = _reader.ReadLine();
                    if (line != null)
                        CurrentLineNumber += 1;
                    return line;
                }
            }

            public string? Peek()
            {
                if (_nextLine != null)
                    return _nextLine;

                if (_reader.EndOfStream)
                    return null;

                _nextLine = _reader.ReadLine();
                return _nextLine;
            }
        }
    }
}
