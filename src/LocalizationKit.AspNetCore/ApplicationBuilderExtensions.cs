using LocalizationKit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Adds per-request culture negotiation to the pipeline.</summary>
public static class LocalizationKitApplicationBuilderExtensions
{
    /// <summary>
    /// Negotiates the culture of each request and runs the rest of the pipeline inside a culture scope
    /// for it.
    /// </summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Place it before anything that produces localized output, and after <c>UseRouting</c> if the
    /// culture comes from a route value, since route values do not exist before routing has matched.
    /// </remarks>
    public static IApplicationBuilder UseLocalizationKitRequestCulture(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Tolerate the service registration having been forgotten: the defaults are what most
        // applications want, and failing to start over an omitted line of configuration would be
        // worse than quietly doing the obvious thing.
        RequestCultureOptions options =
            app.ApplicationServices.GetService<RequestCultureOptions>() ?? new RequestCultureOptions();

        return app.UseMiddleware<RequestCultureMiddleware>(options);
    }
}
