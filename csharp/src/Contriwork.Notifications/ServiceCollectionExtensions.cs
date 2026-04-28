using Microsoft.Extensions.DependencyInjection;

namespace Contriwork.Notifications;

/// <summary>
/// DI registration helpers. The package never owns config sources, so the
/// caller supplies adapters and config explicitly through these extensions
/// rather than relying on auto-discovery.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="INotificationPort"/> with an explicit adapter
    /// factory. The factory is invoked once when the container resolves the
    /// port; the resulting <see cref="NotificationClient"/> is registered
    /// as a singleton.
    /// </summary>
    public static IServiceCollection AddContriworkNotifications(
        this IServiceCollection services,
        Func<IServiceProvider, IEnumerable<IAdapter>> adapterFactory,
        NotificationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(adapterFactory);

        services.AddSingleton<INotificationPort>(sp =>
            new NotificationClient(adapterFactory(sp), config));
        return services;
    }
}
