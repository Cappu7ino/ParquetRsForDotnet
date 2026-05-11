# GitHub Migration Plan

This plan covers moving `ParquetRsForDotnet` from Microsoft's internal Azure DevOps repository to `https://github.com/Cappu7ino/ParquetRsForDotnet` as a public GitHub repository while preserving VS Code agentic coding workflows and enabling NuGet.org publishing.

## Goals

- Preserve full git history, gated by a full-history public-safety audit.
- Move day-to-day development and contributions to GitHub.
- Keep VS Code and GitHub Copilot agent workflows effective through repo-local guidance and GitHub MCP.
- Replace Azure DevOps-specific governance and automation with GitHub-native equivalents.
- Add GitHub Actions for validation, packaging, release creation, and NuGet.org publishing.
- Avoid changing public API semantics or expanding runtime platform support as part of the migration itself.

## Decisions

- Target repository: `https://github.com/Cappu7ino/ParquetRsForDotnet`.
- Ownership target: Cappu7ino personal GitHub account.
- Migration mode: full git history, only after public-safety audit passes.
- Publishing target: NuGet.org after migration.
- Agentic coding direction: keep repo-local agent instructions and replace Azure DevOps MCP usage with GitHub MCP.
- Initial supported native assets remain `win-x64` and `linux-x64`.
- Public license: MIT.
- Strong-name key handling: keep the existing key for the first public migration unless a later review changes that decision.
- First release workflow mode: dry-run/manual package validation only; NuGet publishing remains a later gated step.

## Recommended Tools

### GitHub MCP Server

Use GitHub's official hosted MCP server in VS Code:

```json
{
  "servers": {
    "github": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp/"
    }
  }
}
```

Prefer VS Code OAuth for authentication. If a personal access token is required, use VS Code input variables or environment variables. Do not commit tokens or secrets in `.vscode/mcp.json`.

The GitHub MCP server should cover repository context, issues, pull requests, Actions runs, code security signals, and release workflow assistance.

### Other VS Code MCP Servers

- Keep `microsoft-docs-mcp`; it remains useful for .NET, NuGet, SourceLink, GitHub Actions, and Microsoft documentation.
- Remove `ado-msdata`; Azure DevOps MCP is no longer part of the target workflow.
- Remove `powerbi-modeling-mcp` from the shared workspace config unless it is intentionally needed for unrelated work.
- Keep `azure-mcp` only if future workflows deploy to or inspect Azure resources. Otherwise disable or remove it for this repository.

### Audit And Migration Tools

- Use `gitleaks` or an equivalent scanner for working-tree and full-history secret detection.
- Use `git filter-repo` only if the full-history audit finds material that must not be published.
- Use GitHub CLI (`gh`) for repository, release, and workflow setup where convenient.
- Use GitHub Actions, Dependabot, CodeQL/default setup, secret scanning, and push protection where available.

## Migration Steps

### 1. Confirm Public Release Preconditions

- Confirm public OSS release approval.
- Confirm whether the `ParquetRsForDotnet` package ID is available on NuGet.org.
- Confirm NuGet package ownership under the personal account or a chosen future organization.
- Confirm that full Azure DevOps history can be made public after audit.
- Confirm that keeping the existing strong-name key remains acceptable before the first public package.

The current migration decision is to keep the existing strong-name key. Strong-name signing is not a security boundary, but the key is part of assembly identity and should be handled intentionally.

### 2. Audit Full Git History

Run a full-history scan before pushing anything public. This is separate from a working-tree scan.

Example audit commands:

```powershell
gitleaks detect --source . --no-git
gitleaks detect --source .
```

Also search history for internal-only material such as Azure DevOps URLs, aliases, service IDs, private documentation references, credentials, generated artifacts, and large binary files.

If the audit finds public-blocking content, stop and clean the history with `git filter-repo` before publication. Do not rely on deleting files after making the repository public.

### 3. Clean Repository Metadata

Remove or replace Azure DevOps and Microsoft-internal files:

- Delete [../.config/tsaoptions.json](../.config/tsaoptions.json).
- Delete [../es-metadata.yml](../es-metadata.yml).
- Replace [../owners.txt](../owners.txt) with `.github/CODEOWNERS`.
- Replace [../azurepipelines-coverage.yml](../azurepipelines-coverage.yml) with GitHub coverage workflow configuration or delete it after GitHub CI exists.

Update [../src/ParquetRsForDotnet.csproj](../src/ParquetRsForDotnet.csproj):

- Change `Authors` from `Azure Data Movement` to `Cappu7ino` or the chosen project identity.
- Replace `Microsoft.SourceLink.AzureRepos.Git` with `Microsoft.SourceLink.GitHub`.
- Add repository metadata, package project URL, MIT license metadata, and any package icon/readme metadata needed for NuGet.org.

If the strong-name key is rotated, update `InternalsVisibleTo` public keys in [../src/AssemblyInfo.cs](../src/AssemblyInfo.cs).

### 4. Preserve Agent Guidance

Keep these files as public contributor and agent guidance:

- [../AGENTS.md](../AGENTS.md)
- [agentic-coding-guide.md](agentic-coding-guide.md)
- [ai/bootstrap.md](ai/bootstrap.md)
- [../api/public-api.md](../api/public-api.md)
- [../api/semantic-index.json](../api/semantic-index.json)

These files are useful for VS Code agentic coding, code review context, and package consumers. They should remain part of the repository and the package where already configured.

### 5. Update VS Code MCP Configuration

Update [../.vscode/mcp.json](../.vscode/mcp.json):

- Remove the `ado-msdata` server.
- Add the GitHub hosted MCP server.
- Keep `microsoft-docs-mcp`.
- Remove `powerbi-modeling-mcp` unless it is intentionally needed.
- Keep or remove `azure-mcp` based on whether Azure resource operations are in scope.

