# Progress

## Next release

**Planned:** `v2026.3.924`, 2026-09-24, alongside the rest of the family. The version comes from the
tag; see [docs/publishing.md](docs/publishing.md).

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
| `build: trimming is required` | build: `.github/repository.json` requires trimming, and `TrimmableTests` reads the `IsTrimmable` mark off the compiled assembly; `repo-conventions` and the test each fail if `IsAotCompatible` goes. Nothing a package carries changes |
| `ci: the nuget topic only where packages.push names an id` | ci: `scripts/repo-conventions.cs` is the template's current copy (Templates `fb6961a`). CI only |
