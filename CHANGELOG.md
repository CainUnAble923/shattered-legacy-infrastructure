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
