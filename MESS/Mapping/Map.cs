using MESS.Mathematics.Spatial;
using MESS.Util;

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
        public IReadOnlyList<Entity> Entities => _entities;

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
        public IReadOnlyList<Group> Groups => _groups;

        /// <summary>
        /// A list of all visibility groups in this map. Visibility groups are used in level editors to quickly hide or show related objects.
        /// </summary>
        public IReadOnlyList<VisGroup> VisGroups => _visGroups;

        /// <summary>
        /// A list of cameras. Cameras are only used in level editors.
        /// </summary>
        public List<Camera> Cameras { get; } = new();
        public int? ActiveCameraIndex { get; set; }


        private List<Entity> _entities = new();
        private List<Group> _groups = new();
        private List<VisGroup> _visGroups = new();


        public void AddBrush(Brush brush) => Worldspawn.AddBrush(brush);

        public void AddBrushes(IEnumerable<Brush> brushes) => Worldspawn.AddBrushes(brushes);

        public void RemoveBrush(Brush brush)
        {
            Worldspawn.RemoveBrush(brush);
            brush.RemoveFromGroupAndVisGroups();
        }

        public void RemoveBrushes(IEnumerable<Brush> brushes)
        {
            brushes = brushes.GetSafeEnumerable();

            Worldspawn.RemoveBrushes(brushes);

            foreach (var brush in brushes)
                brush.RemoveFromGroupAndVisGroups();
        }

        public void AddEntity(Entity entity) => _entities.Add(entity);

        public void AddEntities(IEnumerable<Entity> entities) => _entities.AddRange(entities);

        public void RemoveEntity(Entity entity)
        {
            if (!_entities.Remove(entity))
                return;

            entity.RemoveFromGroupAndVisGroups();
        }

        public void RemoveEntities(IEnumerable<Entity> entities)
        {
            entities = entities.GetSafeEnumerable();

            foreach (var entity in entities)
            {
                if (!_entities.Remove(entity))
                    continue;

                entity.RemoveFromGroupAndVisGroups();
            }
        }

        public void MoveEntity(Entity entity, int newIndex)
        {
            if (!_entities.Remove(entity))
                return;

            _entities.Insert(newIndex, entity);
        }


        public void AddGroup(Group group) => _groups.Add(group);

        /// <summary>
        /// Removes the given group from this map. If <paramref name="removeContent"/> is true,
        /// all objects that belong to the group will also be removed from the map.
        /// </summary>
        public void RemoveGroup(Group group, bool removeContent = false)
        {
            if (!_groups.Remove(group))
                return;

            if (removeContent)
            {
                // Remove group content from map:
                foreach (var mapObject in group.Objects.ToArray())
                {
                    switch (mapObject)
                    {
                        case Group childGroup: RemoveGroup(childGroup, removeContent); break;
                        case Entity entity: RemoveEntity(entity); break;
                        case Brush brush: RemoveBrush(brush); break;
                        default: throw new NotImplementedException($"Unknown map object: {mapObject.GetType().Name}.");
                    }
                }
            }
            else
            {
                // Unlink content from group:
                while (group.Objects.Any())
                    group.RemoveObject(group.Objects[0]);
            }
        }

        public void AddVisGroup(VisGroup visGroup) => _visGroups.Add(visGroup);

        /// <summary>
        /// Removes the given VIS group from this map. If <paramref name="removeContent"/> is true,
        /// all objects that are assigned to the VIS group will also be removed from the map.
        /// In that case, the behavior of this method depends on <see cref="VisGroupAssignment"/>.
        /// </summary>
        public void RemoveVisGroup(VisGroup visGroup, bool removeContent = false)
        {
            if (!_visGroups.Remove(visGroup))
                return;

            if (removeContent)
            {
                foreach (var mapObject in visGroup.Objects.ToArray())
                {
                    // Only remove top level objects if VIS group assignment is per group:
                    var isTopLevelObject = mapObject.Group == null;
                    if (VisGroupAssignment == VisGroupAssignment.PerGroup && !isTopLevelObject)
                        continue;

                    switch (mapObject)
                    {
                        case Group group:
                            if (VisGroupAssignment == VisGroupAssignment.PerGroup)
                                RemoveGroup(group, removeContent);
                            break;

                        case Entity entity: RemoveEntity(entity); break;
                        case Brush brush: RemoveBrush(brush); break;
                        default: throw new NotImplementedException($"Unknown map object: {mapObject.GetType().Name}.");
                    }
                }
            }
            else
            {
                // Unlink content from VIS group:
                while (visGroup.Objects.Any())
                    visGroup.RemoveObject(visGroup.Objects[0]);
            }
        }
    }
}
