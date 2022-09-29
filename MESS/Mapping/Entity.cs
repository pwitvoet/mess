using MESS.Common;
using MESS.Mathematics.Spatial;
using MESS.Util;

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
            get => Properties.GetStringProperty(Attributes.Classname) ?? "";
            set => Properties.SetStringProperty(Attributes.Classname, value);
        }

        public int Flags
        {
            get => Properties.GetIntegerProperty(Attributes.Spawnflags) ?? 0;
            set => Properties.SetIntegerProperty(Attributes.Spawnflags, value);
        }

        public Vector3D Origin
        {
            get => Properties.GetVector3DProperty(Attributes.Origin) ?? new Vector3D();
            set
            {
                Properties.SetVector3DProperty(Attributes.Origin, value);
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
                    Properties.SetAnglesProperty(Attributes.Angles, angles);
                else
                    Properties.Remove(Attributes.Angles);
            }
        }

        public double? Scale
        {
            get => Properties.GetNumericProperty(Attributes.Scale);
            set
            {
                if (value is double scale)
                    Properties.SetNumericProperty(Attributes.Scale, scale);
                else
                    Properties.Remove(Attributes.Scale);
            }
        }


        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringEqualityComparer.InvariantIgnoreCase);

        private List<Brush> _brushes = new();
        public IReadOnlyList<Brush> Brushes => _brushes;

        public BoundingBox BoundingBox { get; private set; }


        public Entity(IEnumerable<Brush>? brushes = null)
        {
            if (brushes != null)
                AddBrushes(brushes);
            else
                BoundingBox = new BoundingBox(Origin, Origin);
        }

        public void AddBrush(Brush brush)
        {
            var wasEmpty = _brushes.Count == 0;

            _brushes.Add(brush);
            BoundingBox = wasEmpty ? brush.BoundingBox : BoundingBox.CombineWith(brush.BoundingBox);
        }

        public void AddBrushes(IEnumerable<Brush> brushes)
        {
            var wasEmpty = _brushes.Count == 0;

            _brushes.AddRange(brushes);
            var brushesBoundingBox = BoundingBox.FromBoundingBoxes(brushes.Select(brush => brush.BoundingBox));
            BoundingBox = wasEmpty ? brushesBoundingBox : BoundingBox.CombineWith(brushesBoundingBox);
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
    }
}
