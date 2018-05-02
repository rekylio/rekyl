using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Conventions;
using GraphQLParser;
using GraphQLParser.AST;
using Newtonsoft.Json.Linq;
using Rekyl.Database;
using Rekyl.Schema;
using Rekyl.Schema.Types;
using RethinkDb.Driver.Ast;

namespace Rekyl
{
    public class UserContext : IUserContext, IDataLoaderContextProvider
    {
        public string UserName { get; }

        public enum ReadType
        {
            WithDocument,
            Shallow,
            Full
        }

        public static void InitDb(DatabaseUrl databaseUrl, DatabaseName databaseName)
        {
            new UserContext(null, databaseUrl, databaseName);
        }

        public GraphQLDocument Document { get; }

        protected UserContext() : this(null) { }

        protected UserContext(string body) : this(body, null, null) { }

        protected UserContext(string body, string userName) : this(body, null, null)
        {
            UserName = userName;
        }

        protected UserContext(string body, DatabaseUrl databaseUrl, DatabaseName databaseName)
        {
            if (!DbContext.Initalized)
                DbContext.Initialize(databaseUrl.Url, databaseName.Name);

            if (string.IsNullOrEmpty(body)) return;

            try
            {
                var query = JObject.Parse(body).GetValue("query").ToString();
                Document = GetDocument(query);
            }
            catch (Exception)
            {
                Document = GetDocument(body);
            }
        }



        public T Get<T>(Id id) where T : class
        {
            return Get<T>(id, ReadType.WithDocument);
        }

        public static T Get<T>(Id id, string properties) where T : class
        {
            return new UserContext($"query{{dummy{{{properties}}}}}").Get<T>(id);
        }

        public static T GetShallow<T>(Id id) where T : class
        {
            return new UserContext().Get<T>(id, ReadType.Shallow);
        }

        public static T GetFull<T>(Id id) where T : class
        {
            return new UserContext().Get<T>(id, ReadType.Full);
        }

        private T Get<T>(Id id, ReadType readType) where T : class
        {
            if (!id.IsIdentifierForType<T>())
            {
                var type = typeof(T);
                throw new ArgumentException($"Id type does not match generic type {type.Name}.");
            }

            if (typeof(T).UsesDefaultDbRead())
            {
                var data = DbContext.Instance.ReadByIdDefault<T>(id, readType, Document);
                return data;
            }
            throw new ArgumentException($"Unable to derive type from identifier '{id}'");
        }



        public static T AddDefault<T>(T newItem) where T : NodeBase
        {
            return DbContext.Instance.AddDefault(newItem);
        }



        public static T UpdateDefault<T>(T newItem, Id oldId) where T : NodeBase
        {
            return DbContext.Instance.UpdateDeafult(newItem, oldId);
        }

        public T[] Search<T>(Func<ReqlExpr, ReqlExpr> searchFunc) where T : NodeBase
        {
            return Search<T>(searchFunc, ReadType.WithDocument);
        }

        public static T[] SearchShallow<T>(Func<ReqlExpr, ReqlExpr> searchFunc) where T : NodeBase
        {
            return new UserContext().Search<T>(searchFunc, ReadType.Shallow);
        }

        private T[] Search<T>(Func<ReqlExpr, ReqlExpr> searchFunc, ReadType readType) where T : NodeBase
        {
            var ret = DbContext.Instance.Search<T>(searchFunc, Document, readType);
            return Utils.AddOrInitializeArray(ret);
        }

        public T[] Search<T>(string propertyName, string value) where T : NodeBase
        {
            return Search<T>(propertyName, value, ReadType.WithDocument);
        }

        public static T[] SearchShallow<T>(string propertyName, string value) where T : NodeBase
        {
            return new UserContext().Search<T>(propertyName, value, ReadType.Shallow);
        }

        private T[] Search<T>(string propertyName, string value, ReadType readType) where T : NodeBase
        {
            var ret = DbContext.Instance.Search<T>(d => d.Filter(item => item.G(propertyName).Match(value)), Document, readType);
            return Utils.AddOrInitializeArray(ret);
        }



        public T[] GetAll<T>() where T : NodeBase
        {
            return GetAll<T>(ReadType.WithDocument);
        }

        public static T[] GetAllShallow<T>() where T : NodeBase
        {
            return new UserContext().GetAll<T>(ReadType.Shallow);
        }

        private T[] GetAll<T>(ReadType readType) where T : NodeBase
        {
            return Search<T>(d => d, readType);
        }



        public static void Remove<T>(Id id)
        {
            DbContext.Instance.Remove<T>(id);
        }



        public static GraphQLDocument GetDocument(string query)
        {
            var lexer = new Lexer();
            var parser = new Parser(lexer);
            var source = new Source(query);
            var document = parser.Parse(source);
            return document;
        }

        public Task FetchData(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public static void Reset()
        {
            // ### DANGER!!!!! ###
            // This will delete your database
            DbContext.Instance.Reset();
        }

        public static void SetOverrideAssembly(Assembly assembly)
        {
            DbContext.OverrrideAssembly = assembly;
        }
    }

    public class DefaultUserContext : UserContext
    {
        public DefaultUserContext(string body) : base(body)
        {

        }
    }
}