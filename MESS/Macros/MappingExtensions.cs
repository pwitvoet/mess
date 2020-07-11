using MESS.Mapping;
using MESS.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Macros
{
    static class MappingExtensions
    {
        /// <summary>
        /// Selects all entities with the specified class name.
        /// </summary>
        public static IEnumerable<Entity> GetEntitiesWithClassName(this Map map, string className) => map.Entities.Where(entity => entity.ClassName == className);
}
