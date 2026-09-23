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
