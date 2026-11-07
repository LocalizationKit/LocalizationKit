using LocalizationKit;

// Per-request culture on ASP.NET Core. Try it with:
//
//   curl localhost:5000/                               -> the default language
//   curl localhost:5000/?culture=it-IT                 -> Italian
//   curl -H "Accept-Language: it" localhost:5000/      -> Italian, negotiated from the browser
//   curl localhost:5000/cart/1  and  /cart/4           -> ICU plurals
//
// Nothing here is per-request state: ILocalizer is a singleton, and the middleware scopes the culture
// to each request's own asynchronous flow.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddIcuMessages();
builder.Services.AddLocalizationKit(options =>
{
    options.DefaultCulture = "en-US";
    options.SupportedCultures = ["en-US", "it-IT"];
    options.InitialCulture = "en-US";
});

builder.Services.AddJsonLocalization(options => options.RootDirectory = AppContext.BaseDirectory);
builder.Services.AddLocalizationKitRequestCulture();

WebApplication app = builder.Build();

// After UseRouting, so a culture in the route is visible; before anything localized.
app.UseRouting();
app.UseLocalizationKitRequestCulture();

app.MapGet("/", (ILocalizer localizer, ILocalizationContext context) => Results.Json(new
{
    culture = context.UICulture.Name,
    greeting = localizer.Format("Welcome", new Dictionary<string, object?> { ["name"] = "Francesco" }),
}));

app.MapGet("/cart/{count:int}", (int count, ILocalizer localizer) => Results.Json(new
{
    items = localizer.Format("Cart.Items", new Dictionary<string, object?> { ["count"] = count }),
}));

// A language switch: remember the choice in a cookie, which the next request reads back.
app.MapPost("/language/{culture}", (HttpContext http, string culture, ILocalizationContext context) =>
{
    if (context.NegotiateCulture(culture) is null)
    {
        return Results.BadRequest($"'{culture}' is not one of {string.Join(", ", context.SupportedCultures.Select(c => c.Name))}.");
    }

    new CookieCultureSource().Append(http, culture);
    return Results.NoContent();
});

app.Run();
