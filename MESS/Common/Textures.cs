namespace MESS.Common
{
    public static class Textures
    {
        /// <summary>
        /// Faces covered with this texture will be removed by the compile tools. They will still be part of the collision hulls however.
        /// </summary>
        public const string Null = "NULL";

        /// <summary>
        /// Brush entities can use a brush covered with this texture to specify their origin. Often used for rotating entities.
        /// </summary>
        public const string Origin = "ORIGIN";
    }
}
