#!/usr/bin/env bash
# apply-patches.sh — applies all Shattered Legacy customizations to the stock ModernUO source
#
# This script is a verification gate, not a best-effort copier. If any patch does not
# apply, or any replacement lands somewhere unexpected, the build fails.
#
# It reports every problem it finds before exiting, so one build run diagnoses all of
# them rather than one per run.
set -euo pipefail

PATCHES=/patches
CUSTOMIZATIONS=/customizations
TESTS=/tests
REPO=/build/modernuo

cd "$REPO"

# Patch files deliberately not applied. An entry here is a known defect awaiting
# triage, not an endorsement. See F3 in shard-migration/docs/backlog.md.
# Empty since F3. Every patch file is applied; nothing is skipped. An entry here would
# be a known defect awaiting a decision, and must carry its reason.
SKIPPED_PATCHES=()

APPLIED_PATCHES=()
FAILED_PATCHES=()
HANDLED_PATCHES=$'\n'

fail() {
    echo "[patches] FAILED: $1"
    FAILED_PATCHES+=("$1")
}

# Convert any CRLF patch files to LF (Windows git may add carriage returns)
sed -i 's/\r$//' "$PATCHES"/*.patch

# Replace an upstream file with one of ours. The destination must already exist:
# if upstream moved or renamed it, a plain cp would silently create a new file that
# nothing compiles against, and the override would vanish with no error.
replace_file() {
    local src="$1" dest="$2"

    if [ ! -f "$src" ]; then
        fail "$(basename "$src") — source missing at $src"
        return
    fi
    if [ ! -f "$dest" ]; then
        fail "$(basename "$src") — destination $dest does not exist in pinned upstream"
        return
    fi
    cp "$src" "$dest"
}

# Mirror a tree of .cs files into the build tree, preserving each file's path relative
# to its source root. This is the ONE copy mechanism for structured sources. F2 built it
# for server/customizations subdirectories; server/tests is the same call with different
# roots. Anything else that needs carrying into the build tree calls this too — do not
# add a second copier.
#
#   $1 label       what to call this tree in the build log
#   $2 src_root    directory being mirrored
#   $3 dest_root   destination, relative to $REPO
#   $4 mindepth    find(1) mindepth. 2 leaves a tree's top-level files alone because they
#                  are routed elsewhere; 1 mirrors them too.
#   $5 require_ns  optional. Every mirrored file must declare this namespace.
#   $6 validator   optional. Name of a function called as `validator <src> <rel>` for each
#                  file. It returns non-zero to reject the file, having called fail() itself.
mirror_cs_tree() {
    local label="$1" src_root="$2" dest_root="$3" mindepth="$4" require_ns="${5:-}" validator="${6:-}"
    local count=0 src rel dest

    echo "[patches] Installing $label..."

    # A missing source tree is a broken build contract, not an empty mirror. Copying
    # nothing quietly is how a whole category of files stops reaching the build.
    if [ ! -d "$src_root" ]; then
        fail "$label — source tree $src_root does not exist"
        return
    fi

    while IFS= read -r -d "" src; do
        rel=${src#"$src_root"/}
        dest="$dest_root/$rel"

        # An existing destination means we would be replacing an upstream file without
        # saying so. Replacements are routed explicitly below; silent ones are how an
        # override gets lost. Parent directories are created as needed, since ported
        # content legitimately introduces directories ModernUO does not have.
        if [ -e "$dest" ]; then
            fail "$rel — would overwrite existing upstream file $dest; route it as an explicit replacement instead"
            continue
        fi

        # A test file the runner's filter never selects is a test that can be green while
        # broken, which is the F2/F3 defect wearing different clothes. Assert the
        # namespace docker/uo/build.sh filters on, here, where it fails the build.
        if [ -n "$require_ns" ] && ! grep -q "^namespace $require_ns" "$src"; then
            fail "$rel — must declare 'namespace $require_ns' or docker/uo/build.sh will never run it"
            continue
        fi

        if [ -n "$validator" ] && ! "$validator" "$src" "$rel"; then
            continue
        fi

        mkdir -p "$(dirname "$dest")"
        cp "$src" "$dest"
        count=$((count + 1))
    done < <(find "$src_root" -mindepth "$mindepth" -type f -name "*.cs" -print0)

    echo "[patches] $label installed: $count file(s)."

    # Non-.cs files are not installed. Report them so nothing in the source tree is
    # silently ignored: for customizations this was the stale PetMimic migration set,
    # which must not reach the build tree (backlog F4).
    while IFS= read -r -d "" other; do
        echo "[patches] NOTE: not installed (not a .cs file): ${other#"$src_root"/}"
    done < <(find "$src_root" -mindepth "$mindepth" -type f ! -name "*.cs" -print0)
}

# S8 test-route rules. Both of these were real defects in the S4 route that a consumer worked
# around inside its own fixture, which is the wrong permanent answer: a route that needs the
# same workaround in every consumer is not a route, and the workaround becomes the example the
# next author copies. The route now does both jobs centrally, so doing them by hand is a build
# failure rather than a code-review question. See shard-migration/notes/s8-test-route.md.
#
# Line comments are stripped before matching, so a test file may name and discuss either rule.
ROUTE_CLOCK_FILE="Route/ShardTestClock.cs"

validate_shard_test() {
    local src="$1" rel="$2" rc=0 code

    code=$(grep -v '^[[:space:]]*//' "$src" || true)

    # Q-029. Turning the wheel without moving Core.Now runs every callback against a frozen
    # clock, which is how S6's first eater test ended up measuring Mobile.HitsTimer instead.
    # ShardTestClock.cs itself is exempt: it owns the correct way, and one of its tests has to
    # call the wrong way to prove it is wrong.
    if [ "$rel" != "$ROUTE_CLOCK_FILE" ] && \
       printf '%s\n' "$code" | grep -qE 'Timer\.(Slice|Init)[[:space:]]*\('; then
        fail "$rel — drives the timer wheel directly. Use ShardTestClock.Arm() then ShardTestClock.Advance(); see server/tests/$ROUTE_CLOCK_FILE"
        rc=1
    fi

    # Q-026. A hand-registered speed row stops the constructor throwing and is wrong for every
    # creature the real table classifies as anything but Medium. The fixture patch loads the
    # real Data/npc-speeds.json for every test.
    if printf '%s\n' "$code" | grep -qE 'NPCSpeeds\.(RegisterSpeed|Configure)[[:space:]]*\('; then
        fail "$rel — registers NPC speeds by hand. The route loads Data/npc-speeds.json for every test via UOContentFixture-npc-speeds.patch; delete the workaround"
        rc=1
    fi

    return $rc
}

