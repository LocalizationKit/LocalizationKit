using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalizationKit.Messages.Icu.Tests;

/// <summary>
/// Runs the engine against CLDR's own plural sample data: for every locale and category CLDR
/// publishes, every sample number must select that category.
/// </summary>
/// <remarks>
/// <para>
/// A hand-written matrix proves a few languages work. This proves the engine agrees with the source
/// of truth for all of them, which is the whole reason for taking a dependency rather than writing
/// plural rules by hand. 185 cardinal locales and 100 ordinal locales are exercised.
/// </para>
/// <para>
/// Integer samples only. CLDR's decimal samples encode how many fraction digits are visible, which
/// decides the category in several languages, and this API takes a number rather than a formatted
/// string, so those cannot be expressed unambiguously. <c>CldrPluralSamples.json</c> records the
/// provenance of the data.
/// </para>
/// </remarks>
public sealed class CldrConformanceTests
{
    private const string AllCategories = "zero {zero} one {one} two {two} few {few} many {many} other {other}";

    /// <summary>
    /// Locales where the engine's CLDR snapshot is older than the data this test is generated from.
    /// </summary>
    /// <remarks>
    /// These were measured against two independent engines, Rh.MessageFormat and jeffijoe's
    /// MessageFormat, which disagree with CLDR <c>main</c> on exactly the same locales and exactly the
    /// same number of samples. Two unrelated implementations agreeing with each other and differing
    /// from the newest data is version skew, not a bug in either: these rules changed in CLDR after
    /// both snapshots were taken. Galician gaining <c>many</c> for 10^6, and Spanish and Bulgarian
    /// gaining ordinal categories, are recent additions of exactly that kind.
    /// <para>
    /// The set is asserted rather than skipped, so a new disagreement fails the test and an engine
    /// update that fixes one of these is noticed.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string[]> KnownVersionSkew = new(StringComparer.Ordinal)
    {
        ["plural"] = ["gl", "tg", "vi"],
        ["selectordinal"] = ["af", "bg", "es"],
    };

    private static readonly IcuMessageFormatter Formatter = new();
    private static readonly SampleData Samples = Load();

    [Fact]
    public void Cardinal_categories_match_cldr()
    {
        Result result = Check(Samples.Cardinal, "plural");

        // Guards against the test quietly becoming vacuous if the data or the culture lookup breaks.
        Assert.True(result.Locales > 150, $"Only {result.Locales} locales were exercised.");
        Assert.Equal(KnownVersionSkew["plural"], result.Disagreeing);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Ordinal_categories_match_cldr()
    {
        Result result = Check(Samples.Ordinal, "selectordinal");

        Assert.True(result.Locales > 60, $"Only {result.Locales} locales were exercised.");
        Assert.Equal(KnownVersionSkew["selectordinal"], result.Disagreeing);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void The_languages_this_project_ships_are_covered_and_correct()
    {
        foreach (string language in new[] { "en", "it", "pl", "ar", "ru", "cy", "de", "fr", "pt", "nl" })
        {
            Assert.True(Samples.Cardinal.ContainsKey(language), $"No CLDR samples for '{language}'.");
            Assert.True(
                Formatter.SupportsCulture(CultureInfo.GetCultureInfo(language)),
                $"The engine has no CLDR data for '{language}'.");

            Assert.DoesNotContain(language, KnownVersionSkew["plural"]);
        }
    }

    [Fact]
    public void A_culture_without_cldr_data_is_reported_rather_than_guessed()
    {
        // Najdi Arabic: CLDR gives it Arabic's rules, this engine has no data for the tag.
        CultureInfo culture = CultureInfo.GetCultureInfo("ars");
        Assert.False(Formatter.SupportsCulture(culture));

        // A plain message needs no rules, so it still works.
        Assert.Equal(
            "Ciao Francesco",
            Formatter.Format("Ciao {name}", Arguments(("name", "Francesco")), culture));

        // One that does need them says so, instead of borrowing another language's grammar.
        CultureDataUnavailableException error = Assert.Throws<CultureDataUnavailableException>(
            () => Formatter.Format("{n, plural, one {one} other {other}}", Arguments(("n", 1)), culture));

        Assert.Equal("ars", error.Culture?.Name);
        Assert.Equal("plural", error.Construct);
    }

    [Fact]
    public void A_region_falls_back_to_its_language()
    {
        // No engine data for the exact tag is fine as long as a parent covers it.
        Assert.True(Formatter.SupportsCulture(CultureInfo.GetCultureInfo("it-CH")));
        Assert.Equal(
            "3 articoli",
            Formatter.Format(
                "{n, plural, one {# articolo} other {# articoli}}",
                Arguments(("n", 3)),
                CultureInfo.GetCultureInfo("it-CH")));
    }

    private static Dictionary<string, object?> Arguments(params (string Key, object? Value)[] arguments)
    {
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach ((string key, object? value) in arguments)
        {
            values[key] = value;
        }

        return values;
    }

    private static Result Check(Dictionary<string, Dictionary<string, int[]>> data, string construct)
    {
        string pattern = "{n, " + construct + ", " + AllCategories + "}";
        List<string> failures = [];
        SortedSet<string> disagreeing = new(StringComparer.Ordinal);
        string[] skew = KnownVersionSkew[construct];
        int exercised = 0;

        foreach ((string locale, Dictionary<string, int[]> categories) in data)
        {
            if (!TryGetCulture(locale, out CultureInfo? culture) || !Formatter.SupportsCulture(culture))
            {
                // Out of scope: a tag .NET cannot resolve, or one the engine has no data for. The
                // latter is mostly deprecated ISO codes such as iw, in, ji, jw, mo, sh and tl.
                continue;
            }

            exercised++;

            foreach ((string expected, int[] samples) in categories)
            {
                foreach (int sample in samples)
                {
                    string actual = Formatter.Format(
                        pattern,
                        new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = sample },
                        culture);

                    if (actual == expected)
                    {
                        continue;
                    }

                    disagreeing.Add(locale);

                    if (!skew.Contains(locale, StringComparer.Ordinal))
                    {
                        failures.Add($"{locale} {construct} {sample}: expected '{expected}', got '{actual}'");
                    }
                }
            }
        }

        return new Result(exercised, [.. disagreeing], Summarize(failures));
    }

    /// <summary>Keeps a failure message readable when a whole language is wrong.</summary>
    private static List<string> Summarize(List<string> failures)
    {
        if (failures.Count <= 20)
        {
            return failures;
        }

        List<string> shown = [.. failures.Take(20)];
        shown.Add($"... and {failures.Count - 20} more");
        return shown;
    }

    private static bool TryGetCulture(string locale, [NotNullWhen(true)] out CultureInfo? culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(locale, predefinedOnly: true);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = null;
            return false;
        }
    }

    private static SampleData Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CldrPluralSamples.json");
        string json = File.ReadAllText(path, Encoding.UTF8);

        return JsonSerializer.Deserialize<SampleData>(json)
            ?? throw new InvalidOperationException($"'{path}' could not be read.");
    }

    private sealed record Result(int Locales, string[] Disagreeing, List<string> Failures);

    private sealed class SampleData
    {
        [JsonPropertyName("cardinal")]
        public Dictionary<string, Dictionary<string, int[]>> Cardinal { get; init; } = [];

        [JsonPropertyName("ordinal")]
        public Dictionary<string, Dictionary<string, int[]>> Ordinal { get; init; } = [];
    }
}
