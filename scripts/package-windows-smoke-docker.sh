#!/usr/bin/env bash
# Package TWO distinct, UNRELEASED Windows previews from one source tree.
# USER-CANDIDATE: destructive fixture lab disabled at compile time, no launcher.
# DEV-LOCAL-TEST: fixture lab deliberately enabled for the owner's testing.
# Steam is never initialized by this Linux Docker packaging script.
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
DEV="$ROOT/artifacts/windows-dev-preview"
USER="$ROOT/artifacts/windows-user-preview"
mkdir -p "$ROOT/artifacts"
rm -rf "$DEV" "$USER"
mkdir -p "$DEV" "$USER"

publish_kind() {
  local enabled="$1" subdir="$2"
  echo "=== Windows win-x64 .NET9 publish (EnableFixtureLab=$enabled) ==="
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
      -c Release -f net9.0-windows -r win-x64 \
      -p:EnableWindowsTargeting=true \
      -p:SelfContained=true -p:PublishSingleFile=true \
      -p:UseAppHost=true -p:PublishReadyToRun=false \
      -p:DebugType=none -p:EnableFixtureLab="$enabled" \
      -o "/repo/artifacts/$subdir"
}

echo "Source commit: $(git rev-parse --short HEAD)"
publish_kind true windows-dev-preview
publish_kind false windows-user-preview

test -s "$DEV/SE2-Workshop-UGC-Fix.exe" || { echo "Developer exe missing" >&2; exit 1; }
test -s "$USER/SE2-Workshop-UGC-Fix.exe" || { echo "User exe missing" >&2; exit 1; }
# A matching binary would mean the compile-time lab exclusion failed.
if cmp -s "$DEV/SE2-Workshop-UGC-Fix.exe" "$USER/SE2-Workshop-UGC-Fix.exe"; then
  echo "SAFE-BUILD FAILURE: user and developer binaries are identical!" >&2
  exit 1
fi

python3 - "$DEV" "$USER" "$(git rev-parse HEAD)" <<'PY'
from pathlib import Path
import hashlib
import json
import sys
import zipfile

dev, user = [Path(x) for x in sys.argv[1:3]]
commit = sys.argv[3]
version = "0.5.0-preview.1"
archives = [
    (user, user.parent / f"SE2-Workshop-UGC-Fix-{version}-win-x64-USER-CANDIDATE.zip", False),
    (dev, dev.parent / f"SE2-Workshop-UGC-Fix-{version}-win-x64-DEV-LOCAL-TEST.zip", True),
]
smoke = (
    '@echo off\r\n'
    'cd /d "%~dp0"\r\n'
    'echo Offline UI smoke - Steam is NOT initialized.\r\n'
    'start /wait "" "%~dp0SE2-Workshop-UGC-Fix.exe" --smoke-ui\r\n'
    'if errorlevel 1 (echo FAIL: GUI smoke test) else (echo PASS: GUI smoke test)\r\n'
    'pause\r\n'
)
run = (
    '@echo off\r\n'
    'cd /d "%~dp0"\r\n'
    'echo UNRELEASED PREVIEW - verify Workshop ownership and keep original backups.\r\n'
    'start "" "%~dp0SE2-Workshop-UGC-Fix.exe"\r\n'
)
lab = (
    '@echo off\r\n'
    'cd /d "%~dp0"\r\n'
    'echo INTERNAL LAB - can damage disposable Workshop metadata! NEVER production.\r\n'
    'start "" "%~dp0SE2-Workshop-UGC-Fix.exe" --fixture-lab\r\n'
)
for folder, archive, internal in archives:
    mode = "DEV-LOCAL-TEST" if internal else "USER-CANDIDATE"
    readme = (
        f"SE2 Workshop UGC Fix | {version} | {mode}\r\n"
        f"Source commit: {commit}\r\n\r\n"
        "UNRELEASED: Windows UI and normal backup RESTORE must be checked on this exact build.\r\n"
        "1. Extract ZIP, run RUN-UI-SMOKE.cmd, then RUN-APP.cmd.\r\n"
        "2. Steam running; game and ModHUB closed for a Steam metadata query.\r\n"
        "3. Use CHECK METADATA before any action. Explicit owner consent for updates.\r\n"
        "4. Preserve private metadata backups; do not share them publicly.\r\n"
        "5. Healthy items must never offer unnecessary repair.\r\n"
        "6. Working-reference reconstruction is experimental / not broadly validated.\r\n"
        "This tool does not recover Workshop content or rewrite saved-world GUID references.\r\n"
        + ("INTERNAL LAB: destructive fixture test controls ARE ENABLED.\r\n"
           if internal else
           "USER CANDIDATE: destructive fixture lab is DISABLED at compile time.\r\n")
    )
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        files = sorted(x for x in folder.rglob("*") if x.is_file())
        if not files or len(files) > 100:
            raise RuntimeError("Unexpected Windows publish file count")
        for file in files:
            relative = file.relative_to(folder)
            if file.is_symlink() or ".." in relative.parts:
                raise RuntimeError("Unsafe packaged file path")
            if file.suffix.lower() in {".pdb", ".xml"}:
                continue
            z.write(file, arcname=str(relative).replace("\\", "/"))
        z.writestr("READ-ME-FIRST.txt", readme)
        z.writestr("RUN-UI-SMOKE.cmd", smoke)
        z.writestr("RUN-APP.cmd", run)
        if internal:
            z.writestr("RUN-FIXTURE-LAB.cmd", lab)
    with zipfile.ZipFile(archive) as z:
        names = set(z.namelist())
        if internal != ("RUN-FIXTURE-LAB.cmd" in names):
            raise RuntimeError("Fixture lab launcher isolation check failed")
    print(mode + ": " + str(archive))
    print("Size (bytes):", archive.stat().st_size)
    print("SHA256:", hashlib.sha256(archive.read_bytes()).hexdigest())
    print("Fixture lab launcher:", "INTERNAL ONLY" if internal else "EXCLUDED")
print("NOTICE: Neither ZIP is a public release. No Steam data was changed.")
PY
