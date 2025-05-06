using AutoThreadSafe.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AutoThreadSafe.Internal
{
    internal class LockInfo : ILockInfo
    {
        private static readonly MethodInfoRelator _methodInfoRelator = new MethodInfoRelator();

        private static readonly PropertyInfoRelator _propertyInfoRelator = new PropertyInfoRelator();

        private readonly HashSet<MethodInfo> _methodInfos = new HashSet<MethodInfo>(_methodInfoRelator);

        private readonly HashSet<PropertyInfo> _propertyInfos = new HashSet<PropertyInfo>(_propertyInfoRelator);

        public Guid Id { get; } = Guid.NewGuid();

        public string LockObjectName => $"lock_{this.Id:N}";

        public MethodInfo[] MethodInfos => this._methodInfos.OrderBy(m => m, _methodInfoRelator).ToArray();

        public PropertyInfo[] PropertyInfos => this._propertyInfos.OrderBy(p => p, _propertyInfoRelator).ToArray();

        public bool IsStatic { get; set; }

        public LockInfo(bool isStatic = false)
        {
            this.IsStatic = isStatic;
        }

        public void AddMethod([DisallowNull] MethodInfo methodInfo)
        {
            // TODO: What if _methodInfos has methods from different classes? Inheritable and non?
            this._methodInfos.Add(methodInfo);
        }

        public void AddMethods([DisallowNull] MethodInfo[] methodInfos) => methodInfos.ForEach(m => this.AddMethod(m));

        public void AddProperty([DisallowNull] PropertyInfo propertyInfo)
        {
            // TODO: What if _propertyInfos has properties from different classes? Inheritable and non?
            this._propertyInfos.Add(propertyInfo);
        }

        public void AddProperties([DisallowNull] PropertyInfo[] propertyInfos) => propertyInfos.ForEach(p => this.AddProperty(p));

        public int CompareTo(ILockInfo? other)
        {
            if (other == null) return 1;

            var otherLockInfo = other as LockInfo;
#pragma warning disable CS8602 // Dereference of a possibly null reference. "GetType().FullName" is never null.
            if (otherLockInfo == null) return this.GetType().FullName.CompareTo(other.GetType().FullName);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            if (this.Id != otherLockInfo.Id) return this.Id.CompareTo(otherLockInfo.Id);

            if (this._methodInfos.Count != otherLockInfo._methodInfos.Count) return this._methodInfos.Count - otherLockInfo._methodInfos.Count;

            var myMethods = this.MethodInfos;
            var theirMethods = otherLockInfo.MethodInfos;

            var compared = myMethods.CompareArray(theirMethods, _methodInfoRelator);
            if (compared != 0) return compared;

            var myProperties = this.PropertyInfos;
            var theirProperties = otherLockInfo.PropertyInfos;

            compared = myProperties.CompareArray(theirProperties, _propertyInfoRelator);
            return compared;
        }

        public bool Equals(ILockInfo? other)
        {
            return this.CompareTo(other) == 0;
        }

        public bool HasMethod([DisallowNull] MethodInfo methodInfo) => this._methodInfos.Contains(methodInfo);

        public bool HasProperty([DisallowNull] PropertyInfo propertyInfo) => this._propertyInfos.Contains(propertyInfo);

        public bool IsEmpty() => this._methodInfos.IsEmpty() && this._propertyInfos.IsEmpty();

        public void RemoveMethod([DisallowNull] MethodInfo methodInfo) => this._methodInfos.Remove(methodInfo);

        public void RemoveProperty([DisallowNull] PropertyInfo propertyInfo) => this._propertyInfos.Remove(propertyInfo);
    }
}
