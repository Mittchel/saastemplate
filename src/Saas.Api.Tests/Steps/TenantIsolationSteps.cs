using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public class TenantIsolationSteps(
    TestSettings settings,
    ApiDriver api,
    DatabaseDriver database,
    ApiScenarioContext context)
{
    [Given("I have started a run")]
    public async Task StartRun()
    {
        var (runId, statusCode) = await api.CreateRunAsync("Isolation test run");

        runId.ShouldNotBeNull($"POST /runs returned {statusCode}");
        context.RunId = runId;
    }

    [Then("row-level security is enabled and forced on {string}")]
    public async Task RowLevelSecurityIsForced(string table)
    {
        var (enabled, forced) = await database.GetRowLevelSecurityAsync(table);

        enabled.ShouldBeTrue($"RLS is not enabled on {table}");

        // FORCE is what keeps the policies applying even to the table owner, so
        // a deployment that gets the roles wrong still fails closed.
        forced.ShouldBeTrue($"RLS is not forced on {table}");
    }

    [Then("the run is visible to its own organization")]
    public async Task RunIsVisibleToOwnOrganization()
    {
        var count = await database.CountAsTenantAsync("Runs", context.OrganizationId);

        count.ShouldBeGreaterThan(0);
    }

    [Then("the run is invisible to a different organization")]
    public async Task RunIsInvisibleToDifferentOrganization()
    {
        // Any other tenant id: the policy compares to the session variable, so
        // one that does not match proves the check is on the row, not the query.
        var count = await database.CountAsTenantAsync("Runs", Guid.CreateVersion7());

        count.ShouldBe(0);
    }

    [Then("the run is invisible with no organization set")]
    public async Task RunIsInvisibleWithNoTenant()
    {
        // The interceptor writes an empty string when there is no tenant, which
        // the helper maps to NULL -- so nothing matches and nothing leaks.
        var count = await database.CountAsTenantAsync("Runs", null);

        count.ShouldBe(0);
    }

    [Then("the AI database role cannot read {string}")]
    public async Task AiRoleCannotReadTable(string table)
    {
        // The AI service reaches business data only through the API, carrying
        // the user's token. Its database role has no grant on these tables at
        // all, so even a compromised agent cannot query around the API.
        var aiConnection = settings.PostgresAi;

        // Schema-qualified: the AI role has search_path = ai, so a bare name
        // would resolve to a table that simply does not exist there rather than
        // testing the grant on the business table.
        var canRead = await database.CanReadAsync(aiConnection, $"public.\"{table}\"");

        canRead.ShouldBeFalse($"the AI role can read {table}, which it must not");
    }
}
