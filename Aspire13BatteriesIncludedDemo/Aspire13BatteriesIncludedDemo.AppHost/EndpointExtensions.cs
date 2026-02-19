using Aspire.Hosting;

namespace Aspire13BatteriesIncludedDemo.AppHost;

public static class EndpointExtensions
{
    /// <summary>
    /// Sets the <see cref="EndpointAnnotation.TargetHost"/> for all existing endpoints on the resource,
    /// enabling friendly *.dev.localhost URLs in the Aspire dashboard.
    /// </summary>
    public static IResourceBuilder<T> WithDevLocalhost<T>(
        this IResourceBuilder<T> builder,
        string name) where T : IResourceWithEndpoints
    {
        var targetHost = $"{name}.dev.localhost";

        foreach (var endpoint in builder.Resource.Annotations.OfType<EndpointAnnotation>())
        {
            endpoint.TargetHost = targetHost;
        }

        return builder;
    }
}
