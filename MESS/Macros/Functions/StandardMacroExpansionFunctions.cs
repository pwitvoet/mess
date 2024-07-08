using MESS.Common;
using MESS.Logging;
using MScript.Evaluation;

namespace MESS.Macros.Functions
{
    public class StandardMacroExpansionFunctions
    {
        private Random _random;
        private IDictionary<string, object?> _properties;
        private string _mapPath;
        private ILogger _logger;


        public StandardMacroExpansionFunctions(IDictionary<string, object?> properties, string mapPath, Random random, ILogger logger)
        {
            _random = random;
            _properties = properties;
            _mapPath = mapPath;
            _logger = logger;
        }

        // Randomness:
        public double rand(double? min = null, double? max = null, double? step = null)
        {
            if (min == null && max == null)     // rand()
                return GetRandomDouble(0, 1);

            min = min ?? 0.0;
            max = max ?? 0.0;
            var lower = Math.Min(min.Value, max.Value);
            var upper = Math.Max(min.Value, max.Value);

            if (step == null)
            {
                // rand(max)
                // rand(min, max)
                return GetRandomDouble(lower, upper);
            }
            else
            {
                // rand(min, max, step)
                var stepSize = Math.Abs(step.Value);
                var range = upper - lower;
                var steps = Math.Floor(range / stepSize);
                if (steps != range / stepSize)
                    steps += 1;

                return lower + GetRandomInteger(0, (int)steps) * stepSize;
            }
        }

        public double randi(double? min = null, double? max = null, double? step = null)
        {
            if (min == null && max == null)     // randi()
                return GetRandomInteger(0, 2);

            min = min ?? 0.0;
            max = max ?? 0.0;
            var lower = (int)Math.Min(min.Value, max.Value);
            var upper = (int)Math.Max(min.Value, max.Value);

            if (step == null)
            {
                // randi(max)
                // randi(min, max)
                return GetRandomInteger((int)Math.Min(min.Value, max.Value), (int)Math.Max(min.Value, max.Value));
            }
            else
            {
                // rand(min, max, step)
                var stepSize = (int)Math.Abs(step.Value);
                var range = upper - lower;
                var steps = (range / stepSize) + 1;
                return lower + GetRandomInteger(0, steps) * stepSize;
            }
        }

        public object? randitem(object?[] array, object?[]? weights = null)
        {
            if (weights == null || !weights.Any())
                return array[GetRandomInteger(0, array.Length)];

            var numericalWeights = weights
                .Take(array.Length)
                .Select(weight => weight is double number && number >= 0.0 ? number : 0.0)
                .ToArray();
            var totalWeight = numericalWeights.Sum();
            if (totalWeight == 0)
                return null;

            var value = GetRandomDouble(0, totalWeight);
            var index = 0;
            for (; index < numericalWeights.Length; index++)
            {
                if (numericalWeights[index] == 0)
                    continue;

                value -= numericalWeights[index];
                if (value < 0)
                    break;
            }
            return array[index];
        }

        // Parent entity attributes:
        public double attr_count() => _properties.Count;

        public object? get_attr(object? index_or_name = null)
        {
            if (index_or_name is double index)
            {
                var normalizedIndex = (int)index < 0 ? _properties.Count + (int)index : (int)index;
                if (normalizedIndex < 0 || normalizedIndex >= _properties.Count)
                    return null;

                var property = _properties.ToArray()[normalizedIndex];
                return new MObject(new Dictionary<string, object?> {
                        { "key", property.Key },
                        { "value", property.Value },
                    });
            }
            else if (index_or_name is string name)
            {
                if (!_properties.TryGetValue(name, out var value))
                    return null;

                return new MObject(new Dictionary<string, object?> {
                        { "key", name },
                        { "value", value },
                    });
            }
            else
            {
                return _properties
                    .Select(property => new MObject(new Dictionary<string, object?> {
                            { "key", property.Key },
                            { "value", property.Value },
                    }))
                    .ToArray();
            }
        }

        // Flags:
        public bool hasflag(double flag, double? flags = null)
        {
            if (flags == null)
                flags = _properties.TryGetValue(Attributes.Spawnflags, out var val) && val is double d ? d : 0;

            var bit = (int)flag;
            if (bit < 0 || bit > 31)
                return false;

            return (((int)flags >> bit) & 1) == 1;
        }

        public double setflag(double flag, double? set = 1, double? flags = null)
        {
            if (flags == null)
                flags = _properties.TryGetValue(Attributes.Spawnflags, out var val) && val is double d ? d : 0;

            if (set != 0)
                return (int)flags | (1 << (int)flag);
            else
                return (int)flags & ~(1 << (int)flag);
        }

        // Current map path:
        public string map_path() => _mapPath;

        public string map_dir() => Path.GetDirectoryName(_mapPath) ?? "";


        private double GetRandomDouble(double min, double max) => min + _random.NextDouble() * (max - min);

        private int GetRandomInteger(int min, int max) => _random.Next(min, max);
    }
}
