using AutoThreadSafe.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace AutoThreadSafe.Internal
{
    internal class LockInfoComparer : IComparer<ILockInfo>, IEqualityComparer<ILockInfo>
    {
        public int Compare(ILockInfo? x, ILockInfo? y) =>
            x == null
            ? (y == null ? 0 : -y.CompareTo(x))
            : x.CompareTo(y);

        public bool Equals(ILockInfo? x, ILockInfo? y) =>
            x == null
            ? y == null
            : x.Equals(y);

        public int GetHashCode([DisallowNull] ILockInfo obj) => obj.GetHashCode();
    }
}
