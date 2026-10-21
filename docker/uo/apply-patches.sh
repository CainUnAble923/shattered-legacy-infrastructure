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
# cc-P127. The Forgotten Treasures revamp (ServUO Services/TreasureMaps): pinned's map (915 lines) and chest (558) become
# ServUO's (1,786 and 1,014), and the diff is the whole of each file, so they are replacements, with every pinned caller's
# constructor and static kept. Argued in shard-migration/notes/cc-P127-port-treasure-maps-monster-stealing.md; pinned by
# TreasureMapRevampVerification (proved red).
replace_file "$PATCHES/TreasureMap.cs"      "Projects/UOContent/Items/Maps/TreasureMap.cs"
replace_file "$PATCHES/TreasureMapChest.cs" "Projects/UOContent/Items/Containers/TreasureMapChest.cs"
echo "[patches] Full-file replacements done."

# Install a file of ours at a path the pinned tree does NOT have. The mirror image of replace_file: if the
# destination already exists, upstream has grown a file of that name and a plain cp would silently shadow it,
# so that is a build failure to reconcile by hand.
install_file() {
    local src="$1" dest="$2"

    if [ ! -f "$src" ]; then
        fail "$(basename "$src") - source missing at $src"
        return
    fi
    if [ -e "$dest" ]; then
        fail "$(basename "$src") - destination $dest already exists in pinned upstream; reconcile by hand"
        return
    fi
    cp "$src" "$dest"
}

echo "[patches] Installing build property files..."
# cc-P232 (closes D93). MailKit 4.18.0 in UOContent.csproj pulls System.Security.Cryptography.Pkcs 10.0.0 in through
# MimeKit, and 10.0.0 to 10.0.12 carry two NuGet advisories (GHSA-2fmv-rv2c-59x7 moderate, GHSA-846f-f764-76mx high,
# both fixed in 10.0.13) that pinned Directory.Build.props turns into restore errors (TreatWarningsAsErrors). The
# Dockerfile carried /p:NuGetAuditLevel=critical as a workaround, which let every moderate and high advisory through.
# This file pins Pkcs to [10.0.13, ) for UOContent only: upstream's own fix, commit 6ce2b4770 (2026-10-17), as one
# MSBuild import rather than a patch to UOContent.csproj. Delete both when the pinned commit includes that change.
install_file "$PATCHES/Directory.Build.targets" "Directory.Build.targets"
echo "[patches] Build property files installed."

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
# cc-P70: a mule module above tier I is crafted from the same module one tier lower. AddRes matches by type only
# (Container.ConsumeTotal, IsInstanceOfType), so it cannot ask for "the tier I Ore Hopper"; the alternative is 51 sibling
# types (17 modules x 3 tiers, more if Chase raises the tier count). One check in ConsumeAttributes, which runs with
# consume=false on every check and consume=true only on a successful craft. Applies on top of CraftItem-shard-hooks.
# Argued in shard-migration/notes/cc-P70-mule-modules.md section 3.
apply_patch "$PATCHES/CraftItem-mule-module-tier.patch"
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
# cc-P170 (Chase 2026-10-18): that metal reached the order only through the 30% post-Valorite roll, so a 144 smith got iron
# plate tunics; now an armor order whose rolled metal teaches nothing takes it (ClusterFSmithTeaching.OrderMetal), and the
# armor/weapon choice counts armor in it. Notes: shard-migration/notes/cc-P170-smith-teaching-orders-fix.md; pinned by
# SmithOrdersTeachHighSkillVerification.
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
# cc-P192 added two lines: CheckYoung and the login countdown count play time from the Young clock's start
# (ClusterFYoungPlayer.YoungPlayed, an account tag the Fountain of Youth resets) instead of the whole TotalGameTime.
# With no tag the count is TotalGameTime, so every account behaves as before.
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

