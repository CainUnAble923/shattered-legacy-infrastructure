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

## 2026.10.20.19

### Added
- Field maintenance: use Arms Lore on a damaged weapon or armor of your own (in your pack or worn) and, when you size it up, you also restore some of its durability without a forge (more with more skill; once every 10 seconds). Below 200 skill it can cost a point of maximum durability. A smith's repair still restores it in full.
- Arms Lore can now gain past 100: inspecting, maintaining and crafting better gear (finer materials, exceptional, magic) is what trains it toward 200.

### Changed
- Arms Lore matters more and trains to 200. An exceptional weapon's damage increase now grows to 50% at 200 Arms Lore (42% at 100), and an exceptional armor piece's extra resists from Arms Lore stop at 10 points.
- Your Arms Lore makes your own weapons, armor and shields wear more slowly in a fight: a quarter less at 100, half less at 200.
- Smiths with Arms Lore lose maximum durability less often when they repair weapons and armor with a tool.

### Staff
- All the numbers live in ClusterFArmsLore; small call-site patches to BaseWeapon, BaseArmor, BaseShield, ArmsLore and Repair. Arms Lore on gear is a hook G row past 100, keyed on the item's tier (material requirement + 25 exceptional + 25 magic, window +/- 50).

## 2026.10.20.18

### Staff
- The Golems' night shift now works in batches: from 22:30 it checks the usage every half hour and just after each 5-hour reset, and runs a batch of prompts whenever the week has room under its pace, each batch ending at a stopping point once its share is spent, instead of skipping the whole night when the first check found the week a little ahead. One email when the first batch starts, and the morning summary lists each batch; the console's Golem tab shows the running batch and its budget, or the next check and why.

## 2026.10.20.17

### Staff
- The web console's Golem cards glow behind each portrait so you can tell from across the room what a Golem is doing: a steady pulse in its metal while it works, slower and dimmer when it has been quiet for 10 minutes, a faint ring while it waits, a fast amber pulse when it needs you or has stalled, and no glow (faded) when it is off or offline. The "?" by Golems explains them.

## 2026.10.20.16

### Added
- The Rootdeep Burrows are in the world: a hole in the floor of Old Haven's ruined Second Bank (east of New Haven, through the collapsed stretch of its south wall) drops you onto the Landing, and a rope ladder on the Landing's north wall brings you back up beside the hole.
- Trap training kits, a new way to learn Remove Trap: the circuit kit (walk the circuit; the grid grows with your skill), the cylinder kit (find the five cylinders; a wrong guess does not hurt) and the slider kit (slide the picture back together). Use Remove Trap on a kit to try it. The Collector of Oddities in Moonglow sells all three.
- Carpenters can make the chicken coop, a house stable for up to three chickens.
- Kotl automatons: a grandmaster tinker can bring a Blackrock automaton head (a tinkering bulk order reward) to life as a pet.

### Changed
- The town crier who called the news in the Old Haven ruins is gone. The skeletons by the bank stay.
- Remove Trap no longer needs 50 Lockpicking and 50 Detect Hidden before you can use it, as on the official shards since Endless Journey.
- The tinkering bulk order rewards now include the automaton actuator, the Blackrock moonstone and the Blackrock automaton head, and the carpentry rewards include the runic mallets and chisels.
- Treasure map supply chests can hold the mana phasing orb, and the Assassin's chests the skeleton keys. Clean Up Britannia now takes the mana phasing orb and fey wings.

