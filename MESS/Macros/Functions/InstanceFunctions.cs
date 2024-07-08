using MESS.Common;
using MESS.Logging;
using MScript;
using System.Globalization;

namespace MESS.Macros.Functions
{
    public class InstanceFunctions
    {
        private double _id;
        private double _parentID;
        private double _sequenceNumber;
        private IDictionary<string, object?> _properties;
        private ILogger _logger;


        public InstanceFunctions(double id, double parentID, double sequenceNumber, IDictionary<string, object?> properties, ILogger logger)
        {
            _id = id;
            _parentID = parentID;
            _sequenceNumber = sequenceNumber;
            _properties = properties;
            _logger = logger;
        }


        // Entity ID:
        public string id()
        {
            if (_properties.TryGetValue(Attributes.Targetname, out var targetname))
            {
                var name = Interpreter.Print(targetname);
                if (name != "")
                    return name;
            }

            return _id.ToString(CultureInfo.InvariantCulture);
        }

        public double iid() => _id;

        public double parentid() => _parentID;

        // Enumeration:
        public double nth() => _sequenceNumber;

        // Debugging:
        public object? trace(object? value, string? message = null)
        {
            _logger.Info($"'{Interpreter.Print(value)}' ('{message}', trace from instance: #{_id}, sequence number: #{_sequenceNumber}).");
            return value;
        }
    }
}
