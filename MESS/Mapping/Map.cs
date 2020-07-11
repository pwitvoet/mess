using System.Collections.Generic;

namespace MESS.Mapping
{
    /// <summary>
    /// A map consists of entities (in-game 'things') and brushes (3-dimensional textured shapes).
    /// <para>
    /// The MAP file format only stores entities and brushes, the RMF format also stores additional editor-related data: groups, visibility groups, camera's and paths.
    /// </para>
    /// </summary>
    public class Map
    {
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public List<Brush> WorldGeometry { get; } = new List<Brush>();

        public List<Entity> Entities { get; } = new List<Entity>();


        // RMF format only:
        public List<EntityPath> EntityPaths { get; } = new List<EntityPath>();

        public List<Group> Groups { get; } = new List<Group>();

        public int ActiveCameraIndex { get; set; }
        public List<Camera> Cameras { get; } = new List<Camera>();

        public List<VisGroup> VisGroups { get; } = new List<VisGroup>();
    }
}
