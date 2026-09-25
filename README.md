# Bennewitz.Ninja.AssemblyQuality

Static analysis rules for the shape a .NET assembly **ships** — read by reflection over the
compiled output, which is the thing a consumer actually binds against. A source analyser sees what
was written; this sees what was published.

The assembly-shaped companion to
[Bennewitz.Ninja.XamlQuality](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality): that one
reads markup, this one reads types. Every rule here came from a guard written inline in a real
repository, where it could assert "no offenders today" and never that it would find one.

## Install

```bash
dotnet add package Bennewitz.Ninja.AssemblyQuality
```

Run it where it belongs: in a test project, against the untrimmed build. The rules read by
reflection, so they are not trim-safe. `IAssemblyRule.Analyze` carries
`[RequiresUnreferencedCode]`, so calling it from an application with trimming or AOT enabled
raises a warning at the call site. Without that warning, a trimmed scan would complete and quietly
report less than it should.

## Trusting a clean result

On its own, a rule with no findings says less than it seems to. Together, these three checks make
it mean something:

| Check | What it rules out |
|---|---|
| `Inspected > 0` | The rule had nothing it could check, not "checked and clean". |
| `Skipped` is empty | The rule examined only what it could load, not everything. |
| A check that fails on the wrong assembly | The rules ran against whatever they were handed, not the assembly you meant. |

`Skipped` applies to the rules that load types or references: `BNAQ1001`, `BNAQ1002` and
`BNAQ1004`. `BNAQ1003` reads only the names in the reference list, loads nothing, and so never skips.
An empty-`Skipped` assertion on it passes, but it cannot fail.

Assert on `Skipped` with the entries in the message, for example
`Assert.True(result.Skipped.Count == 0, string.Join("\n", result.Skipped))`. xunit's
`Assert.Empty` shortens each string it prints, which cuts off the part of an entry that says what
would not load and why.

The first two checks in the table describe the scan, and both pass when the scan is pointed at the wrong assembly.
Before running the rules, pin the subject by asserting something that is true only of it, such as
a dependency only it references, or one its neighbours carry and it must not. A helper that loads an
assembly by name and then checks that the name matches does not do this: it passes for whatever it
loaded.

## Releasing

See [docs/publishing.md](docs/publishing.md). The short version:

1. Add the `NUGET_USER` variable — your nuget.org **profile name**, not an email.
2. Create **one** trusted-publishing policy whose glob patterns cover every id in
   [`packages.push`](packages.push) and match nothing in [`packages.local`](packages.local).
3. Run **Release** → *Run workflow* with the version **blank**. That logs in and stops, proving the
   credentials without publishing.
4. Tag `vYYYY.Q.MMDD` and push.

⛔ **One policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to one matching policy — so a second policy is never consulted and its package is rejected `403`
after the first has already published permanently.

## Licence

MIT. See [LICENSE](LICENSE).

## Rules

<!-- BEGIN GENERATED RULES -->
| Id | Requires |
|---|---|
| `BNAQ1001` | No public method takes a CancellationToken with a default value. |
| `BNAQ1002` | No type from a leak-prone namespace appears in the public surface. |
| `BNAQ1003` | An assembly references none of the assemblies its layer forbids. |
| `BNAQ1004` | No declared namespace segment shadows the root namespace of a referenced assembly. |
<!-- END GENERATED RULES -->

**Renamed after `2026.3.922`.** Up to and including that version the IDs were `AQ1001`–`AQ1004`.
The numbers are unchanged, so `AQ1004` is now `BNAQ1004`: rename any suppression or filter that
names an old ID, or it stops matching. The prefix is `BN` plus the product's initials, shared across
the Bennewitz.Ninja quality packages, and it will not change again.

The table above is rendered from the rule types — `Id` and `Summary` on each `IAssemblyRule` — and
a test fails if it drifts. Regenerate it with `AQ_UPDATE_DOCS=1 dotnet test AssemblyQuality.slnx`
and commit the result.

**Every rule reports; none of them assert.** A finding can fail a build, warn in CI or print in a
report — the severity is the consumer's to choose, and a library that threw would have chosen both
the runner and the severity for them.

**Assert on `Inspected`, not only on `Findings`.** A rule given no assemblies, or one configured to
forbid nothing, returns zero findings and reads exactly like a clean result. `Inspected` is what
tells "nothing is wrong" apart from "nothing was checked" — which is the failure every guard this
library replaced actually had.

**Source-side counterparts.** Three of these rules also exist as Roslyn analyzers in
[Bennewitz.Ninja.CodeQuality](https://github.com/JanusMael/Bennewitz.Ninja.CodeQuality), with the same
number under the `BNCQ` prefix. The analyzer reports at the line as it is typed and reads the source,
internal code included. This package reads what actually shipped, including everything a source
generator added. Where you can, run both: each sees something the other cannot.

### BNAQ1001

No public method takes a `CancellationToken` with a default value, and no public method is a
token-less overload of a sibling that takes one. A policy rule rather than a defect rule, so adopt it
deliberately. Counterpart: [`BNCQ1001`](https://github.com/JanusMael/Bennewitz.Ninja.CodeQuality#bncq1001),
off by default, which also reads protected methods and constructors.

### BNAQ1002

No type from a leak-prone namespace appears in a public signature, generic arguments unwrapped.
`new SurfaceLeakRule(extra)` adds namespaces to `LeakProneNamespaces`; `SurfaceLeakRule.Only(list)`
replaces them. Counterpart: [`BNCQ1002`](https://github.com/JanusMael/Bennewitz.Ninja.CodeQuality#bncq1002),
which walks referenced types transitively and also reads generic constraints and a containing type's
type arguments, such as `List<JsonNode>.Enumerator`. This rule reads signature types only.

### BNAQ1003

No assembly directly references one its layer forbids, matched by simple-name prefix. It forbids
nothing until configured, and then says so with `Inspected` of zero. There is no source-side
counterpart: `Microsoft.CodeAnalysis.BannedApiAnalyzers` already reports a forbidden use at its call
site, while this rule reads the reference list the compiled assembly actually kept.

### BNAQ1004

No namespace segment after the first shadows the root namespace of a referenced assembly.
`NamespaceShadowRule.IncludingInternalTypes()` extends it from public types to all of them.
Counterpart: [`BNCQ1004`](https://github.com/JanusMael/Bennewitz.Ninja.CodeQuality#bncq1004).

⚠ **A reference that does not load at run time is invisible here and visible there.** This rule
compares against the roots of references it can load, and names any it cannot in `Skipped`. The
analyzer runs where the compiler has every reference. Measured on this repository's own fixtures:
`BNCQ1004` reports the `Orphan.Absent` shadow in `tests/fixtures/Orphan`, whose referenced assembly
is deliberately missing at run time, while `BNAQ1004` lists that reference in `Skipped`.
