namespace LocalizationKit.Tooling.Tests;

/// <summary>
/// The hand-off to a translator and back. The round trip is the property worth holding on to: a
/// catalogue written and read again has to be the strings that went into it, and an import has to
/// leave the file it touched looking like the file it was.
/// </summary>
public sealed class TranslationTransferTests
{
    private static readonly TransferUnit[] Units =
    [
        new("Common.Save", "Save", "Salva"),
        new("Cart.Items", "{count, plural, one {# item} other {# items}}", string.Empty, "# is the count"),
        new("Dialog.Quoted", "He said \"no\", and left\nwithout a word", "Disse «no» e se ne andò"),
        new("Login.Hint", "Read the <a id=\"help\">guide</a>", "Leggi la <a id=\"help\">guida</a>"),
    ];

    [Theory]
    [InlineData(TransferFormat.Po)]
    [InlineData(TransferFormat.Xliff)]
    public void A_catalogue_round_trips(TransferFormat format)
    {
        string text = format == TransferFormat.Po
            ? PoFile.Write(Units, "en-US", "it-IT")
            : XliffFile.Write(Units, "en-US", "it-IT");

        IReadOnlyList<TransferUnit> read = format == TransferFormat.Po
            ? PoFile.Read(text)
            : XliffFile.Read(text);

        Assert.Equal(Units.Length, read.Count);

        foreach (TransferUnit expected in Units)
        {
            TransferUnit actual = Assert.Single(read, unit => unit.Key == expected.Key);

            Assert.Equal(expected.Source, actual.Source);
            Assert.Equal(expected.Target, actual.Target);
            Assert.Equal(expected.Comment, actual.Comment);
        }
    }

    [Fact]
    public void A_po_catalogue_says_which_languages_it_is_between()
    {
        string text = PoFile.Write(Units, "en-US", "it-IT");

        Assert.Contains("\"Language: it-IT\\n\"", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Source-Language: en-US\\n\"", text, StringComparison.Ordinal);
        Assert.Contains("\"Content-Type: text/plain; charset=UTF-8\\n\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_untranslated_string_is_marked_in_both_formats()
    {
        // A translator's tool filters on this, and it is the difference between a file of work and a
        // file of noise.
        Assert.Contains("#, fuzzy", PoFile.Write(Units, "en-US", "it-IT"), StringComparison.Ordinal);
        Assert.Contains("needs-translation", XliffFile.Write(Units, "en-US", "it-IT"), StringComparison.Ordinal);
    }

    [Fact]
    public void An_icu_message_crosses_whole()
    {
        // It is not taken apart into PO's plural forms: see the note on PoFile for why not.
        string text = PoFile.Write(Units, "en-US", "it-IT");

        Assert.Contains("{count, plural, one {# item} other {# items}}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Exporting_carries_the_translations_a_culture_already_has()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save", "The toolbar button"), new ReswEntry("Common.New", "New"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        IReadOnlyList<TransferUnit> units = PoFile.Read(
            TranslationTransfer.Export(solution.Discover(), "it-IT", TransferFormat.Po));

        Assert.Equal("Salva", Assert.Single(units, unit => unit.Key == "Common.Save").Target);
        Assert.Equal(string.Empty, Assert.Single(units, unit => unit.Key == "Common.New").Target);
        Assert.Equal("The toolbar button", Assert.Single(units, unit => unit.Key == "Common.Save").Comment);
    }

    [Fact]
    public void Importing_changes_only_the_lines_it_has_to()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.New", "New"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"), new ReswEntry("Common.New", "Nuovo"));

        ProjectContext project = solution.Discover();
        string path = project.ResourceFileFor("it-IT");
        string before = File.ReadAllText(path);

        ImportResult result = TranslationTransfer.Import(
            project,
            "it-IT",
            PoFile.Write([new TransferUnit("Common.Save", "Save", "Conserva")], "en-US", "it-IT"),
            TransferFormat.Po);

        FileChange change = Assert.Single(result.Changes);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Added);

