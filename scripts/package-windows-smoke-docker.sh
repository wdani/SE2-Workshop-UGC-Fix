#!/usr/bin/env bash
# Build a PRIVATE, unshipped developer test ZIP. No Steam use, no release upload.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
command -v docker >/dev/null || { echo "Docker is required" >&2; exit 1; }
test "$(git remote get-url origin)" = "https://github.com/wdani/SE2-Workshop-UGC-Fix.git" || {
  echo "Unexpected remote; STOP" >&2; exit 1;
}
test "$(git branch --show-current)" = "feat/universal-metadata-recovery" || {
  echo "Unexpected branch; STOP" >&2; exit 1;
}
PROJECT="legacy-gui-v0.3.0/SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj"
OUTPUT="$ROOT/artifacts/windows-gui-smoke-publish"
ARCHIVE="$ROOT/artifacts/SE2-Workshop-UGC-Fix-0.4.0-preview.5-win-x64-LOCAL-TEST.zip"
mkdir -p "$ROOT/artifacts"
rm -rf "$OUTPUT"
mkdir -p "$OUTPUT"

echo "=== Package Windows self-contained x64 GUI (OFFLINE TEST ONLY) ==="
echo "Source commit: $(git rev-parse --short HEAD)"
docker run --rm \
  --user "$(id -u):$(id -g)" \
  --volume "$ROOT:/repo" \
  --workdir /repo \
  --security-opt no-new-privileges \
  --cap-drop ALL \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/.nuget/packages \
  -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet publish "$PROJECT" \
    -c Release \
    -f net9.0-windows \
    -r win-x64 \
    -p:EnableWindowsTargeting=true \
    -p:SelfContained=true \
    -p:PublishSingleFile=true \
    -p:UseAppHost=true \
    -p:PublishReadyToRun=false \
    -p:DebugType=none \
    -o /repo/artifacts/windows-gui-smoke-publish

test -s "$OUTPUT/SE2-Workshop-UGC-Fix.exe" || {
  echo "Windows executable missing after publish; STOP" >&2; exit 1;
}

python3 - "$OUTPUT" "$ARCHIVE" "$(git rev-parse HEAD)" <<'PY'
from pathlib import Path
import hashlib
import sys
import zipfile

directory = Path(sys.argv[1])
archive = Path(sys.argv[2])
commit = sys.argv[3]
readme = (
    "SE2 Workshop UGC Fix — Windows LOCAL DEVELOPER TEST, v0.4.0-preview.5\r\n"
    "Source Git commit: " + commit + "\r\n\r\n"
    "1. Extract all files.\r\n"
    "2. Run RUN-UI-SMOKE.cmd to check that Windows Forms can initialize.\r\n"
    "   Smoke mode does NOT initialize Steam or edit any Workshop item.\r\n"
    "3. If smoke passes, RUN-APP.cmd opens the test GUI.\r\n"
    "   With Steam running and SE2/ModHUB closed, use CHECK METADATA on an\r\n"
    "   item you own. Recovery of missing or corrupt metadata is PREVIEW ONLY.\r\n"
    "   DO NOT PRESS REPAIR on any production item while testing.\r\n\r\n"
    "This is an UNRELEASED test package, not a public stable release.\r\n"
    "Share no private metadata, Steam credentials or full backup JSON.\r\n"
    "A green UI smoke does NOT confirm Steam API integration or live recovery.\r\n"
)
smoke = (
    "@echo off\r\n"
    "setlocal\r\n"
    "cd /d \"%~dp0\"\r\n"
    "echo Running OFFLINE UI startup check. Steam will NOT be initialized.\r\n"
    "start /wait \"\" \"%~dp0SE2-Workshop-UGC-Fix.exe\" --smoke-ui\r\n"
    "if errorlevel 1 (\r\n"
    "  echo FAIL: Windows UI smoke check did not pass.\r\n"
    ") else (\r\n"
    "  echo PASS: Windows GUI forms initialized and safe controls verified.\r\n"
    ")\r\n"
    "pause\r\n"
)
launch = (
    "@echo off\r\n"
    "setlocal\r\n"
    "cd /d \"%~dp0\"\r\n"
    "echo DEVELOPMENT BUILD. Missing-data recovery is READ-ONLY PREVIEW.\r\n"
    "echo No production Workshop edits should be made in this test.\r\n"
    "start \"\" \"%~dp0SE2-Workshop-UGC-Fix.exe\"\r\n"
)
with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    all_files = sorted(x for x in directory.rglob("*") if x.is_file())
    if not all_files or len(all_files) > 100:
        raise RuntimeError("Unexpected publish file count")
    for file in all_files:
        relative = file.relative_to(directory)
        if file.is_symlink() or ".." in relative.parts:
            raise RuntimeError("Unexpected publish file")
        if file.suffix.lower() in {".pdb", ".xml"}:
            continue
        z.write(file, arcname=str(relative).replace("\\", "/"))
    z.writestr("READ-ME-FIRST.txt", readme)
    z.writestr("RUN-UI-SMOKE.cmd", smoke)
    z.writestr("RUN-APP.cmd", launch)
    z.writestr("RUN-FIXTURE-LAB.cmd",
        '@echo off\r\n'
        'setlocal\r\n'
        'cd /d "%~dp0"\r\n'
        'echo DEVELOPMENT FIXTURE LAB: may create ONE new Workshop item after consent.\r\n'
        'echo Visibility is NOT guaranteed private. Do not edit existing mods.\r\n'
        'start "" "%~dp0SE2-Workshop-UGC-Fix.exe" --fixture-lab\r\n'
    )

digest = hashlib.sha256(archive.read_bytes()).hexdigest()
print("Local test archive:", archive)
print("Size (bytes):", archive.stat().st_size)
print("SHA256:", digest)
print("NOTICE: This archive is UNRELEASED, remains only on dev-worker, and has not run on Windows.")
PY
