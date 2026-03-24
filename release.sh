#!/usr/bin/env bash
set -euo pipefail

CSPROJ="src/Lavr.Configuration.Yaml/Lavr.Configuration.Yaml.csproj"

current_version=$(grep -oP '(?<=<VersionPrefix>)[^<]+' "$CSPROJ")
echo "Current version: $current_version"

if [ -z "${1:-}" ]; then
  echo "Usage: ./release.sh <version>"
  echo "Example: ./release.sh 0.1.0"
  exit 1
fi

new_version="$1"

if git rev-parse "v$new_version" >/dev/null 2>&1; then
  echo "Error: tag v$new_version already exists"
  exit 1
fi

sed -i.bak "s|<VersionPrefix>$current_version</VersionPrefix>|<VersionPrefix>$new_version</VersionPrefix>|" "$CSPROJ"
rm -f "$CSPROJ.bak"

git add "$CSPROJ"
git commit -m "v$new_version"
git tag "v$new_version"

echo ""
echo "Created commit and tag v$new_version"
echo "Run 'git push && git push --tags' to trigger the release"
