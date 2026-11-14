using Reqnroll;
using Reqnroll.BoDi;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;

namespace Saas.Api.Tests.Hooks;

/// <summary>Registers the drivers in Reqnroll's per-scenario container.</summary>
[Binding]
public class DriverHooks(IObjectContainer container)
{
    [BeforeScenario(Order = 0)]
    public void RegisterDrivers()
    {
        var settings = new TestSettings();

        container.RegisterInstanceAs(settings);
        container.RegisterInstanceAs(new AuthDriver(settings));
        container.RegisterInstanceAs(new ApiDriver(settings));
        container.RegisterInstanceAs(new DatabaseDriver(settings));
        container.RegisterInstanceAs(new ApiScenarioContext());
    }
}
