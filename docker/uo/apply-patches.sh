#!/usr/bin/env bash
# apply-patches.sh — applies all Shattered Legacy customizations to the stock ModernUO source
set -euo pipefail

PATCHES=/patches
CUSTOMIZATIONS=/customizations
REPO=/build/modernuo

cd "$REPO"

# Convert any CRLF patch files to LF (Windows git may add carriage returns)
sed -i 's/\r//' "$PATCHES"/*.patch 2>/dev/null || true

apply_patch() {
    local file="$1"
    local name
    name=$(basename "$file")
    if patch -p1 --forward --ignore-whitespace --batch < "$file"; then
        echo "[patches] Applied: $name"
    else
        echo "[patches] WARN: $name did not apply cleanly (may already be applied or conflict)"
    fi
}

echo "[patches] Installing additive customizations..."
find "$CUSTOMIZATIONS" -maxdepth 1 -type f -name '*.cs' \
    ! -name 'CharacterCreation.cs' \
    ! -name 'CraftContext.cs' \
    ! -name 'CraftItem.cs' \
    ! -name 'HammerOfHephaestus.cs' \
    ! -name 'Meditation.cs' \
    ! -name 'ResourceInfo.cs' \
    -exec cp '{}' 'Projects/UOContent/Misc/' \;
echo "[patches] Additive customizations installed."

echo "[patches] Applying full-file replacements..."
cp "$CUSTOMIZATIONS/CharacterCreation.cs" \
    "Projects/UOContent/Engines/Character Creation/CharacterCreation.cs"
cp "$CUSTOMIZATIONS/CraftItem.cs" \
    "Projects/UOContent/Engines/Craft/Core/CraftItem.cs"
cp "$CUSTOMIZATIONS/CraftContext.cs" \
    "Projects/UOContent/Engines/Craft/Core/CraftContext.cs"
cp "$CUSTOMIZATIONS/HammerOfHephaestus.cs" \
    "Projects/UOContent/Items/New Haven Quest Rewards/HammerOfHephaestus.cs"
cp "$CUSTOMIZATIONS/Meditation.cs" \
    "Projects/UOContent/Skills/Meditation.cs"
cp "$PATCHES/Mining.cs"           "Projects/UOContent/Engines/Harvest/Mining.cs"
cp "$PATCHES/BaseGuildmaster.cs"  "Projects/UOContent/Mobiles/Vendors/NPC/Guildmasters/BaseGuildmaster.cs"
cp "$PATCHES/BulkMaterialType.cs" "Projects/UOContent/Engines/Bulk Orders/BulkMaterialType.cs"
cp "$PATCHES/LargeSmithBOD.cs"   "Projects/UOContent/Engines/Bulk Orders/LargeSmithBOD.cs"
cp "$PATCHES/JacobsPickaxe.cs"   "Projects/UOContent/Items/New Haven Quest Rewards/JacobsPickaxe.cs"
cp "$CUSTOMIZATIONS/ResourceInfo.cs" "Projects/UOContent/Misc/ResourceInfo.cs"
echo "[patches] Full-file replacements done."

echo "[patches] Applying .patch files..."
apply_patch "$PATCHES/PlayerMobile-individual-stat-cap.patch"
apply_patch "$PATCHES/Healer-resurrection-policy.patch"
apply_patch "$PATCHES/HealerGuildmaster-resurrection.patch"
apply_patch "$PATCHES/Bandage-pet-mimic-targeting.patch"
apply_patch "$PATCHES/BaseWeapon-talisman-durability.patch"
apply_patch "$PATCHES/BulkMaterialType-PostValorite.patch"
apply_patch "$PATCHES/CraftGump-MakeXClear.patch"
apply_patch "$PATCHES/CraftGumpItem-MakeX.patch"
apply_patch "$PATCHES/CraftItem-HammerBODAutoFill.patch"
apply_patch "$PATCHES/SmithBOD-PostValorite.patch"

echo "[patches] Structural fixes..."
# Stock Lumberjacking must be partial for ClusterFLumberjackingExtension
sed -i 's/public class Lumberjacking/public partial class Lumberjacking/' \
    Projects/UOContent/Engines/Harvest/Lumberjacking.cs

echo "[patches] All patches applied successfully."
