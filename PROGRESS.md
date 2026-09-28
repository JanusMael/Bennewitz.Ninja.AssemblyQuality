# Progress

## Next release

Nothing unreleased since `v2026.3.928`. The version comes from the tag, set from the date when the
maintainer gives the go; see [docs/publishing.md](docs/publishing.md). One release per calendar day,
three-part `YYYY.Q.MMDD`: a fourth part is AutoVersioning's `HHmm` build stamp, not a patch counter.

## Next

**Friend grants across the family**, decided 2026-09-28. Two plans come first, one per repository,
each for the maintainer's approval:

1. **Bennewitz.Ninja.Templates** owns the policy: a generated `AssemblyInfo.InternalsVisibleTo.cs`
   listing every assembly in the solution, linked into every project by `Directory.Build.targets`;
   a hand-written `AssemblyInfo.InternalsVisibleTo.External.cs` for grants to other repositories;
   a csproj property to opt a project out; and conventions checks that the generated file is current
   and that no project declares a grant of its own. It lands first.
2. **This repository** adds two configurable rules, reading what was compiled: `BNAQ1005`, every
   `InternalsVisibleTo` grant names an allowed assembly; then `BNAQ1006`, every reference into
   another assembly's internals resolves against the version that loads.

| Decision | Choice |
|---|---|
| Split | Templates owns the policy; AssemblyQuality provides configurable checks, the `BNAQ1003` / `LayeringTests.Tiers` shape |
| Within a solution | Every project grants to every other, test projects included; grants between product projects are encouraged. `internal` then means solution-internal, so a member no other assembly may reach is `private` |
| Across repositories | Internals may be granted to the maintainer's other repositories wherever that reduces friction, declared in the External file |
| Signing | None. A grant is matched by assembly name, and on modern .NET public signing satisfies a keyed grant without the private key, so neither is a security boundary. Grants name only assemblies the family ships, and public consumers get none |
| Version skew | An internal another repository uses is an informal promise: its owner does not change it without releasing the consumer. `BNAQ1006` in the consumer's tests catches a break against the resolved version |
| `BNAQ1005`'s allowed names | The solution's own assemblies plus the External file's names, supplied by the repository's test. No family registry for now |
| Existing repositories | Each moves over when it next syncs the Templates conventions. AppServices, FileServer and ScopedEditors document "grants to tests only" and rewrite those rules then |
| Order | Plans, then the Templates convention, then `BNAQ1005`, then `BNAQ1006` |

## Last release

**`v2026.3.928`**, 2026-09-28: published to nuget.org and verified from the feed. The DLL in the
feed's package is byte-identical to the GitHub release asset, and a project restoring from nuget.org
alone gets it. It fixes `2026.3.925`'s crash when a scanned type's base class or interface is in an
assembly that will not load (#2, #3); the
[release notes](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality/releases/tag/v2026.3.928)
have the detail. `2026.3.926` was planned for this fix but never tagged.

## Decisions

| Decision | Why |
|---|---|
| **Rule IDs are `BN` + the product's initials, with the rule number kept**: `BNAQ` here, `BNXQ` for XamlQuality, `BNCQ` for the CodeQuality analyzers | An analyzer's ID shares one flat namespace with every analyzer a project loads (`#pragma`, `NoWarn`, `.editorconfig`), and a two-letter prefix is the likeliest to collide. Four letters is unique enough and short enough to type; where a rule comes from is also shown by `helpLinkUri`, `Category` and the package id, so the ID need not spell it out |
| **A prefix never changes once one ID in it has shipped** | Every rename breaks consumers' suppressions. `AQ` → `BNAQ` is the one exception, taken while only `2026.3.922` was published and used only as a test dependency |
| **XamlQuality is asked to move to `BNXQ`** in its next release | One scheme across the family. Sent to the XamlQuality session on 2026-09-25; the rename is its to ship |

## Related

[Bennewitz.Ninja.CodeQuality](https://github.com/JanusMael/Bennewitz.Ninja.CodeQuality) holds the
source-side analyzers `BNCQ1001`, `BNCQ1002` and `BNCQ1004`, first published as `2026.3.925`. Its
decisions live in its own `PROGRESS.md`. Each `BNAQ` rule's README section links its counterpart, and
`RulesCatalogTests.EveryRule_HasAReadmeSectionOfItsOwn` keeps those anchors in place for CodeQuality's
links back.
