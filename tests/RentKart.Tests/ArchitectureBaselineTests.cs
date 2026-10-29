using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RentKart.Tests;

public class ArchitectureBaselineTests
{
    [Fact]
    public void SolutionArchitecture_ProjectReferences_AreResolvable()
    {
        // Assert that the test project successfully resolves types from Core, Infrastructure, and Web layers
        var coreAssembly = typeof(RentKart.Core.Anchor).Assembly;
        var infrastructureAssembly = typeof(RentKart.Infrastructure.Anchor).Assembly;
        var webAssembly = typeof(RentKart.Web.Controllers.HomeController).Assembly;

        Assert.NotNull(coreAssembly);
        Assert.NotNull(infrastructureAssembly);
        Assert.NotNull(webAssembly);
    }
}
