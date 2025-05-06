using System.Reflection;

namespace AutoThreadSafe.Exceptions
{
    public class MemberIntersectionException : AutoThreadSafeException
    {
        public MethodInfo[] Methods { get; }

        public PropertyInfo[] Properties { get; }

        public MemberIntersectionException(string message, IEnumerable<MethodInfo>? methods = null, IEnumerable<PropertyInfo>? properties = null) : base(message)
        {
            this.Methods = methods?.ToArray() ?? Array.Empty<MethodInfo>();
            this.Properties = properties?.ToArray() ?? Array.Empty<PropertyInfo>();
        }
    }
}
