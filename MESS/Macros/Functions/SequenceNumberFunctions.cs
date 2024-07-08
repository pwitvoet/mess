using MESS.Logging;
using MScript;

namespace MESS.Macros.Functions
{
    public class SequenceNumberFunctions
    {
        private double _id;
        private double _sequenceNumber;
        private ILogger _logger;


        public SequenceNumberFunctions(double id, double sequenceNumber, ILogger logger)
        {
            _id = id;
            _sequenceNumber = sequenceNumber;
            _logger = logger;
        }


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
