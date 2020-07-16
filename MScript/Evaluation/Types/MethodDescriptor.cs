using System;

namespace MScript.Evaluation.Types
{
    class MethodDescriptor : MemberDescriptor, IEquatable<MethodDescriptor>
    {
        private IFunction _function;


        public MethodDescriptor(string name, TypeDescriptor type, IFunction function)
            : base(name, type)
        {
            _function = function;
        }

        public override object GetValue(object obj) => new BoundMethod(obj, _function);


        public override bool Equals(object obj) => obj is PropertyDescriptor other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

        public override string ToString() => $"<METHOD: {Name}>";

        public override bool Equals(MemberDescriptor other) => Equals(other as MethodDescriptor);

        public bool Equals(MethodDescriptor other)
        {
            return !(other is null) &&
                Name == other.Name;
        }


        public static object operator ==(MethodDescriptor left, MethodDescriptor right) => left?.Equals(right) ?? false;
        public static object operator !=(MethodDescriptor left, MethodDescriptor right) => !(left?.Equals(right) ?? false);
    }
}
