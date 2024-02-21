using MESS.Common;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Globalization;
using System.Text;

namespace MESS.Formats.MAP
{
    /// <summary>
    /// The text-based MAP file format only stores entities and brushes.
    /// The special 'worldspawn' entity contains all map properties and world geometry.
    /// </summary>
    public static class MapFormat
    {
        public static Map Load(Stream stream)
        {
            using (var context = new ReadingContext(stream))
            {
                var map = new Map();

                var entityNumber = 0;
                while (!context.EndOfStream)
                {
                    try
                    {
                        var line = context.ReadLine();
                        if (line is null)
                            break;

                        if (!line.Trim().StartsWith("{"))
                            continue;

                        var entity = ReadEntity(context);
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
                            map.Entities.Add(entity);
                        }

                        entityNumber += 1;
                    }
                    catch (Exception ex)
                    {
                        ex.Data["EntityNumber"] = entityNumber;
                        ex.Data["LineNumber"] = context.CurrentLineNumber;
                        throw new InvalidDataException($"Failed to parse entity #{map.Entities.Count}.", ex);
                    }
                }
                return map;
            }
        }

        public static void Save(Map map, Stream stream)
        {
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                var worldspawn = new Entity(map.WorldGeometry);
                worldspawn.ClassName = Entities.Worldspawn;
                worldspawn.Properties["sounds"] = "1";
                worldspawn.Properties["MaxRange"] = "4096";
                worldspawn.Properties["mapversion"] = "220";
                foreach (var kv in map.Properties)
                    worldspawn.Properties[kv.Key] = kv.Value;

                WriteEntity(writer, worldspawn);
                foreach (var entity in map.Entities)
                    WriteEntity(writer, entity);

                foreach (var path in map.EntityPaths)
                    WriteEntityPath(writer, path);
            }
        }


        private static Entity ReadEntity(ReadingContext context)
        {
            var properties = new Dictionary<string, string>();
            var brushes = new List<Brush>();
            while (true)
            {
                var line = context.ReadLine()?.Trim();
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

                    properties[parts[1]] = parts[3];
                }
                else if (line.StartsWith("{"))
                {
                    try
                    {
                        brushes.Add(ReadBrush(context));
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

            var entity = new Entity(brushes);
            foreach (var property in properties)
                entity.Properties[property.Key] = property.Value;

            return entity;
        }

        private static Brush ReadBrush(ReadingContext context)
        {
            var faces = new List<Face>();
            while (true)
            {
                var line = context.ReadLine()?.Trim();
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

        private static Face ReadFace(string line)
        {
            var parts = line.Split();
            if (parts.Length < 31)
                throw new InvalidDataException($"Unexpected face format: '{line}'.");

            return new Face {
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


        private static void WriteEntity(TextWriter writer, Entity entity)
        {
            writer.WriteLine("{");

            foreach (var property in entity.Properties)
                writer.WriteLine(FormattableString.Invariant($"\"{property.Key}\" \"{property.Value}\""));

            foreach (var brush in entity.Brushes)
                WriteBrush(writer, brush);

            writer.WriteLine("}");
        }

        private static void WriteBrush(TextWriter writer, Brush brush)
        {
            writer.WriteLine("{");

            foreach (var face in brush.Faces)
                WriteFace(writer, face);

            writer.WriteLine("}");
        }

        private static void WriteFace(TextWriter writer, Face face)
        {
            foreach (var point in face.PlanePoints)
                writer.Write($"( {FormatFloat(point.X)} {FormatFloat(point.Y)} {FormatFloat(point.Z)} ) ");

            writer.Write(face.TextureName);
            writer.Write(" ");
            writer.Write($"[ {FormatFloat(face.TextureRightAxis.X)} {FormatFloat(face.TextureRightAxis.Y)} {FormatFloat(face.TextureRightAxis.Z)} {FormatFloat(face.TextureShift.X)} ] ");
            writer.Write($"[ {FormatFloat(face.TextureDownAxis.X)} {FormatFloat(face.TextureDownAxis.Y)} {FormatFloat(face.TextureDownAxis.Z)} {FormatFloat(face.TextureShift.Y)} ] ");
            writer.WriteLine($"{FormatFloat(face.TextureAngle)} {FormatFloat(face.TextureScale.X)} {FormatFloat(face.TextureScale.Y)} ");
        }

        private static void WriteEntityPath(TextWriter writer, EntityPath entityPath)
        {
            foreach (var entity in entityPath.GenerateEntities())
                WriteEntity(writer, entity);
        }


        private static float ParseFloat(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static string FormatFloat(float f) => f.ToString("r", CultureInfo.InvariantCulture).Replace('E', 'e');


        class ReadingContext : IDisposable
        {
            public int CurrentLineNumber { get; private set; }
            public bool EndOfStream => _reader.EndOfStream;


            private StreamReader _reader;


            public ReadingContext(Stream stream)
            {
                _reader = new StreamReader(stream, Encoding.UTF8);
            }

            public void Dispose() => _reader.Dispose();

            public string? ReadLine()
            {
                var line = _reader.ReadLine();
                if (line != null)
                    CurrentLineNumber += 1;
                return line;
            }
        }
    }
}
