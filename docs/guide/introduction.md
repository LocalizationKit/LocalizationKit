# Introduction

LocalizationKit is a localization library for .NET: one API for resolving strings, one for
formatting numbers and dates, and one notion of "the current culture" that every UI framework
integration reads from.

## What it is for

Most .NET localization is framework-shaped. WinUI resolves strings through compiled PRI resources,
WPF through satellite assemblies and `ResourceManager`, ASP.NET Core through `IStringLocalizer` and
a request culture pipeline. Each works, and none of them is the same as the others — so a solution
with a desktop app and a web front end localizes twice, in two idioms, against two sets of files.

This library is the other arrangement: the resources, the resolution and the culture state sit in a
UI-agnostic core, and each framework gets a thin integration on top. Switching the WinUI app from
`.resw` to JSON is a registration change. Adding a web front end reuses the resource files.

## What it does

- **Reads resources from `.resw`, JSON or YAML**, or anything else you write a provider for.
- **Switches language at runtime**, without restarting and without reloading views.
- **Formats messages with ICU MessageFormat** — CLDR plurals, ordinals, `select`, nesting.
- **Formats numbers, dates and currency** in whatever culture is selected.
- **Serves several cultures at once**, for a server answering concurrent requests in different
  languages.
- **Validates the resources at build time**, so a broken translation is a compile error.
- **Generates typed keys**, so a renamed key breaks the build rather than the screen.
- **Pseudolocalizes**, so layout and un-externalized text break before a translator is paid.

## Design philosophy

**The resource is the unit of translation, not the sentence fragment.** A sentence containing a
link stays one string, with the link inside it, because a translator who receives three fragments
cannot reorder them — and some languages must. See [the WinUI guide](winui.md).

**A translator's mistake should fail a build, not a screen.** Most of the tooling exists for this.
A plural whose categories are wrong for the language, a translation that dropped `{count}`, a key
that `MakePri` cannot survive: each is a diagnostic with a file and a line.
See [Validation on build](validation.md).

**Runtime language switching is a dictionary swap.** `.resw` files are read as loose XML rather
than resolved through a compiled PRI, which is what makes the switch cheap and total. The cost is
that `.resw` brings bookkeeping — project files, IDE state — which [the CLI](cli.md) absorbs.

**Nothing in the core knows about a UI framework.** `LocalizationKit` depends on
`Microsoft.Extensions.DependencyInjection.Abstractions` and nothing else.

## Supported platforms

| | |
| --- | --- |
| Frameworks | .NET 8, .NET 10 |
| UI integrations | WinUI 3, WPF, MAUI, ASP.NET Core |
| Resource formats | `.resw`, JSON, YAML, or a custom provider |
| Trimming | Supported and checked in CI, including native AOT |

The WinUI, WPF and MAUI packages target the platform frameworks they must; everything else is
cross-platform.

## Where to go next

- [Getting started](getting-started.md) — install, register, resolve a string.
- [Core concepts](core-concepts.md) — resolution, fallback, culture state, scopes.
- [Pluralization](pluralization.md) — the part most libraries get approximately right.
