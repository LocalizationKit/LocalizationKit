# MAUI

```xml
<ContentPage xmlns:loc="clr-namespace:LocalizationKit.Maui;assembly=LocalizationKit.Maui">
    <Button Text="{loc:Text Key=Common.Save}" />
</ContentPage>
```

```csharp
await Services.InitializeLocalizationKitAsync();
LocalizationKitXaml.Initialize(Services);     // before the first page
```

The same idea as [WPF](wpf.md) — a markup extension returning a binding — with two differences that
MAUI forces. Both are worth knowing if you ever write a binding against a key yourself, because
both fail silently rather than with an error.

**Its path parser splits on dots before it reads an indexer.** `[Common.Save]` is rejected for a
missing bracket, and every key here has dots in it. So the key goes to a converter instead of into
the binding path.

**A binding whose path is `.` is never subscribed to its source's notifications.** It would read
the right value once and never change again. The binding therefore watches a property that returns
the source, and a culture change raises a notification for that.
