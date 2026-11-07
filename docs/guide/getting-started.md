# Getting started

## Install

```
dotnet add package LocalizationKit.Core
dotnet add package LocalizationKit.Resources.Resw
dotnet add package LocalizationKit.Messages.Icu
```

The core package is the only required one. It is `LocalizationKit.Core` rather than
`LocalizationKit`, which belongs to an unrelated package on nuget.org; the assembly and the
namespaces are plain `LocalizationKit`. A resource provider decides where strings come from —
`.resw`, JSON or YAML — and `LocalizationKit.Messages.Icu` adds plurals, ordinals and `select`.
Add your UI framework's package as well: `LocalizationKit.WinUI`, `LocalizationKit.Wpf`,
`LocalizationKit.Maui` or `LocalizationKit.AspNetCore`.

Requires .NET 8 or .NET 10.

## Lay out the resources

```
Strings/
├── en-US/Resources.resw
└── it-IT/Resources.resw
```

One folder per culture, named as the culture is named. Values are ICU message patterns, so `{name}`
is substituted and an apostrophe quotes — `L'articolo` stays as written, and `'{'` escapes a brace.

## Register the services

[!code-csharp[](../snippets/GettingStarted.cs#registration)]

`DefaultCulture` is the language the resources are authored in and the one a missing translation
falls back to. `SupportedCultures` is what the application ships.
<xref:LocalizationKit.FallbackBehavior> decides what happens when a key resolves nowhere —
`ReturnKey` shows the key itself, which is visible in testing rather than silently empty.

## Load the resources once

[!code-csharp[](../snippets/GettingStarted.cs#initialize)]

This is deliberate: resolving a key before the resources are loaded throws
`InvalidOperationException`, naming the call that was missed, rather than returning key names for
the first second of the application's life — the kind of fault that reaches a screenshot.

ASP.NET Core applications do not need this call — the request culture middleware does it when the
first request arrives. See [ASP.NET Core](aspnet-core.md).

## Resolve strings

[!code-csharp[](../snippets/GettingStarted.cs#resolving)]

<xref:LocalizationKit.ILocalizer.Get(System.String)> returns a string as written.
<xref:LocalizationKit.ILocalizer.Format(System.String,System.Object)> substitutes arguments.
<xref:LocalizationKit.ILocalizer.Plural(System.String,System.Decimal)> picks the plural branch the
language calls for — see [Pluralization](pluralization.md).

To ask whether a key exists at all, without the fallback standing in for it:

[!code-csharp[](../snippets/GettingStarted.cs#try-get)]

## Switch language at runtime

[!code-csharp[](../snippets/GettingStarted.cs#switching)]

Strings obtained through <xref:LocalizationKit.ILocalizer.Observe(System.String)> re-read
themselves, and the UI integrations update every bound value. Nothing is cached across the switch
that should not be, and nothing needs restarting.

## One thing to know before you trim

[!code-csharp[](../snippets/GettingStarted.cs#trim-safe-arguments)]

Reading argument values off an anonymous type needs reflection, and a trimmer may remove the
properties it would read. That overload is marked `[RequiresUnreferencedCode]`, so a trimmed
application gets IL2026 pointing at the dictionary overload. Applications that do not trim see
nothing. [Trimming and native AOT](trimming.md) has the detail.

## Next

- [Core concepts](core-concepts.md) — how resolution, fallback and culture state actually work.
- [Pluralization](pluralization.md) — ICU messages and CLDR categories.
- [Validation on build](validation.md) — catching a broken translation at compile time.