# cc-P78 (F-43, Chase 2026-10-11). Recipe-gated crafting. Two seams, one rule (ClusterFRecipeGate): CraftGump.CreateItemList
# lists only the items the account knows, each keeping its own index in the group for its buttons (so OnResponse is
# untouched), then hint lines for the hidden ones, paged by row as pinned paged items; OnResponse hands a hint line's
# button to the gate first. CraftItem.Craft's two recipe checks become the gate's, which still holds pinned's own recipe
# rule (made account-wide), so Make Last, Last Ten, Make X, forged button ids and the T2A menus are all held to it.
# Rejected: no patch (nothing in pinned or ours can filter the list, and pinned's per-character HasRecipe cannot carry
# account-wide band books); hiding in CraftGump only (every path that skips the list would still craft); a CraftSystem
# subclass per craft (eleven replacements of pinned's Def files); renumbering the list (breaks the button-to-item
# mapping). Applies on top of CraftItem-shard-hooks and CraftGump-category-pages. Argued in shard-migration/notes/
# cc-P78-recipe-gating.md; pinned by RecipeGatingVerification (proved red).
apply_patch "$PATCHES/CraftGump-recipe-gating.patch"

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
# cc-P195 (the Exodus Encounter): Detect Hidden reveals an IRevealableItem (ServUO Skills/DetectHidden.cs:116-126), the
# hidden supply chests of the Exodus Dungeon. One hunk after the trap branch; applies over the cc-P66 patch above.
apply_patch "$PATCHES/DetectHidden-revealable-items.patch"
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

# cc-P193 (High Seas, the fishing half): pinned Fishing hands out the High Seas fish (GetResourceType asks FishInfo first;
# a rare fish is not mutated into a find, is weighed and signed in Give, and has its own catch message), and the taxidermy
# kit makes a fish trophy deed from a rare fish, crab or lobster. Both are hooks into our own additive files under
# Services/Expansions/High Seas; alternatives argued in shard-migration/notes/cc-P193-port-high-seas-fishing.md.
apply_patch "$PATCHES/Fishing-high-seas-fish.patch"
apply_patch "$PATCHES/TaxidermyKit-fish-trophies.patch"

# cc-P71 (cc-P66 decision 4, Chase 2026-10-06: "update these to appropriate levels at 200"): five effects that kept growing
# past OSI's top now scale instead. Each patched line reads the player's skill through ClusterFSkillClamps, which returns
# pinned's own number at or below OSI's top and past it one diminishing-returns curve to a value at 200 that lives in
# modernuo.json (clusterf.skillScaling.*): Nerve Strike's paralysis (certain from Bushido 137.5; 95 percent at 200), the
# stable slots from Taming, Lore and Veterinary (38 at 200/200/200; 23), Fire Horn damage (2.33x at four bard skills 200;
# 1.10x), Stealth steps (40 at 200; 34) and the heal per bandage (93.2 average at 200/200; 72). Creatures as pinned.
# Rejected: clamping at OSI (Chase asked for 200 to be better than OSI's top, not equal); customizations alone (each read
# is a local inside a pinned method, AnimalTrainer.GetMaxStabled is static with one caller and no hook); a curve per
# effect (one curve, one bend key, tuned in one place). Bandage's applies after Bandage-pet-mimic-targeting.patch above.
# Argued in shard-migration/notes/cc-P71-high-skill-effect-scaling.md; pinned by SkillScalingVerification (proved red).
apply_patch "$PATCHES/NerveStrike-paralyze-scaled-200.patch"
apply_patch "$PATCHES/AnimalTrainer-stable-slots-scaled-200.patch"
apply_patch "$PATCHES/FireHorn-damage-scaled-200.patch"
apply_patch "$PATCHES/Stealth-steps-scaled-200.patch"
apply_patch "$PATCHES/Bandage-heal-scaled-200.patch"

# cc-P50: the chest mimic is drawn by an item (ChestMimicShell) over a mobile whose body draws nothing, so players can
# only click the item. TazUO picks a mobile by the pixels of its body frames (MobileView.cs:1112-1125, 1214-1235), and
# Target.Invoke refuses an item for a SpellTarget<Mobile> before any item hook runs (Target.cs:163-178), so no
# override can hand the target over. One hunk: an item that implements ITargetRedirect names the mobile instead.
# Alternatives argued in shard-migration/notes/cc-P50-chest-mimic-monster.md section 3.
apply_patch "$PATCHES/IncomingTargetingPackets-target-redirect.patch"

