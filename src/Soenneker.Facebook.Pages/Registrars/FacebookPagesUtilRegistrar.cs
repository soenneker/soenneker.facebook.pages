using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Facebook.Pages.Abstract;
using Soenneker.Facebook.OpenApiClientUtil.Registrars;

namespace Soenneker.Facebook.Pages.Registrars;

/// <summary>
/// A utility for interacting with Facebook Pages via the Facebook OpenAPI client.
/// </summary>
public static class FacebookPagesUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IFacebookPagesUtil"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddFacebookPagesUtilAsSingleton(this IServiceCollection services)
    {
        services.AddFacebookOpenApiClientUtilAsSingleton();
        services.TryAddSingleton<IFacebookPagesUtil, FacebookPagesUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IFacebookPagesUtil"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddFacebookPagesUtilAsScoped(this IServiceCollection services)
    {
        services.AddFacebookOpenApiClientUtilAsScoped();
        services.TryAddScoped<IFacebookPagesUtil, FacebookPagesUtil>();

        return services;
    }
}
