using MESS.Macros;
using MESS.Mathematics.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Mapping
{
    /// <summary>
    /// Entities are used to create in-game 'things'. They are either point-based, such as enemies, lights and sounds, or brush-based, such as doors and triggers.
    /// Some entities, such as lights, are also used by the map compile tools.
    /// </summary>
    public class Entity : MapObject
    {
        public bool IsPointBased => Brushes.Count == 0;


        public string ClassName
        {
            get => this["classname"];
            set => this["classname"] = value;
        }

        public int Flags
        {
            get => int.TryParse(this["spawnflags"], out var flags) ? flags : 0;
            set => this["spawnflags"] = value.ToString();
        }

        public Vector3D Origin
        {
            get
            {
                if (GetNumericArrayProperty("origin") is double[] array && array.Length == 3)
                    return new Vector3D((float)array[0], (float)array[1], (float)array[2]);

                return new Vector3D();
            }
            set
            {
                Properties["origin"] = $"{value.X} {value.Y} {value.Z}";
                if (IsPointBased)
                    BoundingBox = new BoundingBox(Origin, Origin);
            }
        }

        public Angles? Angles
        {
            get
            {
                // NOTE: Angles uses the order in which rotations are applied:
                if (GetNumericArrayProperty("angles") is double[] array && array.Length == 3)
                    return new Angles((float)array[2], (float)array[0], (float)array[1]);

                return null;
            }
            set
            {
                if (value is Angles angles)
                    Properties["angles"] = $"{angles.Pitch} {angles.Yaw} {angles.Roll}";
                else
                    Properties.Remove("angles");
            }
        }

        public double? Scale
        {
            get => GetNumericProperty("scale") is double scale ? scale : (double?)null;
            set => Properties["scale"] = value.ToString();
        }


        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public IReadOnlyList<Brush> Brushes { get; }
        public BoundingBox BoundingBox { get; private set; }


        public Entity(IEnumerable<Brush> brushes = null)
        {
            Brushes = brushes?.ToArray() ?? Array.Empty<Brush>();
            BoundingBox = IsPointBased ? new BoundingBox(Origin, Origin) : BoundingBox.FromBoundingBoxes(Brushes.Select(brush => brush.BoundingBox));
        }


        // TODO: Use these instead of the indexer!
        public double? GetNumericProperty(string propertyName)
        {
            if (Properties.TryGetValue(propertyName, out var stringValue) &&
                double.TryParse(stringValue, out var value))
                return value;

            return null;
        }

        public double[] GetNumericArrayProperty(string propertyName)
        {
            if (Properties.TryGetValue(propertyName, out var stringValue) &&
                TryParseVector(stringValue, out var array))
                return array;

            return null;
        }

        public string GetStringProperty(string propertyName)
            => Properties.TryGetValue(propertyName, out var value) ? value : null;


        // TODO: Not sure about having both this and Properties, each with slightly different characteristics...
        // TODO: Remove this (obsolete) in favor of the above methods?? TBD...
        public string this[string propertyName]
        {
            get => Properties.TryGetValue(propertyName, out var value) ? value : null;
            set => Properties[propertyName] = value;
        }


        public static object ParseProperty(string value)
        {
            if (value == null)
                return null;

            if (double.TryParse(value, out var number))
                return number;

            if (TryParseVector(value, out var vector))
                return vector;

            return value;
        }

        private static bool TryParseVector(string value, out double[] vector)
        {
            if (value == null)
            {
                vector = null;
                return false;
            }

            var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            vector = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!double.TryParse(parts[i], out var number))
                {
                    vector = null;
                    return false;
                }

                vector[i] = number;
            }
            return true;
        }
    }
}