# cc-P82: the Imbuing rework's custom properties, one call each where its system runs, all into ClusterFImbueEffects:
# BaseWeapon.OnHit (Bond on a companion's damage; Time Slip and Shatter after the special move), WeaponAbility (a
# public ClearCooldown for Time Slip's refund; the window is private), SpellHelper.Damage (Spell Leech, the one place
# immediate and delayed spell damage both pass), CraftItem.GetExceptionalChance (Mastercraft; non-virtual),
# Item.GetProperties (one static delegate for the custom property's tooltip line), and PlayerMobile.OnItemAdded (when
# Fortune's Favor gear went on, for the 60 second achievements). No override reaches any of these. Applies last, on top
# of CraftItem-shard-hooks, CraftItem-mule-module-tier, BaseWeapon-talisman-durability, BaseWeapon-block-chance-osi,
# BaseWeapon-axe-bonus-osi and the PlayerMobile patches. Argued in shard-migration/notes/cc-P82-artificers-imbuing.md.
apply_patch "$PATCHES/Imbue-custom-property-hooks.patch"

# cc-P121: Pet Training part 1, the ability core (Chase's decisions 1a and 2a, 2026-10-15). Each patch adds one call into
# customizations/Services/Pet Training/PetTrainingHooks.cs, where ServUO makes the same call: BaseCreature (OnThink's
# think triggers, OnMovement's approach trigger, OnGaveMeleeAttack's trained hit poison and Colossal Rage's blow), AOS
# (AOS.Damage's combat trigger, which may change the damage, and GetValue's debuff penalties), SpellHelper (spell damage
# marked as spell for that trigger, which pinned's AOS.Damage cannot tell apart; Searing Wounds on a spell heal),
# Bandage (Searing Wounds on a bandage heal) and MageAI (SmartAI for the Magery Mastery school). Profiles themselves live
# in a side table with its own save file (AbilityProfileStore.cs), so BaseCreature's save format is untouched. The
# SpellHelper and AOS patches apply after Imbue-custom-property-hooks and the AOS patches above, whose lines they meet.
# Alternatives argued in shard-migration/notes/cc-P121-pet-ability-core.md; pinned by PetAbilityCoreVerification.
apply_patch "$PATCHES/BaseCreature-pet-ability-hooks.patch"
apply_patch "$PATCHES/AOS-pet-ability-triggers.patch"
apply_patch "$PATCHES/SpellHelper-pet-ability-hooks.patch"
apply_patch "$PATCHES/Bandage-searing-wounds.patch"
apply_patch "$PATCHES/MageAI-pet-magery-mastery.patch"

# cc-P122: Bulk orders part 1, OSI's 2016 rules for smith and tailor deeds (Chase's option A and "guild masters now",
# 2026-10-15). Pinned's deed and book types stay as they are (so every saved deed and book keeps its shape, and the BOD
# patches above keep applying); customizations/Services/BulkOrders/ adds the 2016 system around them. BaseVendor: three
# calls into BulkOrderVendors (the turn-in, the Bulk Order Info offer, the Bribe and Claim Rewards entries), each "not
# mine" for any other vendor. BODType: the six 2016 crafts appended. The deed gumps: the points lines and the
# points-mode button, drawn below EXIT so their earlier rows stay where BOD-combine-contained put them. Applies after
# BOD-combine-contained and BOD-gumps-post-valorite-material. Alternatives argued in
# shard-migration/notes/cc-P122-port-bulk-orders-core.md; pinned by BulkOrderSystemVerification.
apply_patch "$PATCHES/BODType-all-crafts.patch"
apply_patch "$PATCHES/BaseVendor-bulk-orders-2016.patch"
apply_patch "$PATCHES/BOD-gumps-points-mode.patch"

