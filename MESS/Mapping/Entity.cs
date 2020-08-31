﻿using MESS.Mathematics.Spatial;
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
            get => Properties.GetStringProperty("classname");
            set => Properties["classname"] = value;
        }

        public int Flags
        {
            get => Properties.GetIntegerProperty("spawnflags") ?? 0;
            set => Properties["spawnflags"] = value.ToString();
        }

        public Vector3D Origin
        {
            get => Properties.GetVector3DProperty("origin") ?? new Vector3D();
            set
            {
                Properties["origin"] = $"{value.X} {value.Y} {value.Z}";
                if (IsPointBased)
                    BoundingBox = new BoundingBox(Origin, Origin);
            }
        }

        public Angles? Angles
        {
            get => Properties.GetAnglesProperty("angles");
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
            get => Properties.GetNumericProperty("scale");
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


        // TODO: Not sure about having both this and Properties, each with slightly different characteristics...
        // TODO: Remove this (obsolete) in favor of the PropertyExtension methods?? TBD...
        public string this[string propertyName]
        {
            get => Properties.TryGetValue(propertyName, out var value) ? value : null;
            set => Properties[propertyName] = value;
        }
    }
}
