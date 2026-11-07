using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Controls;

namespace LocalizationKit.WinUI;

/// <summary>
/// Attached properties that set a translation and keep it current:
/// <c>&lt;TextBlock loc:Loc.Text="Common.Save" /&gt;</c>, and
/// <c>&lt;TextBlock loc:Loc.Inlines="Login.SignUpPrompt" /&gt;</c> for a sentence containing links.
/// </summary>
/// <remarks>
/// These do the same job as <see cref="TextExtension"/> and are the better choice where a markup
/// extension cannot be refreshed, such as inside a resource dictionary, or where the value is set
/// from a style. The attached property holds the key, so the live value is never in doubt.
/// </remarks>
public static class Loc
{
    /// <summary>Identifies the <c>Text</c> attached property.</summary>
    public static readonly DependencyProperty TextProperty = Register("Text");

    /// <summary>Identifies the <c>Content</c> attached property.</summary>
    public static readonly DependencyProperty ContentProperty = Register("Content");

    /// <summary>Identifies the <c>Header</c> attached property.</summary>
    public static readonly DependencyProperty HeaderProperty = Register("Header");

    /// <summary>Identifies the <c>Description</c> attached property.</summary>
    public static readonly DependencyProperty DescriptionProperty = Register("Description");

    /// <summary>Identifies the <c>PlaceholderText</c> attached property.</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = Register("PlaceholderText");

