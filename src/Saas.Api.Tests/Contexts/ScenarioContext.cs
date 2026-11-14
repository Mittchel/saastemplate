using System.Text.Json;
using Saas.Api.Tests.Drivers;

namespace Saas.Api.Tests.Contexts;

/// <summary>State shared between the steps of one scenario.</summary>
public class ApiScenarioContext
{
    public AuthDriver.TokenPair? Tokens { get; set; }

    /// <summary>A second identity, for the cross-tenant scenarios.</summary>
    public AuthDriver.TokenPair? OtherTokens { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? OtherOrganizationId { get; set; }

    /// <summary>A workspace the signed-in account has no membership of.</summary>
    public Guid? ForeignOrganizationId { get; set; }

    /// <summary>The organization in scope before an operator entered another.</summary>
    public Guid? HomeOrganizationId { get; set; }

    public Guid? RunId { get; set; }

    public int LastStatusCode { get; set; }

    public JsonDocument? LastJson { get; set; }

    public string? RegisteredEmail { get; set; }
}
