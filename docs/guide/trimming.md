# Trimming and native AOT

Every library claims `IsAotCompatible`, which turns the trim, single-file and AOT analyzers on, so
the claim is checked on each build rather than written down once.

`samples/Console` is published with `TrimMode=full` and again with `PublishAot=true` in CI, and
**run**: it exits non-zero if ICU plurals, culture scopes or the resource providers did not survive
the trimmer. A compatibility claim that is only asserted by an attribute is worth very little; this
one fails a build when it stops being true.

The native build is a 17 MB executable with no runtime installed.

## The one API that cannot be made trim-safe

[!code-csharp[](../snippets/GettingStarted.cs#trim-safe-arguments)]

Reading argument values off an anonymous type needs reflection, and a trimmer may remove the
properties it would read — leaving a message formatted with nothing where `{name}` should be.

So <xref:LocalizationKit.ILocalizer.Format(System.String,System.Object)> is marked
`[RequiresUnreferencedCode]`. A trimmed application gets IL2026 pointing at the dictionary
overload, which is reflection-free. An application that does not trim sees nothing and can keep
using whichever reads better.

## What this means in practice

| You are | What to do |
| --- | --- |
| Not trimming | Nothing. Use either overload. |
| Trimming or publishing AOT | Use the dictionary overload where you pass arguments. The analyzer will tell you where. |

Everything else — resource providers, the ICU engine, culture scopes, the formatter, the XAML
integrations — works trimmed without special handling.
