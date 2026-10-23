#!/usr/bin/env bash
# Build an opt-in, UNRELEASED WPF preview without touching the validated WinForms release path.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
test "$(git remote get-url origin)" = "https://github.com/wdani/SE2-Workshop-UGC-Fix.git" || {
  echo "Wrong repo; STOP" >&2; exit 1;
}
test "$(git branch --show-current)" = "feat/wpf-modern-desktop" || {
  echo "Use the WPF feature branch; STOP" >&2; exit 1;
}
command -v docker >/dev/null || { echo "Docker unavailable; STOP" >&2; exit 1; }
command -v python3 >/dev/null || { echo "python3 unavailable; STOP" >&2; exit 1; }

echo "Source HEAD: $(git rev-parse HEAD)"
python3 legacy-gui-v0.3.0/tests/offline-safety-checks.py
python3 scripts/verify-recovery-safety.py
python3 scripts/verify-wpf-preview.py

PROJECT="src/SE2WorkshopUGCFix.Wpf/SE2WorkshopUGCFix.Wpf.csproj"
DEST="$ROOT/artifacts/wpf-user-preview"
mkdir -p "$ROOT/artifacts"
rm -rf "$DEST"
mkdir -p "$DEST"

docker run --rm \
  --user "$(id -u):$(id -g)" \
  --volume "$ROOT:/repo" \
  --workdir /repo \
  --security-opt no-new-privileges \
  --cap-drop ALL \
  -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/.nuget/packages \
  -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -euc '
    dotnet run --project tests/RecoverySafetyTests/RecoverySafetyTests.csproj
    dotnet run --project legacy-gui-v0.3.0/tests/MetadataSafetyTests/MetadataSafetyTests.csproj
    dotnet publish src/SE2WorkshopUGCFix.Wpf/SE2WorkshopUGCFix.Wpf.csproj \
      -c Release -f net9.0-windows -r win-x64 \
      -p:EnableWindowsTargeting=true -p:EnableFixtureLab=false \
      -p:SelfContained=true -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:UseAppHost=true -p:PublishReadyToRun=false \
      -p:DebugType=none -o /repo/artifacts/wpf-user-preview
  '

python3 - "$DEST" "$(git rev-parse HEAD)" <<'PY'
from pathlib import Path
import hashlib
import sys
import zipfile

dest = Path(sys.argv[1])
commit = sys.argv[2]
exe = dest / "SE2WorkshopUGCFix.Wpf.exe"
if not exe.is_file() or not exe.stat().st_size:
    raise SystemExit("WPF executable missing, compilation or publish incomplete.")
archive = dest.parent / "SE2-Workshop-UGC-Fix-0.6.0-preview.2-WPF-win-x64-LOCAL-TEST.zip"
smoke = (
    '@echo off\r\n'
    'cd /d "%~dp0"\r\n'
    'start /wait "" "%~dp0SE2WorkshopUGCFix.Wpf.exe" --smoke-ui\r\n'
    'if errorlevel 1 (\r\n'
    ' echo FAIL: offline WPF smoke.\r\n'
    ' if exist "%LOCALAPPDATA%\\SE2WorkshopUGCFix\\Diagnostics\\last-wpf-smoke.txt" (\r\n'
    '  type "%LOCALAPPDATA%\\SE2WorkshopUGCFix\\Diagnostics\\last-wpf-smoke.txt"\r\n'
    ' )\r\n'
    ' pause\r\n'
    ' exit /b 1\r\n'
    ')\r\n'
    'echo PASS: WPF windows initialized with safe action defaults.\r\n'
    'pause\r\n'
)
launcher = ('@echo off\r\n'
            'cd /d "%~dp0"\r\n'
            'start "" "%~dp0SE2WorkshopUGCFix.Wpf.exe"\r\n')
readme = (
    f"SE2 Workshop Recovery WPF 0.6.0-preview.2\r\nSource: {commit}\r\n"
    "UNRELEASED LOCAL UI TEST. DO NOT DISTRIBUTE.\r\n"
    "Run RUN-WPF-SMOKE.cmd first; then RUN-WPF-PREVIEW.cmd.\r\n"
    "No Steam fault injection. Test CHECK METADATA only on your own known healthy item.\r\n"
    "Recovery controls may WRITE to Steam only after explicit owner approval.\r\n"
    "The WinForms version remains the validated fallback.\r\n"
)
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for path in sorted(dest.rglob("*")):
        if not path.is_file() or path.suffix.lower() in (".pdb", ".xml"):
            continue
        if path.name == "SE2-Workshop-UGC-Fix.exe":
            continue  # No legacy executable bundled with new preview.
        relative = path.relative_to(dest)
        if path.is_symlink() or ".." in relative.parts:
            raise RuntimeError("Unexpected publish file path")
        z.write(path, str(relative).replace("\\", "/"))
    z.writestr("READ-ME-FIRST.txt", readme)
    z.writestr("RUN-WPF-SMOKE.cmd", smoke)
    z.writestr("RUN-WPF-PREVIEW.cmd", launcher)
with zipfile.ZipFile(archive) as z:
    names = set(z.namelist())
    if "SE2WorkshopUGCFix.Wpf.exe" not in names:
        raise RuntimeError("WPF executable missing from archive")
    if any("FIXTURE-LAB" in s.upper() or s == "SE2-Workshop-UGC-Fix.exe" for s in names):
        raise RuntimeError("Legacy lab or executable found in WPF package")
print("WPF ZIP:", archive)
print("SHA256:", hashlib.sha256(archive.read_bytes()).hexdigest())
print("UNRELEASED: Windows GUI smoke and visual acceptance are still required.")
PY
