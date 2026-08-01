using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSMOperationsPlatform.Web.Security;

namespace PSMOperationsPlatform.Web.Tests;

public sealed class PortalAuthenticationCompositionTests
{
    [Fact]
    public async Task RegistersIisAuthenticationAndAuthenticatedFallbackPolicy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPortalWindowsAuthentication();
        await using var provider = services.BuildServiceProvider();

        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = await policyProvider.GetFallbackPolicyAsync();

        Assert.Equal(IISDefaults.AuthenticationScheme, authenticationOptions.DefaultScheme);
        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task AnonymousProtectedRequestReturnsUnauthorized()
    {
        await using var app = await CreateApplicationAsync(authenticated: false);
        var response = await app.GetTestClient().GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedProtectedRequestSucceedsAndPreservesAuthenticationType()
    {
        await using var app = await CreateApplicationAsync(authenticated: true);
        var response = await app.GetTestClient().GetAsync("/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestAuthenticationHandler.TestScheme, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AuthenticatedRequestDeniedByExplicitPolicyReturnsForbidden()
    {
        await using var app = await CreateApplicationAsync(authenticated: true);
        var response = await app.GetTestClient().GetAsync("/forbidden");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HealthIsExplicitlyAnonymousBoundedAndDoesNotUnprotectOtherEndpoints()
    {
        await using var app = await CreateApplicationAsync(authenticated: false);
        var client = app.GetTestClient();

        var health = await client.GetAsync("/health");
        var body = await health.Content.ReadAsStringAsync();
        var protectedResponse = await client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Healthy", body);
        Assert.DoesNotContain("identity", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(body.Length <= 32);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Fact]
    public void CompositionUsesFrameworkMiddlewareInRequiredOrder()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src", "PSMOperationsPlatform.Web", "Security", "PortalAuthenticationComposition.cs"));

        var authentication = source.IndexOf("UseAuthentication()", StringComparison.Ordinal);
        var authorization = source.IndexOf("UseAuthorization()", StringComparison.Ordinal);

        Assert.True(authentication >= 0);
        Assert.True(authorization > authentication);
    }

    [Fact]
    public void ProgramMakesOnlyGenericHealthAnonymous()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src", "PSMOperationsPlatform.Web", "Program.cs"));

        Assert.Contains("MapHealthChecks(\"/health\").AllowAnonymous()", source, StringComparison.Ordinal);
        Assert.Equal(1, source.Split("AllowAnonymous()", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ProductionCompositionContainsNoAlternateAuthenticationOrIdentityTrust()
    {
        var webRoot = Path.Combine(RepositoryRoot(), "src", "PSMOperationsPlatform.Web");
        var source = string.Join('\n', Directory.GetFiles(webRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        var project = File.ReadAllText(Path.Combine(webRoot, "PSMOperationsPlatform.Web.csproj"));

        Assert.DoesNotContain("Negotiate", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AddCookie", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AddJwtBearer", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WindowsIdentity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Remote-User", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Windows-User", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AuthenticationStateProvider", source, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> CreateApplicationAsync(bool authenticated)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddHealthChecks();
        builder.Services.AddPortalWindowsAuthentication();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.TestScheme;
                options.DefaultForbidScheme = TestAuthenticationHandler.TestScheme;
            })
            .AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.TestScheme,
                options => options.Authenticated = authenticated);
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("deny-test", policy => policy.RequireClaim("permission", "granted")));

        var app = builder.Build();
        app.UsePortalAuthentication();
        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapGet("/protected", (ClaimsPrincipal user) => user.Identity?.AuthenticationType ?? string.Empty);
        app.MapGet("/forbidden", () => Results.Ok()).RequireAuthorization("deny-test");
        await app.StartAsync();
        return app;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PSMOperationsPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
    {
        public bool Authenticated { get; set; }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
    {
        public const string TestScheme = "RepositoryTestWindows";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Options.Authenticated)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "repository-test-user")], TestScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestScheme)));
        }
    }
}