# cc-P123: Bulk orders part 2, the six 2016 crafts (carpentry, fletching, tinkering, cooking, alchemy, inscription) and
# their guild givers. Their deeds are new types in customizations/Services/BulkOrders/; these three patches reach the
# pinned files that cannot be hooked otherwise. BOB-all-crafts: pinned's book stores, rebuilds, names and filters smith
# and tailor deeds only; each of its four files sends any other craft to BulkOrderBooks (the filter keeps pinned's
# numbers 0..16 for saved filters and the smith metals now name their craft, as ServUO's do). CraftItem-chefs-apron: the
# Master Chef's Apron's exceptional bonus beside cc-P82's Mastercraft line (GetExceptionalChance is not virtual).
# Spellbook-improvement-talisman: the Guaranteed Spellbook Improvement Talisman's extra property in Spellbook.OnCraft.
# Applies after CraftItem-shard-hooks and its followers. Alternatives argued in
# shard-migration/notes/cc-P123-port-bulk-orders-crafts.md; pinned by BulkOrderCraftsVerification.
apply_patch "$PATCHES/BOB-all-crafts.patch"
apply_patch "$PATCHES/CraftItem-chefs-apron.patch"
apply_patch "$PATCHES/Spellbook-improvement-talisman.patch"

# cc-P127 (ported). The Forgotten Treasures revamp reaches four pinned files that have no hook: Stealing.cs gets ServUO's
# five-line call into the monster-stealing handler (Services/Monster Stealing); LockPick.cs marks a revamp chest whose pick
# failed (the grubber) and, classic, may break one item; Unlock.cs takes full Magery against 50/80/100 by revamp level;
# RemoveTrap.cs gains ServUO's timed disarm with ancient chest guardians; BaseCreature.cs drops maps on every facet, for
# the facet. Each is ServUO's own lines at its own place. Alternatives argued in shard-migration
# notes/cc-P127-port-treasure-maps-monster-stealing.md; pinned by TreasureMapRevampVerification and
# MonsterStealingVerification (each proved red).
apply_patch "$PATCHES/Stealing-monster-stealing.patch"
apply_patch "$PATCHES/LockPick-treasure-chest-failed-pick.patch"
apply_patch "$PATCHES/Unlock-treasure-chest-levels.patch"
apply_patch "$PATCHES/RemoveTrap-treasure-chest-disarm.patch"
apply_patch "$PATCHES/BaseCreature-treasure-map-all-facets.patch"

# cc-P128 (ported). Consumables and crafting gaps: BasePotion.cs gains ServUO's nine SA/TOL PotionEffect values (appended,
# so saved potions keep their numbers); AOS.cs applies the grapes of wrath where ServUO does (+35 weapon, +15 spell
# damage); BaseCreature.cs applies a tasty treat's damage bonus in AlterMeleeDamageTo; PlayerMobile.cs and Spell.cs read
# the Urali trance tonic (max mana, casting at Protection's penalty); PlantItem.cs, PlantSystem.cs and Seed.cs gain
# ServUO's raised-garden hooks (no-upkeep mounds with their own art and messages, a seed on a garden plot). Each is
# ServUO's own lines at its own place. Alternatives argued in shard-migration
# notes/cc-P128-port-consumables-crafting-gaps.md; pinned by ConsumablesVerification, BasketWeavingVerification and
# RaisedGardenVerification (each proved red).
apply_patch "$PATCHES/BasePotion-sa-potion-effects.patch"
apply_patch "$PATCHES/AOS-grapes-of-wrath.patch"
apply_patch "$PATCHES/BaseCreature-tasty-treat-damage.patch"
apply_patch "$PATCHES/PlayerMobile-urali-mana.patch"
apply_patch "$PATCHES/Spell-urali-trance.patch"
apply_patch "$PATCHES/PlantItem-raised-garden-hooks.patch"
apply_patch "$PATCHES/PlantSystem-raised-garden-upkeep.patch"
apply_patch "$PATCHES/Seed-raised-garden-plot.patch"
# cc-P128: CraftItem.cs's colour tables gain ServUO's plant resources (CraftItem.cs:395, :415-416): reeds, clippings
# and pigment pass their hue to what is made from them, and a basket takes its reeds' colour.
apply_patch "$PATCHES/CraftItem-plant-hue-tables.patch"

