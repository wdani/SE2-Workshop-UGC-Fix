#!/usr/bin/env bash
# Build an opt-in, UNRELEASED WPF preview without touching the validated WinForms release path.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
test "$(git remote get-url origin)" = "https://github.com/wdani/SE2-Workshop-UGC-Fix.git" || {
  echo "Wrong repo; STOP" >&2; exit 1;
}
test "$(git branch --show-current)" = "feat/wpf-net10-lts-qualification" || {
  echo "Use the WPF feature branch; STOP" >&2; exit 1;
}
command -v docker >/dev/null || { echo "Docker unavailable; STOP" >&2; exit 1; }
command -v python3 >/dev/null || { echo "python3 unavailable; STOP" >&2; exit 1; }

# A reproducible diagnostic candidate must correspond to the documented HEAD,
# not hidden modifications on the developer VM.
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Tracked source differs from HEAD. Commit or stash changes before packaging." >&2
  exit 1
fi
UNTRACKED_SOURCE="$(git ls-files --others --exclude-standard -- src/SE2WorkshopUGCFix.Wpf tests/SafeReportGuardTests scripts)"
if test -n "$UNTRACKED_SOURCE"; then
  echo "Untracked build/test source detected. STOP:" >&2
  echo "$UNTRACKED_SOURCE" >&2
  exit 1
fi

echo "Source HEAD: $(git rev-parse HEAD)"
python3 legacy-gui-v0.3.0/tests/offline-safety-checks.py
python3 scripts/verify-recovery-safety.py
python3 scripts/verify-wpf-preview.py

PROJECT="src/SE2WorkshopUGCFix.Wpf/SE2WorkshopUGCFix.Wpf.csproj"
DEST="$ROOT/artifacts/wpf-user-preview-net10"
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
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -euc '
    dotnet --info
    dotnet run --project tests/RecoverySafetyTests/RecoverySafetyTests.csproj
    dotnet run --project legacy-gui-v0.3.0/tests/MetadataSafetyTests/MetadataSafetyTests.csproj
    dotnet run --project tests/SafeReportGuardTests/SafeReportGuardTests.csproj -c Release
    dotnet publish src/SE2WorkshopUGCFix.Wpf/SE2WorkshopUGCFix.Wpf.csproj \
      -c Release -f net10.0-windows -r win-x64 \
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
# Ensure the self-contained publish cannot silently use the previous runtime.
deps = dest / "SE2WorkshopUGCFix.Wpf.deps.json"
if not deps.is_file():
    # PublishSingleFile may embed dependency manifests in the executable;
    # trust the project's framework/publish flags and native Windows smoke.
    print("NOTE: deps.json embedded in single-file publish; inspect bundle on Windows.")
else:
    if ".NETCoreApp,Version=v10.0" not in deps.read_text(encoding="utf-8"):
        raise RuntimeError("WPF publish dependencies do not target .NET 10")

archive = dest.parent / "SE2-Workshop-UGC-Fix-0.6.0-preview.10-WPF-win-x64-LOCAL-TEST.zip"
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
    f"SE2 Workshop Recovery WPF 0.6.0-preview.10\r\nSource: {commit}\r\n"
    "UNRELEASED LOCAL UI TEST. DO NOT DISTRIBUTE.\r\n"
    "SHA256-MANIFEST.txt contains per-file hashes. UNSIGNED: not a digital signature.\r\n"
    "Run RUN-WPF-SMOKE.cmd first; then RUN-WPF-PREVIEW.cmd.\r\n"
    "RUN-WPF-REVIEW-RECOVERY.cmd opens a LOCKED OFFLINE UI-only dialog.\r\n"
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
    z.writestr("RUN-WPF-REVIEW-RECOVERY.cmd",
        '@echo off\r\n'
        'cd /d "%~dp0"\r\n'
        'start "" "%~dp0SE2WorkshopUGCFix.Wpf.exe" --review-recovery-ui\r\n')
# Record the source revision and all package member hashes in the archive.
# This is an unsigned integrity manifest, NOT a code signature.
with zipfile.ZipFile(archive, "a") as z:
    members = z.namelist()
    if len(members) != len(set(members)):
        raise RuntimeError("Duplicate names in package")
    if "SHA256-MANIFEST.txt" in members:
        raise RuntimeError("Pre-existing build manifest in publish output")
    manifest = [
        f"SOURCE_COMMIT={commit}",
        "VERSION=0.6.0-preview.10",
        "ALGORITHM=SHA256",
        "UNSIGNED_TEST_ARTIFACT=true",
        "",
    ]
    manifest += [
        f"{hashlib.sha256(z.read(name)).hexdigest()}  {name}"
        for name in sorted(members)
    ]
    z.writestr("SHA256-MANIFEST.txt", "\n".join(manifest) + "\n")

with zipfile.ZipFile(archive) as z:
    names = z.namelist()
    if len(names) != len(set(names)):
        raise RuntimeError("Duplicate ZIP members")
    required = {
        "SE2WorkshopUGCFix.Wpf.exe",
        "RUN-WPF-SMOKE.cmd",
        "RUN-WPF-PREVIEW.cmd",
        "RUN-WPF-REVIEW-RECOVERY.cmd",
        "READ-ME-FIRST.txt",
        "SHA256-MANIFEST.txt",
    }
    if not required.issubset(names):
        raise RuntimeError("Required Windows launcher/manifest missing")
    if any("FIXTURE-LAB" in name.upper() or name == "SE2-Workshop-UGC-Fix.exe" for name in names):
        raise RuntimeError("Legacy fixture-lab or executable found in WPF package")
    for name in names:
        path = Path(name)
        if path.is_absolute() or ".." in path.parts or "\\\\" in name:
            raise RuntimeError("Unsafe ZIP member path")
        entry = z.getinfo(name)
        if (entry.external_attr >> 16) & 0o170000 == 0o120000:
            raise RuntimeError("Symbolic links forbidden in release ZIP")
        if not z.read(name):
            raise RuntimeError(f"Empty archive member: {name}")
    header, *entries = z.read("SHA256-MANIFEST.txt").decode("utf-8").splitlines()
    if header != f"SOURCE_COMMIT={commit}":
        raise RuntimeError("Wrong manifest source SHA")
    for line in entries:
        if "  " in line:
            digest, name = line.split("  ", 1)
            if name not in names or hashlib.sha256(z.read(name)).hexdigest() != digest:
                raise RuntimeError(f"Manifest hash mismatch: {name}")
print("PASS: ZIP member allowlist, manifest hashes and source SHA verified.")
print("WPF ZIP:", archive)
print("SHA256:", hashlib.sha256(archive.read_bytes()).hexdigest())
print("UNRELEASED: Windows GUI smoke and visual acceptance are still required.")
PY
