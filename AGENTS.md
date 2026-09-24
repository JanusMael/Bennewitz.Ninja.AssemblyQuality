# AGENTS.md

Guidance for coding agents working in this repository. Read [README.md](README.md) for what the
library does; this file covers how to change it.

## Commands

```bash
dotnet build AssemblyQuality.slnx -c Release -warnaserror
```

```bash
dotnet test --solution AssemblyQuality.slnx -c Release
```

```bash
dotnet pack AssemblyQuality.slnx -c Release --output ./packages/Release
```

```bash
dotnet run scripts/assert-packages.cs -- ./packages/Release
```

These are the same steps CI runs ([.github/workflows/ci.yml](.github/workflows/ci.yml)).
`TreatWarningsAsErrors` is on everywhere, so a warning fails the build locally too.

## Layout

| Path | What it holds |
|---|---|
| `src/AssemblyQuality/` | The library: `IAssemblyRule`, `AssemblyRuleResult`, `AssemblyScanContext`, and `Rules/` |
| `tests/AssemblyQuality.Tests/` | xunit v3 tests; `Fixtures/` holds real types that break the rules |
| `tests/fixtures/` | Fixture assemblies the tests reflect over. They have their own `Directory.Build.props`, so they are plain libraries, not test projects |
| `packages.push` / `packages.local` | Which package ids publish and which must never publish. `PackagingTests` fails on any packable project listed in neither |
| `docs/publishing.md` | Trusted publishing (OIDC) setup and failure modes |
| `PROGRESS.md` | Work state and the list of what goes into the next release |

## The contract every rule keeps

- **A rule reports and never asserts.** It returns an `AssemblyRuleResult`, and the consumer
  decides how severe a finding is.
- **`Inspected` counts only candidates that could have produced a finding.** A rule whose check
  can't be satisfied (it forbids nothing, or nothing it could match is in reach) reports `0`,
  never the number of things it walked past.
- **`Skipped` names what the rule could not examine**, such as a reference that would not load or a
  type whose signature names a missing assembly. Record the item and move on; one missing
  dependency must never crash the whole scan. Use `AssemblyScanContext.IsLoadFailure` and
  `Unreadable`.
- **Load references through `AssemblyScanContext.ReferencedNamespaces`**, which also reads forwarded
  types. A facade like `System.Runtime` exports nothing and forwards everything.
- **Every reflection entry point carries
  `[RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]`.** `IsAotCompatible` turns on the
  trim analyzers, so a missing attribute fails the build. The library is not trim-safe, and the
  attribute is what says so to consumers.
- **Every rule has a public parameterless constructor.** `RulesCatalogTests` creates each rule that
  way. Put configuration in extra constructors or static factories, such as
  `SurfaceLeakRule.Only(...)` and `NamespaceShadowRule.IncludingInternalTypes()`.
- **A rule's `Id` is public API.** Changing it silently removes every suppression a consumer wrote
  against it.

## Tests

- **Test both directions, always.** Each rule is shown finding a real violation AND clearing a clean
  subject. A test that only asserts "no findings" passes just as well on a rule that has stopped
  matching anything.
- **Fixtures are real compiled types, never mocks**, because the rules read metadata. When a test
  needs a runtime condition, such as a dependency that won't load, build a real assembly under
  `tests/fixtures/` that has it, and assert the condition itself before relying on it (see
  `CoverageTests.TheOrphansReference_ReallyWillNotLoad`).
- **Watch a new test fail without the fix before trusting it.** Put the old code back, confirm the
  new test fails, then restore.
- **The README rules table is generated.** `RulesCatalogTests` fails if it drifts from the rule
  types. To regenerate it, set `AQ_UPDATE_DOCS=1` and run the tests.

## Code style

- XML doc remarks follow a house style: one `<para>` per point, each opening with a marker and a
  bold claim: ⭐ key idea, ⚠ caution, ⛔ trap. Explain *why*, and say "Measured:" when a claim
  comes from an observed failure.
- The root namespace is `Bennewitz.Ninja.<ProjectName>`. Assembly names stay unprefixed
  (`AssemblyQuality`); the package id carries the prefix.
- Finding messages state the defect, why it bites, and every valid fix. Never prescribe one fix when
  a common convention needs a different one (see AQ1004's two renames).

## Commits and releases

- Commits are Conventional Commits with no AI attribution trailer.
- Update `PROGRESS.md` in the same change as the work it describes. Add each change headed for the
  next release to its table, and fill in the hash in a follow-up commit.
- Pushing a `v*.*.*` tag triggers [release.yml](.github/workflows/release.yml), and the package
  version comes from that tag. **A published NuGet version can never be replaced**, so never push a
  tag without the maintainer's explicit go-ahead. Setup, the credential preflight and failure modes
  are in [docs/publishing.md](docs/publishing.md).
