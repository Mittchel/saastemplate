namespace Saas.Api.Tests.Drivers;

/// <summary>
/// A second API caller, so a scenario can act as the development user while
/// <see cref="ApiDriver"/> carries another account's token.
/// </summary>
public class DevelopmentApiDriver : ApiDriver;
