namespace MScript.Evaluation.Types
{
    /// <summary>
    /// Describes an MScript type.
    /// </summary>
    public class TypeDescriptor : IEquatable<TypeDescriptor>
    {
        public string Name { get; }


        private IDictionary<string, MemberDescriptor> _members;


        public TypeDescriptor(string name, params MemberDescriptor[] members)
        {
            Name = name;
            _members = members?.ToDictionary(member => member.Name, member => member) ?? new Dictionary<string, MemberDescriptor>();
        }

        public PropertyDescriptor? GetProperty(string name) => GetMember(name) as PropertyDescriptor;

        public MethodDescriptor? GetMethod(string name) => GetMember(name) as MethodDescriptor;

        public MemberDescriptor? GetMember(string name) => _members.TryGetValue(name, out var member) ? member : null;


        internal void AddMember(MemberDescriptor member) => _members[member.Name] = member;


        public override bool Equals(object? obj) => obj is TypeDescriptor other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

        public override string ToString() => $"<{Name}>";

        public bool Equals(TypeDescriptor? other)
        {
            return other is not null &&
                Name == other.Name;
        }


        public static bool operator ==(TypeDescriptor left, TypeDescriptor right) => left?.Equals(right) ?? false;
        public static bool operator !=(TypeDescriptor left, TypeDescriptor right) => !(left?.Equals(right) ?? false);


        public static TypeDescriptor GetType(object? value) => value switch
        {
            null => BaseTypes.None,
            double _ => BaseTypes.Number,
            object?[] _ => BaseTypes.Array,
            string _ => BaseTypes.String,
            MObject _ => BaseTypes.Object,
            IFunction _ => BaseTypes.Function,

            _ => throw new InvalidOperationException($"Unknown value type: {value.GetType().FullName}."),
        };
    }
}
