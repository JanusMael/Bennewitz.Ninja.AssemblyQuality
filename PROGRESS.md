# Progress

## Next release

Nothing unreleased since `v2026.3.925`. The version comes from the tag, set from the date when the
maintainer gives the go; see [docs/publishing.md](docs/publishing.md).

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
