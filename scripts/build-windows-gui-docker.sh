#!/usr/bin/env bash
# Cross-compile the Windows WinForms GUI with .NET 9 on dev-worker.
# Does not start Steam, launch the GUI, run an EXE, publish, or upload metadata.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
PROJECT="legacy-gui-v0.3.0/SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj"

command -v docker >/dev/null || { echo "Docker unavailable; STOP" >&2; exit 1; }
command -v git >/dev/null || { echo "Git unavailable; STOP" >&2; exit 1; }
test "$(git remote get-url origin)" = "https://github.com/wdani/SE2-Workshop-UGC-Fix.git" || {
  echo "Unexpected git remote; STOP" >&2; exit 1;
}
test "$(git branch --show-current)" = "feat/universal-metadata-recovery" || {
  echo "Use the feature branch for verification; STOP" >&2; exit 1;
}
test -f "$PROJECT" || { echo "Windows GUI project not found; STOP" >&2; exit 1; }

echo "=== Windows GUI cross-compilation only (no execution) ==="
echo "Repository HEAD: $(git rev-parse --short HEAD)"
echo "Project: $PROJECT"

docker run --rm \
  --user "$(id -u):$(id -g)" \
  --volume "$ROOT:/repo" \
  --workdir /repo \
  --security-opt no-new-privileges \
  --cap-drop ALL \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/.nuget/packages \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet build "$PROJECT" \
    --configuration Release \
    --framework net9.0-windows \
    -p:EnableWindowsTargeting=true \
    -p:RuntimeIdentifier=win-x64 \
    -p:SelfContained=false \
    -p:PublishSingleFile=false \
    -p:UseAppHost=false

echo "=== CROSS-COMPILE PASS: Windows GUI assemblies compiled ==="
echo "Important: This does not prove that WinForms works on Windows or that Steam can initialize."
