namespace MESS.Common
{
    public static class Textures
    {
        /// <summary>
        /// Faces covered with this texture will be removed by the compile tools. They will still be part of the collision hulls however.
        /// </summary>
        public const string Null = "NULL";

        /// <summary>
        /// The Quake equivalent of <see cref="Null"/>.
        /// </summary>
        public const string Caulk = "caulk";

        /// <summary>
        /// Brush entities can use a brush covered with this texture to specify their origin. Often used for rotating entities.
        /// </summary>
        public const string Origin = "ORIGIN";

        /// <summary>
        /// Default texture for cordon brushes.
        /// </summary>
        public const string Black = "BLACK";

        /// <summary>
        /// Faces covered with this texture will display the skybox.
        /// </summary>
        public const string Sky = "sky";
    }
}
