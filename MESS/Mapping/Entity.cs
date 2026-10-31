using MESS.Common;
using MLib.Mathematics.Spatial;
using MESS.Util;
using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace MESS.Mapping
{
    /// <summary>
    /// Entities are used to create in-game 'things'. They are either point-based, such as enemies, lights and sounds, or brush-based, such as doors and triggers.
    /// Some entities, such as lights, are also used by the map compile tools.
    /// </summary>
    public class Entity : MapObject
    {
        class EntityProperties : IDictionary<string, string>
        {
            public ICollection<string> Keys => Properties.Keys;
            public ICollection<string> Values => Properties.Values;
            public int Count => Properties.Count;
            public bool IsReadOnly => false;

            private Entity Parent { get; }
            private Dictionary<string, string> Properties { get; }

            public string this[string key]
            {
                get => Properties[key];
                set
                {
                    Properties[key] = value;
                    Parent.OnPropertyChanged(key, value);
                }
            }


            public EntityProperties(Entity parent, IEqualityComparer<string> comparer)
            {
                Parent = parent;
                Properties = new Dictionary<string, string>(comparer);
            }

            public void Add(string key, string value)
            {
                Properties.Add(key, value);
                Parent.OnPropertyChanged(key, value);
            }

            public bool ContainsKey(string key)
                => Properties.ContainsKey(key);

            public bool Remove(string key)
            {
                if (!Properties.Remove(key))
                    return false;

                Parent.OnPropertyRemoved(key);
                return true;
            }

            public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
                => Properties.TryGetValue(key, out value);

            public void Add(KeyValuePair<string, string> item)
            {
                Properties.Add(item.Key, item.Value);
                Parent.OnPropertyChanged(item.Key, item.Value);
            }

            public void Clear()
            {
                Properties.Clear();
                Parent.OnPropertiesCleared();
            }

            public bool Contains(KeyValuePair<string, string> item)
                => Properties.Contains(item);

            public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
                => ((IDictionary<string, string>)Properties).CopyTo(array, arrayIndex);

            public bool Remove(KeyValuePair<string, string> item)
            {
                if (!((IDictionary<string, string>)Properties).Remove(item))
                    return false;

                Parent.OnPropertyRemoved(item.Key);
                return true;
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
                => Properties.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
                => Properties.GetEnumerator();
        }


        public bool IsPointBased => Brushes.Count == 0;

        public string ClassName
        {
            get => Properties.GetString(Attributes.Classname) ?? "";
            set => Properties.SetString(Attributes.Classname, value);
        }

        public int Spawnflags
        {
            get => Properties.GetInteger(Attributes.Spawnflags) ?? 0;
            set => Properties.SetInteger(Attributes.Spawnflags, value);
        }

        public Vector3D Origin
        {
            get => Properties.GetVector3D(Attributes.Origin) ?? new Vector3D();
            set
            {
                Properties.SetVector3D(Attributes.Origin, value);
                if (IsPointBased)
                    BoundingBox = new BoundingBox(Origin, Origin);
            }
        }

        public Angles? Angles
        {
            get => Properties.GetAngles(Attributes.Angles);
            set
            {
                if (value is Angles angles)
                    Properties.SetAngles(Attributes.Angles, angles);
                else
                    Properties.Remove(Attributes.Angles);
            }
        }

        public double? Scale
        {
            get => Properties.GetDouble(Attributes.Scale);
            set
            {
                if (value is double scale)
                    Properties.SetDouble(Attributes.Scale, scale);
                else
                    Properties.Remove(Attributes.Scale);
            }
        }

        public IDictionary<string, string> Properties { get; }

        private List<Brush> _brushes = new();
        public IReadOnlyList<Brush> Brushes => _brushes;

        public BoundingBox BoundingBox { get; private set; }


        // Editor state:
        public bool IsSelected { get; set; }
        public bool IsHidden { get; set; }


        public Entity(IEnumerable<Brush>? brushes = null)
        {
            Properties = new EntityProperties(this, StringEqualityComparer.InvariantIgnoreCase);

            if (brushes != null)
                AddBrushes(brushes);
            else
                BoundingBox = new BoundingBox(Origin, Origin);
        }

        /// <summary>
        /// Creates a copy of this entity that includes editor format-specific data.
        /// Brushes will be copied, but group and VIS group information is excluded.
        /// </summary>
        public virtual Entity PartialCopy()
        {
            var copy = new Entity();
            PartialCopyTo(copy);
            return copy;
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

            var boundingBoxes = new List<BoundingBox>();
            foreach (var brush in brushes)
            {
                _brushes.Add(brush);
                boundingBoxes.Add(brush.BoundingBox);
            }
            if (!boundingBoxes.Any())
                return;

            var brushesBoundingBox = BoundingBox.FromBoundingBoxes(boundingBoxes);
            BoundingBox = wasEmpty ? brushesBoundingBox : BoundingBox.CombineWith(brushesBoundingBox);
        }

        public void RemoveBrush(Brush brush)
        {
            if (_brushes.Remove(brush))
                RecalculateBoundingBox();
        }

        public void RemoveBrushes(IEnumerable<Brush> brushes)
        {
            foreach (var brush in brushes)
                _brushes.Remove(brush);

            RecalculateBoundingBox();
        }


        protected void PartialCopyTo(Entity other)
        {
            other.Color = Color;

            foreach (var kv in Properties)
                other.Properties[kv.Key] = kv.Value;

            other._brushes.AddRange(Brushes.Select(brush => brush.PartialCopy()));
            other.BoundingBox = BoundingBox;

            other.IsSelected = IsSelected;
            other.IsHidden = IsHidden;
        }

        private void RecalculateBoundingBox()
        {
            if (Brushes.Count == 0)
                BoundingBox = new BoundingBox(Origin, Origin);
            else
                BoundingBox = BoundingBox.FromBoundingBoxes(Brushes.Select(b => b.BoundingBox));
        }

        private void OnPropertyChanged(string key, string value)
        {
            if (key == Attributes.Origin && IsPointBased)
                RecalculateBoundingBox();
        }

        private void OnPropertyRemoved(string key)
        {
            if (key == Attributes.Origin && IsPointBased)
                RecalculateBoundingBox();
        }

        private void OnPropertiesCleared()
        {
            if (IsPointBased)
                RecalculateBoundingBox();
        }
    }
}
