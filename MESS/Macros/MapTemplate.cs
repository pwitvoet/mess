using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using MScript;

namespace MESS.Macros
{
    public enum TemplateAreaAnchor
    {
        Bottom = 0,
        Center = 1,
        Top = 2,
        OriginBrush = 3,
    }


    /// <summary>
    /// A map template holds entities and brushes that can be inserted by <see cref="MacroEntity.Insert"/>, <see cref="MacroEntity.Cover"/> and <see cref="MacroEntity.Fill"/> entities.
    /// Templates may contain sub-templates (<see cref="MacroEntity.Template"/>) and conditional content (<see cref="MacroEntity.RemoveIf"/>).
    /// The template's map properties serve as local variables, which are evaluated whenever the template is instantiated.
    /// </summary>
    public class MapTemplate
    {
        public static MapTemplate FromMap(Map map, string path, EvaluationContext context, ILogger logger)
        {
            var subTemplates = ExtractSubTemplates(map, path, context, logger);
            var conditionalContent = ExtractConditionalContent(map);
            return new MapTemplate(map, path, false, "1", subTemplates, conditionalContent);
        }


        /// <summary>
        /// For map files, this is their absolute path.
        /// For sub-templates (<see cref="MacroEntity.Template"/>), this is their name property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Sub-templates can have multiple names (comma-separated list).
        /// For map files, this is empty.
        /// </summary>
        public HashSet<string> Names { get; } = new();

        public Map Map { get; }
        public bool IsSubTemplate => Parent != null;
        public string SelectionWeightExpression { get; }

        public MapTemplate? Parent { get; private set; }
        public IReadOnlyCollection<MapTemplate> SubTemplates { get; }
        public IReadOnlyCollection<RemovableContent> ConditionalContents { get; }


        public MapTemplate(
            Map map,
            string name,
            bool isSubTemplate,
            string selectionWeightExpression = "1",
            IEnumerable<MapTemplate>? subTemplates = null,
            IEnumerable<RemovableContent>? conditionalContent = null)
        {
            Name = name;
            Map = map;
            SelectionWeightExpression = selectionWeightExpression;

            if (isSubTemplate)
            {
                foreach (var subName in Util.ParseCommaSeparatedList(name))
                    Names.Add(subName);
            }

            SubTemplates = subTemplates?.ToArray() ?? Array.Empty<MapTemplate>();
            ConditionalContents = conditionalContent?.ToArray() ?? Array.Empty<RemovableContent>();

            foreach (var subTemplate in SubTemplates)
                subTemplate.Parent = this;
        }


