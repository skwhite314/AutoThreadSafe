using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AutoThreadSafe.Interfaces
{
    public interface ILockInfo : IEquatable<ILockInfo>, IComparable<ILockInfo>
    {
        public Guid Id { get; }

        public string LockObjectName { get; }

        public MethodInfo[] MethodInfos { get; }

        public PropertyInfo[] PropertyInfos { get; }

        public bool IsStatic { get; }

        public bool IsEmpty();

        public bool HasMethod([DisallowNull] MethodInfo methodInfo);

        public void AddMethod([DisallowNull] MethodInfo methodInfo);

        public void AddMethods([DisallowNull] MethodInfo[] methodInfos);

        public void RemoveMethod([DisallowNull] MethodInfo methodInfo);

        public bool HasProperty([DisallowNull]  PropertyInfo propertyInfo);

        public void AddProperty([DisallowNull] PropertyInfo propertyInfo);

        public void AddProperties([DisallowNull] PropertyInfo[] propertyInfos);

        public void RemoveProperty([DisallowNull] PropertyInfo propertyInfo);
    }
}
