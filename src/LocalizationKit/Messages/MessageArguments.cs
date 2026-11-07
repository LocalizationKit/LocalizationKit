using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace LocalizationKit;

/// <summary>Normalizes the loosely typed <c>arguments</c> parameter into a name/value map.</summary>
internal static class MessageArguments
{
    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Accepts <see langword="null"/>, a dictionary, or any object whose public properties are the
    /// arguments, including an anonymous type.
    /// </summary>
    /// <param name="arguments">The caller's argument object.</param>
    /// <returns>The arguments keyed by name, compared case-insensitively.</returns>
    [UnconditionalSuppressMessage(
        "AOT",
        "IL2075:DoesNotHaveMatchingAnnotations",
        Justification = "Reflecting over an anonymous type is the convenience path. Callers in trimmed or "
            + "native-AOT applications pass a dictionary to ILocalizer.Format instead, which never reflects.")]
    [RequiresUnreferencedCode(
        "Message arguments are read from the object's public properties by reflection, and trimming can remove the properties of the type passed in. Use the overload that takes an IReadOnlyDictionary<string, object?>.")]
    public static IReadOnlyDictionary<string, object?> From(object? arguments)
    {
        switch (arguments)
        {
            case null:
                return Empty;

            // Already keyed the way the formatter looks arguments up: no copy needed.
            case Dictionary<string, object?> dictionary when ReferenceEquals(dictionary.Comparer, StringComparer.OrdinalIgnoreCase):
                return dictionary;

            case IReadOnlyDictionary<string, object?> readOnly:
            {
                Dictionary<string, object?> copy = new(readOnly.Count, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, object?> entry in readOnly)
                {
                    copy[entry.Key] = entry.Value;
                }

                return copy;
            }

            case IDictionary<string, object?> mutable:
                return new Dictionary<string, object?>(mutable, StringComparer.OrdinalIgnoreCase);
        }

        PropertyInfo[] properties = arguments.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);

        Dictionary<string, object?> values = new(properties.Length, StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo property in properties)
        {
            if (property.GetIndexParameters().Length == 0 && property.CanRead)
            {
                values[property.Name] = property.GetValue(arguments);
            }
        }

        return values;
    }
}