# cc-P129: the ML peerless bosses' pet parrot. ParrotItem and PetParrot are ports in customizations/Items/Addons/;
# the perch they stand on is pinned's ParrotPerchAddon, which (unlike ServUO's) keeps no Parrot. This patch gives the
# perch and its deed ServUO's Parrot field (version 0 -> 1 on each, migrated from pinned's v0 by the generator), the
# perch moves the parrot with it and puts it away when chopped, and the deed carries it to the next placement. A
# partial class could add the field but not the serialization, the deed hand-off or the OnAfterDelete, so it is a
# patch. Alternatives argued in shard-migration/notes/cc-P129-port-creatures-part2.md; pinned by
# CCP129CreatureVerification (the parrot facts).
apply_patch "$PATCHES/ParrotPerchAddon-pet-parrot.patch"

# cc-P130: Pet Training part 2, training itself (decision 2a again: the training state lives in
# customizations/Services/Pet Training/PetTrainingStore.cs, its own save file; BaseCreature's save format is untouched).
# Five seams, each where ServUO has the same line:
#   BaseCreature: `fieldChanged:` on the ControlSlots field (the generator calls OnControlSlotsChanged, in
#     BaseCreature.PetTraining.cs, where ServUO's hand-written setter computes the slot range on a constructor's
#     assignment, :3843-3861) and SetAverage in the eight min/max setters (:5078-5234), which is what decides a slot
#     lowering. A partial class cannot reach a generated setter or the bodies of SetHits and its kin, so it is a patch.
#   AnimalTaming: CurrentTameSkill in place of MinTameSkill at both checks (a trained pet asks for more, :184, :402)
#     and PetTrainingHooks.OnTamed after the tame (OnAfterTame and the profile's OnTame, :450-457).
#   AnimalLore: the training lore gump for a player while training is on, one at a time (:17-20, :36-42).
#   SkillCheck: a pet in training gains Focus (:372-374).
#   BaseWeapon: TryGetWeaponAbility, the trained weapon ability before the natural one (:1692), the seam cc-P121 left.
# They apply last, after every patch above (their context is the fully patched tree). Alternatives argued in
# shard-migration/notes/cc-P130-pet-training.md; pinned by PetTrainingVerification.
apply_patch "$PATCHES/BaseCreature-pet-training-slots.patch"
apply_patch "$PATCHES/AnimalTaming-pet-training.patch"
# cc-P171 (Chase 2026-10-18): the anger test (:208) calls ClusterFTamingAnger.Angers, a chance that falls with the tamer's
# skill past the creature's minimum (settings clusterf.taming.anger*), in place of a flat 95%. A patch because the test is
# an inline expression inside the private target's OnTarget; no event or virtual reaches it. After the pet-training patch,
# whose context it is. Alternatives argued in shard-migration/notes/cc-P171-taming-anger-scales.md; pinned by
# TamingAngerVerification.
apply_patch "$PATCHES/AnimalTaming-anger-scales.patch"
apply_patch "$PATCHES/AnimalLore-pet-training-gump.patch"
apply_patch "$PATCHES/SkillCheck-pet-focus-gain.patch"
# cc-P172 (Chase 2026-10-18): inside pinned's controlled-creature branch of CheckSkill (SkillCheck.cs:140-143, the x2), the
# gain chance is also multiplied by ClusterFPetGainSpeed.Multiplier (the owner's loops, Animal Taming and Lore above 100,
# Outriders rank; 1 unless a player controls the creature). A patch because gc is a local of the static CheckSkill; no
# event or virtual reaches it. It makes the roll likelier, never a free gain. After the Focus patch (same file, other hunk).
# Alternatives argued in shard-migration/notes/cc-P172-pet-gain-speed.md; pinned by PetGainSpeedVerification.
apply_patch "$PATCHES/SkillCheck-pet-gain-speed.patch"
# cc-P192: the boat deed refuses a sanctum around the roaming Fountain of Youth (FountainSanctumRegion), with pinned's
# own "A boat may not be placed in this area." BaseBoatDeed's target tests region types only (BaseBoatDeed.cs:152-161);
# there is no region virtual to override. Notes: shard-migration notes/cc-P192-cont-fountain-of-youth.md.
apply_patch "$PATCHES/BaseBoatDeed-fountain-sanctum.patch"
# cc-P190 (Chase 2026-10-19): right after pinned's `gc *= skill.Info.GainFactor` (SkillCheck.cs:133), the gain chance is
# multiplied by the owner's Test Crystal strength (TestCrystal.GainScalar: 1 for anyone without one, and on any shard with
# the Test Center off). It replaces the global testCenter.skillGainMultiplier, which changed GainFactor for everyone at
# that same spot. A patch for the reason cc-P172 gives: gc is a local. Notes: shard-migration notes/cc-P190-cont-test-crystal.md.
apply_patch "$PATCHES/SkillCheck-test-crystal.patch"
apply_patch "$PATCHES/BaseWeapon-trained-weapon-ability.patch"

