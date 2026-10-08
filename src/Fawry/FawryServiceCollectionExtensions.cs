using System;
using Fawry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers <see cref="FawryClient"/> in the DI container.
    /// </summary>
    public static class FawryServiceCollectionExtensions
    {
        /// <summary>Adds <see cref="FawryClient"/> as a singleton.</summary>
        public static IServiceCollection AddFawry(
            this IServiceCollection services, FawryClientOptions options)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            services.TryAddSingleton(options ?? throw new ArgumentNullException(nameof(options)));
            services.TryAddSingleton<FawryClient>();
            return services;
        }

        /// <summary>Adds <see cref="FawryClient"/> as a singleton, configured via <paramref name="configure"/>.</summary>
        public static IServiceCollection AddFawry(
            this IServiceCollection services, Action<FawryClientOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var options = new FawryClientOptions();
            configure(options);
            return services.AddFawry(options);
        }
    }
}
