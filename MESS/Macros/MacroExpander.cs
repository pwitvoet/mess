using MESS.Common;
using MESS.EntityRewriting;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MESS.Util;
using MScript;
using MScript.Evaluation;

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
        public static Map ExpandMacros(string path, ExpansionSettings settings, RewriteDirective[] rewriteDirectives, ILogger logger)
        {
            // TODO: Verify that 'path' is absolute! Either that, or document the behavior for relative paths! (relative to cwd?)
            logger.Info("");
            logger.Info($"Expanding macros in '{path}'.");

            var expander = new MacroExpander(settings, rewriteDirectives, logger);
            var mainTemplate = expander.GetMapTemplate(path);

            var templateMapPaths = Array.Empty<string>();
            var templateNames = Array.Empty<string>();
            IDictionary<string, object?>? evaluatedMapProperties = null;

            var context = new InstantiationContext(
                mainTemplate,
                logger,
                settings.Variables ?? new(),
                expander.BaseEvaluationContext,
                settings.TemplatesDirectory,
                evaluatedProperties =>
                {
                    expander.GetAndRemoveAttachedTemplateAttributes(evaluatedProperties, out templateMapPaths, out templateNames);
                    evaluatedMapProperties = evaluatedProperties;
                });

            expander.CreateAttachedTemplateInstances(context, new Vector3D(), evaluatedMapProperties ?? new Dictionary<string, object?>(), templateMapPaths, templateNames);

            expander.CreateInstance(context);

            expander.ApplyRewriteDirectives(context.OutputMap, path, ProcessingStage.AfterMacroExpansion, null, null);

            return context.OutputMap;
        }


        private ExpansionSettings Settings { get; }
        private ILogger Logger { get; }

        /// <summary>
        /// Global variables are used by the <see cref="MacroExpanderFunctions.getglobal(string?)"/>, <see cref="MacroExpanderFunctions.setglobal(string?, object?)"/>
        /// and <see cref="MacroExpanderFunctions.useglobal(string?)"/> MScript functions. They are useful for things like avoiding duplicate template instantiation,
        /// but should be used with care.
        /// </summary>
        private Dictionary<string, object?> Globals { get; }

        /// <summary>
        /// This evaluation context contains bindings for standard library functions, as well as settings-related (directory) functions.
        /// It is intended to be used as a parent context for all other evaluation contexts.
        /// </summary>
        private EvaluationContext BaseEvaluationContext { get; }

        /// <summary>
        /// This evaluation context is based on <see cref="BaseEvaluationContext"/>, but also makes top-level variables available.
        /// It is intended to be used when loading template maps, applying rewrite rules and when creating the main map instance.
        /// </summary>
        private EvaluationContext TopLevelEvaluationContext { get; }

        private Dictionary<string, MapTemplate> _mapTemplateCache = new Dictionary<string, MapTemplate>();
        private int _instanceCount = 0;

        private RewriteDirective[] RewriteDirectives { get; }


        private MacroExpander(ExpansionSettings settings, RewriteDirective[] rewriteDirectives, ILogger logger)
        {
            Settings = settings;
            Logger = logger;

            Globals = settings.Globals;

            var macroExpanderFunctions = new MacroExpanderFunctions(settings.TemplatesDirectory, AppContext.BaseDirectory, Globals);
            BaseEvaluationContext = Evaluation.DefaultContext();
            NativeUtils.RegisterInstanceMethods(BaseEvaluationContext, macroExpanderFunctions);

            TopLevelEvaluationContext = Evaluation.ContextWithBindings(settings.Variables ?? new(), 0, 0, new Random(0), "", Logger, BaseEvaluationContext);


            RewriteDirectives = rewriteDirectives;
        }


        private void ApplyRewriteDirectives(Map map, string mapPath, ProcessingStage processingStage, IEnumerable<string>? tedPathWhitelist, IEnumerable<string>? tedPathBlacklist)
        {
            Logger.Info("");
            Logger.Info($"{processingStage}: applying {RewriteDirectives.Count(directive => directive.Stage == processingStage)} rewrite directives to map.");

            var randomSeed = map.Properties.GetInteger(Attributes.RandomSeed) ?? 0;
            var random = new Random(randomSeed);

            var directiveLookup = new Dictionary<string, List<RewriteDirective>>();
            var wildcardDirectives = new List<RewriteDirective>();
            foreach (var rewriteDirective in RewriteDirectives)
            {
                if (rewriteDirective.Stage != processingStage)
                    continue;

                if (!IsAllowed(rewriteDirective))
                    continue;

                if (rewriteDirective.ClassNames.Length == 0)
                {
                    wildcardDirectives.Add(rewriteDirective);
                    continue;
                }

                foreach (var className in rewriteDirective.ClassNames)
                {
                    if (!directiveLookup.TryGetValue(className, out var directives))
                    {
                        directives = new List<RewriteDirective>();
                        directiveLookup[className] = directives;
                    }
                    directives.Add(rewriteDirective);
                }
            }

            ApplyMatchingDirectives(Entities.Worldspawn, map.Properties, 0);

            for (int i = 0; i < map.Entities.Count; i++)
            {
                var entity = map.Entities[i];
                var entityID = i + 1;
                ApplyMatchingDirectives(entity.ClassName ?? "", map.Entities[i].Properties, entityID);
            }


            void ApplyMatchingDirectives(string className, Dictionary<string, string> entityProperties, int entityID)
            {
                if (directiveLookup.TryGetValue(className, out var matchingDirectives))
                {
                    foreach (var rewriteDirective in matchingDirectives)
                        ApplyRewriteDirective(entityProperties, rewriteDirective, entityID, random, mapPath);
                }

                foreach (var rewriteDirective in wildcardDirectives)
                    ApplyRewriteDirective(entityProperties, rewriteDirective, entityID, random, mapPath);
            }

            bool IsAllowed(RewriteDirective rewriteDirective)
            {
                var path = MtbFileSystem.GetNormalizedPath(rewriteDirective.SourceFilePath);
                if (tedPathWhitelist != null)
                    return ContainsMatch(tedPathWhitelist, path);
                else if (tedPathBlacklist != null)
                    return !ContainsMatch(tedPathBlacklist, path);
                else
                    return true;
            }

            bool ContainsMatch(IEnumerable<string> list, string path)
            {
                if (list == null)
                    return false;

                foreach (var entry in list)
                {
                    if (string.Equals(Path.GetExtension(entry), ".ted", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(entry, path, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else
                    {
                        if (FileSystem.IsParentPath(entry, path))
                            return true;
                    }
                }

                return false;
            }
        }

        private void ApplyRewriteDirective(Dictionary<string, string> entityProperties, RewriteDirective rewriteDirective, int entityID, Random random, string mapPath)
        {
            var unevaluatedProperties = entityProperties.ToDictionary(
                property => property.Key,
                property => Evaluation.ParseMScriptValue(property.Value));
            var context = Evaluation.ContextWithBindings(unevaluatedProperties, entityID, entityID, random, mapPath, Logger, TopLevelEvaluationContext);

            if (rewriteDirective.Condition != null)
            {
                var isMatch = Evaluation.EvaluateInterpolatedStringOrExpression(rewriteDirective.Condition, context);
                if (!Interpreter.IsTrue(isMatch) || isMatch is 0.0)
                    return;
            }

            Logger.Verbose($"Applying directive from '{rewriteDirective.SourceFilePath}' to {entityProperties.GetString(Attributes.Classname)}.");

            NativeUtils.RegisterInstanceMethods(context, new RewriteDirectiveFunctions(rewriteDirective.SourceFilePath));

            foreach (var ruleGroup in rewriteDirective.RuleGroups)
            {
                var isConditionTrue = false;
                if (ruleGroup.HasCondition)
                {
                    var result = Evaluation.EvaluateInterpolatedStringOrExpression(ruleGroup.Condition, context);
                    isConditionTrue = Interpreter.IsTrue(result) && result is not 0.0;
                }

                if (!ruleGroup.HasCondition || isConditionTrue)
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
        private MapTemplate GetMapTemplate(string path)
        {
            path = FileSystem.GetFullPath(path, Settings.TemplatesDirectory);
            if (!_mapTemplateCache.TryGetValue(path, out var template))
            {
                Logger.Info($"Loading map template '{path}' from file.");

                // NOTE: Entity rewrite directives are applied after entity path expansion, so rewriting will also affect these entities.
                //       Rewriting happens before template detection, so rewriting something to a macro_template entity will work as expected:
                var map = MapFile.Load(path);
                map.ExpandPaths();

                var tedPathWhitelist = GetTedPathList(map.Properties, Attributes.AllowRewriteRules);
                var tedPathBlacklist = GetTedPathList(map.Properties, Attributes.DenyRewriteRules);
                ApplyRewriteDirectives(map, path, ProcessingStage.BeforeMacroExpansion, tedPathWhitelist, tedPathBlacklist);

                template = MapTemplate.FromMap(map, path, TopLevelEvaluationContext, Logger);

                _mapTemplateCache[path] = template;

                map.Properties.Remove(Attributes.AllowRewriteRules);
                map.Properties.Remove(Attributes.DenyRewriteRules);
            }
            else
            {
                Logger.Verbose($"Loading map template '{path}' from cache.");
            }
            return template;

            string[]? GetTedPathList(IDictionary<string, string> properties, string propertyName)
            {
                var value = properties.EvaluateToMScriptValue(propertyName, TopLevelEvaluationContext);
                if (value == null)
                    return properties.ContainsKey(propertyName) ? Array.Empty<string>() : null;

                return Interpreter.Print(value).Split(';')
                    .Select(path => FileSystem.GetFullPath(path, Settings.TemplatesDirectory))
                    .ToArray();
            }
        }

        /// <summary>
        /// Resolves a template by either loading it from a file or by picking a sub-template from the current context.
        /// Logs a warning and returns null if the specified template could not be resolved.
        /// </summary>
        private MapTemplate? ResolveTemplate(string? mapPath, string? templateName, InstantiationContext context)
        {
            if (!string.IsNullOrEmpty(mapPath))
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
            else if (!string.IsNullOrEmpty(templateName))
            {
                Logger.Verbose($"Resolving sub-template '{templateName}'.");

                // We'll look for sub-templates in the closest parent context whose template has been loaded from a map file.
                // If there are multiple matches, we'll pick one at random. If there are no matches, we'll fall through and return null.
                var matchingSubTemplates = context.SubTemplates
                    .Where(subTemplate => Evaluation.EvaluateInterpolatedString(subTemplate.Name, context) == templateName)
                    .Select(subTemplate => new {
                        SubTemplate = subTemplate,
                        Weight = PropertyExtensions.TryParseDouble(Evaluation.EvaluateInterpolatedString(subTemplate.SelectionWeightExpression, context), out var weight) ? weight : 0
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
            Logger.Verbose($"Creating instance #{context.ID} (template: '{context.Template.Name}', sequence number: {context.SequenceNumber}) at {context.Transform}.");

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


        private void HandleEntity(InstantiationContext context, Entity entity, bool transformBrushes = true)
        {
            var transform = transformBrushes ? context.Transform : Transform.Identity;

            try
            {
                switch (entity.ClassName)
                {
                    case MacroEntity.Insert:
                        // TODO: Insert 'angles' and 'scale' properties here if the entity doesn't contain them,
                        //       to ensure that transformation always works correctly?
                        HandleMacroInsertEntity(context, entity.Copy(transform));
                        break;

                    case MacroEntity.Cover:
                        // TODO: 'spawnflags' won't be updated here! (however, macro_cover doesn't have any flags, so...)
                        HandleMacroCoverEntity(context, entity.Copy(transform));
                        break;

                    case MacroEntity.Fill:
                        // TODO: 'spawnflags' won't be updated here! (however, macro_fill doesn't have any flags, so...)
                        HandleMacroFillEntity(context, entity.Copy(transform));
                        break;

                    case MacroEntity.Brush:
                        // NOTE: Brush rotation is taken care of within HandleMacroBrushEntity itself:
                        HandleMacroBrushEntity(context, entity.Copy(Transform.Identity));
                        break;

                    default:
                        // Other entities are copied directly, with expressions in their property keys/values being evaluated:
                        HandleNormalEntity(context, entity.Copy(transform));
                        break;
                }
            }
            catch (Exception ex)
            {
                ex.Data["classname"] = entity.ClassName;
                ex.Data["targetname"] = entity.Properties.GetString(Attributes.Targetname);
                ex.Data["context ID"] = context.ID;
                throw;
            }
        }

        private void HandleMacroInsertEntity(InstantiationContext context, Entity insertEntity)
        {
            Logger.Verbose($"Processing a {insertEntity.ClassName} entity for instance #{context.ID}.");

            // Most properties will be evaluated again for each template instance that this entity creates,
            // but there are a few that are needed up-front, so these will only be evaluated once:
            EvaluateAndUpdateProperties(context, insertEntity, Attributes.InstanceCount, Attributes.RandomSeed);

            var instanceCount = insertEntity.Properties.GetInteger(Attributes.InstanceCount) ?? 1;
            var randomSeed = insertEntity.Properties.GetInteger(Attributes.RandomSeed) ?? 0;

            var random = new Random(randomSeed);
            for (int i = 0; i < instanceCount; i++)
            {
                var sequenceContext = context.GetChildContextWithSequenceNumber(i);
                var template = ResolveTemplate(
                    Evaluation.EvaluateInterpolatedString(insertEntity.Properties.GetString(Attributes.TemplateMap), sequenceContext),
                    Evaluation.EvaluateInterpolatedString(insertEntity.Properties.GetString(Attributes.TemplateName), sequenceContext),
                    sequenceContext);
                if (template == null)
                    continue;

                // Evaluating properties again for each instance allows for iterating (nth function) and randomization (rand/randi functions):
                var evaluatedProperties = insertEntity.Properties.EvaluateToMScriptValues(sequenceContext); // TODO: This can produce different values for instance-count, random-seed, template-map and template-name! (also an issue in macro_cover and macro_fill?)

                // NOTE: Because some editors use 0 as default value for missing attributes, we'll swap values here to ensure that Local is the default behavior:
                var orientation = (evaluatedProperties.GetInteger(Attributes.InstanceOrientation) ?? 0) == 0 ? Orientation.Local : Orientation.Global;
                var rotation = Matrix3x3.Identity;
                if (orientation == Orientation.Local)
                    rotation = sequenceContext.Transform.Rotation;

                // Create a child context for this insertion, with a properly adjusted transform:
                var scale = (float)(evaluatedProperties.GetDouble(Attributes.Scale) ?? 1);
                var geometryScale = evaluatedProperties.GetVector3D(Attributes.InstanceGeometryScale) ?? new Vector3D(scale, scale, scale);
                var anglesMatrix = evaluatedProperties.GetAngles(Attributes.Angles)?.ToMatrix() ?? Matrix3x3.Identity;
                var offset = evaluatedProperties.GetVector3D(Attributes.InstanceOffset) ?? new Vector3D();

                var transform = new Transform(
                    sequenceContext.Transform.Scale * scale,
                    sequenceContext.Transform.GeometryScale * geometryScale,
                    rotation * anglesMatrix,
                    sequenceContext.Transform.Apply(insertEntity.Origin + offset));

                // TODO: Maybe filter out a few entity properties, such as 'classname', 'origin', etc?
                evaluatedProperties.UpdateTransformProperties(context.Transform);
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
            EvaluateAndUpdateProperties(context, coverEntity, Attributes.MaxInstances, Attributes.Radius, Attributes.RandomSeed, Attributes.BrushBehavior);
            SetBrushEntityOriginProperty(coverEntity);

            var maxInstances = coverEntity.Properties.GetDouble(Attributes.MaxInstances) ?? 0.0;
            var radius = (float)(coverEntity.Properties.GetDouble(Attributes.Radius) ?? 0);
            var randomSeed = coverEntity.Properties.GetInteger(Attributes.RandomSeed) ?? 0;
            var brushBehavior = (CoverBrushBehavior)(coverEntity.Properties.GetInteger(Attributes.BrushBehavior) ?? 0);


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
                if (selection == null)
                    continue;

                var insertionPoint = selection.Triangle.GetRandomPoint(random);
                if (radius > 0.0 && !availableArea.TryInsert(insertionPoint, radius))
                    continue;

                var sequenceContext = context.GetChildContextWithSequenceNumber(sequenceNumber);
                var template = ResolveTemplate(
                    Evaluation.EvaluateInterpolatedString(coverEntity.Properties.GetString(Attributes.TemplateMap), sequenceContext),
                    Evaluation.EvaluateInterpolatedString(coverEntity.Properties.GetString(Attributes.TemplateName), sequenceContext),
                    sequenceContext);
                if (template == null)
                    continue;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = coverEntity.Properties.EvaluateToMScriptValues(sequenceContext);

                var orientation = (Orientation)(evaluatedProperties.GetInteger(Attributes.InstanceOrientation) ?? 0);
                var transform = GetTransform(insertionPoint, orientation, selection.Face, evaluatedProperties);
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


            Transform GetTransform(Vector3D insertionPoint, Orientation orientation, Face face, Dictionary<string, object?> evaluatedProperties)
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

                var scale = (float)(evaluatedProperties.GetDouble(Attributes.InstanceScale) ?? 1);
                var geometryScale = evaluatedProperties.GetVector3D(Attributes.InstanceGeometryScale) ?? new Vector3D(scale, scale, scale);
                var anglesMatrix = evaluatedProperties.GetAngles(Attributes.InstanceAngles)?.ToMatrix() ?? Matrix3x3.Identity;
                var offset = evaluatedProperties.GetVector3D(Attributes.InstanceOffset) ?? new Vector3D();

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
            EvaluateAndUpdateProperties(context, fillEntity, Attributes.MaxInstances, Attributes.Radius, Attributes.RandomSeed, Attributes.FillMode, Attributes.GridOrientation, Attributes.GridGranularity);
            SetBrushEntityOriginProperty(fillEntity);

            var maxInstances = fillEntity.Properties.GetDouble(Attributes.MaxInstances) ?? 0.0;
            var radius = (float)(fillEntity.Properties.GetDouble(Attributes.Radius) ?? 0);
            var randomSeed = fillEntity.Properties.GetInteger(Attributes.RandomSeed) ?? 0;
            var fillMode = (FillMode)(fillEntity.Properties.GetInteger(Attributes.FillMode) ?? 0);

            // Grid snapping settings:
            var gridOrientation = (Orientation)(fillEntity.Properties.GetInteger(Attributes.GridOrientation) ?? 0);    // TODO: Only global & local!
            var gridOrigin = fillEntity.GetOrigin() ?? new Vector3D();  // TODO: For local grid orientation, pick the insertion point of the current context as fallback!!!

            // NOTE: A granularity of 0 (or lower) will disable snapping along that axis.
            var gridGranularity = new Vector3D();
            if (fillEntity.Properties.GetDoubleArray(Attributes.GridGranularity) is double[] granularityArray)
            {
                if (granularityArray.Length == 1)
                    gridGranularity = new Vector3D((float)granularityArray[0], (float)granularityArray[0], (float)granularityArray[0]);
                else if (granularityArray.Length == 2)
                    gridGranularity = new Vector3D((float)granularityArray[0], (float)granularityArray[1], 0);
                else if (granularityArray.Length >= 3)
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
                        var tetrahedron = TakeFromWeightedList(candidateVolumes, random.NextDouble() * totalVolume, candidate => candidate.Volume)?.Tetrahedron;
                        if (tetrahedron == null)
                            continue;

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
                    Evaluation.EvaluateInterpolatedString(fillEntity.Properties.GetString(Attributes.TemplateMap), sequenceContext),
                    Evaluation.EvaluateInterpolatedString(fillEntity.Properties.GetString(Attributes.TemplateName), sequenceContext),
                    sequenceContext);
                if (template == null)
                    return;

                // Evaluating properties again for each instance allows for randomization:
                var evaluatedProperties = fillEntity.Properties.EvaluateToMScriptValues(sequenceContext);

                var orientation = (Orientation)(evaluatedProperties.GetInteger(Attributes.InstanceOrientation) ?? 0);
                var transform = GetTransform(insertionPoint, orientation, evaluatedProperties);
                var insertionContext = new InstantiationContext(template, Logger, transform, evaluatedProperties, sequenceContext, sequenceNumber: sequenceNumber);
                CreateInstance(insertionContext);

                sequenceNumber += 1;
            }

            Transform GetTransform(Vector3D insertionPoint, Orientation orientation, Dictionary<string, object?> evaluatedProperties)
            {
                var rotation = Matrix3x3.Identity;  // Global (macro_fill only supports global and local orientations)
                if (orientation == Orientation.Local)
                    rotation = context.Transform.Rotation;

                var scale = (float)(evaluatedProperties.GetDouble(Attributes.InstanceScale) ?? 1);
                var geometryScale = evaluatedProperties.GetVector3D(Attributes.InstanceGeometryScale) ?? new Vector3D(scale, scale, scale);
                var anglesMatrix = evaluatedProperties.GetAngles(Attributes.InstanceAngles)?.ToMatrix() ?? Matrix3x3.Identity;
                var offset = evaluatedProperties.GetVector3D(Attributes.InstanceOffset) ?? new Vector3D();

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

            // This function creates a brush-copying instance, so it also counts towards the (failsafe) limits:
            _instanceCount += 1;
            if (_instanceCount > Settings.InstanceLimit)
                throw new InvalidOperationException("Instance limit exceeded.");

            if (context.RecursionDepth > Settings.RecursionLimit)
                throw new InvalidOperationException("Recursion limit exceeded.");


            // A macro_brush only creates a single 'instance' of its template, so all properties are evaluated up-front, once:
            SetBrushEntityOriginProperty(brushEntity);
            var evaluatedProperties = brushEntity.Properties.EvaluateToMScriptValues(context);
            evaluatedProperties.UpdateTransformProperties(context.Transform);

            var template = ResolveTemplate(evaluatedProperties.GetString(Attributes.TemplateMap), evaluatedProperties.GetString(Attributes.TemplateName), context);
            if (template == null)
                return;


            // The brushes of this macro_brush entity are copied and given a texture and/or entity attributes
            // based on the world brushes and brush entities in the template. Another way of looking at it is
            // that the brushes and entities in the template take on the 'shape' of this macro_brush.

            // TODO: Transform! A macro_brush can be part of a normal template, and so it should propagate transform to its entities!
            //       Verify that this works correctly now!!!
            var anchorPoint = brushEntity.GetAnchorPoint(brushEntity.Properties.GetEnum<TemplateAreaAnchor>(Attributes.Anchor) ?? TemplateAreaAnchor.Bottom);
            var anchorOffset = evaluatedProperties.GetVector3D(Attributes.InstanceOffset) ?? new Vector3D();

            var transform = new Transform(context.Transform.Scale, context.Transform.GeometryScale, context.Transform.Rotation, context.Transform.Apply(anchorPoint + anchorOffset));
            var pointContext = new InstantiationContext(template, Logger, transform, evaluatedProperties, context);
            var brushContext = new InstantiationContext(template, Logger, Transform.Identity, evaluatedProperties, context, id: pointContext.ID);

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
            foreach (var templateEntity in template.Map.Entities)
            {
                if (excludedObjects.Contains(templateEntity))
                    continue;

                if (!templateEntity.IsPointBased)
                {
                    // Brush entities take on the 'shape' of the macro_brush.

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
                    foreach (var kv in templateEntity.Properties)
                        entityCopy.Properties[kv.Key] = kv.Value;   // NOTE: Expression evaluation is taken care of by HandleEntity.

                    // The copy already has its final orientation, so we don't want it to be transformed again:
                    HandleEntity(brushContext, entityCopy, transformBrushes: false);
                }
                else
                {
                    // Point entities are copied relative to the macro_brush's anchor point:
                    HandleEntity(pointContext, templateEntity);
                }
            }


            IEnumerable<Brush> CopyBrushes(string textureName, bool excludeOriginBrushes = false)
            {
                // Keep the original textures if the template brush is covered with 'NULL':
                var applyTexture = textureName.ToUpper() != Textures.Null;

                foreach (var brush in brushEntity.Brushes)
                {
                    if (excludeOriginBrushes && brush.IsOriginBrush())
                        continue;

                    var copy = brush.Copy(context.Transform);
                    if (applyTexture && !brush.IsOriginBrush())
                    {
                        foreach (var face in copy.Faces)
                            face.TextureName = textureName;
                    }
                    yield return copy;
                }
            }
        }

        private void HandleNormalEntity(InstantiationContext context, Entity normalEntity)
        {
            // Evaluate expressions in both keys and values:
            var evaluatedProperties = normalEntity.Properties.EvaluateToMScriptValues(context);
            var attachedTemplatesPosition = normalEntity.IsPointBased ? normalEntity.Origin : normalEntity.GetOrigin() ?? normalEntity.GetAnchorPoint(TemplateAreaAnchor.Center);

            // Determine whether this entity requires inverted-pitch handling:
            var invertedPitch = false;
            if (Settings.InvertedPitchPredicate != null)
            {
                var evaluationContext = Evaluation.ContextWithBindings(evaluatedProperties, 0, 0, new Random(), "", Logger, BaseEvaluationContext);
                var predicateResult = Evaluation.EvaluateInterpolatedStringOrExpression(Settings.InvertedPitchPredicate, evaluationContext);
                invertedPitch = Interpreter.IsTrue(predicateResult) && !(predicateResult is double d && d == 0);
            }
            evaluatedProperties.UpdateTransformProperties(context.Transform, invertedPitch);
            GetAndRemoveAttachedTemplateAttributes(evaluatedProperties, out var templateMapPaths, out var templateNames);


            normalEntity.Properties.Clear();
            foreach (var kv in evaluatedProperties)
                normalEntity.Properties[kv.Key] = Interpreter.Print(kv.Value);


            Logger.Verbose($"Creating '{normalEntity.ClassName}', using {(invertedPitch ? "inverted" : "normal")} pitch.");
            context.OutputMap.Entities.Add(normalEntity);

            CreateAttachedTemplateInstances(context, attachedTemplatesPosition, evaluatedProperties, templateMapPaths, templateNames);
        }


        /// <summary>
        /// The first part of handling the special <see cref="Attributes.AttachedTemplateMap"/> and <see cref="Attributes.AttachedTemplateName"/> attributes,
        /// this removes said attributes and returns the paths and names of the specified templates (if any).
        /// </summary>
        private void GetAndRemoveAttachedTemplateAttributes(IDictionary<string, object?> evaluatedProperties, out string[] templateMapPaths, out string[] templateNames)
        {
            templateMapPaths = GetStringArray(Attributes.AttachedTemplateMap);
            evaluatedProperties.Remove(Attributes.AttachedTemplateMap);

            templateNames = GetStringArray(Attributes.AttachedTemplateName);
            evaluatedProperties.Remove(Attributes.AttachedTemplateName);

            string[] GetStringArray(string attributeName)
            {
                if (!evaluatedProperties.TryGetValue(attributeName, out var value))
                    return Array.Empty<string>();

                return Interpreter.Print(value)
                    .Split(';')
                    .Select(part => part.Trim())
                    .Where(part => part != "")
                    .ToArray();
            }
        }

        /// <summary>
        /// The second part of handling the special <see cref="Attributes.AttachedTemplateMap"/> and <see cref="Attributes.AttachedTemplateName"/> attributes,
        /// this creates the actual instances.
        /// </summary>
        private void CreateAttachedTemplateInstances(InstantiationContext context, Vector3D position, IDictionary<string, object?> evaluatedProperties, IEnumerable<string> templateMapPaths, IEnumerable<string> templateNames)
        {
            var templates = templateMapPaths.Select(mapPath => ResolveTemplate(mapPath, null, context))
                .Concat(templateNames.Select(templateName => ResolveTemplate(null, templateName, context)))
                .Where(template => template != null)
                .ToArray();

            var transform = new Transform(
                context.Transform.Scale,
                context.Transform.GeometryScale,
                context.Transform.Rotation,
                position);

            foreach (var template in templates)
            {
                var instantiationContext = new InstantiationContext(template!, Logger, transform, evaluatedProperties, context);
                CreateInstance(instantiationContext);
            }
        }


        private static HashSet<object> GetExcludedObjects(InstantiationContext context, ILogger logger)
        {
            var excludedObjects = new HashSet<object>();
            foreach (var conditionalContent in context.Template.ConditionalContents)
            {
                var removal = Evaluation.EvaluateInterpolatedStringOrExpression(conditionalContent.RemovalCondition, context);
                if (Interpreter.IsTrue(removal) && removal is not 0.0)
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

        /// <summary>
        /// Evaluates the values of only the specified properties, and overwrites their original values.
        /// Useful for macro entity properties that must be evaluated once, up-front.
        /// </summary>
        private static void EvaluateAndUpdateProperties(InstantiationContext context, Entity entity, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (entity.Properties.TryGetValue(name, out var value))
                    entity.Properties[name] = Evaluation.EvaluateInterpolatedString(value, context);
            }
        }

        private static TElement? TakeFromWeightedList<TElement>(IEnumerable<TElement> elements, double selection, Func<TElement, double> getWeight)
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


        class MacroExpanderFunctions
        {
            private string _templatesDirectory;
            private string _messDirectory;
            private IDictionary<string, object?> _globals;


            public MacroExpanderFunctions(string templatesDirectory, string messDirectory, IDictionary<string, object?> globals)
            {
                _templatesDirectory = templatesDirectory;
                _messDirectory = messDirectory;
                _globals = globals;
            }


            // Directories:
            public string dir() => _templatesDirectory;  // TODO: Make this obsolete!

            public string templates_dir() => _templatesDirectory;
            public string mess_dir() => _messDirectory;

            // Globals:
            public object? getglobal(string? name) => _globals.TryGetValue(name ?? "", out var value) ? value : null;
            public object? setglobal(string? name, object? value)
            {
                _globals[name ?? ""] = value;
                return value;
            }
            public bool useglobal(string? name)
            {
                if (_globals.TryGetValue(name ?? "", out var value) && value != null)
                    return true;

                _globals[name ?? ""] = 1.0;
                return false;
            }
        }


        class RewriteDirectiveFunctions
        {
            private string _sourceFilePath;


            public RewriteDirectiveFunctions(string sourceFilePath)
            {
                _sourceFilePath = sourceFilePath;
            }


            // Bundle file or directory:
            public string? mtb_dir() => Path.GetDirectoryName(_sourceFilePath);

            // .ted file path:
            public string ted_file() => _sourceFilePath;
        }
    }
}
