using MESS.Macros;
using MESS.Mapping;
using System.Globalization;
using System.Text;

namespace MESS.Formats.MAP.Trenchbroom
{
    public static class TBUtil
    {
        /// <summary>
        /// TrenchBroom stores groups and layers as func_group entities.
        /// </summary>
        public const string FuncGroup = "func_group";

        public static class TB
        {
            /// <summary>
            /// The type of a func_group is either <see cref="Group"/> or <see cref="Layer"/>.
            /// </summary>
            public const string Type = "_tb_type";

            /// <summary>
            /// Used as a value for <see cref="Type"/>, groups are used to group related objects together.
            /// Also used for the property that stores the numeric ID of the parent group.
            /// </summary>
            public const string Group = "_tb_group";

            /// <summary>
            /// Used as a value for <see cref="Type"/>, layers are used to show or hide related objects, or to exclude them when exporting a map.
            /// Also used for the property that stores the numeric ID of the parent layer.
            /// </summary>
            public const string Layer = "_tb_layer";

            /// <summary>
            /// The numeric ID of a group or layer.
            /// </summary>
            public const string ID = "_tb_id";

            /// <summary>
            /// The name of a group or layer.
            /// </summary>
            public const string Name = "_tb_name";

            /// <summary>
            /// The GUID of a linked group. func_group entities with the same GUID are linked together, but each copy still has its own <see cref="ID"/>.
            /// </summary>
            public const string LinkedGroupID = "_tb_linked_group_id";

            /// <summary>
            /// The transformation of a linked group copy.
            /// </summary>
            public const string Transformation = "_tb_transformation";

            /// <summary>
            /// The position of this layer in the layer list. Lower indexes appear first.
            /// </summary>
            public const string LayerSortIndex = "_tb_layer_sort_index";

            /// <summary>
            /// If true, the properties of this layer cannot be modified.
            /// </summary>
            public const string LayerLocked = "_tb_layer_locked";

            /// <summary>
            /// If true, the contents of this layer are not shown in the editor.
            /// </summary>
            public const string LayerHidden = "_tb_layer_hidden";

            /// <summary>
            /// If true, the contents of this layer will be excluded when the map is exported.
            /// </summary>
            public const string LayerOmitFromExport = "_tb_layer_omit_from_export";

            /// <summary>
            /// A semicolon-separated list of property names.
            /// The values of these properties will not be propagated to other copies of the linked group that this entity is part of.
            /// </summary>
            public const string ProtectedProperties = "_tb_protected_properties";
        }


