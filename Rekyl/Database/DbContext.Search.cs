using System;
using GraphQLParser.AST;
using Newtonsoft.Json.Linq;
using Rekyl.Schema;
using Rekyl.Schema.Types;
using RethinkDb.Driver.Ast;

namespace Rekyl.Database
{
    public partial class DbContext
    {
        public T[] Search<T>(Func<ReqlExpr, ReqlExpr> searchFunction, GraphQLDocument document, UserContext.ReadType readType)
            where T : NodeBase
        {
            var type = typeof(T[]);
            var table = GetTable(type);
            switch (readType)
            {
                case UserContext.ReadType.WithDocument:
                    var selectionSet = GetSelectionSet(document);
                    var hashMap = GetHashMap(selectionSet, type);
                    var importTree = GetImportTree(typeof(T), hashMap, null);
                    var docExpr = searchFunction(table);
                    docExpr = docExpr.Map(item => Merge(item, importTree));
                    docExpr = docExpr.Map(item => item.Pluck(hashMap));
                    var docResult = docExpr.CoerceTo("ARRAY").Run(_connection) as JArray;
                    var docRet = Utils.DeserializeObject(type, docResult);
                    return docRet as T[];
                case UserContext.ReadType.Shallow:
                    var shallowSearchExpr = searchFunction(table);
                    var shallowResult = shallowSearchExpr.CoerceTo("ARRAY").Run(_connection) as JArray;
                    var shallowRet = Utils.DeserializeObject(type, shallowResult);
                    return shallowRet as T[];
            }

            return null;
        }
    }
}
