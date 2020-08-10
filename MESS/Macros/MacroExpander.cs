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

            // TODO: Map properties are currently not evaluated -- but it may be useful (and consistent!) to do so!

            var expander = new MacroExpander();
            var mainTemplate = expander.GetMapTemplate(path);
            var context = new InstantiationContext(mainTemplate, insertionEntityProperties: mainTemplate.Map.Properties);
            expander.CreateInstance(context);

            return context.OutputMap;
        }


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

            // Copy entities:
            foreach (var entity in context.Template.Map.Entities)
            {
                if (excludedObjects.Contains(entity))
                    continue;

                HandleEntity(context, entity);
            }

            // Copy brushes:
            foreach (var brush in context.Template.Map.WorldGeometry)
            {
                if (excludedObjects.Contains(brush))
                    continue;

                context.OutputMap.WorldGeometry.Add(brush.Copy(context.Transform));
            }
        }


        private void HandleEntity(InstantiationContext context, Entity entity)
        {
            switch (entity.ClassName)
            {
                case MacroEntity.Insert:
                    // TODO: Insert 'angles' and 'scale' properties here if the entity doesn't contain them,
                    //       to ensure that transformation always works correctly?
                    HandleMacroInsertEntity(context, entity.Copy(context));
                    break;

                case MacroEntity.Cover:
                    // TODO: 'spawnflags' won't be updated here! (however, macro_cover doesn't have any flags, so...)
                    HandleMacroCoverEntity(context, entity.Copy(context, evaluateExpressions: false));
                    break;

                case MacroEntity.Fill:
                    // TODO: 'spawnflags' won't be updated here! (however, macro_fill doesn't have any flags, so...)
                    HandleMacroFillEntity(context, entity.Copy(context, evaluateExpressions: false));
                    break;

                //case MacroEntity.Brush:
                //case MacroEntity.Script:

                default:
                    // Other entities are copied directly, with expressions in their property keys/values being evaluated:
                    context.OutputMap.Entities.Add(entity.Copy(context));
                    break;
            }
        }

        private void HandleMacroInsertEntity(InstantiationContext context, Entity insertEntity)
        {
            // Resolve the template:
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

        private void HandleMacroCoverEntity(InstantiationContext context, Entity coverEntity)
        {
            // Triangulate all non-NULL faces, creating a list of triangles weighted by surface area:
            var candidateFaces = coverEntity.Brushes
                .SelectMany(brush => brush.Faces)
                .Where(face => face.TextureName.ToUpper() != "NULL")
                .SelectMany(face => face.GetTriangleFan().Select(triangle => new { Triangle = triangle, Area = triangle.GetSurfaceArea() }))
                .ToArray();
            var totalArea = candidateFaces.Sum(candidate => candidate.Area);


            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, coverEntity, "max_instances", "radius", "random_seed");

            var maxInstances = (int)(coverEntity.GetNumericProperty("max_instances") ?? 0);
            var radius = coverEntity.GetNumericProperty("radius") ?? 0;
            var randomSeed = (int)(coverEntity.GetNumericProperty("random_seed") ?? 0);
            var random = new Random(randomSeed);    // TODO: Alternately, pick a random seed from our context!!! (and always pick that!)


            // TODO: If maxInstances is 0 (or lower!), then pick a reasonably number based on fill entity volume and the specified radius!
            // TODO: Also decide what to do if radius is 0 or lower!
            for (int i = 0; i < maxInstances; i++)
            {
                var triangle = TakeFromWeightedList(candidateFaces, random.NextDouble() * totalArea, candidate => candidate.Area).Triangle;
                var insertionPoint = triangle.GetRandomPoint(random);
                // TODO: Check whether this point is far away enough from other instances!

                var template = ResolveTemplate(
                    context.EvaluateInterpolatedString(coverEntity["template_map"]),
                    context.EvaluateInterpolatedString(coverEntity["template_name"]),
                    context);
                if (template == null)
                    continue;   // TODO: LOG THIS!

                // TODO: Determine angles and scale, based on the macro_cover entity settings!
                var transform = new Transform(
                    1,
                    Matrix3x3.Identity, // TODO: world-aligned, face-aligned or texture-plane-aligned! And then take the angles property into account as well!
                    insertionPoint);

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = coverEntity.Properties.ToDictionary(
                    kv => context.EvaluateInterpolatedString(kv.Key),
                    kv => context.EvaluateInterpolatedString(kv.Value));

                var insertionContext = new InstantiationContext(template, transform, evaluatedProperties, context);
                CreateInstance(insertionContext);
            }
        }

        private void HandleMacroFillEntity(InstantiationContext context, Entity fillEntity)
        {
            // Split all brushes into tetrahedrons, creating a list of simplexes weighted by volume:
            var candidateVolumes = fillEntity.Brushes
                .SelectMany(brush => brush.GetTetrahedrons())
                .Select(tetrahedron => new { Tetrahedron = tetrahedron, Volume = tetrahedron.GetVolume() })
                .ToArray();
            var totalVolume = candidateVolumes.Sum(candidate => candidate.Volume);


            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, fillEntity, "max_instances", "radius", "random_seed", "grid_snapping", "grid_offset");

            var maxInstances = (int)(fillEntity.GetNumericProperty("max_instances") ?? 0);
            var radius = fillEntity.GetNumericProperty("radius") ?? 0;
            var randomSeed = (int)(fillEntity.GetNumericProperty("random_seed") ?? 0);
            var random = new Random(randomSeed);    // TODO: Alternately, pick a random seed from our context!!! (and always pick that!)

            // TODO: Grid snapping! (snap to nearest multiple of specified grid, adjusted by offset)
            //       Skip the current insert if it's now outside the fill-entity!
            // TODO: If any component of the grid snapping vector is set to 0, then do not snap along that axis!
            // TODO: Snap to grid in world-space, or in fill-entity space (which may be rotated when it's part of a template)?
            var gridSnapping = fillEntity.GetNumericArrayProperty("grid_snapping") ?? new double[] { 1, 1, 1 };
            var gridOffset = fillEntity.GetNumericArrayProperty("grid_offset") ?? new double[] { 0, 0, 0 };
            // TODO: Angle settings for instances!


            // TODO: If maxInstances is 0 (or lower!), then pick a reasonably number based on fill entity volume and the specified radius!
            // TODO: Also decide what to do if radius is 0 or lower!
            for (int i = 0; i < maxInstances; i++)
            {
                var tetrahedron = TakeFromWeightedList(candidateVolumes, random.NextDouble() * totalVolume, candidate => candidate.Volume).Tetrahedron;
                var insertionPoint = tetrahedron.GetRandomPoint(random);
                // TODO: Check whether this point is far away enough from other instances!

                var template = ResolveTemplate(
                    context.EvaluateInterpolatedString(fillEntity["template_map"]),
                    context.EvaluateInterpolatedString(fillEntity["template_name"]),
                    context);
                if (template == null)
                    continue;   // TODO: LOG THIS!!!


                // TODO: Determine angles and scale, based on the macro_fill entity settings!
                var transform = new Transform(
                    1,
                    Matrix3x3.Identity, // TODO: world-aligned or fill-entity aligned!
                    insertionPoint);

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = fillEntity.Properties.ToDictionary(
                    kv => context.EvaluateInterpolatedString(kv.Key),
                    kv => context.EvaluateInterpolatedString(kv.Value));

                var insertionContext = new InstantiationContext(template, transform, evaluatedProperties, context);
                CreateInstance(insertionContext);
            }
        }


        private static void EvaluateProperties(InstantiationContext context, Entity entity, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (entity.Properties.TryGetValue(name, out var value))
                    entity.Properties[name] = context.EvaluateInterpolatedString(value);
            }
        }

        private static TElement TakeFromWeightedList<TElement>(IEnumerable<TElement> elements, double selection, Func<TElement, double> getWeight)
        {
            var lastElement = default(TElement);
            foreach (var element in elements)
            {
                selection -= getWeight(element);
                if (selection <= 0)
                    return element;

                lastElement = element;
            }
            return lastElement;
        }
    }
}
