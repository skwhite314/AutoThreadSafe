using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AutoThreadSafe.Internal
{
    internal abstract class MemberInfoRelatorBase<TMemberInfo> : IEqualityComparer<TMemberInfo>, IComparer<TMemberInfo> where TMemberInfo : MemberInfo
    {
        public abstract int Compare(TMemberInfo? x, TMemberInfo? y);

        public bool Equals([AllowNull] TMemberInfo x, [AllowNull] TMemberInfo y)
        {
            return this.Compare(x, y) == 0;
        }

        public int GetHashCode([DisallowNull] TMemberInfo obj)
        {
            return obj.GetHashCode();
        }
    }
}
