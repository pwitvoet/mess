using System.Collections.Generic;
using System.Linq;

namespace MScript
{
    public class EvaluationContext
    {
        private IDictionary<string, object> _bindings;


        public EvaluationContext(IDictionary<string, object> bindings = null)
        {
            _bindings = bindings?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, object>();
        }

        public void Bind(string name, object value) => _bindings[name] = value;

        public object Resolve(string name) => _bindings.TryGetValue(name, out var value) ? value : null;
    }
}
