using MESS.Common;
using MESS.Mathematics.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            get => Properties.GetStringProperty(Attributes.Classname);
            set => Properties[Attributes.Classname] = value;
        }

        public int Flags
        {
            get => Properties.GetIntegerProperty(Attributes.Spawnflags) ?? 0;
            set => Properties[Attributes.Spawnflags] = value.ToString(CultureInfo.InvariantCulture);
        }

        public Vector3D Origin
        {
            get => Properties.GetVector3DProperty(Attributes.Origin) ?? new Vector3D();
            set
            {
                Properties[Attributes.Origin] = FormattableString.Invariant($"{value.X} {value.Y} {value.Z}");
                if (IsPointBased)
                    BoundingBox = new BoundingBox(Origin, Origin);
            }
        }

        public Angles? Angles
        {
            get => Properties.GetAnglesProperty(Attributes.Angles);
            set
            {
                if (value is Angles angles)
                    Properties[Attributes.Angles] = FormattableString.Invariant($"{angles.Pitch} {angles.Yaw} {angles.Roll}");
                else
                    Properties.Remove(Attributes.Angles);
            }
        }

        public double? Scale
        {
            get => Properties.GetNumericProperty(Attributes.Scale);
            set => Properties[Attributes.Scale] = value?.ToString(CultureInfo.InvariantCulture);
        }


        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        private List<Brush> _brushes = new List<Brush>();
        public IReadOnlyList<Brush> Brushes => _brushes;

        public BoundingBox BoundingBox { get; private set; }


        public Entity(IEnumerable<Brush> brushes = null)
        {
            if (brushes != null)
                AddBrushes(brushes);
            else
                BoundingBox = new BoundingBox(Origin, Origin);
        }

        public void AddBrush(Brush brush)
        {
            _brushes.Add(brush);
            BoundingBox = BoundingBox.CombineWith(brush.BoundingBox);
        }

        public void AddBrushes(IEnumerable<Brush> brushes)
        {
            _brushes.AddRange(brushes);
            BoundingBox = BoundingBox.CombineWith(BoundingBox.FromBoundingBoxes(brushes.Select(brush => brush.BoundingBox)));
        }

        public void RemoveBrush(Brush brush)
        {
            if (_brushes.Remove(brush))
            {
                if (Brushes.Count == 0)
                    BoundingBox = new BoundingBox(Origin, Origin);
                else
                    BoundingBox = BoundingBox.FromBoundingBoxes(Brushes.Select(b => b.BoundingBox));
            }
        }


        // TODO: Not sure about having both this and Properties, each with slightly different characteristics...
        // TODO: Remove this (obsolete) in favor of the PropertyExtension methods?? TBD...
        public string this[string propertyName]
        {
            get => Properties.TryGetValue(propertyName, out var value) ? value : null;
            set => Properties[propertyName] = value;
        }
    }
}
