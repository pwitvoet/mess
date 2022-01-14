using System.Collections.Generic;

namespace MESS.Mapping
{
    /// <summary>
    /// A map consists of entities (in-game 'things') and brushes (3-dimensional textured shapes).
    /// <para>
    /// The MAP file format only stores entities and brushes, the RMF and JMF formats also store
    /// additional editor-related data: groups, visibility groups, camera's and paths.
    /// </para>
    /// </summary>
    public class Map
    {
        /// <summary>
        /// Map properties. These can also be accessed via the special <see cref="Worldspawn"/> entity.
        /// </summary>
        public Dictionary<string, string> Properties => _worldspawn.Properties;

        /// <summary>
        /// Map world brushes. These can also be accessed via the special <see cref="Worldspawn"/> entity.
        /// </summary>
        public IReadOnlyList<Brush> WorldGeometry => _worldspawn.Brushes;

        /// <summary>
        /// A list of all entities in this map (excluding the special <see cref="Worldspawn"/> entity).
        /// </summary>
        public List<Entity> Entities { get; } = new List<Entity>();


        // RMF/JMF formats only:
        public List<EntityPath> EntityPaths { get; } = new List<EntityPath>();

        public List<Group> Groups { get; } = new List<Group>();

        public int ActiveCameraIndex { get; set; }
        public List<Camera> Cameras { get; } = new List<Camera>();

        public List<VisGroup> VisGroups { get; } = new List<VisGroup>();


        private Entity _worldspawn = new Entity { ClassName = Common.Entities.Worldspawn };


        public void AddBrush(Brush brush) => _worldspawn.AddBrush(brush);

        public void AddBrushes(IEnumerable<Brush> brushes) => _worldspawn.AddBrushes(brushes);

        public void RemoveBrush(Brush brush) => _worldspawn.RemoveBrush(brush);
    }
}
