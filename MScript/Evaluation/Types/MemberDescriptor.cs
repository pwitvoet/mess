using System;

namespace MScript.Evaluation.Types
{
    abstract class MemberDescriptor : IEquatable<MemberDescriptor>
    {
        public string Name { get; }
        public TypeDescriptor Type { get; }


        public MemberDescriptor(string name, TypeDescriptor type)
        {
            Name = name;
            Type = type;
        }

        public abstract object GetValue(object obj);


        public override bool Equals(object obj) => obj is MemberDescriptor other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

        public override string ToString() => $"<MEMBER: {Name}>";

        public abstract bool Equals(MemberDescriptor other);


        public static object operator ==(MemberDescriptor left, MemberDescriptor right) => left?.Equals(right) ?? false;
        public static object operator !=(MemberDescriptor left, MemberDescriptor right) => !(left?.Equals(right) ?? false);
    }
}
