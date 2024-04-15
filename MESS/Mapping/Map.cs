using MESS.Mathematics.Spatial;

namespace MESS.Mapping
{
    public enum VisGroupAssignment
    {
        /// <summary>
        /// <para>
        /// Only 'top-level' objects can be linked to a VIS group. Objects that are part of a group automatically belong to that group's VIS group.
        /// Objects can only be linked to a single VIS group.
        /// </para>
        /// This approach is used by Hammer and TrenchBroom.
        /// </summary>
        PerGroup,

        /// <summary>
        /// <para>
        /// Entities and brushes can be linked to multiple VIS group, on an individual basis.
        /// Groups are not linked to VIS groups.
        /// </para>
        /// This approach is used by J.A.C.K.
        /// </summary>
        PerObject,
    }


    /// <summary>
    /// A map consists of entities (in-game 'things') and brushes (3-dimensional textured shapes).
    /// <para>
    /// The MAP file format only stores entities and brushes, the RMF and JMF formats also store
    /// additional editor-related data: groups, visibility groups, camera's and paths.
    /// </para>
    /// </summary>
    public class Map
    {
        // Data interpretation (different editors may interpret the same data in a different way, and not all formats can store all data):
        public VisGroupAssignment VisGroupAssignment { get; set; }
        public bool HasColorInformation { get; set; }


        /// <summary>
        /// Map properties. These can also be accessed via the special <see cref="Worldspawn"/> entity.
        /// </summary>
        public Dictionary<string, string> Properties => Worldspawn.Properties;

        /// <summary>
        /// Map world brushes. These can also be accessed via the special <see cref="Worldspawn"/> entity.
        /// </summary>
        public IReadOnlyList<Brush> WorldGeometry => Worldspawn.Brushes;

        /// <summary>
        /// A list of all entities in this map (excluding the special <see cref="Worldspawn"/> entity).
        /// </summary>
        public List<Entity> Entities { get; } = new();

        /// <summary>
        /// A special entity that contains the map properties and world geometry.
        /// </summary>
        public Entity Worldspawn { get; set; } = new Entity { ClassName = Common.Entities.Worldspawn };


        // RMF/JMF formats only:
        /// <summary>
        /// A list of all entity paths. These can be expanded into sets of connected path entities.
        /// </summary>
        public List<EntityPath> EntityPaths { get; } = new();


        // Common editor state:
        /// <summary>
        /// A cordon area can be used to export and compile a specific part of a map.
        /// </summary>
        public BoundingBox? CordonArea { get; set; }

        /// <summary>
        /// A list of all groups in this map. This includes groups that are part of other groups.
        /// </summary>
        public List<Group> Groups { get; } = new();

        /// <summary>
        /// A list of all visibility groups in this map. Visibility groups are used in level editors to quickly hide or show related objects.
        /// </summary>
        public List<VisGroup> VisGroups { get; } = new();

        /// <summary>
        /// A list of cameras. Cameras are only used in level editors.
        /// </summary>
        public List<Camera> Cameras { get; } = new();
        public int? ActiveCameraIndex { get; set; }


        public void AddBrush(Brush brush) => Worldspawn.AddBrush(brush);

        public void AddBrushes(IEnumerable<Brush> brushes) => Worldspawn.AddBrushes(brushes);

        public void RemoveBrush(Brush brush) => Worldspawn.RemoveBrush(brush);

        public void RemoveBrushes(IEnumerable<Brush> brushes) => Worldspawn.RemoveBrushes(brushes);
    }
}
