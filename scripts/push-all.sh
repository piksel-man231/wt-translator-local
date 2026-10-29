#!/usr/bin/env bash
# Publish the same committed branch and tags to GitHub and GitLab.
set -euo pipefail
cd "$(dirname "$0")/.."
BRANCH="$(git symbolic-ref --quiet --short HEAD)"
if [[ -n "$(git status --porcelain)" ]]; then
  echo "Commit all source changes and rebuilt archives before publishing." >&2
  exit 1
fi
for REMOTE in origin gitlab; do
  git remote get-url "$REMOTE" >/dev/null
done
for REMOTE in origin gitlab; do
  git push "$REMOTE" "$BRANCH"
  git push "$REMOTE" --tags
  REMOTE_COMMIT="$(git ls-remote "$REMOTE" "refs/heads/$BRANCH" | cut -f1)"
  if [[ "$REMOTE_COMMIT" != "$(git rev-parse HEAD)" ]]; then
    echo "Verification failed for $REMOTE/$BRANCH." >&2
    exit 1
  fi
done
echo "Published $(git rev-parse --short HEAD) to GitHub and GitLab."
