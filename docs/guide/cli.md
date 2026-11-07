# The CLI

```
dotnet tool install -g LocalizationKit.Cli
```

```
localizationkit info                     show what was discovered
localizationkit add-culture it-IT        resw file + project property + both IDE files
localizationkit sync-ide --dry-run       reconcile the gitignored IDE files
localizationkit validate                 keys, ICU syntax, coverage, DefaultLanguage agreement
localizationkit validate --unused        also list keys the source never mentions
localizationkit export --culture it-IT --out it-IT.po      hand a culture to a translator
localizationkit import it-IT.po --culture it-IT --dry-run  take it back
```

## Why a culture is more than a file

`.resw` costs more bookkeeping than JSON. In a WinUI project, adding Italian means three things:

1. `Strings/it-IT/Resources.resw` on disk.
2. A `PRIResource` item in the project's **gitignored** `.csproj.user`.
3. A column in Visual Studio's equally gitignored Resource Explorer state.

Miss the second and the file is not in the build. Miss the third and the Resource Explorer does not
show the language. Both files are gitignored, so a teammate cloning the repository has neither, and
nothing tells them.

`add-culture` writes all three. `sync-ide` reconciles them for someone who just cloned.

```
$ localizationkit sync-ide --dry-run
Project: ...\Simplenote.WinUI\Simplenote.WinUI.csproj
Cultures: en-US, it-IT
  would update  ...\Simplenote.WinUI.csproj.user
            add 1 PRIResource designer entry: Strings\en-US\Resources.resw
  would update  ...\.vs\simplenote-windows.slnx\v18\ResourceExplorer\settings.json
            Resource Explorer columns: add it-IT
  note    Visual Studio is running. It holds this state in memory and rewrites it when the
          solution closes, which can revert these edits; re-run after closing it if the columns
          do not appear.
```

## How it writes

Both writers **edit rather than regenerate**: they preserve the UTF-8 BOM or its absence, CRLF
endings, and the missing trailing newline that Visual Studio writes. They are idempotent, so a
second run is a no-op, and a stale culture column is removed only with `--prune` — because a column
you have not got round to translating yet is not the same as one you deleted.

## Where the culture list comes from

The `LocalizationKitCultures` MSBuild property when it is set, and the culture folders on disk when
it is not. `add-culture` extends the property if it exists, but will not add it to a
source-controlled project file on its own — that is a decision for whoever owns the project file.

## Validation

`localizationkit validate` runs the same rules as [the build](validation.md) and
[the editor](editor.md), by the same codes. `--unused` additionally lists keys that no source file
mentions, which is a report rather than an error: a key referenced only from XAML built at runtime,
or by string concatenation, will show up here and be fine.
