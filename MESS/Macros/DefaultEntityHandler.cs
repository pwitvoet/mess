using MESS.Mapping;

namespace MESS.Macros
{
    /// <summary>
    /// Handles non-macro entities by inserting them into the output map.
    /// </summary>
    class DefaultEntityHandler : IMacroEntityHandler
    {
        public string EntityName => "";


        public void Process(Entity entity, Map map, string workingDirectory, MacroExpander expander) => map.Entities.Add(entity);
    }
}
