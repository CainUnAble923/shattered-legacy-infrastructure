#!/usr/bin/env bash
# apply-patches.sh - applies all Shattered Legacy customizations to the stock ModernUO source
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
        fail "$(basename "$src") - source missing at $src"
        return
    fi
    if [ ! -f "$dest" ]; then
        fail "$(basename "$src") - destination $dest does not exist in pinned upstream"
        return
    fi
    cp "$src" "$dest"
}

# Mirror a tree of .cs files into the build tree, preserving each file's path relative
# to its source root. This is the ONE copy mechanism for structured sources. F2 built it
# for server/customizations subdirectories; server/tests is the same call with different
# roots. Anything else that needs carrying into the build tree calls this too - do not
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
        fail "$label - source tree $src_root does not exist"
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
            fail "$rel - would overwrite existing upstream file $dest; route it as an explicit replacement instead"
            continue
        fi

        # A test file the runner's filter never selects is a test that can be green while
        # broken, which is the F2/F3 defect wearing different clothes. Assert the
        # namespace docker/uo/build.sh filters on, here, where it fails the build.
        if [ -n "$require_ns" ] && ! grep -q "^namespace $require_ns" "$src"; then
            fail "$rel - must declare 'namespace $require_ns' or docker/uo/build.sh will never run it"
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
        fail "$rel - drives the timer wheel directly. Use ShardTestClock.Arm() then ShardTestClock.Advance(); see server/tests/$ROUTE_CLOCK_FILE"
        rc=1
    fi

    # Q-026. A hand-registered speed row stops the constructor throwing and is wrong for every
    # creature the real table classifies as anything but Medium. The fixture patch loads the
    # real Data/npc-speeds.json for every test.
    if printf '%s\n' "$code" | grep -qE 'NPCSpeeds\.(RegisterSpeed|Configure)[[:space:]]*\('; then
        fail "$rel - registers NPC speeds by hand. The route loads Data/npc-speeds.json for every test via UOContentFixture-npc-speeds.patch; delete the workaround"
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
        fail "$name - no such file in $PATCHES"
        return
    fi

    # Dry-run first. A patch that fails halfway would otherwise leave a partially
    # patched tree behind for every patch that follows, turning one broken patch
    # into a cascade of misleading failures. On failure we touch nothing.
    #
    # --fuzz=0: GNU patch's default fuzz of 2 lets a hunk apply with up to two of its
    # context lines at each edge NOT matching, and reports it only on stdout, which the
    # success branch below discards. The CC6 follow-up's red proof mangled a context line
    # of a new patch and the build stayed green (shard-migration/notes/
    # cc6-followup-breath-incubator.md section 6). A patch whose context has drifted is
    # exactly what this gate exists to stop, so context must match exactly.
    if output=$(patch -p1 --forward --ignore-whitespace --fuzz=0 --batch --dry-run < "$file" 2>&1); then
        patch -p1 --forward --ignore-whitespace --fuzz=0 --batch < "$file" >/dev/null
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
# top-level file - there is nothing else for one to mean - so top-level test files mirror
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
# cc-P38. CraftItem.cs and CraftContext.cs were full-file replacements until the bump to d4531cd9. A replacement hides
# upstream drift completely: that bump changed both upstream files (+636/-65, T2A craft menus #2476 and the
# PlayerConstructed stamp #2574), and keeping our old copies would have silently reverted both. They are patches now,
# so the next upstream change to either file stops the build here. The hooks are the Shattered Legacy ones (Craft X,
# extended lumber and ingots, familiarity, the Hammer of Hephaestus, BOD autofill); hook placement against upstream's
# overloads was checked with shard-migration/notes/cc-P30-tools/hook_audit.py. Notes: notes/cc-P38-engine-upgrade.md.
# cc-P61 Part A (D83) added one hunk: GetSuccessChance's gain call goes through ClusterFCraftGain.CheckSkill, so a
# post-Valorite metal can raise Blacksmithy's gain ceiling. The roll is pinned's own Mobile.CheckSkill; only its window
# moves, and only from the item's maximum up; the success chance below it still reads the item's own range. A hook
# cannot do it without a patch: the skill-check handlers do not know the craft's metal. Notes: notes/cc-P61-batch-7.md.
# cc-P67 reuses the same hunk, unchanged, for Carpentry, Fletching and Tinkering (ClusterFCraftGain.CeilingFor).
apply_patch "$PATCHES/CraftItem-shard-hooks.patch"
apply_patch "$PATCHES/CraftContext-shard-hooks.patch"
apply_patch "$PATCHES/PlayerMobile-individual-stat-cap.patch"
apply_patch "$PATCHES/Healer-resurrection-policy.patch"
apply_patch "$PATCHES/HealerGuildmaster-resurrection.patch"
apply_patch "$PATCHES/AOS-pet-mimic-attribute-aggregation.patch"
apply_patch "$PATCHES/Bandage-pet-mimic-targeting.patch"
apply_patch "$PATCHES/BaseWeapon-talisman-durability.patch"
apply_patch "$PATCHES/CraftGump-MakeXClear.patch"
apply_patch "$PATCHES/CraftGumpItem-MakeX.patch"
apply_patch "$PATCHES/SmallSmithBOD-PostValorite.patch"
# cc-P42 Part G1 (Chase, 2026-10-03). The Society of Smiths' small orders ask for items that can still raise the smith's
# Blacksmithy: an overload CreateRandomFor(m, teachingOnly) with two calls into customizations ClusterFSmithTeaching; the
# one-argument form, which regular smiths call, is unchanged. Alternatives (a copy of the generator in customizations)
# argued in shard-migration/notes/cc-P42-defect-batch-3.md, Part G; pinned by SmithOrdersTeachVerification.
# cc-P67 Part C 1 (Chase 2026-10-05): with the character's setting on, the post-Valorite metal roll goes through
# ClusterFSmithTeaching.TeachingMetal (the metals that still teach the smith); in this patch because teachingOnly is
# this patch's parameter. Notes: shard-migration/notes/cc-P67-craft-ceilings.md; pinned by CraftCeilingsVerification.
apply_patch "$PATCHES/SmallSmithBOD-teaching-orders.patch"
# cc-P46 Part A (bug-list D55). Pinned's ore rule covers DullCopper..Valorite only, so a Platinum..Celestial deed took
# any item (an iron one included). SmallBOD: GetMaterial maps our eight post-Valorite CraftResources, and the item
# combine refuses another ore with pinned's 1045168. LargeBOD: a small deed of another post-Valorite ore is refused with
# pinned's 1045162. One else-if beside pinned's own ore branch each. Alternatives (overrides copying pinned's checks,
# renumbering the enum) argued in shard-migration/notes/cc-P46-smith-orders-2.md, Part A; pinned by
# PostValoriteBODMaterialVerification.
apply_patch "$PATCHES/SmallBOD-post-valorite-material.patch"
apply_patch "$PATCHES/LargeBOD-post-valorite-material.patch"

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

