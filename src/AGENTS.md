# AGENTS.md — `src/`

The one shipped project, `AssemblyQuality`, packed as `Bennewitz.Ninja.AssemblyQuality`. It holds the
rule contract in the project root and the rules in `Rules/`. The contract every rule keeps is in the
root [AGENTS.md](../AGENTS.md); this file names where each part of it lives.

| File | What it is |
|---|---|
| `IAssemblyRule.cs` | `IAssemblyRule` (`Id`, `Summary`, `Analyze`) and `AssemblyRuleResult` (`Findings`, `Inspected`, `Skipped`, `Clean`) |
| `AssemblyFinding.cs` | One violation: `RuleId`, `AssemblyName`, `Subject`, `Message`. No line number, because metadata carries none |
| `AssemblyScanContext.cs` | The assemblies a scan covers (`Of`, `ExportedTypes`), plus the internal helpers rules share: `AllTypes`, `ReferencedNamespaces`, `IsLoadFailure`, `Unreadable`, `TrimMessage` |
| `Rules/CancellationTokenRule.cs` | `BNAQ1001`: no public method takes a defaulted `CancellationToken`, nor is a token-less overload of a sibling that takes one. A policy rule, not a defect rule |
| `Rules/SurfaceLeakRule.cs` | `BNAQ1002`: no type from a leak-prone namespace in the public surface, generic arguments unwrapped. Defaults to `LeakProneNamespaces`; `Only(...)` replaces the set |
| `Rules/ForbiddenReferenceRule.cs` | `BNAQ1003`: no direct reference whose simple name starts with a forbidden prefix. No default set, so unconfigured it inspects nothing |
| `Rules/NamespaceShadowRule.cs` | `BNAQ1004`: no namespace segment after the first shadows a referenced assembly's root. Public types by default; `IncludingInternalTypes()` widens it |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| A rule is a public, concrete `IAssemblyRule` in this assembly with a parameterless constructor | The catalogue is discovered by reflection and each rule created through `Activator.CreateInstance`; the shared helpers on `AssemblyScanContext` are `internal` | `RulesCatalogTests.DiscoverRules` |
| `Id` is unique and never changes; `Summary` states the requirement and ends with a full stop | Consumers suppress by id (`IAssemblyRule.Id`); the summary is the README row (`IAssemblyRule.Summary`) | `RulesCatalogTests.EveryRuleId_IsUnique`, `EveryRule_SummarisesWhatItRequires`, `TheCatalogue_IsNotSilentlyEmpty` |
| A rule whose predicate cannot be satisfied returns `Inspected` of zero | Otherwise "nothing checked" reads as "checked and clean" | `RuleTests.EveryRule_GivenNothing_ReportsThatItInspectedNothing`, `ALeakSetNothingInReachExports_ReportsThatItInspectedNothing`, `AForbiddenReferenceRuleWithNoConfiguration_ReportsThatItCheckedNothing`, `AnAssemblyWithNoReferencedRoots_ReportsThatItComparedNothing` |
| A load failure is caught with `AssemblyScanContext.IsLoadFailure`, recorded with `Unreadable` into `Skipped`, and the scan continues | One missing dependency must not take the scan down | `CoverageTests.AReferenceThatWillNotLoad_IsNamedInSkipped`, `ATypeWhoseSignatureCannotBeRead_IsSkippedNotFatal`, `AScanWithEverythingPresent_SkipsNothing` |
| Read a type's whole signature set before counting any of it | A throw part-way through leaves a count matching neither what was examined nor what was not | `CancellationTokenRule.Analyze`, comment |
| Referenced roots come from `AssemblyScanContext.ReferencedNamespaces`, which includes forwarded types | `System.Runtime` is a facade that exports nothing | `CoverageTests.ARootReachableOnlyThroughAFacade_IsComparedAgainst` |
| Every reflection entry point carries `[RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]` | `IsAotCompatible` turns on the trim analyzers and warnings are errors, so an unmarked one fails the build | `AssemblyQuality.csproj`, `IsAotCompatible` |
| `SurfaceLeakRule.LeakProneNamespaces` grows only deliberately | Adding a namespace changes findings for every consumer; growth belongs at the call site | `SurfaceLeakRule` remarks |
| The library references neither `System.Text.Json` nor `Newtonsoft.Json`, and none of its own namespaces shadows a referenced root | The tests use this assembly as the clean subject for `BNAQ1002` and `BNAQ1004` | `RuleTests.ALeakSetNothingInReachExports_ReportsThatItInspectedNothing`, `AnAssemblyWithNoShadowingSegment_IsClean` |
| A new or changed `Summary` regenerates the README table | The table is generated from the rule types | `RulesCatalogTests.TheReadmeTable_MatchesTheRuleTypes`; set `AQ_UPDATE_DOCS=1` and run the tests |
