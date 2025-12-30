namespace MESS.Common
{
    /// <summary>
    /// Placeholders are used in conversion mode when splitting a map into multiple map files, or when creating multiple objects in an .obj file.
    /// They are used to create distinct map or object names.
    /// </summary>
    public static class Placeholders
    {
        public const string TextureName = "texturename";

        public const string MapName = "mapname";
        public const string LayerName = "layername";
        public const string LayerId = "layerid";
        public const string GroupId = "groupid";
        public const string EntityId = "entityid";
        public const string BrushId = "brushid";

        public const string EntityPropertyPrefix = "entity.";
    }
}
