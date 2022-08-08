using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics;
using MScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MESS.Macros
{
    /// <summary>
    /// Template instantiation always happens within a certain context.
    /// </summary>
    public class InstantiationContext
    {
        /// <summary>
        /// Each template instantiation is given a unique ID. This is available to MScript expressions via the 'id()' function.
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// The number of instances that have already been created by the macro entity that is creating this instance.
        /// This is available to MScript expressions via the 'nth()' function.
        /// </summary>
        public int SequenceNumber { get; }

        /// <summary>
        /// The recursion depth of this context. The root context is at depth 0.
        /// </summary>
        public int RecursionDepth { get; }

        /// <summary>
        /// The template that is being instantiated.
        /// </summary>
        public MapTemplate Template { get; }

        /// <summary>
        /// The folder in which the current template map is located. Used to resolve relative template map paths.
        /// </summary>
        public string CurrentWorkingDirectory { get; }

        /// <summary>
        /// If the current template is loaded from a map file, then these are its sub-templates
        /// (any areas defined by a <see cref="MacroEntity.Template"/>).
        /// If the current template is a sub-template, then these are its 'sibling' sub-templates.
        /// </summary>
        public IReadOnlyCollection<MapTemplate> SubTemplates { get; }

        /// <summary>
        /// Template contents are copied into this map.
        /// </summary>
        public Map OutputMap { get; }

        /// <summary>
        /// Template contents are transformed when they are copied into the output map.
        /// </summary>
        public Transform Transform { get; }

        /// <summary>
        /// Storage for global MScript variables, used by the 'getglobal' and 'setglobal' functions.
        /// </summary>
        public IDictionary<string, object> Globals { get; }


        private ILogger _logger;
        private Random _random;
        private IDictionary<string, string> _insertionEntityProperties;
        private InstantiationContext _parentContext;
        private EvaluationContext _evaluationContext;

        private int _nextID = 1;


        public InstantiationContext(
            MapTemplate template,
            ILogger logger,
            Transform transform = null,
            IDictionary<string, string> insertionEntityProperties = null,
            InstantiationContext parentContext = null,
            string workingDirectory = null,
            IDictionary<string, object> globals = null,
            int sequenceNumber = 0)
        {
            // Every context uses its own PRNG. Seeding is done automatically, but can be done explicitly
            // by adding a 'random_seed' attribute to the inserting entity (or to the map properties, for the root context).
            // NOTE: even with explicit seeding, a random value is always obtained from the parent context.
            //       This ensures that switching between explicit and implicit seeding does not result in 'sibling' contexts
            //       getting different seed values.
            var randomSeed = parentContext?._random.Next() ?? 0;
            if (insertionEntityProperties?.GetNumericProperty(Attributes.RandomSeed) is double seed)
                randomSeed = (int)seed;
            _random = new Random(randomSeed);
            _logger = logger;
            _insertionEntityProperties = insertionEntityProperties;
            _parentContext = parentContext;

            ID = GetRootContext()._nextID++;
            SequenceNumber = sequenceNumber;
            RecursionDepth = (parentContext?.RecursionDepth ?? -1) + 1;
            Template = template;
            CurrentWorkingDirectory = workingDirectory ?? Path.GetDirectoryName(GetNearestMapFileContext().Template.Name);
            SubTemplates = GetNearestMapFileContext().Template.SubTemplates;

            Transform = transform ?? Transform.Identity;
            Globals = globals ?? parentContext?.Globals ?? new Dictionary<string, object>();

            var outerEvaluationContext = Evaluation.ContextFromProperties(insertionEntityProperties, ID, SequenceNumber, _random, Globals, _logger);
            var evaluatedTemplateProperties = template.Map.Properties.ToDictionary(
                kv => Evaluation.EvaluateInterpolatedString(kv.Key, outerEvaluationContext),
                kv => PropertyExtensions.ParseProperty(Evaluation.EvaluateInterpolatedString(kv.Value, outerEvaluationContext)));
            _evaluationContext = new EvaluationContext(evaluatedTemplateProperties, outerEvaluationContext);

            // Every instantiation is written to the same map, but with a different transform:
            OutputMap = parentContext?.OutputMap;
            if (OutputMap == null)
            {
                // Copy original map properties:
                OutputMap = new Map();
                foreach (var kv in evaluatedTemplateProperties)
                    OutputMap.Properties[kv.Key] = Interpreter.Print(kv.Value);
            }
        }

        private InstantiationContext(InstantiationContext parentContext, int sequenceNumber)
        {
            _random = parentContext._random;
            _insertionEntityProperties = parentContext._insertionEntityProperties;
            _parentContext = parentContext;

            ID = parentContext.ID;
            SequenceNumber = sequenceNumber;
            RecursionDepth = parentContext.RecursionDepth;
            Template = parentContext.Template;
            CurrentWorkingDirectory = parentContext.CurrentWorkingDirectory;
            SubTemplates = parentContext.SubTemplates;
            Transform = parentContext.Transform;
            Globals = parentContext.Globals;
            OutputMap = parentContext.OutputMap;

            _evaluationContext = Evaluation.ContextFromProperties(_insertionEntityProperties, ID, SequenceNumber, _random, Globals, _logger, parentContext._evaluationContext);
        }


        /// <summary>
        /// Calls <see cref="Evaluation.EvaluateInterpolatedString(string, EvaluationContext)"/>, using this instance's evaluation context.
        /// </summary>
        public string EvaluateInterpolatedString(string interpolatedString) => Evaluation.EvaluateInterpolatedString(interpolatedString, _evaluationContext);

        /// <summary>
        /// Calls <see cref="Evaluation.EvaluateExpression(string, EvaluationContext)"/>, using this instance's evaluation context.
        /// </summary>
        public object EvaluateExpression(string expression) => Evaluation.EvaluateExpression(expression, _evaluationContext);

        /// <summary>
        /// Returns a random double. Min is inclusive, max is exclusive.
        /// </summary>
        public double GetRandomDouble(double min, double max) => (min + _random.NextDouble() * (max - min));

        public InstantiationContext GetChildContextWithSequenceNumber(int sequenceNumber) => new InstantiationContext(this, sequenceNumber);


        private InstantiationContext GetRootContext() => _parentContext?.GetRootContext() ?? this;

        // NOTE: Returns the first parent context who'se template is not a sub-template but a template loaded from a file:
        private InstantiationContext GetNearestMapFileContext() => !Template.IsSubTemplate ? this : _parentContext?.GetNearestMapFileContext();
    }
}
