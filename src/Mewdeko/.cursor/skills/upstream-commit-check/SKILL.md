---
name: upstream-commit-check
description: Review and port upstream git commits into this repo. Use when the user asks to check, grab, cherry-pick, or document upstream commits, easy drive-by commits, skipped commits, or catch-up commits.
---

# Upstream Commit Check

Use this workflow when the user provides upstream commit hashes and asks for easy grabs, drive-by commits, catch-up checks, or skipped-commit documentation.

## Workflow

1. Start from a clean status check:
   - `git status --short --branch`
   - Preserve unrelated user changes. Do not reset or restore files the user changed.

2. Inspect the batch:
   - `git show --stat --oneline --decorate --no-renames <hashes...>`
   - `git show -s --format="%h parents:%p%n%s%n" <hashes...>`
   - Check whether listed hashes are already contained in the current branch.

3. Dry-run each commit:
   - `git diff <hash>^ <hash> --binary | git apply --check --whitespace=nowarn`
   - For promising context drift, also try `git apply --check --3way --whitespace=nowarn`, but treat "with conflicts" as not clean.

4. Classify commits:
   - Clean, small, focused fixes can be cherry-picked.
   - Small conflicts can be manually ported only when the local intent is obvious.
   - Large features, schema-heavy changes, security-sensitive surfaces, broad mixed commits, or commits depending on skipped features should be skipped and documented.
   - Clean apply is not enough. Consider feature size, operational risk, migrations, auth/security impact, and whether the codebase has the required dependencies.

5. Apply easy changes:
   - Prefer preserving upstream commit messages for clean cherry-picks.
   - For partial/manual ports, use small local commit messages that describe the local change.
   - If a cherry-pick conflicts because it includes skipped prerequisite work, keep the local branch behavior and apply only the intended hunk.

6. Document skipped or partial commits in `docs/Skipped-Upstream-Commits.md`:
   - Add a dated section for the batch.
   - List all reviewed hashes.
   - List what was already present and what was taken.
   - For each skipped or partial commit, include:
     - upstream summary
     - why it was skipped
     - things to account for before revisiting

7. Validate:
   - Run `ReadLints` on touched C# files after edits.
   - Run `dotnet build src/Mewdeko/Mewdeko.csproj -p:OutDir=bin/tmp-agent-build/ -p:UseAppHost=false`.
   - Remove `src/Mewdeko/bin/tmp-agent-build` after validation.
   - Existing XML-doc warnings are common; call out new errors or warnings that appear tied to the change.

8. Finish with status:
   - Commit documentation updates unless the user explicitly asked not to commit.
   - Report commits created, skipped categories, validation result, and `git status --short --branch`.

## Local Heuristics

- Be conservative with migrations. Check `Database/Scripts` numbering and migration-lane rules before taking schema changes.
- Do not bring in new dashboard/API management surfaces as drive-bys when they affect auth, Docker/host control, destructive moderation, imports, data retention, or guild-leave behavior.
- For commits with missing files from earlier skipped features, document the dependency chain instead of creating stubs.
- For partial ports, explicitly document the remaining upstream hunks if they may be revisited.
