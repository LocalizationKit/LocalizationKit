# WPF

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

The key may be positional here, unlike [WinUI](winui.md), and `StringFormat` and the rest of
`Binding`'s surface work — because `{loc:Text}` *is* a binding.

## Why this needs less machinery than WinUI

A WPF markup extension may return a binding rather than a string. `{loc:Text}` becomes a binding to
an indexer on one observable object, so a language change raises a single `PropertyChanged` for
that indexer and the framework re-reads every bound value — because the framework already keeps the
list of them.

WinUI's markup extension has no binding to return, which is why it has to record each target it
gave a value to and write to them again later. Same result; this one is less code, and it has no
corner case around resource dictionaries.

The change notification is marshalled to the dispatcher that owns the elements, since the culture
can be switched from anywhere. The tests run on an STA thread with a real dispatcher for that
reason: a `TextBlock` whose `Text` is asserted to change when the culture does proves the claim,
where asserting on the string the extension returned would not.

## In the designer

There is no application and so no localizer, so `{loc:Text}` shows the key — or `DesignTimeValue`
when the key is long and the string is not.