# cc-P29 D44, a pinned defect. AosAttributes.AddStatBonuses named its stat mods by GetHashCode() and
# RemoveStatBonuses removed them by Owner.Serial, so a talisman's or quiver's Str/Dex/Int bonus never came off and
# stacked on every re-equip until restart (unbounded with no stat ceiling). The add path now uses the serial, as
# every other item type does. Alternatives argued in shard-migration/notes/cc-P29-defect-batch-2.md Part A;
# pinned by StatBonusLeakVerification (proved red). ModernUO's to report upstream.
apply_patch "$PATCHES/AOS-stat-bonus-names.patch"

# S8 test route. One of the two patches in this repo that touch an upstream TEST project (cc-P42 added the other), and
# like it has no effect on the shipped server. Argued in shard-migration/notes/s8-test-route.md section 2.
# Since cc-P38 (upstream d4531cd9) it targets TestServerInitializer.cs, where upstream #2473 moved the test
# bootstrap, and upstream now calls NPCSpeeds.Configure itself; the patch adds the five shard calls after it
# (SkillCheck, AntiMacroSystem, NameList, Corpse, BaseCreature). AntiMacroSystem since cc-P46 Part F: SkillCheck's
# handlers read its settings, and without it a gain-range CheckSkill threw unless a craft fact had run first
# (D56; argued in shard-migration/notes/cc-P46-smith-orders-2.md, Part F). The file keeps its old name. If it ever
# stops applying, the build stops here, which is the intended failure.
apply_patch "$PATCHES/UOContentFixture-npc-speeds.patch"

