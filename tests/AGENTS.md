# AGENTS.md — `tests/`

The test project and the fixture assemblies it reflects over. `tests/Directory.Build.props` makes a
project here an xUnit v3 test executable that is never packed; `tests/fixtures/Directory.Build.props`
keeps the fixtures plain libraries.

| Path | What it holds |
|---|---|
| `AssemblyQuality.Tests/Rules/RuleTests.cs` | Every rule in both directions, against `Fixtures/` and against the library itself |
| `AssemblyQuality.Tests/Rules/CoverageTests.cs` | What a rule did not see: `Skipped`, facade roots, the internal-types opt-in, against the `Orphan` fixture |
| `AssemblyQuality.Tests/Rules/RulesCatalogTests.cs` | The catalogue: ids unique, summaries present, the README rules table in sync |
| `AssemblyQuality.Tests/Packaging/PackagingTests.cs` | Every packable project classified in `packages.push` or `packages.local`, and `release.yml` naming what it publishes, never globbing |
| `AssemblyQuality.Tests/Fixtures/` | Compiled subjects: `Offender`, `Overloaded`, `Clean` in `Subjects.cs`; namespace shadows in `Shadowed.cs`, `HiddenShadow.cs`, `OwnRoot.cs` |
| `fixtures/Orphan/`, `fixtures/Absent/` | `Orphan` references `Absent` with `Private="false"`, and only `Orphan`'s dll is copied into the test output's `orphan/` folder, so `Absent` really will not load |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| Both `Directory.Build.props` files here import the root props explicitly | MSBuild applies only the closest one; without the import a project silently loses the root's target framework and settings | the import line in each |
| A fixture project goes under `tests/fixtures/`, never beside the test project | `tests/Directory.Build.props` would make it an xUnit executable | `tests/fixtures/Directory.Build.props` |
| A type added to `AssemblyQuality.Tests/Fixtures/` is seen by every rule scanning `RuleTests.Subjects` | The whole test assembly is the subject, so a stray public type can add or hide a finding | `RuleTests` |
| `OwnRoot.cs` extends the `System` namespace on purpose | It is the only faithful subject for a namespace whose own root is a referenced root | `RuleTests.ANamespaceSharingItsOwnRootWithAReference_IsNotAShadow` |
| A test relying on a runtime condition asserts that condition first | Otherwise it passes on a dependency that loaded after all | `CoverageTests.TheOrphansReference_ReallyWillNotLoad` |
| **Never weaken a packaging test to make it pass** | They stand between a packaging mistake and a permanent release. When one fails, the package list or the workflow is wrong | `PackagingTests` |
