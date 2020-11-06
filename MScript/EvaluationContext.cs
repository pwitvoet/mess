using System.Collections.Generic;
using System.Linq;

namespace MScript
{
    public class EvaluationContext
    {
        private EvaluationContext _parentContext;
        private IDictionary<string, object> _bindings;


        public EvaluationContext(IDictionary<string, object> bindings = null, EvaluationContext parentContext = null)
        {
            _bindings = bindings?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, object>();
            _parentContext = parentContext;
        }

        public void Bind(string name, object value) => _bindings[name] = value;

        public object Resolve(string name) => _bindings.TryGetValue(name, out var value) ? value : _parentContext?.Resolve(name);
    }
}