Do not commit any GitHub token. Use OAuth, VS Code input variables, or environment variables.

### 6. Create And Populate GitHub Repository

Create `Cappu7ino/ParquetRsForDotnet` as a private repository first. Do not let GitHub generate a README, license, or `.gitignore`, because the migrated repository already has content and history.

After the audit passes:

```powershell
git remote add github git@github.com:Cappu7ino/ParquetRsForDotnet.git
git push github --all
git push github --tags
```

Keep the repository private until cleanup commits and GitHub Actions validation pass.

If the current default branch is `master`, rename it to `main` and update any branch references in docs and workflows.

### 7. Configure GitHub Repository Settings

- Enable Dependabot alerts and Dependabot security updates.
- Enable secret scanning and push protection where available.
- Enable CodeQL/default setup where supported.
- Add branch protection or repository rulesets for `main`.
- Require pull request review and required status checks.
- Block force pushes to protected branches.
- Add repository topics: `parquet`, `arrow`, `dotnet`, `rust`, `native-interop`, `nuget`.
- Create a `nuget-release` environment with required approval and a NuGet API key secret.

### 8. Add GitHub Actions CI

Add `.github/workflows/ci.yml` for pull requests and pushes to `main`.

The workflow should validate:

- Rust native tests from `native/` with `cargo test`.
- Managed tests for `net8.0`.
- Managed tests for `net472` on Windows to exercise the .NET Framework consumer path.
- Example project builds.
- Benchmark project compile checks.

Validation commands to preserve:

```powershell
dotnet test tests/ParquetRsForDotnet.Tests.csproj --no-restore
dotnet test tests/ParquetRsForDotnet.Tests.csproj -f net472 --no-restore
cargo test
dotnet build benchmarks/ParquetRsForDotnet.Benchmarks.csproj
```

Run `cargo test` from `native/`.

Add coverage reporting in GitHub Actions to replace the intent of [../azurepipelines-coverage.yml](../azurepipelines-coverage.yml). The current differential target is 50%.

### 9. Add Release And NuGet Publishing Workflow

Add `.github/workflows/release-dry-run.yml` with a manual `workflow_dispatch` path and pull request validation for packaging-sensitive files.

The dry-run release workflow should:

- Build the Windows native asset.
- Build the Linux native asset using the documented Zig and `cargo-zigbuild` path.
- Run `dotnet pack src/ParquetRsForDotnet.csproj -c Release -p:CrossBuildLinux=true`.
- Validate package contents before publishing.
- Upload `.nupkg` and `.snupkg` as GitHub Actions artifacts.
- Avoid pushing to NuGet.org until the repository, secrets, and release process are reviewed.

Expected package contents are documented in [how-to/package-and-native-assets.md](how-to/package-and-native-assets.md) and include:

- `README.md`
- `api/public-api.md`
- `api/semantic-index.json`
- `docs/ai/bootstrap.md`
- `lib/net8.0/ParquetRsForDotnet.dll`
- `lib/netstandard2.0/ParquetRsForDotnet.dll`
- `runtimes/win-x64/native/parquet_rs_for_dotnet.dll`
- `runtimes/linux-x64/native/libparquet_rs_for_dotnet.so`

Add a separate protected NuGet publishing workflow later, or extend this workflow after the dry run is reviewed.

### 10. Add Public Repository Documentation

Add or update:

- `LICENSE`
- `SECURITY.md`
- `.github/CONTRIBUTING.md`
- `.github/CODEOWNERS`
- `.github/dependabot.yml`
- Issue templates
- Pull request template

After GitHub Actions exist, update README badges. Avoid exposing internal Azure DevOps details in public documentation.

### 11. Final Cutover

- Run local validation and GitHub Actions validation while the repo is private.
- Confirm release dry-run artifacts and package contents.
- Switch the repository visibility to public.
- Create the first public release tag.
- Publish to NuGet.org from a protected GitHub Actions workflow after the dry-run release is reviewed.
- Update the local `origin` remote so VS Code development targets GitHub:

```powershell
git remote set-url origin git@github.com:Cappu7ino/ParquetRsForDotnet.git
```

Use the HTTPS equivalent if SSH is not configured.

## Verification Checklist

- Full-history audit passes with no unresolved secrets or internal-only content.
- Working-tree audit confirms no remaining Azure DevOps TSA, Enterprise Services metadata, or Ownership Enforcer configuration.
- GitHub MCP starts in VS Code and can access repo, issue, pull request, Actions, and release context.
- GitHub Actions CI passes on the private repository.
- `dotnet test tests/ParquetRsForDotnet.Tests.csproj --no-restore` passes.
- `dotnet test tests/ParquetRsForDotnet.Tests.csproj -f net472 --no-restore` passes on Windows.
- `cargo test` passes from `native/`.
- `dotnet build benchmarks/ParquetRsForDotnet.Benchmarks.csproj` passes.
- Release dry run creates `.nupkg` and `.snupkg` files with expected managed and native assets.
- A fresh clone from GitHub can run the validation set.
- First public tag publishes a package to NuGet.org and creates a GitHub Release after the dry-run workflow is promoted to a gated publishing workflow.

## Open Risks

- Full git history may contain material that is safe internally but not safe publicly. This is the main migration gate.
- Keeping the existing strong-name key is an approved migration decision, but it should still be called out in release notes and reviewed before the first public package.
- The current native packaging path is Windows-oriented and uses Zig for Linux cross-build. That is acceptable for the first release workflow, but native Linux runner packaging may need later refinement.
- NuGet package ID ownership must be confirmed before final release automation is enabled.
