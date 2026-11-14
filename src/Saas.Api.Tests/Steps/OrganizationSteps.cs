using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public class OrganizationSteps(
    AuthDriver auth,
    ApiDriver api,
    ApiScenarioContext context)
{
    private HttpResponseMessage? _registration;
    private AuthDriver.TokenPair? _switched;
    private int _switchStatus;

    [When("I register a new account")]
    public async Task RegisterNewAccount()
    {
        // Unique per run, so the suite can be re-run without cleaning up.
        context.RegisteredEmail = $"test-{Guid.CreateVersion7():n}@saas.local";

        _registration = await auth.RegisterAsync(
            context.RegisteredEmail, "TestPassword123", "Test", "User");
    }

    [Then("the registration succeeds")]
    public void RegistrationSucceeds() =>
        _registration!.IsSuccessStatusCode.ShouldBeTrue(
            $"registration returned {(int)_registration.StatusCode}");

    [Then("signing in with the new account succeeds")]
    public async Task SigningInWithNewAccountSucceeds()
    {
        context.Tokens = await auth.SignInAsync(context.RegisteredEmail!, "TestPassword123");
        context.Tokens.ShouldNotBeNull();

        api.Authenticate(context.Tokens.AccessToken);
    }

    [Then("the new account has exactly {int} organization")]
    [Then("the new account has exactly {int} organizations")]
    public async Task NewAccountHasOrganizations(int expected)
    {
        using var document = await api.GetJsonAsync("/organizations");

        document.RootElement.GetArrayLength().ShouldBe(expected);
    }

    [Then("the organization is personal")]
    public async Task OrganizationIsPersonal()
    {
        using var document = await api.GetJsonAsync("/organizations");

        document.RootElement[0].GetProperty("isPersonal").GetBoolean().ShouldBeTrue();
    }

    [When("I create an organization named {string}")]
    [Given("I have created an organization named {string}")]
    public async Task CreateOrganization(string name)
    {
        // Suffixed because the slug is globally unique, so a re-run would clash.
        context.OtherOrganizationId =
            await api.CreateOrganizationAsync($"{name} {Guid.CreateVersion7():n}"[..40]);
    }

    [Then("the organization is created")]
    public void OrganizationIsCreated() => context.OtherOrganizationId.ShouldNotBeNull();

    [Then("my organization list includes {string}")]
    public async Task OrganizationListIncludes(string namePrefix)
    {
        using var document = await api.GetJsonAsync("/organizations");

        document.RootElement.EnumerateArray()
            .Select(o => o.GetProperty("name").GetString() ?? string.Empty)
            .ShouldContain(name => name.StartsWith(namePrefix, StringComparison.Ordinal));
    }

    [When("I switch to that organization")]
    [Given("I have switched to that organization")]
    public async Task SwitchToThatOrganization()
    {
        (_switched, _switchStatus) = await auth.SwitchOrganizationAsync(
            context.Tokens!.RefreshToken, context.OtherOrganizationId!.Value);

        if (_switched is not null)
        {
            context.Tokens = _switched;
            context.OrganizationId = context.OtherOrganizationId;
            api.Authenticate(_switched.AccessToken);
        }
    }

    [When("I switch to an organization I am not a member of")]
    public async Task SwitchToForeignOrganization()
    {
        // An organization that cannot exist, which is indistinguishable from
        // one belonging to somebody else -- both are "not a member".
        (_switched, _switchStatus) = await auth.SwitchOrganizationAsync(
            context.Tokens!.RefreshToken, Guid.CreateVersion7());
    }

    [Then("the switch succeeds")]
    public void SwitchSucceeds() =>
        _switched.ShouldNotBeNull($"switch returned {_switchStatus}");

    [Then("the switch is refused")]
    public void SwitchIsRefused() => _switched.ShouldBeNull();

    [Then("the access token carries that organization")]
    public void TokenCarriesThatOrganization() =>
        AuthDriver.ReadClaim(_switched!.AccessToken, "org_id")
            .ShouldBe(context.OtherOrganizationId!.Value.ToString());
}
