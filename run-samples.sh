#!/usr/bin/env bash
# Runs the tester against every local sample page (original + mutated copies).
set -u
cd "$(dirname "$0")"
fail=0
for f in samples/*.html; do
  echo "=================== $f"
  dotnet run --project src -c Release -- --url "file://$PWD/$f" --headless "$@" || fail=1
done
exit $fail
