using System.Collections.Generic;

namespace MESS.Mapping
{
    /// <summary>
    /// Groups allow a level-designer to quickly select multiple related objects in the editor.
    /// </summary>
    public class Group : MapObject
    {
        public int ID { get; set; }
        public List<MapObject> Objects { get; } = new List<MapObject>();
    }
}
