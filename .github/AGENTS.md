# AGENTS.md — `.github/`

The workflows and the repository's GitHub settings.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request to `main`: `build` (build with warnings as errors, then test), `pack` (pack, then `scripts/assert-packages.cs`), and `conventions` (`scripts/repo-conventions.cs check`) |
| `workflows/release.yml` | One `publish` job. A `v*.*.*` tag, or a dispatch with `version` filled in, releases: the version comes from the tag or input, then build, test, pack with `-p:Version`, `assert-packages.cs`, `NuGet/login`, `dotnet nuget push --skip-duplicate`, and `gh release create`. Dispatched with `version` blank, it only logs in and writes the preflight summary |
| `repository.json` | What this repository's GitHub settings vary by: description, topics, `requiredChecks`, and `undocumented` exemptions. `scripts/repo-conventions.cs` applies and checks it |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md` for tools that look here |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **The CI job names `build`, `pack` and `conventions` are required checks**, listed under `requiredChecks` in `repository.json`. Renaming or removing one means updating that list and running `repo-conventions apply` in the same change | A required check that no job reports blocks every pull request, and GitHub never says why | `repo-conventions.cs check`, "required checks" |
| **`release.yml` keeps its file name** | The trusted-publishing policy on nuget.org names this file and the OIDC token is bound to it; renamed, every login fails. The preflight lives in this file for the same reason | `docs/publishing.md` |
| The push and the GitHub release name each package from `packages.push`, never a glob; nothing from `packages.local` appears in the workflow | A glob publishes whatever is in the folder, permanently | `PackagingTests.The_release_workflow_globs_nothing_and_publishes_what_is_declared`, `The_release_workflow_never_names_a_private_package` |
| Every publishing step stays gated on `RELEASING`, and the release refuses to run with `NUGET_USER` unset | A preflight must not publish; an unset user would skip the push yet still create a GitHub release | `release.yml`, step "Refuse to release without NUGET_USER"; `PackagingTests.The_release_workflow_has_a_credential_preflight`, `The_release_workflow_still_has_both_publishing_steps` |
| `NUGET_USER` is read from `vars`, not `secrets` | It is a public profile name, and masking it hides the value in the 401 that says it is wrong | `release.yml`, comment on the job `env` |
| `release.yml` holds `id-token: write` and `contents: write`, nothing broader | `id-token: write` is what replaces a stored API key | `release.yml`, `permissions` |
