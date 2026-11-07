using System.Text;
using System.Text.RegularExpressions;

namespace LocalizationKit;

/// <summary>A <c>.resw</c> file that can be changed and written back without being reformatted.</summary>
/// <remarks>
/// <para>
/// <see cref="ReswFile.Write"/> renders a file from scratch, which is what scaffolding a new culture
/// wants. Changing an existing file is the opposite problem: these files live in source control beside
/// a project that Visual Studio also writes, and a tool that rewrites the whole document to update one
/// string turns a one-word change into a diff of every line.
/// </para>
/// <para>
/// So this keeps the file as the chunks it was read as — the header, each <c>&lt;data&gt;</c> element,
/// the comments and whitespace between them, the footer — and re-renders only the entries that were
/// touched. The indentation, the line endings and the byte order mark are the ones the file had. An
/// entry it does not understand, an image or a byte array, is kept verbatim.
/// </para>
/// <para>
/// One detail is worth knowing, because getting it wrong is silent: every <c>.resw</c> opens with a
/// comment block that documents the format, and the documentation contains example
/// <c>&lt;data name="Name1"&gt;</c> elements. Anything that finds data elements by pattern finds one
/// inside that comment first.
/// </para>
/// </remarks>
public sealed class ReswDocument
{
    private static readonly Regex NameAttribute = new(
        @"<data\b[^>]*\bname=""([^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex NonStringAttribute = new(
        @"<data\b[^>]*\b(type|mimetype)=",
        RegexOptions.Compiled);