# cc-P194 Covetous Void Spawn: ServUO's PlayerMobile adds "The Void Pool" to a player's own context menu inside the
# Void Pool region (PlayerMobile.cs:2418-2429). One hook line after base.GetContextMenuEntries, into customizations
# VoidPoolContextMenu.AddEntries (Services/Revamped Dungeons/Covetous Void Spawn/VoidPoolController.cs); a region
# has no context-menu hook and a partial class cannot reach the override. Alternatives argued in
# shard-migration/notes/cc-P194-port-wrong-and-covetous-void.md; pinned by CovetousVoidSpawnVerification.
apply_patch "$PATCHES/PlayerMobile-void-pool-context.patch"
# cc-P199 (ported). The three trap training kits (customizations Items/Functional), Remove Trap's gain path. RemoveTrap.cs
# gains ServUO's IRemoveTrapTrainingKit and its target branch (a kit takes the target itself, :15-18, :60-63) and the
# Endless Journey rule that Remove Trap no longer needs 50 Lockpicking and 50 Detect Hidden (:30-37); PuzzleChest.cs gains
# a virtual wrong-guess seam (ServUO's DoDamage is virtual, Services/Khaldun/PuzzleChest.cs:372) and a public way to open
# its private puzzle gump, which the cylinder kit sends. After RemoveTrap-treasure-chest-disarm (its context). Alternatives
# argued in shard-migration/notes/cc-P199-port-base-items-remainder.md; pinned by TrapTrainingKitVerification.
apply_patch "$PATCHES/RemoveTrap-trap-training-kits.patch"
apply_patch "$PATCHES/PuzzleChest-trap-training-kit.patch"
# cc-P199: Enhance.cs gains ServUO's Forged Metal of Artifacts check (Enhance.cs:253-258): an aura from the forged metal
# (customizations Items/Tools/ForgedMetalOfArtifacts.cs) makes the next enhance succeed and is spent. A patch because the
# result is a local of Enhance.Invoke between its chance rolls and its outcome switch; no event or virtual reaches it.
apply_patch "$PATCHES/Enhance-forged-metal-of-artifacts.patch"

# cc-P226 (F-26 parts 1 to 4 and 6, Chase 2026-10-02): Arms Lore matters more and trains to 200. Every number is in
# customizations ClusterFArmsLore; these are its call sites, one line or two each, applied last so they sit on top of the
# BaseWeapon, BaseArmor and Repair patches above (no hunk shares their lines):
#   BaseWeapon: the exceptional damage increase (35 + 15 x Arms Lore / 200) and the crafted gain roll in OnCraft; the
#     wielder's Arms Lore saving the wear point in OnHit, after Self Repair's roll.
#   BaseArmor: the exceptional resist bonus capped at 10 and the crafted gain roll in OnCraft; the wearer's Arms Lore
#     thinning OnHit's wear. BaseShield: the same at the top of TryLowerDurability.
#   ArmsLore: field maintenance after a successful inspect of a weapon or armor.
#   Repair: Arms Lore / 10 off the weaken chance for a player's tool repair of a weapon or armor (no deed, no clothing).
# No hook reaches any of them: each is a local or a private method. Argued in shard-migration
# notes/cc-P226-arms-lore-matters.md; pinned by ArmsLoreMattersVerification and SkillGainRowKeysVerification.
apply_patch "$PATCHES/BaseWeapon-arms-lore.patch"
apply_patch "$PATCHES/BaseArmor-arms-lore.patch"
apply_patch "$PATCHES/BaseShield-arms-lore-wear.patch"
apply_patch "$PATCHES/ArmsLore-field-maintenance.patch"
apply_patch "$PATCHES/Repair-arms-lore-weaken.patch"