        /// <summary>
        /// Converts all TB-styled func_group entities to groups and VIS groups.
        /// This function will modify the given map in-place.
        /// </summary>
        public static void ConvertFromTBGroups(Map map)
        {
            // Worldspawn acts as the default layer (VISgroup):
            var defaultLayer = new TBLayer {
                ID = -1,
                Name = "Default Layer",
                IsVisible = map.Properties.GetInteger(TB.LayerHidden) != 1,
                IsLocked = map.Properties.GetInteger(TB.LayerLocked) == 1,
                IsOmittedFromExport = map.Properties.GetInteger(TB.LayerOmitFromExport) == 1,
            };
            defaultLayer.AddObjects(map.WorldGeometry);

            map.Properties.Remove(TB.LayerHidden);
            map.Properties.Remove(TB.LayerLocked);
            map.Properties.Remove(TB.LayerOmitFromExport);

            // Other layers (VISgroups):
            var layers = new Dictionary<int, TBLayer>();
            var layerSortIndexes = new Dictionary<int, int>();
            var layerEntities = map.Entities
                .Where(entity => entity.ClassName == FuncGroup && entity.Properties.GetString(TB.Type) == TB.Layer)
                .ToArray();
            foreach (var layerEntity in layerEntities)
            {
                var layer = new TBLayer {
                    ID = layerEntity.Properties.GetInteger(TB.ID) ?? -1,
                    Name = layerEntity.Properties.GetString(TB.Name) ?? $"Layer #{layers.Count + 1}",
                    IsVisible = layerEntity.Properties.GetInteger(TB.LayerHidden) != 1,
                    IsLocked = layerEntity.Properties.GetInteger(TB.LayerLocked) == 1,
                    IsOmittedFromExport = layerEntity.Properties.GetInteger(TB.LayerOmitFromExport) == 1,
                    // NOTE: No color information.
                };
                layers[layer.ID] = layer;

                map.Entities.Remove(layerEntity);
                if (layerEntity.Brushes.Any())
                {
                    map.AddBrushes(layerEntity.Brushes);
                    layer.AddObjects(layerEntity.Brushes);
                }

                var sortIndex = layerEntity.Properties.GetInteger(TB.LayerSortIndex) ?? -1;
                layerSortIndexes[layer.ID] = sortIndex;
            }

            // Add layers in the correct order:
            map.VisGroups.Add(defaultLayer);
            foreach (var layer in layers.Values.OrderBy(layer => layerSortIndexes[layer.ID]))
                map.VisGroups.Add(layer);


            // Groups:
            var groups = new Dictionary<int, TBGroup>();
            var parenting = new Dictionary<int, int>();
            var groupEntities = map.Entities
                .Where(entity => entity.ClassName == FuncGroup && entity.Properties.GetString(TB.Type) == TB.Group)
                .ToArray();
            foreach (var groupEntity in groupEntities)
            {
                var group = new TBGroup {
                    ID = groupEntity.Properties.GetInteger(TB.ID) ?? -1,
                    Name = groupEntity.Properties.GetString(TB.Name) ?? "",
                    LinkedGroupID = groupEntity.Properties.GetString(TB.LinkedGroupID),
                    LinkedGroupTransformation = groupEntity.Properties.GetString(TB.Transformation),
                    // NOTE: No color information.
                };
                groups[group.ID] = group;

                var parentID = groupEntity.Properties.GetInteger(TB.Group);
                if (parentID != null)
                    parenting[group.ID] = parentID.Value;

                var layerID = groupEntity.Properties.GetInteger(TB.Layer);
                if (layerID != null && layers.TryGetValue(layerID.Value, out var parentLayer))
                    parentLayer.AddObject(group);
                else
                    defaultLayer.AddObject(group);

                // Groups can safely be added in the order that they appear:
                map.Groups.Add(group);
                map.Entities.Remove(groupEntity);
                if (groupEntity.Brushes.Any())
                {
                    map.AddBrushes(groupEntity.Brushes);
                    group.AddObjects(groupEntity.Brushes);
                }
            }

            // Link groups to parent groups:
            foreach (var kv in parenting)
            {
                var group = groups[kv.Key];
                if (groups.TryGetValue(kv.Value, out var parentGroup))
                    parentGroup.AddObject(group);
            }

            // Link other entities to parent groups and layers (VISgroups):
            foreach (var entity in map.Entities)
            {
                var parentID = entity.Properties.GetInteger(TB.Group);
                if (parentID != null && groups.TryGetValue(parentID.Value, out var parentGroup))
                    parentGroup.AddObject(entity);

                var layerID = entity.Properties.GetInteger(TB.Layer);
                if (layerID != null && layers.TryGetValue(layerID.Value, out var parentLayer))
                    parentLayer.AddObject(entity);
                else
                    defaultLayer.AddObject(entity);

                entity.Properties.Remove(TB.Group);
                entity.Properties.Remove(TB.Layer);
            }

            // Finally, give the default layer a unique, positive ID:
            defaultLayer.ID = Math.Max(groups.Max(kv => kv.Key), layers.Max(kv => kv.Key)) + 1;
        }

