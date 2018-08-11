using System;
using NUnit.Framework;

namespace XPlat.Runner.Tests.NUnit
{
    [TestFixture]
    public class UnitTest1
    {
        [Test]
        public void Test1()
        {
#if NETFRAMEWORK
            Console.WriteLine($"Framework: NETFRAMEWORK");
#endif
#if NETCORE
            Console.WriteLine($"Framework: NetCoreApp");
#endif
        }
    }
}
