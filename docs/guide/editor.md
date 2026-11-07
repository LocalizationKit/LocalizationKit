# The editor

A `.resw` file edited in a text editor is XML with ICU syntax inside it, which is not a reasonable
thing to hand a translator — and a plural is the worst of it, because the categories differ per
language and nothing on screen says which ones are needed.

The editor is a single page that opens two cultures side by side and takes the syntax seriously.

## What it does

**One field per plural branch**, with the categories *this* language actually has — taken from the
browser's own CLDR data, so Italian is asked for `one` and `other` while Polish is asked for
`one`, `few`, `many` and `other`. Nobody has to know the table.

**A preview table** under the fields, showing how the message reads at 0, 1, 2, 3, 5, 11, 21 and
101, and which branch each count lands in. This is where a wrong category becomes obvious.

**The same findings as the build**, by the same codes — `LK006`, `LK008`, `LK009`, `LK012`,
`LK015` — so the page and `localizationkit validate` cannot disagree with each other.

## It gives the file back as it found it

The header, the indentation, the line endings and the byte order mark are the ones the file was
opened with, and an entry the editor does not understand — an image, a byte array — is kept as the
bytes it was.

This is not politeness, it is what makes the tool usable on a file in source control: a one-word
translation has to be a one-line diff, or no reviewer will look at it. `roundtrip.test.js` asserts
the property against a real project's files — read and rewrite 338 entries, and the result is byte
for byte what was there.

```
node tools/LocalizationKit.Editor/roundtrip.test.js
```

## Open it now

**[Open the editor](https://localizationkit.github.io/LocalizationKit/editor/)** — it is hosted on this site. Served over HTTPS it gets
the File System Access API, so it opens your `.resw` files and saves back to them directly, with
nothing to install. A translator needs the link and nothing else.

## Where it runs

Same page, three ways in and out:

| Where | How files get in and out |
| --- | --- |
| Visual Studio tool window | The host passes them through, and saves to the files it opened |
| [This site](https://localizationkit.github.io/LocalizationKit/editor/) | The File System Access API opens and saves directly |
| Anywhere else | Drag and drop in, copy the result out as text |

## In Visual Studio

**Tools → LocalizationKit Resw Editor.** It finds the `Strings/<culture>/Resources.resw` files of
the open solution, opens the first two, and saves back to them.

It is a classic VSIX, because Visual Studio's newer out-of-process extensibility model builds tool
windows through Remote UI and has no documented way to hold a WebView2 and exchange messages with
it. That also means it is built on its own, with Visual Studio's MSBuild rather than
`dotnet build` — see the extension's README in the repository.