# CC4 Despise. ServUO's BaseCreature.CanAutoStable, three additive lines across two UOContent files: a
# virtual on BaseCreature and one guard in PlayerMobile.AutoStablePets. Without it a creature possessed
# through a Wisp Orb is taken into the stable when its master logs out. Alternatives argued in
# shard-migration/notes/cc4-despise.md; pinned by DespiseRevampedVerification (proved red).
apply_patch "$PATCHES/BaseCreature-PlayerMobile-can-auto-stable.patch"

# CC6 follow-up (P12, Q-053). ServUO's `targ is ChickenLizardEgg` branch of BaseBeverage.Pour_OnTarget, four lines
# beside the PlantItem branch. Pouring is a targeting flow the beverage owns and pinned has no interface hook for
# the target, so there is no additive way to reach the egg's Pour. Alternatives argued in
# shard-migration/notes/cc6-followup-breath-incubator.md; pinned by CC6FollowupBreathIncubatorVerification (proved red).
apply_patch "$PATCHES/Beverage-chicken-lizard-egg-pour.patch"

# Armour-set completion. Stock SpiritualityHelm and ValorGauntlets are two of the Virtue set's eight pieces, modelled
# standalone with the set bonus folded in, so the six ported pieces could never complete the set. Re-parented onto
# BaseSetArmor with ServUO's values; no save held an instance. A patch rather than a replacement so a bump that
# touches either file stops here. Alternatives argued in shard-migration/notes/armour-set-completion.md section
# A.1.1; pinned by ArmourSetCompletionVerification (proved red).
apply_patch "$PATCHES/Virtue-stock-pieces-set-carrier.patch"

# P7. Pinned wrote these two OnDeath drops and left them commented under "TODO: uncomment once added", waiting on
# armour-set types it never shipped and we now declare. Each patch removes the comment markers and nothing else, except
# that Lady Lissith's LissithsSilk and ParrotItem rolls stay commented: neither type exists in pinned or ours. Pinned's
# other waiting drops (Ilhenir, the Valley champion, Scrapper's Compendium) are deliberately not patched: none reaches
# a player. Argued in shard-migration/notes/cc-P7-pinned-waiting-drops.md; pinned by PinnedWaitingDropsVerification.
apply_patch "$PATCHES/Miasma-pinned-waiting-drop.patch"
apply_patch "$PATCHES/LadyLissith-pinned-waiting-drop.patch"

# P8. Chase approved patching pinned's stock loot tables to match ServUO (2026-09-29). HatTypes gains ServUO's OrcMask
# and TribalMask; the stealable table gains ServUO's 58 entries for the 55 types of ours it names, APPENDED because the
# persistence file maps saved slots to entries by index and the live world holds 80. Treasure chests (gate 3) are not
# here: pinned has none of the lists they were costed as. Argued in shard-migration/notes/cc-P8-loot-tables-and-d35.md;
# pinned by P8LootTablesVerification.
apply_patch "$PATCHES/Loot-hat-types-orc-tribal-mask.patch"
apply_patch "$PATCHES/StealableArtifacts-servuo-entries.patch"

# P9. Test Center deletion. Pinned's RestrictDeletion is a static readonly field set from TestCenter.Enabled when
# AccountHandler's statics are first touched, which can be before TestCenter.Configure runs (same default priority,
# unstable sort), freezing the 7-day wait on a Test Center shard. Latent at 7c9215d97 (a probe put TestCenter first), but
# any added Configure can reorder it. One line: the field becomes a property read at
# deletion time. Live (testCenter.enable false) keeps the wait. Alternatives argued in
# shard-migration/notes/cc-P9-test-center.md section 3; pinned by TestCenterVerification.
apply_patch "$PATCHES/AccountHandler-test-center-deletion.patch"

