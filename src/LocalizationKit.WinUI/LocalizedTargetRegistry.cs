using System.Reflection;
using Microsoft.UI.Xaml;

namespace LocalizationKit.WinUI;

/// <summary>
/// The list of element properties that were set from a resource key, so a culture change can set
/// them again.
/// </summary>
/// <remarks>
/// A WinUI markup extension produces a value once, when the page is loaded; it has no way to update
/// itself afterwards. Recording the target is what turns <c>{loc:Text}</c> into something that
/// survives a language switch. Entries hold the element weakly and are swept as they die, so a page
/// that has been navigated away from is collected normally.
/// </remarks>
internal static class LocalizedTargetRegistry
{
    private static readonly List<Entry> Entries = [];
    private static readonly object Gate = new();

    public static void Track(object target, string propertyName, string key, Type? declaringType)
    {
        if (string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(key))
        {
            return;
        }

        Add(new Entry(new WeakReference<object>(target), propertyName, key, declaringType, null));
    }

    /// <summary>
    /// Tracks a target whose value is not a single property, such as a text block whose inline
    /// collection is rebuilt from the translated string.
    /// </summary>
    public static void Track(object target, string key, Action<object, string> apply)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        Add(new Entry(new WeakReference<object>(target), null, key, null, apply));
    }

    private static void Add(Entry entry)
    {
        lock (Gate)
        {
            // Re-registering the same target and key replaces the old entry rather than stacking.
            Entries.RemoveAll(existing =>
                existing.Key == entry.Key
                && existing.PropertyName == entry.PropertyName
                && (!existing.Target.TryGetTarget(out object? previous)
                    || (entry.Target.TryGetTarget(out object? current) && ReferenceEquals(previous, current))));

            Entries.Add(entry);
        }
    }

    public static void ReapplyAll(Func<string, string> resolve)
    {
        Entry[] snapshot;
        lock (Gate)
        {
            Entries.RemoveAll(entry => !entry.Target.TryGetTarget(out _));
            snapshot = [.. Entries];
        }

        foreach (Entry entry in snapshot)
        {
            if (!entry.Target.TryGetTarget(out object? target))
            {
                continue;
            }

            if (entry.Apply is not null)
            {
                entry.Apply(target, resolve(entry.Key));
            }
            else if (entry.PropertyName is not null)
            {
                Apply(target, entry.PropertyName, resolve(entry.Key), entry.DeclaringType);
            }
        }
    }

    /// <summary>
    /// Sets a property by name, preferring the dependency property so that bindings, styles and
    /// animations on the same property keep behaving as they should.
    /// </summary>
    /// <param name="target">The element to set.</param>
    /// <param name="propertyName">The property name, such as <c>Text</c>.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="declaringType">
    /// The type that declares the property, when XAML reported it. This is what makes an attached
    /// property such as <c>ToolTipService.ToolTip</c> work: the dependency property lives on
    /// <c>ToolTipService</c>, not on the element.
    /// </param>
    public static void Apply(object target, string propertyName, string value, Type? declaringType = null)
    {
        if (target is DependencyObject element)
        {
            if (TryFindDependencyProperty(declaringType, propertyName, out DependencyProperty? declared))
            {
                element.SetValue(declared, value);
                return;
            }

            if (TryFindDependencyProperty(target.GetType(), propertyName, out DependencyProperty? own))
            {
                element.SetValue(own, value);
                return;
            }
        }

        PropertyInfo? clrProperty = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (clrProperty is not null && clrProperty.CanWrite)
        {
            clrProperty.SetValue(target, value);
        }
    }

    private static bool TryFindDependencyProperty(Type? owner, string propertyName, out DependencyProperty? property)
    {
        property = null;
        if (owner is null)
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        string name = propertyName + "Property";

        property = owner.GetProperty(name, flags)?.GetValue(null) as DependencyProperty
            ?? owner.GetField(name, flags)?.GetValue(null) as DependencyProperty;

        return property is not null;
    }

    private sealed record Entry(
        WeakReference<object> Target,
        string? PropertyName,
        string Key,
        Type? DeclaringType,
        Action<object, string>? Apply);
}