        // One value differs, and nothing else: the file is edited rather than regenerated.
        Assert.Equal(
            before.Replace("<value>Salva</value>", "<value>Conserva</value>", StringComparison.Ordinal),
            change.NewContent);
    }

    [Fact]
    public void Importing_adds_a_key_the_culture_did_not_have()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.New", "New"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ImportResult result = TranslationTransfer.Import(
            solution.Discover(),
            "it-IT",
            XliffFile.Write([new TransferUnit("Common.New", "New", "Nuovo")], "en-US", "it-IT"),
            TransferFormat.Xliff);

        Assert.Equal(1, result.Added);
        Assert.Contains("<value>Nuovo</value>", Assert.Single(result.Changes).NewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_the_default_culture_no_longer_has_is_left_out()
    {
        // The string was renamed or deleted while the translator had the file; writing it back would
        // bring it back from the dead.
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ImportResult result = TranslationTransfer.Import(
            solution.Discover(),
            "it-IT",
            PoFile.Write(
                [new TransferUnit("Common.Save", "Save", "Salva"), new TransferUnit("Gone.Key", "Gone", "Andato")],
                "en-US",
                "it-IT"),
            TransferFormat.Po);

        Assert.Equal(1, result.Skipped);
        Assert.Empty(result.Changes);
        Assert.Contains(result.Notes, note => note.Contains("1 unit", StringComparison.Ordinal));
    }

    [Fact]
    public void A_culture_with_no_file_yet_gets_one()
    {
        using SampleSolution solution = new(cultures: "en-US;de-DE");
        solution.WithResw("en-US", new ReswEntry("Common.Save", "Save"));

        ImportResult result = TranslationTransfer.Import(
            solution.Discover(),
            "de-DE",
            PoFile.Write([new TransferUnit("Common.Save", "Save", "Speichern")], "en-US", "de-DE"),
            TransferFormat.Po);

        FileChange change = Assert.Single(result.Changes);

        Assert.Equal(FileChangeKind.Create, change.Kind);
        Assert.True(change.WriteByteOrderMark);
        Assert.Contains("<value>Speichern</value>", change.NewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_translation_changes_nothing()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ImportResult result = TranslationTransfer.Import(
            solution.Discover(),
            "it-IT",
            PoFile.Write([new TransferUnit("Common.Save", "Save", string.Empty)], "en-US", "it-IT"),
            TransferFormat.Po);

        Assert.Empty(result.Changes);
        Assert.Contains(result.Notes, note => note.Contains("already in the file", StringComparison.Ordinal));
    }

    [Fact]
    public void A_malformed_catalogue_says_so()
    {
        Assert.Throws<FormatException>(() => PoFile.Read("msgid \"unterminated\nmsgstr \"\""));
        Assert.Throws<FormatException>(() => XliffFile.Read("<xliff><file>"));
        Assert.Throws<FormatException>(() => XliffFile.Read("<notxliff />"));
    }

    [Fact]
    public void Importing_leaves_the_target_files_own_notes_alone()
    {
        // The note in the exchange file is the source culture's note to the translator. The target
        // file's note is its own, and was written about this translation; overwriting it with the
        // English one loses the only copy. Found by exporting a real project and importing it back
        // unchanged, which reported a change it should not have.
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save", "The toolbar button"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva", "Il pulsante della barra"));

        ProjectContext project = solution.Discover();
        string exported = TranslationTransfer.Export(project, "it-IT", TransferFormat.Po);

        ImportResult result = TranslationTransfer.Import(project, "it-IT", exported, TransferFormat.Po);

        Assert.Empty(result.Changes);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void A_key_new_to_a_culture_keeps_the_note_it_arrived_with()
    {
        // There is nothing to protect in that case, and the note is the context a translator needs.
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.New", "New", "Context"))
            .WithResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ImportResult result = TranslationTransfer.Import(
            solution.Discover(),
            "it-IT",
            PoFile.Write([new TransferUnit("Common.New", "New", "Nuovo", "Context")], "en-US", "it-IT"),
            TransferFormat.Po);

        Assert.Contains("<comment>Context</comment>", Assert.Single(result.Changes).NewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Exporting_and_importing_a_whole_culture_unchanged_is_a_no_op()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution
            .WithResw(
                "en-US",
                new ReswEntry("Common.Save", "Save", "A note"),
                new ReswEntry("Cart.Items", "{count, plural, one {# item} other {# items}}"),
                new ReswEntry("Login.Hint", "Read the <a id=\"help\">guide</a>, or \"ask\""))
            .WithResw(
                "it-IT",
                new ReswEntry("Common.Save", "Salva"),
                new ReswEntry("Cart.Items", "{count, plural, one {# articolo} other {# articoli}}"),
                new ReswEntry("Login.Hint", "Leggi la <a id=\"help\">guida</a>, o \"chiedi\""));

        ProjectContext project = solution.Discover();

        foreach (TransferFormat format in new[] { TransferFormat.Po, TransferFormat.Xliff })
        {
            string exported = TranslationTransfer.Export(project, "it-IT", format);
            ImportResult result = TranslationTransfer.Import(project, "it-IT", exported, format);

            Assert.Empty(result.Changes);
        }
    }
}
