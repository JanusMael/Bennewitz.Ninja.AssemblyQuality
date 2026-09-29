# Progress

## Next release

Nothing unreleased since `v2026.3.929`. The version comes from the tag, set from the date when the
maintainer gives the go; see [docs/publishing.md](docs/publishing.md). One release per calendar day,
three-part `YYYY.Q.MMDD`: a fourth part is AutoVersioning's `HHmm` build stamp, not a patch counter.

## Next

**Friend grants across the family**, decided 2026-09-28. This repository's plan `00001` is complete
and released as `2026.3.929` (below). What remains is Templates':

1. **Templates' `00006`** (approved 2026-09-28) lands the policy: a generated
   `AssemblyInfo.InternalsVisibleTo.cs` listing every assembly in the solution, linked into every
   project by `Directory.Build.targets`; a hand-written `AssemblyInfo.InternalsVisibleTo.External.cs`
   for grants to other repositories; a per-project `SolutionFriendGrants=false` opt-out; and the
   conventions checks. Its step 5 has `bbavalonia` call `BNAQ1005`.
2. **This repository adopts `00006`** when it next syncs the conventions script. Its `Granting`,
   `FriendV1` and `FriendV2` fixtures then set `SolutionFriendGrants` to `false`, since their grants
   are wrong or withdrawn on purpose.
3. **A later Templates plan** sets the provider-side `BNAQ1006` convention: which repositories declare
   their published consumers, and where the release check lives. The wiring it builds on is in the
   README's `BNAQ1006` section.

| Decision | Choice |
|---|---|
| Split | Templates owns the policy; AssemblyQuality provides configurable checks, the `BNAQ1003` / `LayeringTests.Tiers` shape |
| Within a solution | Every project grants to every other, test projects included; grants between product projects are encouraged. `internal` then means solution-internal, so a member no other assembly may reach is `private` |
| Across repositories | Internals may be granted to the maintainer's other repositories wherever that reduces friction, declared in the External file |
| Signing | None. A grant is matched by assembly name, and on modern .NET public signing satisfies a keyed grant without the private key, so neither is a security boundary. Grants name only assemblies the family ships, and public consumers get none |
| Name collisions | Accepted and documented (2026-09-28). Family assembly names are unprefixed, so a public assembly that happens to share one would receive its grants; names such as `AppServices.Tests` make that unlikely |
| Version skew | An internal another repository uses is an informal promise: its owner does not change it without releasing the consumer. **`BNAQ1006` runs at the provider's release** (revised 2026-09-28), loading the already-published consumers beside the new build; in the consumer's own tests a removed internal is a compile error first, so the rule could almost never fail there |
| `BNAQ1005`'s allowed names | The solution's own assemblies plus the External file's names, supplied by the repository's test. No family registry for now |
| Existing repositories | Each moves over when it next syncs the Templates conventions. AppServices, FileServer and ScopedEditors document "grants to tests only" and rewrite those rules then |
| Order | Plans, then the Templates convention, then `BNAQ1005`, then `BNAQ1006` |

## Plan 00001 — friend-grant rules

[`plans/00001-friend-grant-rules.md`](plans/00001-friend-grant-rules.md), approved 2026-09-28.

| Step | State |
|---|---|
| 1. References resolve in the scanned assembly's own load context | **Done**, #10 |
| 2. Spike: `ResolveMember`, framework detection, provider-side wiring | **Done**, 2026-09-28; findings below |
| 3. `BNAQ1005` | **Done**, with its README section |
| 4. `BNAQ1006` | **Done**, with its README section and the provider-side wiring |
| 5. Docs | **Done**, with steps 3 and 4 |
| 6. Release | **Done**, `v2026.3.929` on 2026-09-29; Templates and AppServices told |

### Step 2 findings, for step 4's design

Measured with a throwaway provider and consumer in the scratchpad, not in this repository. The consumer
was compiled against provider v1 and loaded beside each variant in its own `AssemblyLoadContext`, and its
references were resolved and then **called**, so the runtime's verdict was the ground truth:

| Provider variant | `Module.ResolveMember` / `ResolveType` | Calling the consumer |
|---|---|---|
| Unchanged | every reference resolves; internals report as `internal` | runs |
| A member removed | `MissingMethodException` on that member only | the same `MissingMethodException` |
| A type removed | `TypeLoadException` on the type **and on every member reference through it** | the same `TypeLoadException` |
| The grant withdrawn | **every reference resolves**: resolution does not check access | `MethodAccessException` |

- **The resolver fails exactly as the runtime does**, so no signature is decoded by hand.
- **A removed type is one finding, not one per member.** Every member reference under it throws the
  same `TypeLoadException`; report the type once and skip its members.
- **A withdrawn grant is found by the rule itself**: a reference that resolves to a non-public member
  of an assembly whose `InternalsVisibleTo` no longer names the scanned assembly.
- **The shared framework** is recognised by location under the runtime's shared-frameworks folder,
  the parent of `RuntimeEnvironment.GetRuntimeDirectory()`, so ASP.NET Core and WindowsDesktop count
  as framework too, not only `Microsoft.NETCore.App`.
- **The provider-side wiring works.** A provider's test project references the provider's source and
  fetches each published consumer with `<PackageDownload Include="…" Version="[x.y.z]" />`, which
  downloads without joining the restore graph: its output held exactly one provider, the source build
  at `2.0.0.0`, with the consumer's own packaged dependency on `1.0.0` absent. A target copies the
  consumer's `lib/<tfm>/*.dll` from the packages folder, joining the path with `Path.Combine`, since
  `NuGetPackageRoot` has no trailing slash when `RestorePackagesPath` is overridden. Loaded beside the
  source build, the unchanged provider passed, the removed member failed, and the withdrawn grant
  failed.

## Last release

**`v2026.3.929`**, 2026-09-29: published to nuget.org and verified from the feed. The DLL in the
feed's package is byte-identical to the GitHub release asset, and a project restoring from nuget.org
alone gets it and lists all six rules. It adds `BNAQ1005` (#12) and `BNAQ1006` (#13), and fixes a
nested type in one that will not load (#6) and references resolving outside the scanned assembly's
load context (#10). No breaking change. The
[release notes](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality/releases/tag/v2026.3.929)
have the detail.

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
