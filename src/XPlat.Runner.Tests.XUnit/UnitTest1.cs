using System;
using Xunit;

namespace XPlat.Runner.Tests
{
    public class UnitTest1
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public UnitTest1(Xunit.Abstractions.ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test1()
        {
            _output.WriteLine($"Environment.Version: {Environment.Version}");
#if NETFRAMEWORK
            _output.WriteLine($"Framework: NETFRAMEWORK");
            Assert.False(true, "Fail under full framework.");
#endif
#if NETCORE
            _output.WriteLine($"Framework: NetCoreApp");
#endif
            //Assert.True(false);
            _output.WriteLine(new Class1().SayHello());
        }
    }
}
