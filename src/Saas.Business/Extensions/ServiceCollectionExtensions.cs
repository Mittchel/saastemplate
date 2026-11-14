using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saas.Business.Services;
using Saas.Business.Services.Interfaces;
using Saas.Core.Email;
using Saas.Core.Options;

namespace Saas.Business.Extensions;

/// <summary>
/// Business-layer registration, keeping startup in each service down to a
/// handful of <c>Add*</c> calls rather than an inline wall of DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FeatureOptions>(configuration.GetSection(FeatureOptions.SectionName));
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddScoped<IProvisioningService, ProvisioningService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IRunService, RunService>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();

        return services;
    }
}
