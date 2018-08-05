using System;
using System.Collections.Generic;
using System.Text;

namespace XPlat.Runner
{
    public class Class1
    {
        public string SayHello()
        {
            return $"Hello from {this.GetType().FullName}";
        }
    }
}
