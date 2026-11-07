# Pseudolocalization

[!code-csharp[](../snippets/Features.cs#pseudolocalize)]

`Save` then arrives as `⟦Șàṽé∙∙⟧`. Run the application in a debug build and three faults that
otherwise wait for a real second language appear at once:

- **Text that no longer fits** is clipped where anyone can see it. The padding exists because
  translations are usually longer than English — German and Finnish routinely 30% longer.
- **Text that is still plain English** was never externalized. It is the only text on screen
  without accents, so it stands out rather than hiding.
- **A sentence built from two resources** shows up with brackets in the middle of it, which is the
  clearest possible signal that it will not survive a translator who needs to reorder it.

## What it leaves alone

Arguments, plural categories and [link markup](winui.md#sentences-containing-links) are not
rewritten, so a pseudolocalized message still formats:

```
{count, plural, one {# item} other {# items}}
{count, plural, one {# ìțém∙} other {# ìțémș∙∙}}
```

The keywords (`count`, `plural`, `one`, `other`) and the structure survive; only the readable text
inside each branch is rewritten, which is where a translation's long words actually live.

The tests assert that property over every message shape the library supports: rewrite it, and it
must still parse and ask for the same arguments.

## Where to turn it on

A debug build, or behind a developer switch — not a configuration file that could reach
production. It is a diagnostic, and the whole point is that it is obviously wrong on screen.