# cc-P227 (F-28): the eight extended woods' bonuses, from customizations ClusterFWoodBonuses, one call each at the end of
# BaseWeapon.OnCraft (bows, staves, clubs) and BaseArmor.OnCraft (wooden shields), after quality, Resource, the runic
# and the exceptional bonuses are set. Pinned's own wood bonuses are BaseRanged's private switch, which knows only the
# OSI woods; OnCraft is the one place every wood weapon and shield passes at craft time. Applied after the arms-lore
# patches above (no hunk shares their lines). Notes: shard-migration notes/cc-P227-extended-wood-bonuses.md; pinned by
# ExtendedWoodBonusesVerification.
apply_patch "$PATCHES/BaseWeapon-wood-bonuses.patch"
apply_patch "$PATCHES/BaseArmor-wood-bonuses.patch"

# cc-P228 (F-29, Chase 2026-10-03): exceptional items read their material's bonuses x1.5, every material. Pinned reads
# CraftAttributeInfo live, with no quality check, at eight call sites in BaseArmor and BaseWeapon (resists, durability,
# lower requirements, luck, element damage); each now asks customizations ClusterFExceptionalMaterial.Attrs, the one
# place the rule lives. The OSI woods' bow bonuses are BaseRanged's private craft-time switch, so its one call is wrapped
# (snapshot, switch, scale what it added). Applied after the arms-lore and wood-bonus patches (no hunk shares their
# lines). Notes: shard-migration notes/cc-P228-exceptional-material-gap.md; pinned by ExceptionalMaterialVerification.
apply_patch "$PATCHES/BaseArmor-exceptional-material.patch"
apply_patch "$PATCHES/BaseWeapon-exceptional-material.patch"
apply_patch "$PATCHES/BaseRanged-exceptional-wood.patch"

# cc-P231 (ours): hidden joke achievements for OSI's easter-egg moments. Each patch adds one call to customizations
# ClusterFJokeAchievements at the branch where pinned refuses or reports the moment (the running-with-scissors refusal,
# the untrustworthy beggar, eating while full, bandaging the unhurt, a worn-out tool, the *hic*, fishing up footwear, an
# explosion potion catching its thrower), because pinned raises no event there. Made against the tree every patch above
# leaves, so they go last. Alternatives argued in shard-migration notes/cc-P231-osi-joke-achievements.md; pinned by
# JokeAchievementsVerification.
apply_patch "$PATCHES/Scissors-running-achievement.patch"
apply_patch "$PATCHES/Begging-untrustworthy-achievement.patch"
apply_patch "$PATCHES/Food-too-full-achievement.patch"
apply_patch "$PATCHES/Bandage-unhurt-patient-achievement.patch"
apply_patch "$PATCHES/CraftItem-tool-worn-achievement.patch"
apply_patch "$PATCHES/HarvestSystem-tool-worn-achievement.patch"
apply_patch "$PATCHES/Beverage-hiccup-achievement.patch"
apply_patch "$PATCHES/Fishing-shoes-achievement.patch"
apply_patch "$PATCHES/BaseExplosionPotion-self-blast-achievement.patch"

