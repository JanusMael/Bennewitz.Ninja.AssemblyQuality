# AGENTS.md — `scripts/`

File-based C# apps, run with `dotnet run`. Each is compiled with this repository's
`Directory.Build.props`, so warnings are errors here too.

| Script | What it does | Run by |
|---|---|---|
| `assert-packages.cs` | Checks that the `.nupkg` files `dotnet pack` actually produced are exactly the ids `packages.push` and `packages.local` declare: none packed and undeclared, none declared and unpacked | CI's `pack` job, and `release.yml` before it logs in to publish |
| `repo-conventions.cs` | Checks, and applies, the family's repository conventions: the AI-facing documents, GitHub settings, and the rulesets for `main` and release tags, with what varies read from `.github/repository.json` | CI's `conventions` job (`check`), and the maintainer (`check --admin`, `apply`) |

## Rules

| Rule | Why |
|---|---|
| `assert-packages.cs` reads each id from the `.nuspec` inside the package, never from the file name | `<id>.<version>.nupkg` cannot be split reliably: nothing separates an id ending in `.Widget` from one ending in `.Widget.2026` |
| `assert-packages.cs` is run from the repository root | It reads `packages.push` and `packages.local` from the current directory |
| **`repo-conventions.cs` is never edited here** | It is a copy of `templates/bbpkg/scripts/repo-conventions.cs` in Bennewitz.Ninja.Templates, identical in every family repository. Change it there and copy it back; `check` run from that repository reports every copy that differs |
| A file-based app is compiled trimmed | Trim-unsafe calls fail the build. Build JSON with `System.Text.Json.Nodes`, and a `JsonArray` through its constructor, never a collection expression or `Add` (see `Baseline.Rulesets` in `repo-conventions.cs`) |
