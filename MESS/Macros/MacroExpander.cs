using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MESS.Macros
{
    /// <summary>
    /// Handles macro entity expansion.
    /// </summary>
    public class MacroExpander
    {
        /// <summary>
        /// Loads the specified map file, expands any macro entities within, and returns the resulting map.
        /// The given path must be absolute.
        /// </summary>
        public static Map ExpandMacros(string path)
        {
            // TODO: Verify that 'path' is absolute! Either that, or document the behavior for relative paths! (relative to cwd?)

            var expander = new MacroExpander();
            var context = new InstantiationContext(expander.GetMapTemplate(path));
            expander.CreateInstance(context);

            return context.OutputMap;
        }


        private IDictionary<string, Action<InstantiationContext, Entity>> _entityHandlers;
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
        private MapTemplate ResolveTemplate(string mapPath, string templateName, InstantiationContext context)
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
                    .Where(subTemplate => context.EvaluateInterpolatedString(subTemplate.Name) == templateName)
                    .Select(subTemplate => new { SubTemplate = subTemplate, Weight = double.TryParse(context.EvaluateInterpolatedString(subTemplate.SelectionWeightExpression), out var weight) ? weight : 1 })
                    .ToArray();

                // TODO: Check whether this can make randomness too 'unstable' (e.g. a single change in one entity affecting random seeding/behavior of others)!
                var selection = context.GetRandomDouble(0, matchingSubTemplates.Sum(weightedSubtemplate => weightedSubtemplate.Weight));
                foreach (var weightedSubtemplate in matchingSubTemplates)
                {
                    selection -= weightedSubtemplate.Weight;
                    if (selection <= 0f)
                        return weightedSubtemplate.SubTemplate;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a template instance and inserts it into the output map.
        /// Template instantiation involves copying and transforming template content,
        /// evaluating any expressions in entity properties,
        /// and expanding any macro entities inside the template.
        /// </summary>
        private void CreateInstance(InstantiationContext context)
        {
            // Skip conditional contents whose removal condition is true:
            var excludedObjects = new HashSet<object>();
            foreach (var conditionalContent in context.Template.ConditionalContents)
            {
                // TODO: Perhaps more consistent to evaluate this as an interpolated string as well?
                //       We'd then have to parse the result and check whether it's a 'truthy' value...
                var removal = context.EvaluateExpression(conditionalContent.RemovalCondition);
                if (Interpreter.IsTrue(removal))
                    excludedObjects.UnionWith(conditionalContent.Contents);
            }

            foreach (var entity in context.Template.Map.Entities.Where(entity => !excludedObjects.Contains(entity)))
            {
                var entityHandler = GetEntityHandler(entity.ClassName);
                if (entityHandler != null)
                {
                    // Macro entities are expanded:
                    entityHandler(context, entity.Copy(context));
                }
                else
                {
                    // Other entities are copied directly, with expressions in their property keys/values being evaluated:
                    context.OutputMap.Entities.Add(entity.Copy(context));
                }
            }

            foreach (var brush in context.Template.Map.WorldGeometry)
            {
                if (excludedObjects.Contains(brush))
                    continue;

                context.OutputMap.WorldGeometry.Add(brush.Copy(context.Transform));
            }
        }


        private Action<InstantiationContext, Entity> GetEntityHandler(string className)
        {
            if (_entityHandlers == null)
            {
                _entityHandlers = GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(IsValidEntityHandler)
                    .SelectMany(method => method.GetCustomAttributes<EntityHandlerAttribute>().Select(attribute => new { Method = method, EntityClassName = attribute.EntityClassName }))
                    .ToDictionary(me => me.EntityClassName, me => (Action<InstantiationContext, Entity>)me.Method.CreateDelegate(typeof(Action<InstantiationContext, Entity>), this));
                // TODO: This should throw if it detects marked methods with a wrong signature!
            }

            return _entityHandlers.TryGetValue(className, out var entityHandler) ? entityHandler : null;


            bool IsValidEntityHandler(MethodInfo method)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 2)
                    return false;

                return parameters[0].ParameterType == typeof(InstantiationContext) &&
                    parameters[1].ParameterType == typeof(Entity) &&
                    method.ReturnType == typeof(void);
            }
        }

        [EntityHandler(MacroEntity.Insert)]
        private void HandleMacroInsertEntity(InstantiationContext context, Entity insertEntity)
        {
            var template = ResolveTemplate(insertEntity["template_map"], insertEntity["template_name"], context);
            if (template == null)
                throw new Exception("TODO: Unable to resolve template! LOG THIS AND SKIP???");   // TODO: Fail, or ignore? --> better, LOG THIS!!!


            // TODO: Verify that this works correctly even when the macro_insert entity does not originally contain
            //       'angles' and 'scale' properties!

            // Create a child context for this insertion, with a properly adjusted transform:
            var transform = new Transform(
                (float)(insertEntity.Scale ?? 1),
                insertEntity.Angles?.ToMatrix() ?? Matrix3x3.Identity,
                insertEntity.Origin);

            // TODO: Maybe filter out a few entity properties, such as 'classname', 'origin', etc?
            var insertionContext = new InstantiationContext(template, transform, insertEntity.Properties, context);

            CreateInstance(insertionContext);
        }

        [EntityHandler(MacroEntity.Cover)]
        private void HandleMacroCoverEntity(InstantiationContext context, Entity coverEntity)
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
        private void HandleMacroFillEntity(InstantiationContext context, Entity fillEntity)
        {
            // TODO: Very similar to macro_cover, this deals with the volume of brushes. We'll need to split each brush into tetrahedrons first, then determine the volume of each, so we get a weighted list.

            throw new NotImplementedException();
        }

        [EntityHandler(MacroEntity.Brush)]
        private void HandleMacroBrushEntity(InstantiationContext context, Entity brushEntity)
        {
            throw new NotImplementedException();
        }

        [EntityHandler(MacroEntity.Script)]
        private void HandleMacroScriptEntity(InstantiationContext context, Entity scriptEntity)
        {
            throw new NotImplementedException();
        }
    }
}
