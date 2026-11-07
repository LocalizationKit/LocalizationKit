# Typed keys

`LocalizationKit.Build` generates a constant for every key in the default culture's resource file,
so a key can be a compile error instead of a missing string:

```csharp
localizer[LocalizationKeys.Settings.Theme.Header]
```

`Settings.Theme.Header` in the `.resw` is `Settings.Theme.Header` in C#. The generated name reads
like the key, so there is nothing to translate between the two, and renaming or deleting the key
stops the call site compiling.

## Why the default culture only

That file is the list of keys the project has. A translation is allowed to be behind, and a key
that exists *only* in a translation is a finding for [the validator](validation.md), not a constant
to offer.

## Names that are not identifiers

Resource names are written by whoever writes the resources, so they are sanitized on the way in:

| Key | Constant |
| --- | --- |
| `Delete forever` | `Delete_forever` |
| `404` | `_404` |
| `class` | `@class` |
| `Settings.Theme`, next to `Settings.Theme.System` | `Settings.Theme.Value` |

That last one exists because a class cannot share its name with a constant: when a key is also the
prefix of another key, the shorter one moves to `.Value`. Two names that would collide get `2`
appended rather than failing the build.

## Configuration

| Property | Default | Effect |
| --- | --- | --- |
| `LocalizationKitGenerateKeys` | `true` | Generate the class |
| `LocalizationKitKeysClass` | `LocalizationKeys` | Class name |
| `LocalizationKitKeysNamespace` | `$(RootNamespace)` | Namespace |
| `LocalizationKitKeysAccessibility` | `internal` | `internal` or `public` |
| `LocalizationKitDefaultCulture` | `$(DefaultLanguage)` | Which culture's file holds the keys |

## XAML

XAML cannot use constants, so `{loc:Text Key=Settings.Theme.Header}` stays a string.
[Validation](validation.md) is what checks those.
