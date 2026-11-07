using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using LocalizationKit.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace LocalizationKit.Generators.Tests;

/// <summary>Runs the generator the way the compiler does, and compiles what it produced.</summary>
/// <remarks>
/// Asserting on the generated text alone would miss the failure that matters most: source that does
/// not compile. Every case here therefore compiles the result and asserts there are no errors.
/// </remarks>
internal static class GeneratorHarness
{
    public static GeneratorResult Run(
        string reswXml,
        IReadOnlyDictionary<string, string>? options = null,
        bool markAsResourceFile = true)
    {
        AdditionalTextStub resources = new("Strings/en-US/Resources.resw", reswXml);

        Dictionary<string, string> globals = new(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = "Sample.App",
        };

        if (options is not null)
        {
            foreach (KeyValuePair<string, string> option in options)
            {
                globals[option.Key] = option.Value;
            }
        }

        Dictionary<string, string> perFile = markAsResourceFile
            ? new(StringComparer.Ordinal) { ["build_metadata.AdditionalFiles.LocalizationKitKeys"] = "true" }
            : new(StringComparer.Ordinal);

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Sample.App",
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new LocalizationKeysGenerator())
            .AddAdditionalTexts([resources])
            .WithUpdatedAnalyzerConfigOptions(new OptionsProvider(globals, resources, perFile));

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updated,
            out ImmutableArray<Diagnostic> generatorDiagnostics);

        GeneratorDriverRunResult result = driver.GetRunResult();

        string source = result.GeneratedTrees.Length == 0
            ? string.Empty
            : result.GeneratedTrees[0].ToString();

        ImmutableArray<Diagnostic> compileErrors =
        [
            .. updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error),
        ];

        return new GeneratorResult(source, generatorDiagnostics, compileErrors);
    }

    internal sealed record GeneratorResult(
        string Source,
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<Diagnostic> CompileErrors);

    private sealed class AdditionalTextStub(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
            => SourceText.From(content, Encoding.UTF8);
    }

    private sealed class OptionsProvider(
        Dictionary<string, string> globals,
        AdditionalText file,
        Dictionary<string, string> perFile) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(globals);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(new(StringComparer.Ordinal));

        public override AnalyzerConfigOptions GetOptions(AdditionalText text)
            => new Options(ReferenceEquals(text, file) ? perFile : new(StringComparer.Ordinal));

        private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
        }
    }
}
