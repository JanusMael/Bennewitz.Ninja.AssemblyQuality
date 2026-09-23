# Bennewitz.Ninja.AssemblyQuality

Static analysis rules for the shape a .NET assembly **ships** — read by reflection over the
compiled output, which is the thing a consumer actually binds against. A source analyser sees what
was written; this sees what was published.

The assembly-shaped companion to
[Bennewitz.Ninja.XamlQuality](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality): that one
reads markup, this one reads types. Every rule here came from a guard written inline in a real
repository, where it could assert "no offenders today" and never that it would find one.

## Install

```bash
dotnet add package Bennewitz.Ninja.AssemblyQuality
```

Run it where it belongs: in a test project, against the untrimmed build. The rules read by
reflection, so they are not trim-safe. `IAssemblyRule.Analyze` carries
`[RequiresUnreferencedCode]`, so calling it from an application with trimming or AOT enabled
raises a warning at the call site. Without that warning, a trimmed scan would complete and quietly
report less than it should.

## Releasing

See [docs/publishing.md](docs/publishing.md). The short version:

1. Add the `NUGET_USER` variable — your nuget.org **profile name**, not an email.
2. Create **one** trusted-publishing policy whose glob patterns cover every id in
   [`packages.push`](packages.push) and match nothing in [`packages.local`](packages.local).
3. Run **Release** → *Run workflow* with the version **blank**. That logs in and stops, proving the
   credentials without publishing.
4. Tag `vYYYY.Q.MMDD` and push.

⛔ **One policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to one matching policy — so a second policy is never consulted and its package is rejected `403`
after the first has already published permanently.

## Licence

MIT. See [LICENSE](LICENSE).

## Rules

<!-- BEGIN GENERATED RULES -->
| Id | Requires |
|---|---|
| `AQ1001` | No public method takes a CancellationToken with a default value. |
| `AQ1002` | No type from a leak-prone namespace appears in the public surface. |
| `AQ1003` | An assembly references none of the assemblies its layer forbids. |
| `AQ1004` | No declared namespace segment shadows the root namespace of a referenced assembly. |
<!-- END GENERATED RULES -->

The table above is rendered from the rule types — `Id` and `Summary` on each `IAssemblyRule` — and
a test fails if it drifts. Regenerate it with `AQ_UPDATE_DOCS=1 dotnet test AssemblyQuality.slnx`
and commit the result.

**Every rule reports; none of them assert.** A finding can fail a build, warn in CI or print in a
report — the severity is the consumer's to choose, and a library that threw would have chosen both
the runner and the severity for them.

**Assert on `Inspected`, not only on `Findings`.** A rule given no assemblies, or one configured to
forbid nothing, returns zero findings and reads exactly like a clean result. `Inspected` is what
tells "nothing is wrong" apart from "nothing was checked" — which is the failure every guard this
library replaced actually had.
