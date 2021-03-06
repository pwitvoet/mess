﻿using MESS.Mapping;
using MESS.Mathematics.Spatial;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MESS.Formats
{
    /// <summary>
    /// The text-based MAP file format only stores entities and brushes.
    /// The special 'worldspawn' entity contains all map properties and world geometry.
    /// </summary>
    public static class MapFormat
    {
        public static Map Load(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var map = new Map();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!line.Trim().StartsWith("{"))
                        continue;

                    var entity = ReadEntity(reader);
                    if (entity == null)
                        break;

                    if (entity.ClassName == "worldspawn")
                    {
                        foreach (var kv in entity.Properties)
                            map.Properties[kv.Key] = kv.Value;

                        map.WorldGeometry.AddRange(entity.Brushes);
                    }
                    else
                    {
                        map.Entities.Add(entity);
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
                worldspawn.ClassName = "worldspawn";
                worldspawn["sounds"] = "1";
                worldspawn["MaxRange"] = "4096";
                worldspawn["mapversion"] = "220";
                foreach (var kv in map.Properties)
                    worldspawn.Properties[kv.Key] = kv.Value;

                WriteEntity(writer, worldspawn);
                foreach (var entity in map.Entities)
                    WriteEntity(writer, entity);

                foreach (var path in map.EntityPaths)
                    WriteEntityPath(writer, path);
            }
        }


        private static Entity ReadEntity(TextReader reader)
        {
            var properties = new Dictionary<string, string>();
            var brushes = new List<Brush>();
            while (true)
            {
                var line = reader.ReadLine().Trim();
                if (line.StartsWith("\""))
                {
                    var parts = line.Split('"');    // NOTE: There is no support for escaping double quotes!
                    properties[parts[1]] = parts[3];
                }
                else if (line.StartsWith("{"))
                {
                    brushes.Add(ReadBrush(reader));
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

        private static Brush ReadBrush(TextReader reader)
        {
            var faces = new List<Face>();
            while (true)
            {
                var line = reader.ReadLine().Trim();
                if (line.StartsWith("("))
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
                    new Vector3D(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])),
                    new Vector3D(float.Parse(parts[6]), float.Parse(parts[7]), float.Parse(parts[8])),
                    new Vector3D(float.Parse(parts[11]), float.Parse(parts[12]), float.Parse(parts[13])),
                },
                TextureName = parts[15],
                TextureRightAxis = new Vector3D(float.Parse(parts[17]), float.Parse(parts[18]), float.Parse(parts[19])),
                TextureDownAxis = new Vector3D(float.Parse(parts[23]), float.Parse(parts[24]), float.Parse(parts[25])),
                TextureShift = new Vector2D(float.Parse(parts[20]), float.Parse(parts[26])),
                TextureAngle = float.Parse(parts[28]),
                TextureScale = new Vector2D(float.Parse(parts[29]), float.Parse(parts[30])),
            };
        }


        private static void WriteEntity(TextWriter writer, Entity entity)
        {
            writer.WriteLine("{");

            foreach (var property in entity.Properties)
                writer.WriteLine($"\"{property.Key}\" \"{property.Value}\"");

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
                writer.Write($"( {point.X} {point.Y} {point.Z} ) ");

            writer.Write(face.TextureName);
            writer.Write(' ');
            writer.Write($"[ {face.TextureRightAxis.X} {face.TextureRightAxis.Y} {face.TextureRightAxis.Z} {face.TextureShift.X} ] ");
            writer.Write($"[ {face.TextureDownAxis.X} {face.TextureDownAxis.Y} {face.TextureDownAxis.Z} {face.TextureShift.Y} ] ");
            writer.WriteLine($"{face.TextureAngle} {face.TextureScale.X} {face.TextureScale.Y} ");
        }

        private static void WriteEntityPath(TextWriter writer, EntityPath entityPath)
        {
            foreach (var entity in entityPath.GenerateEntities())
                WriteEntity(writer, entity);
        }
    }
}