        /// <summary>
        /// Converts all groups and VIS groups to TB-styled func_group entities.
        /// This function will modify the given map in-place.
        /// </summary>
        public static void ConvertToTBGroups(Map map, TooManyVisGroupsHandling tooManyVisGroupsHandling, Action<MapObject> onVisGroupFailure)
        {
            // Create layer entities:
            var layerEntities = new Dictionary<int, Entity>();
            foreach (var visGroup in map.VisGroups)
            {
                Entity layerEntity;
                if (visGroup.Name == "Default Layer")
                {
                    layerEntity = map.Worldspawn;
                }
                else
                {
                    layerEntity = new Entity { ClassName = FuncGroup };
                    layerEntity.Properties[TB.Type] = TB.Layer;
                    layerEntity.Properties[TB.Name] = visGroup.Name ?? "";
                    layerEntity.Properties[TB.ID] = visGroup.ID.ToString(CultureInfo.InvariantCulture);
                    layerEntity.Properties[TB.LayerSortIndex] = layerEntities.Count.ToString(CultureInfo.InvariantCulture);

                    layerEntities[visGroup.ID] = layerEntity;
                }

                if (!visGroup.IsVisible)
                    layerEntity.Properties[TB.LayerHidden] = "1";

                if (visGroup is TBLayer tbLayer)
                {
                    if (tbLayer.IsLocked)
                        layerEntity.Properties[TB.LayerLocked] = "1";

                    if (tbLayer.IsOmittedFromExport)
                        layerEntity.Properties[TB.LayerOmitFromExport] = "1";
                }
            }


            // Create group entities:
            var groupEntities = new Dictionary<int, Entity>();
            foreach (var group in map.Groups)
            {
                var groupEntity = new Entity { ClassName = FuncGroup };
                groupEntity.Properties[TB.Type] = TB.Group;
                groupEntity.Properties[TB.Name] = $"Group #{group.ID}";
                groupEntity.Properties[TB.ID] = group.ID.ToString(CultureInfo.InvariantCulture);

                if (group is TBGroup tbGroup)
                {
                    groupEntity.Properties[TB.Name] = tbGroup.Name;

                    if (tbGroup.LinkedGroupID != null)
                        groupEntity.Properties[TB.LinkedGroupID] = tbGroup.LinkedGroupID;

                    if (tbGroup.LinkedGroupTransformation != null)
                        groupEntity.Properties[TB.Transformation] = tbGroup.LinkedGroupTransformation;
                }

                groupEntities[group.ID] = groupEntity;
            }

            // Link groups to parent groups and layers:
            foreach (var group in map.Groups)
            {
                if (group.Group != null)
                {
                    // Child groups link to their parent group:
                    groupEntities[group.ID].Properties[TB.Group] = group.Group.ID.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // Top-level groups can be linked to layers:
                    var visGroup = SelectVisGroup(group);
                    if (visGroup != null)
                        groupEntities[group.ID].Properties[TB.Layer] = visGroup.ID.ToString(CultureInfo.InvariantCulture);
                }
            }


            // Link brushes to group and layer entities:
            var movedBrushes = new List<Brush>();
            foreach (var brush in map.WorldGeometry)
            {
                if (brush.Group != null && groupEntities.TryGetValue(brush.Group.ID, out var groupEntity))
                {
                    groupEntity.AddBrush(brush);
                    movedBrushes.Add(brush);
                }
                else
                {
                    var visGroup = DetermineVisGroup(brush);
                    if (visGroup != null && layerEntities.TryGetValue(visGroup.ID, out var layerEntity))
                    {
                        layerEntity.AddBrush(brush);
                        movedBrushes.Add(brush);
                    }
                }
            }

            if (movedBrushes.Any())
                map.RemoveBrushes(movedBrushes);


            // Link entities to group and layer entities:
            foreach (var entity in map.Entities)
            {
                if (entity.Group != null)
                {
                    entity.Properties[TB.Group] = entity.Group.ID.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    var visGroup = DetermineVisGroup(entity);
                    if (visGroup != null)
                    {
                        entity.Properties[TB.Layer] = visGroup.ID.ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (entity is TBEntity tbEntity && tbEntity.ProtectedProperties != null)
                {
                    entity.Properties[TB.ProtectedProperties] = tbEntity.ProtectedProperties;
                    tbEntity.ProtectedProperties = null;
                }
            }


            // Finally, replace groups and visGroups with group and layer entities:
            map.Groups.Clear();
            map.Entities.AddRange(groupEntities.Values);

            map.VisGroups.Clear();
            map.Entities.AddRange(layerEntities.Values);


            VisGroup? DetermineVisGroup(MapObject mapObject)
            {
                switch (map.VisGroupAssignment)
                {
                    case VisGroupAssignment.PerGroup: return SelectVisGroup(mapObject.TopLevelGroup ?? mapObject);
                    case VisGroupAssignment.PerObject: return SelectVisGroup(mapObject);
                    default: throw new NotImplementedException($"Unknown VIS group assignment approach: {map.VisGroupAssignment}.");
                }
            }

            VisGroup? SelectVisGroup(MapObject mapObject)
            {
                switch (mapObject.VisGroups.Count)
                {
                    case 0: return null;
                    case 1: return mapObject.VisGroups[0];
                    default:
                        switch (tooManyVisGroupsHandling)
                        {
                            default:
                            case TooManyVisGroupsHandling.UseFirst: return mapObject.VisGroups.First();
                            case TooManyVisGroupsHandling.UseLast: return mapObject.VisGroups.Last();

                            case TooManyVisGroupsHandling.Fail:
                                onVisGroupFailure(mapObject);
                                return null;
                        }
                }
            }
        }

        /// <summary>
        /// Utility function for saving maps in a TrenchBroom-specific format.
        /// Creates a deep copy of the given map, which includes entities, brushes, groups and VIS groups, but excludes any other information.
        /// </summary>
        public static Map CopyMap(Map map)
        {
            var copy = new Map();
            copy.VisGroupAssignment = map.VisGroupAssignment;
            copy.HasColorInformation = map.HasColorInformation;

            // First copy VIS groups and groups:
            copy.VisGroups.AddRange(map.VisGroups.Select(CopyVisGroup));
            var visGroups = copy.VisGroups.ToDictionary(visGroup => visGroup.ID, visGroup => visGroup);

            copy.Groups.AddRange(map.Groups.Select(CopyGroup));
            var groups = copy.Groups.ToDictionary(group => group.ID, group => group);

            // Link up groups to parents:
            foreach (var group in map.Groups)
                CopyGroupAndVisGroupLinks(group, groups[group.ID]);


            // Then copy everything else:
            copy.Worldspawn = CopyEntity(map.Worldspawn, ignoreGroups: false);

            foreach (var entity in map.Entities)
                copy.Entities.Add(CopyEntity(entity));

            foreach (var entityPath in map.EntityPaths)
                copy.EntityPaths.Add(CopyEntityPath(entityPath));

            return copy;


            VisGroup CopyVisGroup(VisGroup visGroup)
            {
                return new VisGroup {
                    ID = visGroup.ID,
                    Name = visGroup.Name,
                    Color = visGroup.Color,
                    IsVisible = visGroup.IsVisible,
                };
            }

            Group CopyGroup(Group group)
            {
                return new Group {
                    ID = group.ID,
                    IsSelected = group.IsSelected,
                    IsHidden = group.IsHidden,
                };
            }

            Entity CopyEntity(Entity entity, bool ignoreGroups = true)
            {
                var copy = new Entity();
                foreach (var kv in entity.Properties)
                    copy.Properties[kv.Key] = kv.Value;

                if (entity.Brushes.Any())
                    copy.AddBrushes(entity.Brushes.Select(brush => CopyBrush(brush, ignoreGroups)));

                CopyGroupAndVisGroupLinks(entity, copy);
                return copy;
            }

            Brush CopyBrush(Brush brush, bool ignoreGroups = false)
            {
                var copy = new Brush(brush.Faces.Select(CopyFace));

                if (!ignoreGroups)
                    CopyGroupAndVisGroupLinks(brush, copy);

                return copy;
            }

            Face CopyFace(Face face)
            {
                var copy = new Face();
                copy.Vertices.AddRange(face.Vertices);
                copy.PlanePoints = face.PlanePoints.ToArray();
                copy.Plane = face.Plane;

                copy.TextureName = face.TextureName;
                copy.TextureRightAxis = face.TextureRightAxis;
                copy.TextureDownAxis = face.TextureDownAxis;
                copy.TextureShift = face.TextureShift;
                copy.TextureAngle = face.TextureAngle;
                copy.TextureScale = face.TextureScale;
                return copy;
            }

            EntityPath CopyEntityPath(EntityPath entityPath)
            {
                var copy = new EntityPath();
                copy.Name = entityPath.Name;
                copy.ClassName = entityPath.ClassName;
                copy.Type = entityPath.Type;
                copy.Nodes.AddRange(entityPath.Nodes.Select(CopyEntityPathNode));
                copy.Color = entityPath.Color;
                return copy;
            }

            EntityPathNode CopyEntityPathNode(EntityPathNode node)
            {
                var copy = new EntityPathNode();
                copy.Position = node.Position;
                copy.NameOverride = node.NameOverride;

                foreach (var kv in node.Properties)
                    copy.Properties[kv.Key] = kv.Value;

                copy.Color = node.Color;
                copy.IsSelected = node.IsSelected;
                return copy;
            }

            void CopyGroupAndVisGroupLinks(MapObject original, MapObject copy)
            {
                foreach (var visGroup in original.VisGroups)
                    visGroups[visGroup.ID].AddObject(copy);

                if (original.Group != null)
                    groups[original.Group.ID].AddObject(copy);
            }
        }
    }
}
