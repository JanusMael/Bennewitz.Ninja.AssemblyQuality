# 00001 — Friend-grant rules: BNAQ1005 and BNAQ1006

> Status: **approved 2026-09-28**. Supersedes nothing.

Two configurable rules that read what was compiled, so the family's `InternalsVisibleTo` convention
can be checked rather than trusted. The convention itself (the generated, linked
`AssemblyInfo.InternalsVisibleTo.cs`, the hand-written External file, the opt-out property) belongs
to Bennewitz.Ninja.Templates and is planned there as `00006`. This plan covers only what
AssemblyQuality ships. The maintainer's decisions of 2026-09-28 are in `PROGRESS.md` under "Next".

## Decisions

| Decision | Choice | Why |
|---|---|---|
| **`BNAQ1005` — grants name only allowed assemblies** | `FriendGrantRule(allowed)` reads every `InternalsVisibleToAttribute` on each scanned assembly, and reports a grant whose simple name is not in `allowed` | A grant to a misspelled name, or to a namespace where an assembly name belongs, compiles, ships and grants nothing. Read from the compiled assembly, it covers every spelling: the attribute, the SDK `<InternalsVisibleTo>` item, `<AssemblyAttribute>` |
| What `BNAQ1005` allows | **Only what the caller passes**: the solution's assemblies plus the External file's names, per the maintainer's decision. Unconfigured, it allows nothing to be checked and reports `Inspected` of `0`, as `BNAQ1003` does | Consumers scan their *shipped* assemblies, never their tests (ScopedEditors' `AssemblyQualityTests.Shipped`). A default of "the assemblies in the scan" would therefore report every grant to a test assembly, which under `00006` is every shipped assembly's first grant |
| How names compare | Simple name only, case-insensitively, as the runtime compares them. A `PublicKey=` part is ignored | The family does not sign. A keyed grant is matched by name like any other |
| **Where `BNAQ1006` runs** | **At the provider's release**, not in the consumer's tests (the maintainer, 2026-09-28). The provider's tests load the consumer assemblies already published against it beside the provider's new build, and scan those consumers | A consumer's own tests compile against the provider version they load, and a removed internal is a compile error there, so the rule could almost never fail. Skew breaks only a downstream app that resolves a newer provider under an older consumer, and the provider's release is the one place that combination can be assembled before anyone ships it |
| **`BNAQ1006` — references across repositories resolve** | `FriendReferenceRule` enumerates each scanned assembly's type and member references with `System.Reflection.Metadata`, and resolves each token through `Module.ResolveMember` / `ResolveType` against the assemblies that actually loaded | The runtime's own resolution, not a reimplementation: decoding signatures by hand to match overloads and generics is where a checker quietly disagrees with the loader |
| What `BNAQ1006` examines | References into every referenced assembly **outside the scan and outside the shared framework** | Assemblies in the scan are built together, so their references cannot skew. The framework is the runtime's. Everything else, another family repository or a package, is where a newer version can remove what this assembly calls |
| What `BNAQ1006` reports | A reference that does not resolve; and a reference that resolves to a **non-public** member of an assembly that no longer grants the scanned assembly | Both fail at run time: the first with `MissingMethodException` and relatives, the second with `MethodAccessException`. Scoping to "assemblies that grant this one" would hide the second, because a withdrawn grant removes the provider from that set |
| `Skipped` | A reference into an assembly that will not load is named, as every rule does | The contract in `AGENTS.md` |
| This repository's own fixtures | The fixtures that hold deliberate grants, or a deliberately withdrawn one, set `SolutionFriendGrants` to `false` once this repository adopts `00006` | They exist to break the convention on purpose; the shared, linked grant file would otherwise grant them everything and hide the case under test. This depends on `00006` letting an opted-out project declare its own grants (see Dependencies) |

Dismissed:

- **A family registry of assembly names** for `BNAQ1005`: the maintainer chose the solution plus the
  External file; a registry is one more list to keep current.
- **A default allow-list of "the assemblies in the scan"** for `BNAQ1005`: wrong for how consumers
  scan (see above).
- **Scoping `BNAQ1006` to assemblies that grant the scanned one**: it cannot see the grant that was
  withdrawn, which is one of the two failures it exists for.
- **Hand-decoding member-reference signatures** for `BNAQ1006`: slower to write and wrong in the
  cases that matter (generic methods, `modreq`, nested generic types).
- **Running `BNAQ1006` in the consumer's tests**: it compiles against what it loads, so a break is a
  compile error before the rule could see it.
- **Source-side counterparts in CodeQuality**: the compiled grant list and the resolved version are
  what matter, and neither exists at compile time. Not planned.

## Dependencies

