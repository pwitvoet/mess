namespace MESS.Formats.Obj
{
    public enum ObjUpAxis
    {
        Y,
        Z,
    }

    public enum ObjCoordinateSystem
    {
        RightHanded,
        LeftHanded,
    }

    public enum ObjObjectGrouping
    {
        Map,
        Layer,
        Group,
        Entity,
        Brush,
    }

    public class ObjFileSaveSettings : FileSaveSettings
    {
        /// <summary>
        /// The file path of the associated .mtl file, relative to the .obj file path.
        /// The default value, null, means that the .obj filename will be used.
        /// When set to an empty string, no .mtl file will be created.
        /// </summary>
        public string? MtlFilePath { get; set; }

        /// <summary>
        /// This format applies to all map_Kd entries in the .mtl file.
        /// This is useful if textures are in a different directory, or if they're not .png files.
        /// The default format is "{texturename}.png".
        /// Available placeholders are: {texturename}.
        /// </summary>
        public string? TexturePathFormat { get; set; }

        /// <summary>
        /// This determines how the map is divided into objects.
        /// The default, <see cref="ObjObjectGrouping.Brush"/>, creates an object for each brush.
        /// </summary>
        public ObjObjectGrouping ObjectGrouping { get; set; } = ObjObjectGrouping.Brush;

        /// <summary>
        /// This format applies to all object names in the .obj file.
        /// Available placeholders are: {layername}, {layerid}, {groupid}, {entityid}, {entity.&lt;property&gt;} and {brushid},
        /// though not all placeholders are available for all object grouping modes.
        /// </summary>
        public string? ObjectNameFormat { get; set; }

        /// <summary>
        /// Faces that have one of these textures will be omitted from the .obj file.
        /// </summary>
        public HashSet<string> SkipTextures { get; } = new();

        /// <summary>
        /// The convention for .obj files is that the Y axis is up, so that is the default value.
        /// </summary>
        public ObjUpAxis UpAxis { get; set; } = ObjUpAxis.Y;

        /// <summary>
        /// This scale is applied to all geometry. The default is 1.
        /// </summary>
        public float Scale { get; set; } = 1f;


        public ObjFileSaveSettings(FileSaveSettings? settings = null)
            : base(settings)
        {
        }
    }
}
