using AutoThreadSafe.Enums;
using AutoThreadSafe.Exceptions;
using AutoThreadSafe.Interfaces;
using AutoThreadSafe.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace AutoThreadSafe.Impl
{
    internal class WeaveTypeBuilder<TType> : IWeaveTypeBuilder<TType> where TType : class
    {
        private static readonly MethodInfoRelator _methodInfoRelator = new MethodInfoRelator();
        private static readonly PropertyInfoRelator _propertyInfoRelator = new PropertyInfoRelator();

        private static readonly TypeAttributes _typeBuilderAttributes = TypeAttributes.Class |
                                                                        TypeAttributes.AutoClass |
                                                                        TypeAttributes.AnsiClass |
                                                                        TypeAttributes.BeforeFieldInit |
                                                                        TypeAttributes.AutoLayout |
                                                                        TypeAttributes.Sealed |
                                                                        (typeof(TType).IsPublic? TypeAttributes.Public : TypeAttributes.NotPublic);

        private static readonly FieldAttributes _instanceLockObjectBaseFieldAttributes = FieldAttributes.Private | FieldAttributes.InitOnly;

        private static readonly FieldAttributes _staticLockObjectFieldAttributes = _instanceLockObjectBaseFieldAttributes | FieldAttributes.Static;

        private static readonly MethodAttributes[] _inverseAttributeFlags = new[] {
                ~MethodAttributes.Public,
                ~MethodAttributes.Family,
                ~MethodAttributes.Assembly,
                ~MethodAttributes.FamANDAssem,
                ~MethodAttributes.FamORAssem
            };



        private readonly AssemblyName _assemblyName;
        private readonly AssemblyBuilder _assemblyBuilder;
        private readonly ModuleBuilder _moduleBuilder;
        private readonly TypeBuilder _typeBuilder;

        private readonly Dictionary<Guid, ILockInfo> _lockInfos = new Dictionary<Guid, ILockInfo>();

        private readonly List<MemberInfo> _allBuilts = new List<MemberInfo>();

        public string WovenAssemblyName => this._assemblyName.FullName;

        public string WovenClassName => $"{typeof(TType).Name}{this.TypeGuid:N}_{WeaveSuffix}";

        public bool IsWoven { get; private set; } = false;

        public Type WovenType { get; private set; } = typeof(TType);

        public InitialWeaveType InitialWeaveType { get; }

        private ConstructorInfo[]? _constructors = null;
        public ConstructorInfo[] Constructors
        {
            get
            {
                if (this.IsWoven) return this._constructors ??= WovenType.GetUseableConstructors();

                return Array.Empty<ConstructorInfo>();
            }
        }

        internal const string WeaveSuffix = "Woven";

        internal Guid TypeGuid { get; } = Guid.NewGuid();

        internal string StaticLockObjectInitMethodName => $"Initialize_Static_Locks_{this.TypeGuid:N}";

        internal string InstanceLockObjectInitMethodName => $"Initialize_Instance_Locks{this.TypeGuid:N}";

        // TODO: Consider having woven types based on types from the same assembly put in the same woven assembly
        public WeaveTypeBuilder(InitialWeaveType initialWeaveType)
        {
            this.InitialWeaveType = initialWeaveType;

            this._assemblyName = new AssemblyName($"{typeof(TType).Assembly.GetName().Name}_{this.WovenClassName}");
            this._assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(this._assemblyName, AssemblyBuilderAccess.Run);
            this._moduleBuilder = this._assemblyBuilder.DefineDynamicModule($"{this.WovenAssemblyName}.dll");
            this._typeBuilder = this._moduleBuilder.DefineType(this.WovenClassName, _typeBuilderAttributes, typeof(TType));

            this.InitialWeaveType = initialWeaveType;

            this.ClearLockInfo();

            if (this.InitialWeaveType == InitialWeaveType.All) this.PopulateLockInfoDictionary();
        }

        // TODO: Handle case where lock info contains methods/properties from multiple/other classes
        public void AddLockInfo([DisallowNull] ILockInfo lockInfo, bool throwOnMembersAlreadyPresent = true)
        {
            if (lockInfo.IsEmpty()) return;

            var intersectingMethodInfos = new List<MethodInfo>();
            var intersectingPropertyInfos = new List<PropertyInfo>();

            var lockInfoMethods = lockInfo.MethodInfos;
            var lockInfoProperties = lockInfo.PropertyInfos;
            
            foreach (var li in this._lockInfos.Values)
            {
                var methodIntersects = lockInfoMethods.Intersect(li.MethodInfos, _methodInfoRelator).ToArray();
                var propertyIntersects = lockInfoProperties.Intersect(li.PropertyInfos, _propertyInfoRelator).ToArray();

                if (methodIntersects.Length > 0 || propertyIntersects.Length > 0)
                {
                    intersectingMethodInfos.AddRange(methodIntersects);
                    intersectingPropertyInfos.AddRange(propertyIntersects);

                    if (!throwOnMembersAlreadyPresent)
                    {
                        methodIntersects.ForEach(mi => li.RemoveMethod(mi));
                        propertyIntersects.ForEach(pi => li.RemoveProperty(pi));
                    }
                }
            }

            if (throwOnMembersAlreadyPresent && (intersectingMethodInfos.Any() || intersectingPropertyInfos.Any()))
            {
                throw new MemberIntersectionException($"Cannot add {nameof(LockInfo)}; methods or properties in the {nameof(LockInfo)} object are already present in this {nameof(WeaveTypeBuilder<TType>)}.", intersectingMethodInfos, intersectingPropertyInfos);
            }

            this._lockInfos.Add(lockInfo.Id, lockInfo);

            var emptyLockInfos = this._lockInfos.Keys.Where(id => this._lockInfos[id].IsEmpty()).ToArray();
            emptyLockInfos.ForEach(id => this._lockInfos.Remove(id));
        }

        public void ClearLockInfo()
        {
            this._lockInfos.Clear();
            this.WovenType = typeof(TType);
            this._allBuilts.Clear();
            this.IsWoven = false;
            this._constructors = null;
        }

        public ILockInfo? GetLockInfo(Guid id) => this._lockInfos.TryGetValue(id, out var lockInfo) ? lockInfo : null;

        public ILockInfo? GetLockInfo(MethodInfo? methodInfo) => methodInfo == null ? null : this._lockInfos.Values.FirstOrDefault(li => li.MethodInfos.Contains(methodInfo, _methodInfoRelator));

        public ILockInfo? GetLockInfo(PropertyInfo? propertyInfo) => propertyInfo == null ? null : this._lockInfos.Values.FirstOrDefault(li => li.PropertyInfos.Contains(propertyInfo, _propertyInfoRelator));

        public ILockInfo? GetLockInfo(MemberInfo? memberInfo)
        {
            if (memberInfo == null) return null;

            if (memberInfo.TryCast(out MethodInfo? mi)) return this.GetLockInfo(mi);
            if (memberInfo.TryCast(out PropertyInfo? pi)) return this.GetLockInfo(pi);

            throw new NotImplementedException($"{nameof(GetLockInfo)} not implemented for {nameof(MemberInfo)} subclass {memberInfo.GetType().Name}");
        }

        public void RemoveLockInfo([DisallowNull] ILockInfo lockInfo) => this._lockInfos.Remove(lockInfo.Id);

        public void RemoveLockInfo(Guid id) => this._lockInfos.Remove(id);

        public void ResetLockInfo()
        {
            this.ClearLockInfo();
            if (this.InitialWeaveType == InitialWeaveType.All) this.PopulateLockInfoDictionary();
        }

        public void WeaveType()
        {
            var staticLockFieldBuilders = this.CreateStaticLockFields(out MethodBuilder? staticFieldInitBuilder);
            var instanceLockFieldBuilders = this.CreateInstanceLockFields();

            this.CreateConstructors();

            if (staticLockFieldBuilders != null)
            {
                foreach (var pair in staticLockFieldBuilders)
                {
                    this.BuildMethodsForLockInfo(pair.LockInfo, pair.FieldBuilder);
                    this.BuildPropertiesForLockInfo(pair.LockInfo, pair.FieldBuilder);
                }
            }

            foreach (var pair in instanceLockFieldBuilders)
            {
                this.BuildMethodsForLockInfo(pair.LockInfo, pair.FieldBuilder);
                this.BuildPropertiesForLockInfo(pair.LockInfo, pair.FieldBuilder);
            }

            this.WovenType = this._typeBuilder.CreateType();

            if (staticFieldInitBuilder != null) this.WovenType.GetMethod(staticFieldInitBuilder.Name, BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
        }

        private void PopulateLockInfoDictionary()
        {
            var lockInfo = new LockInfo();

            var methodsToAdd = typeof(TType).GetOverrideableMethods();
            var propertyesToAdd = typeof(TType).GetOverrideableProperties();

            lockInfo.AddMethods(methodsToAdd);
            lockInfo.AddProperties(propertyesToAdd);

            this.AddLockInfo(lockInfo);
        }

        private (ILockInfo LockInfo, FieldBuilder FieldBuilder)[]? CreateStaticLockFields(out MethodBuilder? staticFieldMethodBuilder)
        {
            var staticLocks = this._lockInfos.Values.Where(li => li.IsStatic).ToArray();

            staticFieldMethodBuilder = null;
            if (staticLocks.Length == 0) return null;

            staticFieldMethodBuilder = this._typeBuilder.DefineMethod(
                this.StaticLockObjectInitMethodName,
                MethodAttributes.Static | MethodAttributes.Private,
                CallingConventions.Standard,
                typeof(void),
                Array.Empty<Type>());

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type. We know for certain that the "object" type has a constructor with no parameters.
            ConstructorInfo objConstructor = typeof(object).GetConstructor(Array.Empty<Type>());
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            var initMethodIL = staticFieldMethodBuilder.GetILGenerator();

            var result = staticLocks.Select(lockInfo => 
                {
                    var fieldBuilder = this.CreateLockObjectFieldBuilder(lockInfo, _staticLockObjectFieldAttributes);
#pragma warning disable CS8604 // Possible null reference argument. We know for certain it's not.
                    initMethodIL.Emit(OpCodes.Call, objConstructor);
#pragma warning restore CS8604 // Possible null reference argument.
                    initMethodIL.Emit(OpCodes.Stsfld, fieldBuilder);
                    return (lockInfo, fieldBuilder);
                }).ToArray();

            initMethodIL.Emit(OpCodes.Ret);

            this._allBuilts.Add(staticFieldMethodBuilder);
            return result;
        }

        private (ILockInfo LockInfo, FieldBuilder FieldBuilder)[] CreateInstanceLockFields()
        {
            var instanceLocks = this._lockInfos.Values.Where(li => !li.IsStatic).ToArray();

            return instanceLocks.Select(lockInfo => (lockInfo, this.CreateLockObjectFieldBuilder(lockInfo, _instanceLockObjectBaseFieldAttributes))).ToArray();
        }

        private FieldBuilder CreateLockObjectFieldBuilder(ILockInfo lockInfo, FieldAttributes fieldAttributes)
        {
            var fieldBuilder = this._typeBuilder.DefineField(lockInfo.LockObjectName, typeof(object), fieldAttributes);
            this._allBuilts.Add(fieldBuilder);
            return fieldBuilder;
        }

        private void CreateConstructors()
        {
            var constructors = typeof(TType).GetUseableConstructors();
            constructors.ForEach(c => this.CreateConstructor(c));
        }

        private void CreateConstructor([DisallowNull] ConstructorInfo constructorInfo)
        {
            // Consideration: if people are not writing dodgy code then constructors are thread safe (assuming objects passed in to the constructor or static fields are also thread safe); so we don't need to create lock objects for them

            var parameters = constructorInfo.GetParameters();
            var parameterTypes = parameters.Select(pi => pi.ParameterType).ToArray();
            var requiredMods = parameters.Select(pi => pi.GetRequiredCustomModifiers()).ToArray();
            var optionalMods = parameters.Select(pi => pi.GetOptionalCustomModifiers()).ToArray();

            var constructorBuilder = parameters.IsEmpty()
                ? this._typeBuilder.DefineConstructor(constructorInfo.Attributes, constructorInfo.CallingConvention, null)
                : this._typeBuilder.DefineConstructor(constructorInfo.Attributes, constructorInfo.CallingConvention, parameterTypes, requiredMods, optionalMods);

            constructorBuilder.SetImplementationFlags(constructorBuilder.GetMethodImplementationFlags());

            var constructorBuilderIL = constructorBuilder.GetILGenerator();

            constructorBuilderIL.Emit(OpCodes.Ldarg_0);
            if (parameters.Any()) parameters.ForEach((p, i) => constructorBuilderIL.Emit(OpCodes.Ldarg, i + 1));
            constructorBuilderIL.Emit(OpCodes.Call, constructorInfo);
            constructorBuilderIL.Emit(OpCodes.Ret);

            this._allBuilts.Add(constructorBuilder);
        }

        private void BuildMethodsForLockInfo([DisallowNull] ILockInfo lockInfo, FieldBuilder lockFieldBuilder) =>
            lockInfo.MethodInfos.ForEach(mi => this.BuildMethod($"{lockInfo.LockObjectName}_", mi, lockFieldBuilder));

        private void BuildMethod([DisallowNull] string methodPrefix, [DisallowNull] MethodInfo methodInfo, [DisallowNull] FieldInfo lockFieldInfo)
        {
            var parameters = methodInfo.GetParameters();
            var parameterTypes = parameters.Any() ? parameters.Select(p => p.ParameterType).ToArray() : null;
            var requiredMods = parameters.Any() ? parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray() : null;
            var optionalMods = parameters.Any() ? parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray() : null;

            var methodName = $"{methodPrefix}_{methodInfo.Name}";

            var methodFlags = methodInfo.Attributes;
            _inverseAttributeFlags.ForEach(f => methodFlags &= f);
            methodFlags |= MethodAttributes.Private;

            Type? returnType = null;
            Type[]? returnTypeRequiredCustomModifiers = null;
            Type[]? returnTypeOptionalCustomModifiers = null;

            if (methodInfo.ReturnType != typeof(void))
            {
                returnType = methodInfo.ReturnType;
                returnTypeRequiredCustomModifiers = methodInfo.ReturnParameter.GetRequiredCustomModifiers();
                returnTypeOptionalCustomModifiers = methodInfo.ReturnParameter.GetOptionalCustomModifiers();
            }

            var methodBuilder = this._typeBuilder.DefineMethod(methodName, methodFlags, methodInfo.CallingConvention, returnType, returnTypeRequiredCustomModifiers, returnTypeOptionalCustomModifiers, parameterTypes, requiredMods, optionalMods);

            var enterLockMethod = typeof(System.Threading.Monitor).GetMethod("Enter", [typeof(object)]);
            var exitLockMethod = typeof(System.Threading.Monitor).GetMethod("Exit", [typeof(object)]);

            var mbil = methodBuilder.GetILGenerator();

            LocalBuilder? localBuilder1 = null;
            LocalBuilder? localBuilder2 = null;

            if (returnType != null)
            {
                localBuilder1 = mbil.DeclareLocal(returnType);
                localBuilder2 = mbil.DeclareLocal(returnType);
            }

            mbil.Emit(OpCodes.Ldarg_0);
            mbil.Emit(OpCodes.Ldfld, lockFieldInfo);
#pragma warning disable CS8604 // Possible null reference argument. We know this method exists
            mbil.Emit(OpCodes.Call, enterLockMethod);
#pragma warning restore CS8604 // Possible null reference argument.

            var tryBlock = mbil.BeginExceptionBlock();

            mbil.Emit(OpCodes.Ldarg_0);
            parameters.ForEach((p, i) => mbil.Emit(OpCodes.Ldarg, i + 1));
            mbil.Emit(OpCodes.Call, methodInfo);

            if (returnType != null)
            {
                mbil.Emit(OpCodes.Stloc_0);
                mbil.Emit(OpCodes.Ldloc_0);
                mbil.Emit(OpCodes.Stloc_1);
            }

            mbil.BeginFinallyBlock();

            mbil.Emit(OpCodes.Ldarg_0);
            mbil.Emit(OpCodes.Ldfld, lockFieldInfo);
#pragma warning disable CS8604 // Possible null reference argument. We know this method exists
            mbil.Emit(OpCodes.Call, exitLockMethod);
#pragma warning restore CS8604 // Possible null reference argument.

            mbil.EndExceptionBlock();

            if (returnType != null) mbil.Emit(OpCodes.Ldloc_1);

            mbil.Emit(OpCodes.Ret);

            this._typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);

            this._allBuilts.Add(methodBuilder);
        }

        private void BuildPropertiesForLockInfo([DisallowNull] ILockInfo lockInfo, FieldBuilder lockField)
        {
            lockInfo.PropertyInfos.ForEach(pi => this.BuildProperty($"{lockInfo.LockObjectName}_", pi, lockField));
        }

        private void BuildProperty([DisallowNull] string methodPrefix, [DisallowNull] PropertyInfo propertyInfo, [DisallowNull] FieldInfo lockField)
        {
            this.BuildPropertyGetter(methodPrefix, propertyInfo, lockField);
            this.BuildPropertySetter(methodPrefix, propertyInfo, lockField);
        }

        private void BuildPropertyGetter([DisallowNull] string methodPrefix, [DisallowNull] PropertyInfo propertyInfo, [DisallowNull] FieldInfo lockField)
        {
            var getter = propertyInfo.GetGetMethod(true);

            if (getter == null ||
                !getter.IsVirtual ||
                getter.Attributes.HasFlag(MethodAttributes.Private) ||
                getter.Attributes.HasFlag(MethodAttributes.FamANDAssem))
            {
                return;
            }

            this.BuildMethod(methodPrefix, getter, lockField);
        }

        private void BuildPropertySetter([DisallowNull] string methodPrefix, [DisallowNull] PropertyInfo propertyInfo, [DisallowNull] FieldInfo lockField)
        {
            var setter = propertyInfo.GetSetMethod(true);

            if (setter == null ||
                !setter.IsVirtual ||
                setter.Attributes.HasFlag(MethodAttributes.Private) ||
                setter.Attributes.HasFlag(MethodAttributes.FamANDAssem))
            {
                return;
            }

            this.BuildMethod(methodPrefix, setter, lockField);
        }

    }
}
