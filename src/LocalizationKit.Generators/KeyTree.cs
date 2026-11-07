using System;
using System.Collections.Generic;
using System.Text;

namespace LocalizationKit.Generators;

/// <summary>Turns dotted resource names into the nested classes they describe.</summary>
/// <remarks>
/// <c>Settings.Theme.Header</c> becomes <c>Settings.Theme.Header</c> in C# too, which is the whole
/// point: the generated name reads like the key, so nobody has to translate between the two.
/// </remarks>
internal sealed class KeyTree
{
    private readonly SortedDictionary<string, KeyTree> _children =
        new SortedDictionary<string, KeyTree>(StringComparer.Ordinal);

    /// <summary>Gets the resource key this node is itself, when it is one.</summary>
    public string? Key { get; private set; }

    /// <summary>Gets a value indicating whether this node is a key and not a prefix of others.</summary>
    public bool IsLeaf => Key is not null && _children.Count == 0;

    /// <summary>Adds a resource name to the tree.</summary>
    /// <param name="key">The resource name, with dots separating its segments.</param>
    public void Add(string key)
    {
        KeyTree node = this;

        foreach (string segment in key.Split('.'))
        {
            if (segment.Length == 0)
            {
                // A name such as "A..B", or one with a trailing dot: there is nothing sensible to
                // generate, and the validator reports the name itself.
                return;
            }

            if (!node._children.TryGetValue(segment, out KeyTree? child))
            {
                child = new KeyTree();
                node._children.Add(segment, child);
            }

            node = child;
        }

        node.Key = key;
    }

    /// <summary>Writes the members of this node, which is always a class, into the source.</summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The indentation of the members.</param>
    public void Write(StringBuilder builder, string indent)
    {
        // Names are handed out before anything is written, because two different resource names can
        // want the same identifier — "Add tag" and "Add-tag" both want Add_tag — and a class cannot
        // share its name with the constant for the key it is a prefix of. Children first, so a key
        // whose class already exists is the one that moves aside.
        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        List<KeyValuePair<string, KeyTree>> children = new List<KeyValuePair<string, KeyTree>>();
        List<string> names = new List<string>();

        foreach (KeyValuePair<string, KeyTree> child in _children)
        {
            children.Add(child);
            names.Add(Identifier.Unique(used, Identifier.For(child.Key)));
        }

        bool wroteAnything = false;

        if (Key is not null)
        {
            // This node is a key and also a prefix of others — Settings.Theme next to
            // Settings.Theme.System — so the key lands on the class as Value. MakePri rejects that
            // pair and the validator reports it (LK012), but a project that resolves its strings
            // without PRI may legitimately have it.
            builder.Append(indent).Append("/// <summary>The key <c>").Append(Key).AppendLine("</c>.</summary>");
            builder.Append(indent).Append("public const string ").Append(Identifier.Unique(used, "Value"))
                .Append(" = \"").Append(Key).AppendLine("\";");

            wroteAnything = true;
        }

        for (int index = 0; index < children.Count; index++)
        {
            if (wroteAnything)
            {
                builder.AppendLine();
            }

            wroteAnything = true;

            KeyTree child = children[index].Value;
            string name = names[index];

            if (child.IsLeaf)
            {
                builder.Append(indent).Append("/// <summary>The key <c>").Append(child.Key)
                    .AppendLine("</c>.</summary>");
                builder.Append(indent).Append("public const string ").Append(name)
                    .Append(" = \"").Append(child.Key).AppendLine("\";");
                continue;
            }

            builder.Append(indent).Append("/// <summary>Keys under <c>").Append(children[index].Key)
                .AppendLine("</c>.</summary>");
            builder.Append(indent).Append("public static class ").AppendLine(name);
            builder.Append(indent).AppendLine("{");
            child.Write(builder, indent + "    ");
            builder.Append(indent).AppendLine("}");
        }
    }
}
