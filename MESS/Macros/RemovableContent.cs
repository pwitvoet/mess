using System;
using System.Collections.Generic;
using System.Linq;

namespace MESS.Macros
{
    /// <summary>
    /// This specifies which entities and brushes in a <see cref="MapTemplate"/> should be excluded when the template is inserted,
    /// if the removal condition is true.
    /// </summary>
    public class RemovableContent
    {
        public string RemovalCondition { get; }
        public IReadOnlyCollection<object> Contents { get; }

        public RemovableContent(string removalCondition, IEnumerable<object> contents)
        {
            RemovalCondition = removalCondition;
            Contents = contents?.ToArray() ?? Array.Empty<object>();
        }
    }
}
