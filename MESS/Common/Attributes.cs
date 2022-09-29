﻿namespace MESS.Common
{
    public static class Attributes
    {
        // Standard Half-Life entity attributes:

        /// <summary>
        /// The type name of an entity.
        /// </summary>
        public const string Classname = "classname";

        /// <summary>
        /// The flags of an entity, combined into a single number, where each bit represents a flag.
        /// </summary>
        public const string Spawnflags = "spawnflags";

        /// <summary>
        /// The position of a point based entity.
        /// </summary>
        public const string Origin = "origin";

        /// <summary>
        /// The orientation of an entity, in 'pitch yaw roll' format. Defaults to `0 0 0`.
        /// </summary>
        public const string Angles = "angles";

        /// <summary>
        /// The scale of an entity. Defaults to 1.
        /// </summary>
        public const string Scale = "scale";

        /// <summary>
        /// The name of an entity.
        /// </summary>
        public const string Targetname = "targetname";

        /// <summary>
        /// The name of the entity (or entities) that will be triggered by an entity.
        /// </summary>
        public const string Target = "target";

        /// <summary>
        /// For path entities: the name of the entity (or entities) that is triggered when a train passes a path entity.
        /// For most other entities: the message that is shown when an entity is triggered.
        /// </summary>
        public const string Message = "message";


        // Instance orientation attributes, used by macro_insert, macro_cover and macro_fill:

        /// <summary>
        /// The scale for the geometry (brushes) of an instance. If empty, the <see cref="Scale"/> or <see cref="InstanceScale"/> attribute is used instead.
        /// This attribute will be evaluated again for each instance.
        /// </summary>
        public const string InstanceGeometryScale = "instance_geometry_scale";

        /// <summary>
        /// The offset for an instance. This allows repositioning instances with scripting.
        /// This attribute will be evaluated again for each instance.
        /// </summary>
        public const string InstanceOffset = "instance_offset";

        /// <summary>
        /// How a multi-inserting macro entity will orient instances.
        /// This attribute will be evaluated again for each instance.
        /// </summary>
        public const string InstanceOrientation = "instance_orientation";


        // Template-referencing attributes, used by macro_insert, macro_cover, macro_fill and macro_brush:

        /// <summary>
        /// The path of a map file that will be used as template. Paths can be absolute or relative.
        /// Supports .map, .rmf and .jmf files (the extension must be included).
        /// Multi-inserting macro entities will evaluate this again for each instance.
        /// </summary>
        public const string TemplateMap = "template_map";

        /// <summary>
        /// The name of a macro_template entity in the current map. This setting is ignored if the template_map attribute is not empty.
        /// Multi-inserting macro entities will evaluate this again for each instance.
        /// </summary>
        public const string TemplateName = "template_name";


        // macro_insert attributes that are only evaluated once:

        /// <summary>
        /// The number of instances to create. Defaults to 1.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string InstanceCount = "instance_count";


        // macro_cover and macro_fill attributes that are only evaluated once:

        /// <summary>
        /// The maximum number of instances that a multi-inserting macro entity will create.
        /// Values between 0 and 1 (excluding 1 itself) are used as coverage percentage.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string MaxInstances = "max_instances";

        /// <summary>
        /// The amount of space that a multi-inserting macro entity will leave empty around the center of each instance.
        /// Can be used to prevent instances from overlapping each other.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string Radius = "radius";

        /// <summary>
        /// The initialization value for 'random' numbers generated by a macro entity.
        /// This affects the position of instances in multi-inserting macro entities, among other things.
        /// The same seed always produces the same sequence of semi-random numbers.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string RandomSeed = "random_seed";

        /// <summary>
        /// What to do with the brushwork of a macro_cover. See <see cref="Macros.CoverBrushBehavior"/>.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string BrushBehavior = "brush_behavior";

        /// <summary>
        /// How a macro_fill entity will determine instance positions. See <see cref="Macros.FillMode"/>.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string FillMode = "fill_mode";

        /// <summary>
        /// The orientation of the grid that a macro_fill entity uses in <see cref="Macros.FillMode.RandomSnappedToGrid"/> and <see cref="Macros.FillMode.AllGridPoints"/> mode.
        /// See <see cref="Macros.Orientation"/>.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string GridOrientation = "grid_orientation";

        /// <summary>
        /// The granularity of the grid that a macro_fill snaps instances to.
        /// In <see cref="Macros.FillMode.RandomSnappedToGrid"/> mode, setting an axis to 0 will disable snapping along that axis.
        /// In <see cref="Macros.FillMode.AllGridPoints"/> mode, each axis has a minimum granularity of 1.
        /// This attribute will only be evaluated once.
        /// </summary>
        public const string GridGranularity = "grid_granularity";


        // macro_cover and macro_fill-specific attributes that are evaluated again for each instance:

        /// <summary>
        /// The orientation of an instance created by a multi-inserting macro entity.
        /// This attribute will be evaluated again for each instance.
        /// </summary>
        public const string InstanceScale = "instance_scale";

        /// <summary>
        /// The scale of an instance created by a multi-inserting macro entity.
        /// This attribute will be evaluated again for each instance.
        /// </summary>
        public const string InstanceAngles = "instance_angles";


        // macro_template attributes:

        /// <summary>
        /// The origin of a macro_template entity, relative to its bounding box.
        /// </summary>
        public const string Anchor = "anchor";

        /// <summary>
        /// The likelihood that a template is selected compared to other templates with the same name.
        /// If this is set to 0 then the template will not be chosen at all, even if there are no other templates with the same name.
        /// This attribute is evaluated each time a template is selected.
        /// </summary>
        public const string SelectionWeight = "selection_weight";


        // macro_remove_if attributes:

        /// <summary>
        /// The removal condition of a macro_remove_if entity.
        /// A condition that evaluates to 'none' (empty) or 0 will prevent removal.
        /// </summary>
        public const string Condition = "condition";
    }
}
