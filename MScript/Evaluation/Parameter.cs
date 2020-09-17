using MScript.Evaluation.Types;

namespace MScript.Evaluation
{
    public class Parameter
    {
        public string Name { get; }
        public TypeDescriptor Type { get; }
        public bool IsOptional { get; }
        public object DefaultValue { get; }


        public Parameter(string name, TypeDescriptor type, bool isOptional = false, object defaultValue = null)
        {
            Name = name;
            Type = type;
            IsOptional = isOptional;
            DefaultValue = defaultValue;
        }
    }
}
