using System;
using System.Collections.Generic;
using System.Text;
using Rekyl.Schema.Types;

namespace Rekyl.Test.Model
{
    public class Test2 : NodeBase<Test3>
    {
        public Test3 Test3 { get; }

        public Test2(Test3 test3)
        {
            Test3 = test3;
        }
    }
}
