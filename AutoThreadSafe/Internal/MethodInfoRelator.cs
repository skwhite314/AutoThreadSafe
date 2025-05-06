using System.Reflection;

namespace AutoThreadSafe.Internal
{
    internal class MethodInfoRelator : MemberInfoRelatorBase<MethodInfo>
    {
        public override int Compare(MethodInfo? x, MethodInfo? y)
        {
            if (object.ReferenceEquals(null, x)) return object.ReferenceEquals(null, y) ? 0 : -1;

            if (object.ReferenceEquals(null, y)) return 1;

            if (object.ReferenceEquals(x,y)) return 0;

            if (x.DeclaringType == null) return y.DeclaringType == null ? 0 : -1;
            if (y.DeclaringType == null) return 1;

            if (!x.DeclaringType.IsAssignableFrom(y.DeclaringType) && !y.DeclaringType.IsAssignableFrom(x.DeclaringType))
            {
                throw new ArgumentException($"Method {x.Name} is defined in type {x.DeclaringType.FullName} which is not in an inheritance hierarchy with method {y.Name} defined in {y.DeclaringType.FullName}");
            }

            var compareResult = x.Name.CompareTo(y.Name);
            if (compareResult != 0) return compareResult;

            var xparams = x.GetParameters().OrderBy(p => p.Position).ToArray();
            var yparams = y.GetParameters().OrderBy(p => p.Position).ToArray();

            if (xparams.Length != yparams.Length) return xparams.Length - yparams.Length;

            for (var i = 0; i < xparams.Length; i++)
            {
                if (xparams[i].ParameterType != yparams[i].ParameterType) return xparams[i].GetHashCode() - yparams[i].GetHashCode();

                var xflags = xparams[i].ConvertParameterInfoFlagsToInt();
                var yflags = yparams[i].ConvertParameterInfoFlagsToInt();

                if (xflags != yflags) return xflags - yflags;
            }

            MethodInfo subClassMethodInfo, baseClassMethodInfo;

            var baseIsX = false;
            if (x.DeclaringType.IsAssignableFrom(y.DeclaringType))
            {
                subClassMethodInfo = y;
                baseClassMethodInfo = x;
                baseIsX = true;
            }
            else
            {
                subClassMethodInfo = x;
                baseClassMethodInfo = y;
            }

            do
            {
                subClassMethodInfo = subClassMethodInfo.GetBaseDefinition();
            }
            while (!baseClassMethodInfo.DeclaringType.IsAssignableFrom(subClassMethodInfo.DeclaringType));

            if (baseClassMethodInfo == subClassMethodInfo)
            {
                return 0;
            }

            return baseIsX ? 1 : -1;
        }
    }
}
