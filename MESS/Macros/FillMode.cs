
namespace MESS.Macros
{
    enum FillMode
    {
        /// <summary>
        /// Instances are created at random positions inside the macro entity.
        /// </summary>
        Random,

        /// <summary>
        /// Instances are created at random grid positions inside the macro entity.
        /// </summary>
        RandomSnappedToGrid,

        /// <summary>
        /// An instance is created at each grid position inside the macro entity.
        /// </summary>
        AllGridPoints,
    }
}
