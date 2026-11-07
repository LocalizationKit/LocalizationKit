using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.AspNetCore.Tests;

public sealed class RequestCultureMiddlewareTests
{
    private sealed class Resources : ILocalizationResourceProvider
    {
        public string Name => "test";

        public ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
            CultureInfo culture,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Greeting"] = culture.Name switch
                {
                    "it-IT" => "Ciao",
                    "de-DE" => "Hallo",
                    _ => "Hello",
                },
            };

            return ValueTask.FromResult<IReadOnlyDictionary<string, string>?>(entries);
        }
    }

    private static ServiceProvider Build()
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT", "de-DE"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new Resources());
        services.AddLocalizationKitRequestCulture();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs one request through the middleware and reports what the pipeline below it saw.
    /// </summary>
    private static async Task<(string Greeting, string Culture, HttpContext Context)> RequestAsync(
        ServiceProvider services,
        Action<HttpContext> describeRequest,
        Action<RequestCultureOptions>? configure = null)
    {
        RequestCultureOptions options = services.GetRequiredService<RequestCultureOptions>();
        configure?.Invoke(options);

        ILocalizer localizer = services.GetRequiredService<ILocalizer>();
        ILocalizationContext context = services.GetRequiredService<ILocalizationContext>();

        string greeting = string.Empty;
        string culture = string.Empty;

        RequestCultureMiddleware middleware = new(
            _ =>
            {
                // Read inside the pipeline: this is the only place the scope is in effect.
                greeting = localizer["Greeting"];
                culture = context.UICulture.Name;
                return Task.CompletedTask;
            },
            context,
            options);

        DefaultHttpContext httpContext = new();
        describeRequest(httpContext);

        await middleware.InvokeAsync(httpContext);

        return (greeting, culture, httpContext);
    }

    [Fact]
    public async Task The_query_string_chooses_the_language()
    {
        await using ServiceProvider services = Build();

        (string greeting, string culture, _) = await RequestAsync(
            services,
            http => http.Request.QueryString = new QueryString("?culture=it-IT"));

        Assert.Equal("Ciao", greeting);
        Assert.Equal("it-IT", culture);
    }

    [Fact]
    public async Task A_route_value_chooses_the_language()
    {
        await using ServiceProvider services = Build();

        (string greeting, _, _) = await RequestAsync(
            services,
            http => http.Request.RouteValues["culture"] = "de-DE");

        Assert.Equal("Hallo", greeting);
    }

    [Fact]
    public async Task A_cookie_makes_the_choice_stick()
    {
        await using ServiceProvider services = Build();

        (string greeting, _, _) = await RequestAsync(
            services,
            http => http.Request.Headers.Cookie = $"{CookieCultureSource.DefaultCookieName}=it-IT");

        Assert.Equal("Ciao", greeting);
    }

    [Fact]
    public async Task Accept_language_is_honoured_in_the_order_the_client_ranked_it()
    {
        await using ServiceProvider services = Build();

        // Japanese is preferred and not shipped; German outranks Italian among the rest.
        (string greeting, _, HttpContext http) = await RequestAsync(
            services,
            context => context.Request.Headers.AcceptLanguage = "ja;q=1.0, it-IT;q=0.3, de-DE;q=0.7");

        Assert.Equal("Hallo", greeting);
        Assert.Contains("Accept-Language", http.Response.Headers.Vary.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_language_offered_with_q0_is_not_used()
    {
        await using ServiceProvider services = Build();

        (string greeting, _, _) = await RequestAsync(
            services,
            http => http.Request.Headers.AcceptLanguage = "it-IT;q=0, de-DE;q=0.5");

        Assert.Equal("Hallo", greeting);
    }

    [Fact]
    public async Task A_language_without_a_region_resolves_to_the_one_that_is_shipped()
    {
        await using ServiceProvider services = Build();

        // A browser asking for "it" has to reach the shipped it-IT; the same walk up the culture chain
        // is what makes an Italian region the application does not ship land there too.
        (string greeting, string culture, _) = await RequestAsync(
            services,
            http => http.Request.Headers.AcceptLanguage = "it");

        Assert.Equal("Ciao", greeting);
        Assert.Equal("it-IT", culture);
    }

    [Fact]
    public async Task An_unknown_or_malformed_culture_leaves_the_selected_one_alone()
    {
        await using ServiceProvider services = Build();

        // A header is attacker-controlled: nonsense in it is an ordinary event, not an exception.
        (string greeting, string culture, HttpContext http) = await RequestAsync(
            services,
            context =>
            {
                context.Request.QueryString = new QueryString("?culture=not-a-culture");
                context.Request.Headers.AcceptLanguage = "\u0000 nonsense ;;; q=what";
            });

        Assert.Equal("Hello", greeting);
        Assert.Equal("en-US", culture);
        Assert.Empty(http.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task The_explicit_choice_wins_over_the_browser_and_the_cookie()
    {
        await using ServiceProvider services = Build();

        (string greeting, _, _) = await RequestAsync(
            services,
            http =>
            {
                http.Request.QueryString = new QueryString("?culture=it-IT");
                http.Request.Headers.Cookie = $"{CookieCultureSource.DefaultCookieName}=de-DE";
                http.Request.Headers.AcceptLanguage = "en-US";
            });

        Assert.Equal("Ciao", greeting);
    }

    [Fact]
    public async Task A_fallback_culture_applies_when_nothing_was_asked_for()
    {
        await using ServiceProvider services = Build();

        (string greeting, _, _) = await RequestAsync(
            services,
            _ => { },
            options => options.FallbackCulture = "de-DE");

        Assert.Equal("Hallo", greeting);
    }

    [Fact]
    public async Task The_culture_does_not_leak_out_of_the_request()
    {
        await using ServiceProvider services = Build();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        await RequestAsync(services, http => http.Request.QueryString = new QueryString("?culture=it-IT"));

        Assert.Equal("Hello", localizer["Greeting"]);
    }

    [Fact]
    public async Task Two_hundred_overlapping_requests_each_keep_their_own_language()
    {
        // The point of the whole package: concurrent requests in different languages, through one
        // singleton localizer.
        await using ServiceProvider services = Build();

        string[] cultures = ["en-US", "it-IT", "de-DE"];
        string[] expected = ["Hello", "Ciao", "Hallo"];

        Task<(string Greeting, string Culture, HttpContext Context)>[] requests =
        [
            .. Enumerable.Range(0, 200).Select(index => Task.Run(async () =>
            {
                int language = index % 3;
                return await RequestAsync(
                    services,
                    http => http.Request.QueryString = new QueryString($"?culture={cultures[language]}"));
            })),
        ];

        (string Greeting, string Culture, HttpContext Context)[] results = await Task.WhenAll(requests);

        for (int index = 0; index < results.Length; index++)
        {
            Assert.Equal(expected[index % 3], results[index].Greeting);
            Assert.Equal(cultures[index % 3], results[index].Culture);
        }
    }

    [Fact]
    public async Task The_first_request_loads_the_resources_so_a_scope_has_something_to_use()
    {
        // Without this, the first request would hit "the resources are not loaded" from the scope: a
        // scope is synchronous and cannot load them itself.
        await using ServiceProvider services = Build();

        Assert.False(services.GetRequiredService<ILocalizationContext>().IsInitialized);

        (string greeting, _, _) = await RequestAsync(
            services,
            http => http.Request.QueryString = new QueryString("?culture=it-IT"));

        Assert.Equal("Ciao", greeting);
        Assert.True(services.GetRequiredService<ILocalizationContext>().IsInitialized);
    }

    [Fact]
    public async Task A_language_switch_endpoint_can_remember_the_choice()
    {
        await using ServiceProvider services = Build();
        CookieCultureSource cookies = new();

        DefaultHttpContext httpContext = new();
        cookies.Append(httpContext, "it-IT");

        string setCookie = httpContext.Response.Headers.SetCookie.ToString();

        Assert.Contains($"{CookieCultureSource.DefaultCookieName}=it-IT", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);

        // And the next request reads it back.
        (string greeting, _, _) = await RequestAsync(
            services,
            http => http.Request.Headers.Cookie = $"{CookieCultureSource.DefaultCookieName}=it-IT");

        Assert.Equal("Ciao", greeting);
    }
}