# cc-P238 (F-36, ours): climbing boots. While a player stands on a climbed mountain tile they may mine, prospect and hop
# only: PlayerMobile.CanBeHarmful refuses their attacks and harmful spells (harm done to them is the attacker's own
# check, untouched), AllowSkillUse refuses taming, a new CheckSpellCast override refuses Mark, and HousePlacement.Check
# refuses placement. Each line asks customizations ClusterFClimbing. Made against the tree every patch above leaves, so
# they go last. Alternatives argued in shard-migration notes/cc-P238-climbing-boots.md; pinned by ClimbingBootsVerification.
apply_patch "$PATCHES/PlayerMobile-climbed-tile-rules.patch"
apply_patch "$PATCHES/HousePlacement-climbed-tile.patch"
# cc-P239 (F-14, ours): creature levels and Appraisal. BaseCreature.OnSingleClick sends the con color as its own
# label after the name (the name line and its notoriety hue stay pinned's), and Item Identification's target hands a
# creature or NPC to Appraisal (a player and every item stay stock). One call each into customizations
# ClusterFCreatureLevels.cs. The property list cannot carry a per-viewer color (one list for every viewer), and a
# callback set from our own Initialize would race pinned's (Initialize order is not declared). Argued in
# shard-migration notes/cc-P239-creature-levels-appraisal.md; pinned by CreatureLevelsVerification.
apply_patch "$PATCHES/BaseCreature-con-color-line.patch"
apply_patch "$PATCHES/ItemIdentification-appraisal.patch"

# cc-P262 (ours): accessory slots (extra rings, bracelets, toe rings). AosAttributes.GetValue adds the active jewellery in
# the wearer's accessory container, which customizations ClusterFAccessories decides (resists need no patch: the
# container reports them through its own resistance properties, which ComputeResistances already reads). PlayerMobile's
# OnItemAdded and OnItemRemoved tell ClusterFAccessories when shoes go on or off, which turns toe rings off or on. GetValue
# is static with no event, and a partial class cannot reach those two overrides. Made against the tree every patch above
# leaves, so they go last. Alternatives argued in shard-migration notes/cc-P262-accessory-slots.md; pinned by
# AccessorySlotsVerification.
apply_patch "$PATCHES/AOS-accessory-container-aggregation.patch"
apply_patch "$PATCHES/PlayerMobile-accessory-hooks.patch"

# cc-P261 (ours): levelable crafted gear. AosAttributes.GetValue adds each worn item's level-bought bonuses, which
# customizations ClusterFLevelGear keeps by item serial (no field on any stock item type, P72 B.1). One line beside the
# set-bonus hunk; GetValue is a static method with no event, so customizations alone cannot reach it. Resists need no
# patch (the GetItemResistance virtual already exists). Made against the tree every patch above leaves, so it goes last.
# Alternatives argued in shard-migration notes/cc-P261-levelable-gear-build.md; pinned by LevelGearVerification.
apply_patch "$PATCHES/AOS-level-gear-attribute-aggregation.patch"

# cc-P263 (ours): "Noted by the System", hidden achievements for the shard's exploits and odd habits. Each patch adds one
# call to customizations ClusterFNotedBySystem where pinned raises no event: a recall's and a gate's arrival, a rune that
# fills a runebook, a pet's release, each craft attempt's result, and a recycle (Resmelt, the salvage bag) just before it
# deletes the item. SmallBOD-crafted-only is the list's "fix instead of reward" (Store-Bought): a small deed refuses an
# armor, weapon or clothing item nobody crafted (ClusterFStoreBought, clusterf.bods.craftedOnly). Made against the tree
# every patch above leaves, so they go last. Notes: shard-migration notes/cc-P263-noted-by-the-system-achievements.md;
# pinned by NotedBySystemVerification.
apply_patch "$PATCHES/Recall-arrival-achievement.patch"
apply_patch "$PATCHES/Moongate-arrival-achievement.patch"
apply_patch "$PATCHES/Runebook-full-achievement.patch"
apply_patch "$PATCHES/PetOrders-release-achievement.patch"
apply_patch "$PATCHES/CraftItem-fail-streak-achievement.patch"
apply_patch "$PATCHES/Resmelt-recycle-achievement.patch"
apply_patch "$PATCHES/SalvageBag-recycle-achievement.patch"
apply_patch "$PATCHES/SmallBOD-crafted-only.patch"

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
