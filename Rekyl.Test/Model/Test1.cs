using System;
using System.Collections.Generic;
using System.Text;
using Rekyl.Schema.Types;

namespace Rekyl.Test.Model
{
    public class Test1: NodeBase<Test1>
    {
        public Test2 Test2 { get; }

        public Test1(Test2 test2)
        {
            Test2 = test2;
        }
    }
}
