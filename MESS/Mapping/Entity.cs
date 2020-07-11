using MESS.Spatial;
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
                var parts = this["origin"]?.Split();
                if (parts == null)
                    return new Vector3D();

                return new Vector3D(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            }
            set
            {
                this["origin"] = $"{value.X} {value.Y} {value.Z}";
                if (IsPointBased)
                    BoundingBox = new BoundingBox(Origin, Origin);
            }
        }

        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public IReadOnlyList<Brush> Brushes { get; }
        public BoundingBox BoundingBox { get; private set; }


        public Entity(IEnumerable<Brush> brushes = null)
        {
            Brushes = brushes?.ToArray() ?? Array.Empty<Brush>();
            BoundingBox = IsPointBased ? new BoundingBox(Origin, Origin) : BoundingBox.FromBoundingBoxes(Brushes.Select(brush => brush.BoundingBox));
        }


        public string this[string propertyName]
        {
            get => Properties.TryGetValue(propertyName, out var value) ? value : null;
            set => Properties[propertyName] = value;
        }
    }
}
