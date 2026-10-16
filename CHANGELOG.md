# Shattered Legacy changelog

<!--
How this file works (F-15, cc-P22). This file is the only place patch notes are written.

- Newest first. One section per version, headed "## " and the version: a date, 2026.09.30. A second update on the
  same day is 2026.09.30.2, then .3. The newest section's version also goes in VERSION, one line.
- Under a version, up to three lists, each headed "### Added", "### Changed" or "### Fixed". Leave out a list with
  nothing in it.
- A fourth list, "### Staff", is for staff: players never see it, and staff see it in [version (in its own color).
- One player-facing line per item, starting "- ". Plain words: what a player sees, not how it was built. Internal
  detail (file names, tests, bug numbers) stays out.
- Anything else in this file, including this comment, is ignored.

Who reads it:
- The server, at start (server/customizations/ShardVersion.cs): the version in game ([version), the patch notes in
  the login bulletin once per account per version, and "version" in status.json. Docker copies this file and VERSION
  into the image, so a server shows the notes of the build it is running.
- The wiki page uo:patch_notes, generated from the same parse; scripts/Publish-PatchNotes.ps1 publishes it.

Example:

## 2026.10.02

### Added
- A Thieves' Den lookout sits by the fighting pit and points the way to the guildmaster.

### Fixed
- Restoring a Jacob's Pickaxe no longer refuses ingots kept in your bank.
-->

## 2026.10.16.6

### Staff
- The Shard Console resizes properly (the status, tabs and output panes have draggable splitters, the tiles wrap, and the window remembers its size and place), opens in a new Enhanced Client look with green, amber and red status crystals on every tile and a Golem that glows while it works, reads art from scripts\console-art (a spec for making it is in the docs repo), draws every confirm phrase and warning in colors that read on the stone, and shows two new lights: whether the test shard needs a rebuild, and whether the player package needs a build or a publish.

## 2026.10.16.5

### Staff
- The Shard Console's Golem tab can open a Claude Code session itself (Wake the Golem, with a model picker), and the Golem opens the next one on its own after a context rest, a model switch or the run cap, runs a night shift from 22:30 to 06:30 when the usage leaves room, and says plainly when it is resting and when it can run again, on the tile, by email and as a Windows notification.

## 2026.10.16.4

### Added
- Pet training. Use Animal Lore on a pet you control and press Begin Animal Training: from then on its battles against wild creatures fill a training bar (the pet says how much it learned from each kill, and the first hour of fighting each day counts double). When the bar is full, Pet Training Options opens the animal training menu: spend the pet's training points on strength, dexterity, intelligence, hits, stamina, mana, regeneration, base damage, the five resistances, higher skill caps (with the matching power scroll in your pack), magical schools, special abilities, special moves and area effects, each with its cost and its limits. Every pet starts at the minimum control slots for its kind and rises one slot per training level up to its maximum, asking for more Animal Taming as it goes; a real-time tracker shows every pet in training, and a planning mode lets you line choices up before spending.
- Animal Lore's gump has new pages: regeneration, all of a pet's skills with their caps, its pet slots (current and maximum) and the taming skill it asks for, its abilities, and the advancements it has trained.
- Animal trainers mention the new taming techniques when you walk up.

### Changed
- Tameable creatures use the Time of Legends control slots: a dragon is 4 slots (5 fully trained), a white wyrm 4, a nightmare 3, a bear 1 (3 fully trained), and so on through every tameable. Pets already in the world that sat below their kind's new minimum were raised to it once, at the first start on this version.
- A pet that has been trained needs the Animal Taming its training raised it to, not its wild requirement, to be tamed again.
- A pet's trained special moves (Armor Ignore, Bleed Attack, Dismount and the rest) are the ones it uses in combat; pets in training gain Focus.

### Staff
- `[PetTrainTest <type>` spawns a tamed, bonded pet of that type at your feet, its skills scaled as a tame's are. `[add PetTrainingGate` (a pet in training that crosses it is instantly ready to train), `[add PetBondRemoveGate` (bonds a pet at once) and `[add PowerScrollGiver` (a bag of 120 power scrolls for every trainable skill) are the testing items.

## 2026.10.16.3

### Added
- More creatures to find and tame: the blood fox, cold drake, crimson and platinum drakes (each in one of five elements), dragon wolf, frost dragon, frost mite, lion, ossein ram, saber-toothed tiger, triceratops, stygian drake, iron beetle (it mines ore for its owner) and the wild, white and black tigers (pelts when carved), plus the giant turkey, shadow dweller, Viscera, vicious macaws nesting around gold foil sheets, the stone monster, the leather wolf and its pack, the Butcher with his Jack O' Lantern carving kit, and the gargish outcast. Tameables bring their natural abilities (dragon breath, grasping claw, life leech, auras, howls) with them.
- Four Mondain's Legacy peerless bosses: Chief Paroxysmus, the Crimson Dragon, Lady Melisande and the Monstrous Interred Grizzle, with their artifacts, statuettes and pet parrots. A pet parrot placed on a parrot perch in your house lives there, ages, repeats what it hears and takes parrot wafers.
- Old Haven's Drelgor the Impaler, the Cleansing Old Haven quest target, now spawns in the ruins.

### Changed
- The shard's own Old Haven warlord is now Vorlak the Impaler (he was Drelgor); his spawner, loot and the achievement for slaying him are unchanged.

## 2026.10.16.2

### Added
- Cooks and alchemists have the Mondain's Legacy and Stygian Abyss tables: the enchanted apple (lifts every curse) and the grapes of wrath (a burst of weapon and spell damage), the fruit bowl, parrot wafers, and the six Eodon tribal potions (hemolymph concentrate, burn poultice, ambusher's essence, draft of might, trance tonic, prophylaxis) brewed from Eodon's eggsacs, lava berries, perfect bananas, river moss and blue corn, plus the elixir of rebirth that revives a dead bonded pet from Medusa's blood.
- New things to find or be handed: thrown fear essence, shatter and exploding tar potions, tasty treats for pets (three strengths), poppies dust that identifies seeds, a skin tingeing tincture, Valentine chocolates, the Thanksgiving table (sweet potato pie, a basket of rolls, a turkey platter) and chocolate nutcrackers.
- Basket weaving: a Grandmaster Tinker who reads "Making valuables with Basket Weaving" cuts decorative plants into clippings or reeds with clippers (Tinkering 50), softens reeds with scouring toxin, and weaves eleven baskets in the reeds' colour. Plant clippings make plant pigment (Alchemy or Cooking), pigments mix, and pigment with colour fixative makes a five-use natural dye for cloth, leather, metal, books, jewellery and locked-down furniture.
- Raised garden beds (a deed, four shapes) for houses: a seed on a plot raises a mound that grows with no watering and no pests; a potted coffee plant that yields coffee pods.

### Changed
- The begging rewards (cheese, cookies, dates, fish steaks, bread, pizza, sake, a shirt, stew, a turnip, wine, a flower garland and three pitchers) exist, marked "Acquired by begging"; Begging itself does not hand them out yet.

## 2026.10.16

### Added
- Treasure maps are the Forgotten Treasures kind: five levels (stash, supply, cache, hoard, trove) in five themes (artisan, assassin, mage, ranger, warrior), dropped by monsters on every facet for that facet, decoded and dug with Cartography, with a rusty, standard or gold chest by your skill. A dug chest is trapped: pick the lock, use Remove Trap (a failed try wakes an ancient chest guardian), then open it for gold, gems, skill scrolls and themed gear.
- Stealing from a monster's pack in a Felucca dungeon or at a champion spawn can turn up a thief's prize (balms, lotions, a seed of life, a mana draught, a Gem of Salvation), once per creature, from Stealing 100, and trains Stealing past 120 against the creature's fame.

## 2026.10.15.10

### Staff
- The console's Golem tab can start a long run (a cap of prompts, or until the queue is empty, and an end time), tell the Golem to wrap up after the current task or find a stopping point in it, and shows Claude Code's reported plan usage, each queued prompt's title and model, and the last ten finished prompts. The Golem now rests (and emails once) on a usage limit, a stall, a full context window or a prompt that wants a different model, and keeps clear of the 01:30 play-test bot.

## 2026.10.15.9

### Staff
- The player package now ships our own TazUO build (with the shard's client patches) by default, and refuses to build from a client that is out of date with the repo's patches or has no build record; the Player package tab shows which client it will ship before you build. Only the safe-for-work paperdoll art can ship, whatever is in the client folder.

## 2026.10.15.8

### Added
- Bulk orders for six more crafts, on the same 2016 rules as smithing and tailoring: carpentry at Carpenters, fletching at Bowyers, tinkering at Tinkers, cooking at Cooks and Bakers, alchemy at Alchemists and inscription at Scribes. Each craft has its own three offers, its own deeds, small and large, and its own rewards list.
- Guild masters give and take their own craft's bulk orders for members: the Foresters' Union (carpentry and fletching), the Tinkers' Union, the Maritime Brotherhood (cooking), the Order of the Alembic (alchemy) and the Arcane Society (inscription).
- New rewards: the Master Chef's Apron (a bonus to making exceptional items while worn), a keg for powder of fortification, a plum tree for your house, natural dyes in five colours (some for spellbooks only), and a talisman that adds a property to each spellbook you scribe while wearing it. Dr. Spector's lenses and the kotl black rod exist on the shard too, though nothing makes them yet.
- A deed made of a wood (oak, ash, yew, heartwood, bloodwood, frostwood) asks for items made of that wood.

### Changed
- Bulk order books hold the new crafts' deeds, and the filter lists all eight crafts with their metals and woods. The blacksmith metal filters now show only blacksmith deeds.

### Fixed
- The Tailors' Circle guild master now shows Bulk Order Info to its members; before, a member had no way to ask.

### Staff
- Bulk orders part 2 (the six crafts). Rewards still to come are listed in the rewards gump as "not on the shard yet" and cannot be bought: reward title deeds, recipes, smelter's and woodsman's talismans, harvest maps, runic mallets and chisels, cauldrons of transmutation, the fermentation barrel and the automaton and blackrock rewards. Cooked food, potions, scrolls and most furniture cannot be exceptional or of a wood on this shard, so deeds for them never ask for either. A few items ServUO's lists name are not on the shard (sweet cocoa butter, the three-tiered cake, enchanted apples, grapes of wrath, the runic atlas, clippers, the metal container engraver, the nexus core) and are left out of the deeds.

## 2026.10.15.7

### Changed
- Bulk orders at town Blacksmiths, Weaponsmiths, Tailors and Weavers now follow the 2016 rules: you can hold up to three offers per craft, with one more every six hours, and a turn-in pays gold into your bank plus reward points that you spend on a reward of your choice or bank for later.
- The deed shows what it is worth in points, and a button sets whether you are asked to bank, always claim, or always bank.
- Vendors can be bribed to improve a bulk order deed that has not been started.
- Society of Smiths members are paid exactly as before.

### Added
- The Tailors' Circle guild master now gives and takes tailoring bulk orders for members.
- Two new bulk order rewards: the rock hammer and the master craftsman talisman.

### Staff
- Bulk orders part 1 (the 2016 system, smith and tailor). Rewards still to come are listed in the rewards gump as "not on the shard yet" and cannot be bought: reward title deeds, smelter's talismans and harvest maps. The other six crafts' deeds come with part 2. Offers, points and vendors' bribe state save in their own file, BulkOrderSystem.

## 2026.10.15.6

### Changed
- A tamed creature that poisons now does it the way trained pets do: its Poisoning skill decides how often a hit poisons and how strong the poison is, up to Lethal at 100.
- Poisoning hits from creatures show a green flash on the victim.

### Staff
- Pet Training part 1, the ability core: creatures can now carry OSI's special abilities (29), area effects (7) and magical schools, the way ServUO's creatures call them. `[props` a creature's AbilityProfile to see them; a stock dragon lists its breath, a cu sidhe its healing. Nothing in the world uses the new ones yet: the creatures that do come with Creatures part 2, and training itself (points, the training gumps, the Animal Trainer) is part 2 of Pet Training. Profiles save in their own file, PetAbilityProfiles.

## 2026.10.15.5

### Added
- Twenty-nine more creatures now exist on the shard, among them the Night Terror, the Abyssal Abomination, the Arch Daemon, the Dread Warhorse, the Ice Hound, the Orc Chopper, the Skeletal Drake, the Wight, the Treefellow Guardian, the Black Order Master and the Fire Rabbit. They are not placed in the world yet.
- The Black Order Master drops a tiger claw key, the Fire Rabbit animal pheromones and the Treefellow Guardian treefellow wood.

### Staff
- Port list, Creatures part 1: 29 ServUO creatures and three items they drop. None is on a spawner yet; [add any of them by type name. The Wight cannot use Cold Wind yet, so it always uses Mortal Strike. A new overnight bot check (P119-1) fights five of them.

## 2026.10.15.4

### Fixed
- A new player's first login shows the patch-notes bulletin again, with the latest notes for players, even when the newest update only changed things for staff.

## 2026.10.15.3

### Staff
- The Shard Console's tabs scroll when their buttons do not fit (the Golem tab's were cut off), and the GOLEM tile counts the prompts actually waiting in the queue folder.

## 2026.10.15.2

### Staff
- Every open play-test item shows who tests it (You, Bot, Bot then you, You decide, or CC) and how: where, what to do and what a pass looks like, one click away. New Mine, Bot and CC filters, and a count for each in the header.

## 2026.10.15

### Staff
- Chase's Human, Admin, Gargoyle and Elf test shortcuts start our own TazUO build, still logged straight in, with each character's gump layout, macros and hotkeys.

## 2026.10.14.19

### Staff
- The Shard Console's window, taskbar button and dialogs show the same icon as its desktop shortcut.

## 2026.10.14.18

### Staff
- The play-test page marks with a robot every item the overnight bot will try tonight (and, dimmer, the ones it already checked), shows its last bot result with a link to the report, and adds a "Bot tonight" count and filter.

## 2026.10.14.17

### Staff
- Our own TazUO build has a Paperdoll bodies choice in Options (Paperdoll tab) that changes the paperdoll at once; the launcher leaves the choice to it.

## 2026.10.14.16

### Staff
- One dashboard on Haven for Shattered Legacy, Sunflower Custom Gifts and the home lab, set up from the Shard Console's Websites tab with a matching roll back. It replaces the old Homepage on Haven and the game PC's; the print queue now reports its order counts to it.

## 2026.10.14.15

### Staff
- The Shard Console's Websites tab can now move the Sunflower print queue to Haven and back: one button sets it up and moves its orders over (checking every file arrived), one brings it back to the game PC with every edit made on Haven. Until the move nothing changes.

## 2026.10.14.14

### Staff
- The live shard is retired until it returns as a fresh 1.0 world: the Shard Console shows one grey LIVE tile and greys every live button, and nothing checks, probes, emails or deploys to live any more. The test shard is the only shard, and Start fresh is greyed because its accounts came from live.

## 2026.10.14.13

### Staff
- The Play tests, Ideas, Bug reports and Shard status pages can now move to Haven and back from the Shard Console's Websites tab: one button sets them up on Haven, one moves them over (checking every file arrived), and one brings them back to the game PC without losing anything marked on Haven. Until the move nothing changes; after it, each commit carries Haven's results, and the game PC tells Haven every minute whether the Golem and the shards are up.

## 2026.10.14.12

### Staff
- The Shard Console now shows when a shard is stuck waiting for an answer at its console, such as a save it cannot load: its tile turns amber and says the question, and an email goes out once per question. A new Shard question tab says what each answer would do and sends it, backing up the save first and asking for a typed phrase before any answer that deletes, with a Stop button beside them.

## 2026.10.14.11

### Staff
- The play-test page can now run on Haven as well as the game PC: its data can live apart from the notes, a token-protected side door hands its data back for each commit and takes in the game PC's status, and when Cloudflare Access is set up it signs each result, idea and bug report with who made it. Nothing changes on the game PC until it is configured.

## 2026.10.14.10

### Staff
- Our own TazUO build can now be made from a pinned TazUO with our patches, and its first patch lets the server open the World Map, the Journal or the options window for the player. Tutorial stations that teach those windows show a button for each to that client only; every other client keeps the written directions. Players get it when the build ships in the player package.

## 2026.10.14.9

### Staff
- The overnight play-test bot can run every night at 01:30, but only when an open play test it can check has not been checked on the current code. It emails before it starts and again when it finishes or fails, and moves bot transcripts older than 14 days to iCloud Drive after checking every file arrived.

## 2026.10.14.8

### Staff
- The Shard Console has a Websites tab and tile: a light for every site (play tests, status, Homepage, the website, the wiki, Vaultwarden and Nginx Proxy Manager), with open, restart, redeploy and logs for the ones on the game PC, Open NPM, and a one-click deploy of the website or the wiki macros to Haven after a typed phrase.

## 2026.10.14.7

### Staff
- [VBuild opens a Visual Builder palette: search the art by name or number, then paint, fill, erase, wipe, hue and nudge decorations. Every step can be undone, a wipe included, and the undo history survives a restart.

## 2026.10.14.6

### Staff
- The autopilot is now the Golem: the Shard Console says Golem (Wake the Golem, Rest the Golem, a GOLEM tile and tab), it starts when you say "Golem, please work on the shard tasks" in CC, and it no longer misses a prompt that was retried or sends false "stopped mid-task" emails.

## 2026.10.14.5

### Staff
- [CaptureLayout saves a box of the world (up to 64 by 64) as a layout file, and [StampLayout places it elsewhere as decoration, with nudge and undo.

## 2026.10.14.4

### Staff
- The play-test page has a Bugs tab: report a bug with screenshots, from scratch or straight from a play test (or when marking one Fail), and copy the new ones for the bug list. Declining or deleting an idea now asks why.

## 2026.10.14.3

### Staff
- The Shard Console has a Fix test shard tab: diagnose a test shard that will not start, restart it, roll it back to the last build that came up healthy, rebuild a chosen commit, and prune old images. The test shard now reports healthy only once it is listening.

## 2026.10.14.2

### Staff
- Fixed a start-up crash in the Imbuing catalogue.

## 2026.10.14

### Staff
- The Shard Console autopilot tile shows which prompt is running, how far through the run and its steps it is, and when it last did anything; the Autopilot tab adds the run cap, the settings and a button to open them.

## 2026.10.13.13

### Staff
- Status page tracks the ServUO port list instead of every ServUO file.

## 2026.10.13.12

### Staff
- An overnight play-test bot: one command makes a scratch copy of the test world with its own accounts, runs the open play tests a bot can settle three times each, soaks the shard, and emails a morning report; it never marks results, and never touches the live or test shard.

## 2026.10.13.11

### Staff
- The play-test page has an Ideas tab: jot a feature idea mid-session (or from any play test), see the existing feature queue to avoid duplicates, and copy the new ones for the features chat; the count of new ideas shows on the page, the menu, Homepage and the Shard Console.

## 2026.10.13.10

### Staff
- The play-test page, the shard status page and the Homepage dashboard share one menu bar (Play tests, Shard status, Dashboard) with the page you are on marked, from one file; Homepage shows it once sl-play-tests is restarted.

## 2026.10.13.9

### Staff
- The Shard Console's UO look is easier to read: text on the stone sits on a dark backing, tabs and tile buttons are bolder, greyed buttons stay legible, colored status lines are darker, and the status tiles wrap to a second row instead of cutting off words on a narrow window.

## 2026.10.13.8

### Staff
- The Shard Console has a Disconnect VPN button by the PUBLIC DNS tile and on Diagnostics: it ends a WireGuard tunnel, opens Proton VPN with what to click (Proton has no scripted disconnect), or names any other VPN to turn off, then re-checks public DNS and offers to restart sl-ddns.

## 2026.10.13.7

### Staff
- The shard status dashboard says how old its judgment is, lists the CC queue and the play-test counts, and is served on the home network at port 8093 /status beside the play-test page (after its redeploy); the Shard Console, the play-test page and Homepage link to it.

## 2026.10.13.6

### Staff
- The Shard Console has a UO look (stone, parchment, gold and the crest; Plain is the old look, switched in its status bar), a tooltip on every control, and an Action output pane that streams non-interactive actions and log tails inside the window, with Stop.

## 2026.10.13.5

### Added
- Imbuing is now learned. Every account knows the basics (resists, stats, regens and the plain utilities) at a Basic strength; the Artificers' Order sells formula books, one per rank, for Essence Shards, and each teaches your whole account new properties and stronger tiers (Basic, Refined, Full) of the ones it knows.
- The Master Formulary (top rank, after the Order's trial) lets your items carry a sixth imbued property.
- Beyond the books, the strongest Overcharged tiers and rare properties come from quests, commissions, loot boxes and bosses. Overcharged imbuing needs a soul forge: the Queen's Soulforge or an Artificers' hall forge.
- Eleven new custom properties, one per item, each with its own monster part: Bountiful, Prospector, Butcher's Eye and Mastercraft for gatherers and crafters, Time Slip, Shatter, Spell Leech and Bond for fighters and tamers, and Fortune's Favor (better loot box contents) from boxes and bosses. Echo of the Loop and Renowned are learnable now and take effect when their systems arrive.
- Skill bonuses can be imbued: combat and magic skills on jewelry (one per skill group, up to +15, more with Overcharged), crafting and gathering skills on aprons.
- Mule owners with the Imbued Barding Socket can imbue near their mule: up to Refined at II, up to Full with the soul forge bonus at III.
- Two secret achievements for the superstitious.

### Changed
- The imbue menu shows only what your account knows, with one line per category on where to learn more.
- Self Repair is no longer locked to Master Artificers; every account knows it at Basic. It still costs more than any other property.
- Imbue slots and strength now come from what your account has learned, not from your Artificers' standing.

### Staff
- [ImbueKnowledge opens a player's account imbue knowledge to view, grant or revoke tiers; [ImbueKnowledge reload reads Configuration/imbuing.json. Staff place an Artificers' hall forge with [add ArtificersSoulForge.

## 2026.10.13.4

### Added
- New paperdoll art: every body and most worn items on your paperdoll are redrawn (the UO Paperdoll Project, Siege of Sosaria, by Zigholtul), with sharper spell icons. It installs with the next player package update; nothing to do on your side.

### Staff
- The art ships as gump PNGs in the player package (vendor\paperdoll-sos, SFW only), checked by the package build.

## 2026.10.13.3

### Added
- New characters are met in New Haven by a League recruiter, who runs the new player tutorial: a short core (moving, your paperdoll and backpack, chat, death, the bank, travel, the League and a vision of the loop), then optional wings for every kind of skill.
- The tutorial stations are placeholders for now: each shows what it will teach and counts as done.
- You can skip the tutorial. The System will ask twice, and remember. Skippers can come back for remedial training any time.
- Graduating gives you your first loot box and your choice of a starter pet: a dog, a cat, a fox or a bird.
- Two new achievements: "Didn't Read the Manual" and "Read the Manual Eventually".

### Changed
- Young status now also ends at your first real loop. The tutorial's vision of the loop does not count.

### Staff
- Place the recruiter with [ClusterFSeedTutorial (dry run first; test shard first). [Tutorial status|enter|station|skip|remedial|graduate|reset targets a player (test shard). tutorial.enabled turns new entries off.

## 2026.10.13.2

### Added
- Loot boxes. The first time you earn an achievement, slay a boss, or reach a League rank, you get a box, from tier 1 to tier 6 by how hard it was.
- Boxes come in themes: Miner's, Woodsman's, Artisan's and Hunter's boxes are named for their metal, wood or leather (a Valorite Box, a Barbed Box); the rest run from Common to Celestial.
- What is inside is decided the moment you earn it, so holding a box never changes it. Open it in a guarded town or a house where you are an owner, co-owner or friend.
- Boxes are bound to your account, and so are the cosmetics inside. Everything else inside can be traded.
- A fanfare plays when you earn something: fireworks and a word from the System, which has opinions. The bigger the deed, the bigger the show, and a few deeds are announced to the whole shard.
- [FanfareMute turns your own fanfare off. Shard-wide announcements still show.

### Staff
- [LootBox give <theme> <tier>, [LootBox reload, [LootBox cleanup, and [Fanfare <minor|major|capstone> (test shard). Numbers in Configuration/loot-boxes.json, lines in Configuration/fanfare-lines.json.

## 2026.10.13

### Added
- Crafting now has recipes. Anything that needs 30 skill or less is always known. Higher items stay out of the craft menu until your account learns them.
- Pattern books teach a whole band of a craft at once: Band I (30 to 50 skill), II (50 to 70), III (70 to 90) and IV (90 to 100). Designs above 100 come one at a time on recipe scrolls.
- Everything you learn is learned by your whole account, for every character and every loop. Unread books and scrolls can be traded.
- The craft menu tells you what you are missing: "4 more patterns: Society of Smiths, rank Apprentice." Click the line to see where to go.
- Each crafting guild sells its pattern books for guild currency, one band ahead of your rank, and from rank 4 a daily selection of Band V scrolls.
- Rising in a crafting guild hands you that rank's pattern book. Large bulk orders have a small chance at a Band V scroll.

### Changed
- Crafting guild ranks now need the guild's craft skill as well as standing: 30 for the second rank, then 50, 70, 90 and 100. A rank you have reached is never lost, even when a loop resets your skills.
- Mule module schematics are learned once by your whole account. Tier III schematics now need guild rank 4.
- Recipes you learned before, and the Masonry and Glassblowing books, now count for your whole account.

### Staff
- [Recipes (list, grant, revoke, learnall, forgetall) targets a player's account. [RecipeGating lists the bands and books per craft; [RecipeGating reload reads Configuration/recipe-gating.json. crafting.recipeGating.enabled turns gating off.

## 2026.10.12.10

### Added
- A new guild, the Order of the Alembic, teaches Alchemy and Taste ID. Its guildmaster keeps the Little Shop of Alchemy in New Haven, and joining is free.
- Joining the Order hands you an alchemist's starter kit: empty bottles, a mortar and pestle and a robe.

### Changed
- The Healers' Covenant now teaches Healing and Anatomy. Alchemy and Taste ID moved to the Order of the Alembic; your standing with the Healers stays where it is.

### Staff
- Place the Order's guildmaster on an existing world with [ClusterFSeedGuildHalls (dry run first), and its hall props with [ClusterFSeedAlembicHall (dry run first). Test shard first.

## 2026.10.12.9

### Added
- Pack mules take modules: seventeen of them, one from each guild, three tiers each, fitted by dropping one on your mule. A mule carries as many as its tier.
- Each guild's hall sells the schematics for its module; a schematic teaches you the recipe for good, and you craft the module in that guild's craft, with a monster part. A higher tier is built on the tier below it.
- Ore satchels from the Surveyor's on, and the top lumber and hunting satchels, can unload straight into your mule.
- Your mule's new Options menu takes modules off, tracks your treasure maps, trashes and salvages, sells cargo and opens your bank, as its modules allow.

### Changed
- Mule traits now strengthen the matching modules (Emberblood the smelter, Stoneback the barding, and so on).

### Staff
- [add MuleModule <module> <tier> makes any module. Steady Hoof Wraps can be crafted and fitted but does nothing yet; the Field Medic Pack's ranged bandaging, the Clockwork Scavenger and imbuing the Barding Socket are still to come (their tooltips say so).

## 2026.10.12.8

### Added
- Pack mules now come in five tiers, from the Compact mule to the Celestial mule: each tier is tougher, carries more and wears its metal's color.
- Every mule has a bloodline: how well it rolled within its tier, up to three inherited traits, and a family name. Strong parents pass their traits to their foals, and Animal Lore above 120 helps.
- The League Registrar has a new errand, The Compact's Mule: League citizens of Dull Copper rank or higher can earn a bonded pack mule from the Miners' Compact.

### Changed
- A pack mule now takes one follower slot instead of two.
- The Civic Mule Exchange no longer sells mules. It raises your mule's tier instead, and its first step costs what a mule used to.
- A foal deed shows the foal's sex, quality, traits and family before you use it.

### Fixed
- A foal no longer goes missing if the server restarts during the 24-hour wait.

### Staff
- [add PackMule <tier> makes a mule of that tier; pack mule deeds remain for staff and rewards only. Existing mules became Shadowhide (tier 3) mules with their old strength.

## 2026.10.12.7

### Staff
- Evo pets are on the test build: [EvoPet give dog (or cat, fox, bird) and target a player to give them a pet that grows as it fights, tamed and bonded, with a color to pick. [EvoPet stage, xp, bond, loops and info walk one through its stages; nothing in the world hands them out yet.
- Several newer animal bodies (the big dogs, the tigers, the lion, the sabertooth, the manticore, the small fox, the leather wolf, the phoenix bird, the windrunner) now count as animals, so creatures on them no longer open doors.

## 2026.10.12.6

### Staff
- The chest mimic is on the test build: [add ChestMimic (level 1 to 6, default 4), or [MimicChest <level> and target a chest on the ground to turn it into one on the spot. It never leaves its tile and shuts its lid against attackers it cannot reach. Players need the new package to see it; without it they see the chest only until it wakes.

## 2026.10.12.5

### Added
- The Burrows beneath Old Haven are being dug on the test shard: a dungeon whose map is still to come, with its own rules already in place.
- No spell or scroll takes you into the Burrows, and no rune can be marked or gate opened inside them; walk in, and recall or walk out.
- In the Burrows no more than three monsters at once can set on a Young player.
- Four starter materials for crafters around 45 skill: Dawnstone ore and Rootwood, found only in the Burrows, and Burrowhide leather and Gloomsilk cloth, which the Burrows' creatures will carry.

### Changed
- Play Shattered Legacy now needs Ultima Online 7.0.117.0 or newer. An older install waits for EA's launcher to update it, as a new one does.
- Ironwood, Ghostwood, Emberbark, Frostbark, Shadowbark, Runewood, Voidwood and Starwood logs and boards now show their own color and name.

### Fixed
- A Grandmaster stone miner digging Platinum through Celestial now turns up that metal's granite, which masons can work, instead of nothing.
- A Hunter's Satchel no longer turns hides it does not know into plain leather.

## 2026.10.12.4

### Added
- From December 1 to January 6, New Haven dresses for winter: a Christmas tree with presents and an animated Father Christmas in the square, candy cane arches at the town gates, wreaths and bells on the lamp posts, a feast and a crackling fireplace at the Bountiful Harvest Inn, and a sleigh waiting on the pier. Update the game when the launcher asks, or the new decorations show as "UNUSED".

### Staff
- [ClusterFWinterDecor status, up and down (each with dryrun) show, put up or take down the winter decor; it goes up and comes down by itself on the dates, and up works early on the test shard.

## 2026.10.12.3

### Staff
- A play-test page lists every in-game check waiting to be tried, fills itself as work finishes, and takes Pass, Fail or Later from any device at home; its counts show on the dashboard and the Shard Console.

## 2026.10.12.2

### Staff
- Shard Console: an AUTOPILOT tile and an Autopilot tab show what the CC autopilot is doing and turn it on or off, set up its email and send a test email.

## 2026.10.12

### Staff
- Shard Console > Test shard > Wipe test accounts: deletes every test account and character, with all they own, keeps the world, and makes one new Owner account; it backs up the test save first and never runs on live.

## 2026.10.10

### Changed
- Nerve Strike's paralysis is never certain: 90% at Bushido 120, rising to 95% at 200.
- Stable slots from Animal Taming, Animal Lore and Veterinary rise to 23 at 200 in all three (14 at 120).
- Fire Horn damage rises to a tenth more than at 120 when all four bard skills reach 200.
- Stealth steps rise to 34 at Stealth 200 (24 at 120).
- A bandage heals about 72 on average at Healing and Anatomy 200 (about 57 at 120); Veterinary and Animal Lore on pets the same.

## 2026.10.07

### Added
- Carpentry, Bowcraft/Fletching and Tinkering now climb to 200 by crafting, the way Blacksmithy does: crafting in a shard wood or metal can raise your skill past an item's normal top, up to the skill the next wood or metal needs. Cutting logs into boards, or anything that does not use up the wood or metal, still teaches nothing.
- Tinkers can work Platinum through Celestial ingots, at the same skill smiths need for each.

### Changed
- The eight shard woods now need rising Carpentry and Bowcraft/Fletching skill: Ironwood 100, Ghostwood 115, Emberbark 130, Frostbark 140, Shadowbark 155, Runewood 170, Voidwood 185, Starwood 200.
- Past an item's normal top, harder items now teach faster: a plate tunic trains a smith better than a dagger in the same metal.
- Worn skill bonuses no longer stop crafting gains early: a shard metal or wood teaches until your own skill, without the bonus, reaches its limit.
- With "Orders that still teach me" on, the Society of Smiths' post-Valorite orders come in a metal that still teaches you, so far more of them do.
- Tinker traps stop growing in damage past Tinkering 120, though they keep getting harder to disarm.
- An axe's Lumberjacking damage bonus stops at 30% (Lumberjacking 100); potion strength from Alchemy stops at 30% (Alchemy 100); the chance of deep-water finds while fishing stops growing at Fishing 120.

## 2026.10.06

### Added
- Hiding, Stealth, Detect Hidden, Snooping, Stealing, Poisoning, Healing, Veterinary, Begging, Forensic Evaluation, Animal Lore, Musicianship, Tracking, Taste Identification, Camping, Resisting Spells and Bushido no longer stop gaining at 100 or 120: past that point they keep rising with use, up to your skill cap. Below it they train exactly as before.
- Detect Hidden past 100 gains only when a search finds something: a hidden player or creature, a trapped chest or container, or a hidden trap.
- Animal Lore past 120 gains on creatures that are hard to tame, the same ones Animal Taming trains on; easy animals teach nothing more.
- Stealth past 142 gains in any armor, and Stealing past 127.5 gains only on the heaviest (10 stone) thefts.

### Changed
- Skill bonuses on worn items no longer stop a skill gaining before it reaches its cap.
- A thief is caught as often as at 120 Stealing, however high their skill or gear.
- Hiding above 100 no longer lets you hide right next to a fight; you still need to be more than 8 tiles from anyone fighting you.
- Detect Hidden searches up to 10 tiles at most, the same as at 100.
- Musicianship above 120 no longer makes creatures easier to calm, provoke or discord.
- The chance to block a hit tops out at 60 percent.
- Tracking searches up to 110 tiles at most, the same as at 100.
- Resisting Spells above 120 no longer turns Nether Cyclone into a gift of stamina and mana.

## 2026.10.05.3

### Added
- [version opens the full patch history: every update, newest first, a page at a time. The login notes have an "All patch notes" button that opens it.

### Changed
- Long patch note lines now wrap onto a second or third line instead of being cut off at the edge.

### Staff
- Shard Console: the Player package tab can check any built zip against the package gates, a declined publish now says it was cancelled, and the Commit tab waits 60 seconds after the last change instead of 5 minutes, counts down to when it can commit, and has a Force box that skips only that wait.

## 2026.10.05.2

### Changed
- Crafting in Platinum to Celestial now raises Blacksmithy past the item's own limit, up to the next metal's need: Platinum to 125, Toxic to 137.5, Blaze to 150, Frost to 162.5, Obsidian to 175, Mythril to 187.5, Adamantium and Celestial to 200. The chance to make the item is unchanged.
- "Orders that still teach me" counts the order's metal: a Platinum to Celestial order still teaches until your Blacksmithy reaches that metal's limit.

### Fixed
- Earned achievements no longer show a progress line.
- Platinum to Celestial smith deeds now say which ingots they need, on the deed, the offer and the tooltip. Take the launcher's update to see the line.

### Staff
- Shard Console: a Player package tab builds the player zip and publishes a chosen one, and the Test shard tab can put an image a prompt already built on the test shard. Publishing and deploying each ask for a typed phrase first.

## 2026.10.05

### Added
- Society of Smiths members choose what happens to a finished smith deed: always bank it as Smithing Seals, always cash it out, or ask each time. Set it at the guildmaster or in the Smithing Guild Book.
- Single-click the Hammer of Hephaestus and choose Metal Familiarity to see it, anywhere.
- The shard has its own menu words: Breed on a pack mule, Smelt Ore on the ore satchels, Metal Familiarity on the Hammer of Hephaestus. Take the launcher's update to see them; without it those menu rows are blank.
- The Society guildmaster's Bulk Order Info now asks whether you want a small or a large order.

### Changed
- Each skill now caps at 200. Nobody loses skill they already have.
- Platinum to Celestial now need 112.5 to 200 Blacksmithy, one step of 12.5 per metal, so Celestial can be worked at the 200 cap. Smith orders, commissions and the salvage bag follow the same steps.
- Platinum to Celestial ore now smelts and mines at the same steps, from 112.5 Mining for Platinum to 200 for Celestial. Below a metal's step, its vein gives iron.
- Skill bonuses from items and from temporary effects now count above the skill cap: a +15 ring on a capped skill reads 15 over the cap. Skill gain still stops at the cap.
- Armor no longer stops meditation from 150 Meditation (was 200).
- Any town blacksmith or weaponsmith now asks whether you want a small or a large bulk order. Large needs 70.1 Blacksmithy.
- Society of Smiths members are paid the guild's way for smith deeds at every smith, town blacksmiths included.
- Banking a smith deed with the Society now pays far more Smithing Seals, always at least what a non-member gets from a town smith in gold and reward items.
- Metal Familiarity on the Hammer of Hephaestus grows more slowly and steadily: up to 1,000 per metal (2,500 on the Reinforced hammer), with an exceptional piece counting 3.
- The Hammer of Hephaestus gives its Blacksmithy bonus only while you hold it.
- Achievements now say plainly what earned them, after their usual line.
- Six skill achievements that needed skill above 200 are retired. If you earned one, you keep it, with its points, in a Retired section.
- Ore satchels and Jacob's pickaxes are colored by tier, following the ore ladder.
- Craft menus show material names as "Iron", "Dull Copper" and so on, the same style as the shard's own metals.
- The smith guild is called the Society of Smiths everywhere.
- Seal catalog prices are three times what they were. A banked deed still pays the same Seals.
- Banking a Platinum to Celestial deed pays more Seals, more for each higher metal.
- The achievements window puts a row's Earned, Requires and Progress lines each on a line of its own, shows its tabs as full words on two rows, and is no longer see-through.
- Smelting from an ore satchel now works exactly like the forge (the Reinforced Ore Satchel gave about a quarter of the ingots), the ingots go into the satchel, and the Surveyor's, Deepdelver's and Master Expedition satchels can smelt too.

### Fixed
- The game now starts on a computer with no OpenGL 2.1 graphics driver: the launcher starts it again with its automatic graphics driver.
- The smith guild's menus (Seal catalog, Bulk Orders, Commissions, Hammer upgrades, Guild Contracts, Mule Exchange) have a working Back button, and the Seal catalog's note no longer covers its Close button.
- Blacksmithy and Inscription menus page their categories instead of running into the notices box.
- Achievement rows no longer run into each other.
- A thief can no longer take a whole heavy pile at high Stealing: a steal takes at most 10 stones' worth, as it always did up to 100 Stealing.
- Menu labels: satchels no longer say "Salvage Ingots" (the ore satchels say "Smelt Ore"), the trash bag says "Clean Up Britannia", the pet mimic says "Status", and a pack mule's breeding entry says "Breed".

### Staff
- Staff Hub: solid background, long values wrap, Green Acres is in the travel list, and you can jump to the same spot on another facet.
- Shard Console: a Commit tab previews the shard's uncommitted work and commits and pushes it, after a typed confirmation.

## 2026.10.04

### Added
- League ranks: seventeen of them, one for each metal from Iron Citizen to Celestial Citizen. The League Registrar's Rank page shows what the next rank needs (lifetime Renown and guild ranks) and promotes you when you have it.
- Each milestone League rank adds 100 items to your bank box.
- The Guild Directory shows the League at the top, with your League rank and a "Show me the way" button to the Registrar. The guild welcome page mentions the League too.
- Raptors hunt in packs: attack one and two more come to its aid. Raptors can also drop ancient pottery fragments.

### Fixed
- The League Registrar now talks to passers-by.
- The Guild Directory shows the Miners' Compact's own rank names.

### Staff
- [SL opens the Staff Hub: every staff command by category with dry runs, a player panel with grants, go to and bring, and travel to shard places and saved spots.
- [LeagueLadder reload reads the League rank table again. The dev reset's League and Renown options also clear League ranks and lifetime Renown.

## 2026.10.03

### Added
- The Society of Smiths' Bulk Orders button works from anywhere and lets you choose a small or a large order. Large needs 70.1 Blacksmithy and no guild rank. The Smithing Guild Book does the same.
- "Orders that still teach me" (on by default): the Society's orders and commissions ask only for items that can still raise your Blacksmithy. Turn it off on the Bulk Orders page.
- Large smith deeds can be filled with crafted items directly, as well as with small deeds.
- The wiki has a Macros page: a Mining macro and a Retaliate helper for TazUO, ready to download.

### Changed
- Society of Smiths bulk orders pay twice the standing. Commissions pay three times the Seals and twice the standing.
- Runic hammers from Gold up cost more Seals in the catalog.
- Imbuing Self Repair needs Master Artificer and costs more than any other property.
- Trammel Despise runs only the revamped dungeon: the old ettins, lizardmen and elementals are gone and its chests stay. Felucca Despise is unchanged.
- The installer waits for EA's patch to finish instead of stopping.

### Fixed
- A shrunk pet now stays in its figurine across a logout or a restart, follows you when restored, and counts once toward your followers, even if another player restores it.
- Possessing a creature with a Wisp Orb no longer leaves you attacking it, and logging out in Despise no longer destroys the orb.
- You no longer attack your own possessed creature in a fight, and its death no longer costs you karma.
- Platinum to Celestial smith deeds take only items of their own ore.
- The Hammer of Hephaestus gains Metal Familiarity when you craft iron without picking a material.
- The game starts on a computer without Microsoft's Visual C++ runtime.

### Staff
- The Old Haven cleanup leaves stock spawners and New Haven's own placements alone. A Despise cleanup command removes the old Trammel Despise spawns.

## 2026.10.02

### Added
- Clean Up Britannia: throw unwanted items in a trash barrel (your house's counts too) for points, and spend them at the Clean Up Britannia store through a Cleanup Officer or the Sanitation Warden.
- The Fountain of Fortune stands in Ter Mur, with stepping stones out to it.

### Changed
- The Custodians rank by Clean Up Britannia points. Civic Tokens are retired, Civic Contracts pay gold only, and each character gets one free trash bag.
- Imbuing takes essences from your pack first, then the Artificers' Essence Satchel, then your bank, and says where they are.
- Each tree keeps the same wood, so a grove in your logging book always leads back to that wood.
- Self Repair is now very rare on loot: about 1 in 500 magic weapons and armor from bosses, champions and paragons.
- The League Registrar's field office has tidier decor.

### Fixed
- Starwood can be chopped and Celestial ore can be mined. Neither could be found before.
- Talismans, quivers and the Pet Mimic no longer stack their Strength, Dexterity or Intelligence bonus each time they are put on.
- Meditation no longer gets harder to start as your Intelligence rises.
- Extracting a property now lowers an item's current durability along with its maximum.
- Essences in the Artificers' Essence Satchel can be used to imbue.
- The Imbuing Table's menu says "Imbue Item" instead of "Salvage Ingots".
- Cleaning up litter no longer takes a player's corpse.

### Staff
- [GuildStanding sets a character's guild standing on the test shard.
- Running the spawner import again no longer removes other spawners that share a tile.
- [ClusterFPlaceCleanUp places a Cleanup Officer and a barrel; [ClusterFSeedRegistrarOffice replace swaps in the new office decor.

## 2026.10.01

### Added
- Wren, a Thieves' Den lookout, sits by the New Haven fighting pit and points the way to the guildmaster.
- "Combine this deed with contained items" on bulk order deeds: target a bag in your pack to combine everything in it.
- Your bank now holds 1,000 items.
- Patch notes: new updates show here when you log in, and [version shows them any time.
- The Reset Stone in New Haven is now a floating, shattered runestone. Accept the game update when it is offered to see it.

### Changed
- Guild upgrades, restorations and turn-ins now use materials from your bank as well as your pack, pack first.
- Restoring a Jacob's Pickaxe at tiers 1 and 2 costs much less.
- The Smith Seal catalog no longer sells power scrolls.

### Fixed
- The Guild Directory listed the Artificers' Order twice.
- "Show me the way" now points to the nearest guildmaster and names the town.
- Salvaging a smithed item can no longer return more ingots than it cost.
