using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.IISIntegration;

namespace PSMOperationsPlatform.Web.Security;

public static class PortalAuthenticationComposition
{
    public static IServiceCollection AddPortalWindowsAuthentication(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication(IISDefaults.AuthenticationScheme);
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static IApplicationBuilder UsePortalAuthentication(
        this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.UseAuthentication();
        application.UseAuthorization();

        return application;
    }
}