### Staff
- The map changes ship in the player package as a patch of our own changes (app\trammel.mapops, applied to copies of each player's own files at launch; nothing of EA's ships). The server reads the same map from client-data\sl-map-overlay, built by docker\uo\Build-MapOverlay.ps1 and mounted first in the test shard's data folders; live gets the same two lines at a restart you plan.
- [ClusterFSeedRootdeep also places the two way-in teleporters (the hole, the ladder's foot). [ClusterFOldHavenRetire also removes pinned's town crier spawner in the ruins; a fresh world never places it.
- [ClusterFPlaceCollectorOfOddities [dryrun] places the Collector of Oddities (the trap kit vendor) at ServUO's spot in Moonglow, Trammel 4305,1003. Dry run first, test shard first.
- About eighty more ServUO base items now exist for staff and for later reward tables: resources (Blackrock, blood of the dark father, fey wings, void essence, abyssal cloth and others), tools (skeleton keys, the metallic dye tub, the pen of wisdom, the chest of sending, the greater bracelet of binding, forged metal of artifacts, the snake charmer flute, the scale collar), house items (the jewelry box, the seed box, Coral the Owl, the coffee grinder, the serpent's jawbone, sliding doors, the house craftables) and the quest reward containers. Most have no player source until their quests, vendors or rewards are ported.
- Not ported, each needs a decision or its own system: the five peerless altars, the wall safe, Book of Masteries, the Primeval Lich puzzle, the ethereal retouching tool, the dragon turtle egg and the focusing gem. Details in the cc-P199 notes.

## 2026.10.20.15

### Added
- Wrong dungeon, revamped (Trammel and Felucca): the level-3 maze with its invisible walls and lockpickable doors, the prisoners' bedrolls that hide traps, soap and mysterious tunnels out of the cell blocks, the Demonic Jailor who drags you back to your cell, Fezzik the Ogre Cook and his gooey maggots, archmages, brigand cannibals, lizardman squatters and defenders, hungry ogres, savage pack wolves, prison rats and Wrong cave trolls, and the seven hot-item chests whose stolen loot burns until you leave the dungeon.
- Wrong prisoners offer the escort quest "We Who Are About To Die": walk one to the Wrong entrance for 500 gold and compassion.
- Covetous Void Spawn: the battle for the Void Pool at the bottom of Covetous, waves of void creatures (abyss, repond, undead, reptile or elemental) marching on the pool, a scoreboard and record wave, and Void Pool reward points spent at Vela in Cove for cauldrons of transmutation, a temporary forge, the Bracelet of Protection, Hephaestus and the Blight of the Tundra. Covetous level 3 is held by leveled void creatures, levels 1 and 2 by strange gazers, headless miners, dazzled harpies and vampire mongbats, and Cora the sorceress rules the dungeon.
- Void artifacts: Prismatic Lenses, Brightblade, the Bracelet of Protection, the Blight of the Tundra and Hephaestus, from the void spawn and from Cora.

### Staff
- Design notes written for rolled Mining and Lumberjacking contracts (tier values, five contract types, roll ranges, turn-in windows); no player change yet.
- [GenWrongRevamp and [DeleteWrongRevamp build and remove the Wrong revamp; [SetupNewCovetous and [DeleteCovetous the Void Pool. Both are test-shard commands until Chase runs them live.

## 2026.10.20.14

### Staff
- Design notes written for more ways to be knocked off a mount (big monsters, low-ceiling dungeon spots, traps, charging wildlife); nothing changes in play until Chase answers them.

## 2026.10.20.13

### Staff
- [props on a creature now opens its pet-training Ability Profile and Training Profile with the row's arrow, instead of showing "..." with no way in.

## 2026.10.20.12

### Staff
- The server build checks its packages for security advisories again: the one package that tripped it has been moved to a fixed version, so the setting that waved moderate and high advisories through is gone.

## 2026.10.20.11

### Staff
- The Fountain of Youth makes a drinker's whole account Young again (two more weeks of play, the tutorial and the New Haven trainer quests start over); skills, items and everything else are kept. On the test shard it stands in New Haven; elsewhere it moves after every drink and weekly, and nothing can be marked near it. [ClusterFSeedFountainOfYouth [dryrun] places it; [WhereIsFountain, [GoFountain and [MoveFountain find and move it. Its spots and rules are in the admin notes only: never put it on the wiki or in player notes.

## 2026.10.20.10

### Staff
- The Shard Console's Golem tab lists the queue in run order with each prompt's one-line note; Build package suggests the "What's new" line from what changed for players since the published package; the header shows the console's own icon; "Poke the Golem" opens a session with the model picked for you (Auto); every control has a hover tooltip, saying why when greyed out; and the running prompt shows its step, a bar and a time estimate.

## 2026.10.20.9

### Added
- High Seas fishing, the first half. Fishing now brings up the High Seas catch: from 50 skill the shoreline gives twelve new shore fish, from 80 deep water gives eighteen ocean fish, from 106 dungeon water gives twelve dungeon fish, and the rare fish of each place (sixteen rare, nineteen legendary, each with its own hue, caught by one fisher and weighed) turn up with the skill the wiki says. Every fish carves into fish steaks, the rare ones into their own.
- Lobster traps: throw one into water (or lava, for the lava trap), it bobs once a minute, trains Fishing, and fills with crabs and lobsters, now and then a rare one; pull it back before the sea takes it. Guild and party may pull yours; in Felucca anyone may, as a crime.
- Fish trophies: a taxidermy kit mounts a rare fish, crab or lobster as a wall trophy in your house, hued as the fish, showing who caught it, when and how heavy; chop it to get the deed back.
- Two sea champions for staff to place: Charybdis (tentacles, lightning on your boat, a wave that pushes it) and Osiredon the Scalis Enforcer (water balls, fire bursts, parasitic eels), with their artifacts: the Fisherman's set (hat, vest, eelskin gloves, trousers), the Helm of Vengeance, Ring of the Soulbinder, Rune Engraved Peg Leg, Culling Blade, Enchanted Coral Bracelet, Wand of Thundering Glory, Leviathan Hide Bracers, Smiling Moon Blade, the enchanted blade and vortex deeds, and the yellow polka dot bikini.

### Staff
- Baits and hooks, the Oracle of the Sea and the charybdis spawner, lava fishing, fish pies, the profession fish quests and Corgul are the second half; the sea champions cannot damage a boat until ship combat is ported. Details in the cc-P193 notes.

## 2026.10.20.8

### Added
- Old Haven is an apprentice training ground again, as on the official shards. While you are on a New Haven trainer's quest that sends you there (Magic Resist, the fighting skills, Magery, Healing and the others), you learn that skill two to four times as fast in the ruins, until the quest is done at 50. Your trainer's words and an "Accelerated Skillgain" icon tell you when you are in the right place.

### Changed
- Old Haven is safer for new adventurers on their way to Rootdeep: the Old Haven mages and Vorlak the Impaler have left the ruins. The undead and Drelgor the Impaler remain.

### Fixed
- Drelgor the Impaler now issues his challenge to those in the ruins, as he should.
- Old Haven's Least Welcome Regular (slay 50 creatures in Old Haven's ruins) can now be earned.

### Staff
- [ClusterFOldHavenRetire [dryrun] removes the Old Haven Mage and Vorlak spawners (and their creatures) from a world that already has them; [ClusterFSeedOldHaven no longer places them. The classes, and their achievements, are kept for a new home.

## 2026.10.20.7

### Staff
- Away mode: tell the Golems you are away until a day and time (on the Golem tab, the web console or by email) and they keep working around the clock without spending next week's usage. A prompt that stops for a decision is parked so the others carry on, and a short digest arrives each morning.
- Email commands: reply to any Golem email with one line (status, resume, wrap up, stopping point, away until..., away off, night shift on or off, run next, skip, unpark). Only your own Gmail, with the code from that email, is obeyed.

## 2026.10.20.6

### Added
- The player wiki catches up with the shard: new pages for achievements, loot boxes, Young players, the guilds, the League, requisitions and the Appraiser, Clean Up Britannia, pet training, pack mules, the shard's metals and woods, recipes and pattern books, bulk orders, imbuing and the Artificers' Order, and stealing from monsters; the skill gain, accounts, New Haven and new player pages are brought up to date; and draft pages for the Rootdeep Burrows, the tutorial and the Test Crystal wait in a drafts section until those go live. The pages are in the repo's wiki seed and reach the wiki with its next deploy.

## 2026.10.20.5

### Changed
- Pets learn their skills faster for a seasoned owner: Animal Taming and Animal Lore above 100, each loop and each Rangers' League rank add to it, up to three times as fast. Animal Lore on your pet shows its learning speed on the skill pages.

### Staff
- Pet learning speed settings: clusterf.petGain.perLoop, perSkillPoint, perRank and maxMultiplier (defaults 0.1, 0.004, 0.1, 3.0).

## 2026.10.20.4

### Staff
- Shadow Iron, a Golem that only merges: every 10 minutes it merges the other Golems' finished work into the shard, oldest first, one build at a time, whatever the other Golems are doing. A branch that will not merge goes back to its Golem once; if the fix fails too, it comes to you. Its card on the Golem tab (both consoles) shows the last round, what is waiting, the last merge and failure and whether the folders are clean, with Merge now, Pause merging and Resume merging.
- The Iron Golem now works on its own branch like the others.

## 2026.10.20.3

### Added
- The League runs a requisitions board. Every guild posts three requests a day for a magic item it wants: a sword with Hit Chance for the Warriors, a spellbook with Lower Mana Cost for the Arcane Society, an undead slayer for the Keepers, a slayer instrument for the Bards, and so on. Found or crafted both count. Turn one in at the board in the New Haven field office, at the guild's New Haven chapter (the guildmaster's Requisitions button), at the League Registrar or at the Appraiser, and the item is taken for Renown, that guild's standing and gold. Unfilled requests simply roll over at 05:00 UTC; nothing is lost and nothing is owed.
- Requests grow with your rank in that guild, and your League rank sets how high they can go.
- The Appraiser, a League clerk in the New Haven field office and by the banks of Britain, Trinsic, Minoc, Vesper, Moonglow, Skara Brae, Luna and the Royal City, buys any magic item no request wants. The price follows how many properties the item has and how strong they are, near what a shopkeeper pays. Drop an item on the Appraiser for a quote, or ask which of your requests wants it.
- Leads: some requests come with a rumor naming a place and a kind of creature. For three days, kills there give you an extra chance at that item, and it is certain by about the fortieth. A Lead that runs out just fades.
- [requisitions lists your open requests, for the combat script's loot helper.

### Staff
- Configuration/requisitions.json holds every number of the board, the Appraiser and Leads (tiers, the League gate, each guild's themes, areas, prices); [RequisitionsStaff reload|reroll|show. [ClusterFSeedRequisitions places the Appraisers and the office board; it also runs at every world load.

## 2026.10.20.2

### Staff
- Marathon mode: the Golems project where the week's usage will land by the weekly reset ("on track for 82% by Wed 4:00 PM", on the Golem tab, the web console and the morning email), and when it would fall short they go all out on their own: every Golem woken, no rests or run cap, the night shift at full, and the extra PCs also by day (they still back off when their owner is using them). It turns itself off once the week will be used, and can be forced on or off from either console.
- While marathon mode is on, the overseer is emailed when a Golem is running short of prompts, and a free weekly reset on offer comes with advice on when to press it.

## 2026.10.20

### Staff
- The web console does everything the Shard Console does, from a phone: a page for every tab (test shard, fixes, the shard's question, snapshots, world setup, map and art checks, diagnostics, commit, player package, websites, art requests and the live shard), with lists to tap and the console's own plan shown before anything runs.
- Dangerous buttons on the web console (the live shard, publishing, deploying, deleting, committing) ask a second time, in plain words, what will happen and to what; anything that must stay on the game PC says why.
- A Golem picker: the Golem page's buttons act for the picked Golem (the confirm word names it), Wake starts on that Golem's own model, and a queued prompt can be assigned to it. The Shard Console's Golem tab has the same picker and Assign.
- Queued, running and finished prompts (and their notes) can be read on the web console.

## 2026.10.19.5

### Changed
- Skills now rise at a rate meant to feel earned: every gain is the normal 0.1 and comes about twice as often as on a stock server, instead of the old five-point-per-gain, three-tries-per-use sprint. A skill reaches 100 in roughly an hour of steady fighting or an hour and a half of crafting; the Test Crystal, New Haven and Old Haven training, loop memory and pet training still speed things up on top. Existing characters keep every point they have.

### Staff
- Golems on more PCs: one join kit sets up any home PC (its own Windows account, a work drive, the programs, the sign-ins, GitHub access, laptop power and wake rules) and a readiness check proves it can claim, build and push.
- The web console lists every Golem with its machine, lanes and last readiness check, and its buttons work for each one, including Golems on other PCs, which pick up their commands over Tailscale.
- Each Golem has its own portrait (placeholders until the art is approved).
- A Golem pauses while its PC is in use, on battery or outside its hours, and finds a stopping point when it must stop.
- The Shard Console no longer crashes now and then when it closes while a background read is still starting.
- The skill gain rate is measured, not guessed: a seeded simulation drives the real gain code for six skills under stock, today and four candidate rates, and reports uses and hours to 50, 80, 100, 120, 150 and 200 (the table is in the cc-P191 notes). The code defaults match the new rate; the live config line is written for Chase in the notes.

## 2026.10.19.4

### Staff
- Several Golems can work the prompt queue at once, like a dev team: a registry names each one (the Iron Golem integrates on Opus, a Bronze Golem on Fable), every other Golem works in its own copy on its own branch, prompts carry a Touches line so two never edit the same area together, shared equipment (the test shard, Docker builds, the package, Haven, the commit) is a sign-up sheet, and the integrator merges finished branches into main one at a time, writing a fix-up prompt when one conflicts. Off until Chase turns it on.
- The Golem tab and the web console show one card per Golem: lane, machine, state, the prompt and branch it runs, the locks it holds.
- A Merge branches now button on the Golem tab and the web console runs the integrator's merge round by hand.
- A setup guide and scripts for a second PC (a D: drive from a VHDX, the clones, the sign-in, a one-command check).

## 2026.10.19.3

### Staff
- The Golem always wakes a session on the model the next prompt asks for: it reads the queue again right before every wake, notices when the queue is renamed while it waits, and a session that started on the wrong model hands over at once instead of waiting five minutes.
- One "Golem, please work on the shard tasks" now starts a new run after a full run that ended in a stopping point, a wrap up or any other rest (it used to need saying twice).
- When the usage reading from claude.ai goes stale, the Golem keeps waking itself from Claude Code's own usage numbers and emails once a day to say the reading is old.
- A prompt that stopped for Chase no longer blocks the Golem once it is back in the queue, or once both repos are committed when it stopped for that.

## 2026.10.19.2

### Staff
- The web console has the Golem's buttons: Wake, Turn on, Poke, Rest, Wrap up after this task, Find a stopping point, Long run, Night shift on and off, Run next and Retry stopped prompt (Away mode shows greyed until it is built). Each asks for a typed word in a sheet made for a phone, runs through the Shard Console's own code, streams its output back and goes on the Timeline. Only Chase, only over the tailnet, one at a time.
- The web console's tailnet address now proves each request came through its own Tailscale node, with a secret only that node holds, so nothing else on the game PC can act as Chase.
- The Shard Console's Golem tab has Poke the Golem, Run next (the picked queue row goes first, nothing renamed) and Retry stopped prompt (the STOPPED prompt goes back first and a session wakes on its model).
- The web console now shows all the console art: the Golem figure by its state, the tile icons, the runes, the header band and the textures, and has a slot for each Golem button's icon.
- Installing the web console no longer shows Docker's normal progress as red errors, and an update restarts the running web console.

## 2026.10.19

### Staff
- Art requests: every piece of art Chase has to make is now a request with its sizes, budget and destination, and the console has an Art tab listing them with thumbnails. From there: copy the prompt for ChatGPT, generate with Codex on the ChatGPT plan (or the OpenAI API under a $10 monthly cap), import a picture made elsewhere, approve or reject, and place.
- Placing approved art sizes it, trims it, checks its edges, seams and file size, and builds the animated strips (the crystals turning, the Golem breathing, the spinner) from one still.
- The Golem tab shows the ChatGPT plan's Codex limits beside the Claude bars, with the Golem's own share and a budget that keeps room for Chase's own use.

## 2026.10.18.17

### Added
- Imbuing table: Unravel Container. Target a bag in your backpack and every magic item in it is unraveled at once, each with its own skill check and ingredients. Plain, blessed, insured and quest items stay in the bag, as do bags inside it, and a summary tells you how many were unraveled, how many were skipped and what you got.

### Changed
- Imbuing table: a blessed item, an item you are wearing, a quest item or a faction reward can no longer be fully disenchanted, as on OSI.

### Staff
- Full Disenchant and Unravel Container now share one unravel path (yield, skill check, refusals).

## 2026.10.18.16

### Added
- Clean Up Britannia store: 73 more of OSI's rewards, 122 of 134 in all: the five named robes, lily pads, mushrooms, the Nocturne earrings, Sherry the Mouse statue, the chaos and eight virtue tiles, yucca, table lamp and bamboo, the horse barding, the snake skin and four creature boots, Nature's Tears, Primordial Decay and Arachnid Doom, the elven tapestries, both chests of drawers, the dragon head, the nest with eggs, the fire pit, the presentation stone, the beehive, all 24 pigments and the six charms.

### Changed
- Clean Up Britannia: magic items you find now pay extra for their magic properties, by how strong they are. Items you craft still pay what their materials are worth.
- Custodians: clearing the corpse of a monster you killed now pays by how fearsome it was (about 1.5 points for an orc, 15 for a dragon), plus what is inside. Corpses of pets, summons and kills that were not yours pay only what is inside.

### Staff
- New checks: magic properties pay their weight / 30 on loot and nothing on crafted gear; no imbued property pays more than its imbuing gold; no runic craft pays more than the tool's use; corpse values by creature; each of the eight extended woods resolves; each new reward can be bought at OSI's price and survives a save.
- A flaky plant test (a dry bowl's seedling died about half the time) now asserts what it meant.

## 2026.10.18.15

### Staff
- The Golem no longer closes the session it has just opened when it was resting; that session now starts its prompt, and only the older, finished session is closed once the new one reports.

## 2026.10.18.14

### Fixed
- Imbuing: every slayer can now be extracted, disenchanted and imbued, including Snow Elemental, the fire, water, air, earth and blood elemental slayers, Elemental Ban, Exorcism, Orc, Terathan, Lizardman, Gargoyle and Balron slayers. Before, a weapon with one of these showed no slayer at the Imbuing Table.

### Staff
- A check now fails the build if any slayer is missing from the imbuing table.

## 2026.10.18.13

### Changed
- Animal Taming: the chance to anger a beast that can be angered (dragons, nightmares, the Dread Warhorse and others) now falls as your skill rises above the creature's minimum: 95% at the minimum, about 77% twelve points above it, about 32% at forty-two, and 15% from about fifty-four points above it.

### Staff
- Three server settings tune it: clusterf.taming.angerBase (0.95), clusterf.taming.angerPerPoint (0.015) and clusterf.taming.angerFloor (0.15).

## 2026.10.18.12

### Fixed
- Society of Smiths: with "Orders that still teach me" on, a high-skill smith no longer gets iron (or other low-metal) plate tunic orders that teach nothing; armor orders, small and large, now come in the metal that still raises your Blacksmithy, such as Blaze at 144.

### Staff
- New checks ask for 100 Society orders on each path at 130, 144 (with and without a held Hammer of Hephaestus) and 160 Blacksmithy and require every one to teach; plain (iron) deeds still accept any metal, as in ModernUO and ServUO, pending a decision.

## 2026.10.18.11

### Fixed
- Fox ears and tail: facing west or north, the tail now sweeps out behind you from the small of your back instead of lying flat down your legs.
- Fox ears and tail: the ears sit down on the head instead of floating just above it, which showed most on a bald head, on your paperdoll and in the world.

### Staff
- The paperdoll fit check now also refuses ears that leave air above a bald scalp, and tests the face by the ears' pixels rather than their outline box.

## 2026.10.18.10

### Fixed
- The Achievements button now shows on your paperdoll and on the top bar in the Shattered Legacy client, and the tutorial's "Open the World Map", "Open the Journal" and "Open Options" buttons now appear too.

### Staff
- The server's TazUO handshake went out while ModernUO still blocks packets during login, so no client ever received it; it is now sent once the character is in the world. No client change: the current Shattered Legacy client works as it is.

## 2026.10.18.9

### Staff
- The play-test page opens on "Up next": one short session at a time (6 to 12 things to try, in order, with a "Before you start" box and big Pass, Fail and Later buttons), with the full list one tap away under All items. A "Probably stale" group lets old items go Later with their reason in one tap.

## 2026.10.18.8

### Staff
- Haven's dashboard after the first deploys: the retired tower print queue and anything planned or not installed yet no longer show as down tiles, the Sunflower dev store says "Stopped, started by hand", Nginx Proxy Manager is watched at its front door, and Grafana, Prometheus and the tailnet NPM admin found running on Haven have their own tiles.
- Portainer (Haven): deploy and roll back on the Shard Console's Websites tab install Portainer on Haven (backed up first, home and tailnet only); the first deploy shows the first-login link and its 5-minute limit.

## 2026.10.18.7

### Added
- An Achievements button on your own paperdoll (where Help was) and on the top bar opens your achievements page, the same page as `[achievements`. It comes with the Shattered Legacy client in the next player download.

### Staff
- The Achievements button is our client's third patch (shard buttons, one table entry per page) with a placeholder button picture; Help is still in the top bar's More + menu. Players get it with the next Build package and Publish.

## 2026.10.18.6

### Staff
- The play-test page asks your open decisions as questions you answer there (pick an option, keep what's built, write your own, or ask me later); answers are saved with who and when and reach the overseer on the next commit, and new Where chips (Questions, Outside the game, In game: Admin, In game: Player by test account) combine with Mine.

## 2026.10.18.5

### Added
- A combat script for TazUO, SL_Combat, in the Legion scripts set: it fights for you in Assist mode (you move, it targets, uses weapon specials and heals) or AFK mode (it also seeks targets around an anchor and walks back), bandages and drinks potions on your thresholds, bandages your companions and calls "all guard me" when you are hurt, with hotkeys to switch it on, change mode, pause and panic. It attacks reds, criminals and oranges, grays only when they attack you or are on your hunt list, and never blues, greens, your pets or your party.
- The script's loot helper rates magic items against a weighted profile (Keep, Upgrade, Unravel, Sell, Junk), compares them with what you wear slot by slot, can sort them into bags, and a Suggest button writes a profile from your skills and gear with a plain explanation.
- `[artificerrank` says your Artificers' Order rank and the imbue properties your account knows; the loot helper reads it so Unravel only flags essences you can use.

### Staff
- The combat script is phase 1 (melee and the rater): shard-migration notes/design-combat-script.md holds the roadmap for the archer, mage, tamer, stance and skill-training phases. The scripts are in the Legion scripts repo and need a Publish to dev and Promote to official to reach players.

## 2026.10.18.4

### Added
- The game launcher now keeps Shattered Legacy's Legion scripts (like SL_Mining and SL_Lumberjack) up to date on every start, and a new package comes with the current ones. If you changed one of them yourself, your copy is kept in the LegionScripts\backup folder before the new one goes in, and your own scripts are never touched.
- "Update Scripts" in the game folder gets the newest Legion scripts right away, without starting the game.

### Staff
- Chase's Legion scripts live in their own private repo now, linked into every TazUO client on his PCs: the Player package tab's Set up scripts repo makes the link (once per PC), Pull scripts updates it, and the Commit tab commits it with the shard's two repos.
- Publish to dev sends the scripts to a Tailscale-only channel that only Chase's own PCs read; Promote to official copies the chosen dev scripts to every player's install. Check scripts shows the repo, dev and official side by side. Publishing is refused until the scripts repo is committed and pushed, so the home and work PCs never overwrite each other.

## 2026.10.18.3

### Added
- Thieves' Den members can steal from monsters on every facet, not just Felucca: an Initiate works Felucca's dungeons, champion spawns and the Rootdeep Burrows (the tutorial dungeon, outside the Vault), a Journeyman every facet's dungeons, a Master bosses anywhere, the Burrows' own bosses among them. Too low a rank, or no Den, and the Den tells you so.
- Monster stealing starts at Stealing 60 with a minor haul (coin, Den scrip, lockpicks, small potions, bandages); the special items start at 100 as before, and the odds now keep rising all the way to 200.
- New things to steal from 100: five stolen-only components (shadowsilk thread, ember ash, whisperleaf, thief's wire, a lucky fang), recipe scrolls, the odd loop scroll fragment, and a Burrows creature sometimes gives up its family part.
- Three Thieves' Den recipes, learned from stolen scrolls only: the Shadowsilk Cloak (Tailoring), the Thief's Smoke Bomb (Alchemy, hides on Hiding instead of Ninjitsu) and the Pickpocket's Charm (Tinkering).

### Changed
- A missed monster steal no longer uses the creature up: it turns on you and you can try again a few seconds later. One successful steal per creature, as before.
- Every monster steal attempt can raise Stealing, up to your cap against famous enough creatures.

### Fixed
- A creature already stolen from, and the six-hour wait on a Gem of Salvation, now survive a server restart.

## 2026.10.18.2

### Staff
- The Rootdeep Burrows' tutorial Vault has its rules on the test shard: only a character in the tutorial (and their pet) can walk in from the landing, graduates and anyone else are turned back with a line from the Echo, and teleporting in is refused. When The Overdraft dies, everyone in the tutorial who fought it is credited, a crawlway in the strongroom's south wall opens for two minutes into the Warren, and the Vault stays sealed to them afterwards. A character who logs in inside the Vault without the right to be there wakes on the landing.
- `[ClusterFSeedRootdeep [dryrun | force]` places the Burrows' 33 spawners, the landing's working ankh and the Vault's crawlway once the map batch has built the floors; until then it refuses (the area is still empty land). The dungeon's region now carries its player name, the Rootdeep Burrows.
- The layout plan and the look choices for the Burrows are in the docs repo for Chase's approval before the map batch starts.

## 2026.10.18

### Staff
- The Rootdeep Burrows' creatures exist for `[add` on the test shard: the tutorial farm's Gourdling, Crop Crow, Burrow Scamp Slinger, the four Cacklers and the Burrow Hog; the Vault's Ledger Bat, Penny Rat, Hoarding Rat, Safe Deposit Box and The Overdraft; the Warren's Burrow Grub, Burrow Scamp, Redcap Scamp and The Burrow Mother; the Tangle's Rootling, Sporecap and Bramblehulk; the Dawnseam's Dawnshard and Seamguard; the Gloom's Gloomling, Gloomspinner, Gloomweaver and The Gloom Queen. Nothing places them yet (the Burrows map and spawners are later prompts). Each family drops its part (burrower claws, rootheart sap, dawnstone shards, gloom glands), the hog and the grub carve Burrowhide, the bosses drop jewelry and a boss part, and the tutorial creatures drop fixed loot.
- The player package carries 23 more creature bodies in its anim7 file (about 25 MB more), with a render sheet in the docs repo. The encoded animation files are now generated at package build time from the committed sources instead of being committed themselves.

## 2026.10.17.12

### Staff
- The player package can carry a creature body of our own. The first is body 2048, the burrow scamp slinger (a kobold archer from a third-party pack), drawn by TazUO from our anim7 file through the launcher's copies of Bodyconv.def and, new, mobtypes.txt; nothing of EA's ships. The package gates and the art checks (find-free-art, validate-art) know the registry's new body kind.
- `[add BurrowScampSlingerBodyTest` spawns the proof creature on the test shard: a weak archer on that body whose bow plays its one attack animation on every shot.

## 2026.10.17.11

### Staff
- In the last 24 hours before the weekly usage reset the Golem spends what is left of the week instead of letting it expire: it keeps handing on prompts until the week is at 97% (the last 3% stays yours), whatever the usual pace says, and ignores claude.ai's "you'll run out" warning. The Fable week does the same for Fable prompts by its own reset.
- In that window the night shift runs without its pace check, and the Golem wakes itself when the window opens and after each 5-hour reset. It emails when the window opens and, after the reset, how much of the week and the Fable week went unused.
- The console's Golem tab says when the end-of-week window is spending, when it opens next, or that it is off (endOfWeekSpend in the Golem's settings).

## 2026.10.17.10

### Staff
- The Golem closes its own finished sessions: the previous one once the next has started and reported, and a finished one two minutes after its last turn when the Golem rests. It never closes the one running a prompt, one busy in the last five minutes, one that stopped or paused for you (the Golem tab says it is kept open), or any session it did not open.
- The Golem's sessions open as tabs in one Windows Terminal window named golem instead of a new window each, and a second wake for the same prompt within two minutes is refused.
- The console's Golem tab has a sessions line: how many Golem sessions are open, which are kept open for you, and the last one closed.

## 2026.10.17.9

### Staff
- The Golem paces itself through the day from the real plan usage page: it hands on a prompt only while the week (and, for a Fable prompt, the Fable week) is within its even pace plus a small head start, rests with the reason and the time it is back on pace, and wakes itself then.
- A Fable prompt held by the Fable week lets the next Opus prompt go first when that one does not depend on it; claude.ai's "you'll run out" warning holds new hand-offs until the page stops showing it.
- The Golem never uses a free limit reset or usage credits: it emails once a day to ask about a reset when the week is running short, and still stops at the plan limit with credits on.
- The console's Golem tab and the web console show three usage bars (5-hour, week, Fable week) with a pace marker and reset time, the pace warning, when the usage page was last read, and the free reset and cloud credit.

## 2026.10.17.8

### Changed
- The fox ears and tail can be worn by men as well as women (humans and elves; gargoyles are told they cannot).

### Fixed
- The fox ears sit on top of the head and the tail comes from the lower back on the paperdoll, instead of a fox face and a tail by the knees; a man's paperdoll shows them too.
- The fox ears and tail stay on when you stand still or run, instead of vanishing until you walk.

### Staff
- Building the player package now refuses a paperdoll gump of ours that does not fit the shipped bodies (ears on the crown, tail from the lower back).

## 2026.10.17.7

### Added
- With an Artificers' Essence Satchel in your backpack, the essences you unravel or disenchant, and the essences, magical residue and relic fragments you loot or pick up, go straight into it; a full satchel leaves them in your backpack.

### Fixed
- The ankh beside the League Registrar in New Haven now offers a ghost resurrection, like the shrine ankhs.
- Disenchanting an item with no magic is refused ("There is no magic in this to unravel.") and no longer pays an Essence Shard.

### Staff
- [ClusterFAnkhSurvey lists every ankh in the world and whether it works; [ClusterFSeedAnkhs places the working ankhs OSI had that the world lacks. The play-test page picks up new code without a restart, and the overnight bot's creature fight keeps its fighter alive.

## 2026.10.17.6

### Staff
- The shard status page keeps its own judgments current and opens with a "Needs overseer review" box listing what only the overseer or Chase can settle, each with the one command that settles it.

## 2026.10.17.5

### Staff
- Six map checks for staff, typed in a client: whether a building footprint can stand where you target and why not, every spot in a spawn or decoration file checked for water, cliffs and walls, duplicate or buried map statics in a box, the tiles nobody can walk on (listed, or marked for a minute with red runes on the test shard), where water meets land and how high the coast is, and a targeted rectangle or points written out in the spawn files' own JSON. The Shard Console has a Map and Art tab that runs the art checks and prints these commands' syntax.

## 2026.10.17.4

### Staff
- A creature art check for staff: before a downloaded animation is imported it says which body and animation slot are free, what the client needs to know about it (its type, a hover idle), and what a replacement would also change, can draw any frame to a picture, compares two hue files, and checks the art registry. Claude sessions can now also look things up in the client files directly (a map spot, every body, a hue).

## 2026.10.17.3

### Staff
- A new web console, read only for now: the Shard Console's tiles and lights, the Golem with its queue and what each prompt is for, everything waiting on you, a timeline, the logs and the links, on your phone or the work PC at https://uoconsole.tail81098d.ts.net/ (Tailscale only, and only your login), once its Tailscale key is made and its installer run.
- The Shard Console opens with the tiles as they last were, while it reads them fresh in the background.

## 2026.10.17.2

### Staff
- The Shard Console no longer freezes: its window opens in about 2 seconds instead of about 40, the status, the lights, the Golem and each button's checks are read in the background while the window keeps answering, only the open tab's details are read, and the World setup tab is built the first time it is opened.

## 2026.10.17

### Staff
- Haven's home dashboard is finished and reachable only over Tailscale (http://haven.tail81098d.ts.net/): five sections (Sunflower, Filament, Monitoring, Ultima Online / Shattered Legacy, Admin), a tile with live up or down for every service on Haven and on the game PC (which shows when it went offline while it sleeps), all generated from one service inventory with a runbook beside it, and the print queue now proves its Hostinger key from Haven before the tower copy stops.

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
