## Purpose

This file gives concise, repository-specific instructions for AI coding agents to be immediately productive here.

## Quick repository snapshot

- Current root contains only: `LICENSE` and a Git history (`.git`).
- There are no obvious source files, manifests, or CI configs to inspect yet.

## First-run checklist (must follow)
1. Confirm workspace contents and branches.
2. Search the repo tree for common language manifests (package.json, pyproject.toml, requirements.txt, Makefile, src/, tests/).
3. Inspect recent commits and remotes to find omitted source files or submodules.
4. If no source is present, ask the repository owner which language/framework to scaffold or whether there is a private submodule.

## Useful commands (PowerShell)
Run these in the workspace root to discover hidden context:

```powershell
git status --porcelain; git fetch --all --prune
git branch -a
git log --oneline -n 50
git ls-tree -r HEAD --name-only
git remote -v
```

If you find manifests, run the relevant quick checks:

```powershell
# Node.js
npm install; npm test

# Python
python -m pip install -r requirements.txt; python -m pytest -q

# Generic
make -n || echo "No Makefile"
```

## What to look for and why it matters
- package.json / package-lock.json / pnpm-lock.yaml: indicates Node projects and test/build scripts under `scripts`.
- pyproject.toml / requirements.txt / setup.cfg: indicates Python project layout and test runners.
- src/ and tests/: common source/test layout—prefer running tests from root.
- .github/workflows/: CI shows build/test commands and environment matrix.
- docs/, scripts/, infra/: may hold runbooks, ETL jobs, or deployment scripts.

## If you add or change files
- Keep changes minimal and scoped. Create or update a top-level README.md when adding the first substantive code.
- Add a minimal test (happy-path) alongside new logic (use project's test runner when available).

## Merge guidance for maintainers
- If `.github/copilot-instructions.md` already exists, merge preserving its original advice; append only repository-specific updates.

## Example repo-specific notes
- Current repo: no source files discovered. Refer to `LICENSE` at project root as the only authoritative file now. Before implementing features, confirm with the owner whether source is in another branch, submodule, or omitted from the push.

## Ask the owner
- If anything is missing (code, CI, or language), ask these two questions:
  1. Which language/framework should I scaffold or expect here?
  2. Is source located in a different branch or private submodule?

Please review these instructions and tell me which areas need more detail or concrete examples for this project.
