
namespace MESS.Macros
{
    enum CoverBrushBehavior
    {
        /// <summary>
        /// Removes the brushes of a macro entity after expansion.
        /// </summary>
        Remove,

        /// <summary>
        /// Turns the brushes of a macro entity into world brushes after expansion.
        /// </summary>
        WorldGeometry,

        /// <summary>
        /// Turns the brushes of a macro entity into a func_detail after expansion.
        /// </summary>
        FuncDetail,

        // TODO: Another mode could be to copy a 'brush template' (most flexible, but requires a template)!
    }
}