        /// <summary>
        /// Removes any template areas (<see cref="MacroEntity.Template"/>) and their contents from the given map, returning them as a dictionary.
        /// Template area names do not need to be unique.
        /// </summary>
        private static IEnumerable<MapTemplate> ExtractSubTemplates(Map map, string path, EvaluationContext context, ILogger logger)
        {
            var templateEntities = map.GetEntitiesWithClassName(MacroEntity.Template);
            var objectsMarkedForRemoval = new HashSet<object>(templateEntities);

            var evaluatedMapProperties = map.Properties.EvaluateToMScriptValues(context);
            var randomSeed = evaluatedMapProperties.GetInteger(Attributes.RandomSeed) ?? 0;

            var mapContext = Evaluation.ContextWithBindings(evaluatedMapProperties, 0, 0, 0, new Random(randomSeed), path, logger, context);

            // Create a MapTemplate for each macro_template entity. These 'sub-templates' can only be used within the current map:
            var subTemplates = new List<MapTemplate>();
            foreach (var templateEntity in templateEntities)
            {
                var templateArea = templateEntity.BoundingBox.ExpandBy(0.5f);
                var templateName = templateEntity.Properties.EvaluateToString(Attributes.Targetname, mapContext);
                var selectionWeight = templateEntity.Properties.EvaluateToString(Attributes.SelectionWeight, mapContext);
                if (string.IsNullOrEmpty(selectionWeight))
                    selectionWeight = "1";

                if (!Enum.TryParse<TemplateAreaAnchor>(templateEntity.Properties.EvaluateToString(Attributes.Anchor, context), out var anchor))
                    anchor = TemplateAreaAnchor.Center;

                if (anchor == TemplateAreaAnchor.OriginBrush && templateEntity.GetOrigin() == null)
                    logger.Warning($"Template '{templateName}' in map '{path}' has no origin! Add an origin brush, or use a different anchor point.");

                var offset = -templateEntity.GetAnchorPoint(anchor);
                var templateMap = new Map();

                // Copy custom properties into the template map properties - these will serve as local variables that will be evaluated whenever the template is instantiated:
                foreach (var property in templateEntity.Properties)
                    templateMap.Properties[property.Key] = property.Value;

                templateMap.Properties.Remove(Attributes.Classname);
                templateMap.Properties.Remove(Attributes.Targetname);
                templateMap.Properties.Remove(Attributes.SelectionWeight);
                templateMap.Properties.Remove(Attributes.Anchor);

                // Include all entities that are fully inside this template area (except for other macro_template entities - nesting is not supported):
                foreach (var entity in map.Entities.Where(entity => entity.ClassName != MacroEntity.Template))
                {
                    if (templateArea.Contains(entity))
                    {
                        templateMap.AddEntity(entity.Copy(offset));
                        objectsMarkedForRemoval.Add(entity);
                    }
                }

                // Include all brushes that are fully inside this template area:
                foreach (var brush in map.WorldGeometry)
                {
                    if (templateArea.Contains(brush))
                    {
                        templateMap.AddBrush(brush.Copy(offset));
                        objectsMarkedForRemoval.Add(brush);
                    }
                }


                var conditionalContent = ExtractConditionalContent(templateMap);
                subTemplates.Add(new MapTemplate(templateMap, templateName, true, selectionWeight, conditionalContent: conditionalContent));
            }

            // Now that we've checked all template areas, we can remove the macro_template entities and their contents from the map:
            foreach (var mapObject in objectsMarkedForRemoval)
            {
                switch (mapObject)
                {
                    case Entity entity: map.RemoveEntity(entity); break;
                    case Brush brush: map.RemoveBrush(brush); break;
                }
            }

            return subTemplates;
        }

        /// <summary>
        /// Removes any remove-if areas (<see cref="MacroEntity.RemoveIf"/>), returning a dictionary that maps expressions (removal conditions) to removable contents.
        /// Remove-if areas can overlap each other, so some contents may be referenced by multiple expressions.
        /// </summary>
        private static IEnumerable<RemovableContent> ExtractConditionalContent(Map map)
        {
            var conditionalContents = new List<RemovableContent>();
            foreach (var removeIfEntity in map.GetEntitiesWithClassName(MacroEntity.RemoveIf))
            {
                var removeIfArea = removeIfEntity.BoundingBox.ExpandBy(0.5f);
                var condition = removeIfEntity.Properties.GetString(Attributes.Condition) ?? "";
                var removableContent = new HashSet<object>();

                // Reference all entities that are fully inside this remove-if area (except for other macro_remove_if entities):
                foreach (var entity in map.Entities.Where(entity => entity.ClassName != MacroEntity.RemoveIf))
                {
                    if (removeIfArea.Contains(entity))
                        removableContent.Add(entity);
                }

                // Reference all bushes that are fully inside this remove-if area:
                foreach (var brush in map.WorldGeometry)
                {
                    if (removeIfArea.Contains(brush))
                        removableContent.Add(brush);
                }


                conditionalContents.Add(new RemovableContent(condition, removableContent));
            }

            // At this point we no longer need the macro_remove_if entities:
            map.RemoveEntities(map.GetEntitiesWithClassName(MacroEntity.RemoveIf));

            return conditionalContents;
        }
    }
}
