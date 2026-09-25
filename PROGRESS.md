# Progress

## Next release

**On hold** at the maintainer's call. It has no fixed version: the tag is set from the date when the
maintainer gives the go, and nothing is tagged before that. The version comes from the tag; see
[docs/publishing.md](docs/publishing.md).

⛔ **It carries a breaking rename.** Every rule ID moves from `AQ100x` to `BNAQ100x`, same numbers
(see Decisions). A consumer's suppressions by the old IDs stop matching, so the release notes carry
the old and new IDs side by side.

Unreleased since `v2026.3.922`:

| Commit | Change |
|---|---|
| `0200315` | fix(AQ1004): the shadow finding names both fixes — rename the namespace alone, or the assembly name with it where namespaces follow the assembly name |
| `7db0478` | fix(AQ1004): the package id is not a namespace and may keep the shadowing word |
| `3f34a4a` | feat: trim analyzers on (`IsAotCompatible`); `IAssemblyRule.Analyze`, every rule's `Analyze` and `AssemblyScanContext.ExportedTypes` carry `[RequiresUnreferencedCode]` — a public API change |
| `05a6060` | fix: `Inspected` counts only what could have fired — AQ1004 reports 0 with no referenced roots, AQ1002 reports 0 where no covered namespace is in reach; new `SurfaceLeakRule.Only(...)` replaces the stock set; AQ1001 also reports a token-less overload of a tokened sibling. From DiffView's adoption review. Behaviour change: `Inspected` can drop to 0 and AQ1001 can report more |
| `044e71a` | feat: `AssemblyRuleResult.Skipped` names what a rule could not examine (references that would not load, types whose signatures name a missing assembly — previously a crash in AQ1001/AQ1002); `NamespaceShadowRule.IncludingInternalTypes()`. fix: AQ1004 reads forwarded types, so a root reachable only through a facade such as `System.Runtime` is compared against — before this, such an assembly's `.System` shadows went unreported |
| `ad75b45` | build: `IsContinuousIntegration` is gone and AutoVersioning is `2026.3.916` (`plans/00004` in Bennewitz.Ninja.Templates). Build only |
| `d147dba`, `b9711a3` | ci: `scripts/repo-conventions.cs` evaluates every project against the family's build properties. CI only |
| `e9f63e1` | build: `.github/repository.json` requires trimming, and `TrimmableTests` reads the `IsTrimmable` mark off the compiled assembly; `repo-conventions` and the test each fail if `IsAotCompatible` goes. Nothing a package carries changes |
| `bdd4bef` | ci: `scripts/repo-conventions.cs` is the template's current copy (Templates `fb6961a`). CI only |
| `0deb5c3` | feat!: rule IDs `AQ1001`–`AQ1004` are now `BNAQ1001`–`BNAQ1004`. Breaking for any suppression by the old IDs |

## Decisions

| Decision | Why |
|---|---|
| **Rule IDs are `BN` + the product's initials, with the rule number kept**: `BNAQ` here, `BNXQ` for XamlQuality, `BNCQ` for the planned CodeQuality analyzers | An analyzer's ID shares one flat namespace with every analyzer a project loads (`#pragma`, `NoWarn`, `.editorconfig`), and a two-letter prefix is the likeliest to collide. Four letters is unique enough and short enough to type; where a rule comes from is also shown by `helpLinkUri`, `Category` and the package id, so the ID need not spell it out |
| **A prefix never changes once one ID in it has shipped** | Every rename breaks consumers' suppressions. `AQ` → `BNAQ` is the one exception, taken while only `2026.3.922` was published and used only as a test dependency |
| **XamlQuality is asked to move to `BNXQ`** in its next release | One scheme across the family. Sent to the XamlQuality session on 2026-09-25; the rename is its to ship |

## Next

**Bennewitz.Ninja.CodeQuality**, a new repository for Roslyn analyzers, decided 2026-09-25. Its
decisions move into that repository once it exists:

| Decision | Choice |
|---|---|
| Source | A clean-room rewrite. Analyzers written for another project are reference only; none of that code is copied |
| Scope | Analyzers only. Code generators go elsewhere, e.g. beside Bennewitz.Ninja.AutoVersioning |
| Base class | A self-scoping `DiagnosticAnalyzer` base, internal to the analyzer assembly: a sealed `Initialize`, a scope gate that cannot register actions, and a test that every analyzer derives from it |
| Scaffolding | Generate with `bbpkg`, adapt `src/` and `tests/` to an analyzer (`netstandard2.0`, `IsRoslynComponent`, packed under `analyzers/dotnet/cs`, Roslyn analyzer testing), release it, then upstream the proven shape to Bennewitz.Ninja.Templates as a third template beside `bbpkg` and `bbavalonia` |
| First rules | Source-side counterparts of `BNAQ1004` (namespace shadow), `BNAQ1001` (cancellation token, opt-in) and `BNAQ1002` (surface leak, with a transitive member walk). Not `BNAQ1003`: `BannedApiAnalyzers` already covers a forbidden use site |
| IDs | `BNCQ`, each rule cross-linked to its `BNAQ` counterpart in both READMEs |
