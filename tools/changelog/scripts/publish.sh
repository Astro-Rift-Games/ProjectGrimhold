#!/usr/bin/env bash
# Regenerates changelog outputs on top of the latest remote branch and pushes them
# only when they changed. Used by GitHub Actions; retries when the branch moved.
#
# Usage: publish.sh <branch> <commit-message> [generate flags...]
set -euo pipefail

branch="$1"
message="$2"
shift 2

outputs=(CHANGELOG_DEV.md CHANGELOG.md changelog-players.json docs/changelog)

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

for attempt in 1 2 3; do
  git fetch --quiet --tags origin "$branch"
  git checkout --quiet -B "$branch" "origin/$branch"

  node tools/changelog/src/cli.js generate "$@"

  if [ -z "$(git status --porcelain -- "${outputs[@]}")" ]; then
    echo "Changelogs already up to date on $branch."
    exit 0
  fi

  # Include generated developer archives; --only dev may precede player files.
  for output in "${outputs[@]}"; do
    if [ -e "$output" ]; then
      git add -- "$output"
    fi
  done
  git commit --quiet -m "$message"

  if git push --quiet origin "HEAD:$branch"; then
    echo "Pushed changelog update to $branch: $(git rev-parse --short HEAD)."
    exit 0
  fi

  echo "Push to $branch rejected (attempt $attempt); retrying on the latest remote state."
done

echo "::error::Could not push changelog update to $branch after 3 attempts."
exit 1