apply_patch() {
    local file="$1"
    local name output
    name=$(basename "$file")
    HANDLED_PATCHES+="$name"$'\n'

    if [ ! -f "$file" ]; then
        fail "$name — no such file in $PATCHES"
        return
    fi

    # Dry-run first. A patch that fails halfway would otherwise leave a partially
    # patched tree behind for every patch that follows, turning one broken patch
    # into a cascade of misleading failures. On failure we touch nothing.
    if output=$(patch -p1 --forward --ignore-whitespace --batch --dry-run < "$file" 2>&1); then
        patch -p1 --forward --ignore-whitespace --batch < "$file" >/dev/null
        echo "[patches] Applied: $name"
        APPLIED_PATCHES+=("$name")
    else
        fail "$name"
        printf '%s\n' "$output" | sed 's/^/[patches]     | /'
    fi
}

echo "[patches] Installing additive customizations..."
# -maxdepth 1 is deliberate: it keeps server/customizations subdirectories (notably
# the stale migrations/ set) out of the build tree. The name exclusions below are the
# files that are upstream replacements rather than additions; they are routed
# explicitly further down. RegenRates is among them as of F5: it previously reached
# Projects/UOContent/Misc/ through this additive copy and overwrote ModernUO's file of
# the same name by coincidence of directory, rather than by being routed.
find "$CUSTOMIZATIONS" -maxdepth 1 -type f -name '*.cs' \
    ! -name 'CharacterCreation.cs' \
    ! -name 'CraftContext.cs' \
    ! -name 'CraftItem.cs' \
    ! -name 'HammerOfHephaestus.cs' \
    ! -name 'Meditation.cs' \
    ! -name 'RegenRates.cs' \
    ! -name 'ResourceInfo.cs' \
    -exec cp '{}' 'Projects/UOContent/Misc/' \;
