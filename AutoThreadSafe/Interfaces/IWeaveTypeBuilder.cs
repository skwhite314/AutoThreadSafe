using AutoThreadSafe.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AutoThreadSafe.Interfaces
{
    public interface IWeaveTypeBuilder<TType> where TType : class?
    {
        public string WovenAssemblyName { get; }

        public string WovenClassName { get; }

        public InitialWeaveType InitialWeaveType { get; }

        public bool IsWoven { get; }

        public Type WovenType { get; }

        public ConstructorInfo[] Constructors { get; }

        public ILockInfo? GetLockInfo(Guid guid);

        public ILockInfo? GetLockInfo(MethodInfo? methodInfo);

        public ILockInfo? GetLockInfo(PropertyInfo? propertyInfo);

        public ILockInfo? GetLockInfo(MemberInfo? memberInfo);

        public void AddLockInfo([DisallowNull] ILockInfo lockInfo, bool throwOnMembersAlreadyPresent = false);

        public void RemoveLockInfo([DisallowNull] ILockInfo lockInfo);

        public void RemoveLockInfo(Guid id);

        public void ClearLockInfo();

        public void ResetLockInfo();

        public void WeaveType();
    }
}
