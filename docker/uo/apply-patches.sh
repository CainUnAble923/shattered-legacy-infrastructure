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
echo "[patches] Installing structured customizations..."
structured_count=0
while IFS= read -r -d "" src; do
    rel=${src#"$CUSTOMIZATIONS"/}
    dest="Projects/UOContent/$rel"

    # An existing destination means we would be replacing an upstream file without
    # saying so. Replacements are routed explicitly below; silent ones are how an
    # override gets lost. Parent directories are created as needed, since ported
    # content legitimately introduces directories ModernUO does not have.
    if [ -e "$dest" ]; then
        fail "$rel — would overwrite existing upstream file $dest; route it as an explicit replacement instead"
        continue
    fi

    mkdir -p "$(dirname "$dest")"
    cp "$src" "$dest"
    structured_count=$((structured_count + 1))
done < <(find "$CUSTOMIZATIONS" -mindepth 2 -type f -name "*.cs" -print0)
echo "[patches] Structured customizations installed: $structured_count file(s)."

# Non-.cs files in subdirectories are not installed. Report them so that nothing in
# server/customizations is silently ignored: today this is the stale PetMimic migration
# set, which must not reach the build tree (backlog F4).
while IFS= read -r -d "" other; do
    echo "[patches] NOTE: not installed (not a .cs file): ${other#"$CUSTOMIZATIONS"/}"
done < <(find "$CUSTOMIZATIONS" -mindepth 2 -type f ! -name "*.cs" -print0)

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
