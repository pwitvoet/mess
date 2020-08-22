using MESS.Mapping;
using MESS.Mathematics;
using MESS.Mathematics.Spatial;
using MScript;
using MScript.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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


        private InstantiationContext _parentContext;
        private EvaluationContext _evaluationContext;
        private Random _random;

        private int _nextID = 1;


        // NOTE: This regex takes into account that strings inside expressions can contain curly braces:
        private static Regex _expressionRegex = new Regex(@"{(?<expression>(('[^']*')|[^}'])*)}");


        public InstantiationContext(
            MapTemplate template,
            Transform transform = null,
            IDictionary<string, string> insertionEntityProperties = null,
            InstantiationContext parentContext = null)
        {
            _parentContext = parentContext;
            _evaluationContext = new EvaluationContext(insertionEntityProperties?.ToDictionary(
                kv => kv.Key,
                kv => Entity.ParseProperty(kv.Value)));
            RegisterContextFunctions();

            ID = GetRootContext()._nextID++;
            RecursionDepth = (parentContext?.RecursionDepth ?? -1) + 1;
            Template = template;
            CurrentWorkingDirectory = Path.GetDirectoryName(GetNearestMapFileContext().Template.Name);
            SubTemplates = GetNearestMapFileContext().Template.SubTemplates;

            // Every instantiation is written to the same map, but with a different transform:
            OutputMap = parentContext?.OutputMap ?? new Map();
            Transform = transform ?? Transform.Identity;

            // Every context uses its own PRNG. Seeding is done automatically, but can be done explicitly
            // by adding a 'random_seed' attribute to the inserting entity (or to the map properties, for the root context).
            // NOTE: even with explicit seeding, a random value is always obtained from the parent context.
            //       This ensures that switching between explicit and implicit seeding does not result in 'sibling' contexts
            //       getting different seed values.
            var randomSeed = parentContext?._random.Next() ?? 0;
            if (insertionEntityProperties != null &&
                insertionEntityProperties.TryGetValue("random_seed", out var value) &&
                double.TryParse(value, out var doubleValue))
            {
                randomSeed = (int)doubleValue;
            }
            _random = new Random(randomSeed);
        }

        /// <summary>
        /// Evaluates the given interpolated string. Expression parts are delimited by curly braces.
        /// For example: "name{1 + 2}" evaluates to "name3".
        /// Note that identifiers are case-sensitive: 'name' and 'NAME' do not refer to the same variable.
        /// </summary>
        public string EvaluateInterpolatedString(string interpolatedString)
        {
            if (interpolatedString == null)
                return null;

            return _expressionRegex.Replace(interpolatedString, match =>
            {
                var expression = match.Groups["expression"].Value;
                var result = EvaluateExpression(expression);
                return Interpreter.Print(result);
            });
        }

        /// <summary>
        /// Evaluates the given expression and returns the resulting value.
        /// </summary>
        public object EvaluateExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression?.Trim()))
                return null;

            return Interpreter.Evaluate(expression, _evaluationContext);
        }

        /// <summary>
        /// Returns true if the given string contains one or more expressions (delimited by curly braces).
        /// </summary>
        public static bool ContainsExpressions(string interpolatedString)
            => _expressionRegex.IsMatch(interpolatedString);


        /// <summary>
        /// Returns a random integer. Min is inclusive, max is exclusive.
        /// </summary>
        public int GetRandomInteger(int min, int max) => _random.Next(min, max);

        /// <summary>
        /// Returns a random double. Min is inclusive, max is exclusive.
        /// </summary>
        public double GetRandomDouble(double min, double max) => (min + _random.NextDouble() * (max - min));


        private InstantiationContext GetRootContext() => _parentContext?.GetRootContext() ?? this;

        // NOTE: Returns the first parent context who'se template is not a sub-template but a template loaded from a file:
        private InstantiationContext GetNearestMapFileContext() => !Template.IsSubTemplate ? this : _parentContext?.GetNearestMapFileContext();

        private void RegisterContextFunctions()
        {
            _evaluationContext.Bind("id", new NativeFunction(Array.Empty<Parameter>(), GetInstanceID));

            // TODO: Make these arguments optional? rand() --> 0-1, rand(max) -> 0-max, rand(min,max) -> min-max
            _evaluationContext.Bind("rand", new NativeFunction(new[] {
                new Parameter("min", BaseTypes.Number),
                new Parameter("max", BaseTypes.Number),
            }, GetRandomDouble));

            // TODO: Same here -- optional args!
            _evaluationContext.Bind("randi", new NativeFunction(new[] {
                new Parameter("min", BaseTypes.Number),
                new Parameter("max", BaseTypes.Number),
            }, GetRandomInteger));
        }

        private object GetInstanceID(object[] arguments, EvaluationContext context)
            => context.Resolve("targetname") ?? ID;

        private object GetRandomDouble(object[] arguments, EvaluationContext context)
            => GetRandomDouble((double)arguments[0], (double)arguments[1]);

        private object GetRandomInteger(object[] arguments, EvaluationContext context)
            => (double)GetRandomInteger((int)((double)arguments[0]), (int)((double)arguments[1]));  // NOTE: MScript only uses doubles, not ints.
    }
}
