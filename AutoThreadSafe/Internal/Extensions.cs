using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AutoThreadSafe.Internal
{
    internal static class Extensions
    {
        private const BindingFlags TargetBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly PropertyInfo[] _parameterInfoFlags = typeof(ParameterInfo).GetProperties(BindingFlags.Instance)
                                                                                          .Where(propertyInfo => propertyInfo.PropertyType == typeof(bool))
                                                                                          .OrderBy(propertyInfo => propertyInfo.Name, StringComparer.Ordinal)
                                                                                          .ToArray();

        public static int ConvertParameterInfoFlagsToInt(this ParameterInfo parameterInfo)
        {
            var boolArray = _parameterInfoFlags.Select(propertyInfo => (bool)(propertyInfo.GetValue(parameterInfo) ?? false)).ToArray();
            var result = boolArray.Select((b, i) => b ? 1 << i : 0).Sum();

            return result;
        }

        public static int CompareArray<T>([DisallowNull] this T[] array1, [DisallowNull] T[] array2, [DisallowNull] IComparer<T> comparer)
        {
            if (array1.Length != array2.Length) return array1.Length - array2.Length;

            for (var i = 0; i < array1.Length; i++)
            {
                var comp = comparer.Compare(array1[i], array2[i]);
                if (comp != 0) return comp;
            }

            return 0;
        }

        public static bool IsEmpty<T>([DisallowNull] this IEnumerable<T> items) => !items.Any();

        public static ConstructorInfo[] GetUseableConstructors(this Type type) => 
            type.GetConstructors(TargetBindingFlags).Where(ci => (ci.IsFamily || ci.IsPublic) && !ci.IsAssembly).ToArray();

        public static MethodInfo[] GetOverrideableMethods(this Type type)
        {
            // TODO: Consider removing !mi.IsAssembly; we want in-assembly calls to methods to still be thread safe

            var result = type.GetMethods(TargetBindingFlags).Where(mi =>
                                                                mi.IsVirtual &&
                                                                !mi.IsFinal &&
                                                                !mi.IsAssembly &&
                                                                !mi.IsSpecialName &&
                                                                (mi.IsFamily || mi.IsPublic))
                                                            .ToArray(); 

            return result;
        }

        public static PropertyInfo[] GetOverrideableProperties(this Type type)
        {
            // TODO: Consider removing !pa.IsAssembly; we want in-assembly references to properties to still be thread safe

            var result = type.GetProperties(TargetBindingFlags).Where(pi => pi.GetAccessors(true).Any(pa => pa.IsVirtual &&
                                                                                                            !pa.IsFinal &&
                                                                                                            !pa.IsAssembly &&
                                                                                                            (pa.IsFamily || pa.IsPublic)))
                                                               .ToArray();

            return result;
        }

        public static void ForEach<T>([DisallowNull] this IEnumerable<T> items, Action<T> action)
        {
            foreach (var i in items) action(i);
        }

        public static void ForEach<T>([DisallowNull] this IEnumerable<T> items, Action<T, int> action)
        {
            var i = 0;
            foreach (var item in items) action(item, i++);
        }

        public static bool TryCast<T>([DisallowNull] this object obj, out T? result) where T : class
        {
            result = obj as T;
            return result != null;
        }
    }
}
