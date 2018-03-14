using System.ComponentModel;
using GraphQL.Conventions.Relay;

namespace Rekyl.Schema.Output
{
    [Description("Default result")]
    public class DefaultResult<T> : IRelayMutationOutputObject
    {
        public DefaultResult(T result)
        {
            Result = result;
        }

        public string ClientMutationId { get; set; }

        public T Result { get; }
    }
}