# P18 (F-5, ours). Young status by time played: 2 weeks (336 h of account game time) instead of 40 h, the
# login countdown in days and hours, and no skill-total rule (the murder rule stays). The duration and the
# countdown are in pinned Account.cs and the skill rule in pinned PlayerMobile.OnSkillChange, with no hook to
# reach them from customizations; both patches point pinned at ClusterFYoungPlayer.cs. Argued in
# shard-migration/notes/cc-P18-reset-stone-young-craftx.md; pinned by YoungPlayerVerification.
apply_patch "$PATCHES/Account-young-duration.patch"
apply_patch "$PATCHES/PlayerMobile-young-time-only.patch"

# P18 (F-1, ours). Craft X recycles its rejects through pinned's own code, not a copy: a smith's reject through the
# craft menu's Smelt (Resmelt.cs), and a salvage bag's contents through the bag's Salvage All (SalvageBag.cs). Both
# were private; each patch only makes them internal. Argued in shard-migration/notes/cc-P18-reset-stone-young-craftx.md
# section 3; pinned by CraftXVerification.
apply_patch "$PATCHES/Resmelt-smelt-one.patch"
apply_patch "$PATCHES/SalvageBag-salvage-all.patch"

# P22 (F-17, ported). OSI's "Combine this deed with contained items" (cliloc 1157304) on pinned's small and large BOD
# gumps: one row taller, one button, one response branch calling BODCombineContained (customizations), which runs
# every item through the deed's own EndCombine. No hook reaches a pinned gump's layout. Argued in
# shard-migration/notes/cc-P22-small-features-1.md (F-17); pinned by BODCombineContainedVerification.
apply_patch "$PATCHES/BOD-combine-contained.patch"

# cc-P61 Part C (D85, Chase 2026-10-05: our own clilocs). Pinned's four GetMaterialNumberFor (the small and large deed
# gumps, which the deeds' tooltips also call, SmallBOD.cs:64 and LargeBOD.cs:55, and the two accept gumps) map only Dull
# Copper..Valorite to EA's 1045142+ and return 0 for anything else, so a Platinum..Celestial deed had no "All items must
# be made with ... ingots." line anywhere. Each is a public static on a pinned gump class, called by pinned code: no
# hook or override reaches it, and a copy of the gumps would hide upstream drift. One line each: the fall-through
# return 0 becomes ShardClilocs.PostValoriteMaterial(material), which is our 1900003-1900010 for the eight metals and
# still 0 for everything else. Applied after the combine patch (same two gump files). Argued in shard-migration
# notes/cc-P61-batch-7.md, Part C; pinned by PostValoriteDeedMaterialLineVerification.
apply_patch "$PATCHES/BOD-gumps-post-valorite-material.patch"

# cc-P32 Part A (PT-07), a pinned defect. The JSON spawner import deleted every same-type spawner on the entry's x,y
# (GetItemsAt ignores z), and pinned's data stacks spawners on one tile, so 338 of shared/** and post-uoml/** were
# lost. The patch drops that delete; the import's guid match still replaces on a re-run. Argued in
# shard-migration/notes/cc-P32-spawner-importer-bods-self-repair.md A.2; pinned by SpawnerImportVerification (proved
# red). ModernUO's to report upstream.
apply_patch "$PATCHES/ImportSpawners-guid-key.patch"
# cc-P42 Part C. Upstream's own ImportCleanupTests.Import_DuplicateLocation_ReplacesExistingSpawner asserted the delete by
# location the patch above removes, so the upstream suite had 1 failure (cc-P38 section 13). This test patch makes that one
# test expect our behaviour: the other-guid spawner on the tile is kept, the same-guid one is replaced. With the patch above
# removed it fails, so a later upstream change to the importer stops here. The second patch that touches an upstream TEST
# project (the first is the fixture patch above). Argued in shard-migration/notes/cc-P42-defect-batch-3.md, Part C.
apply_patch "$PATCHES/ImportCleanupTests-guid-key.patch"
# cc-P42 Part J (D53, Chase 2026-10-03). Trammel Despise runs the revamp only: the importer asks customizations
# ClusterFDespiseStockSpawns.Admit about every spawner it builds, which keeps the treasure chests of upstream's
# shared/trammel/Despise.json and drops its creatures; every other spawner is untouched. An exclusion we own rather than an
# edited copy of upstream's JSON, argued in shard-migration/notes/cc-P42-defect-batch-3.md, Part J; pinned by
# DespiseStockSpawnsVerification (proved red).
apply_patch "$PATCHES/ImportSpawners-despise-exclusion.patch"

