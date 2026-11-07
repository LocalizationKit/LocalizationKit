using System.Globalization;
using LocalizationKit;
using Microsoft.Extensions.DependencyInjection;

// The smallest end-to-end use of the kit: JSON resources, ICU messages, a language switch and a
// culture scope. It is also how the trimming and native-AOT claim is checked — CI publishes this
// sample with TrimMode=full and with PublishAot, runs it, and fails on a non-zero exit code. Each
// line below therefore both demonstrates something and asserts it.
ServiceCollection services = new();

services.AddIcuMessages();
services.AddLocalizationKit(options =>
{
    options.DefaultCulture = "en-US";
    options.SupportedCultures = ["en-US", "it-IT"];
    options.InitialCulture = "en-US";

    // Needed by the culture scope further down: a scope is synchronous and cannot load resources.
    options.PreloadSupportedCultures = true;
});

services.AddJsonLocalization(options => options.RootDirectory = AppContext.BaseDirectory);

await using ServiceProvider provider = services.BuildServiceProvider();

ILocalizationContext context = await provider.InitializeLocalizationKitAsync();
ILocalizer localizer = provider.GetRequiredService<ILocalizer>();

int failures = 0;

Check("one item", "1 item", Items(1));
Check("four items", "4 items", Items(4));

// A language change is a dictionary swap: no PRI, no restart, no reload of anything else.
await context.SetCultureAsync("it-IT");

Check("italian, one", "1 articolo", Items(1));
Check("italian, four", "4 articoli", Items(4));

// One flow in another language, while the selected culture stays Italian. This is what lets a server
// answer two users in two languages at once.
using (context.BeginCultureScope("en-US"))
{
    Check("inside a scope", "Welcome, Francesco!", Welcome("Francesco"));
}

Check("after the scope", "Benvenuto, Francesco!", Welcome("Francesco"));

// CLDR plural rules for a language this application does not even ship: Polish needs "few" for 2 and
// "many" for 5, which is the sort of thing that cannot be written by hand in each application.
IMessageFormatter formatter = provider.GetRequiredService<IMessageFormatter>();
const string Files = "{count, plural, one {# plik} few {# pliki} many {# plików} other {# pliku}}";

Check("polish, few", "2 pliki", Polish(2));
Check("polish, many", "5 plików", Polish(5));

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures;

string Items(int count) => localizer.Format(
    "Cart.Items",
    new Dictionary<string, object?> { ["count"] = count });

string Welcome(string name) => localizer.Format(
    "Welcome",
    new Dictionary<string, object?> { ["name"] = name });

string Polish(int count) => formatter.Format(
    Files,
    new Dictionary<string, object?> { ["count"] = count },
    CultureInfo.GetCultureInfo("pl-PL"));

void Check(string what, string expected, string actual)
{
    bool passed = string.Equals(expected, actual, StringComparison.Ordinal);
    failures += passed ? 0 : 1;

    Console.WriteLine(passed
        ? $"ok    {what,-18}  {actual}"
        : $"FAIL  {what,-18}  {actual}  (expected: {expected})");
}
