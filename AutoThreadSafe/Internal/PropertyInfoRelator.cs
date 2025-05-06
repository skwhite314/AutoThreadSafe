using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AutoThreadSafe.Internal
{
    internal class PropertyInfoRelator : MemberInfoRelatorBase<PropertyInfo>
    {
        public override int Compare([AllowNull] PropertyInfo x, [AllowNull] PropertyInfo y)
        {
            if (x == null)
            {
                return y == null ? 0 : -1;
            }

            if (y == null)
            {
                return 1;
            }

            if (x == y)
            {
                return 0;
            }

            if (x.DeclaringType == null) return y.DeclaringType == null ? 0 : -1;
            if (y.DeclaringType == null) return 1;

            if (x.DeclaringType == y.DeclaringType)
            {
                return x.GetHashCode() - y.GetHashCode();
            }

            PropertyInfo subInfo, baseInfo;

            if (x.DeclaringType.IsAssignableFrom(y.DeclaringType))
            {
                subInfo = y;
                baseInfo = x;
            }
            else
            {
                subInfo = x;
                baseInfo = y;
            }

            if (subInfo.DeclaringType.BaseType == null || subInfo.DeclaringType.BaseType != baseInfo.DeclaringType)
            {
                return x.GetHashCode() - y.GetHashCode();
            }

            var baseProperty = subInfo.DeclaringType.BaseType.GetProperty(subInfo.Name, subInfo.PropertyType);

            if (baseProperty == null || baseProperty != baseInfo)
            {
                return x.GetHashCode() - y.GetHashCode();
            }

            return 0;
        }
    }
}
