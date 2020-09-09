
namespace MESS.Macros
{
    public static class MacroEntity
    {
        /// <summary>
        /// <para>"macro_insert"</para>
        /// <para>This point entity is used to instantiate a template at a specific place.</para>
        /// </summary>
        public const string Insert = "macro_insert";

        /// <summary>
        /// <para>"macro_cover"</para>
        /// <para>This brush entity is used to cover certain surfaces with multiple template instances.</para>
        /// </summary>
        public const string Cover = "macro_cover";

        /// <summary>
        /// <para>"macro_fill"</para>
        /// <para>This brush entity is used to fill a certain volume with multiple template instances.</para>
        /// </summary>
        public const string Fill = "macro_fill";


        /// <summary>
        /// <para>"macro_template"</para>
        /// <para>This brush entity is used to mark areas of a map as a template. The template itself is removed, but the contents are made available for insertion by other macro entities.</para>
        /// <para>Only entities and brushes that are fully inside the bounding box of this entity will be part of the template.
        /// Optionally, an origin brush can be added if a custom anchor point is required.
        /// Templates cannot be nested: templates that cover the same area will just end up having the same contents.
        /// If there are multiple templates with the same name, one will be chosen at random each time that template is inserted.
        /// Template names and other properties may contain expressions: these are evaluated each time a template is inserted.</para>
        /// </summary>
        public const string Template = "macro_template";

        /// <summary>
        /// <para>"macro_remove_if"</para>
        /// <para>This brush entity is used to exclude specific content when a template is inserted, if the removal condition is true.</para>
        /// <para>Only entities and brushes that are fully inside the bounding box of this brush will be affected.
        /// Objects that are covered by multiple removal entities will be removed when the removal condition of any of these entities is true.</para>
        /// </summary>
        public const string RemoveIf = "macro_remove_if";


        /// <summary>
        /// <para>"macro_brush"</para>
        /// <para>This brush entity is used to replace a brush with one or more retextured brushes or brush entities.</para>
        /// <para>Only template brushes that are covered with a single texture are taken into account.
        /// The 'NULL' texture can be used to prevent retexturing.</para>
        /// </summary>
        public const string Brush = "macro_brush";

        /// <summary>
        /// <para>"macro_script"</para>
        /// <para>This point entity is used to run a custom script.</para>
        /// <para>TODO: THIS ENTITY IS STILL IN THE PLANNING PHASE!</para>
        /// </summary>
        public const string Script = "macro_script";
    }
}
