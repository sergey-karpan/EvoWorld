using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyIsReferencedAndLoadable()
    {
        var coreAssembly = typeof(AssemblyMarker).Assembly;

        Assert.Equal("EvoWorld.Core", coreAssembly.GetName().Name);
        Assert.NotEqual(typeof(SmokeTests).Assembly, coreAssembly);
    }
}