    private static readonly Regex ValueElement = new(
        @"<value>(.*?)</value>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CommentElement = new(
        @"<comment>(.*?)</comment>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly List<Chunk> _chunks = [];
    private readonly string _header;
    private readonly string _footer;
    private readonly string _eol;
    private readonly string _dataIndent;
    private readonly string _childIndent;
    private readonly bool _bom;

    private ReswDocument(
        string header,
        string footer,
        string eol,
        string dataIndent,
        string childIndent,
        bool bom,
        List<Chunk> chunks)
    {
        _header = header;
        _footer = footer;
        _eol = eol;
        _dataIndent = dataIndent;
        _childIndent = childIndent;
        _bom = bom;
        _chunks = chunks;
    }

    /// <summary>Gets a value indicating whether the file started with a byte order mark.</summary>
    /// <remarks>
    /// Visual Studio writes one, so a file that had one has to keep it; a caller that writes the text
    /// itself rather than through <see cref="Save"/> needs to know which.
    /// </remarks>
    public bool HasByteOrderMark => _bom;

    /// <summary>Gets the entries, in the order the file holds them.</summary>
    public IReadOnlyList<ReswEntry> Entries =>
        [.. _chunks.Where(chunk => chunk.Entry is not null).Select(chunk => chunk.Entry!)];

    /// <summary>Reads a file, keeping everything needed to write it back unchanged.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The document.</returns>
    public static ReswDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path), HasBom(path));
    }

    /// <summary>Reads a document from text.</summary>
    /// <param name="text">The document text, without a byte order mark.</param>
    /// <param name="bom">Whether the file it came from starts with a byte order mark.</param>
    /// <returns>The document.</returns>
    /// <exception cref="FormatException">The text is not a <c>.resw</c> document.</exception>
    public static ReswDocument Parse(string text, bool bom = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        string body = text.StartsWith('﻿') ? text[1..] : text;
        string eol = body.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        int rootClose = body.LastIndexOf("</root>", StringComparison.Ordinal);
        if (rootClose < 0)
        {
            throw new FormatException("The document has no <root> element, so it is not a .resw file.");
        }

        List<Chunk> chunks = [];
        string? header = null;
        string dataIndent = "    ";
        string childIndent = "        ";
        int index = 0;

        while (index < rootClose)
        {
            if (body.AsSpan(index).StartsWith("<!--", StringComparison.Ordinal))
            {
                int end = body.IndexOf("-->", index, StringComparison.Ordinal);
                int stop = end < 0 ? rootClose : Math.Min(end + 3, rootClose);
                chunks.Add(new Chunk(body[index..stop], null));
                index = stop;
                continue;
            }

            int dataAt = FindData(body, index, rootClose);
            if (dataAt < 0)
            {
                chunks.Add(new Chunk(body[index..rootClose], null));
                break;
            }

            int lineStart = body.LastIndexOf('\n', dataAt) + 1;
            bool indentOnly = body[lineStart..dataAt].Trim().Length == 0;
            int prefixEnd = indentOnly ? lineStart : dataAt;

            if (prefixEnd > index)
            {
                chunks.Add(new Chunk(body[index..prefixEnd], null));
            }

            int close = body.IndexOf("</data>", dataAt, StringComparison.Ordinal);
            int end2 = close < 0 ? rootClose : close + "</data>".Length;
            int stop2 = end2;

            if (stop2 < body.Length && body[stop2] == '\r') { stop2++; }
            if (stop2 < body.Length && body[stop2] == '\n') { stop2++; }

            string raw = body[prefixEnd..stop2];

            if (header is null)
            {
                header = string.Concat(chunks.Select(chunk => chunk.Raw));
                chunks.Clear();
                dataIndent = body[lineStart..dataAt];
                childIndent = ChildIndentOf(raw, dataIndent);
            }

            chunks.Add(new Chunk(raw, ReadEntry(raw)));
            index = stop2;
        }

        if (header is null)
        {
            throw new FormatException("The document has no <data> elements, so there is nothing to change.");
        }

        return new ReswDocument(header, body[rootClose..], eol, dataIndent, childIndent, bom, chunks);
    }

    /// <summary>Sets an entry's value and note, adding the entry when it is not there.</summary>
    /// <param name="name">The resource name.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="comment">The translator note, or <see langword="null"/> to leave it as it is.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    public bool Set(string name, string value, string? comment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        for (int i = 0; i < _chunks.Count; i++)
        {
            ReswEntry? entry = _chunks[i].Entry;
            if (entry is null || !string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string note = comment ?? entry.Comment ?? string.Empty;
            if (entry.Value == value && (entry.Comment ?? string.Empty) == note)
            {
                return false;
            }

            _chunks[i] = new Chunk(
                null,
                entry with { Value = value, Comment = note.Length == 0 ? null : note });

            return true;
        }

        // A new entry goes where the last one is, so it does not land after the file's closing
        // whitespace.
        int at = _chunks.FindLastIndex(chunk => chunk.Entry is not null);
        _chunks.Insert(
            at < 0 ? _chunks.Count : at + 1,
            new Chunk(null, new ReswEntry(name, value, string.IsNullOrEmpty(comment) ? null : comment)));

        return true;
    }

    /// <summary>Renders the document.</summary>
    /// <returns>The text, without a byte order mark.</returns>
    public string ToText()
    {
        StringBuilder builder = new(_header);

        foreach (Chunk chunk in _chunks)
        {
            if (chunk.Raw is not null)
            {
                _ = builder.Append(chunk.Raw);
                continue;
            }

            ReswEntry entry = chunk.Entry!;

            _ = builder.Append(_dataIndent).Append("<data name=\"").Append(EscapeAttribute(entry.Name))
                .Append("\" xml:space=\"preserve\">").Append(_eol)
                .Append(_childIndent).Append("<value>").Append(EscapeText(entry.Value)).Append("</value>")
                .Append(_eol);

            if (!string.IsNullOrEmpty(entry.Comment))
            {
                _ = builder.Append(_childIndent).Append("<comment>").Append(EscapeText(entry.Comment))
                    .Append("</comment>").Append(_eol);
            }

            _ = builder.Append(_dataIndent).Append("</data>").Append(_eol);
        }

        return builder.Append(_footer).ToString();
    }

    /// <summary>Writes the document back.</summary>
    /// <param name="path">The file to write.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, ToText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: _bom));
    }

    private static bool HasBom(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> start = stackalloc byte[3];
        return stream.Read(start) == 3 && start is [0xEF, 0xBB, 0xBF];
    }

    private static int FindData(string body, int from, int limit)
    {
        int index = from;

        while (index < limit)
        {
            int next = body.IndexOf("<data", index, StringComparison.Ordinal);
            if (next < 0 || next >= limit)
            {
                return -1;
            }

            int comment = body.LastIndexOf("<!--", next, StringComparison.Ordinal);
            int closed = body.LastIndexOf("-->", next, StringComparison.Ordinal);

            if (comment >= 0 && comment > closed)
            {
                int end = body.IndexOf("-->", next, StringComparison.Ordinal);
                index = end < 0 ? limit : end + 3;
                continue;
            }

            return next;
        }

        return -1;
    }

    private static string ChildIndentOf(string raw, string dataIndent)
    {
        int newline = raw.IndexOf('\n');
        if (newline < 0)
        {
            return dataIndent + "    ";
        }

        int start = newline + 1;
        int end = start;
        while (end < raw.Length && (raw[end] == ' ' || raw[end] == '\t'))
        {
            end++;
        }

        return end > start ? raw[start..end] : dataIndent + "    ";
    }

    private static ReswEntry? ReadEntry(string raw)
    {
        Match name = NameAttribute.Match(raw);
        if (!name.Success || NonStringAttribute.IsMatch(raw))
        {
            return null;
        }

        Match value = ValueElement.Match(raw);
        Match comment = CommentElement.Match(raw);

        return new ReswEntry(
            Unescape(name.Groups[1].Value),
            value.Success ? Unescape(value.Groups[1].Value) : string.Empty,
            comment.Success ? Unescape(comment.Groups[1].Value) : null);
    }

    private static string Unescape(string text)
    {
        return text
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&apos;", "'", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);
    }

    // Text content and an attribute value escape differently, and the difference shows up in a diff:
    // a quotation mark inside <value> stays as it is, and escaping it there rewrites every line that
    // holds inline link markup.
    private static string EscapeText(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string EscapeAttribute(string text)
    {
        return EscapeText(text).Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    /// <summary>Either a run of text kept as it was, or an entry to be rendered.</summary>
    private sealed record Chunk(string? Raw, ReswEntry? Entry);
}
