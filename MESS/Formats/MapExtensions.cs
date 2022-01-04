using MESS.Common;
using MESS.Mapping;
using System;
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
            foreach (var node in path.Nodes)
            {
                var entity = CreateEntityForNode(node);

                if (previousEntity != null)
                    yield return previousEntity;

                previousEntity = entity;
                index += 1;
            }

            if (path.Type == PathType.Circular)
            {
                // Point the last node back at the first:
                previousEntity[Attributes.Target] = path.Name;
            }
            else if (path.Type == PathType.PingPong)
            {
                // Generate additional nodes for the way back (in reverse order, excluding the first and last corner):
                foreach (var node in path.Nodes.Skip(1).Take(path.Nodes.Count - 2).Reverse())
                {
                    var entity = CreateEntityForNode(node);

                    if (previousEntity != null)
                        yield return previousEntity;

                    previousEntity = entity;
                    index += 1;
                }
                previousEntity[Attributes.Target] = path.Name;
            }

            if (previousEntity != null)
                yield return previousEntity;

            Entity CreateEntityForNode(EntityPathNode node)
            {
                var entity = new Entity();
                foreach (var property in node.Properties)
                    entity[property.Key] = property.Value;

                entity.ClassName = path.ClassName;
                entity.Origin = node.Position;

                var targetname = node.NameOverride;
                if (string.IsNullOrEmpty(targetname))
                    targetname = (index == 0) ? path.Name : FormattableString.Invariant($"{path.Name}{index:00}");
                entity[Attributes.Targetname] = targetname;

                if (previousEntity != null)
                    previousEntity[Attributes.Target] = targetname;

                return entity;
            }
        }
    }
}
