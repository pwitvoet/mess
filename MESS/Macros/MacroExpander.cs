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
        public static Map ExpandMacros(string path, ExpansionSettings settings, Logger logger)
        {
            // TODO: Verify that 'path' is absolute! Either that, or document the behavior for relative paths! (relative to cwd?)

            // TODO: Map properties are currently not evaluated -- but it may be useful (and consistent!) to do so!

            var expander = new MacroExpander(settings, logger);
            var mainTemplate = expander.GetMapTemplate(path);

            var context = new InstantiationContext(mainTemplate, insertionEntityProperties: mainTemplate.Map.Properties);
            expander.CreateInstance(context);

            return context.OutputMap;
        }


        private ExpansionSettings Settings { get; }
        private Logger Logger { get; }

        private Dictionary<string, MapTemplate> _mapTemplateCache = new Dictionary<string, MapTemplate>();
        private int _instanceCount = 0;


        private MacroExpander(ExpansionSettings settings, Logger logger)
        {
            Settings = settings;
            Logger = logger;
        }


        /// <summary>
        /// Loads the specified map and returns it as a template. Templates are cached, so maps that are requested multiple times only need to be loaded once.
        /// </summary>
        private MapTemplate GetMapTemplate(string path)
        {
            path = Path.GetFullPath(path);
            if (!_mapTemplateCache.TryGetValue(path, out var template))
            {
                Logger.Info($"Loading map template '{path}' from file.");
                template = MapTemplate.Load(path);
                _mapTemplateCache[path] = template;
            }
            else
            {
                Logger.Verbose($"Loading map template '{path}' from cache.");
            }
            return template;
        }

        /// <summary>
        /// Resolves a template by either loading it from a file or by picking a sub-template from the current context.
        /// Logs a warning and returns null if the specified template could not be resolved.
        /// </summary>
        private MapTemplate ResolveTemplate(string mapPath, string templateName, InstantiationContext context)
        {
            if (mapPath != null)
            {
                Logger.Verbose($"Resolving map template '{mapPath}'.");

                // We support both absolute and relative paths. Relative paths are relative to the map that a template is being inserted into.
                if (!Path.IsPathRooted(mapPath))
                    mapPath = Path.Combine(context.CurrentWorkingDirectory, mapPath);

                try
                {
                    // TODO: If no extension is specified, use a certain preferential order (.rmf, .map, ...)? ...
                    return GetMapTemplate(mapPath);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to load map template '{mapPath}'.");
                    return null;
                }
            }
            else if (templateName != null)
            {
                Logger.Verbose($"Resolving sub-template '{templateName}'.");

                // We'll look for sub-templates in the closest parent context whose template has been loaded from a map file.
                // If there are multiple matches, we'll pick one at random. If there are no matches, we'll fall through and return null.
                var matchingSubTemplates = context.SubTemplates
                    .Where(subTemplate => context.EvaluateInterpolatedString(subTemplate.Name) == templateName)
                    .Select(subTemplate => new { SubTemplate = subTemplate, Weight = double.TryParse(context.EvaluateInterpolatedString(subTemplate.SelectionWeightExpression), out var weight) ? weight : 1 })
                    .ToArray();

                if (matchingSubTemplates.Length == 0)
                {
                    Logger.Warning($"No sub-templates found with the name '{templateName}'.");
                    return null;
                }

                Logger.Verbose($"{matchingSubTemplates.Length} sub-templates found with the name '{templateName}'.");

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
            Logger.Verbose($"Creating instance #{context.ID} at {context.Transform}.");

            _instanceCount += 1;
            if (_instanceCount > Settings.InstanceLimit)
                throw new InvalidOperationException("Instance limit exceeded.");

            if (context.RecursionDepth > Settings.RecursionLimit)
                throw new InvalidOperationException("Recursion limit exceeded.");


            // Skip conditional contents whose removal condition is true:
            var excludedObjects = new HashSet<object>();
            foreach (var conditionalContent in context.Template.ConditionalContents)
            {
                // TODO: Perhaps more consistent to evaluate this as an interpolated string as well?
                //       We'd then have to parse the result and check whether it's a 'truthy' value...
                var removal = context.EvaluateExpression(conditionalContent.RemovalCondition);
                if (Interpreter.IsTrue(removal))
                {
                    Logger.Verbose($"Removal condition '{conditionalContent.RemovalCondition}' is true, excluding {conditionalContent.Contents.Count} objects.");
                    excludedObjects.UnionWith(conditionalContent.Contents);
                }
                else
                {
                    Logger.Verbose($"Removal condition '{conditionalContent.RemovalCondition}' is not true, keeping {conditionalContent.Contents.Count} objects.");
                }
            }
            Logger.Verbose($"A total of {excludedObjects.Count} objects will be excluded.");

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
            Logger.Verbose($"Processing a {insertEntity.ClassName} entity for instance #{context.ID}.");

            // Resolve the template:
            var template = ResolveTemplate(insertEntity["template_map"], insertEntity["template_name"], context);
            if (template == null)
                return;

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
            Logger.Verbose($"Processing a {coverEntity.ClassName} entity for instance #{context.ID}.");

            // TODO: Also ignore other 'special' textures?
            // Triangulate all non-NULL faces, creating a list of triangles weighted by surface area:
            var candidateFaces = coverEntity.Brushes
                .SelectMany(brush => brush.Faces)
                .Where(face => face.TextureName.ToUpper() != "NULL")
                .SelectMany(face => face.GetTriangleFan().Select(triangle => new {
                    Triangle = triangle,
                    Area = triangle.GetSurfaceArea(),
                    Face = face }))
                .ToArray();
            var totalArea = candidateFaces.Sum(candidate => candidate.Area);


            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, coverEntity, "max_instances", "radius", "instance_orientation", "random_seed", "brush_behavior");

            var maxInstances = coverEntity.GetIntegerProperty("max_instances") ?? 0;
            var radius = (float)(coverEntity.GetNumericProperty("radius") ?? 0);
            var orientation = (Orientation)(coverEntity.GetIntegerProperty("instance_orientation") ?? 0);
            var randomSeed = coverEntity.GetIntegerProperty("random_seed") ?? 0;
            var brushBehavior = (CoverBrushBehavior)(coverEntity.GetIntegerProperty("brush_behavior") ?? 0);
            var random = new Random(randomSeed);    // TODO: Alternately, pick a random seed from our context!!! (and always pick that!)

            // TODO: If maxInstances is 0 (or lower!), then pick a reasonably number based on fill entity volume and the specified radius!
            // TODO: Also decide what to do if radius is 0 or lower!
            var availableArea = new SphereCollection();
            for (int i = 0; i < maxInstances; i++)
            {
                var selection = TakeFromWeightedList(candidateFaces, random.NextDouble() * totalArea, candidate => candidate.Area);
                var insertionPoint = selection.Triangle.GetRandomPoint(random);
                if (!availableArea.TryInsert(insertionPoint, radius))
                    continue;

                var template = ResolveTemplate(
                    context.EvaluateInterpolatedString(coverEntity["template_map"]),
                    context.EvaluateInterpolatedString(coverEntity["template_name"]),
                    context);
                if (template == null)
                    continue;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = coverEntity.Properties.ToDictionary(
                    kv => context.EvaluateInterpolatedString(kv.Key),
                    kv => context.EvaluateInterpolatedString(kv.Value));

                var transform = GetTransform(insertionPoint, selection.Face, evaluatedProperties);
                var insertionContext = new InstantiationContext(template, transform, evaluatedProperties, context);
                CreateInstance(insertionContext);
            }

            switch (brushBehavior)
            {
                default:
                case CoverBrushBehavior.Remove:
                    break;

                case CoverBrushBehavior.WorldGeometry:
                    foreach (var brush in coverEntity.Brushes)
                        context.OutputMap.WorldGeometry.Add(brush.Copy(new Vector3D()));
                    break;

                case CoverBrushBehavior.FuncDetail:
                    var funcDetail = new Entity(coverEntity.Brushes.Select(brush => brush.Copy(new Vector3D())));
                    funcDetail.ClassName = "func_detail";
                    context.OutputMap.Entities.Add(funcDetail);
                    break;
            }


            Transform GetTransform(Vector3D insertionPoint, Face face, Dictionary<string, string> evaluatedProperties)
            {
                var rotation = Matrix3x3.Identity;  // Global
                switch (orientation)
                {
                    case Orientation.Local:
                    {
                        rotation = context.Transform.Rotation;
                        break;
                    }

                    case Orientation.Face:
                    {
                        // NOTE: This fails if the texture plane is perpendicular to the current surface:
                        // This uses the face's normal to determine what's up, but because a face does not have a direction,
                        // we'll use the texture alignment to determine what's forwards and left:
                        var up = face.Plane.Normal;
                        var left = up.CrossProduct(face.TextureRightAxis).Normalized();
                        var forward = left.CrossProduct(up);
                        rotation = new Matrix3x3(forward, left, up);
                        break;
                    }

                    case Orientation.Texture:
                    {
                        // This uses the texture plane to determine up, forwards and left:
                        var up = face.TextureDownAxis.CrossProduct(face.TextureRightAxis).Normalized();
                        var forward = face.TextureRightAxis.Normalized();
                        var left = up.CrossProduct(forward);
                        rotation = new Matrix3x3(forward, left, up);
                        break;
                    }
                }

                var scale = evaluatedProperties.GetNumericProperty("instance_scale") ?? 1;
                var angles = (evaluatedProperties.GetAnglesProperty("instance_angles") ?? new Angles()).ToMatrix();

                return new Transform((float)scale, rotation * angles, insertionPoint);
            }
        }

        private void HandleMacroFillEntity(InstantiationContext context, Entity fillEntity)
        {
            Logger.Verbose($"Processing a {fillEntity.ClassName} entity for instance #{context.ID}.");

            // Split all brushes into tetrahedrons, creating a list of simplexes weighted by volume:
            var candidateVolumes = fillEntity.Brushes
                .SelectMany(brush => brush.GetTetrahedrons())
                .Select(tetrahedron => new { Tetrahedron = tetrahedron, Volume = tetrahedron.GetVolume() })
                .ToArray();
            var totalVolume = candidateVolumes.Sum(candidate => candidate.Volume);


            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, fillEntity, "max_instances", "radius", "orientation", "random_seed", "grid_snapping", "grid_offset");

            var maxInstances = fillEntity.GetIntegerProperty("max_instances") ?? 0;
            var radius = (float)(fillEntity.GetNumericProperty("radius") ?? 0);
            var orientation = (Orientation)(fillEntity.GetIntegerProperty("orientation") ?? 0); // TODO: Only the first 2 orientations are valid!
            var randomSeed = fillEntity.GetIntegerProperty("random_seed") ?? 0;
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
            var availableArea = new SphereCollection();
            for (int i = 0; i < maxInstances; i++)
            {
                var tetrahedron = TakeFromWeightedList(candidateVolumes, random.NextDouble() * totalVolume, candidate => candidate.Volume).Tetrahedron;
                var insertionPoint = tetrahedron.GetRandomPoint(random);
                // TODO: Snap to nearest grid point (take grid orientation into account!)
                if (!availableArea.TryInsert(insertionPoint, radius))
                    continue;

                var template = ResolveTemplate(
                    context.EvaluateInterpolatedString(fillEntity["template_map"]),
                    context.EvaluateInterpolatedString(fillEntity["template_name"]),
                    context);
                if (template == null)
                    continue;


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
