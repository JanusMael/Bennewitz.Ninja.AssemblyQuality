# Progress

## Next release

**Planned: `v2026.3.926`**, tagged on the maintainer's go. The version comes from the tag; see
[docs/publishing.md](docs/publishing.md). One release per calendar day, three-part `YYYY.Q.MMDD`:
the maintainer declined a same-day `2026.3.925.1` on 2026-09-25, because in this family a fourth
part is AutoVersioning's `HHmm` build stamp, not a patch counter.

Unreleased since `v2026.3.925`:

| Commit or PR | Change |
|---|---|
| #2 | fix: a public type whose base class is in a missing assembly no longer takes the scan down. `GetExportedTypes()` throws `FileNotFoundException` there instead of `ReflectionTypeLoadException`, which escaped the skip handling in every type-reading rule; the loadable types are now examined and the rest named in `Skipped`. Found by ScopedEditors adopting `2026.3.925` (view models deriving from `CommunityToolkit.Mvvm`) |
| #3 | test: a type that only implements an interface from a missing assembly fails to load the same way, and is covered by the same fix |

**Once it is published**, tell ScopedEditors and AppServices: both have public types deriving from, or
implementing, types in other assemblies, and each takes the fix in its own change.

## Last release

**`v2026.3.925`**, 2026-09-25: published to nuget.org and verified from the feed. The DLL in the
feed's package is byte-identical to the GitHub release asset, and a project restoring from nuget.org
alone gets it and reports the `BNAQ` IDs. It carries the breaking `AQ` → `BNAQ` rename; the
[release notes](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality/releases/tag/v2026.3.925)
hold the old and new IDs and every behaviour change since `v2026.3.922`.

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
