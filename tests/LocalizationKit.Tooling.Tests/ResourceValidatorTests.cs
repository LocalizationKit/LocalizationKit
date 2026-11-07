namespace LocalizationKit.Tooling.Tests;

public sealed class ResourceValidatorTests
{
    private static IReadOnlyList<ValidationMessage> Validate(SampleSolution solution)
        => ResourceValidator.Validate(solution.Discover());

    [Fact]
    public void A_complete_set_of_translations_reports_nothing()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        Assert.Empty(Validate(solution));
    }

    [Fact]
    public void Reports_keys_missing_from_a_translation()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.Cancel", "Cancel"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK006");

        Assert.Equal(ValidationSeverity.Warning, message.Severity);
        Assert.Contains("Common.Cancel", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_a_key_that_exists_only_in_a_translation()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"), new ReswEntry("Leftover", "Vecchio"));

        Assert.Contains(Validate(solution), m => m.Code == "LK007" && m.Text.Contains("Leftover", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_an_untranslated_value()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"))
            .WithResw("it-IT", new ReswEntry("Common.Save", string.Empty));

        Assert.Contains(Validate(solution), m => m.Code == "LK008");
    }

    [Fact]
    public void Reports_a_malformed_message_as_an_error()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("Broken", "Hello {name"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK009");

        Assert.Equal(ValidationSeverity.Error, message.Severity);
    }

    [Fact]
    public void Accepts_a_message_written_for_the_phase_2_plural_engine()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Cart.Items", "{count, plural, =0 {No items} one {# item} other {# items}}"));

        Assert.Empty(Validate(solution));
    }

    [Fact]
    public void Reports_a_default_language_with_no_resources()
    {
        using SampleSolution solution = new(cultures: "it-IT");
        solution.WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK002");

        Assert.Equal(ValidationSeverity.Error, message.Severity);
        Assert.Contains("en-US", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_a_culture_folder_the_project_does_not_declare()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"))
            .WithResw("fr-FR", new ReswEntry("Common.Save", "Enregistrer"));

        Assert.Contains(Validate(solution), m => m.Code == "LK003" && m.Text.Contains("fr-FR", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_a_declared_culture_with_no_file()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution.WithResw("en-US", new ReswEntry("Common.Save", "Save"));

        Assert.Contains(Validate(solution), m => m.Code == "LK004" && m.Text.Contains("it-IT", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_a_default_culture_with_no_entries_at_all()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US");

        Assert.Contains(Validate(solution), m => m.Code == "LK011");
    }

    [Fact]
    public void Reports_a_key_that_is_also_the_prefix_of_another_key()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Settings.Theme.System", "System"),
            new ReswEntry("Settings.Theme.System.ToolTip", "Follow Windows"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK012");

        Assert.Equal(ValidationSeverity.Error, message.Severity);
        Assert.Contains("PRI278", message.Text, StringComparison.Ordinal);
        Assert.Contains("Settings.Theme.SystemToolTip", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotted_keys_that_do_not_collide_are_fine()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Settings.Theme.System", "System"),
            new ReswEntry("Settings.Theme.SystemToolTip", "Follow Windows"),
            new ReswEntry("Settings.Theme.Light", "Light"));

        Assert.Empty(Validate(solution));
    }

    [Fact]
    public void Reports_malformed_link_markup()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("Login.SignUp", "No account? <a>Sign up."));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK013");

        Assert.Equal(ValidationSeverity.Error, message.Severity);
    }

    [Fact]
    public void Reports_a_translation_that_lost_a_link_id()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Login.Terms", "Agree to the <a id=\"terms\">Terms</a>."))
            .WithResw("it-IT", new ReswEntry("Login.Terms", "Accetti i Termini."));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK014");

        Assert.Equal(ValidationSeverity.Error, message.Severity);
        Assert.Contains("terms", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reordered_links_in_a_translation_are_fine()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw(
                "en-US",
                new ReswEntry("Login.Terms", "Agree to the <a id=\"terms\">Terms</a> and <a id=\"privacy\">Privacy</a>."))
            .WithResw(
                "it-IT",
                new ReswEntry("Login.Terms", "Accetti l'<a id=\"privacy\">Informativa</a> e i <a id=\"terms\">Termini</a>."));

        Assert.Empty(Validate(solution));
    }

    [Fact]
    public void Errors_are_listed_before_warnings()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("A", "A"), new ReswEntry("B", "B"))
            .WithResw("it-IT", new ReswEntry("A", "Rotto {"));

        IReadOnlyList<ValidationMessage> messages = Validate(solution);

        Assert.Equal(ValidationSeverity.Error, messages[0].Severity);
    }

    [Fact]
    public void A_finding_about_one_key_carries_the_line_that_key_is_on()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Common.Save", "Save"),
            new ReswEntry("Cart.Items", "{count, plural, one {# item}"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK009");

        Assert.NotNull(message.File);
        Assert.NotNull(message.Line);

        // The line has to be the key's own, which is what makes the build error clickable.
        string[] lines = File.ReadAllLines(message.File!);
        Assert.Contains("Cart.Items", lines[message.Line!.Value - 1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_scope_collision_points_at_the_longer_key()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Settings.Theme", "Theme"),
            new ReswEntry("Settings.Theme.System", "System"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK012");

        Assert.NotNull(message.Line);

        string[] lines = File.ReadAllLines(message.File!);
        Assert.Contains("Settings.Theme.System", lines[message.Line!.Value - 1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_finding_that_summarizes_many_keys_has_no_line()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.Cancel", "Cancel"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK006");

        // There is no single line to blame, and inventing one would send the reader to the wrong key.
        Assert.Null(message.Line);
        Assert.NotNull(message.File);
    }

    [Fact]
    public void A_translation_that_drops_an_argument_is_an_error()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Cart.Items", "{count, plural, one {# item} other {# items}}"))
            .WithResw("it-IT", new ReswEntry("Cart.Items", "Articoli nel carrello"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK015");

        Assert.Equal(ValidationSeverity.Error, message.Severity);
        Assert.Contains("count", message.Text, StringComparison.Ordinal);
        Assert.NotNull(message.Line);
    }

    [Fact]
    public void A_translation_that_renames_an_argument_is_an_error()
    {
        // The sentence is in Italian, so the placeholder gets translated too; the engine then has no
        // value for it and the message fails at runtime rather than at build time.
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Welcome", "Welcome, {name}!"))
            .WithResw("it-IT", new ReswEntry("Welcome", "Benvenuto, {nome}!"));

        ValidationMessage message = Assert.Single(Validate(solution), m => m.Code == "LK015");

        Assert.Contains("name", message.Text, StringComparison.Ordinal);
        Assert.Contains("nome", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Arguments_in_a_different_order_are_fine()
    {
        // Word order is the translator's business; which arguments exist is not.
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Range", "{from} to {to}"))
            .WithResw("it-IT", new ReswEntry("Range", "Da {to} a {from}"));

        Assert.DoesNotContain(Validate(solution), m => m.Code == "LK015");
    }

    [Fact]
    public void A_nested_argument_counts_as_used()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry(
                "Tagged",
                "{count, plural, =0 {{total, plural, one {one} other {#}}} other {Added to #}}"))
            .WithResw("it-IT", new ReswEntry(
                "Tagged",
                "{count, plural, =0 {{total, plural, one {una} other {#}}} other {Aggiunta a #}}"));

        Assert.DoesNotContain(Validate(solution), m => m.Code == "LK015");
    }

    [Fact]
    public void A_malformed_translation_is_only_reported_once()
    {
        // LK009 already says the message is broken; saying its arguments disagree adds nothing.
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Cart.Items", "{count, plural, one {# item} other {# items}}"))
            .WithResw("it-IT", new ReswEntry("Cart.Items", "{count, plural, one {# articolo}"));

        IReadOnlyList<ValidationMessage> found = Validate(solution);

        Assert.Contains(found, m => m.Code == "LK009");
        Assert.DoesNotContain(found, m => m.Code == "LK015");
    }

    [Fact]
    public void A_key_no_source_file_mentions_is_listed()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Common.Save", "Save"),
            new ReswEntry("Forgotten.Key", "Nobody reads this"));

        solution.WithSource("MainPage.xaml.cs", "Localizer[\"Common.Save\"];");

        ValidationMessage message = Assert.Single(UnusedKeyScanner.Scan(solution.Discover()));

        Assert.Equal("LK016", message.Code);
        Assert.Equal(ValidationSeverity.Info, message.Severity);
        Assert.Contains("Forgotten.Key", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Common.Save", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_reached_through_the_generated_constants_counts_as_used()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("Settings.Theme.Header", "Theme"));
        solution.WithSource("SettingsPage.xaml.cs", "Localizer[LocalizationKeys.Settings.Theme.Header];");

        Assert.Empty(UnusedKeyScanner.Scan(solution.Discover()));
    }

    [Fact]
    public void A_key_whose_name_csharp_would_reject_is_found_through_its_identifier()
    {
        // The generator writes "Delete forever" as Delete_forever, so the dotted name never appears.
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("Dialog.Delete forever", "Delete forever"));
        solution.WithSource("MainPage.xaml.cs", "Localizer[LocalizationKeys.Dialog.Delete_forever];");

        Assert.Empty(UnusedKeyScanner.Scan(solution.Discover()));
    }

    [Fact]
    public void A_key_named_in_xaml_counts_as_used()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("Common.Save", "Save"));
        solution.WithSource("MainPage.xaml", "<Button Content=\"{loc:Text Key=Common.Save}\" />");

        Assert.Empty(UnusedKeyScanner.Scan(solution.Discover()));
    }
}