echo "[patches] Additive customizations installed."

# Structured customizations: anything in a subdirectory of server/customizations is
# mirrored into Projects/UOContent/<same relative path> instead of being flattened.
#
# Top-level .cs keeps its existing meaning ("additive into Misc/") so this is additive
# rather than a rewrite. Flattening cannot carry ServUO's structured content: Revamped
# Dungeons alone has 32 duplicate basenames across 137 files (four dungeons each with a
# Generate.cs), and a flat cp would silently overwrite all but the last.
# See shard-migration/docs/flat-namespace-problem.md.
mirror_cs_tree "structured customizations" "$CUSTOMIZATIONS" "Projects/UOContent" 2

# The shard's own regression tests. Same mirror, different roots: server/tests/<path>
# becomes Projects/UOContent.Tests/Tests/<path>, at any depth.
#
# mindepth is 1 rather than 2 because server/tests has no flat-into-Misc meaning for a
# top-level file — there is nothing else for one to mean — so top-level test files mirror
# straight into Tests/.
#
# No patch to UOContent.Tests.csproj is needed and none should be written: the project
# file carries no <Compile Include> items, so the SDK's default **/*.cs glob compiles
# anything under the project directory at any depth. Verified against pinned 7c9215d97.
# UOContent.Tests is already in ModernUO.slnx, so `dotnet publish ModernUO.slnx` compiles
# these files and a test that does not COMPILE already fails the docker build. A test that
# compiles and FAILS is caught by docker/uo/build.sh; see notes/s4-test-route.md for
# why that gate cannot live inside `docker build` on Docker Desktop.
#
# The validator is S8's addition: two things the route now does centrally, and which a test
# doing by hand is a build failure. See validate_shard_test above.
mirror_cs_tree "shard tests" "$TESTS" "Projects/UOContent.Tests/Tests" 1 "ShatteredLegacy.Tests" \
    validate_shard_test

echo "[patches] Applying full-file replacements..."
replace_file "$CUSTOMIZATIONS/CharacterCreation.cs" \
    "Projects/UOContent/Engines/Character Creation/CharacterCreation.cs"
replace_file "$CUSTOMIZATIONS/CraftItem.cs" \
    "Projects/UOContent/Engines/Craft/Core/CraftItem.cs"
replace_file "$CUSTOMIZATIONS/CraftContext.cs" \
    "Projects/UOContent/Engines/Craft/Core/CraftContext.cs"
replace_file "$CUSTOMIZATIONS/HammerOfHephaestus.cs" \
    "Projects/UOContent/Items/New Haven Quest Rewards/HammerOfHephaestus.cs"
replace_file "$CUSTOMIZATIONS/Meditation.cs" \
    "Projects/UOContent/Skills/Meditation.cs"
replace_file "$PATCHES/Mining.cs"           "Projects/UOContent/Engines/Harvest/Mining.cs"
replace_file "$PATCHES/BaseGuildmaster.cs"  "Projects/UOContent/Mobiles/Vendors/NPC/Guildmasters/BaseGuildmaster.cs"
replace_file "$PATCHES/BulkMaterialType.cs" "Projects/UOContent/Engines/Bulk Orders/BulkMaterialType.cs"
replace_file "$PATCHES/LargeSmithBOD.cs"    "Projects/UOContent/Engines/Bulk Orders/LargeSmithBOD.cs"
replace_file "$PATCHES/JacobsPickaxe.cs"    "Projects/UOContent/Items/New Haven Quest Rewards/JacobsPickaxe.cs"
replace_file "$CUSTOMIZATIONS/RegenRates.cs"   "Projects/UOContent/Misc/RegenRates.cs"
replace_file "$CUSTOMIZATIONS/ResourceInfo.cs" "Projects/UOContent/Misc/ResourceInfo.cs"
echo "[patches] Full-file replacements done."

