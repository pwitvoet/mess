using MESS.Mapping;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Formats
{
    static class MapExtensions
    {
        public static IEnumerable<Entity> GenerateEntities(this EntityPath path)
        {
            // NOTE: This always yields the previous entity, because its target may have been updated by the current entity,
            //       and it may be too late to update an entity after it has already been yielded.

            var index = 0;
            Entity previousEntity = null;
            foreach (var corner in path.Corners)
            {
                var entity = CreateEntityForCorner(corner);

                if (previousEntity != null)
                    yield return previousEntity;

                previousEntity = entity;
                index += 1;
            }

            if (path.Type == PathType.Circular)
            {
                // Point the last corner back at the first:
                previousEntity["target"] = path.Name;
            }
            else if (path.Type == PathType.PingPong)
            {
                // Generate additional corners for the way back (in reverse order, excluding the first and last corner):
                foreach (var corner in path.Corners.Skip(1).Take(path.Corners.Count - 2).Reverse())
                {
                    var entity = CreateEntityForCorner(corner);

                    if (previousEntity != null)
                        yield return previousEntity;

                    previousEntity = entity;
                    index += 1;
                }
                previousEntity["target"] = path.Name;
            }

            if (previousEntity != null)
                yield return previousEntity;

            Entity CreateEntityForCorner(Corner corner)
            {
                var entity = new Entity();
                foreach (var property in corner.Properties)
                    entity[property.Key] = property.Value;

                entity.ClassName = path.ClassName;
                entity.Origin = corner.Position;

                var targetname = corner.NameOverride;
                if (string.IsNullOrEmpty(targetname))
                    targetname = (index == 0) ? path.Name : $"{path.Name}{index:00}";
                entity["targetname"] = targetname;

                if (previousEntity != null)
                    previousEntity["target"] = targetname;

                return entity;
            }
        }
    }
}
