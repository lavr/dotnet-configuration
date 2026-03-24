#!/usr/bin/env bash
set -euo pipefail

CSPROJ="src/Lavr.Configuration.Yaml/Lavr.Configuration.Yaml.csproj"

if [ -n "$(git status --porcelain)" ]; then
  echo "Error: working tree is not clean. Commit or stash your changes first."
  git status --short
  exit 1
fi

current_version=$(sed -n 's/.*<VersionPrefix>\([^<]*\)<\/VersionPrefix>.*/\1/p' "$CSPROJ")

IFS='.' read -r major minor patch <<< "$current_version"

patch_version="$major.$minor.$((patch + 1))"
minor_version="$major.$((minor + 1)).0"
major_version="$((major + 1)).0.0"

echo "Current version: $current_version"
echo ""
echo "1) patch  → $patch_version"
echo "2) minor  → $minor_version"
echo "3) major  → $major_version"
echo ""
read -rp "Select release type [1/2/3]: " choice

case "$choice" in
  1) new_version="$patch_version" ;;
  2) new_version="$minor_version" ;;
  3) new_version="$major_version" ;;
  *) echo "Invalid choice"; exit 1 ;;
esac

if git rev-parse "v$new_version" >/dev/null 2>&1; then
  echo "Error: tag v$new_version already exists"
  exit 1
fi

sed -i '' "s|<VersionPrefix>$current_version</VersionPrefix>|<VersionPrefix>$new_version</VersionPrefix>|" "$CSPROJ"

git add "$CSPROJ"
git commit -m "v$new_version"
git tag "v$new_version"

echo ""
echo "Created commit and tag v$new_version"
echo "Run 'git push && git push --tags' to trigger the release"
