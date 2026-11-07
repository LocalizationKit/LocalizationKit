using System.Windows.Input;
using Microsoft.UI.Xaml;

namespace LocalizationKit.WinUI;

/// <summary>
/// What a link inside a translated sentence should do, matched to the sentence by
/// <see cref="Id"/>.
/// </summary>
/// <remarks>
/// Matching on an id rather than on position is the point: a translator is free to move the links
/// around, which languages with a different word order require, and each link still does what it
/// was meant to do.
/// </remarks>
public sealed partial class Link : DependencyObject
{
    /// <summary>Identifies the <see cref="Id"/> dependency property.</summary>
    public static readonly DependencyProperty IdProperty = DependencyProperty.Register(
        nameof(Id), typeof(string), typeof(Link), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(Link), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(Link), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="NavigateUri"/> dependency property.</summary>
    public static readonly DependencyProperty NavigateUriProperty = DependencyProperty.Register(
        nameof(NavigateUri), typeof(Uri), typeof(Link), new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the id this entry answers to, matching an <c>&lt;a id="…"&gt;</c> in the
    /// translated string.
    /// </summary>
    public string? Id
    {
        get => (string?)GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>Gets or sets the command to run when the link is clicked.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// Gets or sets the address the link opens. A link whose address is already in the translated
    /// string as <c>href</c> needs no entry here at all.
    /// </summary>
    public Uri? NavigateUri
    {
        get => (Uri?)GetValue(NavigateUriProperty);
        set => SetValue(NavigateUriProperty, value);
    }
}
