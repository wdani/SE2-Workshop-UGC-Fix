#!/usr/bin/env bash
# Integrity-checked import of v0.3.0 GUI SOURCE into the draft branch.
# Never publishes a release, runs Steam or alters main.
set -euo pipefail
if (( $# != 1 )); then
  echo "Usage: $0 /path/to/SE2-Workshop-UGC-Fix-GUI-SOURCE-v0.3.0.zip" >&2
  exit 2
fi
ZIP="$(realpath "$1")"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
test -f "$ZIP" || { echo "Missing ZIP: $ZIP" >&2; exit 1; }
EXPECTED_SHA256="9331981bd4bd34e6c78771fd444b0fefcda0c59b52fa7e43f3d27349135f951d"
ACTUAL_SHA256="$(sha256sum "$ZIP" | cut -d ' ' -f 1)"
test "$ACTUAL_SHA256" = "$EXPECTED_SHA256" || {
  echo "Source SHA256 mismatch. STOP" >&2; exit 1
}
test "$(git remote get-url origin)" = "https://github.com/wdani/SE2-Workshop-UGC-Fix.git" || {
  echo "Wrong Git remote: STOP" >&2; exit 1
}
test "$(git branch --show-current)" = "feat/universal-metadata-recovery" || {
  echo "Wrong Git branch: STOP" >&2; exit 1
}
test -z "$(git status --porcelain)" || {
  echo "Working tree has other changes: STOP" >&2; git status --short; exit 1
}
git fetch origin
git pull --ff-only origin feat/universal-metadata-recovery
DEST="$ROOT/legacy-gui-v0.3.0"
test ! -e "$DEST" || { echo "Legacy GUI already imported: STOP" >&2; exit 1; }

python3 - "$ZIP" "$DEST" <<'PY'
from pathlib import Path
import shutil, stat, sys, tempfile, zipfile
archive = Path(sys.argv[1])
dest = Path(sys.argv[2])
prefix = "se2-ugc-fix-gui/"
allowed = {".cs", ".csproj", ".md", ".cmd", ".txt", ".py"}
stage = Path(tempfile.mkdtemp(prefix=".ugc-gui-import-", dir=dest.parent))
try:
    with zipfile.ZipFile(archive) as z:
        files = [i for i in z.infolist() if not i.is_dir()]
        if not 15 <= len(files) <= 40: raise RuntimeError("Unexpected file count")
        for info in files:
            if not info.filename.startswith(prefix) or info.file_size > 1_000_000:
                raise RuntimeError("Unexpected archive entry")
            rel = Path(info.filename[len(prefix):])
            if not rel.parts or rel.is_absolute() or ".." in rel.parts or rel.suffix.lower() not in allowed:
                raise RuntimeError("Unsafe/unexpected source path")
            if stat.S_ISLNK(info.external_attr >> 16):
                raise RuntimeError("Source archive contains a symlink")
            content = z.read(info)
            if len(content) != info.file_size:
                raise RuntimeError("Corrupt source archive")
            target = stage / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(content)
    for rel in [
        "BUILD-EXE.cmd",
        "SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj",
        "SE2WorkshopUGCFix/Program.cs",
        "SE2WorkshopUGCFix/MetadataPatch.cs",
        "SE2WorkshopUGCFix/DescriptionPreflight.cs",
        "tests/offline-safety-checks.py",
        "tests/MetadataSafetyTests/MetadataSafetyTests.csproj",
        "README-START-HERE-EN.md"
    ]:
        if not (stage / rel).is_file():
            raise RuntimeError("Missing required source file: " + rel)
    stage.rename(dest)
    print("Imported", len(files), "unchanged source files to", dest)
except Exception:
    shutil.rmtree(stage, ignore_errors=True)
    raise
PY

echo "=== Legacy GUI static safety checks (offline) ==="
python3 "$DEST/tests/offline-safety-checks.py"

if command -v dotnet >/dev/null 2>&1; then
  echo "=== .NET recovery tests ==="
  dotnet run --project tests/RecoverySafetyTests/RecoverySafetyTests.csproj
  echo "=== .NET GUI tests ==="
  dotnet run --project "$DEST/tests/MetadataSafetyTests/MetadataSafetyTests.csproj"
else
  echo "NOTICE: .NET SDK unavailable. C# build/tests still require Windows runner."
fi
test -z "$(git status --porcelain --untracked-files=no)" || {
  echo "Existing tracked files modified unexpectedly: STOP" >&2; exit 1
}
git add legacy-gui-v0.3.0
git commit -m "import(gui): preserve reviewed v0.3.0 source for recovery integration"
git push origin HEAD:feat/universal-metadata-recovery
echo "GUI source pushed to Draft PR #1. No Steam changes or release."
