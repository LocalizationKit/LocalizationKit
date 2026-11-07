# Resource formats

A provider decides where strings come from. One is required; which one is a registration line.

[!code-csharp[](../snippets/Features.cs#providers)]

## `.resw`

The default, and the reason the library exists in the shape it does.

```
Strings/
├── en-US/Resources.resw
└── it-IT/Resources.resw
```

Files are read as **loose XML at runtime**, not resolved through a compiled PRI. That is what makes
switching language a dictionary swap rather than a restart, and it is why the WinUI integration can
update a live window.

The cost is bookkeeping. In a WinUI project a culture is three things: a file on disk, a
`PRIResource` item in the gitignored `.csproj.user`, and a column in Visual Studio's equally
gitignored Resource Explorer state. [The CLI](cli.md) keeps all three in step.

One naming rule, enforced by the platform rather than by this library: **`MakePri` reads a dot as a
scope separator**, so `Common.Save` and `Common.Save.Hint` cannot both be keys — the build fails
with `PRI278`. [Validation](validation.md) reports it as `LK012` first, with a suggested rename.

Resources can also be embedded in the assembly, or overlaid: a file on disk overriding an embedded
default is how a shipped application takes a corrected translation without a new build.

## JSON

Flat or nested, both read into the same flat key space:

```json
{
  "Common": { "Save": "Save", "Cancel": "Cancel" },
  "Cart.Items": "{count, plural, one {# item} other {# items}}"
}
```

`Common.Save` and the nested form are the same key. Nesting is for the author's comfort; nothing
downstream can tell which was used.

## YAML

The same, for projects that prefer it. Multi-line scalars make long messages readable, which is the
main reason to choose it:

```yaml
Cart.Items: "{count, plural, one {# item} other {# items}}"
Login.Terms: >-
  Agree to the <a id="terms">Terms</a>
  and the <a id="privacy">Privacy Policy</a>.
```

## Writing a provider

Implement <xref:LocalizationKit.ILocalizationResourceProvider> and register it. A provider is asked
for a culture and returns its keys and values; everything else — fallback, caching, observation,
message formatting — is the core's job and happens the same way regardless of where the strings
came from.

That is the extension point for a database, a remote service, or a format not listed here.
