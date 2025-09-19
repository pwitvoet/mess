namespace MESS.Common
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

        /// <summary>
        /// The render mode of a visual entity: normal, color, texture, glow, solid or additive.
        /// </summary>
        public const string Rendermode = "rendermode";

        /// <summary>
        /// The render color of a visual entity. Usage depends on render mode.
        /// </summary>
        public const string RenderColor = "rendercolor";

        /// <summary>
        /// The render amount of a visual entity. Typically used to control opacity.
        /// </summary>
        public const string RenderAmount = "renderamt";

        /// <summary>
        /// The special render effects of a visual entity, such as pulse, strobe, flickering or hologram effects.
        /// </summary>
        public const string RenderFX = "renderfx";

        /// <summary>
        /// The body selection of a model-displaying entity.
        /// </summary>
        public const string Body = "body";

        /// <summary>
        /// The skin selection of a model-displaying entity.
        /// </summary>
        public const string Skin = "skin";

        /// <summary>
        /// The animation sequence of a model-displaying entity.
        /// </summary>
        public const string Sequence = "sequence";

        /// <summary>
        /// The animation framerateof a model or sprite-displaying entity.
        /// </summary>
        public const string Framerate = "framerate";


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
        /// See <see cref="Macros.Orientation"/>.
        /// </summary>
        public const string InstanceOrientation = "instance_orientation";


        // Instance positioning and scaling, used by macro_insert:

        /// <summary>
        /// This attribute determines whether the <see cref="InstanceOffset"/> property is used as an offset or as an absolute position.
        /// See <see cref="Macros.Positioning"/>.
        /// </summary>
        public const string InstancePositioning = "instance_positioning";

        /// <summary>
        /// This attribute determines whether the <see cref="InstanceScale"/> property is used as a relative or as an absolute scale.
        /// See <see cref="Macros.Scaling"/>.
        /// </summary>
        public const string InstanceScaling = "instance_scaling";


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


        // Special instruction attributes:

        /// <summary>
        /// Adding this attribute to a (template) map will block rewrite directives from all .ted files, except for the paths listed in this attribute.
        /// Paths can cover specific files or entire directories, and do not need to contain a .ted extension.
        /// This attribute is removed afterwards.
        /// </summary>
        public const string AllowRewriteRules = "_mess_allow_rewrite_rules";

        /// <summary>
        /// Adding this attribute to a (template) map will block rewrite directives from the .ted file paths listed in this attribute.
        /// Paths can cover specific files or entire directories, and do not need to contain a .ted extension.
        /// This attribute is removed afterwards.
        /// </summary>
        public const string DenyRewriteRules = "_mess_deny_rewrite_rules";

        /// <summary>
        /// Adding this attribute to a normal (non-macro) entity will insert the specified template map (or maps) at the entity's position.
        /// The attribute is then removed from the entity.
        /// </summary>
        public const string AttachedTemplateMap = "_mess_attached_template_map";

        /// <summary>
        /// Adding this attribute to a normal (non-macro) entity will insert the specified template (or templates) at the entity's position.
        /// The attribute is then removed from the entity.
        /// </summary>
        public const string AttachedTemplateName = "_mess_attached_template_name";

        /// <summary>
        /// This set of attributes can be used to enable or disable a specific spawn flag with MScript.
        /// These attributes are removed from the entity after the <see cref="Spawnflags"/> attribute has been updated.
        /// The first spawnflag has number 0, the last has number 31.
        /// </summary>
        public const string SpawnflagN = "_mess_spawnflag{0}";

        /// <summary>
        /// Brush entities with the same merge ID are merged together into a single brush entity. The type and properties
        /// of the merged entity are determined by the first entity that is marked as a master. If no entity is marked as master,
        /// then the type and properties of the first entity with the same merge ID are used.
        /// </summary>
        public const string MergeEntityID = "_mess_merge_entity_id";

        /// <summary>
        /// When merging brush entities, the entity marked as master determines the type and properties of the merged entity.
        /// If multiple entities are marked as master, then the first master wins. If no entities are marked as master,
        /// then the first entity becomes the master.
        /// </summary>
        public const string MergeEntityMaster = "_mess_merge_entity_master";

        /// <summary>
        /// Removes the entity that contains this attribute, if the value of this attribute is true.
        /// An empty value or a 0 are treated as false, any other value is treated as true.
        /// </summary>
        public const string RemoveIf = "_mess_remove_if";

        /// <summary>
        /// Determines in which order entities are processed. Must be an integer, not an expression.
        /// Entities with a higher value are processed before entities with a lower value.
        /// Entities with the same value are processed in order of appearance.
        /// The default value is 0.
        /// </summary>
        public const string InputOrder = "_mess_input_order";

        /// <summary>
        /// Determines the order of entities in the output map. Can be an integer or an expression that evaluates to an integer.
        /// Entities with higher values are moved to the front.
        /// Entities with the same value are left in the same order in which they were created.
        /// The default value is 0.
        /// </summary>
        public const string OutputOrder = "_mess_output_order";


        // Texture adjustment:

        /// <summary>
        /// This attribute can be used to adjust the texture properties of a face. Like other special attributes, it is removed afterwards.
        /// The 'adjust' attribute takes priority over the other single-property texture attributes. It can be used in a few different ways:
        /// <para>
        /// To adjust faces with a specific texture: "_mess_adjust_texture wall1" "{{texture: 'wall2', offset: [0, 0], angle: 90, scale: [1, 1]}}".
        /// This is called a 'named rule'.
        /// </para>
        /// <para>
        /// To adjust the properties of all faces that don't match any named rule: "_mess_adjust_texture" "{{texture: 'default'}}".
        /// This is a 'default rule'. Note that all fields are optional in the MScript object that defines the new face properties.
        /// This rule only replaces the texture, it does not modify the offset, angle or scale.
        /// </para>
        /// <para>
        /// To create multiple named rules with a single key: "_mess_adjust_texture" "{{wall1: {texture: 'wall2'}, floor1: {texture: 'floor2'}, '': {texture: 'default'}}".
        /// Each field produces a separate named rule. The empty string key produces a default rule.
        /// </para>
        /// Besides static values, it's also possible to provide an MScript function that takes a face-info object as argument.
        /// This makes it possible to adjust existing values instead of blindly replacing them.
        /// For example: "_mess_adjust_texture" "{face => face.texture.contains('blue') ? {texture: face.texture.replace('blue', 'red'), offset: face.offset + [16, 0]} : none}".
        /// This only adjust faces whose texture name contains the text 'blue'. It also shifts the texture 16 units to the right, instead of setting the offset to 16,0.
        /// </summary>
        public const string AdjustTexture = "_mess_adjust_texture";

        /// <summary>
        /// Similar to <see cref="AdjustTexture"/>, this attribute only replaces the texture name of a face.
        /// </summary>
        public const string ReplaceTexture = "_mess_replace_texture";

        /// <summary>
        /// Similar to <see cref="AdjustTexture"/>, this attribute only replaces the texture offset of a face.
        /// </summary>
        public const string ShiftTexture = "_mess_shift_texture";

        /// <summary>
        /// Similar to <see cref="AdjustTexture"/>, this attribute only replaces the texture angle of a face.
        /// </summary>
        public const string RotateTexture = "_mess_rotate_texture";

        /// <summary>
        /// Similar to <see cref="AdjustTexture"/>, this attribute only replaces the texture scale of a face.
        /// </summary>
        public const string ScaleTexture = "_mess_scale_texture";
    }
}
