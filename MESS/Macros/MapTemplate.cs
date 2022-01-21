using MESS.Common;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using MScript;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public static MapTemplate Load(string path, IDictionary<string, object> globals)
        {
            path = NormalizePath(path);

            var map = MapFile.Load(path);
            map.ExpandPaths();

            return FromMap(map, path, globals);
        }

        public static MapTemplate FromMap(Map map, string path, IDictionary<string, object> globals)
        {
            var subTemplates = ExtractSubTemplates(map, globals);
            var conditionalContent = ExtractConditionalContent(map);
            return new MapTemplate(map, path, false, "1", subTemplates, conditionalContent);
        }


        /// <summary>
        /// For map files, this is their absolute path.
        /// For sub-templates (<see cref="MacroEntity.Template"/>), this is their name property (which may contain expressions).
        /// </summary>
        public string Name { get; }
        public Map Map { get; }
        public bool IsSubTemplate { get; }
        public string SelectionWeightExpression { get; }

        public IReadOnlyCollection<MapTemplate> SubTemplates { get; }
        public IReadOnlyCollection<RemovableContent> ConditionalContents { get; }


        public MapTemplate(Map map, string name, bool isSubTemplate, string selectionWeightExpression = "1", IEnumerable<MapTemplate> subTemplates = null, IEnumerable<RemovableContent> conditionalContent = null)
        {
            Name = name;
            Map = map;
            SelectionWeightExpression = selectionWeightExpression;
            IsSubTemplate = isSubTemplate;

            SubTemplates = subTemplates?.ToArray() ?? Array.Empty<MapTemplate>();
            ConditionalContents = conditionalContent?.ToArray() ?? Array.Empty<RemovableContent>();
        }


        private static string NormalizePath(string path) => System.IO.Path.GetFullPath(path);


        /// <summary>
        /// Removes any template areas (<see cref="MacroEntity.Template"/>) and their contents from the given map, returning them as a dictionary.
        /// Template area names do not need to be unique.
        /// </summary>
        private static IEnumerable<MapTemplate> ExtractSubTemplates(Map map, IDictionary<string, object> globals)
        {
            var templateEntities = map.GetEntitiesWithClassName(MacroEntity.Template);
            var objectsMarkedForRemoval = new HashSet<object>(templateEntities);

            var randomSeed = (int)(map.Properties.GetNumericProperty(Attributes.RandomSeed) ?? 0);
            var context = Evaluation.ContextFromProperties(map.Properties, 0, new Random(randomSeed), globals);

            // Create a MapTemplate for each macro_template entity. These 'sub-templates' can only be used within the current map:
            var subTemplates = new List<MapTemplate>();
            foreach (var templateEntity in templateEntities)
            {
                var templateArea = templateEntity.BoundingBox.ExpandBy(0.5f);
                var templateName = templateEntity.GetStringProperty(Attributes.Targetname) ?? "";
                var selectionWeight = templateEntity.GetStringProperty(Attributes.SelectionWeight) ?? "";
                var offset = GetTemplateEntityOrigin(templateEntity, context) * -1;
                var templateMap = new Map();

                // Copy custom properties into the template map properties - these will serve as local variables that will be evaluated whenever the template is instantiated:
                foreach (var property in templateEntity.Properties)
                    templateMap.Properties[property.Key] = property.Value;
                templateMap.Properties.Remove(Attributes.Targetname);
                templateMap.Properties.Remove(Attributes.SelectionWeight);
                templateMap.Properties.Remove(Attributes.Anchor);

                // Include all entities that are fully inside this template area (except for other macro_template entities - nesting is not supported):
                foreach (var entity in map.Entities.Where(entity => entity.ClassName != MacroEntity.Template))
                {
                    if (templateArea.Contains(entity))
                    {
                        templateMap.Entities.Add(entity.Copy(offset));
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
                    case Entity entity: map.Entities.Remove(entity); break;
                    case Brush brush: map.RemoveBrush(brush); break;
                }
                // TODO: They're also still part of groups, vis-groups, etc!
            }

            return subTemplates;
        }

        private static Vector3D GetTemplateEntityOrigin(Entity templateEntity, EvaluationContext context)
        {
            if (!Enum.TryParse<TemplateAreaAnchor>(Evaluation.EvaluateInterpolatedString(templateEntity.GetStringProperty(Attributes.Anchor), context), out var anchor))
                anchor = TemplateAreaAnchor.OriginBrush;

            if (anchor == TemplateAreaAnchor.OriginBrush)
            {
                if (templateEntity.GetOrigin() is Vector3D origin)
                    return origin;
            }

            // NOTE: The bottom anchor point is our default fallback for when there's no origin brush.
            switch (anchor)
            {
                default:
                case TemplateAreaAnchor.Bottom: return new Vector3D(templateEntity.BoundingBox.Center.X, templateEntity.BoundingBox.Center.Y, templateEntity.BoundingBox.Min.Z);
                case TemplateAreaAnchor.Center: return templateEntity.BoundingBox.Center;
                case TemplateAreaAnchor.Top: return new Vector3D(templateEntity.BoundingBox.Center.X, templateEntity.BoundingBox.Center.Y, templateEntity.BoundingBox.Max.Z);
            }
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
                var condition = removeIfEntity.GetStringProperty(Attributes.Condition) ?? "";  // TODO: Validate the expression somehow?
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
            foreach (var removeIfEntity in map.GetEntitiesWithClassName(MacroEntity.RemoveIf).ToArray())
                map.Entities.Remove(removeIfEntity);

            return conditionalContents;
        }
    }
}
