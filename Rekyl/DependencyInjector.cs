using System.Collections.Generic;
using System.Reflection;
using GraphQL.Conventions;

namespace Rekyl
{
    public class DependencyInjector : IDependencyInjector
    {
        private readonly Dictionary<TypeInfo, object> _registrations =
            new Dictionary<TypeInfo, object>();

        public void Register<TType>(TType instance)
        {
            _registrations.Add(typeof(TType).GetTypeInfo(), instance);
        }

        public object Resolve(TypeInfo typeInfo)
        {
            object instance;
            return _registrations.TryGetValue(typeInfo, out instance) ? instance : null;
        }
    }
}