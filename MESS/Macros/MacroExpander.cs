using MESS.Common;
using MESS.EntityRewriting;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using MScript.Evaluation;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        public static Map ExpandMacros(string path, ExpansionSettings settings, ILogger logger)
        {
            // TODO: Verify that 'path' is absolute! Either that, or document the behavior for relative paths! (relative to cwd?)

            var globals = new Dictionary<string, object>();
            var expander = new MacroExpander(settings, logger);
            var mainTemplate = expander.GetMapTemplate(path, globals);

            var context = new InstantiationContext(
                mainTemplate,
                logger,
                insertionEntityProperties: settings.Variables?.ToDictionary(kv => kv.Key, kv => Interpreter.Print(kv.Value)) ?? new Dictionary<string, string>(),
                workingDirectory: settings.Directory,
                globals: globals);
            expander.CreateInstance(context);

            return context.OutputMap;
        }


        private ExpansionSettings Settings { get; }
        private ILogger Logger { get; }

        private Dictionary<string, MapTemplate> _mapTemplateCache = new Dictionary<string, MapTemplate>();
        private int _instanceCount = 0;

        private RewriteDirective[] _rewriteDirectives = Array.Empty<RewriteDirective>();
        private DirectoryFunctions _directoryFunctions;


        private MacroExpander(ExpansionSettings settings, ILogger logger)
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

            var randomSeed = (int)(map.Properties.GetNumericProperty(Attributes.RandomSeed) ?? 0);
            var random = new Random(randomSeed);
            var globals = new Dictionary<string, object>();

            var directiveLookup = rewriteDirectives.ToLookup(rewriteDirective => rewriteDirective.ClassName);

            foreach (var rewriteDirective in directiveLookup[Entities.Worldspawn])
                ApplyRewriteDirective(map.Properties, rewriteDirective, 0, random, globals);

            for (int i = 0; i < map.Entities.Count; i++)
            {
                var entity = map.Entities[i];
                var entityID = i + 1;

                var matchingDirectives = directiveLookup[entity.ClassName];
                foreach (var rewriteDirective in matchingDirectives)
                    ApplyRewriteDirective(entity.Properties, rewriteDirective, entityID, random, globals);
            }
        }

        private void ApplyRewriteDirective(Dictionary<string, string> entityProperties, RewriteDirective rewriteDirective, int entityID, Random random, IDictionary<string, object> globals)
        {
            var context = Evaluation.ContextFromProperties(entityProperties, entityID, entityID, random, globals, Logger);
            NativeUtils.RegisterInstanceMethods(context, _directoryFunctions);

            foreach (var ruleGroup in rewriteDirective.RuleGroups)
            {
                if (!ruleGroup.HasCondition || Interpreter.IsTrue(PropertyExtensions.ParseProperty(Evaluation.EvaluateInterpolatedString(ruleGroup.Condition, context))))
                {
                    foreach (var rule in ruleGroup.Rules)
                        ApplyRewriteRule(entityProperties, rule, context);
                }
                else if (ruleGroup.HasCondition)
                {
                    foreach (var rule in ruleGroup.AlternateRules)
                        ApplyRewriteRule(entityProperties, rule, context);
                }
            }
        }

        private void ApplyRewriteRule(Dictionary<string, string> entityProperties, RewriteDirective.Rule rule, EvaluationContext context)
        {
            var attributeName = Evaluation.EvaluateInterpolatedString(rule.Attribute, context);
            if (rule.DeleteAttribute)
            {
                entityProperties.Remove(attributeName);
                context.Bind(attributeName, null);
            }
            else
            {
                var value = Evaluation.EvaluateInterpolatedString(rule.NewValue, context);
                entityProperties[attributeName] = value;
                context.Bind(attributeName, value);
            }
        }


        /// <summary>
        /// Loads the specified map and returns it as a template. Templates are cached, so maps that are requested multiple times only need to be loaded once.
        /// </summary>
        private MapTemplate GetMapTemplate(string path, IDictionary<string, object> globals)
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

                template = MapTemplate.FromMap(map, path, globals, Logger);

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
                    return GetMapTemplate(mapPath, context.Globals);
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
                    .Select(subTemplate => new {
                        SubTemplate = subTemplate,
                        Weight = double.TryParse(context.EvaluateInterpolatedString(subTemplate.SelectionWeightExpression), NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) ? weight : 0
                    })
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
            Logger.Verbose("");
            Logger.Verbose($"Creating instance #{context.ID} (sequence number: {context.SequenceNumber}) at {context.Transform}.");

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

                context.OutputMap.AddBrush(brush.Copy(context.Transform));
            }
        }


        private void HandleEntity(InstantiationContext context, Entity entity, bool applyTransform = true)
        {
            try
            {
                switch (entity.ClassName)
                {
                    case MacroEntity.Insert:
                        // TODO: Insert 'angles' and 'scale' properties here if the entity doesn't contain them,
                        //       to ensure that transformation always works correctly?
                        HandleMacroInsertEntity(context, entity.Copy(context, applyTransform: applyTransform, evaluateExpressions: false));
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
            catch (Exception ex)
            {
                ex.Data["classname"] = entity.ClassName;
                ex.Data["targetname"] = entity.GetStringProperty("targetname");
                ex.Data["context ID"] = context.ID;
                throw;
            }
        }

        private void HandleMacroInsertEntity(InstantiationContext context, Entity insertEntity)
        {
            Logger.Verbose($"Processing a {insertEntity.ClassName} entity for instance #{context.ID}.");

            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateProperties(context, insertEntity, Attributes.InstanceCount, Attributes.RandomSeed);

            var instanceCount = insertEntity.GetIntegerProperty(Attributes.InstanceCount) ?? 1;
            var randomSeed = insertEntity.GetIntegerProperty(Attributes.RandomSeed) ?? 0;

            var random = new Random(randomSeed);
            for (int i = 0; i < instanceCount; i++)
            {
                var sequenceContext = context.GetChildContextWithSequenceNumber(i);
                var template = ResolveTemplate(
                    sequenceContext.EvaluateInterpolatedString(insertEntity.GetStringProperty(Attributes.TemplateMap)),
                    sequenceContext.EvaluateInterpolatedString(insertEntity.GetStringProperty(Attributes.TemplateName)),
                    sequenceContext);
                if (template == null)
                    continue;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = insertEntity.Properties.ToDictionary(
                    kv => sequenceContext.EvaluateInterpolatedString(kv.Key),
                    kv => sequenceContext.EvaluateInterpolatedString(kv.Value));    // TODO: This can produce different values for instance-count, random-seed, template-map and template-name! (also an issue in macro_cover and macro_fill?)

                // TODO: Verify that this works correctly even when the macro_insert entity does not originally contain
                //       'angles' and 'scale' properties!

                // Create a child context for this insertion, with a properly adjusted transform:
                // NOTE: MappingExtensions.Copy(Entity) already applied context.Transform to the scale and angles attributes, but geometry-scale is not affected:
                var scale = (float)(evaluatedProperties.GetNumericProperty(Attributes.Scale) ?? 1);
                var geometryScale = (evaluatedProperties.GetVector3DProperty(Attributes.InstanceGeometryScale) ?? new Vector3D(scale, scale, scale)) * sequenceContext.Transform.GeometryScale;
                var anglesMatrix = evaluatedProperties.GetAnglesProperty(Attributes.Angles)?.ToMatrix() ?? Matrix3x3.Identity;
                var offset = evaluatedProperties.GetVector3DProperty(Attributes.InstanceOffset) ?? new Vector3D();

                var transform = new Transform(
                    scale,
                    geometryScale,
                    anglesMatrix,
                    insertEntity.Origin + offset);

                // TODO: Maybe filter out a few entity properties, such as 'classname', 'origin', etc?
                var insertionContext = new InstantiationContext(template, Logger, transform, evaluatedProperties, sequenceContext, sequenceNumber: i);

                CreateInstance(insertionContext);
            }
        }

        private void HandleMacroCoverEntity(InstantiationContext context, Entity coverEntity)
        {
            Logger.Verbose($"Processing a {coverEntity.ClassName} entity for instance #{context.ID}.");

            // TODO: Also ignore other 'special' textures?
            // Triangulate all non-NULL faces, creating a list of triangles weighted by surface area:
            var candidateFaces = coverEntity.Brushes
                .SelectMany(brush => brush.Faces)
                .Where(face => face.TextureName.ToUpper() != Textures.Null)
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
            EvaluateProperties(context, coverEntity, Attributes.MaxInstances, Attributes.Radius, Attributes.InstanceOrientation, Attributes.RandomSeed, Attributes.BrushBehavior);
            SetBrushEntityOriginProperty(coverEntity);

            var maxInstances = coverEntity.GetNumericProperty(Attributes.MaxInstances) ?? 0.0;
            var radius = (float)(coverEntity.GetNumericProperty(Attributes.Radius) ?? 0);
            var orientation = (Orientation)(coverEntity.GetIntegerProperty(Attributes.InstanceOrientation) ?? 0);
            var randomSeed = coverEntity.GetIntegerProperty(Attributes.RandomSeed) ?? 0;
            var brushBehavior = (CoverBrushBehavior)(coverEntity.GetIntegerProperty(Attributes.BrushBehavior) ?? 0);


            // Between 0 and 1, maxInstances acts as a 'coverage factor':
            if (maxInstances > 0 && maxInstances < 1)
            {
                var instanceArea = Math.PI * Math.Pow(Math.Max(1.0, radius), 2);;
                maxInstances = Math.Ceiling(maxInstances * (totalArea / instanceArea));
            }

            Logger.Verbose($"Maximum number of instances: {maxInstances}, total surface area: {totalArea}.");

            var random = new Random(randomSeed);
            var sequenceNumber = 0;
            var availableArea = new SphereCollection();
            for (int i = 0; i < (int)maxInstances; i++)
            {
                var selection = TakeFromWeightedList(candidateFaces, random.NextDouble() * totalArea, candidate => candidate.Area);
                var insertionPoint = selection.Triangle.GetRandomPoint(random);
                if (radius > 0.0 && !availableArea.TryInsert(insertionPoint, radius))
                    continue;

                var sequenceContext = context.GetChildContextWithSequenceNumber(sequenceNumber);
                var template = ResolveTemplate(
                    sequenceContext.EvaluateInterpolatedString(coverEntity.GetStringProperty(Attributes.TemplateMap)),
                    sequenceContext.EvaluateInterpolatedString(coverEntity.GetStringProperty(Attributes.TemplateName)),
                    sequenceContext);
                if (template == null)
                    continue;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = coverEntity.Properties.ToDictionary(
                    kv => sequenceContext.EvaluateInterpolatedString(kv.Key),
                    kv => sequenceContext.EvaluateInterpolatedString(kv.Value));

                var transform = GetTransform(insertionPoint, selection.Face, evaluatedProperties);
                var insertionContext = new InstantiationContext(template, Logger, transform, evaluatedProperties, sequenceContext, sequenceNumber: sequenceNumber);
                CreateInstance(insertionContext);

                sequenceNumber += 1;
            }

            switch (brushBehavior)
            {
                default:
                case CoverBrushBehavior.Remove:
                    break;

                case CoverBrushBehavior.WorldGeometry:
                    foreach (var brush in coverEntity.Brushes)
                        context.OutputMap.AddBrush(brush.Copy(new Vector3D()));
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

                var scale = (float)(evaluatedProperties.GetNumericProperty(Attributes.InstanceScale) ?? 1);
                var geometryScale = evaluatedProperties.GetVector3DProperty(Attributes.InstanceGeometryScale) ?? new Vector3D(scale, scale, scale);
                var anglesMatrix = evaluatedProperties.GetAnglesProperty(Attributes.InstanceAngles)?.ToMatrix() ?? Matrix3x3.Identity;
                var offset = evaluatedProperties.GetVector3DProperty(Attributes.InstanceOffset) ?? new Vector3D();

                return new Transform(
                    scale,
                    geometryScale,
                    rotation * anglesMatrix,
                    insertionPoint + offset);
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
            EvaluateProperties(context, fillEntity, Attributes.MaxInstances, Attributes.Radius, Attributes.InstanceOrientation, Attributes.RandomSeed, Attributes.FillMode, Attributes.GridOrientation, Attributes.GridGranularity);
            SetBrushEntityOriginProperty(fillEntity);

            var maxInstances = fillEntity.GetNumericProperty(Attributes.MaxInstances) ?? 0.0;
            var radius = (float)(fillEntity.GetNumericProperty(Attributes.Radius) ?? 0);
            var orientation = (Orientation)(fillEntity.GetIntegerProperty(Attributes.InstanceOrientation) ?? 0); // TODO: Only global & local!
            var randomSeed = fillEntity.GetIntegerProperty(Attributes.RandomSeed) ?? 0;
            var fillMode = (FillMode)(fillEntity.GetIntegerProperty(Attributes.FillMode) ?? 0);

            // Grid snapping settings:
            var gridOrientation = (Orientation)(fillEntity.GetIntegerProperty(Attributes.GridOrientation) ?? 0);    // TODO: Only global & local!
            var gridOrigin = fillEntity.GetOrigin() ?? new Vector3D();  // TODO: For local grid orientation, pick the insertion point of the current context as fallback!!!

            // NOTE: A granularity of 0 (or lower) will disable snapping along that axis.
            var gridGranularity = new Vector3D();
            if (fillEntity.GetNumericArrayProperty(Attributes.GridGranularity) is double[] granularityArray)
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
            var sequenceNumber = 0;
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
                var sequenceContext = context.GetChildContextWithSequenceNumber(sequenceNumber);
                var template = ResolveTemplate(
                    sequenceContext.EvaluateInterpolatedString(fillEntity.GetStringProperty(Attributes.TemplateMap)),
                    sequenceContext.EvaluateInterpolatedString(fillEntity.GetStringProperty(Attributes.TemplateName)),
                    sequenceContext);
                if (template == null)
                    return;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = fillEntity.Properties.ToDictionary(
                    kv => sequenceContext.EvaluateInterpolatedString(kv.Key),
                    kv => sequenceContext.EvaluateInterpolatedString(kv.Value));

                var transform = GetTransform(insertionPoint, evaluatedProperties);
                var insertionContext = new InstantiationContext(template, Logger, transform, evaluatedProperties, sequenceContext, sequenceNumber: sequenceNumber);
                CreateInstance(insertionContext);

                sequenceNumber += 1;
            }

            Transform GetTransform(Vector3D insertionPoint, Dictionary<string, string> evaluatedProperties)
            {
                var rotation = Matrix3x3.Identity;  // Global (macro_fill only supports global and local orientations)
                if (orientation == Orientation.Local)
                    rotation = context.Transform.Rotation;

                var scale = (float)(evaluatedProperties.GetNumericProperty(Attributes.InstanceScale) ?? 1);
                var geometryScale = evaluatedProperties.GetVector3DProperty(Attributes.InstanceGeometryScale) ?? new Vector3D(scale, scale, scale);
                var anglesMatrix = evaluatedProperties.GetAnglesProperty(Attributes.InstanceAngles)?.ToMatrix() ?? Matrix3x3.Identity;
                var offset = evaluatedProperties.GetVector3DProperty(Attributes.InstanceOffset) ?? new Vector3D();

                return new Transform(
                    scale,
                    geometryScale,
                    rotation * anglesMatrix,
                    insertionPoint + offset);
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

            var template = ResolveTemplate(brushEntity.GetStringProperty(Attributes.TemplateMap), brushEntity.GetStringProperty(Attributes.TemplateName), context);
            if (template == null)
                return;


            SetBrushEntityOriginProperty(brushEntity);

            // The brushes of this macro_brush entity are copied and given a texture and/or entity attributes
            // based on the world brushes and brush entities in the template. Another way of looking at it is
            // that the brushes and entities in the template take on the 'shape' of this macro_brush.

            var brushContext = new InstantiationContext(template, Logger, insertionEntityProperties: brushEntity.Properties, parentContext: context);
            var excludedObjects = GetExcludedObjects(brushContext, Logger);
            Logger.Verbose($"A total of {excludedObjects.Count} objects will be excluded.");

            // World brushes:
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
                    context.OutputMap.AddBrush(copy);
            }

            // Entities:
            var sequenceNumber = 0;
            foreach (var templateEntity in template.Map.Entities)
            {
                if (templateEntity.IsPointBased || excludedObjects.Contains(templateEntity))
                    continue;

                var sequenceContext = brushContext.GetChildContextWithSequenceNumber(sequenceNumber);

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
                foreach (var kv in templateEntity.Properties)
                    entityCopy.Properties[sequenceContext.EvaluateInterpolatedString(kv.Key)] = sequenceContext.EvaluateInterpolatedString(kv.Value);


                // The copy already has its final orientation, so we don't want it to be transformed again:
                HandleEntity(sequenceContext, entityCopy, applyTransform: false);

                sequenceNumber += 1;
            }


            IEnumerable<Brush> CopyBrushes(string textureName, bool excludeOriginBrushes = false)
            {
                // Keep the original textures if the template brush is covered with 'NULL':
                var applyTexture = textureName.ToUpper() != Textures.Null;

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


        private static HashSet<object> GetExcludedObjects(InstantiationContext context, ILogger logger)
        {
            var excludedObjects = new HashSet<object>();
            foreach (var conditionalContent in context.Template.ConditionalContents)
            {
                var removal = PropertyExtensions.ParseProperty(context.EvaluateInterpolatedString(conditionalContent.RemovalCondition));
                if (Interpreter.IsTrue(removal) && !(removal is double d && d == 0))
                {
                    logger.Verbose($"Removal condition '{conditionalContent.RemovalCondition}' is true ({removal?.ToString()}), excluding {conditionalContent.Contents.Count} objects.");
                    excludedObjects.UnionWith(conditionalContent.Contents);
                }
                else
                {
                    logger.Verbose($"Removal condition '{conditionalContent.RemovalCondition}' is not true ({removal?.ToString()}), keeping {conditionalContent.Contents.Count} objects.");
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

        private static void SetBrushEntityOriginProperty(Entity entity)
        {
            if (!entity.IsPointBased && !entity.Properties.ContainsKey(Attributes.Origin) && entity.GetOrigin() is Vector3D origin)
                entity.Properties[Attributes.Origin] = FormattableString.Invariant($"{origin.X} {origin.Y} {origin.Z}");
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
