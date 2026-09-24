# AGENTS.md — `docs/`

Documents for humans. What an agent needs lives in the `AGENTS.md` files, not here.

| Document | What it is | Kept in step with |
|---|---|---|
| `publishing.md` | The release runbook: the `NUGET_USER` variable, the trusted-publishing policy, the credential preflight, choosing the version, tagging, and verifying against the feed rather than the workflow | `.github/workflows/release.yml`, `packages.push` and `packages.local`. A step renamed or reordered in the workflow is renamed or reordered here in the same change |

## Rules

| Rule | Why |
|---|---|
| The policy's Workflow File is `release.yml` exactly, and `publishing.md` says so | The OIDC token is bound to the workflow file name; a renamed file fails every login |
| `NUGET_USER` is described as a repository variable holding the nuget.org profile name | `release.yml` reads it from `vars.NUGET_USER`; as a secret its value is masked in the very log line that would show it was wrong |