- **`00006` in Bennewitz.Ninja.Templates** must let a project with `SolutionFriendGrants` set to
  `false` declare grants of its own, or give this repository another sanctioned way to keep fixtures
  whose grants are deliberately wrong. As drafted, its decision 7 fails `check` on any declared grant.
  Raised with the Templates session.
- **A later Templates plan** owns the provider-side convention: which repositories declare their
  consumers, and where the provider's `BNAQ1006` test lives. `00006`'s decision 9 places the check at
  the provider's release and defers that wiring until this plan's step 2 proves it. This plan ships
  the rule and the proof; the convention is Templates'.

## Scope

In:

- `BNAQ1005` and `BNAQ1006`, their fixtures, tests and README sections.
- Loading references through the scanned assembly's own `AssemblyLoadContext` (step 1), which
  `BNAQ1006`'s tests need and the existing rules should have had.
- `PROGRESS.md`, and the release on the maintainer's go.

Out:

- The Templates convention and `bbavalonia`'s call to `BNAQ1005`.
- Strong-name signing, which the maintainer declined.
- Each repository's migration, which happens when it syncs the conventions.

## Steps

1. **Resolve references in the scanned assembly's load context.**
   `AssemblyScanContext.ReferencedNamespaces` calls `Assembly.Load(reference)`, which resolves in the
   default context even for an assembly loaded into another one. Use
   `AssemblyLoadContext.GetLoadContext(assembly)` instead.
   *Verified by:* a test that scans an assembly loaded into its own `AssemblyLoadContext`, beside a
   different version of a dependency, and sees that version's namespaces. The test fails first on the
   current code.

2. **Spike `Module.ResolveMember` against a missing member.** Confirm what it throws for a
   `MemberRef` whose target is gone, a `TypeRef` whose type is gone, and a member that exists but is no
   longer accessible, on the runtime of the SDK `global.json` pins. Confirm too how to tell a
   shared-framework assembly from a package one at run time.
   Then prove the provider-side wiring: a provider's test project fetches a consumer package with
   `<PackageDownload>`, which downloads it without adding it to the restore graph, so the provider
   project's own build is not unified away, and loads the consumer's DLL beside that build in an
   `AssemblyLoadContext`.
   *Verified by:* the spike's observed exception types and framework test, written into step 4's
   design; and a throwaway provider and consumer pair, published to a local feed, where removing the
   consumed internal from the provider makes its test fail. If `ResolveMember` does not fail
   faithfully, or `PackageDownload` cannot keep the two versions apart, stop and re-propose rather
   than hand-decode or work around it.

3. **`BNAQ1005`.** Fixture assembly `tests/fixtures/Granting` with four grants: one to an assembly
   in the scan, one to an External name, one to a namespace-form name
   (`Bennewitz.Ninja.Granting.Tests`), and one to a name that exists nowhere.
   *Verified by:* configured with the scan's assemblies it reports the last three; with the External
   name added it reports two; `Inspected` equals the number of grants; unconfigured it reports
   `Inspected` of `0`; each new test is seen to fail before the rule is written.

4. **`BNAQ1006`.** Fixtures: a provider built twice under one assembly name (`FriendV1` with an
   internal method it grants to the consumer, and `FriendV2` without that method, and no longer
   granting while keeping a second internal the consumer calls), and a consumer compiled against V1.
   V2 is built but not referenced, and copied beside the consumer the way `Orphan` is, so the test
   project never references two assemblies of one name. The test loads the consumer beside V2 in its
   own `AssemblyLoadContext`, as a consumer picking up a newer provider would.
   *Verified by:* the rule reports the missing member and the call into the no-longer-granted
   internal; beside V1 it reports nothing; an assembly whose only references are to the framework and
   to assemblies in the scan reports `Inspected` of `0`; a provider that will not load lands in
   `Skipped`; each new test is seen to fail before the rule is written.

5. **Docs.** A `### BNAQ1005` and a `### BNAQ1006` README section, which
   `EveryRule_HasAReadmeSectionOfItsOwn` already demands. Each says what a public consumer gets from
   it outside the family: any solution that grants internals can check its grants, and any assembly
   can check that what it calls in its dependencies still resolves. `BNAQ1006`'s section shows the
   provider-side wiring from step 2, since that is where it can fail. "Trusting a clean result" and
   `src/AGENTS.md` gain the new rules.
   *Verified by:* the catalogue, README and prefix tests pass; `repo-conventions check --release`
   conforms.

6. **Release** on the maintainer's go, then tell the Templates session the version, so `bbavalonia`
   can call `BNAQ1005`.
   *Verified by:* the version on nuget.org, installed from the feed alone.

Each step is its own pull request, merged by admin once `build`, `pack` and `conventions` are green.
