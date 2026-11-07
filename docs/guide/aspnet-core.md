# ASP.NET Core

```csharp
builder.Services.AddLocalizationKit(options => { /* ... */ });
builder.Services.AddReswLocalization();          // or AddJsonLocalization()
builder.Services.AddLocalizationKitRequestCulture();

app.UseRouting();
app.UseLocalizationKitRequestCulture();
```

Each request is answered in the language it asked for, and concurrent requests in different
languages do not disturb each other — the middleware opens a
[culture scope](core-concepts.md#serving-two-languages-at-once) per request rather than setting a
process-wide culture.

## How the language is chosen

Query string, then route value, then cookie, then `Accept-Language`. What the user just asked for
beats what they asked for earlier, which beats what their browser guessed. `Sources` is an ordered
list you can reorder, trim or add to.

A language the application does not ship is an ordinary event rather than an error — a header is
attacker-controlled — so an unknown or malformed name leaves the selected culture in place, and a
request for `it` resolves to the shipped `it-IT`.

A response whose language came from `Accept-Language` gets `Vary: Accept-Language`, so a shared
cache cannot serve it to the next reader of another language. That header is easy to forget when
rolling your own, and its absence is the kind of bug that only appears behind a CDN.

## Remembering a choice

```csharp
app.MapPost("/language/{culture}", (HttpContext http, string culture) =>
{
    new CookieCultureSource().Append(http, culture);
    return Results.LocalRedirect("/");
});
```

## No startup call

The middleware initializes the kit and loads every supported culture when the first request
arrives, so there is no `InitializeLocalizationKitAsync` to remember and no ordering to get right.

This matters more than it looks. A culture scope is synchronous by necessity and so cannot load
resources as it opens — something has to guarantee they are already there, and this is that
something.
