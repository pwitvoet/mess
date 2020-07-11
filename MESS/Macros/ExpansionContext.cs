using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MESS.Macros
{
    public class ExpansionContext
    {
        public int ID { get; }
        public MapTemplate Template { get; }
        public string CurrentWorkingDirectory { get; }
        public IReadOnlyCollection<MapTemplate> SubTemplates { get; }

        private ExpansionContext _parentContext;
        private IDictionary<string, string> _substitutionValues;

        private Random _random;
        private Random Random
        {
            get
            {
                if (_random == null)
                    _random = (_substitutionValues.TryGetValue("random_seed", out var seedValue) && int.TryParse(seedValue, out var seed)) ? new Random(seed) : new Random();

                return _random;
            }
        }

        private int _nextID = 1;


        public ExpansionContext(MapTemplate template, IDictionary<string, string> substitutionValues = null, ExpansionContext parentContext = null)
        {
            _parentContext = parentContext;
            _substitutionValues = substitutionValues?.ToDictionary(kv => kv.Key.ToUpper(), kv => kv.Value) ?? new Dictionary<string, string>();

            ID = GetRootContext()._nextID++;
            Template = template;
            CurrentWorkingDirectory = Path.GetDirectoryName(GetNearestMapFileContext().Template.Name);
            SubTemplates = GetNearestMapFileContext().Template.SubTemplates;
        }

        /// <summary>
        /// Evaluates the given string. Expression parts are delimited by curly braces. For example: "name{1 + 2}" evaluates to "name3".
        /// Note that identifiers are case-insensitive: both 'name' and 'NAME' refer to the same variable.
        /// </summary>
        public string Evaluate(string input)
        {
            if (input == null)
                return null;

            // TODO: When this expression language is expanded to contain strings, we'll need to improve parsing to skip curly braces inside strings:
            //       (and of course we'd need to have an actual expression parser!)
            return Regex.Replace(input, @"{(?<expression>[^}]+)}", match =>
            {
                var expression = (match.Groups["expression"].Value ?? "").Trim().ToUpper();

                // TODO: Replace this with a proper expression evaluation system! But for now, checking a few hard-coded function names will do...
                if (expression == "ID()")
                    return _substitutionValues.TryGetValue("targetname", out var targetname) ? targetname : ID.ToString();

                return _substitutionValues.TryGetValue(expression, out var value) ? value : "";
            });
        }

        // NOTE: Both min and max are INCLUSIVE, so (0, 1) can return both 0 and 1!
        public int GetRandomInteger(int min, int max) => Random.Next(min, max + 1);

        // NOTE: Here, max is not inclusive... make this stuff more consistent!
        public float GetRandomFloat(float min, float max) => (float)(min + Random.NextDouble() * (max - min));


        private ExpansionContext GetRootContext() => _parentContext?.GetRootContext() ?? this;

        // NOTE: Returns the first parent context who'se template is not a sub-template but a template loaded from a file:
        private ExpansionContext GetNearestMapFileContext() => !Template.IsSubTemplate ? this : _parentContext?.GetNearestMapFileContext();
    }
}
