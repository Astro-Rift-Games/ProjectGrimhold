---
name: grimhold-commit-message
description: "Trigger: commit message, commit title, título del commit, descripción del commit. Draft a Conventional Commit for the current Grimhold diff."
license: Apache-2.0
metadata:
  author: "Astro-Rift-Games"
  version: "1.1"
---

## Activation Contract

Load when the user asks for a commit title, commit description or commit message for the current working tree of Project Grimhold.

Do not load for PR descriptions, changelogs or commits in other repositories.

## Hard Rules

- Draft only. Do not stage, commit or push unless the user explicitly asks.
- Write the subject and body in English, whatever language the conversation uses.
- Use Conventional Commits: `type(scope): imperative summary`, subject <= 72 chars, no trailing period.
- Never add `Co-Authored-By` or any AI attribution.
- Describe only changes that belong to the task; never describe a file you did not verify in the diff.
- Report every excluded modified file explicitly instead of silently omitting it.

## Decision Gates

| Modified file | Action |
|---|---|
| Content diff belongs to the task | Include |
| `git diff --numstat` shows no lines changed (EOL-only rewrite, e.g. regenerated `.anim`) | Exclude; suggest `git checkout -- <path>` |
| Serialized asset changed outside task scope (e.g. loot table weights flushed by `AssetDatabase.SaveAssets`) | Exclude; warn "do not commit" |
| Unclear ownership | Ask one question, then stop |

| Change kind | Type |
|---|---|
| New behavior, content or configuration | `feat` |
| Bug fix | `fix` |
| Tests only | `test` |
| Docs only | `docs` |
| Restructure without behavior change | `refactor` |

| Player impact | Player note |
|---|---|
| New content or feature a player can see and use | `Player-Added` |
| Better feel, usability or flow of existing gameplay | `Player-Improved` |
| Bug a player could notice is gone | `Player-Fixed` |
| Tuned stats, costs, rates or difficulty | `Player-Balanced` |
| Refactor, tests, docs, tooling, architecture, internal networking, debug/dev-only tools, or work that is incomplete or hidden from players | None |
| Unclear whether a player can observe it | None; mention the omission so the user can add one |

The type never implies a note: a `feat` gets one only when a player can observe the result.

## Execution Steps

1. Run `git status --short`, `git diff --stat` and `git diff --numstat`; include untracked files.
2. Read the diff of every candidate file; classify each with the decision gates.
3. Run `git log --oneline -10` and reuse the existing scope vocabulary (e.g. `vfx`, `animation`).
4. Write the subject: what the change delivers, not how.
5. Write the body wrapped at 72 columns:
   - first paragraph: the result and why;
   - derived values, contracts or configuration that reviewers must verify;
   - compact list of added or generalized tests;
   - docs updated.
6. Add player notes with the player-impact gates, as the last body paragraph
   (footer), one trailer per observable change, with no blank line between
   them and other footers such as `Refs:`:
   `Player-Fixed: Take All and Leave All buttons in the Stash work again.`
   Write plain English for non-technical players: the observable result,
   no class, system or network terms, one sentence of at most 240 characters.
7. List excluded files with the reason.

## Output Contract

Return, in the user's language for prose and English for the message:

- **Title:** one fenced block with the subject.
- **Description:** one fenced block with the body, including any player-note footer.
- Whether player notes were added, or why none apply.
- Excluded files with reason and the suggested action.

## References

- `../../../AGENTS.md` — commit language and scope policy.
- `../../../Project Grimhold/AGENTS.md` — serialized asset and generated-file checks.
- `../../../tools/changelog/README.md` — player-note trailers and how they reach the changelogs.
