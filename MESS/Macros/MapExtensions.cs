using MESS.Mapping;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Macros
{
    static class MapExtensions
    {
        public static IEnumerable<Entity> GetEntitiesWithClassName(this Map map, string className) => map.Entities.Where(entity => entity.ClassName == className);
    }
}
