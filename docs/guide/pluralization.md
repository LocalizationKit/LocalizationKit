# Pluralization

English has two plural forms. Polish has four, Arabic six, Japanese one. Code that writes
`count == 1 ? "item" : "items"` has hard-coded English grammar into the application, and no
translator can undo it from a resource file.

ICU MessageFormat moves the decision into the resource, and CLDR decides which branch a number
lands in for the language being rendered.

## Add the engine

```csharp
services.AddIcuMessages();   // LocalizationKit.Messages.Icu
```

Without it, the default formatter does `{name}` substitution and refuses everything else with
<xref:LocalizationKit.UnsupportedMessageConstructException>, naming the key and the construct. That
is deliberate: a plural rendered by a formatter that does not understand plurals would be a
plausible, wrong string.

## Write the message

```
Cart.Items  en-US:  {count, plural, =0 {No items} one {# item} other {# items}}
            it-IT:  {count, plural, =0 {Nessun articolo} one {# articolo} other {# articoli}}
            pl-PL:  {count, plural, one {# plik} few {# pliki} many {# plików} other {# pliku}}
```

[!code-csharp[](../snippets/Features.cs#plural)]

`#` is the count, formatted for the culture. `=0` matches that exact value, before categories are
considered — useful because "No items" is usually better than "0 items", and it is a translator's
decision, not a developer's.

## The categories a language actually has

CLDR defines six: `zero`, `one`, `two`, `few`, `many`, `other`. Which ones a language uses is a
property of the language, and `other` is required everywhere.

| Language | Categories |
| --- | --- |
| Japanese, Chinese | `other` |
| English, German | `one`, `other` |
| Italian | `one`, `many`, `other` |
| Polish | `one`, `few`, `many`, `other` |
| Arabic | `zero`, `one`, `two`, `few`, `many`, `other` |

Writing a branch a language does not use is harmless but dead. Omitting one it does use means
numbers falling through to `other`, which reads wrong in exactly the cases a developer testing in
English never sees. [The editor](editor.md) offers the right fields per language, and
[validation](validation.md) catches the rest.

## Beyond plurals

**Ordinals**, which are a different set of rules from plurals:

```
Race.Position  {place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}
```

**`select`**, for anything with named cases — grammatical gender, a status, a role:

```
Invite.Sent  {gender, select, female {Lei ha invitato} male {Lui ha invitato} other {Hanno invitato}} {name}
```

**`offset:`**, for "you and 3 others":

```
Likes  {count, plural, offset:1 =1 {You liked this} one {You and # other} other {You and # others}}
```

**Nesting**, because a `select` branch can hold a plural and the other way round:

```
Notify  {kind, select, file {{count, plural, one {# file} other {# files}}} other {{count, plural, one {# item} other {# items}}}}
```

**Inline formatting**, so a number or date inside a message follows the culture:

```
Summary  {total, number, currency} on {when, date, long}
```

## Apostrophes

ICU uses `'` for quoting, which matters in languages full of apostrophes.

| Written | Renders as |
| --- | --- |
| `L'articolo` | `L'articolo` — a lone apostrophe is literal |
| `'{'` | `{` — quotes the brace |
| `''` | `'` — an escaped apostrophe |

The rule in practice: an apostrophe is literal unless it is immediately before `{` or `}`. Italian
and French resources mostly need no thought; a message that must show a literal brace does.

## Checking a message without rendering it

[!code-csharp[](../snippets/Features.cs#validate-message)]

<xref:LocalizationKit.IcuMessageSyntax> is in the core, not the ICU package, so validation does not
depend on the engine being referenced. It rejects malformed patterns, a misspelled category such as
`oen` — which an engine renders happily by falling through to `other` — and a `plural` or `select`
with no `other` branch.

It also answers what a message asks to be given:

[!code-csharp[](../snippets/Features.cs#message-arguments)]

That is the check behind `LK015`, the rule that makes a plural trustworthy across languages. See
[Validation on build](validation.md).