# cc-P32 Part C (F-27, ours). Self Repair leaves the random property table for armor, shields and hats (two
# m_Props.Set lines, as pinned already does for Mage Armor), so loot and runic crafting never roll it; the rare
# roll is ClusterFRareSelfRepair (customizations). Argued in the same notes, C.2; pinned by
# RareSelfRepairVerification (proved red).
apply_patch "$PATCHES/BaseRunicTool-no-random-self-repair.patch"

# cc-P33 (F-3, ported). Clean Up Britannia pays for what goes into any trash barrel or trash chest, a house's
# included, as OSI's does (ServUO pub57 Items/Containers/TrashBarrel.cs and TrashChest.cs derive from BaseTrash).
# Pinned's two have no hook, so each patch adds the calls into customizations Services/CleanUpBritannia/
# CleanUpTrash.cs, which holds BaseTrash's bookkeeping: record a drop, confirm it as the container deletes it, pay,
# and the "Appraise for Cleanup" entry. The alternatives (our own barrel only, or replacing house barrels) are argued
# in shard-migration/notes/cc-P33-clean-up-britannia.md; pinned by CleanUpBritanniaBarrelVerification (proved red).
apply_patch "$PATCHES/TrashBarrel-clean-up-britannia.patch"
apply_patch "$PATCHES/TrashChest-clean-up-britannia.patch"

# cc-P38 (D49, Chase 2026-10-02), an engine defect, pinned 7c9215d97 and upstream d4531cd9 alike. HarvestDefinition.
# GetVeinFrom tested `randomValue <= VeinChance` over values 0..VeinWeights-1, so the first vein got one value too many
# and the last one too few: our last veins, Starwood and Celestial (weight 1 each), could never occur. One character,
# `<`. Stock shares move by one value per thousand (the first vein loses one, the last gains one). Measured in
# shard-migration/notes/cc-P37-upgrade-prep.md section 5 and cc-P38-engine-upgrade.md; pinned by
# PermanentGrovesVerification facts 3 and 4 (proved red). ModernUO's to report upstream (not exploit-class).
apply_patch "$PATCHES/HarvestDefinition-vein-boundary.patch"

# cc-P51 Part A (F-31, ported). Upstream's Raptor left out ServUO's pack friends and its 25% AncientPotteryFragments drop
# (its own TODO, removed here). The patch adds an OnCombatantChange override that calls base and then customizations
# Mobiles/Normal/ClusterFRaptorPack.cs, and the pottery roll before upstream's claw roll. Friends are RaptorPackFriend, a
# customization subclass that deletes itself after load, so Raptor keeps its version 0 save shape and needs no migration.
# Alternatives (a serialized _isFriend with a version bump, a world poll timer, our old class as a replacement) argued in
# shard-migration/notes/cc-P51-staff-hub-and-raptor-pack.md, Part A; pinned by RaptorPackFriendsVerification.
apply_patch "$PATCHES/Raptor-pack-friends.patch"

