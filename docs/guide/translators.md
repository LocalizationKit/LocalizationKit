# Handing a culture to a translator

PO and XLIFF are interchange formats, so neither is a resource provider: a translator receives one
and returns one, and converting belongs in the tooling rather than at runtime.

```
localizationkit export --culture it-IT --out it-IT.po
localizationkit import it-IT.po --culture it-IT --dry-run
```

The format is inferred from the file name — `.po`, `.pot`, `.xlf`, `.xliff` — or stated with
`--format`.

## What maps to what

| LocalizationKit | PO | XLIFF |
| --- | --- | --- |
| Key | `msgctxt` | `trans-unit` id |
| Source text | `msgid` | `source` |
| Translation | `msgstr` | `target` |
| Untranslated | `#, fuzzy` | `state="needs-translation"` |

The untranslated marker matters: it is what a translator's tool filters its queue by, so without it
they are handed a file with no indication of what still needs doing.

## Two decisions worth knowing

**An ICU message crosses whole.** PO has its own plural model, with a `msgstr[n]` per form chosen
by the catalogue's `Plural-Forms` header, and an ICU message already carries every form in one
string. Mapping between them means deciding which ICU category becomes which PO index, for every
language, and reassembling it afterwards without losing what PO cannot hold — `=0` exact matches,
nested arguments, a plural inside a `select` branch.

So the message travels as the string it is, and the translator sees the ICU syntax. That is a real
cost, and it is why [the editor](editor.md) exists.

**An import changes only what it has to.** It goes through the same format-preserving document the
editor uses, so a handful of new translations is a handful of changed lines rather than a rewritten
file — which keeps the diff reviewable.

It also skips units naming keys the default culture no longer has, because those were renamed or
deleted while the translator held the file, and writing them back would resurrect them. And it
never writes the exchange file's note over the target file's own: notes travel outwards, from the
source culture to the translator, not back.

Exporting a culture and importing it back unchanged is a no-op. That is asserted in the tests, and
it is the property that caught the note bug.

## Always dry-run first

```
localizationkit import it-IT.po --culture it-IT --dry-run
```

It prints what would change without touching anything. Worth making a habit, because an import
edits files that are in source control.
