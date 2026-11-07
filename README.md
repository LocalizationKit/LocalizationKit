# LocalizationKit

One localization API for every .NET application: resource providers, runtime culture switching,
observable localized strings, ICU pluralization and culture-aware formatting — with no dependency on
any UI framework.

```csharp
localizer.Plural("Cart.Items", 5);        // "5 articoli"  ·  "5 plików"  ·  "5 items"
await context.SetCultureAsync("it-IT");   // every bound string on screen updates
```

`.resw` is the default resource format, read as loose XML at runtime rather than resolved through a
compiled PRI — which is what makes switching language a dictionary swap instead of a restart. JSON
and YAML providers are available too, and writing another is one interface.

> **Status:** 0.1, pre-release. The library is feature-complete against its roadmap and covered by
> 367 tests, including CLDR plural conformance across 285 locales. Nothing is published to NuGet
> yet, so the package references below do not resolve until the first release.

## Contents

- [Install](#install) · [Quick start](#quick-start) · [Resource files](#resource-files)
- [Pluralization](#pluralization) · [Formatting](#formatting)
- UI frameworks: [WinUI 3](#winui-3) · [WPF](#wpf) · [MAUI](#maui)
- Servers: [ASP.NET Core](#aspnet-core) · [Two languages at once](#two-languages-at-once) · [IStringLocalizer](#existing-code-that-uses-istringlocalizer)
- Tooling: [Validation on build](#validation-on-build) · [Typed keys](#typed-keys) · [The editor](#the-editor) · [The CLI](#the-cli)
- [Pseudolocalization](#pseudolocalization) · [Translator hand-off](#handing-a-culture-to-a-translator) · [Trimming and AOT](#trimming-and-native-aot)
- [Packages](#packages) · [Contributing](#contributing) · [License](#license)

## Install

```
dotnet add package LocalizationKit
dotnet add package LocalizationKit.Resources.Resw
dotnet add package LocalizationKit.Messages.Icu      # plurals, ordinals, select
```

Add the UI package for your framework — `LocalizationKit.WinUI`, `LocalizationKit.Wpf`,
`LocalizationKit.Maui` or `LocalizationKit.AspNetCore`. The full list is under [Packages](#packages).

## Quick start

```csharp
services.AddLocalizationKit(options =>
{
    options.DefaultCulture = "en-US";
    options.SupportedCultures = ["en-US", "it-IT"];
    options.FallbackBehavior = FallbackBehavior.ReturnKey;
});

services.AddReswLocalization(options =>
{
    options.ResourcesPath = "Strings";       // Strings/<culture>/Resources.resw
    options.FileName = "Resources.resw";
});
```

Load the resources once during startup. Resolving a key before that throws, rather than quietly
returning key names:

```csharp
await serviceProvider.InitializeLocalizationKitAsync();
```

Then inject and use:

```csharp
public sealed class SettingsViewModel(ILocalizer localizer, ILocalizationFormatter formatter)
{
    // Re-reads itself when the culture changes.
    public ILocalizedString SaveText { get; } = localizer.Observe("Common.Save");

    public string Welcome => localizer.Format("Welcome", new { name = "Francesco" });

    public string Price => formatter.Currency(123.50m, "EUR");
}
```

Switching language updates every observed string:

```csharp
await context.SetCultureAsync("it-IT");
```

### Resource files

```
Strings/
├── en-US/Resources.resw
└── it-IT/Resources.resw
```

Values are message patterns, so `{name}` substitution and ICU apostrophe quoting work as they do in
ICU MessageFormat. `L'articolo` stays intact; `'{'` escapes a brace.

## Pluralization

Add the engine once:

```csharp
services.AddIcuMessages();   // LocalizationKit.Messages.Icu
```

The count then lives in the resource, where a translator can move it, and the categories are
whatever the language actually needs:

```
Cart.Items  en-US:  {count, plural, =0 {No items} one {# item} other {# items}}
            it-IT:  {count, plural, =0 {Nessun articolo} one {# articolo} other {# articoli}}
            pl-PL:  {count, plural, one {# plik} few {# pliki} many {# plików} other {# pliku}}
```

```csharp
localizer.Plural("Cart.Items", 5);
```

CLDR decides the category, so Polish gets `one`/`few`/`many`/`other` and Arabic all six, without a
single `if (count == 1)` anywhere in the application. Ordinals (`selectordinal`), gender and other
`select` constructs, nesting, `offset:` and in-message number and date formatting all work.

Without that package the default engine does plain substitution and refuses the rest with
`UnsupportedMessageConstructException`, naming the key and the construct, rather than returning a
plausible but wrong string.

Validation does not depend on the engine. Patterns are checked by `IcuMessageSyntax` in the core
either way, which rejects malformed patterns, a misspelled category such as `oen` (an engine renders
that happily by falling through to `other`), and a `plural` or `select` with no `other` branch.

## Formatting

```csharp
formatter.Number(1234.5);          formatter.Currency(123.50m, "EUR");
formatter.Date(when);              formatter.Relative(then);
```

All of it follows the selected culture, including inside a [culture scope](#two-languages-at-once),
so a formatted number and the sentence around it can never disagree about the language.

## WinUI 3

Reference `LocalizationKit.WinUI`, load the resources before the first window, and hand XAML the
localizer:

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    await Services.InitializeLocalizationKitAsync();
    LocalizationKitXaml.Initialize(Services);

    Window = new MainWindow();
    Window.Activate();
}
```

```xml
<Page xmlns:loc="using:LocalizationKit.WinUI">
    <TextBlock Text="{loc:Text Key=Settings.Title}" />
    <Button Content="{loc:Text Key=Common.Save}"
            ToolTipService.ToolTip="{loc:Text Key=Common.Save.Hint}" />
    <TextBlock loc:Loc.Text="Common.Save" />
</Page>
```

### Sentences containing links

A sentence with a link in it must stay one resource. Split into `"Don't have an account? "`,
`"Sign up"` and `"."`, a translator gets three disconnected fragments and no way to move the link,
which any language with a different word order needs to do. So the link lives inside the string:

```
Login.SignUpPrompt    Don't have an account? <a>Sign up</a>.
Login.ForgotPassword  <a href="https://example.com/forgot/">Forgot your password?</a>
Login.Terms           Agree to the <a id="terms">Terms</a> and the <a id="privacy">Privacy Policy</a>.
```

```xml
<!-- one link: the shorthand -->
<TextBlock loc:Loc.Inlines="Login.SignUpPrompt"
           loc:Loc.LinkCommand="{x:Bind ViewModel.StartSignUpCommand}" />

<!-- the address is in the translation, so nothing to wire -->
<TextBlock loc:Loc.Inlines="Login.ForgotPassword" />

<!-- several links, each matched by id -->
<TextBlock loc:Loc.Inlines="Login.Terms">
    <loc:Loc.Links>
        <loc:Link Id="terms" Command="{x:Bind ViewModel.ShowTermsCommand}" />
        <loc:Link Id="privacy" NavigateUri="https://example.com/privacy" />
    </loc:Loc.Links>
</TextBlock>
```

Links are matched by `id`, never by position, so a translator can reorder them freely. `\n` becomes
a line break. `<a>` and `</a>` are the only markup; any other angle bracket is literal text, so
`"a < b"` reads as written. A link with no command and no address renders as plain text rather than
a dead hyperlink, and malformed markup is shown as written rather than swallowed. `LK013` and
`LK014` fail the build on malformed markup and on link ids that do not match the default culture's.

### Two things to know

`Key=` is required: Windows Runtime XAML has no positional arguments in a custom markup extension. A
markup extension is also evaluated once, when the page loads, so the integration records the element
and property each value went to — weakly — and sets them again on the UI dispatcher when the culture
changes. Where the XAML runtime reports no target, such as inside a resource dictionary, the value is
right at load time but will not refresh; the `loc:Loc` attached properties always refresh.

Mind one `.resw` rule when naming keys: `MakePri` reads a dot as a scope separator, so `Common.Save`
and `Common.Save.Hint` cannot both exist — the build fails with `PRI278`. The validator reports it as
`LK012` first.

### The strings that are not yours

A WinUI window is full of text this engine never sees: a text box's context menu, the navigation
pane's toggle tooltip, a `ContentDialog`'s default buttons, whatever the Community Toolkit's controls
say for themselves. Those come from MRT, out of compiled resources, and MRT neither knows nor cares
which culture LocalizationKit selected — it reads Windows' own setting. So switching language leaves
them behind, in a way that looks like a bug in the switch.

The fix is one line, next to wherever the culture is applied:

```csharp
// Windows.Globalization
ApplicationLanguages.PrimaryLanguageOverride = "it-IT";   // "" to follow Windows
```

A control keeps the strings it loaded, so this reaches framework text in whatever is created
afterwards, and all of it on the next launch — Windows persists the override.

## WPF

```xml
<Window xmlns:loc="clr-namespace:LocalizationKit.Wpf;assembly=LocalizationKit.Wpf">
    <Button Content="{loc:Text Key=Common.Save}" />
    <TextBlock Text="{loc:Text Common.Label, StringFormat={}{0}:}" />
</Window>
```

```csharp
await Services.InitializeLocalizationKitAsync();
LocalizationKitXaml.Initialize(Services);     // before the first window
```

WPF needs less machinery than WinUI for the same result, because a WPF markup extension may return a
*binding* rather than a string: `{loc:Text}` becomes a binding to an indexer on one observable
object, and a language change raises a single `PropertyChanged` that makes the framework re-read
every bound value. The notification is marshalled to the dispatcher that owns the elements, so the
culture can be switched from anywhere.

In the designer, where there is no application and so no localizer, `{loc:Text}` shows the key — or
`DesignTimeValue` when the key is long and the string is not.

## MAUI

```xml
<ContentPage xmlns:loc="clr-namespace:LocalizationKit.Maui;assembly=LocalizationKit.Maui">
    <Button Text="{loc:Text Key=Common.Save}" />
</ContentPage>
```

```csharp
await Services.InitializeLocalizationKitAsync();
LocalizationKitXaml.Initialize(Services);     // before the first page
```

The same idea as WPF, with two differences MAUI forces that are worth knowing if you ever write a
binding against a key yourself. Its path parser splits on dots before it reads an indexer, so
`[Common.Save]` is rejected for a missing bracket — the key therefore goes to a converter instead of
into the path. And a binding whose path is `.` is never subscribed to its source's notifications, so
the binding watches a property that returns the source instead.

## ASP.NET Core

```csharp
builder.Services.AddLocalizationKit(options => { /* ... */ });
builder.Services.AddReswLocalization();          // or AddJsonLocalization()
builder.Services.AddLocalizationKitRequestCulture();

app.UseRouting();
app.UseLocalizationKitRequestCulture();
```

Each request is answered in the language it asked for, and concurrent requests in different
languages do not disturb each other. The culture is negotiated from the query string, then a route
value, then a cookie, then `Accept-Language`: what the user just asked for beats what they asked for
earlier, which beats what their browser guessed. `Sources` is an ordered list you can rebuild.

A language the application does not ship is an ordinary event rather than an error — a header is
attacker-controlled — so an unknown or malformed name leaves the selected culture in place, and a
request for `it` resolves to the shipped `it-IT`. A response whose language came from
`Accept-Language` gets `Vary: Accept-Language`, so a shared cache cannot serve it to the next reader
of another language.

To remember a choice:

```csharp
app.MapPost("/language/{culture}", (HttpContext http, string culture) =>
{
    new CookieCultureSource().Append(http, culture);
    return Results.LocalRedirect("/");
});
```

The middleware also initializes the kit and loads every supported culture when the first request
arrives, so there is no startup call to remember and no ordering to get right.

## Two languages at once

A selected culture is process-wide, which cannot describe two users being served in two languages at
the same time. A scope overrides the culture for one asynchronous flow:

```csharp
// once, during startup
options.PreloadSupportedCultures = true;

// per request, per job, per anything
using (context.BeginCultureScope("it-IT"))
{
    await next();      // everything awaited in here resolves in Italian
}
```

Scopes nest, restore on dispose, and do not raise `CultureChanged`, which stays an application-wide
signal for observable strings. They are deliberately synchronous: an `AsyncLocal` written inside an
`async` method never escapes back to its caller, so a scope that loaded resources before pushing
would silently fail to apply. Resources must be loaded first — hence `PreloadSupportedCultures` or
`PreloadAsync()`.

## Existing code that uses IStringLocalizer

```csharp
services.AddLocalizationKitStringLocalizer();   // LocalizationKit.StringLocalizer
```

`IStringLocalizer`, `IStringLocalizer<T>` and `IStringLocalizerFactory` then resolve through
LocalizationKit, so Razor views, controllers, validation attributes and libraries that take one keep
their call sites. `localizer["Cart.Items", 5]` renders an ICU plural; `localizer["Welcome", name]`
works whether the resource uses `{0}` or `{name}`; and `ResourceNotFound` is accurate, because the
bridge asks `TryGet` rather than taking the configured fallback.

The `T` in `IStringLocalizer<T>` names a key prefix: an `IStringLocalizer<CheckoutPage>` asked for
`Save` looks for `CheckoutPage.Save`, and falls through to the bare key when the prefix has none —
so `Common.Cancel` still resolves. `localizer.Scoped("Checkout")` does the same without the generic.

## Validation on build

```xml
<PackageReference Include="LocalizationKit.Build" Version="..." PrivateAssets="all" />
```

That is the whole setup. Every build then checks the resource files and reports what it finds as
real build diagnostics, with a code and a line, so a malformed plural or a key that cannot survive
`MakePri` fails the build instead of the app:

```
Strings\en-US\Resources.resw(9): error LK009: [en-US] 'Bad.Message' is not a valid message: The
    plural argument 'count' is not closed with '}'.
Strings\en-US\Resources.resw(15): error LK012: [en-US] 'Settings.Theme' is a key and also the prefix
    of 'Settings.Theme.System'. MakePri treats a dot as a scope separator, so this fails the build
    with PRI278. Rename one of them, for example to 'Settings.ThemeSystem'.
Strings\it-IT\Resources.resw(21): error LK015: [it-IT] 'Cart.Items' does not use {count}. The
    arguments of a translation have to match the ones its message is given, or the value is dropped
    at best and the string fails at worst.
Strings\it-IT\Resources.resw : warning LK006: [it-IT] 2 keys are missing: Bad.Message, Cart.Items
```

`LK015` is the one that makes a plural trustworthy. Every other rule checks that a message is well
formed; this checks that it is the *same* message — a translator who drops `{count}`, or writes
`{conteggio}` because the sentence around it is Italian, leaves valid ICU that the engine then has
no value for, and it fails in one language on whichever screen happens to show it.

The package carries its own copy of the CLI, so nothing has to be installed first, and it is skipped
in design-time builds. Everything is a property:

| Property                                  | Default          | Effect                                                   |
| ----------------------------------------- | ---------------- | -------------------------------------------------------- |
| `LocalizationKitValidate`                 | `true`           | Validate on build                                        |
| `LocalizationKitValidateWarningsAsErrors` | `false`          | Make missing translations fail; worth turning on in CI   |
| `LocalizationKitSyncIde`                  | `false`          | Reconcile the gitignored Visual Studio files, Debug only |
| `LocalizationKitResourcesPath`            | `Strings`        | Folder holding the culture subfolders                    |
| `LocalizationKitResourceFileName`         | `Resources.resw` | Resource file name inside each culture folder            |

Validation is incremental: it runs when a `.resw` or the project changes, and the up-to-date stamp is
written only when the findings are clean, so a project with broken resources is checked again on the
next build rather than skipped.

## Typed keys

The same package generates a constant for every key in the default culture's resource file, so a key
can be a compile error instead of a missing string:

```csharp
localizer[LocalizationKeys.Settings.Theme.Header]
```

`Settings.Theme.Header` in the `.resw` is `Settings.Theme.Header` in C# — the generated name reads
like the key, so there is nothing to translate between the two. Rename or delete the key and the call
site stops compiling.

It reads the *default* culture only, deliberately: that file is the list of keys the project has, and
a translation is allowed to be behind. Resource names are sanitized on the way in — `Delete forever`
becomes `Delete_forever`, `404` becomes `_404`, `class` becomes `@class` — and a key that is also the
prefix of another lands on the class as `Settings.Theme.Value`, because a class cannot share its name
with a constant.

| Property                           | Default              | Effect                              |
| ---------------------------------- | -------------------- | ----------------------------------- |
| `LocalizationKitGenerateKeys`      | `true`               | Generate the class                  |
| `LocalizationKitKeysClass`         | `LocalizationKeys`   | Class name                          |
| `LocalizationKitKeysNamespace`     | `$(RootNamespace)`   | Namespace                           |
| `LocalizationKitKeysAccessibility` | `internal`           | `internal` or `public`              |
| `LocalizationKitDefaultCulture`    | `$(DefaultLanguage)` | Which culture's file holds the keys |

XAML cannot use constants, so `{loc:Text Key=Settings.Theme.Header}` stays a string; the validator is
what checks those.

## The editor

`tools/LocalizationKit.Editor/index.html` is a single page that edits `.resw` files with the plural
syntax in mind. It opens two cultures side by side, and for a message that is a plural it offers one
field per branch — with the categories *this* language actually has, taken from the browser's own
CLDR data, so Italian is asked for `one` and `other` while Polish is asked for `one`, `few`, `many`
and `other`. A table under the fields shows how the message reads at 0, 1, 2, 3, 5, 11, 21 and 101,
and which branch each count lands in.

It reports the same findings as the build, by the same codes, so the page and
`localizationkit validate` cannot disagree.

Saving preserves the file it read: the header, the indentation, the line endings and the byte order
mark are the ones it was opened with, and an entry it does not understand (an image, a byte array) is
kept as the bytes it was — so a one-word translation stays a one-line diff.

Where the page runs decides how files get in and out: inside a Visual Studio tool window the host
passes them through, on a page served over HTTPS it opens and saves them itself through the File
System Access API, and anywhere else files arrive by drag and drop and the result is copied out as
text.

[`tools/LocalizationKit.VsExtension`](https://github.com/LocalizationKit/LocalizationKit/blob/main/tools/LocalizationKit.VsExtension) is that tool window —
**Tools → LocalizationKit Resw Editor** — which finds the `Strings/<culture>/Resources.resw` files of
the open solution and saves back to them.

## The CLI

```
dotnet tool install -g LocalizationKit.Cli
```

`.resw` costs more bookkeeping than JSON: a culture is a file on disk, a `PRIResource` item in the
project's gitignored `.csproj.user`, and a column in Visual Studio's equally gitignored Resource
Explorer state. The CLI keeps all three in step.

```
localizationkit info                     show what was discovered
localizationkit add-culture it-IT        resw file + project property + both IDE files
localizationkit sync-ide --dry-run       reconcile the gitignored IDE files
localizationkit validate                 keys, ICU syntax, coverage, DefaultLanguage agreement
localizationkit validate --unused        also list keys the source never mentions
localizationkit export --culture it-IT --out it-IT.po      hand a culture to a translator
localizationkit import it-IT.po --culture it-IT --dry-run  take it back
```

Both file writers edit rather than regenerate, preserve the UTF-8 BOM (or its absence), CRLF endings
and the missing trailing newline that Visual Studio writes, and are idempotent: a second run is a
no-op. Stale culture columns are removed only with `--prune`.

The culture list comes from the `LocalizationKitCultures` MSBuild property when it is set, and from
the culture folders on disk when it is not.

## Pseudolocalization

```csharp
options.Pseudolocalize = true;     // a debug build, or behind a developer switch
```

`Save` then arrives as `⟦Șàṽé∙∙⟧`. Three faults that otherwise only appear once a second language is
in use appear immediately instead: text that no longer fits is clipped where anyone can see it, text
that is still plain English was never externalized, and a sentence built from two resources shows up
with brackets in the middle of it.

Arguments, plural categories and link markup are left alone, so a rewritten message still formats —
`{count, plural, one {# item} other {# items}}` keeps its keywords and rewrites only the branches,
which is where a translation's long words live.

## Handing a culture to a translator

PO and XLIFF are interchange formats, so neither is a resource provider: a translator receives one
and returns one. The format is inferred from the file name — `.po`, `.pot`, `.xlf`, `.xliff` — or
stated with `--format`.

```
localizationkit export --culture it-IT --out it-IT.po
localizationkit import it-IT.po --culture it-IT --dry-run
```

The key is the PO `msgctxt` and the XLIFF `trans-unit` id, the source text is the `msgid`/`source`,
and the translation is the `msgstr`/`target`. Untranslated strings are marked — `#, fuzzy` and
`state="needs-translation"` — because that is what a translator's tool filters its queue by.

**An ICU message crosses whole.** PO has its own plural model, with a `msgstr[n]` per form, and an
ICU message already carries every form in one string. Mapping between them means losing what PO
cannot hold: `=0` exact matches, nested arguments, a plural inside a branch. So the message travels
as the string it is and the translator sees the syntax. That cost is why the editor exists.

**An import changes only what it has to.** It goes through the same format-preserving document the
editor uses, so a handful of new translations is a handful of changed lines rather than a rewritten
file. It skips units naming keys the default culture no longer has, because those were renamed or
deleted while the translator had the file. Exporting a culture and importing it back unchanged is a
no-op.

## Trimming and native AOT

Every library claims `IsAotCompatible`, which turns the trim, single-file and AOT analyzers on, so
the claim is checked on each build rather than written down once. `samples/Console` is published with
`TrimMode=full` and again with `PublishAot=true` in CI, and run: it exits non-zero if ICU plurals,
culture scopes or the resource providers did not survive. The native build is a 17 MB executable
with no runtime installed.

One API cannot be made trim-safe and says so:

```csharp
localizer.Format("Welcome", new { name });                     // IL2026 in a trimmed application
localizer.Format("Welcome", new Dictionary<string, object?> { ["name"] = name });   // the safe path
```

Reading arguments off an anonymous type needs reflection, and a trimmer may remove the properties it
would read, so that overload is marked `[RequiresUnreferencedCode]`. Applications that do not trim
see nothing.

## Packages

| Package | What it adds |
| ------- | ------------ |
| `LocalizationKit` | Core engine, abstractions, DI, culture context, fallback, caching, formatting |
| `LocalizationKit.Resources.Resw` | `.resw` provider — loose files, embedded resources, overrides |
| `LocalizationKit.Resources.Json` | JSON provider, flat or nested keys |
| `LocalizationKit.Resources.Yaml` | YAML provider, flat or nested keys |
| `LocalizationKit.Messages.Icu` | ICU MessageFormat: CLDR plurals, ordinals, `select`, nesting |
| `LocalizationKit.WinUI` | `{loc:Text}` markup extension and `loc:Loc` attached properties |
| `LocalizationKit.Wpf` | `{loc:Text}` as a binding |
| `LocalizationKit.Maui` | `{loc:Text}` as a binding, through a converter |
| `LocalizationKit.AspNetCore` | Per-request culture negotiation middleware |
| `LocalizationKit.StringLocalizer` | `IStringLocalizer` compatibility |
| `LocalizationKit.Build` | MSBuild targets: validation on build, typed-key generation, IDE sync |
| `LocalizationKit.Tooling` | The validators and file writers, as a library |
| `LocalizationKit.Cli` | The `localizationkit` command, as a .NET tool |

Target frameworks are `net8.0` and `net10.0`, except `LocalizationKit.WinUI`
(`net8.0-windows10.0.19041.0`), `LocalizationKit.Wpf` (`net8.0-windows`, `net10.0-windows`) and
`LocalizationKit.Maui` (the MAUI target set).

## Contributing

```
dotnet build LocalizationKit.slnx
dotnet test LocalizationKit.slnx
```

Issues and pull requests are welcome, against `main`. A few things worth knowing before opening one:

- **Warnings are errors, and public members are documented.** The build is the first reviewer, and
  CI runs on Windows because the tooling asserts the exact bytes it writes (BOM, CRLF).
- **Behaviour claims come with a test.** The suite is the specification; where a comment and a test
  disagree, the test is right.
- **The Visual Studio extension builds separately.** It is a classic VSIX and needs Visual Studio's
  MSBuild rather than `dotnet build` — see
  [its README](https://github.com/LocalizationKit/LocalizationKit/blob/main/tools/LocalizationKit.VsExtension/README.md).

Repository layout:

```
src/          the twelve libraries
tools/        the CLI, the .resw editor, the Visual Studio extension
samples/      Console (also the trimming and AOT check) and Web (per-request culture)
tests/        367 tests, including CLDR conformance across 285 locales
```

## License

MIT. See [LICENSE](https://github.com/LocalizationKit/LocalizationKit/blob/main/LICENSE).
