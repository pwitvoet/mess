using MESS.Mapping;

namespace MESS.Macros
{
    /// <summary>
    /// A handler for a specific macro entity type.
    /// </summary>
    interface IMacroEntityHandler
    {
        string EntityName { get; }


        /// <summary>
        /// Expands the given entity into the given map.
        /// This typically inserts brushes and normal entities.
        /// </summary>
        void Process(Entity entity, Map map, string workingDirectory, MacroExpander expander);
    }
}
