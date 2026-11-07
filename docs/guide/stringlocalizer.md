# Existing code that uses IStringLocalizer

[!code-csharp[](../snippets/Features.cs#stringlocalizer-registration)]

`IStringLocalizer`, `IStringLocalizer<T>` and `IStringLocalizerFactory` then resolve through
LocalizationKit, so Razor views, controllers, validation attributes and any library that takes one
keep their call sites.

[!code-csharp[](../snippets/Features.cs#stringlocalizer-use)]

ICU plurals work through the indexer, and `localizer["Welcome", name]` works whether the resource
uses `{0}` or `{name}`.

`ResourceNotFound` is accurate, which matters if anything branches on it: the bridge asks
<xref:LocalizationKit.ILocalizer.TryGet(System.String,System.String@)> rather than taking the
configured fallback, so a key that would only have "resolved" through `ReturnKey` still reports as
not found.

## What the type argument means

`IStringLocalizer<CheckoutPage>` asked for `Save` looks for `CheckoutPage.Save`. There is still one
flat key space — this is a [key prefix](core-concepts.md#key-prefixes), not a separate resource
file — and a key the prefix does not have falls through to the bare key, so `Common.Cancel` still
resolves and so does every flat key that resolved before.

It is the nearest thing in a flat key space to the per-type resource file Microsoft's own
implementation would have used, without giving up the single namespace that makes
[validation](validation.md) and [typed keys](typed-keys.md) possible.

`localizer.Scoped("Checkout")` does the same thing without the generic.
