using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rekyl.Database;
using Rekyl.Test.Model;

namespace Rekyl.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void ThreeTierShoudWork()
        {
            var dir = Directory.GetCurrentDirectory();
            var assembly = Assembly.LoadFrom(Path.Combine(dir, "Rekyl.Test.dll"));
            UserContext.SetOverrideAssembly(assembly);
            UserContext.InitDb(new DatabaseUrl("localhost"), new DatabaseName("rekylTest"));
            UserContext.Reset();
            var test3 = new Test3();
            var test2 = new Test2(test3);
            var test1 = new Test1(new[] { test2 }, new[] { "a", "b" }, "sdfli", 0);
            UserContext.AddDefault(test3);
            UserContext.AddDefault(test2);
            UserContext.AddDefault(test1);
            var context = new TestContext("query{test1{test2{test3{id}}}}");
            var data = UserContext.GetFull<Test1>(test1.Id);
        }
    }

    public class TestContext : UserContext
    {
        public TestContext(string body) : base(body)
        {

        }
    }
}
