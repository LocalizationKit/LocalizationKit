# Core concepts

## The pieces

| Type | What it is |
| ---- | ---------- |
| <xref:LocalizationKit.ILocalizer> | Resolves keys to strings. The one most code touches. |
| <xref:LocalizationKit.ILocalizationContext> | The selected culture, and changing it. |
| <xref:LocalizationKit.ILocalizationFormatter> | Numbers, dates, currency, in the selected culture. |
| <xref:LocalizationKit.ILocalizationResourceProvider> | Where strings come from. `.resw`, JSON, YAML, or yours. |
| <xref:LocalizationKit.IMessageFormatter> | How a message pattern becomes a string. ICU, or the default. |

All five are registered by `AddLocalizationKit` plus a provider package, and all five are
interfaces you can replace.

## How a key is resolved

1. The **selected culture** is asked for the key. Inside a
   [culture scope](#serving-two-languages-at-once) that is the scope's culture, otherwise it is
   whatever `SetCultureAsync` last set.
2. If the culture is specific (`it-IT`) and has no entry, its **neutral parent** (`it`) is asked.
3. Then the **default culture** — `DefaultCulture`, the language the resources are authored in.
4. If nothing has it, <xref:LocalizationKit.FallbackBehavior> decides what happens.

Only step 4 is configurable, and it is worth configuring deliberately:

[!code-csharp[](../snippets/CoreConcepts.cs#fallback)]

`ReturnKey` is the default because a key on screen is visible in testing, where an empty label is
not. `Throw` is the strict option for a project that would rather not ship with a gap.

To find out about gaps without failing, there is a hook:

[!code-csharp[](../snippets/CoreConcepts.cs#missing-key)]

For the build-time version of the same question — which is where it belongs — see
[Validation on build](validation.md).

## Initialization is explicit

Resources are loaded once, during startup, by awaiting
`IServiceProvider.InitializeLocalizationKitAsync()` or
<xref:LocalizationKit.ILocalizationContext.InitializeAsync(System.Threading.CancellationToken)>.

Resolving before that throws `InvalidOperationException` naming the call that was missed. The
alternative — resolving lazily on first use — would mean either a synchronous file read on the UI
thread or key names appearing for the first frames of the application. Neither is better than an
exception during development.

ASP.NET Core is the exception: the request culture middleware initializes on the first request, so
there is no startup call to remember. See [ASP.NET Core](aspnet-core.md).

## Strings that update themselves

<xref:LocalizationKit.ILocalizer.Observe(System.String)> returns an
<xref:LocalizationKit.ILocalizedString>: a key, a current value, and `INotifyPropertyChanged`.

[!code-csharp[](../snippets/CoreConcepts.cs#observing)]

This is what the XAML integrations are built on, and what to use in a view model property. A plain
`Get` is correct for a string that is used once and thrown away — a log line, an exception message,
the subject of an email.

## Key prefixes

One flat key space, with a way to work inside part of it:

[!code-csharp[](../snippets/CoreConcepts.cs#scoped)]

A key the prefix does not have falls through to the bare key, so shared keys like `Common.Cancel`
keep working inside a scoped localizer. `IStringLocalizer<T>` uses the same mechanism with the type
name as the prefix — see [IStringLocalizer](stringlocalizer.md).

## Serving two languages at once

The selected culture is process-wide, which cannot describe two users being served in two languages
at the same time. A scope overrides it for one asynchronous flow:

[!code-csharp[](../snippets/CoreConcepts.cs#culture-scope)]

Scopes nest and restore on dispose. They do **not** raise `CultureChanged`, which stays an
application-wide signal for observable strings — a request being answered in Italian should not
make a desktop window redraw itself.

They are deliberately synchronous, and that has a consequence worth knowing: an `AsyncLocal`
written inside an `async` method does not flow back to the caller, so a scope cannot load resources
as it opens. **The cultures must already be loaded.** Set `PreloadSupportedCultures = true` at
registration, or await
<xref:LocalizationKit.ILocalizationContext.PreloadAsync(System.Threading.CancellationToken)>
during startup.

## Accepting a culture name from outside

[!code-csharp[](../snippets/CoreConcepts.cs#negotiate)]

<xref:LocalizationKit.ILocalizationContext.NegotiateCulture(System.String)> maps a requested name
onto something the application ships: `it` finds `it-IT`, and an unknown or malformed name returns
`null` rather than throwing. Culture names usually arrive from a query string or an HTTP header,
which are attacker-controlled, so a bad one is an ordinary event rather than an error.

## Formatting

[!code-csharp[](../snippets/CoreConcepts.cs#formatting)]

All of it follows the selected culture, inside a culture scope as well, so a formatted number and
the sentence around it can never disagree about the language. `In(...)` overrides the culture for
one call — an invoice that must be in the customer's language regardless of the UI.

## What comes next

- [Pluralization](pluralization.md) — ICU messages and CLDR categories.
- [Resource formats](resources.md) — `.resw`, JSON, YAML, and writing a provider.
- [Validation on build](validation.md) — catching a bad translation before it ships.