# cc-P53 Part C (deviation D-113, Chase 2026-10-04). Item and temporary skill bonuses count above the skill's cap.
# PlayerMobile.AddSkillMod hands every mod to ClusterFSkillBonusAboveCap (customizations), which clears ObeyCap, so the
# three pinned sources that clamp (AosSkillBonuses.AddTo, Animal Form's two) and any later one are covered by one hook.
# Rejected: patching Skills.cs (core, every mobile, changes what ObeyCap means), patching AOS.cs and AnimalForm.cs (two
# files, misses the next source), customizations alone (no event fires on AddSkillMod). Argued in
# shard-migration/notes/cc-P53-two-hundred-cap.md Part C; pinned by SkillBonusAboveCapVerification (proved red).
apply_patch "$PATCHES/PlayerMobile-skill-bonus-above-cap.patch"

# cc-P53 Part E (D63). The craft gump's category list has no paging (CraftGump.CreateGroupList; ServUO's is the same)
# and its panel holds ten groups, so blacksmithy's twelfth drew over NOTICES. Past ten groups the list pages nine at a
# time, re-sending the gump; ten or fewer draw exactly as pinned. Rejected: a tighter row pitch (buttons overlap and are
# hard to click), folding a group (moves ServUO content, and the stock blacksmithy list alone is eleven), a second
# column (200 pixels wide). Argued in the same notes, Part E; pinned by CraftCategoryPagingVerification (proved red).
apply_patch "$PATCHES/CraftGump-category-pages.patch"

# cc-P55 Part B (D70, Chase 2026-10-05). The ore satchels' Smelt Ore smelts each pile exactly as the forge does, through
# the forge's own step rather than a copy of its ratio and skill check: BaseOre's private target becomes internal, gains
# SmeltAt (its own OnTarget, with the forge the satchel found) and an optional IngotDestination so the ingots go back into
# the satchel (the backpack when it cannot hold them, as before). Rejected: no patch, driving the player's own target
# cursor (replaces any pending target, says "Select the forge" for every pile, drops ingots loose in the pack); copying
# the formula (drifts from the forge). Argued in shard-migration/notes/cc-P55-bug-batch-5.md, Part B; pinned by
# OreSatchelSmeltVerification (proved red).
# cc-P56 Part B (D78, Chase 2026-10-05): a third hunk in the same forge step gives each post-Valorite ore its metal's
# tier as smelt difficulty (ClusterFMetalTiers, Platinum 112.5 .. Celestial 200; pinned gives every unlisted ore 50),
# so the satchel inherits it with no copy. Rejected: a second patch on the same file (two seams for one method), a
# satchel-only check (the forge would still smelt Celestial at 50). Pinned by OreSmeltDifficultyVerification.
apply_patch "$PATCHES/Ore-smelt-at-forge.patch"

# cc-P57 Part G (bug-list D82). Stealing from a stack: the amount a thief may take was Stealing / 10 stones, so above
# OSI's top Stealing (100) a part or a whole pile could weigh up to 20 stones, its skill window ran to 227.5, and Stealing
# gained at every skill up to the 200 cap (and is never caught from 150). One clamp: what is taken weighs no more than
# MaxWeightToSteal (10), the limit the single-item test already applies; identical to pinned at every Stealing up to 100.
# Rejected: testing the whole pile's weight before a partial steal (OSI and pinned let a thief take part of a heavy pile),
# capping the window's numbers (leaves 20-stone piles stealable), a customization (the target class is private, no hook).
# Argued in shard-migration/notes/cc-P57-batch-6.md, Part G; pinned by StealingStackWeightVerification (proved red).
apply_patch "$PATCHES/Stealing-stack-weight.patch"

