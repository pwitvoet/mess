using MScript.Evaluation.Types;

namespace MScript.Evaluation
{
    public class Parameter
    {
        public string Name { get; }
        public TypeDescriptor Type { get; }
        // TODO: Optional parameters & default values?

        public Parameter(string name, TypeDescriptor type)
        {
            Name = name;
            Type = type;
        }
    }
}
