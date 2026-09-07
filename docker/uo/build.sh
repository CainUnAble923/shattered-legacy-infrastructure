#!/usr/bin/env bash
# build.sh — build the Shattered Legacy ModernUO image with both verification gates.
#
# THIS IS THE BUILD COMMAND FOR THIS REPO. Plain `docker build` is not, and neither is
# `docker compose build`: both produce an image whose regression tests have never run.
#
# Three steps, each gating the next:
#
#   1. docker build --target builder   — apply-patches.sh runs here. A patch that does
#                                        not apply fails the build (F2), and a test file
#                                        that does not compile fails it too, because
#                                        UOContent.Tests is in ModernUO.slnx.
#   2. dotnet test in a container      — the shard's own tests under server/tests/.
#                                        A failing test stops the build here.
#   3. docker build                    — the runtime image, only if 1 and 2 passed.
#
# Step 2 cannot live inside the Dockerfile. ModernUO's test fixtures all call
# NetState.Configure(), which needs io_uring, which Docker Desktop's default seccomp
# profile blocks. Relaxing it needs `--security-opt seccomp=unconfined`, a `docker run`
# flag with no `docker build` equivalent; the BuildKit alternative
# (`RUN --security=insecure`) needs an entitled builder that `docker compose build`
# cannot use, which would break the deploy path. docker-compose.yml already sets the
# same seccomp option for the same reason. See notes/s4-test-route.md for the evidence.
#
# Usage, from anywhere:
#     docker/uo/build.sh [-t IMAGE_TAG] [--tests-only]
#
# PowerShell 5.1 has no shell for this; run it from Git Bash, or run the three commands
# it prints by hand. notes/s4-test-route.md lists them.
set -euo pipefail

IMAGE_TAG=sl-modernuo:latest
BUILDER_TAG=sl-uo-builder:latest
TESTS_ONLY=0

# Every test the route carries declares this namespace; apply-patches.sh fails the build
# for one that does not, so this filter cannot silently select nothing.
TEST_NAMESPACE=ShatteredLegacy.Tests
TEST_PROJECT=Projects/UOContent.Tests/UOContent.Tests.csproj

while [ $# -gt 0 ]; do
    case "$1" in
        -t|--tag)     IMAGE_TAG="$2"; shift 2 ;;
        --tests-only) TESTS_ONLY=1; shift ;;
        -h|--help)    sed -n '2,30p' "$0"; exit 0 ;;
        *)            echo "build.sh: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$REPO_ROOT"

echo "== 1/3 building builder stage (patches gate) =============================="
docker build --target builder -t "$BUILDER_TAG" -f docker/uo/Dockerfile .

echo
echo "== 2/3 running $TEST_NAMESPACE tests (test gate) =========================="
# --network=none: these tests need no network and must not reach one.
# --security-opt seccomp=unconfined: io_uring, as above.
# --no-restore: step 1 already restored the whole solution.
#
# MSYS_NO_PATHCONV=1: Git Bash rewrites any argument that looks like a Unix absolute path into
# a Windows one before exec. Without it `-w /build/modernuo` reaches Docker as
# `C:/Program Files/Git/build/modernuo` and the daemon rejects it, failing the test gate with
# "the working directory ... is invalid" and reporting it as a test failure. AGENTS.md says to
# run this script from Git Bash, so the fix belongs here rather than in anyone's environment.
# The variable is inert outside MSYS. Found in S2; see notes/s2-fountain.md.
set +e
test_output=$(MSYS_NO_PATHCONV=1 docker run --rm --network=none --security-opt seccomp=unconfined \
    -w /build/modernuo "$BUILDER_TAG" \
    dotnet test "$TEST_PROJECT" -c Release --no-restore \
    --filter "FullyQualifiedName~$TEST_NAMESPACE" \
    --logger 'console;verbosity=normal' 2>&1)
test_status=$?
set -e
printf '%s\n' "$test_output"

if [ $test_status -ne 0 ]; then
    echo
    echo "BUILD FAILED: $TEST_NAMESPACE tests did not pass. No image was built."
    exit 1
fi

# A filter that matches nothing exits 0 on some SDK versions. A green run that ran no
# tests is exactly the "green while broken" failure F2 and F3 were spent removing, so
# assert a positive count rather than trusting the exit code.
passed=$(printf '%s\n' "$test_output" | sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' | tail -1)
if printf '%s\n' "$test_output" | grep -q 'No test matches the given testcase filter'; then
    echo
    echo "BUILD FAILED: the filter matched no tests. server/tests/ reached the build tree"
    echo "but nothing in it ran. No image was built."
    exit 1
fi
if [ -z "$passed" ] || [ "$passed" -eq 0 ]; then
    echo
    echo "BUILD FAILED: could not read a passing test count from the run. Treating that as"
    echo "a failure rather than a pass. No image was built."
    exit 1
fi
echo "[tests] $passed test(s) passed."

if [ "$TESTS_ONLY" -eq 1 ]; then
    echo
    echo "--tests-only: stopping before the runtime image."
    exit 0
fi

echo
echo "== 3/3 building runtime image $IMAGE_TAG ================================="
docker build -t "$IMAGE_TAG" -f docker/uo/Dockerfile .

echo
echo "Built $IMAGE_TAG with patches applied and $passed shard test(s) passing."