# cc-P66 Part A (batch X, cc-P54 B.4/B.5, Chase's decision 9: clamps before any gain path past OSI's tops). Seven pinned
# effects read a skill with no upper bound, so a player's skill above OSI's top made them certain or immune. Each patched
# line now reads the player's skill through customizations ClusterFSkillClamps, which counts it at most at OSI's top
# Value (the power-scroll cap, else 100); creatures read theirs as pinned, and every value at or below OSI's top is
# unchanged. Stealing's caught test (applied after the stack patch above, same file): a thief at 150 by worn bonuses was
# never caught; now caught as at 120. Hiding's combat range: from Hiding 118 a hider could hide beside a combatant; never
# below 8 tiles now. Detect Hidden's radius: 20 tiles at 200, past the screen; 10 now (the contest still reads the whole
# skill), plus one call into ClusterFSkillGain when the scan found something (Part B). The bard skills: Musicianship's
# reduction of difficulty was (Mus - 100) / 2 uncapped, 50 at 200; at most 10 now (three files, one patch). Block: the
# final chance at Parry 200, Bushido 120, Evasion, a two-hander was 0.95; at most OSI's own highest, 0.600036. Tracking's
# radius: 210 tiles at 200; 110 now. Nether Cyclone: a resister past 160 against a 120/120 mystic got stamina and mana back;
# resist counts at most 120 now. Rejected for all seven: clamping the skill everywhere (the skills are meant to grow past
# OSI, only these effects break), customizations alone (each line is inside a pinned method or a private target class with
# no hook), and clamping creatures too (ServUO's creatures above OSI's tops would change). Argued in
# shard-migration/notes/cc-P66-clamps-and-hook-g.md, Part A; pinned by SkillClampsVerification (proved red).
apply_patch "$PATCHES/Stealing-caught-osi-skill.patch"
apply_patch "$PATCHES/Hiding-combat-range-osi.patch"
apply_patch "$PATCHES/DetectHidden-osi-radius-found-gain.patch"
apply_patch "$PATCHES/Bard-musicianship-reduction-osi.patch"
apply_patch "$PATCHES/BaseWeapon-block-chance-osi.patch"
apply_patch "$PATCHES/Tracking-radius-osi.patch"
apply_patch "$PATCHES/NetherCyclone-resist-osi.patch"

# cc-P67 (cc-P54 B.3/B.4, Chase's decisions 10 and 11): four more effects read a skill with no upper bound, and the skills
# behind them climb past OSI's tops now or will. Each patched line reads the player's skill through ClusterFSkillClamps,
# as cc-P66's seven; creatures as pinned, and every value at or below OSI's top is unchanged. Tinkering's trap damage
# (DefTinkering TrapCraft): the level was Tinkering / 10, so a Tinkering 200 trap exploded for 200-600; the damage level
# counts at most 120 (12) while the trap's power, Remove Trap's difficulty, still reads the whole skill (cc-P54 B.3: they
# are separate fields, and capping the power would stall Remove Trap at 138). The axe's Lumberjacking damage bonus
# (BaseWeapon, applied after the block patch above, same file): 50% at 200, outside the damage cap; at most 30% (GM's).
# Potion strength (BasePotion): Alchemy's share was Alchemy x 10 / 33, 60 at 200; at most 30. Fishing's deep-water finds
# (Fishing.MutateType): (Fishing - 80) / 4000 each, 3% at 200; a chance that grows with Fishing counts at most 120 (1%).
# Rejected, as for cc-P66's: clamping the skill everywhere, customizations alone (each is a local inside a pinned method),
# clamping creatures. Argued in shard-migration/notes/cc-P67-craft-ceilings.md; pinned by CraftCeilingsVerification.
apply_patch "$PATCHES/Tinkering-trap-damage-osi.patch"
apply_patch "$PATCHES/BaseWeapon-axe-bonus-osi.patch"
apply_patch "$PATCHES/BasePotion-alchemy-bonus-osi.patch"
apply_patch "$PATCHES/Fishing-deep-water-finds-osi.patch"

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
        fail "$name - present in $PATCHES but never applied or skipped by name"
    fi
done

echo "[patches] Structural fixes..."
# Stock Lumberjacking must be partial for ClusterFLumberjackingExtension.
# sed reports success when it matches nothing, so assert the result instead.
LUMBERJACKING=Projects/UOContent/Engines/Harvest/Lumberjacking.cs
sed -i 's/public class Lumberjacking/public partial class Lumberjacking/' "$LUMBERJACKING"
if ! grep -q 'public partial class Lumberjacking' "$LUMBERJACKING"; then
    fail "structural fix - 'public partial class Lumberjacking' not present in $LUMBERJACKING after sed"
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
