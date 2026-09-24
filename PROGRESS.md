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

## Follow-ups

Found while this repository's directory documentation was written (plans/00003 step 8 in
Bennewitz.Ninja.Templates) and left for this repository's own work:

- `AGENTS.md`'s Layout lists `AssemblyRuleResult` as though it had a file of its own; it is
  declared in `IAssemblyRule.cs`, and `AssemblyFinding` beside it is not listed.
- No test covers the release step `Refuse to release without NUGET_USER`:
  `PackagingTests.The_release_workflow_has_a_credential_preflight` asserts only that `RELEASING`
  and `NuGet/login` appear in the workflow.
