using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Tests;

/// <summary>
/// Culture scopes: what lets one process serve two languages at once, which is the case a
/// process-wide selected culture cannot represent.
/// </summary>
public sealed class CultureScopeTests
{
    private static ServiceProvider Build(bool preload = true, bool flowToThreadCulture = false)
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT", "de-DE"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = flowToThreadCulture;
            options.PreloadSupportedCultures = preload;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new InMemoryResourceProvider()
            .With("en-US", ("Common.Save", "Save"))
            .With("it-IT", ("Common.Save", "Salva"))
            .With("de-DE", ("Common.Save", "Speichern")));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_scope_overrides_the_culture_and_restores_it()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        Assert.Equal("Save", localizer["Common.Save"]);

        using (context.BeginCultureScope("it-IT"))
        {
            Assert.Equal("Salva", localizer["Common.Save"]);
            Assert.Equal("it-IT", context.UICulture.Name);
        }

        Assert.Equal("Save", localizer["Common.Save"]);
        Assert.Equal("en-US", context.UICulture.Name);
    }

    [Fact]
    public async Task Scopes_nest()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        using (context.BeginCultureScope("it-IT"))
        {
            using (context.BeginCultureScope("de-DE"))
            {
                Assert.Equal("Speichern", localizer["Common.Save"]);
            }

            Assert.Equal("Salva", localizer["Common.Save"]);
        }

        Assert.Equal("Save", localizer["Common.Save"]);
    }

    [Fact]
    public async Task A_scope_applies_to_work_awaited_inside_it()
    {
        // The reason the scope is pushed in the caller's frame: an AsyncLocal written inside an async
        // method would not escape to here, but one written here does reach what this frame awaits.
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        using (context.BeginCultureScope("it-IT"))
        {
            await Task.Yield();
            Assert.Equal("Salva", await Task.Run(() => localizer["Common.Save"]));
            Assert.Equal("Salva", await NestedAsync(localizer));
        }

        static async Task<string> NestedAsync(ILocalizer localizer)
        {
            await Task.Delay(1);
            return localizer["Common.Save"];
        }
    }

    [Fact]
    public async Task Concurrent_flows_do_not_see_each_other_s_culture()
    {
        // Two hundred overlapping "requests" in three languages: the case that rules out a static
        // global, and the one OVERVIEW section 3 is written around.
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        (string Culture, string Expected)[] expectations =
        [
            ("en-US", "Save"),
            ("it-IT", "Salva"),
            ("de-DE", "Speichern"),
        ];

        IEnumerable<Task> requests = Enumerable.Range(0, 200).Select(async index =>
        {
            (string culture, string expected) = expectations[index % expectations.Length];

            using (context.BeginCultureScope(culture))
            {
                await Task.Yield();
                Assert.Equal(expected, localizer["Common.Save"]);

                await Task.Delay(index % 5);
                Assert.Equal(expected, localizer["Common.Save"]);
                Assert.Equal(culture, context.UICulture.Name);
            }
        });

        await Task.WhenAll(requests);

        Assert.Equal("Save", localizer["Common.Save"]);
    }

    [Fact]
    public async Task A_scope_does_not_raise_the_application_wide_event()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        int changes = 0;
        context.CultureChanged += (_, _) => changes++;

        using (context.BeginCultureScope("it-IT"))
        {
            Assert.Equal("it-IT", context.UICulture.Name);
        }

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task An_unsupported_culture_cannot_be_scoped_to()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        Assert.Throws<CultureNotSupportedException>(() => context.BeginCultureScope("ja-JP"));
    }

    [Fact]
    public async Task A_culture_that_was_not_preloaded_says_what_to_do()
    {
        await using ServiceProvider services = Build(preload: false);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => context.BeginCultureScope("de-DE"));

        Assert.Contains("PreloadAsync", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preloading_on_demand_makes_a_scope_possible()
    {
        await using ServiceProvider services = Build(preload: false);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        await context.PreloadAsync();

        using (context.BeginCultureScope("de-DE"))
        {
            Assert.Equal("Speichern", localizer["Common.Save"]);
        }
    }

    [Fact]
    public async Task A_scope_can_carry_the_formatting_culture_too()
    {
        await using ServiceProvider services = Build(flowToThreadCulture: true);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        CultureInfo before = CultureInfo.CurrentCulture;

        using (context.BeginCultureScope("de-DE"))
        {
            Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
            Assert.Equal("1.234,50", 1234.50m.ToString("N2", CultureInfo.CurrentCulture));
        }

        Assert.Equal(before.Name, CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public async Task A_region_is_negotiated_to_a_supported_culture()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        using (context.BeginCultureScope("it-CH"))
        {
            Assert.Equal("Salva", localizer["Common.Save"]);
        }
    }
}
