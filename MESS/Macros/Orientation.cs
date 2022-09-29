namespace MESS.Macros
{
    enum Orientation
    {
        /// <summary>
        /// Instance orientations are used as-is.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Instance orientations are relative to the parent instance.
        /// </summary>
        Local = 1,

        /// <summary>
        /// Instance orientations are relative to the current face.
        /// </summary>
        Face = 2,

        /// <summary>
        /// Instance orientations are relative to the texture plane of the current face.
        /// </summary>
        Texture = 3,
    }
}
