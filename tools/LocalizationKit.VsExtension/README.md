# LocalizationKit Resw Editor

A Visual Studio tool window that edits `.resw` resource files with their ICU plural syntax in mind.

**Tools → LocalizationKit Resw Editor.** It finds the `Strings/<culture>/Resources.resw` files of the
open solution, opens the first two side by side, and saves back to the files it opened.

For a message that is a plural it offers one field per branch, with the categories the language
actually has — Italian is asked for `one` and `other`, Polish for `one`, `few`, `many` and `other` —
and shows how the message reads at 0, 1, 2, 3, 5, 11, 21 and 101. It reports the same findings as
`localizationkit validate` and the build, by the same codes.

Saving preserves the file it read: header, indentation, line endings and byte order mark are the
ones it was opened with, and an entry it does not understand is kept as the bytes it was. A
one-word translation stays a one-line diff.

The same page runs in a browser from `tools/LocalizationKit.Editor/index.html` if you would rather
not install anything. The extension links that file rather than keeping a copy of it.

## Installing

Install **`bin\Release\LocalizationKit.VsExtension.vsix`**, and check that it is the one you are
double-clicking — a `bin\Debug` build from an earlier day is the easiest mistake to make, and the
symptom is the extension appearing under whatever name it had back then.

If a previous build is already installed, uninstall it first (Extensions → Manage Extensions →
Installed) and restart Visual Studio before installing the new one. Visual Studio caches menu
definitions per extension version, so reinstalling the *same* version number over itself can leave
the old command text in the menus even though the new assembly is the one loaded.

Requires Visual Studio 2022 or newer and the WebView2 runtime, which Visual Studio normally brings.

## Building

The extension is **not** in `LocalizationKit.slnx`, which is why opening the kit's solution does not
show it. It cannot be: a VSIX is a .NET Framework project built by the VS SDK's targets, and CI
builds the kit with `dotnet build`, which cannot build one. Putting it in the same solution would
break every build of everything else.

It has a solution of its own. Open `LocalizationKit.VsExtension.sln` in Visual Studio, or:

```
pwsh tools\LocalizationKit.VsExtension\build.ps1
```

which finds Visual Studio's MSBuild through `vswhere` and builds Release to
`bin\Release\LocalizationKit.VsExtension.vsix`.

It builds with `-t:Rebuild` deliberately. The VSIX targets judge the container up to date from the
assembly alone, so a change confined to `source.extension.vsixmanifest` — the display name, the
version, the icon — otherwise leaves the previous manifest inside the new `.vsix`.

Bump the version in `source.extension.vsixmanifest` whenever anything visible changes, so Visual
Studio treats an install as an upgrade rather than serving its cached command table.

Pressing F5 starts a second Visual Studio — the experimental instance — with the extension loaded,
which is how to debug the tool window without installing anything.

## How it works

A classic VSIX, because Visual Studio's newer out-of-process extensibility model builds a tool
window's contents through Remote UI, which has no documented way to hold a WebView2 and exchange
messages with it. This model hosts the control directly, which is what the page needs.

The page and the host exchange four messages. The page sends `ready` when it has loaded, `openFiles`
when someone asks for different files, and `save` when a file was edited; the host answers with
`files`, each carrying a name, a path and its text.

The page decides the bytes. It keeps the header, indentation, line endings and byte order mark of
the file it read, and puts the mark back as the first character of the text it returns — so the host
writes with a UTF-8 encoder that adds none of its own. `roundtrip.test.js` next to the page asserts
that property against a real project's files.

The extension's `Identity Id` still contains `ReswStudio`, the name this started under. It is
deliberate and never displayed: the id is how Visual Studio recognises an install as an upgrade of
the one already there, so changing it would leave an existing copy installed alongside the new one.

The build is tested; the window is not, because opening a tool window needs Visual Studio running.
If it does not appear, check that the WebView2 runtime is installed and read the status line under
the page, which says which files it found.
