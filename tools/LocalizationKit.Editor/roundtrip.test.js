// Pulls the editor's own parse/serialize and plural reader out of the page and runs them against the
// real resource files, because the property that matters is that saving an untouched file changes
// nothing on disk.
const fs = require("fs");

const path = require("path");

const HERE = __dirname;
const APP = process.env.RESW_PROJECT
  || path.join(HERE, "..", "..", "..", "simplenote-windows", "Simplenote.WinUI");

const page = fs.readFileSync(path.join(HERE, "index.html"), "utf8");

const script = page.slice(page.indexOf('<script>') + 8, page.lastIndexOf("</script>"));

// The page decodes XML text with DOMParser; node has none, so stub the few entities a .resw holds.
const stub = [
  "class DOMParser {",
  "  parseFromString(text) {",
  "    var inner = text.slice(3, text.length - 4);",
  "    var decoded = inner",
  '      .split("&lt;").join("<")',
  '      .split("&gt;").join(">")',
      '      .split("&quot;").join(String.fromCharCode(34))',
  '      .replace(/&#(\d+);/g, function (m, d) { return String.fromCharCode(Number(d)); })',
  '      .split("&amp;").join("&");',
  "    return { documentElement: { textContent: decoded } };",
  "  }",
  "}",
].join("\n");

// Everything up to the state object is the part under test; the rendering below it needs a DOM.
const logic = script.slice(0, script.lastIndexOf("//", script.indexOf("the I/O")));

const harness = new Function(stub + logic + "\nreturn { ReswFile, readPlural, writePlural, scanMessage, formatPlural, state };");
const { ReswFile, readPlural, writePlural, scanMessage, formatPlural } = harness();

let failures = 0;
function check(what, actual, expected) {
  const ok = actual === expected;
  failures += ok ? 0 : 1;
  console.log(`${ok ? "ok  " : "FAIL"}  ${what}${ok ? "" : `\n      got      ${JSON.stringify(actual)}\n      expected ${JSON.stringify(expected)}`}`);
}

// --------------------------------------------------------------- the real files, read and rewritten
for (const culture of (process.env.RESW_CULTURES || "en-US,it-IT").split(",")) {
  const path = `${APP}/Strings/${culture}/Resources.resw`;
  if (!fs.existsSync(path)) {
    console.log(`skip  ${culture}: no file at ${path} (set RESW_PROJECT to point at one)`);
    continue;
  }

  const original = fs.readFileSync(path, "utf8");
  const file = ReswFile.parse(original);
  const written = ReswFile.serialize(file);

  check(`${culture}: ${file.entries.length} entries round-trip byte for byte`, written, original);
}

// ------------------------------------------------------------------------------- the plural reading
const message = "{count, plural, one {Elimina definitivamente} other {Elimina definitivamente # note}}";
const plural = readPlural(message);
check("a plural is taken apart", plural.branches.length, 2);
check("and put back together unchanged", writePlural(plural), message);

const nested = "{count, plural, =0 {{total, plural, one {one} other {#}}} other {Added to #}}";
check("a nested plural survives", writePlural(readPlural(nested)), nested);

check("arguments are found at depth", scanMessage(nested).args.join(","), "count,total");
check("an apostrophe is not quoting", scanMessage("L'articolo è pronto").error, null);
check("a missing other is reported", readPlural("{n, plural, one {x}}").error !== null, true);
check("a bad category is reported", readPlural("{n, plural, oen {x} other {y}}").error !== null, true);

// ------------------------------------------------------------------------------- the preview column
check("Italian 1 uses one", formatPlural(plural, "it-IT", 1).category, "one");
check("Italian 5 uses other", formatPlural(plural, "it-IT", 5).category, "other");
check("# becomes the count", formatPlural(plural, "it-IT", 5).text, "Elimina definitivamente 5 note");

const polish = readPlural("{count, plural, one {# plik} few {# pliki} many {# plików} other {# pliku}}");
check("Polish 2 is few", formatPlural(polish, "pl-PL", 2).category, "few");
check("Polish 5 is many", formatPlural(polish, "pl-PL", 5).category, "many");

console.log(failures === 0 ? "\nAll checks passed." : `\n${failures} check(s) failed.`);
process.exit(failures === 0 ? 0 : 1);
