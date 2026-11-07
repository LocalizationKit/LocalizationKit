# WinUI 3

```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    await Services.InitializeLocalizationKitAsync();
    LocalizationKitXaml.Initialize(Services);

    Window = new MainWindow();
    Window.Activate();
}
```

```xml
<Page xmlns:loc="using:LocalizationKit.WinUI">
    <TextBlock Text="{loc:Text Key=Settings.Title}" />
    <Button Content="{loc:Text Key=Common.Save}"
            ToolTipService.ToolTip="{loc:Text Key=Common.Save.Hint}" />
    <TextBlock loc:Loc.Text="Common.Save" />
</Page>
```

Both forms update when the culture changes. `{loc:Text}` works on any property; `loc:Loc.Text` is
an attached property, shorter where it applies.

## Sentences containing links

A sentence with a link in it must stay **one** resource. Split into three strings, a translator
receives three disconnected fragments and no way to move the link — which any language with a
different word order needs to do.

So the link lives inside the string:

```
Login.SignUpPrompt    Don't have an account? <a>Sign up</a>.
Login.ForgotPassword  <a href="https://example.com/forgot/">Forgot your password?</a>
Login.Terms           Agree to the <a id="terms">Terms</a> and the <a id="privacy">Privacy Policy</a>.
```

```xml
<!-- one link: the shorthand -->
<TextBlock loc:Loc.Inlines="Login.SignUpPrompt"
           loc:Loc.LinkCommand="{x:Bind ViewModel.StartSignUpCommand}" />

<!-- the address is in the translation, so nothing to wire -->
<TextBlock loc:Loc.Inlines="Login.ForgotPassword" />

<!-- several links, each matched by id -->
<TextBlock loc:Loc.Inlines="Login.Terms">
    <loc:Loc.Links>
        <loc:Link Id="terms" Command="{x:Bind ViewModel.ShowTermsCommand}" />
        <loc:Link Id="privacy" NavigateUri="https://example.com/privacy" />
    </loc:Loc.Links>
</TextBlock>
```

Links are matched by `id`, never by position, so a translator may reorder them freely.

| | |
| --- | --- |
| `\n` | becomes a line break |
| `<a>`, `</a>` | the only markup; any other angle bracket is literal, so `a < b` reads as written |
| A link with no command and no address | renders as plain text, not a dead hyperlink |
| Malformed markup | shown as written rather than swallowed |

Two rules protect this at build time: `LK013` rejects malformed markup, and `LK014` fails when a
translation's link ids do not match the default culture's — a renamed id leaves a link that does
nothing, in one language only, which is close to undiscoverable by hand.

## Two things to know

**`Key=` is required.** Windows Runtime XAML has no positional arguments in a custom markup
extension.

**A markup extension is evaluated once**, when the page loads, and cannot return a binding. So the
integration records — weakly — the element and property each value went to, and sets them again on
the UI dispatcher when the culture changes. Where the XAML runtime reports no target, such as
inside a resource dictionary, the value is right at load time but will not refresh. The `loc:Loc`
attached properties always refresh, and are the way out of that corner.

## The strings that are not yours

A WinUI window is full of text this engine never sees: a text box's context menu, the navigation
pane's toggle tooltip, a `ContentDialog`'s default buttons, whatever the Community Toolkit's
controls say for themselves. Those come from MRT, out of compiled resources, and MRT reads
**Windows' own language setting** — not the culture LocalizationKit selected. Switching language
leaves them behind, in a way that looks like a bug in the switch.

One line, next to wherever the culture is applied:

```csharp
// Windows.Globalization
ApplicationLanguages.PrimaryLanguageOverride = "it-IT";   // "" to follow Windows
```

A control keeps the strings it loaded, so this reaches framework text in anything created
afterwards, and all of it on the next launch — Windows persists the override, so that launch
already has it.

## Naming keys

`MakePri` reads a dot as a scope separator, so `Common.Save` and `Common.Save.Hint` cannot both be
keys — the build fails with `PRI278`. [Validation](validation.md) reports it as `LK012` first,
with a suggested rename.
