﻿using MESS.Mathematics.Spatial;
using System.Collections.Generic;

namespace MESS.Mapping
{
    /// <summary>
    /// Nodes are part of a path, and are used to generate a trail of connected entities.
    /// </summary>
    public class EntityPathNode
    {
        public Vector3D Position { get; set; }
        public int Index { get; set; }
        public string NameOverride { get; set; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
    }
}
