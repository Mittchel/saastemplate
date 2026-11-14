using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public class AuthenticationSteps(
    AuthDriver auth,
    ApiDriver api,
    TestSettings settings,
    ApiScenarioContext context)
{
    [Given("I am signed in as the development user")]
    [When("I sign in as the development user")]
    public async Task SignInAsDevelopmentUser()
    {
        context.Tokens = await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword);

        if (context.Tokens is not null)
        {
            api.Authenticate(context.Tokens.AccessToken);
            context.OrganizationId = Guid.Parse(
                AuthDriver.ReadClaim(context.Tokens.AccessToken, "org_id")!);
        }
    }

    [When("I sign in with email {string} and password {string}")]
    public async Task SignInWith(string email, string password)
    {
        context.Tokens = await auth.SignInAsync(email, password);
    }

    [Then("the sign in succeeds")]
    public void SignInSucceeds() => context.Tokens.ShouldNotBeNull();

    [Then("the sign in fails")]
    public void SignInFails() => context.Tokens.ShouldBeNull();

    [Then("the access token carries an organization")]
    public void TokenCarriesOrganization()
    {
        var organizationId = AuthDriver.ReadClaim(context.Tokens!.AccessToken, "org_id");

        organizationId.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParse(organizationId, out _).ShouldBeTrue();
    }

    [Then("the access token carries the role {string}")]
    public void TokenCarriesRole(string role) =>
        AuthDriver.ReadClaim(context.Tokens!.AccessToken, "org_role").ShouldBe(role);

    [Then("the access token carries the permission {string}")]
    public void TokenCarriesPermission(string permission) =>
        AuthDriver.ReadClaim(context.Tokens!.AccessToken, "permission")
            .ShouldNotBeNull()
            .ShouldContain(permission);

    [When("I request {string}")]
    public async Task RequestPath(string path)
    {
        var response = await api.GetAsync(path);

        context.LastStatusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            context.LastJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
    }

    [When("I request {string} without a token")]
    public async Task RequestWithoutToken(string path)
    {
        // A driver with no Authorize header, so this really is unauthenticated.
        var anonymous = new ApiDriver(settings);
        var response = await anonymous.GetAsync(path);

        context.LastStatusCode = (int)response.StatusCode;
    }

    [Then("the response status is {int}")]
    public void ResponseStatusIs(int expected) => context.LastStatusCode.ShouldBe(expected);

    [Then("the response field {string} matches the token's organization")]
    public void ResponseFieldMatchesTokenOrganization(string path)
    {
        var element = context.LastJson!.RootElement;

        foreach (var segment in path.Split('.'))
        {
            element = element.GetProperty(segment);
        }

        element.GetString().ShouldBe(context.OrganizationId!.Value.ToString());
    }
}