    /// <summary>
    /// Identifies the <c>Inlines</c> attached property, which fills a
    /// <see cref="TextBlock"/> from a translated sentence that may contain links and line breaks.
    /// </summary>
    public static readonly DependencyProperty InlinesProperty = DependencyProperty.RegisterAttached(
        "Inlines",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, OnInlinesChanged));

    /// <summary>
    /// Identifies the <c>Links</c> attached property: what each <c>&lt;a id="…"&gt;</c> in the
    /// sentence should do.
    /// </summary>
    public static readonly DependencyProperty LinksProperty = DependencyProperty.RegisterAttached(
        "Links",
        typeof(DependencyObjectCollection),
        typeof(Loc),
        new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <c>LinkCommand</c> attached property, the shorthand for a sentence with a
    /// single link.
    /// </summary>
    public static readonly DependencyProperty LinkCommandProperty = DependencyProperty.RegisterAttached(
        "LinkCommand",
        typeof(ICommand),
        typeof(Loc),
        new PropertyMetadata(null));

    /// <summary>Identifies the <c>LinkCommandParameter</c> attached property.</summary>
    public static readonly DependencyProperty LinkCommandParameterProperty = DependencyProperty.RegisterAttached(
        "LinkCommandParameter",
        typeof(object),
        typeof(Loc),
        new PropertyMetadata(null));

    /// <summary>Gets the key whose translation fills the element's <c>Text</c>.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The resource key, or <see langword="null"/>.</returns>
    public static string? GetText(DependencyObject element) => Get(element, TextProperty);

    /// <summary>Sets the key whose translation fills the element's <c>Text</c>.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="key">The resource key.</param>
    public static void SetText(DependencyObject element, string? key) => Set(element, TextProperty, key);

    /// <summary>Gets the key whose translation fills the element's <c>Content</c>.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The resource key, or <see langword="null"/>.</returns>
    public static string? GetContent(DependencyObject element) => Get(element, ContentProperty);

    /// <summary>Sets the key whose translation fills the element's <c>Content</c>.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="key">The resource key.</param>
    public static void SetContent(DependencyObject element, string? key) => Set(element, ContentProperty, key);

    /// <summary>Gets the key whose translation fills the element's <c>Header</c>.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The resource key, or <see langword="null"/>.</returns>
    public static string? GetHeader(DependencyObject element) => Get(element, HeaderProperty);

    /// <summary>Sets the key whose translation fills the element's <c>Header</c>.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="key">The resource key.</param>
    public static void SetHeader(DependencyObject element, string? key) => Set(element, HeaderProperty, key);

    /// <summary>Gets the key whose translation fills the element's <c>Description</c>.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The resource key, or <see langword="null"/>.</returns>
    public static string? GetDescription(DependencyObject element) => Get(element, DescriptionProperty);

    /// <summary>Sets the key whose translation fills the element's <c>Description</c>.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="key">The resource key.</param>
    public static void SetDescription(DependencyObject element, string? key) => Set(element, DescriptionProperty, key);

    /// <summary>Gets the key whose translation fills the element's <c>PlaceholderText</c>.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The resource key, or <see langword="null"/>.</returns>
    public static string? GetPlaceholderText(DependencyObject element) => Get(element, PlaceholderTextProperty);

    /// <summary>Sets the key whose translation fills the element's <c>PlaceholderText</c>.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="key">The resource key.</param>
    public static void SetPlaceholderText(DependencyObject element, string? key)
        => Set(element, PlaceholderTextProperty, key);

    /// <summary>Gets the key whose translated sentence fills the text block's inlines.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The resource key, or <see langword="null"/>.</returns>
    public static string? GetInlines(DependencyObject element) => Get(element, InlinesProperty);

    /// <summary>Sets the key whose translated sentence fills the text block's inlines.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="key">The resource key.</param>
    public static void SetInlines(DependencyObject element, string? key) => Set(element, InlinesProperty, key);

    /// <summary>
    /// Gets the link definitions for an element, creating the collection on first use so that XAML
    /// can fill it with child elements.
    /// </summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The collection of <see cref="Link"/> entries.</returns>
    public static DependencyObjectCollection GetLinks(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.GetValue(LinksProperty) is not DependencyObjectCollection links)
        {
            links = [];
            element.SetValue(LinksProperty, links);
        }

        return links;
    }

    /// <summary>Sets the link definitions for an element.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="links">The collection of <see cref="Link"/> entries.</param>
    public static void SetLinks(DependencyObject element, DependencyObjectCollection links)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(LinksProperty, links);
    }

    /// <summary>Gets the command run by the single link in the element's sentence.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The command, or <see langword="null"/>.</returns>
    public static ICommand? GetLinkCommand(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (ICommand?)element.GetValue(LinkCommandProperty);
    }

    /// <summary>Sets the command run by the single link in the element's sentence.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="command">The command to run.</param>
    public static void SetLinkCommand(DependencyObject element, ICommand? command)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(LinkCommandProperty, command);
    }

    /// <summary>Gets the parameter passed to <c>LinkCommand</c>.</summary>
    /// <param name="element">The element to read from.</param>
    /// <returns>The parameter, or <see langword="null"/>.</returns>
    public static object? GetLinkCommandParameter(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(LinkCommandParameterProperty);
    }

    /// <summary>Sets the parameter passed to <c>LinkCommand</c>.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="parameter">The parameter to pass.</param>
    public static void SetLinkCommandParameter(DependencyObject element, object? parameter)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(LinkCommandParameterProperty, parameter);
    }

    private static string? Get(DependencyObject element, DependencyProperty property)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(property);
    }

    private static void Set(DependencyObject element, DependencyProperty property, string? key)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(property, key);
    }

    private static DependencyProperty Register(string targetProperty) => DependencyProperty.RegisterAttached(
        targetProperty,
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (element, e) => OnKeyChanged(element, targetProperty, e)));

    private static void OnKeyChanged(DependencyObject element, string targetProperty, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string key || key.Length == 0)
        {
            return;
        }

        LocalizedTargetRegistry.Apply(element, targetProperty, LocalizationKitXaml.Resolve(key));
        LocalizationKitXaml.Track(element, targetProperty, key);
    }

    private static void OnInlinesChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBlock textBlock || e.NewValue is not string key || key.Length == 0)
        {
            return;
        }

        LocalizationKitXaml.Track(
            textBlock,
            key,
            static (target, value) => Build((TextBlock)target, value));

        // The links are declared as child elements, which XAML sets after this attribute. Building on
        // the dispatcher runs once the element has been parsed completely.
        DispatcherQueue? queue = textBlock.DispatcherQueue;
        if (queue is null || !queue.TryEnqueue(() => Build(textBlock, LocalizationKitXaml.Resolve(key))))
        {
            Build(textBlock, LocalizationKitXaml.Resolve(key));
        }
    }

    private static void Build(TextBlock textBlock, string value)
    {
        if (!LocalizedMarkup.TryParse(value, out IReadOnlyList<LocalizedMarkupSegment> segments, out _))
        {
            // Malformed markup is shown as it was written: visibly wrong beats invisibly missing.
            textBlock.Inlines.Clear();
            textBlock.Text = value;
            return;
        }

        int linkCount = segments.Count(segment => segment.Kind == LocalizedMarkupKind.Link);

        textBlock.Inlines.Clear();
        foreach (LocalizedMarkupSegment segment in segments)
        {
            textBlock.Inlines.Add(segment.Kind switch
            {
                LocalizedMarkupKind.LineBreak => new LineBreak(),
                LocalizedMarkupKind.Link => CreateLink(textBlock, segment, linkCount),
                _ => new Run { Text = segment.Text },
            });
        }
    }

    /// <summary>
    /// Turns a link in the sentence into a hyperlink, matching it to its action by id.
    /// </summary>
    /// <remarks>
    /// The single-link shorthand is only used when it cannot be ambiguous: either the link has no id,
    /// or it is the only link in the sentence. With several identified links and no matching entry, the
    /// link renders as plain text rather than quietly running the wrong command.
    /// </remarks>
    private static Inline CreateLink(TextBlock owner, LocalizedMarkupSegment segment, int linkCount)
    {
        Link? match = FindLink(owner, segment.Id);

        bool shorthandApplies = match is null && (segment.Id is null || linkCount == 1);
        ICommand? command = match?.Command ?? (shorthandApplies ? GetLinkCommand(owner) : null);
        object? parameter = match?.CommandParameter
            ?? (shorthandApplies ? GetLinkCommandParameter(owner) : null);

        Uri? uri = match?.NavigateUri ?? CreateUri(segment.Href);

        if (command is null && uri is null)
        {
            return new Run { Text = segment.Text };
        }

        Hyperlink hyperlink = new();
        hyperlink.Inlines.Add(new Run { Text = segment.Text });

        if (command is not null)
        {
            // A command takes precedence: a link should not both navigate and act.
            hyperlink.Click += (_, _) =>
            {
                if (command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                }
            };
        }
        else if (uri is not null)
        {
            hyperlink.NavigateUri = uri;
        }

        return hyperlink;
    }

    private static Link? FindLink(DependencyObject owner, string? id)
    {
        if (owner.GetValue(LinksProperty) is not DependencyObjectCollection links)
        {
            return null;
        }

        foreach (DependencyObject entry in links)
        {
            if (entry is Link link
                && link.Id is { Length: > 0 } linkId
                && string.Equals(linkId, id, StringComparison.OrdinalIgnoreCase))
            {
                return link;
            }
        }

        return null;
    }

    private static Uri? CreateUri(string? href)
        => href is { Length: > 0 } && Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) ? uri : null;
}
