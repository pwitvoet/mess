using System;

namespace MScript.Evaluation.Types
{
    class PropertyDescriptor : MemberDescriptor, IEquatable<PropertyDescriptor>
    {
        private Func<object, object> _getter;


        public PropertyDescriptor(string name, TypeDescriptor type, Func<object, object> getter)
            : base(name, type)
        {
            _getter = getter;
        }

        public override object GetValue(object obj) => _getter(obj);


        public override bool Equals(object obj) => obj is PropertyDescriptor other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

        public override string ToString() => $"<PROPERTY: {Name}>";

        public override bool Equals(MemberDescriptor other) => Equals(other as PropertyDescriptor);

        public bool Equals(PropertyDescriptor other)
        {
            return !(other is null) &&
                Name == other.Name;
        }


        public static object operator ==(PropertyDescriptor left, PropertyDescriptor right) => left?.Equals(right) ?? false;
        public static object operator !=(PropertyDescriptor left, PropertyDescriptor right) => !(left?.Equals(right) ?? false);
    }
}
