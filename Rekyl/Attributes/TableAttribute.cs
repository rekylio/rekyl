﻿using System;

namespace Rekyl.Attributes
{
    public class TableAttribute : Attribute
    {
        public bool UseDefaultDbRead { get; }
        public string TableName { get; }

        public TableAttribute(bool useDefaultDbRead = true, string tableName = null)
        {
            UseDefaultDbRead = useDefaultDbRead;
            TableName = tableName;
        }
    }
}