echo "[patches] Applying .patch files..."
apply_patch "$PATCHES/PlayerMobile-individual-stat-cap.patch"
apply_patch "$PATCHES/Healer-resurrection-policy.patch"
apply_patch "$PATCHES/HealerGuildmaster-resurrection.patch"
apply_patch "$PATCHES/AOS-pet-mimic-attribute-aggregation.patch"
apply_patch "$PATCHES/Bandage-pet-mimic-targeting.patch"
apply_patch "$PATCHES/BaseWeapon-talisman-durability.patch"
apply_patch "$PATCHES/CraftGump-MakeXClear.patch"
apply_patch "$PATCHES/CraftGumpItem-MakeX.patch"
apply_patch "$PATCHES/SmallSmithBOD-PostValorite.patch"

# S1 armour set-bonus subsystem. All three are additive hooks; none rewrites upstream logic.
# See shard-migration/notes/s1-armour-sets.md for what a pinned-commit bump must reconcile.
apply_patch "$PATCHES/Mobile-set-item-resistance-hook.patch"
apply_patch "$PATCHES/AOS-set-attribute-aggregation.patch"
apply_patch "$PATCHES/BaseArmor-set-self-repair.patch"

# S2 Fountain of Fortune. One line, additive: PlayerMobile.Luck gains the fountain's temporary
# bonus, which is how ServUO's RealLuck reaches loot generation. See notes/s2-fountain.md.
apply_patch "$PATCHES/PlayerMobile-fountain-luck-bonus.patch"

# S6 Stygian Abyss damage eaters. One hook into AOS.Damage; the eater property is inert without
# it while still showing on tooltips. See notes/s6-absorption.md section 3.
apply_patch "$PATCHES/AOS-damage-eater-hook.patch"

# S8 test route. The ONLY patch in this repo that touches an upstream TEST project, and the only
# one with no effect on the shipped server: it adds the NPCSpeeds.Configure call that
# UOContentFixture omits, so a test can construct a BaseCreature at all. Argued in
# shard-migration/notes/s8-test-route.md section 2. If it ever stops applying, every creature
# test fails with KeyNotFoundException in the constructor — loud, and the intended failure.
apply_patch "$PATCHES/UOContentFixture-npc-speeds.patch"

# A .patch file that no apply_patch line above names would be dead weight applied to
# nothing, with no way to tell from the build log. Account for every file explicitly.
echo "[patches] Checking every patch file is accounted for..."
for skipped in "${SKIPPED_PATCHES[@]}"; do
    HANDLED_PATCHES+="$skipped"$'\n'
    echo "[patches] Skipped by name (recorded, awaiting triage): $skipped"
done
for file in "$PATCHES"/*.patch; do
    name=$(basename "$file")
    if ! printf '%s' "$HANDLED_PATCHES" | grep -Fxq "$name"; then
        fail "$name — present in $PATCHES but never applied or skipped by name"
    fi
done

echo "[patches] Structural fixes..."
# Stock Lumberjacking must be partial for ClusterFLumberjackingExtension.
# sed reports success when it matches nothing, so assert the result instead.
LUMBERJACKING=Projects/UOContent/Engines/Harvest/Lumberjacking.cs
sed -i 's/public class Lumberjacking/public partial class Lumberjacking/' "$LUMBERJACKING"
if ! grep -q 'public partial class Lumberjacking' "$LUMBERJACKING"; then
    fail "structural fix — 'public partial class Lumberjacking' not present in $LUMBERJACKING after sed"
fi

if [ ${#FAILED_PATCHES[@]} -gt 0 ]; then
    echo "[patches] ------------------------------------------------------------------"
    echo "[patches] ERROR: ${#FAILED_PATCHES[@]} item(s) did not apply. Failing the build."
    for name in "${FAILED_PATCHES[@]}"; do
        echo "[patches]   - $name"
    done
    echo "[patches] ------------------------------------------------------------------"
    exit 1
fi

echo "[patches] All ${#APPLIED_PATCHES[@]} patches applied successfully."
