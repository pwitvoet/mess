using MESS.EntityRewriting;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using MScript.Evaluation;
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

            var context = new InstantiationContext(
                mainTemplate,
                insertionEntityProperties: mainTemplate.Map.Properties,
                workingDirectory: settings.Directory);
            expander.CreateInstance(context);

            return context.OutputMap;
        }


        private ExpansionSettings Settings { get; }
        private Logger Logger { get; }

        private Dictionary<string, MapTemplate> _mapTemplateCache = new Dictionary<string, MapTemplate>();
        private int _instanceCount = 0;

        private RewriteDirective[] _rewriteDirectives = Array.Empty<RewriteDirective>();
        private DirectoryFunctions _directoryFunctions;


        private MacroExpander(ExpansionSettings settings, Logger logger)
        {
            Settings = settings;
            Logger = logger;

            _rewriteDirectives = settings.GameDataPaths?.SelectMany(LoadRewriteDirectives)?.ToArray() ?? Array.Empty<RewriteDirective>();
            _directoryFunctions = new DirectoryFunctions(settings.Directory, Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
        }


        private IEnumerable<RewriteDirective> LoadRewriteDirectives(string fgdPath)
        {
            try
            {
                var path = Path.GetFullPath(fgdPath);
                Logger.Info($"Loading rewrite directives from '{path}'.");

                var rewriteDirectives = RewriteDirectiveParser.ParseRewriteDirectives(path).ToArray();
                Logger.Info($"{rewriteDirectives.Length} rewrite directives loaded.");

                return rewriteDirectives;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load rewrite directives from '{fgdPath}':", ex);
                return Array.Empty<RewriteDirective>();
            }
        }

        private void ApplyRewriteDirectives(Map map, IEnumerable<RewriteDirective> rewriteDirectives)
        {
            Logger.Info($"Applying {rewriteDirectives.Count()} rewrite directives to map.");

            var randomSeed = 0;
            if (map.Properties.TryGetValue("random_seed", out var value) && double.TryParse(value, out var doubleValue))
            {
                randomSeed = (int)doubleValue;
            }
            var random = new Random(randomSeed);

            var directiveLookup = rewriteDirectives.ToLookup(rewriteDirective => rewriteDirective.ClassName);
            for (int entityID = 0; entityID < map.Entities.Count; entityID++)
            {
                var entity = map.Entities[entityID];

                var matchingDirectives = directiveLookup[entity.ClassName];
                foreach (var rewriteDirective in matchingDirectives)
                    ApplyRewriteDirective(entity, rewriteDirective, entityID, random);
            }
        }

        private void ApplyRewriteDirective(Entity entity, RewriteDirective rewriteDirective, int entityID, Random random)
        {
            var context = Evaluation.ContextFromProperties(entity.Properties, entityID, random);
            NativeUtils.RegisterInstanceMethods(context, _directoryFunctions);

            foreach (var ruleGroup in rewriteDirective.RuleGroups)
            {
                if (!ruleGroup.HasCondition || Interpreter.IsTrue(PropertyExtensions.ParseProperty(Evaluation.EvaluateInterpolatedString(ruleGroup.Condition, context))))
                {
                    foreach (var rule in ruleGroup.Rules)
                        ApplyRewriteRule(entity, rule, context);
                }
                else if (ruleGroup.HasCondition)
                {
                    foreach (var rule in ruleGroup.AlternateRules)
                        ApplyRewriteRule(entity, rule, context);
                }
            }
        }

        private void ApplyRewriteRule(Entity entity, RewriteDirective.Rule rule, EvaluationContext context)
        {
            var attributeName = Evaluation.EvaluateInterpolatedString(rule.Attribute, context);
            if (rule.DeleteAttribute)
            {
                entity.Properties.Remove(attributeName);
                context.Bind(attributeName, null);
            }
            else
            {
                var value = Evaluation.EvaluateInterpolatedString(rule.NewValue, context);
                entity.Properties[attributeName] = value;
                context.Bind(attributeName, value);
            }
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

                // NOTE: Entity rewrite directives are applied after entity path expansion, so rewriting will also affect these entities.
                //       Rewriting happens before template detection, so rewriting something to a macro_template entity will work as expected:
                var map = MapFile.Load(path);
                map.ExpandPaths();
                ApplyRewriteDirectives(map, _rewriteDirectives);

                template = MapTemplate.FromMap(map, path);

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
                    Logger.Warning($"Failed to load map template '{mapPath}':", ex);
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
                    .Select(subTemplate => new { SubTemplate = subTemplate, Weight = double.TryParse(context.EvaluateInterpolatedString(subTemplate.SelectionWeightExpression), out var weight) ? weight : 0 })
                    .ToArray();
                if (matchingSubTemplates.Length == 0)
                {
                    Logger.Warning($"No sub-templates found with the name '{templateName}'.");
                    return null;
                }

                matchingSubTemplates = matchingSubTemplates
                    .Where(weightedSubtemplate => weightedSubtemplate.Weight > 0)
                    .ToArray();
                if (matchingSubTemplates.Length == 0)
                {
                    Logger.Warning($"No sub-templates with the name '{templateName}' have a positive selection weight.");
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
            var excludedObjects = GetExcludedObjects(context, Logger);
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


        private void HandleEntity(InstantiationContext context, Entity entity, bool applyTransform = true)
        {
            switch (entity.ClassName)
            {
                case MacroEntity.Insert:
                    // TODO: Insert 'angles' and 'scale' properties here if the entity doesn't contain them,
                    //       to ensure that transformation always works correctly?
                    HandleMacroInsertEntity(context, entity.Copy(context, applyTransform: applyTransform));
                    break;

                case MacroEntity.Cover:
                    // TODO: 'spawnflags' won't be updated here! (however, macro_cover doesn't have any flags, so...)
                    HandleMacroCoverEntity(context, entity.Copy(context, applyTransform: applyTransform, evaluateExpressions: false));
                    break;

                case MacroEntity.Fill:
                    // TODO: 'spawnflags' won't be updated here! (however, macro_fill doesn't have any flags, so...)
                    HandleMacroFillEntity(context, entity.Copy(context, applyTransform: applyTransform, evaluateExpressions: false));
                    break;

                case MacroEntity.Brush:
                    // TODO: 'spawnflags' won't be updated here! (however, macro_brush doesn't have any flags, so...)
                    HandleMacroBrushEntity(context, entity.Copy(context, applyTransform: applyTransform));
                    break;

                //case MacroEntity.Script:

                default:
                    // Other entities are copied directly, with expressions in their property keys/values being evaluated:
                    context.OutputMap.Entities.Add(entity.Copy(context, applyTransform: applyTransform));
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
            if (!candidateFaces.Any())
            {
                Logger.Warning($"{coverEntity.ClassName} has no non-NULL surfaces and will be skipped.");
                return;
            }

            var totalArea = candidateFaces.Sum(candidate => candidate.Area);


            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, coverEntity, "max_instances", "radius", "instance_orientation", "random_seed", "brush_behavior");

            var maxInstances = coverEntity.GetNumericProperty("max_instances") ?? 0.0;
            var radius = (float)(coverEntity.GetNumericProperty("radius") ?? 0);
            var orientation = (Orientation)(coverEntity.GetIntegerProperty("instance_orientation") ?? 0);
            var randomSeed = coverEntity.GetIntegerProperty("random_seed") ?? 0;
            var brushBehavior = (CoverBrushBehavior)(coverEntity.GetIntegerProperty("brush_behavior") ?? 0);


            // Between 0 and 1, maxInstances acts as a 'coverage factor':
            if (maxInstances > 0 && maxInstances < 1)
            {
                var instanceArea = Math.PI * Math.Pow(Math.Max(1.0, radius), 2);;
                maxInstances = Math.Ceiling(maxInstances * (totalArea / instanceArea));
            }

            Logger.Verbose($"Maximum number of instances: {maxInstances}, total surface area: {totalArea}.");

            var random = new Random(randomSeed);
            var availableArea = new SphereCollection();
            for (int i = 0; i < (int)maxInstances; i++)
            {
                var selection = TakeFromWeightedList(candidateFaces, random.NextDouble() * totalArea, candidate => candidate.Area);
                var insertionPoint = selection.Triangle.GetRandomPoint(random);
                if (radius > 0.0 && !availableArea.TryInsert(insertionPoint, radius))
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

        // TODO: Check for invalid settings values (invalid enums, missing or wrong data type, negative numbers, etc.)!
        //       And log those as warnings!
        private void HandleMacroFillEntity(InstantiationContext context, Entity fillEntity)
        {
            Logger.Verbose($"Processing a {fillEntity.ClassName} entity for instance #{context.ID}.");

            // Split all brushes into tetrahedrons, creating a list of simplexes weighted by volume:
            var candidateVolumes = fillEntity.Brushes
                .SelectMany(brush => brush.GetTetrahedrons())
                .Select(tetrahedron => new { Tetrahedron = tetrahedron, Volume = tetrahedron.GetVolume() })
                .ToArray();
            if (!candidateVolumes.Any())
            {
                Logger.Warning($"{fillEntity.ClassName} is empty and will be skipped.");
                return;
            }

            var totalVolume = candidateVolumes.Sum(candidate => candidate.Volume);


            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, fillEntity, "max_instances", "radius", "instance_orientation", "random_seed", "grid_orientation", "grid_granularity");

            var maxInstances = fillEntity.GetNumericProperty("max_instances") ?? 0.0;
            var radius = (float)(fillEntity.GetNumericProperty("radius") ?? 0);
            var orientation = (Orientation)(fillEntity.GetIntegerProperty("instance_orientation") ?? 0); // TODO: Only global & local!
            var randomSeed = fillEntity.GetIntegerProperty("random_seed") ?? 0;
            var fillMode = (FillMode)(fillEntity.GetIntegerProperty("fill_mode") ?? 0);

            // Grid snapping settings:
            var gridOrientation = (Orientation)(fillEntity.GetIntegerProperty("grid_orientation") ?? 0);    // TODO: Only global & local!
            var gridOrigin = fillEntity.GetOrigin() ?? new Vector3D();  // TODO: For local grid orientation, pick the insertion point of the current context as fallback!!!

            // NOTE: A granularity of 0 (or lower) will disable snapping along that axis.
            var gridGranularity = new Vector3D();
            if (fillEntity.GetNumericArrayProperty("grid_granularity") is double[] granularityArray)
            {
                if (granularityArray.Length == 1)
                    gridGranularity = new Vector3D((float)granularityArray[0], (float)granularityArray[0], (float)granularityArray[0]);
                else if (granularityArray.Length == 2)
                    gridGranularity = new Vector3D((float)granularityArray[0], (float)granularityArray[1], 0);
                else
                    gridGranularity = new Vector3D((float)granularityArray[0], (float)granularityArray[1], (float)granularityArray[2]);
            }

            var hasGridSnapping = gridGranularity.X > 0 || gridGranularity.Y > 0 || gridGranularity.Z > 0;


            var random = new Random(randomSeed);
            switch (fillMode)
            {
                default:
                case FillMode.Random:
                case FillMode.RandomSnappedToGrid:
                {
                    // Between 0 and 1, maxInstances acts as a 'coverage factor':
                    if (maxInstances > 0 && maxInstances < 1)
                    {
                        var instanceVolume = (4.0 / 3.0) * Math.PI * Math.Pow(Math.Max(1.0, radius), 3);
                        maxInstances = Math.Ceiling(maxInstances * (totalVolume / instanceVolume));
                    }

                    Logger.Verbose($"Maximum number of instances: {maxInstances}, total volume: {totalVolume}.");

                    // Create instances at random (grid) points:
                    var availableArea = new SphereCollection();
                    for (int i = 0; i < (int)maxInstances; i++)
                    {
                        var tetrahedron = TakeFromWeightedList(candidateVolumes, random.NextDouble() * totalVolume, candidate => candidate.Volume).Tetrahedron;
                        var insertionPoint = tetrahedron.GetRandomPoint(random);
                        if (fillMode == FillMode.RandomSnappedToGrid && hasGridSnapping)
                        {
                            // NOTE: Grid snapping can produce a point that's outside the fill area. Those points will be discarded:
                            insertionPoint = SnapToGrid(insertionPoint, gridGranularity);
                            if (!fillEntity.Brushes.Any(brush => brush.Contains(insertionPoint)))
                                continue;
                        }

                        if (radius > 0.0 && !availableArea.TryInsert(insertionPoint, radius))
                            continue;

                        CreateInstanceAtPoint(insertionPoint);
                    }
                    break;
                }

                case FillMode.AllGridPoints:
                {
                    // Create an instance at every grid point:
                    var cellSize = new Vector3D(Math.Max(1, gridGranularity.X), Math.Max(1, gridGranularity.Y), Math.Max(1, gridGranularity.Z));
                    var min = SnapToGrid(fillEntity.BoundingBox.Min, cellSize);
                    var max = SnapToGrid(fillEntity.BoundingBox.Max, cellSize);

                    for (float x = min.X; x <= max.X; x += cellSize.X)
                    {
                        for (float y = min.Y; y <= max.Y; y += cellSize.Y)
                        {
                            for (float z = min.Z; z <= max.Z; z += cellSize.Z)
                            {
                                var insertionPoint = new Vector3D(x, y, z);
                                if (!fillEntity.Brushes.Any(brush => brush.Contains(insertionPoint)))
                                    continue;

                                CreateInstanceAtPoint(insertionPoint);
                            }
                        }
                    }
                    break;
                }
            }


            void CreateInstanceAtPoint(Vector3D insertionPoint)
            {
                var template = ResolveTemplate(
                    context.EvaluateInterpolatedString(fillEntity["template_map"]),
                    context.EvaluateInterpolatedString(fillEntity["template_name"]),
                    context);
                if (template == null)
                    return;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = fillEntity.Properties.ToDictionary(
                    kv => context.EvaluateInterpolatedString(kv.Key),
                    kv => context.EvaluateInterpolatedString(kv.Value));

                var transform = GetTransform(insertionPoint, evaluatedProperties);
                var insertionContext = new InstantiationContext(template, transform, evaluatedProperties, context);
                CreateInstance(insertionContext);
            }

            Transform GetTransform(Vector3D insertionPoint, Dictionary<string, string> evaluatedProperties)
            {
                var rotation = Matrix3x3.Identity;  // Global (macro_fill only supports global and local orientations)
                if (orientation == Orientation.Local)
                    rotation = context.Transform.Rotation;

                var scale = evaluatedProperties.GetNumericProperty("instance_scale") ?? 1;
                var angles = (evaluatedProperties.GetAnglesProperty("instance_angles") ?? new Angles()).ToMatrix();

                return new Transform((float)scale, rotation * angles, insertionPoint);
            }

            Vector3D SnapToGrid(Vector3D point, Vector3D cellSize)
            {
                if (cellSize.X > 0)
                    point.X = gridOrigin.X + SnapToNearest(point.X - gridOrigin.X, cellSize.X);

                if (cellSize.Y > 0)
                    point.Y = gridOrigin.Y + SnapToNearest(point.Y - gridOrigin.Y, cellSize.Y);

                if (cellSize.Z > 0)
                    point.Z = gridOrigin.Z + SnapToNearest(point.Z - gridOrigin.Z, cellSize.Z);

                return point;


                float SnapToNearest(float value, float spacing)
                {
                    var f = value / spacing;
                    if (f - Math.Floor(f) < 0.5f)
                        return (float)(Math.Floor(f) * spacing);
                    else
                        return (float)(Math.Ceiling(f) * spacing);
                }
            }
        }

        private void HandleMacroBrushEntity(InstantiationContext context, Entity brushEntity)
        {
            Logger.Verbose($"Processing a {brushEntity.ClassName} entity for instance #{context.ID}.");

            var template = ResolveTemplate(brushEntity["template_map"], brushEntity["template_name"], context);
            if (template == null)
                return;


            // The brushes of this macro_brush entity are copied and given a texture and/or entity attributes
            // based on the world brushes and brush entities in the template. Another way of looking at it is
            // that the brushes and entities in the template take on the 'shape' of this macro_brush.

            var brushContext = new InstantiationContext(template, insertionEntityProperties: brushEntity.Properties, parentContext: context);
            var excludedObjects = GetExcludedObjects(brushContext, Logger);
            Logger.Verbose($"A total of {excludedObjects.Count} objects will be excluded.");

            foreach (var templateBrush in template.Map.WorldGeometry)
            {
                if (excludedObjects.Contains(templateBrush))
                    continue;

                // Discard world brushes with multiple textures:
                if (templateBrush.Faces.Select(face => face.TextureName).Distinct().Count() != 1)
                {
                    Logger.Warning($"{brushEntity.ClassName} encountered a template brush with multiple textures. No copy will be made for this brush.");
                    continue;
                }

                var textureName = templateBrush.Faces[0].TextureName;
                foreach (var copy in CopyBrushes(textureName, excludeOriginBrushes: true))
                    context.OutputMap.WorldGeometry.Add(copy);
            }

            foreach (var templateEntity in template.Map.Entities)
            {
                if (templateEntity.IsPointBased || excludedObjects.Contains(templateEntity))
                    continue;

                // Origin brushes are only copied if the template entity also contains an origin brush.
                var templateHasOrigin = templateEntity.Brushes.FirstOrDefault(brush => brush.IsOriginBrush()) != null;
                var templateTextureNames = templateEntity.Brushes
                    .Where(brush => !brush.IsOriginBrush())
                    .SelectMany(brush => brush.Faces)
                    .Select(face => face.TextureName)
                    .Distinct();
                if (templateTextureNames.Count() != 1)
                {
                    Logger.Warning($"{brushEntity.ClassName} encountered a '{templateEntity.ClassName}' template entity with multiple textures. No copy will be made for this entity.");
                    continue;
                }

                var textureName = templateEntity.Brushes[0].Faces[0].TextureName;
                var entityCopy = new Entity(CopyBrushes(textureName, excludeOriginBrushes: !templateHasOrigin));

                // Use the current transform - macro_brushes do not support angles/scale:
                var insertionContext = new InstantiationContext(template, brushContext.Transform, brushEntity.Properties, brushContext);
                foreach (var kv in templateEntity.Properties)
                    entityCopy.Properties[insertionContext.EvaluateInterpolatedString(kv.Key)] = insertionContext.EvaluateInterpolatedString(kv.Value);


                // The copy already has its final orientation, so we don't want it to be transformed again:
                HandleEntity(insertionContext, entityCopy, applyTransform: false);
            }


            IEnumerable<Brush> CopyBrushes(string textureName, bool excludeOriginBrushes = false)
            {
                // Keep the original textures if the template brush is covered with 'NULL':
                var applyTexture = textureName.ToUpper() != "NULL";

                foreach (var brush in brushEntity.Brushes)
                {
                    if (excludeOriginBrushes && brush.IsOriginBrush())
                        continue;

                    var copy = brush.Copy(new Vector3D());
                    if (applyTexture && !brush.IsOriginBrush())
                    {
                        foreach (var face in copy.Faces)
                            face.TextureName = textureName;
                    }
                    yield return copy;
                }
            }
        }


        private static HashSet<object> GetExcludedObjects(InstantiationContext context, Logger logger)
        {
            var excludedObjects = new HashSet<object>();
            foreach (var conditionalContent in context.Template.ConditionalContents)
            {
                var removal = PropertyExtensions.ParseProperty(context.EvaluateInterpolatedString(conditionalContent.RemovalCondition));
                if (Interpreter.IsTrue(removal) || (removal is double d && d == 0))
                {
                    logger.Verbose($"Removal condition '{conditionalContent.RemovalCondition}' is true, excluding {conditionalContent.Contents.Count} objects.");
                    excludedObjects.UnionWith(conditionalContent.Contents);
                }
                else
                {
                    logger.Verbose($"Removal condition '{conditionalContent.RemovalCondition}' is not true, keeping {conditionalContent.Contents.Count} objects.");
                }
            }
            return excludedObjects;
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


        class DirectoryFunctions
        {
            private string _directory;
            private string _messDirectory;


            public DirectoryFunctions(string directory, string messDirectory)
            {
                _directory = directory;
                _messDirectory = messDirectory;
            }


            public string dir() => _directory;
            public string messdir() => _messDirectory;
        }
    }
}
