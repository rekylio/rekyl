using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using GraphQL;
using GraphQL.Conventions.Relay;
using GraphQL.Conventions.Web;
using Rekyl.Core;
using Rekyl.Core.Schema;

namespace Rekyl.Api.Handlers
{
    public class GraphQlDefaultHandler : SpecialHandler
    {
        public override string Path => "api";

        private readonly IRequestHandler _requestHandler;

        public GraphQlDefaultHandler(params Assembly[] assemblies)
        {
            assemblies = assemblies.Concat(new[] { Assembly.GetEntryAssembly() }).ToArray();
            var operationClassTypes = assemblies.SelectMany(d => d.GetTypes())
                .Where(d => d.GetCustomAttribute<ImplementViewerAttribute>() != null);

            var builder = RequestHandler.New();
            foreach (var operationClass in operationClassTypes)
            {
                switch (GetOperationType(operationClass))
                {
                    case OperationType.Query:
                        builder = builder.WithQuery(operationClass); break;
                    case OperationType.Mutation:
                        builder = builder.WithMutation(operationClass); break;
                    case OperationType.Subscription:
                        builder = builder.WithSubscription(operationClass); break;
                    case null:
                        throw new ArgumentOutOfRangeException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            _requestHandler = builder.Generate();
        }

        private OperationType? GetOperationType(Type type)
        {
            var attribute = type.GetCustomAttribute<ImplementViewerAttribute>();
            var attributeOperationType = Utils.GetFields(typeof(ImplementViewerAttribute)).First(d => d.Name == "_operationType")
                .GetValue(attribute) as OperationType?;
            return attributeOperationType;
        }

        private bool MatchesOperationType(ImplementViewerAttribute attribute, OperationType operationType)
        {
            var attributeOperationType = Utils.GetFields(typeof(ImplementViewerAttribute)).First(d => d.Name == "_operationType")
                .GetValue(attribute) as OperationType?;
            return attributeOperationType == operationType;
        }

        public virtual UserContext GetUserContext(string body)
        {
            return new DefaultUserContext(body);
        }

        public virtual void HandleError(string errorMessage) { }

        public override void Process(HttpListenerContext context)
        {
            if (string.Compare(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase) == 0)
            {
                context.Response.StatusCode = 200;
                return;
            }

            if (string.Compare(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var streamReader = new StreamReader(context.Request.InputStream);
            var body = streamReader.ReadToEnd();
            var userContext = GetUserContext(body);
            var result = _requestHandler
                .ProcessRequest(Request.New(body), userContext).Result;
            context.Response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            context.Response.StatusCode = result.Errors?.Count > 0 ? 400 : 200;
            foreach (var error in result.Errors ?? new List<ExecutionError>())
            {
                HandleError(error.Message);
            }
            var outputWriter = new StreamWriter(context.Response.OutputStream);
            outputWriter.Write(result.Body);
            outputWriter.Flush();
            outputWriter.Close();
            context.Response.OutputStream.Close();
        }
    }
}
