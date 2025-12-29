namespace MESS.Mapping
{
    /// <summary>
    /// Methods for splitting a map into multiple maps, where each resulting map contains a specific VIS group (layer), group or entity from the original map.
    /// Each method yields tuples of a map and the VIS group/group/entity that was used to produce that map.
    /// </summary>
    public static class MapSplitter
    {
        public static IEnumerable<(Map, VisGroup?)> SplitByVisGroup(Map map)
        {
            // Create an output map for each VIS group (layer):
            foreach (var visGroup in map.VisGroups)
            {
                var outputMap = CopyMap(map, GetMapObjectsOfType<Brush>(visGroup.Objects), GetMapObjectsOfType<Entity>(visGroup.Objects));
                yield return (outputMap, visGroup);
            }

            // Create another output map for all content that is not part of any VIS group (if there is such content):
            var otherBrushes = map.WorldGeometry.Where(brush => !GetVisGroups(brush).Any());
            var otherEntities = map.Entities.Where(entity => !GetVisGroups(entity).Any());
            if (otherBrushes.Any() || otherEntities.Any())
            {
                var outputMap = CopyMap(map, otherBrushes, otherEntities);
                yield return (outputMap, null);
            }


            IReadOnlyList<VisGroup> GetVisGroups(MapObject mapObject)
            {
                if (map.VisGroupAssignment == VisGroupAssignment.PerGroup && mapObject.TopLevelGroup != null)
                    return mapObject.TopLevelGroup.VisGroups;
                else
                    return mapObject.VisGroups;
            }
        }

        public static IEnumerable<(Map, Group?)> SplitByGroup(Map map)
        {
            // Create an output map for each top-level group:
            foreach (var group in map.Groups.Where(group => group.Group is null))
            {
                var outputMap = CopyMap(map, GetMapObjectsOfType<Brush>(group.Objects), GetMapObjectsOfType<Entity>(group.Objects));
                yield return (outputMap, group);
            }

            // Create another output map for all content that is not part of any group (if there is such content):
            var otherBrushes = map.WorldGeometry.Where(brush => brush.Group is null);
            var otherEntities = map.Entities.Where(entity => entity.Group is null);
            if (otherBrushes.Any() || otherEntities.Any())
            {
                var outputMap = CopyMap(map, otherBrushes, otherEntities);
                yield return (outputMap, null);
            }
        }

        public static IEnumerable<(Map, Entity)> SplitByEntity(Map map)
        {
            // Create an output map for each entity:
            foreach (var entity in map.Entities)
            {
                var outputMap = CopyMap(map, Array.Empty<Brush>(), new[] { entity });
                yield return (outputMap, entity);
            }

            // And an output map for the special worldspawn entity:
            var worldspawnOutputMap = CopyMap(map, map.WorldGeometry, Array.Empty<Entity>());
            yield return (worldspawnOutputMap, map.Worldspawn);
        }


        /// <summary>
        /// Creates a copy of the given map. The given brushes and entities should belong to the given map.
        /// </summary>
        private static Map CopyMap(Map originalMap, IEnumerable<Brush> originalBrushes, IEnumerable<Entity> originalEntities)
        {
            var outputMap = originalMap.PartialCopy();
            var outputVisGroups = new Dictionary<int, VisGroup>();
            var outputGroups = new Dictionary<int, Group>();

            foreach (var originalBrush in originalBrushes)
            {
                var outputBrush = originalBrush.PartialCopy();
                SetGroupAndVisGroups(originalBrush, outputBrush);
                outputMap.AddBrush(outputBrush);
            }

            foreach (var originalEntity in originalEntities)
            {
                var outputEntity = originalEntity.PartialCopy();
                SetGroupAndVisGroups(originalEntity, outputEntity);
                outputMap.AddEntity(outputEntity);
            }

            return outputMap;


            void SetGroupAndVisGroups(MapObject originalObject, MapObject outputObject)
            {
                foreach (var originalVisGroup in originalObject.VisGroups)
                    GetOutputVisGroup(originalVisGroup).AddObject(outputObject);

                if (originalObject.Group != null)
                    GetOutputGroup(originalObject.Group).AddObject(outputObject);
            }

            VisGroup GetOutputVisGroup(VisGroup originalVisGroup)
            {
                if (outputVisGroups.TryGetValue(originalVisGroup.ID, out var outputVisGroup))
                    return outputVisGroup;

                outputVisGroup = originalVisGroup.PartialCopy();
                outputMap.AddVisGroup(outputVisGroup);

                outputVisGroups[outputVisGroup.ID] = outputVisGroup;
                return outputVisGroup;
            }

            Group GetOutputGroup(Group originalGroup)
            {
                if (outputGroups.TryGetValue(originalGroup.ID, out var outputGroup))
                    return outputGroup;

                outputGroup = originalGroup.PartialCopy();

                foreach (var originalVisGroup in originalGroup.VisGroups)
                    GetOutputVisGroup(originalVisGroup).AddObject(outputGroup);

                if (originalGroup.Group != null)
                    GetOutputGroup(originalGroup.Group).AddObject(outputGroup);
                else
                    outputMap.AddGroup(outputGroup);

                outputGroups[outputGroup.ID] = outputGroup;
                return outputGroup;
            }
        }

        private static IEnumerable<TMapObject> GetMapObjectsOfType<TMapObject>(IEnumerable<MapObject> mapObjects)
            where TMapObject : MapObject
        {
            foreach (var mapObject in mapObjects)
            {
                if (mapObject is TMapObject typedMapObject)
                {
                    yield return typedMapObject;
                }
                else if (mapObject is Mapping.Group group)
                {
                    foreach (var childMapObject in GetMapObjectsOfType<TMapObject>(group.Objects))
                        yield return childMapObject;
                }
            }
        }
    }
}
