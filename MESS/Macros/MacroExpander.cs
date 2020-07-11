using MESS.Mapping;
using MESS.Spatial;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MESS.Macros
{
    /// <summary>
    /// Handles macro entity substitution.
    /// </summary>
    public class MacroExpander
    {
        // TODO: Convenience entry point. NOTE: 'path' must be an absolute path!
        public static Map ExpandMacros(string path)
        {
            var expander = new MacroExpander();
            var template = expander.GetMapTemplate(path);
            return expander.CreateInstance(template, new Vector3D(), new ExpansionContext(template));
        }


        static MacroExpander()
        {
            _entityHandlers = typeof(MacroExpander).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(IsValidEntityHandler)
                .SelectMany(method => method.GetCustomAttributes<EntityHandlerAttribute>().Select(attribute => new { Method = method, EntityClassName = attribute.EntityClassName }))
                .ToDictionary(me => me.EntityClassName, me => me.Method);

            bool IsValidEntityHandler(MethodInfo method)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 3)
                    return false;

                return parameters[0].ParameterType == typeof(Map) && parameters[1].ParameterType == typeof(Entity) && parameters[2].ParameterType == typeof(ExpansionContext);
            }
        }


        private static IDictionary<string, MethodInfo> _entityHandlers;

        private Dictionary<string, MapTemplate> _mapTemplateCache = new Dictionary<string, MapTemplate>();


        /// <summary>
        /// Loads the specified map and returns it as a template. Templates are cached, so maps that are requested multiple times only need to be loaded once.
        /// </summary>
        private MapTemplate GetMapTemplate(string path)
        {
            path = Path.GetFullPath(path);
            if (!_mapTemplateCache.TryGetValue(path, out var template))
            {
                template = MapTemplate.Load(path);
                _mapTemplateCache[path] = template;
            }
            return template;
        }

        /// <summary>
        /// Resolves a template by either loading it from a file or by picking a sub-template from the current context.
        /// </summary>
        private MapTemplate ResolveTemplate(string mapPath, string templateName, ExpansionContext context)
        {
            if (mapPath != null)
            {
                // We support both absolute and relative paths. Relative paths are relative to the map that a template is being inserted into.
                if (!Path.IsPathRooted(mapPath))
                    mapPath = Path.Combine(context.CurrentWorkingDirectory, mapPath);

                // TODO: Add support for wildcard characters (but in filenames only?)!
                // TODO: If no extension is specified, use a certain preferential order (.rmf, .map, ...)? ...
                return GetMapTemplate(mapPath);
            }
            else if (templateName != null)
            {
                // We'll look for sub-templates in the closest parent context whose template has been loaded from a map file.
                // If there are multiple matches, we'll pick one at random. If there are no matches, we'll fall through and return null.
                var matchingSubTemplates = context.SubTemplates
                    .Where(subTemplate => context.Evaluate(subTemplate.Name) == templateName)
                    .Select(subTemplate => new { SubTemplate = subTemplate, Weight = float.TryParse(context.Evaluate(subTemplate.SelectionWeightExpression), out var weight) ? weight : 1 })
                    .ToArray();

                var selection = context.GetRandomFloat(0, matchingSubTemplates.Sum(weightedSubtemplate => weightedSubtemplate.Weight));
                foreach (var weightedSubtemplate in matchingSubTemplates)
                {
                    selection -= weightedSubtemplate.Weight;
                    if (selection <= 0f)
                        return weightedSubtemplate.SubTemplate;
                }
            }

            return null;
        }

        // NOTE: The given context has been created by the entity that is about to insert this template!
        // TODO: 'offset' should become a 'transform' -- and the related Copy methods should be updated accordingly as well!
        private Map CreateInstance(MapTemplate template, Vector3D offset, ExpansionContext context)
        {
            var instance = new Map();

            // Skip conditional contents whose removal condition is true:
            var excludedObjects = new HashSet<object>();
            foreach (var conditionalContent in template.ConditionalContents.Where(conditionalContent => IsTruthy(context.Evaluate(conditionalContent.RemovalCondition))))
                excludedObjects.UnionWith(conditionalContent.Contents);

            foreach (var entity in template.Map.Entities.Where(entity => !excludedObjects.Contains(entity)))
            {
                if (_entityHandlers.TryGetValue(entity.ClassName, out var macroEntityHandler))
                {
                    // Macro entities are expanded:
                    macroEntityHandler.Invoke(this, new object[] { instance, entity.Copy(offset), context });
                }
                else
                {
                    // Other entities are copied directly, with expressions in their property keys/values being evaluated:
                    instance.Entities.Add(entity.CopyWithEvaluation(offset, context));
                }
            }

            foreach (var brush in template.Map.WorldGeometry.Where(brush => !excludedObjects.Contains(brush)))
            {
                instance.WorldGeometry.Add(brush.Copy(offset));
            }

            return instance;
        }


        [EntityHandler(MacroEntity.Insert)]
        private void HandleMacroInsertEntity(Map map, Entity insertEntity, ExpansionContext context)
        {
            var template = ResolveTemplate(insertEntity["template_map"], insertEntity["template_name"], context);
            if (template == null)
                throw new Exception("TODO: Unable to resolve template! LOG THIS AND SKIP???");   // TODO: Fail, or ignore? --> better, LOG THIS!!!


            // Then create a child context for this insertion:
            var insertionContext = new ExpansionContext(template, insertEntity.Properties, context);   // TODO: Can we just pass on all properties here as substitution values, or should we filter out some special ones???
            var instance = CreateInstance(template, insertEntity.Origin, insertionContext);

            // NOTE: No need to copy entities/brushes, because they're already transformed correctly, and we'll throw away 'instance' afterwards anyway:
            foreach (var entity in instance.Entities)
                map.Entities.Add(entity);

            foreach (var brush in instance.WorldGeometry)
                map.WorldGeometry.Add(brush);
        }

        [EntityHandler(MacroEntity.Cover)]
        private void HandleMacroCoverEntity(Map map, Entity coverEntity, ExpansionContext context)
        {
            // TODO: Do the same as for macro_insert, but then repeatedly (including the template resolving, because sub-templates may have dynamic names!)
            //       -- it would be more efficient if we could skip that if we know that there's no dynamicism going on...

            var candidateFaces = coverEntity.Brushes
                .SelectMany(brush => brush.Faces)
                .Where(face => face.TextureName.ToUpper() != "NULL")
                // TODO: Split each face into triangular sections first -- that'll make the rest easier.
                // TODO: Calculate surface area of each face, so we get a weighted list!
                .ToArray();

            // Look at entity settings: max number of insertions, min radius around each insertion, random seed, how to orient insertions (face-aligned or world-aligned), how to rotate insertions, etc.
            // Then run a loop. Each time, resolve the templates again (though we could skip that if there's no dynamicism going on!), create an instance and insert it in the chosen spot,
            // if we managed to find a fitting spot of course (we'll need to remember previous insertions for the radius check).

            throw new NotImplementedException();
        }

        [EntityHandler(MacroEntity.Fill)]
        private void HandleMacroFillEntity(Map map, Entity fillEntity, ExpansionContext context)
        {
            // TODO: Very similar to macro_cover, this deals with the volume of brushes. We'll need to split each brush into tetrahedrons first, then determine the volume of each, so we get a weighted list.

            throw new NotImplementedException();
        }

        [EntityHandler(MacroEntity.Brush)]
        private void HandleMacroBrushEntity(Map map, Entity brushEntity, ExpansionContext context)
        {
            throw new NotImplementedException();
        }

        [EntityHandler(MacroEntity.Script)]
        private void HandleMacroScriptEntity(Map map, Entity scriptEntity, ExpansionContext context)
        {
            throw new NotImplementedException();
        }


        // TODO: Figure out a good, reliable approach to handle boolean expressions!
        private static bool IsTruthy(string value) => !string.IsNullOrEmpty(value) && value != "0";
    }
}
