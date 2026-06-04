namespace HotspotMaker.Util
{
    public static class ReflectionExtensions
    {
        public static object? GetValue(this object obj, string propertyName)
        {
            if (obj is null)
                return null;

            var type = obj.GetType();
            var property = type.GetProperty(propertyName);
            if (property is null)
                return null;

            return property.GetValue(obj);
        }
    }
}
