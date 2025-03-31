using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{

    public static partial class IServiceCollectionExtensions
    {
        public static IServiceCollection AddRepositorySettings(this IServiceCollection services, IConfiguration configuration, Action<RepositorySettings>? manualConfigure = null)
        {

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<RepositorySettings>(configuration.GetSection(nameof(RepositorySettings)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<RepositorySettings>(manualConfigure);
            }

            return services;
        }
    }
}