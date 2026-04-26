# Template Usage

You just clicked **Use this template** on `contriwork-repo-template`. Follow these steps in order. Do NOT push a release tag until step 10 is green.

The placeholder tokens used throughout the template:

| Context | Placeholder | Replace with (example `config-core`) |
|---------|-------------|--------------------------------------|
| Python dist name | `contriwork-PACKAGE_NAME` | `contriwork-config-core` |
| Python import | `contriwork_PACKAGE_NAME` | `contriwork_config_core` |
| C# namespace / assembly | `Contriwork.PackageName` | `Contriwork.ConfigCore` |
| TypeScript symbol | `PackageName` | `ConfigCore` |
| npm package | `@contriwork/PACKAGE_NAME` | `@contriwork/config-core` |

---

## 1. Rename directories

```bash
git mv python/src/contriwork_PACKAGE_NAME python/src/contriwork_<your_name>
git mv csharp/src/Contriwork.PackageName csharp/src/Contriwork.<YourName>
git mv csharp/tests/Contriwork.PackageName.Tests csharp/tests/Contriwork.<YourName>.Tests
```

## 2. Global find-and-replace

Use your editor's project-wide replace (case-sensitive). Do the replacements in this order to avoid partial-match collisions:

1. `@contriwork/PACKAGE_NAME` → `@contriwork/<your-name>` (kebab-case, npm)
2. `contriwork-PACKAGE_NAME` → `contriwork-<your-name>` (kebab-case, PyPI)
3. `contriwork_PACKAGE_NAME` → `contriwork_<your_name>` (snake_case, Python import)
4. `Contriwork.PackageName` → `Contriwork.<YourName>` (PascalCase, C#)
5. `PackageName` → `<YourName>` (PascalCase, TypeScript symbols)
6. `PACKAGE_NAME` → `<your-name>` or `<your_name>` — context-dependent; verify by diff.

Rename C# solution file:

```bash
git mv csharp/Contriwork.PackageName.sln csharp/Contriwork.<YourName>.sln
```

## 3. Pin Dockerfile base image digests

Every `FROM` line carries a `@sha256:TODO` placeholder. Pin each to a current digest:

```bash
docker pull python:3.13-slim-trixie
docker inspect --format '{{index .RepoDigests 0}}' python:3.13-slim-trixie
# copy the @sha256:... suffix into python/Dockerfile
```

Repeat for:

- `python/Dockerfile` — build stage `python:3.13-slim-trixie`, runtime stage `python:3.13-slim-trixie`.
- `csharp/Dockerfile` — build stage `mcr.microsoft.com/dotnet/sdk:10.0`, runtime stage `mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled-extra`.
- `typescript/Dockerfile` — build stage `node:24-bookworm-slim`, runtime stage `node:24-alpine`.

## 4. Fill `CONTRACT.md`

The contract is the single source of truth. Complete every `TODO` block before writing implementation code. A PR that touches public behavior without updating `CONTRACT.md` is rejected by the PR checklist.

## 5. Fill the READMEs (four of them)

Each registry displays a **different** README from the package tarball; if any of them ships the repo-root README, consumers see cross-registry badges and sister-package references that read as noise on that registry. Keep the root README as the ecosystem landing page (GitHub repo view) and let every language directory carry its own.

- `README.md` (repo root) — ecosystem overview, all three registry badges, cross-language quick-tour. This is what shows up on GitHub.
- `python/README.md` — ships to PyPI (declared by `python/pyproject.toml`'s `readme` field).
- `csharp/README.md` — ships to NuGet (packed by `csharp/src/Contriwork.PackageName/Contriwork.PackageName.csproj` via `<None Include="..\..\README.md" Pack="true" PackagePath="\" />`).
- `typescript/README.md` — ships to npm (included by `typescript/package.json`'s `"files": ["dist", "README.md", "LICENSE"]`).

For each of the three per-registry READMEs, replace the `TODO` blocks:

- `## Install` — keep only the install command for that registry (`pip install ...` on PyPI's README, `dotnet add package ...` on NuGet's, `npm install ...` on npm's).
- `## Quick start` — one-line usage example in the matching language.
- Any cross-registry links (sister packages, root README, CONTRACT.md) must be **absolute GitHub URLs**, not relative paths — PyPI / NuGet / npm do not resolve `../` relative links.

For the repo-root `README.md`, replace the `## Why` and `## Quick start` blocks with ecosystem-level content (not language-specific). Badges auto-populate once step 2 is done.

## 6. Register PyPI Trusted Publisher

1. Go to <https://pypi.org/manage/account/publishing/> (sign in with a PyPI account that owns the package name — reserve the name first if needed).
2. Add a **pending publisher** with:
   - PyPI Project Name: `contriwork-<your-name>`
   - Owner: `contriwork`
   - Repository name: `contriwork-<your-name>`
   - Workflow: `release-python.yml`
   - Environment: `pypi`
3. First successful publish converts the pending publisher into a permanent one.

## 7. Register NuGet Trusted Publisher

1. Sign in to <https://www.nuget.org/account/trustedpublishing> as the NuGet
   account that owns (or will own) this package. The account name is
   **case-sensitive** and must match the `vars.NUGET_ACCOUNT` Actions
   variable exactly.
2. Click **+ Create** to open a new Trusted Publisher policy:
   - Publisher: **GitHub Actions**
   - Repository Owner: `contriwork`
   - Repository: `contriwork-<your-name>` — **do not use the wildcard `*`
     form**. NuGet's dormant grace-period mechanic (step 3) interacts badly
     with wildcard policies and produces silent 401 rejections that are
     difficult to diagnose later. Per-package policies isolate the failure
     mode.
   - Workflow File: `release-dotnet.yml`
   - Environment: `nuget`
3. Policy state after creation is a 7-day grace period labelled **"Use
   within N day(s)"**. The first successful publish inside this window
   converts the policy to permanent. If the window expires before a
   publish occurs, the policy goes **dormant** and returns a silent HTTP
   401 on the next OIDC exchange — even though the UI may still present
   it as "Active". The **Activate for 7 days** button re-opens the window
   but does not itself make the policy permanent; only a successful
   publish does. Fastest path to stability: publish within the window.
4. Expose the NuGet account name to the release workflow. The workflow
   (`release-dotnet.yml`) reads it from a repo-level Actions variable so
   different packages can be published under different NuGet accounts
   without forking the workflow:

       Repo Settings → Secrets and variables → Actions → Variables →
       New variable → NUGET_ACCOUNT = <exact nuget.org account name>

   The value is case-sensitive and must match step 1 character-for-
   character.
5. First tag push after the TP is created and `NUGET_ACCOUNT` is set
   should publish cleanly. If you see HTTP 401 despite a green OIDC
   token exchange, investigate in this order: (a) `NUGET_ACCOUNT`
   capitalization, (b) policy is not dormant (re-click "Activate for
   7 days"), (c) the account actually has ownership or push rights on
   the package name.

## 8. Register npm Trusted Publisher

1. Sign in to <https://www.npmjs.com/> as a member of the `contriwork` org.
2. Under **Packages → Publishing access**, add a Trusted Publisher for `@contriwork/<your-name>`:
   - Repository: `contriwork/contriwork-<your-name>`
   - Workflow: `release-npm.yml`
   - Environment: `npm`
3. Package must exist (publish a `0.0.0` placeholder first if needed) or be scoped-reserved by the org.

## 9. Enable branch ruleset

In **Settings → Rules → Rulesets** for this repo, apply the org default ruleset for `main`:

- Require signed commits.
- Require linear history.
- Require a pull request before merging (approvals: at least 1 for multi-dev projects; 0 is acceptable for a solo template-bootstrap phase but switch to 1 before onboarding contributors).
- **Allow merge methods: Squash ONLY.** Do NOT enable "Rebase merging" or "Merge commits". GitHub cannot produce a verified signature on rebased commits (server-side rebase changes the committer metadata), so `require signed commits` + rebase merge leaves the merged commits in an "Unverified" state that the ruleset itself then blocks. Squash merge works because GitHub signs the new single commit with its own web-flow identity, which counts as verified.
- Require status checks to pass. Modern GitHub Actions writes **only** to
  the Check Runs API (not the legacy Status API), and the Check Run name is
  the job's `name:` field alone — no `{workflow}/` prefix. A ruleset entry
  of `ci / python` therefore never matches and the ruleset hangs on
  "N of N required status checks are expected" forever. Use the plain
  Check Run names below; these are what both the exported ruleset JSON
  and the runtime output agree on:

  ```
  python
  csharp
  typescript
  contract
  gitleaks
  hadolint (Dockerfiles) (python/Dockerfile)
  hadolint (Dockerfiles) (csharp/Dockerfile)
  hadolint (Dockerfiles) (typescript/Dockerfile)
  trivy (filesystem)
  grype
  semgrep
  codeql (python, none)
  codeql (csharp, manual)
  codeql (javascript-typescript, none)
  deps (python / pip-audit)
  deps (dotnet list --vulnerable)
  deps (pnpm audit)
  SBOM (CycloneDX) (python)
  SBOM (CycloneDX) (csharp)
  SBOM (CycloneDX) (typescript)
  ```

  When you type these into the ruleset "Add checks" search box, GitHub's
  autocomplete displays them as `ci / python`, `security-scan / gitleaks`
  etc. for UI grouping — **ignore the prefix**; the stored context must be
  the short form. If autocomplete inserts the prefix anyway, edit the
  stored entry or `gh api --method PUT repos/:owner/:repo/rulesets/:id`
  with a JSON body that uses the short form.

- Block force pushes.
- Restrict deletions.
- **Bypass list empty.** No admin bypass — include administrators in the rule.

If the org ruleset is not yet configured, apply the same rules as a repo-level ruleset temporarily.

## 10. Verify locally

```bash
pre-commit install --install-hooks
pre-commit run --all-files
cd python && uv sync && uv run pytest && uv run ruff check && uv run mypy src && cd ..
cd csharp && dotnet restore && dotnet build && dotnet test && dotnet format --verify-no-changes && cd ..
cd typescript && pnpm install --frozen-lockfile && pnpm build && pnpm test && pnpm lint && pnpm typecheck && cd ..
hadolint python/Dockerfile csharp/Dockerfile typescript/Dockerfile
```

Every step green → proceed. Any red → fix before tagging.

## 11. Scaffold commit via PR

Your rename + find-replace + Dockerfile pins + `CONTRACT.md` + `README.md` edits are on the default branch locally, but have not been pushed. The ruleset blocks direct pushes to `main` (require PR + signed commits), so even the **first** scaffold commit goes through a PR.

```bash
# Create a branch for the scaffold edits
git switch -c scaffold/initial
git add -A
git commit -s -m "chore: scaffold from template (rename + digest pins)"
git push -u origin scaffold/initial

# Open the PR and watch checks
gh pr create --base main --head scaffold/initial --fill

# Wait for all 20 required checks to go green
gh pr checks --watch

# Squash-merge. Rebase is NOT allowed: GitHub cannot sign rebased commits,
# and the ruleset's "require signed commits" then blocks the merged commits.
# Squash produces a single new commit that GitHub web-flow signs as verified.
gh pr merge --squash --delete-branch

# Refresh local main to the merged state
git switch main
git fetch origin main
git reset --hard origin/main
```

If `gh pr merge --squash` fails because of branch-protection enforcement, either add your identity to the ruleset bypass list temporarily, or pass `--admin` (requires admin role on the repo) to bypass the pull-request-approval requirement while still going through the squash path.

## 12. Initial release

All three publish workflows gate on CI being green on the tagged commit. If any of PyPI / NuGet / npm publish fails, the GitHub Release is marked failed and consumers must not adopt that tag.

**Release is a two-step flow: merge the version bump via PR, then tag the merge commit.** Direct-push to `main` is blocked by the ruleset (`require a pull request before merging`) and tagging an unmerged branch commit would publish code that never landed on `main`.

```bash
# ---- on a release branch ----
git switch -c release/0.1.0

# bump VERSION and add a row to VERSION_MATRIX.md
echo "0.1.0" > VERSION
# edit CHANGELOG.md: move [Unreleased] to [0.1.0] with all three language sub-sections

git add VERSION VERSION_MATRIX.md CHANGELOG.md
git commit -s -m "chore(release): 0.1.0"
git push -u origin release/0.1.0

# ---- open a PR, wait for CI (all 20 checks green), SQUASH-MERGE via UI ----
# The squash merge produces a new commit on main that GitHub signs as
# "verified" using the web-flow identity. This is the commit we tag.

# ---- back on main, after the PR is merged ----
git switch main
git pull
git tag -s v0.1.0 -m "v0.1.0"
git push origin v0.1.0
```

Why this order: the tag MUST point at a commit that actually exists on `main`. If you tag the release branch's pre-merge commit, the three release workflows publish code that isn't on `main`, and any future CHANGELOG diff will disagree with what was published.

### If a publish step fails

- The tag stays in git, but the release is invalid. Do NOT retry the same tag — registries may reject a second attempt at the same version.
- Diagnose the root cause (check the Actions run logs; common issues: Trusted Publisher not registered, OIDC claim mismatch, package name collision, SBOM artifact upload timeout).
- Delete the remote tag, bump the patch (`0.1.1`), add a CHANGELOG note explaining the skipped version, and re-release via a new PR:

  ```bash
  git push --delete origin v0.1.0
  git tag -d v0.1.0

  git switch -c release/0.1.1
  echo "0.1.1" > VERSION
  # update CHANGELOG.md and VERSION_MATRIX.md to mark 0.1.0 as "failed — never published"
  git add VERSION CHANGELOG.md VERSION_MATRIX.md
  git commit -s -m "chore(release): skip 0.1.0, re-release as 0.1.1"
  git push -u origin release/0.1.1
  # open PR, wait for CI green, SQUASH-MERGE

  git switch main
  git pull
  git tag -s v0.1.1 -m "v0.1.1"
  git push origin v0.1.1
  ```

- Rolling back is **per-tag, not per-registry**. If one of the three succeeded and two failed, the one that succeeded is still on its registry — document it in `CHANGELOG.md` under the failed version and supersede it with the next tag.
