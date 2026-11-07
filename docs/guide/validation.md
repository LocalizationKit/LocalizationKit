# Validation on build

```xml
<PackageReference Include="LocalizationKit.Build" Version="..." PrivateAssets="all" />
```

That is the whole setup. Every build then checks the resource files and reports what it finds as
build diagnostics, with a code and a line:

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

The package carries its own copy of the CLI, so nothing has to be installed first, and validation
is skipped in design-time builds so the IDE stays responsive.

## The rule that matters most

`LK015` checks that a translation asks for **the same arguments** as the message it translates.

Every other rule checks that a message is well formed. This one checks that it is the *same*
message. A translator who drops `{count}`, or writes `{conteggio}` because the sentence around it
is Italian, leaves valid ICU that the engine then has no value for — and it fails in one language,
on whichever screen happens to show it, which is close to undiscoverable by hand.

## Configuration

| Property | Default | Effect |
| --- | --- | --- |
| `LocalizationKitValidate` | `true` | Validate on build |
| `LocalizationKitValidateWarningsAsErrors` | `false` | Make missing translations fail; worth turning on in CI |
| `LocalizationKitSyncIde` | `false` | Reconcile the gitignored Visual Studio files, Debug only |
| `LocalizationKitResourcesPath` | `Strings` | Folder holding the culture subfolders |
| `LocalizationKitResourceFileName` | `Resources.resw` | Resource file name inside each culture folder |

Validation is incremental: it runs when a `.resw` or the project changes, and the up-to-date stamp
is written only when the findings are clean — so a project with broken resources is checked again
next build rather than skipped.

## Consuming the kit from source

Import the two files and point them at the CLI you built:

```xml
<Import Project="..\LocalizationKit\src\LocalizationKit.Build\build\LocalizationKit.Build.props" />
<!-- ... -->
<PropertyGroup>
  <LocalizationKitCliAssembly>..\LocalizationKit\tools\LocalizationKit.Cli\bin\$(Configuration)\net8.0\LocalizationKit.Cli.dll</LocalizationKitCliAssembly>
</PropertyGroup>
```

## The same checks elsewhere

The CLI runs them (`localizationkit validate`) and so does [the editor](editor.md), by the same
codes, so the three cannot disagree. The syntax check itself is
<xref:LocalizationKit.IcuMessageSyntax> in the core, available at runtime too.
