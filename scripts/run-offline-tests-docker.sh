#!/usr/bin/env bash
# Run offline C# tests using the already available Docker engine on dev-worker.
# Never contacts Steam or modifies any Workshop item.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
command -v docker >/dev/null || { echo "Docker is not installed; STOP" >&2; exit 1; }
command -v git >/dev/null || { echo "Git is required" >&2; exit 1; }
test "$(git remote get-url origin)" = "https://github.com/wdani/SE2-Workshop-UGC-Fix.git" || {
  echo "Wrong git repository; STOP" >&2; exit 1
}
test "$(git branch --show-current)" = "feat/universal-metadata-recovery" || {
  echo "Expected review branch; STOP" >&2; exit 1
}
echo "=== Offline source-only static safety checks ==="
python3 legacy-gui-v0.3.0/tests/offline-safety-checks.py
python3 scripts/verify-recovery-safety.py
echo "=== .NET 9 tests in isolated Docker container ==="
docker run --rm \
  --user "$(id -u):$(id -g)" \
  --volume "$ROOT:/repo" \
  --workdir /repo \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -euc '
    dotnet --version
    dotnet run --project tests/RecoverySafetyTests/RecoverySafetyTests.csproj
    dotnet run --project legacy-gui-v0.3.0/tests/MetadataSafetyTests/MetadataSafetyTests.csproj
  '
echo "=== COMPLETED: local offline tests (no Steam calls) ==="
