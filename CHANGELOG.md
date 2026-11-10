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

## 2026.11.10.4

### Added
- Eleven lamps that really light: a dragon lantern, bird, koi and dragon egg lamps, a stained glass lamp, tall antique and modern lamps, an ornate table lamp, an ancient dragon brazier, a gold tray of candles and a jack-o-lantern with eleven carvings. Double-click one to light it or put it out, and turn it with your house's interior decorator. Ten are in the Achievement store; the jack-o-lantern is in the Festival Peddler's Halloween stock. After Drafted and Milva.
- A fortune teller's wagon now stands beside the Festival Peddler's stall in New Haven while a festival is on. Climb the steps to its porch and look inside. After Olorin.

## 2026.11.08.4

### Changed
- The League field office is tidied: its bookcases face the room, and the sign by the door hangs out from the wall.

### Fixed
- The League requisitions board in New Haven's League field office now hangs flat on the hall's north wall, facing the room, instead of standing edge-on out of the wall. Worlds that already have it turn it by themselves.

## 2026.11.08.3

### Changed
- The Name Colors lesson now happens in a new New Haven jail: click five people to learn the colors, take from a marked crate and see your own name turn gray (no real penalty), read your karma and fame in the warden's ledger, and call the guards on a thief.

### Fixed
- The New Haven jail's south-west corner no longer shows a stub of wall, no torch hangs on a window, and the marked crate stands in the hall where you can see it.
- The New Haven jail's cells now have closed fronts: the bars run in one line with the gates instead of showing as stubs, the cell walls meet the bars without a gap, and the hall's candelabra no longer stands in front of a cell gate.

## 2026.11.08.2

### Fixed
- On the Wakening Isle, the bench where hurt Pip sits in the farmhouse now runs along the wall as one whole bench.

## 2026.11.08

### Added
- A gold "!" now floats over someone who has something new for you, and a "?" over someone waiting for you to hand something in: Rook and the farmhand during the tutorial, the League Registrar (joining, your next rank, the mule errand), and the guildmaster of every guild you belong to (work orders, and the smiths' bulk orders and commissions). Only you see your own markers.
- Keys you can see at a glance: boss room doors that open only for their own key, League trial gates that let through only the holder of that chapter's key, and top-tier loot boxes that come locked and open with a Vault key, which bosses sometimes drop.
- Moongates in four new spinning colours, blue, red, black and white. A white gate at League Headquarters can take you to any guild hall, and a blue gate at each hall brings you back. The Valley of Eodon's moongate turns red. The old moongates are unchanged.

### Changed
- Character creation: humans only, Tutorial Island start, a brighter wake, and your pet waits at the end of the isle.
- The trade (Blacksmith) starting choice is gone; you can still pick Blacksmithy, Mining, Tailoring and Tinkering with Advanced, where you choose your own skills.
- On the Wakening Isle, Merrin the banker faces his counter and tells you what to do as you walk in, and the island bank is tidier: its lesson sign stands by the path instead of before the door.
- On the Wakening Isle, the League lodge's door is now on its east side, where you can see it as you come along the road: a brick walk leads to it from the road between two braziers, the lodge's signs stand where the walk meets the road, the lesson says which side the door is on and its arrow points at the door, and Elara calls out to you if you are outside. Get the new player package to see the moved door and the walk.

### Staff
- The creation screen's race row and profession list need the new player package; the server already makes any other race Human and refuses the Blacksmith choice.
- Quest markers: setting clusterf.questMarkers (on by default) turns them off for everyone. The art is RedRooster's "Quest Markers" (art 46592-46597); he asks to be in the shard credits list.
- Keys: ShardKey ([add ShardKey "<lock tag>" "<name>" <design 1-34> <hue>), ShardLockedDoor (a boss room door, LockTag and LockName in [props; ConsumeKey takes the key), keyed moongates (LockTag on a ShardMoongate). Top-tier boxes: clusterf.lootBoxes.keyTier (6; 0 turns locked boxes off) and clusterf.lootBoxes.vaultKeyChance (0.25 a boss kill). Boxes earned before this update stay unlocked. The keys and gem tokens are Hestia's "Quest, Item, Token Keys" (art 46618-46663), credit Hestia.
- Gates: [add ShardMoongate Red (Blue, Red, Black, White; Color in [props). [ShardGates guildhalls dryrun shows the guild hall network, [ShardGates guildhalls places it, [ShardGates clear removes it. The Eodon gate takes its red look the next time [GenEodon runs. The gates are Carl Hamilton's spinning moongates (art 46598-46617), credit Carl Hamilton.
- Players need the package update to see the new art; without it they see the "UNUSED" placeholder.

## 2026.11.07.9

### Changed
- Waking on the Wakening Isle is quiet now: one window tells you where you are, how to get up and that you are a young player, instead of a wall of System messages. The "Your skill in ... has increased by 30" lines for your starting skills are gone.
- New Haven's lessons keep their teaching in the lesson window, which stays open until you close it: each step, each tour stop and the skills a lesson only explains.
- The Runes lesson starts at New Haven's public moongate and gives you a blank rune, a Mark scroll and two Recall scrolls: mark your rune, recall to Rook's bench, then recall back. The scrolls always work in the lesson.
- New Haven's lessons happen in fitting places: blacksmithing at the smithy, fishing off the docks pier, lumberjacking at the Woodsman's lodge, cooking in the Albatross kitchen, scribing in the library, thieving in the Thieves' Guild, and the other crafts in their shops.
- Mysticism is taught in the Haven Magery School by Ilyra Moonwhisper, its elven adjunct instructor, and Necromancy at the necromancer's shrine at the north-east of town, where Morvain Ashgrave keeps the candles.

### Fixed
- The old tutorial farm west of New Haven is cleared from every world by itself.

## 2026.11.07.8

### Changed
- The pet choice scroll, the "Didn't Read the Sign" achievement and the League shop have new windows: a parchment scroll with Dog and Cat tiles, a dark System panel, and a dark shop with tabs for Titles and the Hall and a Buy button on every row you can buy. Get the new player package to see them.

### Staff
- [add FinishersBanner places the Hall of Finishers banner, a dark hanging banner with a broken sword (new art in the player package).

## 2026.11.07.7

### Staff
- [VBuild capture <name> saves what you decorated: target two corners, and every piece in the box (its art, place, hue and name, and a door's facing) is written as decoration data to decor-captures/<name>.cfg beside the saves. The default build form is relative to the box's corner, so the file can go into the builds folder and be placed anywhere with [PlaceBuild. Add world to keep map coordinates for the facet decoration folders. Pieces that can't be remade as their own type (addon parts, for example) are kept as plain statics of the same art, and the capture says how many.
- [AnimLocate shows what each animation of a body does. Pick one of our bodies from the list (or type its number) and a frozen stand of it plays every action in turn, with the action number over its head. [AnimLocate target plays them on any creature you pick, and [AnimLocate stop ends it. The pace and the number of actions per kind of body are in Configuration/staff-build-tools.json.

## 2026.11.07.6

### Added
- A small casino in the Haven inn's east room, gold only. Two slot machines take bets of 10 to 5,000 gold and pay three of a kind (three Gems pay 1,000 times the bet), and two cherries give your bet back. At the blackjack table, beat the dealer for even money; a natural blackjack pays 6:5, and you can double on your first two cards. The house keeps a few percent over time, and the whole shard hears about big wins. Each account can play up to 250,000 gold a day. Young characters can't play. If the server restarts in the middle of a hand, your bet goes back to your bank.

### Staff
- [Casino status shows the casino's books: gold taken in, paid out and kept, today and all time, for each game, and the house edge. The same line goes to Logs/casino.log at every world save. All the numbers are in Configuration/casino.json: bets, the pay table, blackjack rules, the daily limit and where the machines stand. A pay table that would pay back 99% or more is refused. [Casino reload reads the file again, and [Casino seed places anything missing.

## 2026.11.07.5

### Fixed
- The stables sign in New Haven now stands clear of the barn's eave.
- The ankh outside the League building in New Haven now resurrects ghosts. It appears by itself on a world that did not have it.
- The Animals lesson's injured horse can now be bandaged and tamed; it refused both before.
- Rook's bench in New Haven is now one whole bench, not two short stubs.

## 2026.11.07.4

### Added
- Egg hunts: when staff start one, everyone on that facet gets an arrow pointing to a hidden egg. The first player to reach it and double-click it wins 25 Festival Tokens, and the whole shard hears who found it.
- Mine mazes: staff can open a field of hidden mines. Walk in over the start row and find the safe way across. Step on a mine and you die, but you keep everything you carry and rise at the start a few seconds later. The first three across win Festival Tokens. When staff reset the maze, the mines move and the prizes open again. Young characters can't enter, and no one can recall, gate or teleport in or out.

### Staff
- The Staff Hub's Events tab has a new "Egg hunt, maze" button (or type [StaffEvents). From there you can hide the egg here or by target, end the hunt, mark the maze's two corners, lay it, reset it or remove it. The start row is the end nearest your first corner. All the numbers, including the maze prize (Tokens or LootBox), are in Configuration/staff-events.json, written with the defaults on first start. [StaffEvents reload reads it again.

## 2026.11.07.2

### Added
- Horse barding: a blacksmith can make it (400 iron ingots, Blacksmithy 60) and use it on their own tamed horse, which becomes an armored horse in the metal's color. While you ride, it takes 5% of the damage monsters deal to you (10% if exceptional) until it wears out after 500 points (1,250 if exceptional), and then your horse looks as it did before. It is half as strong as the swamp dragon's, since a horse is far easier to tame. Other players' attacks pass by it.
- Pet perches: a carpenter can make a pet perch deed (Carpentry 50, 100 logs). Place it in your house, double-click it and choose your own tamed chicken lizard, eagle or small bird: it settles on the perch for good as a house pet, keeps its name and color, shows its age, and the house's owners can rename it. Redeed the perch and the pet goes with it.

### Staff
- Horse barding's numbers are in Configuration/horse-barding.json and the perch's in Configuration/pet-perch.json (including the kinds of pet it takes), written with the defaults on first start.

## 2026.11.07

### Added
- The Clockwork Scavenger: reach Journeyman in the Tinkers' Union and ask a Tinker guildmaster about the "scavenger". Gather the parts (gears, springs, clock parts, an axle with gears and iron ingots), use the frame, and a little brass crawler follows you and picks gold, gems and reagents from the monsters you kill. Choose what it collects in its Options. It takes only what you could loot yourself, never from players. With a Clockwork Load Harness III on your mule, it carries the loot to the mule instead. One per character.

### Staff
- The Scavenger's parts, look, reach and pace are in Configuration/clockwork-scavenger.json, written with the defaults on first start.

## 2026.11.05.5

### Added
- Herbalism arrives: forage wild herbs and grow your own reagents. It's a new skill to 200 with the Order of the Alembic. Wild garlic, ginseng, mandrake, nightshade, blood moss and rarer herbs grow around the land and regrow after picking, and [herbalism keeps your ledger of finds. The Order's herb table in New Haven gives each character a bowl and seeds, and sells a raised bed deed. Plants in bowls grow at the usual pace, a bed is tended as one, and crossing plants of the same strain can breed a better one, up the metal ladder to Celestial. Harvest herbs, double-click them to work them into bundles, and open a bundle for reagents. Plants forgive a few days away, more as your skill rises. Green Thumb is yours for your first harvest. Vendors still sell reagents.

### Changed
- The Camping lesson now has you pick wild garlic with Herbalism.

### Staff
- Every Herbalism number and the wild patch list are in Configuration/herbalism.json, written with the defaults on first start. [Herbalism reload reads it again.
- Herbalism needs the new player package: the skill's name is written into each player's skill files, and the client shows a skill no saved group lists.

## 2026.11.05.3

### Added
- The League sash now grows with you. Elara gives every registered citizen one for free, one per character, blessed and bound to that character, and it stays with you through every loop. As your League rank reaches Dull Copper, Bronze, Verite, Blaze, Mythril and Celestial, go back to Elara and pay Renown to raise it a step: each step gives it a new name and metal hue and adds 1% to the Renown you earn from achievements and League jobs while you wear it, up to 6%. Spending Renown never lowers your rank.

### Changed
- League sashes you already have become your new sash at its first step. A second one stays as an old League keepsake. Loot boxes only give the sash to a character who has none yet.

### Staff
- Every League sash number is in Configuration/league-sash.json, written with the defaults on first start. [LeagueSash lists the steps, [LeagueSash reload reads the file again, and [LeagueSash step <n> sets your own sash.

## 2026.11.05.2

### Added
- The Drover's Handbook. Animal Trainers sell this book of Herding abilities, cast from the book for a little stamina, with no crook needed. It comes with Corral: a wild animal is easier for your next taming attempt, up to 25 easier at 200 Herding, for a minute. Trainers also sell three pages to drop in the book. Calm stops a wild animal fighting for up to 13 seconds. Herd walks up to 10 wild animals to a spot you pick. Lure walks one wild animal up to 15 tiles away to you, seen or not. Using them on harder animals trains Herding all the way to 200. [Corral, [Calm, [Herd and [Lure work for macros. (Book structure after Hammerhand's druid spell pack.)

### Staff
- Every Drover's Handbook number is in modernuo.json under clusterf.drover, written with the defaults on first start.

## 2026.11.04.3

### Added
- Seven new ethereal mounts, solid rather than see-through, and each a blessed statuette you double-click to ride. The League shop sells four at 1,750 AP: the peacock for 1,500 Renown, and the windrunner, dark tiger and light tiger for 2,000. Three are achievement rewards: the lasher for finishing all 38 New Haven trainer quests, the boura for discovering all 16 ore types, and the ancient hellhound for reaching 200 in three skills. If your account has already earned one of those three, it is in your backpack at your next login. (After the ethereal mounts of Neshoba, Shazzy and Yvan Major.)

### Staff
- The ethereal mount settings are clusterf.etherealLooks.hue, .followerSlots, .shopAp and .renown.<peacock|windrunner|tiger|light-tiger>.

## 2026.11.04

### Added
- Peacocks strut the meadows north of Trinsic. Tame one (Animal Taming 29.1, one follower slot, it eats fruit and grain) and ride it, with an ostard's strength and speed. A pet leash shrinks one into its own figurine. (After WiKKiD's peacock mount.)

### Staff
- Every peacock number (taming, stats, the meadows and their spawners) is in Configuration/peacock.json, written with the defaults on first start. [Peacock status|reload|seed [dryrun]. Meadow spawners are placed by themselves after every world load, once per meadow.

## 2026.11.03.11

### Changed
- Play testers: the task list is easier to read, and staff accounts no longer get player-only tasks.

## 2026.11.03.9

### Added
- Beekeeping. Buy a beehive deed from a beekeeper (Vesper has one) and set up a beehive in your house; it takes one lockdown, and a house holds two. Start it with a bee swarm, bought from the beekeeper or caught at a wild hive. Keep flowers (blooming plant bowls, potted and garden flowers) and water (a trough, tub, barrel or a pitcher of water) within 5 tiles, and treat the colony like a plant: a Poison potion for parasites, a Cure potion for disease, Heal and Greater Heal to mend it, Strength to make it hardier for a day and Agility to let the bees range further. From its third day a healthy hive makes honey and raw beeswax every day; harvest them with a hive tool. Render raw beeswax into beeswax in a wax pot by a fire or oven (the pot is sold by beekeepers and made with Tinkering), then make candles, short, long and large candles under Alchemy's new Candles group from beeswax and thread.
- About a dozen wild beehives grow in Trammel's woods near Yew, Britain, Vesper, Minoc, Trinsic and Skara Brae. Anyone may pull a honeycomb from one (and get stung a little); a beekeeper with a hive tool can catch a swarm there for free. An emptied hive regrows after a few hours. (After the apiculture in Shafev's Cooking Expansion Set.)

### Staff
- Every beekeeping number (the daily check, range, flower and water needs, ailment chances, potion effects, honey and wax per day, the hive tool's uses, the wax pot, the candle recipes, the beekeeper's prices, hives per house and the wild hive spots) is in Configuration/beekeeping.json, written with the defaults on first start; candle rows must be stock candles and the beekeeper sells only beekeeping goods. [Beekeeping status|reload|seed [dryrun]. Wild hives are placed by themselves after every world load, once per spot.

## 2026.11.03.8

### Added
- A wicked witch haunts the Halloween season. While Halloween is on, one stands near each of the Yew, Cove and Britain graveyards, and travelers in the wild at night may meet one coming for them. She is a strong mage (about 900 hits). Her corpse holds a prize: a witch's Ouija board that glows and moans when used, a torture block deed or a gore rack deed for your house. Whoever hurt her most also gets 5 Festival Tokens. A slain witch returns to her graveyard after a while, and all of them leave when Halloween ends. (After Milva's Halloween Witch.)

### Staff
- Every witch number (her hits, skills and resists, gold, the prizes and their odds, the tokens, the graveyard spots, how soon she returns, and her random encounter) is in Configuration/halloween-witch.json, written with the defaults on first start; scrolls, gear and bless deeds are refused as prizes. [HalloweenWitch status|reload|sync|clear. Her encounter joins the random encounters by itself, also on a world whose encounters.json was written before.

## 2026.11.03.7

### Added
- Chocolate making in Cooking grows. Under Chocolatiering you can now make sweet cocoa butter (sugar and cocoa butter, at an oven), and from it the dark, milk and white chocolate nutcrackers, wrapped in a foil sheet. Pour a bar of dark, milk or white chocolate into a mold and wrap it in foil for a chocolate orc, beetle, bunny, solen or unicorn. Provisioners now sell the five chocolate molds, sacks of sugar, vanilla and foil sheets. Cocoa butter, cocoa liquor and the three chocolates were already in Cooking. (Figures and molds after Carlos_ar's Chocolatiering Adventure.)

### Staff
- Every chocolatiering number (each recipe's skill range and ingredients, and what provisioners sell at what price) is in Configuration/chocolatiering.json, written with the defaults on first start and read at start; deeds, scrolls and gear are refused there. [Chocolatiering status says what is in force and checks the file.

## 2026.11.03.6

### Added
- A holiday stocking hangs on the Festival Peddler's table in New Haven while the winter festival is on. Double-click it and hold still for six seconds to pull out a treat: gingerbread cookies, candy canes, jelly beans, a cake, or a little winter decoration (a holiday bell, a poinsettia or a snowflake). Once a day per account, and the day's first grab also gives a Festival Token. (After Candy Apple's Stocking full of goodies.)

### Staff
- Every holiday stocking number (its season, the hold-still time, the reach, grabs a day, the token, where it hangs, and the prizes with their weights) is in Configuration/holiday-stocking.json, written with the defaults on first start; deeds, scrolls and gear are refused there. [HolidayStocking status|reload|place|clear. It hangs and comes down by itself with the winter stall.

## 2026.11.03.5

### Added
- Artisan Festival decorations for spring: 64 pieces from Britannia's Artisan Festival, made for the virtues and the dungeons. There are paper lanterns and wooden sculptures that light up when you double-click them where they stand, and throw pillows and tapestries; double-click a lantern, pillow or tapestry in your backpack to turn it to face the other way. While spring is on, the Festival Peddler sells twelve of them for 10 to 25 Festival Tokens, and every loot box you earn in spring may hold one of the other 52. (After Sareus's Artisan Festival reward scripts.)

### Staff
- Every Artisan Festival piece, the twelve the Festival Peddler sells and their prices, and the weight of the rest in the spring loot box slot are in Configuration/artisan-festival.json, written with the defaults on first start; [ArtisanFestivalStaff reload|status|give <key>.

## 2026.11.03.4

### Added
- Grave digging. Tinkers now sell a gravedigger's spade. Double-click it and target a grave in one of Britannia's cemeteries (Britain, Vesper, Cove, Moonglow, Jhelom, Yew, the Chapel of Saint Lucia and others) to dig with Forensic Evaluation. Anyone can turn up bones; from 50 skill you also find fertile dirt, and from 75 the necromancer's reagents: grave dust, nox crystal, bat wing, daemon blood and pig iron. Now and then the spade strikes something else: human remains, a gravestone or headstone for your house, from 75 skill an old treasure map, and from 90 a wooden coffin. Each grave gives up a handful of finds and then rests for a while, for everyone. Graves outside cemeteries are left alone. (After the Grave Digging system by Massapequa.)

### Staff
- Every grave digging number (the cemeteries, the graves' yield, what each skill tier finds, the bonus chance and list, the spade's price and uses) is in Configuration/grave-digging.json, written with the defaults on first start; [GraveDiggingStaff reload|status|here ("here" says whether your spot counts as a cemetery).

## 2026.11.03.3

### Added
- Gold panning. With Mining 60 or more, buy a prospector's pan from the Compact assayer who stands beside the Miners' Compact Liaison, then double-click it and target the water at a river or lake (not the open sea). Each pan may turn up a small gold nugget, from Mining 75 a medium one and from 90 a large one, and now and then a blue diamond, fire ruby, white pearl or another fine gem. A stretch of water gives a handful of finds, then needs time to settle. Sell the nuggets to the assayer for gold or, as a member of the Miners' Compact, hand them to him for standing with the Compact. (After the Gold Panning system by Hammerhand and Milva, shared by manwitch.)

### Staff
- Every gold panning number (skill, sizes, prices, standing, gem chance, the water's yield, which tiles are water and how sheltered it must be, the pan's price and uses) is in Configuration/gold-panning.json, written with the defaults on first start; [GoldPanningStaff reload|status. The assayers place themselves beside each Miners' Compact Liaison on every start; [ClusterFSeedAssayers does it at once.

## 2026.11.03

### Added
- League hunts on the requisitions board. Each day the board posts two hunts for you from the League Bestiary ("cull 12 hedge snatchers", "hunt down 4 tunnelmaws"), sized to your rank in the guild that posts them, and each week one great hunt with a bigger count. Kills count by themselves: the player who did the most damage gets the kill, and party members nearby count it too. A finished hunt pays League Renown, guild standing and gold on the spot; the great hunt also pays a System's Silver Loot Box. The board shows your progress, and unfinished hunts simply roll over, with no penalty. (The daily-hunt idea is after DragnMaw's DailyQuests.)
- While you hold a hunt, the board's "any slayer" requests can ask for a slayer of that hunt's creatures.

### Staff
- [hunts lists a character's hunts; [LeagueHuntsStaff reload, reroll and show. Every hunt number (counts, pay by tier, the great hunt's multipliers and box, the weekly reset day, party range, the families) is in Configuration/league-hunts.json, written with the defaults on first start. A family is only offered while one of its bestiary spawners stands, so run [ClusterFSeedBestiary if it has not been run on a world.

## 2026.11.02.6

### Added
- Travelers may now run into trouble on the roads. Out in the wilds of Trammel and Felucca (never in towns, dungeons, houses or on boats, and never on the Wakening Isle or in New Haven), you may meet brigands demanding a toll, a League surveyor pinned down by local wildlife, or, on Halloween nights, a lantern procession best left alone. Young characters, the hidden, the idle and anyone already in a fight are left in peace, and each traveler meets at most one every 25 minutes.

### Staff
- [Encounters status, check, reload and force <id>: the random encounters, set in Configuration/encounters.json ("enabled": false turns them off).

## 2026.11.02.5

### Added
- Loot boxes now roll a rarity when you earn them: Common, Rare, Epic or Legendary. Rarer boxes hold more items in bigger stacks, Epic and Legendary boxes add a guaranteed prize for their kind, and each step has a bigger celebration; a Legendary box is announced to the whole shard. Higher-grade boxes are rare more often.
- Six new kinds of loot box: Fisher's (your first catch of each High Seas fish), Sailor's (your first SOS chest of each level), Herbalist's (each kind of plant you first set to decorative), Society (each new rank in a crafting guild; the Maritime Brotherhood gives Fisher's and Sailor's boxes, the Healers' Covenant Herbalist's), Delver's (a boss's first kill inside a dungeon) and League Dive (every tenth guild work order).
- Seasonal loot boxes: while Halloween, winter or spring is on, every box you earn holds one extra seasonal treat or decoration and carries the season's name.
- New loot box prizes: Bags of Holding in five sizes, which hold a set number of items however heavy (after TheArt's Bags of Holding), and a Reagent Bag that holds only reagents and makes them lighter (after Bittiez's Regeant Bag); paintings, trophies, crystal balls and other house decorations; tools, trap training kits, pet treats, dyes, League title deeds and more.

### Changed
- Everything in a loot box now does something: the placeholder items, loop scroll fragments and epic components are gone from boxes, recipe scrolls come only for crafts you have 50 or more skill in, and a boss box holds a real trophy named for the boss.
- Loot box grades are now named Plain, Sturdy, Fine, Grand, Exalted and Celestial, and a box's name shows its rarity, grade and kind, as in "Epic Fine Explorer's Box".

### Staff
- [LootBox give takes an optional rarity and season, [LootBox season says which season's slot boxes earned now get, and [ClusterFSpring shows the new spring window (clusterf.seasonal.spring.start and .end in modernuo.json, 03-20 to 04-20). Every loot box number and item list is in loot-boxes.json.

## 2026.11.02.3

### Staff
- A Golem's question to Chase now stays open on the Golem card until it is answered, retried, cleared or the prompt moves on. Before, a stopped session that woke once more for its own background task (a watcher ending) looked like Chase answering, so the question closed before he saw it and his answer was refused.

## 2026.11.02.2

### Added
- Play testers: say [tasks for your test list and [report for anything odd.

## 2026.11.02

### Added
- Tutorial tips and fixes from playtester notes: each Wakening Isle lesson now has its own achievement, and short tips in the lessons (hover over items, resize the game view, read old chat in the Journal, open doors, see inside buildings).
- Your first finished quest beyond the tutorial earns "Gainfully Employed", and "The Tutorial Has a Checkbox" now comes as soon as you turn in your first New Haven trainer quest.

### Changed
- The isle's lessons say how to get up off the beach, how to open your character window and backpack, and what to do at Elara Voss's League window. Saying "lesson" now opens the lesson window too, as [lesson does.
- After each New Haven lesson you are told to go back to Rook, with an arrow to him. The tinkering lesson names the Item Identification skill, and double-clicking the odd curio uses it.

### Fixed
- The bank lesson's blue token now comes the first time: saying "bank" inside the isle's bank starts the lesson if you walked in before starting it.
- The New Haven escorts (the one asking for the weapons trainer, and the others) can be taken to their destinations again.
- The starting city boxes on the character screen no longer look ticked when they are not.
- On the character screen, Pants Color now shows on a woman's skirt.

### Staff
- The isle's death lesson logs each step and any disconnect during it.

## 2026.11.01.6

### Added
- A pet leash: double-click it and target your pet, and the pet shrinks into a figurine in your backpack (weight 1). Double-click the figurine to call your pet back out, with its name and loyalty as they were. Only the owner can call it out, and not in a fight; summons and pets carrying items cannot be leashed. Animal trainers sell leashes for 150 gold, and the pet choice scroll (dog or cat) now comes with one. Ported from "Pet shrink for 57.2" by Queen.

### Staff
- The leash's price, uses (unlimited by default), fight rule and blessed options are clusterf.petLeash.* in the shard's configuration.

## 2026.11.01.5

### Added
- Every guild now has a Guild Store at its guildmaster ("Guild Store" in the guild's window, say "guild store", or type [guildstore beside the guildmaster), paid in the guild's own points: tools, materials, runic tools for the craft guilds, decor, a cloak in the guild's colours and, at the top rank, a guild title. Spending points never lowers your standing or rank.
- Tailors, Foresters, Tinkers, Maritime, Alembic and Arcane bulk orders now also pay their guild's points (1 per 50 order points), and every requisition pays its guild's members 1 point per 10 standing. Guilds that showed "scrip" now have named points: Arcane Sigils, Tailors' Thimbles, Tinkers' Cogs, Healers' Tokens, Bards' Notes, Warriors' Marks, Maritime Charts, Merchants' Bills, Dojo Seals, Keepers' Candles and Alembic Drops.
- Festival Tokens: while Halloween, winter or spring is on, strong creatures sometimes drop a token to whoever did the most damage, each Sorcerer's Dungeon artifact you hand in pays 2, and your first login of the day pays 1. Spend them at the Festival Peddler's stall in New Haven, which is there only during a festival and sells that season's treats, decorations, a festival title and a festival loot box.
- The Achievement Store ("AP Store" in the achievements window, at the League Registrar, or type [apstore): spend your achievement points on titles, achiever's sashes, the robe of the Noted, an engraved plaque and twelve classic paintings. Your AP total never goes down, so nothing it has opened closes.
- The League shop has fourteen more rows: a blessed League runebook, special hair and beard dye, an oil lantern, a bag of sending, furniture and metallic dye tubs, a crystal ball, two floating crystals, a pet bonding potion, a name change deed, the title "the League Champion" and the System's Platinum loot box.

### Staff
- Every reward store price and what opens each row are in Configuration/reward-stores.json (written with the defaults on first start); [RewardStores reload reads it again. [FestivalMarket status|place|clear|season shows or moves the Festival Peddler's stall; clusterf.festival.peddlerAt in the shard's configuration is where she stands.

## 2026.11.01.4

### Added
- Test shard: a new character who runs straight past the isle's lessons and through the moongate earns "Didn't Read the Sign" and a reward box holding a pet choice scroll: a dog or a cat that follows you. There is no going back to the isle for that character.

### Changed
- New characters start on the Wakening Isle and are Human for now. New Haven shows as locked on the character screen until the isle is finished; an Elf or Gargoyle you pick is made Human, and you are told why.

### Staff
- Two switches in the shard's configuration reopen these: newCharacters.newHavenStart (false for now) and newCharacters.races ("Human" for now). Staff accounts are not locked. The account reset's flags option now also clears the tutorial record, so the next character takes the lessons from the start.

## 2026.11.01.3

### Changed
- Test shard: in the Wakening Isle's vision, the Stranger who breaks the Gem is now a hooded, faceless figure: a deep cowl over the head, a dark robe and cloak, and the sword, with no name shown.

## 2026.11.01.2

### Staff
- The Golems' night shift no longer sits idle when Claude's usage report is incomplete: it takes the missing figure from the plan usage page, works on the week's figure alone when no 5-hour figure is reported anywhere, and when both reports are out of date it still runs one batch a night if the last known week is on pace. A Golem now always takes the prompt picked as its Run next, even one marked for the Codex lane.

## 2026.10.31.11

### Changed
- Test shard: New Haven's sparring lesson with the echo is now held in the Proving Grounds, the fenced warrior training yard on the east side of town.
- Test shard: the mining lesson takes you along the mountain path south of town to a cave, where you dig real ore from the cave floor. A signpost marks where the path starts.
- Test shard: the animals lesson is now in the stables beside Uzeraan's mansion: bandage an injured horse in the sick-bay stall, then tame it. The first horse you tame there is yours to keep and ride. Getting your pet on the Wakening Isle no longer marks this lesson done.
- Test shard: in the bard lesson, after a successful Provocation the two echoes fight each other for a few seconds so you can see what it does. The lesson is held in the Bardic Guild's hall.
- Test shard: the magery lesson is held in the Haven Magery School, where a mage echo shows you Magic Arrow a few times, words of power and all, until you cast it yourself.
- Test shard: Rook now sits on a bench by the Albatross tavern, just south of the square, instead of standing in the middle of it. The isle's moongate sets you down in front of him.
- Test shard: the housing lesson has three clear stops, each with a signpost and one thing to learn: a small house, where houses can be built, and keeping your deed in the bank.

### Fixed
- Test shard: in the thieving lesson, opening the practice chest no longer leaves you unable to finish Remove Trap: the lesson sets a new trap on the chest.
- Test shard: the leftover tutorial farm west of New Haven is gone.

## 2026.10.31.10

### Staff
- Build reviews no longer wait forever for a volunteer: a building or decoration review nobody has taken after two hours goes to the Iron Golem, in a fresh session that did not build the work. Building work never reaches the shard unreviewed on a time-out any more; only a review or the overseer's skip lets it through. A prompt resuming after Chase answers its question keeps its place ahead of a Run next picked meanwhile.

## 2026.10.31.9

### Changed
- Test shard: Chivalry and Necromancy each begin with a teacher's quest. Until you finish it you cannot cast that school's spells or train it, whatever books or scrolls you carry. Sister Aveline at Fort Whitestone teaches Chivalry (A Place on the Watch); Oren Pike at the Dhampire Ruins teaches Necromancy for the Keepers of the Last Door (Names for the Last Door). Each quest gives you the school's book and a few starting supplies, once. Your skill starts at zero, and you can learn both.
- Test shard: a new character starts as a Fighter, Archer (Thrower for gargoyles), Mage or Maker, or chooses its own skills from a shorter list. Paladin, Necromancer, Samurai and Ninja are no longer starting choices: their skills are learned in the world.
- Test shard: in the tutorial, the Chivalry and Necromancy lessons show you the school and send you to its teacher. The casting lesson opens once you have learned the school.

### Staff
- [SchoolGate on, off or auto turns the Chivalry and Necromancy gates on or off (auto: the test shard only); [SchoolGate status, grant and revoke work on one player and are logged. [Initiation place puts the two teachers, the patrol, the courtyard ankh and the Dhampire graves in the world on the test shard; [Initiation reset restarts a player's quest.

## 2026.10.31.8

### Staff
- The Custodians' travel stop in Hammerhill moves onto the top step at the hall's south doors, where you can see yourself arrive; on the street below, the house south of the lane hid you from view.

## 2026.10.31.7

### Changed
- Test shard: the Wakening Isle's vision now takes you into a small dungeon shrine. You watch, unseen, as the Stranger walks out of the dark to the Gem on its pedestal and strikes it, and the Gem breaks into shards in a flash of light. When the vision ends you are back exactly where you stood.

## 2026.10.31.5

### Staff
- The overnight play-test bot's New Haven checks (a new character pointed at the Guild Board, and the patch notes on an account's first login) make their character with the New Haven start again, on an account that has skipped the tutorial, now that new characters wake on the Wakening Isle. When an account still has New Haven locked, the bot first has a throwaway character wake on the isle and its Admin skips that tutorial.

## 2026.10.31.4

### Changed
- Test shard: the Wakening Isle's coast reads as one smooth shore: the surf follows the sand all the way round, with no square notches or sand-coloured lumps, and driftwood, seaweed and shells lie along the waterline.
- Test shard: the shipwreck on the waking beach is now a whole ship, both halves, run aground across the waterline: one half in the shallows, the other up the beach, with driftwood round it.
- Test shard: every sign on the Wakening Isle stands on the north side of its path, facing it, and the sign for the moongate stands where the road meets the dock, where you see it before you walk out.
- Test shard: the isle's farm is turned round so its lessons run one way: the field is to the west with its gate on the east side, and the farmhouse, where Pip waits to be bandaged, stands right beside the gate. The field has ploughed soil and fewer, lower crops, so the rats are easy to spot.
- Test shard: the practice bank sits in the grass with a small paved step at its door and a paved spot round the fountain, instead of a huge square of paving, with plants by its walls and more inside: scales and coin on the counter and a strongbox in the vault.
- Test shard: the League lodge has an even wooden floor and is furnished as a meeting hall: bookcases on its walls, a long table with chairs, a brazier and candles, and its banners outside between the windows.
- Test shard: the farmhouse on the Wakening Isle is set right: its fireplace stands against the back wall under the middle of the roof, so no chimney pokes through the thatch, nothing stands in front of its windows, and its door opens inward along the wall instead of swinging out.
- Test shard: the farm fence closes neatly at its corners, with a post at one end and a corner piece at the other.
- Test shard: the League lodge has a sign with its name, beside the League lesson sign where the path turns to its door.
- Test shard: the beach below the training yard runs straight along the yard's south and west sides, so the shoreline there no longer looks stepped.

## 2026.10.31.3

### Staff
- The Rangers' guildmaster at Fort Viridian now stands in the fort's open inner court, where you can see him; in the south court the rampart hid him from view. [ClusterFGuildHalls place moves a guildmaster left at the old spot, and its reply lists what it took away from a hall's old site on a line of its own.

## 2026.10.31

### Staff
- Six guild headquarters move onto the Siege of Sosaria land, in places on Zigholtul's map: the League's headquarters (with its own steward, Aldous Penhallow; Elara and joining stay in New Haven) and the Merchants' Exchange in Valunstead, the Healers' Covenant at Fort Whitestone, the Rangers' League at Fort Viridian, and the Custodians and the Tinkers' Union in Hammerhill, each in its own building. [ClusterFGuildHalls place puts them there on the test shard and takes away anything a hall left at its old site. The Society of Smiths stays at Serpent's Hold. Joining a guild never needs a headquarters.

## 2026.10.30.3

### Changed
- Test shard: waking on the Wakening Isle's beach is easier to see. The dusk is lighter, so you can see yourself lying on the sand, and "You lie on the wet sand..." shows over your head. When you move, your character gets up slowly, then the light comes up, and only then do the patch notes and other windows open.
- Test shard: in the healing lesson, if you have no bandages, Wren tells you where to get more. Double-click Wren for more, or pick up bandages from the table beside Pip in the farmhouse.

## 2026.10.30

### Staff
- Blackthorn Dungeon is rebuilt for the Siege of Sosaria land, away from the author's dungeons that now stand where it was: the whole dungeon, its invasion rooms too, in a place of its own beside the Wakening Isle, entered by a cellar hatch in the south-west storeroom of Blackthorn Castle's keep on Trammel (Felucca has no way in). Run [GenBlackthorn once on a shard whose map has it; it refuses while the land is missing. The players' line waits with the swap's other go-live lines.
- The Siege of Sosaria swap's credits are written and wait for the go-live release: the player lines for that release's patch notes and the wiki steps are in player-package/vendor/sos-trammel-map/go-live-changelog.md, and the world map page is in the wiki's drafts (uo:drafts:world_map). Players see none of it while switch.txt says off.

## 2026.10.29

### Changed
- Test shard: the Wakening Isle has real beaches now: sand runs down to the water and the surf breaks along the shore all the way round, instead of a jagged edge of square sand tiles against the sea. You can walk the sand right to the water's edge.
- Test shard: the Wakening Isle is a tropical island: palms, banana trees and jungle trees, ferns, fan plants and grasses, thick in the island's middle and thinning out toward the beaches. The road, every lesson stop and the beach where you wake stay clear.

## 2026.10.28.12

### Fixed
- Test shard: waking on the Wakening Isle's beach, your first step or turn now gets you up in the game client; before, it did nothing and you lay there for a minute.
- Test shard: while you lie on the beach you are drawn lying on the sand, in the dark, and the light comes up as you stand; before, you showed standing in daylight.

## 2026.10.28.10

### Added
- Test shard: the Wakening Isle is dressed up: a shipwreck on the beach where you wake, a lighthouse on the point by the dock, a stone frame with steps round the moongate at the end of the dock, and a brass fountain on the bank's square. Thanks to Celestial, Milva, Carl Hamilton and Lagatha, whose designs these are.

## 2026.10.28.9

### Added
- Pet Info: click your evo pet with the right mouse button and choose Pet Info, or type [PetInfo and click it, to see its kind, its stage, how close it is to the next one, its loyalty and how it grows.
- Test shard: a new lesson on the Wakening Isle, Healing: Pip the farm boy sits hurt on a bench inside the farmhouse, and you bandage him.
- Test shard: on the Wakening Isle, the Quests button on your character window shows your lessons: which are done, the one you are on with its steps ticked, and what comes next. Your other quests are one click away.

### Changed
- Test shard: at the farm, your pet is now Wren's thanks for clearing the rats and snakes: first the pitchfork, war mode and the pests, then you choose a dog or a cat, then its commands.
- The cloak of the Wakening Isle and Wren's pitchfork now help you heal: worn and held on the Wakening Isle they make a rat fight safe, and elsewhere they still give a little hit point regeneration.

## 2026.10.28.8

### Changed
- A new character's first login no longer opens the game client's own "Version History" window on top of the world. You can still open it any time by typing -version.

## 2026.10.28.7

### Changed
- Test shard: waking on the Wakening Isle's beach, you stay down on the sand until you move; your first move gets you up and the first lesson starts. If you wait, the System says "Move to get up."
- Test shard: on a new character's first login, the patch notes, your first achievement and the welcome lines wait until you have stood up on the beach, and the guild welcome waits until you reach town.
- Test shard: at the clothes stop, a chest appears with your cloak of the Wakening Isle inside: double-click the chest, then drag the cloak to your backpack or onto your character. Only you can open it, and it goes once you have the cloak.
- Test shard: at the farm, Wren hands you the pitchfork first: hold it and switch to war mode, then teach your pet its commands, then the pests.
- Test shard: when the cabbage rows are clear, Wren, the System and the lesson window all say so and tell you to go back to Wren, and the arrow points at her. Each finished lesson names the next one and points at its signpost, and typing [lesson opens the lesson window again.

### Fixed
- Test shard: Wren no longer says "Defeat 0 more rats or grass snakes"; she says what is still left to do.

### Staff
- New Wakening Isle pieces (like Pip at the farm) now appear on the test world by themselves at each restart, on the isle and on every copy; the server log lists what was placed.

## 2026.10.28.6

### Added
- Test shard: a new character's first moments on the Wakening Isle: you lie on the wet sand in the dark while the waves wash in, then get up as the light comes.
- Test shard: dying on the Wakening Isle tells you what to do: you are a ghost, follow the arrow to Sera the healer to come back to life, and (if your things are on your body) follow the arrow back to it and double-click it.
- Test shard: at the farm, before the pests, Wren gives you a pitchfork to keep and teaches you to fight: hold the pitchfork, press Tab (or the Peace/War button) for war mode, and double-click a rat to attack it yourself.

### Changed
- Test shard: the farm's bandage lesson has a patient who stays hurt: Pip, the farm boy beside Wren. Wren tells you a bandage takes a few seconds; on the isle your own bandage takes 4 seconds, you see how long to wait, and a failed try says so plainly so you can try again.
- Test shard: the loop vision at the standing stones is lit from the start, the Stranger walks up to the Gem and strikes it with his sword, and the whole vision is slower and easier to read (about a minute). Windows that covered the middle of the screen close when it starts.

## 2026.10.28.5

### Changed
- A new character walks until you turn on Always run (Options, then Gameplay, then Movement), and starts with its character window closed: double-click yourself to open it. Characters you already have keep their own settings.

## 2026.10.28.4

### Changed
- Test shard: Wren's farmhouse on the Wakening Isle is rebuilt as one whole thatched cottage: roof, walls, steps and door line up, the chimney is part of the hearth inside, and it is furnished, with its own sign by the door.
- Test shard: the farm field has one tidy fence all round, with the animal pen closed inside it and the field gate at Wren's spot the only way in.
- Test shard: each lesson on the Wakening Isle is marked by a standing signpost named for it ("Lesson 5: The farm ..."), in place of the blue rune on the ground. Double-click the sign to start that lesson.
- Test shard: the practice bank looks like a bank: a pitched roof, windows, a bank sign by the door, a counter with Merrin behind it, vault chests, a carpet and lights. The lesson's arrow points at the door, then at Merrin once you are inside.
- Test shard: the League lodge has a roof, its door in the north wall, League banners outside, and inside a desk for Elara, bookshelves, chairs and a notice board: double-click it to read about the League without joining.
- Test shard: the bank's and the lodge's doors fold back against the wall when open instead of swinging out past it.

### Fixed
- Test shard: you can walk onto the dock to the Wakening Isle's moongate: its first planks were a step too high from the beach.

## 2026.10.28.3

### Added
- Test shard: on the Wakening Isle, Tamsin, Wren and Elara speak first when you come near and tell you to double-click them, every half minute until you do.
- Test shard: your cloak from the Wakening Isle is yours to keep. It waits on the table by the clothes rack: drag it onto your character to wear it. It stays with you after death and cannot be sold, traded or dropped.
- Test shard: optional client tips, at any time from Tamsin's or Rook's window. The first shows how to turn on Always run.
- Test shard: at the farm, Wren teaches pet commands before the pests: say "all follow me", then "all guard me" (or use your pet's menu), and let your pet take at least one pest with "all kill". She hands you bandages, says how to use them, and how to make more with scissors and cloth.

### Changed
- Test shard: the backpack lesson starts by opening your character window: double-click yourself, or click the paperdoll button.
- Test shard: the farm's rats and snakes stay behind the field gate and leave you alone until your pest lesson; Wren opens the gate when it starts.
- Test shard: the signposts on the Wakening Isle are proper standing signposts, named so you can tell what they say.

### Fixed
- Test shard: fighting on the Wakening Isle no longer floods the screen with skill gains: each skill can rise at most once every 10 seconds there.

### Staff
- clusterf.tutorial.gainSecondsPerSkill (10) sets the Wakening Isle's gain pace; 0 turns it off.

## 2026.10.28

### Changed
- Test shard: your account's first character starts on Tutorial Island. "New Haven" shows as locked on the character creation screen until one of your characters finishes or skips the tutorial; from then on, every new character on the account can choose New Haven, even if that character is later deleted.

### Staff
- Staff accounts always see both starts. The reset window's account flags box ([ResetMyAccount, [ClusterFReset, the Reset Stone) locks New Haven again, so day one can be tried again; [Tutorial reset also clears the account's tutorial record.

## 2026.10.27.4

### Added
- Test shard: the Wakening Isle is your own. Each new player, or a party together, gets a private copy of the isle, so nobody else's pests, pets or doors get in the way of a lesson. Log out and back in within half an hour and you are still there; after longer, you start a fresh copy at your next lesson. If every copy is busy, you wait a moment on the beach and are taken to yours as soon as one is free.

### Changed
- Test shard: the loop vision at the Wakening Isle's standing stones is now a short scene of its own, under a minute: the world darkens, the Stranger strikes the Gem, your own echo wakes and speaks, and a hooded figure of the League appears. Only you see and hear it, words show in the middle of the screen, and it changes nothing about your character. If you log out or are interrupted, you are back at the stones and can watch it again.

### Fixed
- Test shard: the farm's healing lesson on the Wakening Isle can always be finished. Wren gives you a small scratch to bandage when nobody is hurt (and so does a bandage that finds nobody hurt, for example when your health came back by itself), and her practice bandages always work, even if you have not learned Healing.

### Staff
- [Tutorial copies lists the isle's 16 private copies, who is on each, and who is waiting.
- The loop vision's two full-screen stills are reserved and off (clusterf.tutorial.visionPanels); placeholders ship until the drawings do.
- The player package keeps the stock Trammel and Felucca land until the Siege of Sosaria swap goes live: the new map is built and checked but stays out of the download, behind one switch (player-package/vendor/sos-trammel-map/switch.txt, off). The swap's go-live release turns it on.
- clusterf.tutorial.copyHoldMinutes (30) sets how long an empty copy of the isle is held before it resets; a test world can shorten it.

## 2026.10.27.3

### Added
- Every town's vet can now bring your dead bonded pet back to life. Walk up to the vet with your pet's ghost nearby, once its body has disappeared, and choose the pet in the window. The vet charges a fee from your bank that depends on how hard the pet is to tame, and the pet loses a little of each skill.
- On the Wakening Isle, while you are Young, your pet's ghost comes back to life the moment it reaches Tobin the vet at the farm gate: free, with no skill loss, and with everything it was carrying. You do not have to wait for its body to disappear.
- At the farm, Wren tells you what to do if your pet dies, and Sera the healer at the shrine sends pet ghosts to Tobin.

## 2026.10.27.2

### Fixed
- Test shard: choosing "Tutorial Island" while the Wakening Isle is not yet on the world's map now starts you in New Haven with a note saying so, instead of in empty darkness. Your account's tutorial stays unused, so a character made once the isle opens can still take it.

### Staff
- A staff character made with "Tutorial Island" selected starts at New Haven's start, not in the sea where the island's button is drawn. With the tutorial off, New Haven's start comes from the same place as every other New Haven spot. The tutorial's lesson marker and moongate at the end of the dock stand on the dock, not on the sea floor under it.

## 2026.10.27

### Added
- Test shard: a new character chooses where to start. "Tutorial Island" (the first choice, recommended) wakes you on the Wakening Isle; "New Haven" skips the tutorial and starts you in town.
- Test shard: the tutorial is on the Wakening Isle now, one road from the beach to the dock. Tamsin on the beach starts each lesson; the farm, with your pet, is one of them; the moongate at the end of the dock is the last, and it takes you and your pets to Rook in New Haven, who graduates you.

### Changed
- Test shard: Rook's "Restart the tutorial" sends you to the Wakening Isle's beach; the dock's moongate brings you back to him.

### Staff
- [ClusterFSeedTutorial now seeds the tutorial on the Wakening Isle (the Fold, map 32) and refuses until the isle's map patch is on the server; Rook and the town vet stay in New Haven. [ClusterFSeedTutorialFarm now places only New Haven's farmhouse, door and pen, for the optional lessons there. [Tutorial enter also sends the player to the isle.

## 2026.10.26.13

### Added
- Redrawn world art you can turn on: Options, Interface, Appearance, "Redrawn world art (Zigholtul)". Fuller trees, crisper furniture, chests and crystals, drawn over the stock look; only you see it, it changes at once, and it is off until you tick it. By Zigholtul. Update your game through the launcher to see the option.

## 2026.10.26.12

### Staff
- The website's address now always points at the house, with the game PC's VPN on or off: the DNS updater takes the house's address from the home server, and it refuses to publish, and leaves the address alone, when the home server cannot say it or says an address known to be a VPN's. A test shard restart stops an old DNS updater that could publish the VPN's address.

## 2026.10.26.11

### Staff
- The Fold, the future home of the new player isle, now exists as its own world (with 16 private copies) on Trammel's map, closed to houses, recall, mark and gates, always in daylight. Nobody can reach it yet; on the test shard, [Fold goto <n> takes staff there.

## 2026.10.26.10

### Fixed
- Persuasion: when you held more than one courier letter from before the one-job rule, the replaced ones are torn up when the server restarts, so a replaced letter can no longer become payable again later.

### Staff
- Guard witness log: a second crime by the same person in the same instant and spot (a victimless crime right after an attack, or a second attack on the same victim) is now its own entry with its own witnesses, instead of being added to the earlier one.

## 2026.10.26.9

### Fixed
- Trash barrels pay for what is actually thrown away. Lifting part of a stack back out no longer leaves the whole stack's points behind, and two players' stacks that join in a barrel each pay their own share.
- A new trash bag from a Sanitation Warden no longer destroys the old bag's contents. They move into the new bag, or your backpack or bank if they do not fit.
- The Custodians' sweep, cleanup and "Clear corpses nearby" only take what you could reach yourself: nothing behind a wall, on another floor, or loose in someone else's house. A corpse holding a blessed or quest item, or a filled container, is left alone.
- The Clean Up store no longer completes a purchase after you have changed facet, gone to another floor or the trader is gone.

## 2026.10.26.8

### Fixed
- Tracking Ping: if too many creatures crowd in while a ping is running, it ends free, with your stamina and wait given back.
- Tracking Ping: the Ping button on an old Tracking menu no longer skips the wait from a skill you used since.
- Tracking Ping: players hidden from you by a tournament duel, and their pets, are no longer found by a ping and do not feel watched.
- Tracking Ping: a ping no longer reads the area or warns anyone after its time is up, even when the server is slow.
- Persuasion: the menu checks again when you choose. If you have died, changed facet, moved away or out of sight, or the person can no longer be asked, nothing happens and nothing is spent.
- Persuasion: a courier letter left in your bank still counts as your one job. Deliver it, or let it go stale, before asking for another.
- Persuasion: bankers in part of a town, such as Luna's bank, count as that town for courier letters, and gypsy bankers and minters take banker letters.

### Staff
- Test-shard-only features now refuse to switch on anywhere else, and their config switches do nothing there: [ItemKnowledge, [GuardWitness, [LeagueInvestigationSeed, [LeagueEcho (status still works), [ClusterFTownsfolk, [LevelGearProcs and [Leaseholds.
- Item identification: unidentified items keep their blessed, cursed, insured, blessed-for, quest-item and locked-down labels; a fully identified item still hides any line nobody has reviewed; Use Best Weapon Skill and skill bonuses now count toward how hard an item is to appraise.
- Guard witness log: each separate crime by one person is its own entry, only people who could see the victim count as witnesses, a pet's crime names its owner without a second entry, and people the guards would ignore (blessed, staff, invulnerable) leave no entry.
- League warrants: a warrant can no longer go inside a bag (one already in a bag moves back to the top of the pack or bank after a restart). The seeder refuses a site when the facet's map or statics files are not loaded, or when any region over it is not allowed, including one hidden under another region or at another height.
- League Echo: it stops attacking the moment its caller stops qualifying, ends at once when its caller disconnects, cannot be called while offline, and its death no longer counts toward pet-loss achievements.
- Townsfolk: Mara and Ellis can no longer be moved by "<name> move", pet orders said beside them reach the pets, and the dialogue table refuses repeated answers, pet-order words, a wrong region for a resident and topic names that are not plain words. A table file that cannot be read keeps the old table.
- Weapon milestones: the bought effect shows on the weapon's tooltip, the confirmation charges only the price and level it showed, its page says when the effect is switched off and shows the level it was bought at, and a chest mimic with its lid shut no longer uses up a strike.
- Town houses: [Leaseholds refuses a building not wholly inside a guarded town (upper floors included), a street spot that cannot be stood on, another structure reaching into the building, a town addon container inside, and one of a linked pair of doors without the other. A town house's storage now holds twice its lockdowns, as stock houses do.

## 2026.10.26.7

### Staff
- Town houses, first piece (test shard only; no rent and no player leasing yet): [Leaseholds new <lowest z> <highest z> makes a town house site over an empty town building (target two corners of its floor, then the street spot outside its door), and refuses one that holds a creature, spawner or house, overlaps another site, or reaches outside the town. [Leaseholds door registers the building's own doors. [Leaseholds assign gives a player a test lease with no money: the building becomes their house (house sign, house doors, lockdowns, friends, the account's one house) while the town guards still answer inside. [Leaseholds end gives the building back with its own doors; [Leaseholds lists sites, and go and remove work on them. Belongings are not packed yet when a lease ends: empty the house first.

## 2026.10.26.5

### Staff
- Weapon milestones, first piece (test shard only; off until [LevelGearProcs on): a levelable crafted weapon at item level 100 shows a Weapon milestone button in [ItemLevel. For 100 of its points and one property slot it can bind one permanent effect: Stormstrike (12 energy damage) or Emberstrike (12 fire damage). On a damaging hit against a wild creature you are fighting it has a 5% chance to strike, then waits 10 seconds; that wait is shared by you and your companions and stays on the weapon if it changes hands. Players, pets and summons are never struck by it. [LevelGearProcs shows the switch and your wait. Level dormancy (a weapon above your own level) is not built yet.

## 2026.10.26.4

### Staff
- New Haven townsfolk talk, first piece (test shard only; off until [ClusterFTownsfolk on, and nobody is placed yet): [add ClusterFTownsfolk plaza makes Mara, the town steward, and [add ClusterFTownsfolk trades makes Ellis, the workshop runner. Standing within 3 tiles, say hello, name, job, help, inn, rumors or goodbye (or double-click them, or choose Talk) and the nearest one answers you alone, once every 3 seconds. A character still in the tutorial core is sent to Rook. [ClusterFTownsfolk reload re-reads the dialogue table and keeps the old one if the new one has a mistake.

## 2026.10.26.3

### Staff
- League Echo, first piece (test shard only; off until [LeagueEcho on, and no Tome or Registrar page yet): a Mythril League character can call an Echo of the Stygian Dragon with [LeagueEcho call. It is a weak, friendly melee helper in League blue that stays 20 seconds, uses 2 follower slots and then lets the account call again only after 30 minutes. It fights wild creatures only, leaves no corpse or loot, can be dispelled, and fades if its caller logs out, dies, goes more than 12 tiles away, changes facet or enters a town, house, jail or other protected place. Felucca is refused. [LeagueEcho shows the switch and your account's wait; [LeagueEcho dismiss sends yours away.

## 2026.10.26

### Staff
- League investigation warrants, first piece (test shard only; investigations are off and nothing is sold yet): [add LeagueInvestigationWarrant makes a warrant for The Siegebreaker or The Hollow Oak, bound to the first player who picks it up. Double-click gives directions to its site, or the reason it cannot be called. [LeagueInvestigationSeed dryrun lists the investigation markers it would place, at Covetous's harpy hall and the Yew woods, and refuses a site near a town, a house or the Covetous Void Pool.

## 2026.10.25.14

### Staff
- Item identification (test shard only, off until switched on with [ItemKnowledge on): new magic weapons and armor from monster loot start unidentified. Appraisal names their magic from 30 and reads its values from 60. Arms Lore assesses resists, damage types and condition from 30 and measures them from 60. What anyone learns shows to everyone. Existing and crafted items are never hidden. [ItemKnowledge conceal marks a weapon or armor as an unknown find.
- Guard witness pilot (test shard only, off until [GuardWitness on): in Cove on Trammel the server notes who could actually see each crime (in range, in sight, not hidden) and which guards could hear each call for the guards. [GuardWitness log lists them. It only watches: guards still answer exactly as before. It is the groundwork for guards that walk to what they see.

## 2026.10.25.13

### Added
- Persuasion, Ask about work: use Persuasion on any banker and the Merchants' Exchange hands you a sealed letter to deliver in person (drag it onto them) to someone in another town; beginners get an errand across their own town. Each delivery trains Persuasion, all the way to 200, and harder letters go farther.
- Persuasion, Ask for help: a townsperson may spare a few coins from their own purse (up to 14, and at most 50 gold a day per account) or a loaf of bread (two a day). Asking no longer costs you karma.

### Changed
- Begging is now called Persuasion. It is the same skill with the same points. Using it on a person opens a short menu of what you can ask. Your client's skill list may still say Begging until the next client update.
- Asking for help only trains Persuasion up to 20, once for each different person who helps you. Courier letters are the way to train past that.

## 2026.10.25.11

### Added
- Tracking Ping: at 90 Tracking (trained skill; gear does not count) use Tracking and press Ping, or type [TrackingPing, to sense the creatures around you on your floor for a few seconds. Your chat gets the count (hostile, neutral, friendly, and how tough the hostiles look), and [TrackingPing results lists them. A ping costs 15 stamina, reveals you, and can be used once every 30 seconds. Its range and length grow with Tracking up to 200.
- From 120 Tracking, a ping also finds players you can see within 12 tiles, and they feel watched. Hidden players are never found, and neither are the pets of hidden owners.
- Ping does not work inside dungeons yet, and dots on the mini map come in a later update.

## 2026.10.25.10

### Changed
- The guild and League menus (guildmasters, dashboards, contract ledgers, the League board and register) have a new stone and bronze frame in the Shattered Legacy client. Other clients keep the classic look.

## 2026.10.25.8

### Fixed
- Treasures of Sorcerer's Dungeon: when the event ends, every undead it raised now goes with it, including those left standing in earlier rooms and the ones hatched from Jack the Pumpkin King's unholy pumpkins.
- Jack the Pumpkin King's unholy pumpkins now land only inside Sorcerer's Dungeon while he is the event's boss, and never after the event has ended.
- Killing someone's tamed pet in Sorcerer's Dungeon no longer counts toward an artifact of Enchanted Origin.

## 2026.10.25.7

### Fixed
- The tutorial farm's Farmhand no longer counts a bandage gift your full backpack could not take. Make room and ask her again.
- If the character doing your account's tutorial is deleted or moved to another account, your next character carries on with it, lessons and rewards kept, when it logs in or double-clicks Rook.

## 2026.10.25.6

### Fixed
- A Hunter's Satchel takes only hides, leather, bones, wool, feathers and raw meat however they go in: dropped into its open window or into a bag inside it, as well as onto the satchel. Hides dropped into its open window are cut into leather too.
- A trash bag never clears a bonded pet's corpse.
- "Clear corpses nearby" clears only the corpses its warning counted. If you move away, change facet or put your trash bag away before saying yes, nothing is cleared.

### Staff
- The launcher (with the next player package): a check that cannot read a file no longer throws away the art list the game starts with; two starts at once share one rebuild; the time limit now covers putting the new art in place, and an art set left half swapped is put back; the progress bar names the map patch while it is read; after an update that ran out of time, the game no longer waits for the art a second time.

## 2026.10.25.5

### Fixed
- Companions stay yours: they can no longer be transferred or traded away, they never turn wild from neglect, a companion with a weapon fights with its full damage right after a restart, and companions left out when you log off (or when the server restarts) are safely put away and come back when you log in, even while companions are switched off.
- Unraveling a container breaks down exactly the items you approved: anything added or swapped in after the preview is left alone, blessed items are never unraveled, and nothing behind a locked chest is touched.
- The tutorial's loaned cloth, logs, bow and arrows can no longer be cut, chopped, split or kept: the trainer takes back every piece, and after a restart your tithing points and Hiding are given back exactly.
- The Outriders guildmaster's menu sells only when you are standing with a guildmaster, checks your pack has room before taking your gold, and refunds you if the item cannot be delivered. The tutorial vet no longer charges for a pet you walked away from.
- The Exodus encounter finishes moving players out of Ver Lor Reg after a restart, and a restart cannot raise a second Exodus. Either Asclepius staff now adds its bonus to bandage healing, the Exodus rewards are worth cleanup points, and a revealed Exodus chest no longer stays open after a restart.
- The Void Pool's Cauldron of Transmutation keeps its charges through a restart, every player who sets a new best battle score is recorded, the record battle keeps its scores, and a restart clears the old wave before the next battle starts.
- If the server starts without the Rootdeep map, the way down into the Old Haven Burrows is closed instead of dropping you into the void; the ladder out always works. Young players stepping into the Burrows are warned they are leaving Young protection.
- Extra jewelry worn in the League's accessory slots can always be taken off, follows the usual equipment rules, and counts toward level gear, imbuing effects, Fortune's Favor and the jewelry achievements.
- Guessing wrong twice in a row on an Underworld puzzle board or a trap-training kit no longer stops the board working; a mini champion switched off by staff stays off after a restart; the magic key takes back its puzzle keys and boards from bags inside your pack.

### Staff
- The play-test page keeps each answer with its own question when notes change, shows an answer whose question was removed or reworded instead of hiding it, refuses a stale "Taken into" or "Not taken", and keeps what you were typing when the page refreshes.

## 2026.10.25.3

### Changed
- The tutorial's words are clearer: every lesson, hint and window now says plainly what to click and where to go.

## 2026.10.25.2

### Staff
- Our game client now animates the guild hall decor (the bubbling vat, the specimen tanks, the crystal ball, the fire pit and blacksmith, the floating crystals and arc orbs); before, each piece stood still on its first frame. It reaches players with the next client build and player package. Nothing of the decor is placed in the world yet.

## 2026.10.25

### Added
- Fish mongers stand on the docks of Britain, Moonglow, Trinsic, Vesper, Jhelom, Skara Brae, Papua and the Sea Market. Bring your boat close and talk to one: he gives you an order for a catch of fish to deliver to another port, puts a shipping crate for it in your hold, and pays you from the Order of the Dragonfish when you deliver it. Destroying the crate forfeits the order.
- Fish mongers reward you with bait, magic fishhooks, lava lobster traps, rare fishing books, Fishing power scrolls and the Oracle of the Sea. [FishMongerStatus tells you your standing with them.
- Bait: put it on a fishing pole (fish bait) or a lobster trap (crab and lobster bait). It makes the fish it is for twice as likely to bite, three times with enhanced bait. Each catch uses one.
- Magic fishhooks: tie one to your fishing pole. A lava hook lets you fish in lava, a junk-proof hook mostly keeps old boots off your line, and a dredging hook brings up barnacle-covered shipwreck finds. Each catch wears the hook, and a worn-out hook leaves a plain pole.
- Lava fishing: with a lava hook, cast into lava for lava fish, stone footwear, stone pavers and cracked lava rocks. Split a rock with an axe to find a geode or a lava rock. Stone footwear is wearable, but you cannot move while you wear it.
- The Oracle of the Sea: a spyglass that tells you where Charybdis will rise. Cast a pole baited for charybdis into that spot with 100 Fishing and the Oracle in your pack, and Charybdis rises.
- Fish pies: bake the sixteen rare fish steaks and meats into pies (Cooking, band II recipes, at an oven). Each gives a five-minute boost: Meditation, Focus, damage, hit or defense chance, spell damage, regeneration, or a soak against one type of damage.
- Charybdis now leaves two to five fish pies.
- Fishing up an SOS's wreck can now bring up oars, fish heads, unfinished furniture, a yellow polka dot bikini, or rarely a ship's strongbox.

### Staff
- [GenFishMongers places the sixteen fish mongers (eight docks, Trammel and Felucca). Run it once on each shard; it skips docks that already have theirs.
- [add CorgulTheSoulBinder places Corgul the Soulbinder; set SpawnCrew in his properties to raise his crew. His altar and the island warp are not built yet, so players cannot reach him.
- [CharydbisSpawner opens the charybdis spawner's properties.

## 2026.10.24.18

### Added
- The Masterwork Hammer of Hephaestus, the hammer's third tier: 800 uses, +15 Blacksmithy while held, and metal familiarity up to 5,000 a metal. A Society of Smiths Master (15,000 standing) with 100 Blacksmithy can have the Reinforced Hammer upgraded at the guildmaster; your familiarity comes with it.
- Its bonus: metals you know well waste fewer ingots. A successful craft gives back up to a quarter of its ingots at full familiarity with that metal, less the less you know it.
- Craft This Order: hold the Masterwork Hammer at an anvil and forge, pick Craft This Order on its menu and target a smith bulk order in your backpack. The hammer crafts what the order still needs, one normal craft at a time (your skill, your ingots, one hammer use each), and puts each piece that fits into the order. It stops when the order is full, or when you run out of ingots or hammer uses, move, get hurt, or press Stop. A large order first takes any finished small orders for it from your backpack. Update your game through the launcher to see the menu entry's name.
- Bulk order deeds now show guild members what handing them in pays: a Society of Smiths member sees the Seals (or the gold, if completed orders are set to cash out) and the standing, in the deed and its tooltip; a member of the Tailors' Circle or another order-giving guild sees the gold their guildmaster pays. The numbers shown are exactly what you are paid.

### Fixed
- The Artificers' guildmaster and the Order's welcome message now tell it as it works: the Apprentice task pays your first Standing and Essence Shards, and what you can imbue, and how strongly, comes from the formula tiers you learn.

### Staff
- [add MasterworkHammerOfHephaestus gives the T3. The T4 (two T3s combined) is still not built.

## 2026.10.24.17

### Staff
- The guild hall towers from [DecorHallStructure can be walked into: each has a door that opens on a double-click (it closes itself after 20 seconds) and a floor inside.
- The guild hall decor commands refuse, and place nothing, when the template changed since that spot was dressed (clear it first), when the new placement would overlap the same set placed one tile off, or when a piece would stand in a wall, an object or a doorway. A dry run lists what is in the way.

## 2026.10.24.16

### Fixed
- The Eodon cliff vines cannot be climbed while paralyzed.

### Staff
- A tamed dragon turtle hatchling now keeps its stats and instead loses skill on its first tame, as the original does: its skills fall to about 40% of a wild one's and cap at 100. Taming it back after a release takes nothing more.
- [GenEodon's spawners now pick their creatures in the original proportions, so the white and black tigers are rare again (1 in 25 on the tiger rows, not 1 in 8). Run [GenEodon again to update a world that already has them.

## 2026.10.24.15

### Fixed
- Some hidden achievements now check their conditions more carefully.

## 2026.10.24.14

### Fixed
- The first time you help defeat a boss, its loot box now arrives.
- Kills by your pets and summons now count for the League bestiary.

## 2026.10.24.12

### Changed
- Abandoning a commission from the Smith Guild Book now waits like abandoning one at the guild.
- Large commissions share that wait: after abandoning a commission of either size, you wait before requesting the next one of either size.

## 2026.10.24.11

### Fixed
- The New Haven tutorial gives each account one starter pet: the Farmhand's or the graduation pick, never both.
- A graduation pet you have not chosen yet waits for you: closing the window, logging out or a server restart no longer loses it, and Rook offers it again.
- Only the character who started the tutorial can take its lessons, skip, graduate or choose its pet. Your other characters see Rook's page but cannot change it.
- The Farmhand hands out a limited number of bandages per account.
- The farm's patch-up lesson now finishes only when your bandage actually heals someone.
- Players not in the tutorial can no longer attack tutorial creatures.

## 2026.10.24.10

### Changed
- The League Register's Blaze repair stays free, for the gear you are wearing. It no longer repairs gear you carry or bank; a remote repair deed still does.
- The League Field Satchel counts as one item only while it is in your backpack. In your bank box or a house's secure storage, everything in it counts.

### Fixed
- The League Field Satchel takes your guild satchels again however full they are, and ore, logs, hides and essences go into the satchels inside it.
- A satchel the League Field Satchel refuses no longer costs its merge fee, and moving a satchel around inside it is free.
- Buying from the League Registrar, or taking a guild out of your Register, with a full pack no longer drops the item at your feet. You are asked to make room, and nothing is taken.
- Turning in every ready order from the League Register now finds complete smith deeds in bags and books in your pack.

## 2026.10.24.9

### Fixed
- Levelable gear no longer gains experience from killing pets.
- Fletching, carpentry and tinkering bulk orders now take only items a player crafted, like every other bulk order. A bow, staff or cleaver bought from a shopkeeper no longer fills them.
- A hidden achievement was sometimes granted when it should not have been.

## 2026.10.24.8

### Added
- The Miners' Compact sells its own copy of Mining For Quality Stone: Learn Stone Mining on Garrett's member dashboard, 50 Mining Vouchers from Journeyman rank. Reading it still needs 100 Mining.
- Short of Mining Vouchers to restore your Jacob's Pickaxe? The Liaison can cover only the missing vouchers in gold, at 100 gold each, and shows both prices before you confirm.

### Changed
- Stone Contracts are listed only once you have learned stone mining. Until then, the ledger tells you where to get the book. A Stone Contract you already hold can still be turned in.
- The Compact's two raw ore contracts are now mining quotas: Mine Iron Ore (100 iron ore) and Mine Dull Copper Ore (50 dull copper ore, Journeyman, 65 Mining). Only ore you mine after accepting counts, and nothing is taken at turn-in. A contract you already hold can still be finished by turning in its iron ore.

## 2026.10.24.7

### Added
- Your trash bag has two new right-click options: Clear a corpse, and Clear corpses nearby. Any player with a trash bag can use them (join the Custodians at a Sanitation Warden for a free one), and you earn Clean Up points as usual.
- Clearing a corpse deletes it and everything still inside it, so it asks first when a corpse still holds loot. A player's corpse can never be cleared, and a monster someone else killed is theirs to loot until their looting rights run out.

## 2026.10.24.6

### Changed
- Cleaning up no longer hands out civic waste bundles. The Custodians' Civic Contracts now ask for Clean Up points earned after you take one, and pay the same gold as before. The points you earn stay yours to spend in the Clean Up store.
- Still holding civic waste bundles? Hand them to the Sanitation Warden: each one counts as 10 Clean Up points.

## 2026.10.24.4

### Changed
- The way down into the Rootdeep Burrows, in Old Haven's ruined bank, is now a wide burrow in a crater of dirt and rocks instead of one small hole. Step anywhere into its dark mouth to go down; the rope ladder back up leaves you just outside the bank's broken wall.

### Staff
- [ClusterFSeedRootdeep places a teleporter on every tile of the burrow's mouth, and on a world that already had the old hole it points the ladder's teleporter at the new landing instead of adding a second one.

## 2026.10.24.3

### Changed
- With our game client, a tree you chopped clean disappears, leaving only its stump, and grows back when its wood does.

### Fixed
- The stump left by a tree you chopped clean now sits on the ground at the trunk instead of floating up in the leaves.
- Holding a polearm to a chopped-clean tree now says there is not enough wood there, instead of telling you to equip an axe.

## 2026.10.24.2

### Changed
- After an update, the game launcher shows a progress bar while it prepares the shard's own art, text and map, and the game always starts: if preparing takes more than a minute, you play with the art you had and the new art is used from the next start.
- Updates now prepare the new art as part of the update, so the first start afterwards is quick.
- "Play Shattered Legacy" connects to the test shard, as the real shard is closed until it opens as a fresh world.

### Staff
- Every launch and every art rebuild is written to app\logs in the game folder (the last ten of each), so a launch that misbehaves on a player's computer can be read afterwards, step by step with its seconds.
- app\Play-Skip-Art.bat starts the game without checking or preparing the art.

## 2026.10.24.1

### Staff
- When someone sits down at a PC a Codex helper is working on, the helper now stops within two minutes for certain, saves what it had done on its own branch, and puts the prompt back in the queue for any Golem to finish from there.
- Send to the second PC on the Shard Console now reaches a Codex helper on another PC, as the web console's button already did.
- A Codex helper on the game PC shows as on while it works, takes its own Run next pick, and holds at a stopping point you ask for until you cancel it.
- A Codex helper with a newly signed-in login no longer refuses to start for want of usage readings, and no longer stays held after its 5-hour window resets.

## 2026.10.24

### Staff
- When the merge Golem's build of a finished branch goes red, it now builds once more before giving up: a test that fails only now and then no longer sends a finished prompt back for a fix-up. Every build's full output is kept for two weeks, and the builds that went red then green are listed by the test that failed, so a flaky test can be found and fixed.
- A branch that really does not build now reports the first errors (the failed test and its message, or the compiler error) in its fix-up prompt, its email and its card, instead of the last few thousand characters of the test server's chatter.
- A prompt finished through a fix-up is filed as done along with its fix-up, and a Golem whose claim timed out no longer gets back a prompt that is already in main. No more "do P263 again" hand-offs.
- Answering a Golem's question now gets it going again from every console and through another PC's agent (before, the answer was saved but the Golem stayed stopped until someone pressed Retry). The overseer can answer as "overseer (for Chase)".
- Run next picks a prompt for that one Golem: another Golem's next hand-off leaves it alone.
- The merge Golem's "needs you" clears itself once that branch merges, or once you remove its handled markers to ask for another try.
- A Golem whose copy of the prompt queue got stuck halfway through an update puts it right by itself and carries on, instead of resting with "nothing for" it.
- A Golem on another PC can be cleared from afar (a new clear command for its agent, with a reason), and a "needs you" about uncommitted changes clears itself once that Golem's work copies are clean.

## 2026.10.23.9

### Added
- Eighteen new mounts to earn. Each comes as a statuette that calls the mount to you, bonded and ready to ride.
- In the League shop (1,750 AP): four draft horses (chestnut, bay, grey and palomino), a barded warhorse and a gilded charger.
- As achievement rewards: a bonewalker steed for slaying 50 undead, an emberhoof for 10,000 kills, a moonlit unicorn for visiting every facet, and a war wolf for 500 Achievement Points.
- In loot boxes: top-tier Tamer boxes can hold a riding drake, a riding bear, a riding wolf or a saddled horse; top-tier Boss boxes can hold a marsh, dusk or sky raptor, or a bog strider.
- The new mounts stable, shrink and take a follower slot like any other mount.
- Two new evo pet lines: a horse that grows from a foal into a draft horse, then a warhorse, then a unicorn, emberhoof or bonewalker; and a lizard that hatches, runs as an ostard, becomes a raptor and ends as a bog strider.
- The evo dog's last stage can now be a great grey wolf; the windrunner is still there as the other choice.

### Staff
- [add RewardMarshRaptor (and the other Reward* mounts, GameMaster), or [add RewardMountStatuette "<key>". Every mount has a horse's numbers for now and is never tamable. Box chances: clusterf.rewardMounts.tamerBoxChance (0.10 a tier above 4) and clusterf.rewardMounts.bossBoxChance (0.15 at tier 6).
- Credits: the mounts are Thagoras' "Mount Pack" (made by Rafael Costa, UoBrazilian; renamed), Krazy Zacks' riding drake and bear (shipped exactly as he made them), Prapilk's riding wolf, foals, young lizards, calves and KR Wolf, and Socrarr's saddled horse (uo-pixel.de). All belong in the shard credits list.
- Players need the package update (about 29 MB more animation data) to see them; without it the new creatures do not draw.

## 2026.10.23.8

### Added
- New looks you can pick for your backpack, bags, metal chests (and your bank box), crates and spell scrolls: Options, Interface, Appearance. Only you see them, Classic stays the default, and the artists are credited in the game folder.

## 2026.10.23.6

### Added
- Every tutorial lesson is playable now. In the trades yard south-east of the training yard you can mine, chop, fish, light a campfire, try all nine crafts (and keep what you make), play the bard and practise the thief's ten skills on a dummy chest and mark; at the farm pen you herd, tame and care for a goat; and four short walking tours explain houses, travel, reputation and the shard's own systems.

## 2026.10.23.5

### Added
- The tutorial's combat and magic lessons are playable in the training yard east of the farm gate: pick a weapon and spar, bandage yourself and a wounded villager, or try Magery, Necromancy, Chivalry, Bushido, Ninjitsu or Mysticism with a loaned book and reagents. The yard takes its loans back when the lesson ends or you leave.

## 2026.10.23.4

### Added
- Animal trainers sell the elixir of rebirth, which brings a dead bonded pet back. Rangers' League members can buy it from their guildmaster for less.

### Changed
- The tutorial is clearer: glowing tiles, the backpack lesson first, and the recruiter shows the core lessons first.
- The tutorial's pages use bright, easy-to-read text, say plainly what each thing looks like, and the practice cloak, bag and token are bright blue. If one goes missing, "Hint and arrow" gives you another.
- At graduation the companion pick shows each pet with a picture. If you close it, Rook keeps it for you until you choose, and reminds you when you log in.
- A vet stands beside the tutorial farm's Farmhand and brings a dead pet back (free while you are Young), and the graduation box holds one elixir of rebirth. The Farmhand tells you where the vet is if your pet falls.

### Staff
- [ClusterFSeedTutorial also places the farm vet, and a vet in New Haven when the town has none.

## 2026.10.23.3

### Added
- The player wiki catches up a third time: a page for each of nine guilds (the Society of Smiths, the Artificers' Order, the Order of the Alembic, the Miners' Compact, the Foresters' Union, the Rangers' League, the Healers' Covenant, the Custodians and the Thieves' Den), a new players' corner with an FAQ and a glossary, guides to crafting here and to High Seas fishing, a dungeons page, and the Halloween event in Sorcerer's Dungeon; the League, achievement, loot box, trap, bulk order, materials, mining, imbuing and New Haven pages are brought up to date. The pages reach the wiki with its next update.

## 2026.10.23.2

### Fixed
- In the tutorial's death lesson, double-clicking the healer as a ghost now offers the resurrection.
- A tutorial or guild arrow left over from an earlier login no longer clears the arrow to your next lesson.

### Staff
- The nine core tutorial trials are ready for test-world validation, with action checks, hints, arrows and resets. Live tutorial entry stays disabled.
- Staff characters are no longer entered into the new player tutorial at login.

## 2026.10.23

### Staff
- The guilds' real halls stand on the test shard only: [ClusterFGuildHalls place puts each guild's sign, guildmaster and decor in the hall Chase picked (Castle British's keep for the League, the Lycaeum, Empath Abbey, the Musha-Shugyo Dojo shared with its armourers, and the rest), and OSI's Guild House for the Society of Smiths at Serpent's Hold and the Merchants' Exchange at Magincia. status, clear, go <hall> and runes (recall runes to every hall) do the rest; on the live shard it refuses. The Maritime Brotherhood's hall waits.

## 2026.10.22

### Added
- Treasures of Sorcerer's Dungeon comes to Ilshenar for Halloween, October 24 to November 2: undead fill the dungeon's rooms one after another, and every 80 slain in a room bring out its boss, from the nightmare fairy and the headless elf to Jack the Pumpkin King himself.
- While it runs, a player slaying monsters in Sorcerer's Dungeon now and then finds a piece of equipment of Enchanted Origin in their pack. The researcher by the dungeon entrance trades them, one point each, for Jack's Bag of Tricks, a Jack in the Box, the Shackled Heart of the Pumpkin King, a Serpent's Jawbone, a Sterling Silver Ring, and Boots or Talons of Escaping.

### Staff
- Treasures of Sorcerer's Dungeon starts and stops with the Halloween dates by itself. [SorcerersDungeon status reports it; on, off and remove run it by hand (off keeps the researcher so points can still be spent; remove also takes him and the entrance pumpkins away); boss 1-9 raises a room's boss at once (9 is Jack). The same verbs work on the server console as tosd. [TOSDSpawner shows the nine rooms and moves you into one; [EnchantedOriginArtifact makes a test artifact.
- A rideable red dragon, and a dragon that takes any hue, for staff to try: [add RideableRedDragon or [add RideableColorableDragon, then double-click it to ride (set Hue on the second). Players cannot tame or get one yet; their numbers are a horse's until it is decided what they are and how players earn one.

## 2026.10.21.24

### Added
- The shard can play music of its own in chosen places and fights, with the stock Ultima Online music kept everywhere else: a region, or a spot inside one, can have a track of its own, and a chosen monster or boss brings its track when it fights you, the area's music coming back a few seconds after the fight. Our tracks come with the player package (none ship yet: the first are being picked in the Music Room). Type [ShardMusic off in game to keep only the stock music, and [ShardMusic on to hear ours again.
- The Valley of Eodon in Ter Mur comes alive: a public moongate at the ring of dirt in the valley (the moongate menu has always listed Valley of Eodon; now there is a gate to arrive at), and the valley's wildlife, dinosaurs and six tribes spread across it.
- In the valley: the anchisaur and the gallusaurus (tamable) join the other dinosaurs, and desert scorpions hunt the western desert.
- The tribes of Eodon (Jukari, Kurak, Barrab, Barako, Urali and Sakkhra): warriors, shamans and chieftains who leave you alone until you attack one, then the whole tribe nearby comes for you.
- Wandering shamans walk the valley and heal like wandering healers.
- Vines on a cliff near the Barako village can be climbed to the clifftop.
- Chopping the last usable wood from a grove leaves a stump at the tree you chopped. It disappears when the wood returns; using an axe on it tells you there is not enough wood left.

### Staff
- [ShardMusicPlay <id|stop|list> plays any music ID to you (stock 0-104, ours 105 up), stops the music, or lists the shard music table and what applied at start. Settings: clusterf.music.enabled, regionLayer, monsterLayer, bossLayer, returnSeconds (8), fightRange (24).
- [GenEodon [dryrun] (Administrator) places the Eodon moongate at Ter Mur 719,1863,40, the two climbing vines and the 46 Eodon spawners; it replaces its own earlier run, never doubles a gate that is already there, and [DeleteEodon takes it all down. The server console has the same as "eodon status|dryrun|generate|delete", and every start logs whether the Eodon gate stands. Nothing runs by itself: run the dry run, then [GenEodon, on the test shard first.
- On no spawner yet, for [add: the greater phoenix, the infernus (a trail of fire under its target), the dragon turtle hatchling (tamable; a wild one carves dragon turtle scutes), Britannian infantry and the Semidar legend cards.
- Temporary lumber stumps are on by default (clusterf.lumber.stumps); clusterf.lumber.stumpItemId defaults to 0 for random stock stump art, or selects a fixed ID from 0x0E56 through 0x0E59.

## 2026.10.21.23

### Added
- Fourteen new creatures for the League's hunts: hedge snatchers, their mounted outrider, hornback raiders and ditch crawlers in the hills and gullies around New Haven; mire croakers in the swamps; brine lurkers in Covetous's flooded halls; moonmaws and thornwisps in the Yew woods; pale drifters in Deceit; slaghearts in the Fire dungeon; glasswardens in the Ice dungeon; tunnelmaws in Shame; and two mini-bosses, the Siegebreaker in Covetous's lower hall and the Hollow Oak in a Yew clearing. Each drops a monster part. The first time you kill one, the System adds it to your bestiary with a line of its own. Their art ships in the player package (update it from the launcher).

### Staff
- [ClusterFSeedBestiary [dryrun] places the new creatures' spawners on Trammel where missing (test shard first, then live).

## 2026.10.21.22

### Added
- New Haven dresses up for winter from December 1 to January 6: a lit Christmas tree, garlands, candles and presents in the bank, small lit trees at the bank's doors, on the square and along the main roads, snow here and there, and an advent calendar on the bank's wall with a small gift behind each day's door, one door a day in December.

### Staff
- [ClusterFWinterDecor status, preview, place and clear (an area may follow: bank, square, main road, inn ...) show, put up or take down the winter decor; it goes up and comes down by itself on clusterf.seasonal.winter.start and .end (12-01 and 01-06), and clear removes only what it placed, by serial. The old clusterf.winterDecor.enabled setting is no longer read; clusterf.seasonal.winter.automatic replaces it. The art ships in the player package.

## 2026.10.21.21

### Changed
- Graduating from the New Haven tutorial now completes your first loop, with its fanfare and Loop box, and opens the island to travel. Your Young hours keep running.
- Before graduation, island exits point you back to the League recruiter. Skipping provisionally opens the island too; remedial training remains available.

### Staff
- New settings: clusterf.tutorial.islandGate (default on) and clusterf.tutorial.skipCountsAsGraduation (default on; off requires remedial graduation). Staff are exempt from the island gate.

## 2026.10.21.20

### Added
- The League Register: from Dull Copper the League Registrar gives you a free League Register, a book that holds your guilds. Bind a guild into it (a guild that has a book, like the Smith Guild Book or the Dispatch Ledger, gives its book up; you can take it back out free at any time). It holds 2 guilds at Dull Copper, 4 at Bronze, 6 at Verite, 9 at Blaze, 12 at Mythril and all 18 at Celestial.
- The Register's pages: your bound guilds; a Guild Ledger with every guild's rank, standing and currency; League actions that open as your League rank rises (Bronze opens any bound guild's contracts from anywhere, Verite turns in every ready order of your bound guilds at once, Blaze repairs everything you wear, carry and bank in one pass); a Bank page that counts your bank's gold and materials; and League goods.
- League goods, paid in Renown with a little gold: a bigger backpack (150 items at Bronze, 175 at Verite, 200 at Blaze, 250 at Mythril; weight is unchanged), the League Field Satchel (Bronze: holds your ore, lumber, hunter's and essence satchels and counts as one item), and the Combined Logbook (Dull Copper: your Prospector's and Forester's logbooks in one).
- Repair items for smiths with Arms Lore: a field repair deed (repairs one item), a field repair kit (repairs one item per charge) and a remote repair deed (repairs everything you wear, carry and bank, once). Find them under Repair Items in the smithing menu.

### Changed
- If you leave a guild, your Register shows it greyed out once and then drops it, freeing its slot.

### Staff
- Every Register, League goods and repair item number is a clusterf.leagueRegister.* setting; the repair items' craft thresholds are read at server start.

## 2026.10.21.19

### Staff
- The art library's guild hall decor is in the player package (279 new art IDs, nothing replaced): for the Order of the Alembic, Hestia's bubbling vat, RedRooster's eight creature tanks and crystal ball, and Ems' flasks; for the Society of Smiths, a blacksmith at work and MrRiots' fire pit; for the Artificers, MrRiots' floating crystals and otimpyre's arc orbs; and for any hall, Hestia's five towers and Outlands' marble railings and ladder. Nothing is placed in the world.
- Four test-shard-only commands dress a hall from a template: [DecorAlembicHall, [DecorSmithsHall, [DecorArtificersHall and [DecorHallStructure, each with place (target a point, or 'here'), a group name to place one part, clear, status and dryrun. On the live shard they refuse.

## 2026.10.21.18

### Added
- The New Haven tutorial has its farm, west of town past the Bardic Guild: a fenced field of cabbages, wheat, corn, carrots and pumpkins, a pen of goats, sheep, cows and chickens, and a little thatched farmhouse whose porch opens onto the field.
- Wren Tallow, the League farmhand, waits at the farm gate. Trainees pick a dog or a cat from her to keep: it grows as it fights. Then they clear the pests out of the crop rows with it and patch themselves or their pet up with her bandages.
- If you took your dog or cat at the farm, graduation no longer offers you a second pet.
- The client's map update clears the two trees that stood inside the farm's fence.

### Staff
- [ClusterFSeedTutorialFarm [dryrun | force] places the farm (farmhouse and field, the door, the Farmhand, the pest and pen spawners) where missing. It is test shard only (clusterf.tutorialFarm.allowLive turns it on elsewhere), and refuses while the server's map still has the two trees.

## 2026.10.21.17

### Added
- Deployable traps: tinkers can craft eight trap kits (snare net, caltrops, gas, blast, dart, bear trap, alarm and shackle), and Remove Trap now sets them on the floor as well as disarming them, gaining skill both ways.
- Traps only ever catch monsters, never players or pets. A trap works only while you stay within 12 tiles of it; the alarm reaches you anywhere on the same map.
- A trap fires a few times and then sits spent until you re-set it by hand. Drop a poison potion on a gas trap, an explosion potion on a blast trap, or fukiya darts on a dart trap to load it. Your Poisoning skill decides how strong the gas is.
- Damage from your traps counts as yours, so you get looting rights and loot box credit for what they help kill.
- The Thieves' Den: a Journeyman can keep one more trap out, and a Master's traps work on bosses.
- In the Rootdeep Burrows, the Echo hands trainees two practice nets in the Stacks: catch a rat with one to learn the trick. The Burrows floors also have a few traps of their own now. Watch your step.

### Staff
- New settings under clusterf.traps.*: pvp (off; when on, Felucca traps can hit players and flag the owner), presenceRange, decay times, area cap and every trap's numbers. [ClusterFSeedRootdeep places the eight Burrows floor traps on its next run.

## 2026.10.21.16

### Staff
- The web console has a Music Room (the Music Room link, or /music): every candidate music track on disk (the SOS Mix Vol.2 and SOS Mix + DLC packs, the Paranoia remixes and Turfin, and the stock client music to compare with), 353 in all, with its length, the stock track it replaces or remixes, and the album its file tag names ("not tagged Ultima" marks the ones from other games). Play any track with a seek bar, rate it Use, Maybe or Pass, tag where it fits (town, dungeon, boss and more), name a target area, monster or boss, and write a note. Next unrated works through the list like a playlist; autoplay is off unless you tick it.
- Notes save as you type to docs/music/music-notes.json in the docs repo. Save to repo commits that one file (it never pushes; the next commit's push carries it), and the console also commits it once an hour when it changed. The Summary view lists the tracks marked Use by tag and target, for adding music for areas, monsters and bosses later.
- Companions, first part: the companion itself and its record (lineage id that is never reused, path, branch, personality, loop, rank, level, epithet, parents and generation for breeding later). Its skill caps are a player's at its own loop, its hits, stamina and mana use the player formulas, and it logs out and back in with its owner instead of being stabled. Slot cost by rank, party size and the follower curve are clusterf.companions.* settings. No hiring yet: [CompanionMake <path> [personality] [male|female] [loop] [rank] makes a test companion for you, and [CompanionSet <loop|rank|level|branch> <value> changes one.

## 2026.10.21.15

### Added
- Opening a loot box now sends the best thing inside flying out of it: a sword, a bow, a staff, a piece of armor, a shield, a ring, a gem, a potion or a bit of clothing arcs up out of your pack and lands at your feet, just before the fanfare. Boxes of tier 4 and up show it to everyone around you; smaller ones to you alone. Muting your fanfare ([FanfareMute) hides it too. Update your game through the launcher to see it.

### Staff
- The Golems' emails are in plain English now: what happened comes first, then a "What to do" line ("Nothing to do, it keeps going" or the one thing to do), and the technical details at the bottom. Subjects start with "Needs you:" only when you have to act.
- A finished prompt's email says in plain words what it changed, from a short "In plain words" section every prompt's notes now start with; the morning night-shift summary gets one such line per prompt.
- Routine Golem emails (a prompt starting, a session opened by itself, a merge into main) no longer come on their own: they ride along in the next email. One finished prompt is one email.
- The overnight test bot's emails and the Shard Console's question email have the same shape.
- The animations are Clint Bellanger's "Flying Loot Animations" (CC-BY 3.0, opengameart.org; base icon styles by Blarumyrran), reworked for UO: art 15536-15589 in the player package. Their credit belongs in the shard credits list. The setting clusterf.lootBoxes.flyingLoot (on by default) turns the effect off for everyone; nothing about what a box holds changes either way.

## 2026.10.21.14

### Added
- The League Shop has a Hall page: decor for your house, opened by your Achievement Points and bought with Renown. Display cases of books, gems or plain glass (the shelf and its glass dye separately), dragon and gargoyle statues, and mounted trophies and heads: minotaur, sabertooth, troglodyte, skull, titan, oni, ophidian and terathan matriarchs, and Paroxysmus. Press Hall in the League Shop to see them; locked pieces show the AP they need, and every purchase asks you to confirm.
- Each piece comes as a deed for a house you own: use it, choose the facing (or the wall, for a trophy), and target the spot. Chop a placed piece to get its deed back. The deeds are bound to your account. Update your game through the launcher to see the new art.
- The System has been taking notes. A new set of secret achievements notices the shard's favorite shortcuts and odd habits, in fights, on the road, at the forge, at home and around loot boxes. You will find out what they are when you do them.
- Some of them come with a Dented Box: a small loot box with one guaranteed, entirely useless keepsake inside, bound to you. Three of them come with a title.

### Changed
- Bulk orders and smith commissions take only items a player crafted. Items bought from a shopkeeper (or found as loot) no longer fill them.
- After you abandon a smith commission, the Guildmaster waits 5 minutes before offering you another.

### Staff
- Every Hall price and AP gate is a setting (clusterf.leagueHall.<piece>.ap and .renown), to be tuned in play testing. Renown spent here never lowers League rank. The art is RedRooster's display cases, Hestia's statues and trophies and Archaaz's trophies and heads (art 15592-15629); all three belong in the shard credits list.
- The "Noted by the System" achievements are 45 more in the Mischief category, listed by [ClusterFAchievement list. 40 can be earned now; Middle Management, Deja Vu, Procrastinator, Cold Feet and Infamous wait for companions, loops and commission expiry and cannot be earned yet. Each fires from the game event (a kill, a gate or recall, a craft, a recycle, a mining swing, a step, a death at home, a loot box) and is earned once per account.
- Their rewards (which give a Dented Box and its keepsake, and which give the titles Larry, the Supervisor and the Brave (citation needed)) are one table, NotedRewards in ClusterFNotedBySystem.cs. The titles appear in the title picker only once held.
- clusterf.bods.craftedOnly (default true) turns the crafted-only rule for orders off; clusterf.commissions.abandonCooldownMinutes (default 5, 0 for none) sets the abandon wait.

## 2026.10.21.13

### Added
- The achievements window now groups each list by category under a heading with the category's own icon: crossed swords for Combat, an open book for Skills, a compass for Exploration, a pick for Mining, an anvil for Crafting, a scroll for Legacy, a magnifying glass for Discovery, a sack for Collection, a laurel crown for the League, and a grinning imp for the secret joke achievements once you have earned one. Update your game through the launcher to see the icons; without the update the headings show the category names alone.

### Staff
- The icons are gumps 22459-22468 in the player package, from Praxiiz's conversion of the game-icons.net set (CC BY 3.0). The license requires the credit "Icons made by Lorc" (game-icons.net) and a credit to Praxiiz for the conversion in the shard's credits list.

## 2026.10.21.12

### Staff
- Levelable crafted gear (first part). A Smith's Seal makes a crafted weapon, armor piece, shield or jewel levelable; found gear and artifacts are refused. Seals come only from staff for now ([add SmithsSealDeed) until their source is chosen. A levelable item gains XP from every kill its wearer helped with, shared by damage, with pets, summons and spells counting as the owner's; it levels to 200 and earns points (4 a level, 8 every 10th) to spend through [ItemLevel on properties and resists, never past the property's imbuing ceiling. Creatures can wear it too. Every number is a clusterf.levelGear.* setting; [LevelGearSet sets an item's level for testing.

## 2026.10.21.11

### Added
- The world map now has a fog of war: it starts dark and clears where you have walked, and what you have explored is kept across logins. Choose "Fog of war" in the world map's right-click menu to turn the fog off. Update your game through the launcher to see it.
- The Exodus Encounter (Ilshenar): Lord Dupre's champion, knight and squires battle Exodus's zealots, drones, sentinels, a juggernaut and a minion lord through the Exodus Dungeon, hidden supply caches wait for a keen Detect Hidden, and broken Nexus machines yield punch cards to anyone with an Exotic Toolkit and a steady hand. Win the four keys of the ritual (the Summoning Rite, the Sacrificial Dagger, the Robe of Rite and the Summoning Altar) by fighting, stealing from zealots, picking the caches or crafting them, build the Summoning Tome at a shrine with your party, and be carried to Ver Lor Reg to face Clockwork Exodus for his gold and ten artifact replicas.
- Hunter the Arch Zealot stands near Ver Lor Reg to explain the ritual and trade a gargoyle's dagger.
- Blackrock: a small piece of blackrock turns up one dig in a thousand at 100 Mining.

### Staff
- The fog of war is drawn by our TazUO build (client patch 0004), so players see it only once a player package built with that client is published; until then the server's exploration data is sent but not shown.
- [GenExodusEncounter places the encounter on Ilshenar (the controller and Clockwork Exodus, the exit moongate and ankh, the 39 Nexus machines and the Keypunch Reader, the spawners and the Arch Zealot) and [DeleteExodusEncounter removes it; both are test-shard commands until Chase runs them live.

## 2026.10.21.10

### Added
- Accessory slots: type [accessories to wear more jewelry than your paperdoll holds. Extra rings and a second bracelet count exactly like the ones you wear, with every usual cap. The second bracelet slot opens at League rank Valorite Citizen; the extra ring slots will open with loops.
- Toe rings, made by tinkers. They only work with bare feet (or in Plate Socks, which are technically shoes). Your toe rings will not forgive you for wearing boots over them.
- A few new things for the System to notice about your feet.

### Staff
- Toe ring slots open from two hidden barefoot achievements. Plate Socks, the Singed Toe Ring and the Mood Toe Ring come only from [add for now. Every slot threshold is a clusterf.accessories.* setting; the extra ring slots stay locked (clusterf.accessories.loopSlotsEnabled) until loops exist.
- Two draft wiki pages for the Mage trainer (SL_Mage) and the Crafter trainer (SL_Crafter) Legion scripts wait in the wiki seed's drafts section until the scripts are published; the Test Crystal draft page now describes the skill gain crystal as well as the staff testing crystal.

## 2026.10.21.9

### Added
- Every creature and NPC now shows how tough it is next to you. Click it (or show all names) and a colored word appears under its name: grey (trivial), green (easy), blue (fair), yellow (even match), orange (tough), red (dangerous) or a dark red (deadly). Until character levels arrive, your level is your best combat skill.
- Item Identification is now Appraisal, and it works on creatures too. Anyone can appraise a creature and get a feel for it; from 30 you also learn its level, from 60 its hits, stats and resistances, and from 90 a stat card with its damage, special abilities and whether it is a paragon or a boss. On items it works as it always did, and it now trains past 100 on creatures near your skill.

### Changed
- The skill window calls Item Identification "Appraisal". Update your game through the launcher to see the new name.

### Staff
- [CreatureLevels writes every creature type's Bard difficulty and level to creature-levels.txt in the server's temp folder. Levels are computed from OSI's Bard difficulty (1 to 200); champions are 200, peerless bosses 190, invulnerable NPCs 1, and ClusterFCreatureLevels.TypeOverrides holds hand levels. Settings: clusterf.creatureLevels.conLine, clusterf.creatureLevels.provisionalPlayerLevel and clusterf.creatureLevels.fixedPlayerLevel.

## 2026.10.21.8

### Staff
- Every Golem has its own test-shard accounts, "<golem>-admin" and "<golem>-player", each with a starter character logged out in Green Acres. A test shard makes them at start; on a running test world [SeedGolemAccounts (with dryrun) makes any that are missing and touches no other account. It refuses on the live shard.

## 2026.10.21.7

### Changed
- Ore, lumber and hunter's satchels, the Artificers' Essence Satchel, the Gemologist's Satchel and the Smith Guild Salvage Bag have new icons of their own. Satchels you already own change over by themselves; update your game through the launcher to see the new look.
- The Artificers' Essence Satchel is purple now, so it no longer looks black.

### Staff
- The Codex lanes can run on the second PC, one per ChatGPT login with its own budget: at night or when sent with the new Send to the second PC button (or by email), never while someone is using that PC or a game is running (a run already going stops at a safe point within a couple of minutes). The game PC wakes that PC for its window, shows each lane's step on the Golem page, and runs a lane's tests again before it merges the work. A lane never takes a task that needs the test shard, the overnight bot or a Haven deploy.
- The icons are art IDs 0x3380-0x3385 (player package). A satchel or bag a staff member set to another graphic or hue keeps it; a player without the package update sees the slot's "UNUSED" placeholder until they update.

## 2026.10.21.6

### Added
- New Haven has new decorations made by the community: fruit and vegetable carts at the market and in the west meadow, oil lanterns on the docks and along the main road, marble railings at the League field office and in the Healer's Hall, support beams under the boardwalk at the docks, and paintings in the inn, the field office, the Healer's Hall, the Magery School, the Bardic Guild and A Stitch In Time.
- An orc camp has sprung up in the meadow on the road from New Haven to the Rootdeep way in.
- Thanks to the artists: Moonrose (farm carts), Ems (oil lantern), OUTLANDSOwyn (marble railings), gollin (support beams), Vrark (orc camp) and Odin (paintings).

### Staff
- [ClusterFCommunityArt status, place, replace and clear (each with dryrun; Administrator) put up and take down New Haven's community art. It places nothing twice and removes only its own pieces. Nothing runs by itself: run 'place' on the test shard first. The art needs the player package update; without it players see "UNUSED" tiles.

## 2026.10.21.5

### Added
- Sixteen secret achievements for some of Britannia's sillier moments. They stay hidden until you stumble into one, and each comes with a few words from the System. We won't say what they are, but your parents probably warned you about at least one of them.
- Climbing boots from the Miners' Compact Liaison: Surveyor's (hop 4 tiles), Delver's (hop 8, half the stamina) and Deepwarden's (hop 12, and they remember your last ledge so one hop takes you back up from within 30 tiles). Wear them, use them, and target a mountain tile to hop onto it, then mine the ore no one could reach before. Each hop costs stamina, more the higher you climb, with a short pause between hops.
- While you stand on a climbed mountain tile you can only mine, prospect and hop: no fighting, harmful spells, taming, Mark or house placement, though monsters' spells can still reach you. Your pets wait at the foot of the climb and fall in behind you again when you hop down.
- The boots need Compact rank, Mining skill and blocks mined (Surveyor, 120, 100 blocks; Master Delver, 150, 300; Deepwarden, 180, 600), plus vouchers, gold and ingots; each upgrade trades in the boots before it. They are blessed, bound to your account, and the Liaison replaces lost ones.
- Your Prospector's Logbook now counts the ore blocks you have mined, and remembers when you first mined each one and what ore it gave.

### Staff
- The secret achievements are listed by [ClusterFAchievement list; they sit in their own Mischief category, shown under All once earned. Each fires from the game event itself (never from message text) and is earned once per account, so repeating the event earns nothing past its tiers.
- Every climbing number (hop ranges, gates, prices, stamina, cooldown, ledge range) is a clusterf.climbing.* setting. Climbing boots also open the Fountain of Youth's climbing-only spots.

## 2026.10.21.4

### Added
- Monster surges: from time to time staff may call a surge, waves of creatures descending on one place. A shard-wide message says where, and moongates in Britain and New Haven may open to take you there. Prizes go to those who did the most damage, and can also go to everyone who fought or stood in the area for at least half the surge. Some surges are no-loot zones: fall inside one and you keep everything you carry. When the surge ends, its creatures and moongates are gone.

### Staff
- The Staff Hub's Events tab is open ([SL, GameMaster and up, live and test). Pick a creature template (Orc raid, Undead rising and Ogre warband to start; New makes your own, with an optional named boss for the last wave), set the spot, radius, waves, count per wave and interval, then Start or Dry run. Options per surge: moongates from Britain and New Haven to a staging spot, a no-loot zone, and prizes in any mix (top damage dealers, everyone present or who did damage, and extras you hand out by target). You name each prize item; gold is the default. End removes everything the surge made; a restart mid-surge removes it at start-up with no prizes.

## 2026.10.21.3

### Added
- The League Shop: the League Registrar in New Haven ("League Shop") and the achievements window both open it. Achievement Points open each item for good and Renown pays for it, so spending never lowers your League rank. Items your AP has not reached yet show greyed with the AP they need, and every purchase asks you to confirm.
- On sale: a League tabard, cloak and regalia (robe, cloak and sandals) in League colours sold nowhere else, a League banner, an achievement trophy engraved with your name and AP, a one-use League pigment, a hair restyling deed, a blessed recall rune marked to the Registrar, and the System's Loot Boxes (Bronze, Silver and Gold) holding League goods.
- League titles: buy "the Registered Adventurer", "the Veteran of the League" or "the System's Favorite" in the shop, or earn one free at 100, 250, 500, 1,000 and 1,750 AP. Choose the one shown after your name under Titles, at the Registrar or in the achievements window. A title you have earned is yours for good.

### Staff
- League titles are kept on the account (each character picks its own to show) and saved with the account data; a title is put back on the character at login. Shop prices and AP gates are F-13's starter list, to be tuned in play testing.

## 2026.10.21.2

### Staff
- A proposed shared look for every guild menu (a crest and colour per guild, a section list, one requirement style, paging) is drawn as mockups for approval. No menu uses it yet; players see no change.
- Codex (on the ChatGPT plan, beside the Claude plan) now reviews every prompt before the Golem commits it: the review lands in the notes, high findings go in the done email and on the overseer's list, and it never blocks the commit. A Codex lane can also take a prompt the overseer marks Codex OK, at night by default, in its own work copies, merged only when it merges cleanly; it waits for its own Codex sign-in before it starts.

## 2026.10.21

### Changed
- Exceptional crafted items now get their material's bonuses one and a half times over, for every ore, leather, scale and wood: an exceptional Valorite plate chest has more physical, cold, poison and energy resist than a normal one, an exceptional Golden sword more luck and lower requirements, an exceptional Mythril weapon more energy damage, an exceptional Oak bow more luck and damage. Normal items keep the values they had. Your resists still stop at the usual caps.
- Material penalties (the -3 resist on dragon scales, Frost ore's -2 fire resist) are not made worse on exceptional items. Runic properties, the exceptional damage and resist bonus, and the extended woods' own exceptional line are not multiplied.

### Staff
- Items already in the world change at once for resists, luck, lower requirements, element damage and durability bonus (it is read live). Their maximum hit points and an exceptional Oak to Frostwood bow's wood bonuses were set when crafted and stay as they were; new crafts get the full rule. Cloth has no material bonuses to multiply.

## 2026.10.20.23

### Added
- Blackthorn Dungeon (revamped), the dungeon beneath Lord Blackthorn's castle, on Trammel and Felucca: the ruined city rooms are populated (brigands, skeletons, liches, dragons and drakes, golems and their controllers, hell hounds, evil mages, giant spiders, frost oozes, golden and earth elementals, rotting corpses, giant rats, vampire bats, Exodus minions and a balron), with a hitching post at the entrance to stable your pet, the invasion rooms' doors, and the Felucca castle's teleporters in and out (Trammel's castle already has its own).
- Hunting in Blackthorn Dungeon can earn an artifact bearing the crest of Minax: every kill builds a hidden tally, and the longer you hunt without one, the better the odds of the next kill handing you one (a magic weapon, armor piece, shield, jewel or hat, hued the Minax purple).
- The Agent of the Crown, in the Trammel castle and in nine Felucca cities, takes your Minax artifacts (Turn In Minax Artifacts) for Blackthorn points, one each, and sells 132 rewards for them (Claim Blackthorn Artifacts): a Decorative Shard Shield deed and two paintings for 10, and 129 pieces of clothing and armor bearing the crest of Blackthorn for 25 to 100 (hats, helms, circlets, earrings and glasses; robes, dresses, kimonos and epaulettes; belts, obis and half aprons; tunics, a doublet and a surcoat; chest armor), each with the properties the matching artifact of old carried. He also resurrects, locks karma and takes tithes.
- Castle Blackthorn in Trammel: no spells or abilities cast there; mounts are refused at the door (you are set down and cannot remount for half a minute) and pets are sent to the stables, or outside the castle when the stables are full.
- In the dungeon, a hidden player is revealed, and one who stands still for two minutes south of the invasion rooms is thrown into a noxious gas cloud elsewhere in the dungeon (lethal poison, and the nearby creatures come for you). Recall, gate and mark do not work inside; teleport does.
- Minax's invaders and invader captains, the human and elven soldiers of the city invasions (thirteen fighting styles, each with its gear; a captain's death hands the crest of Minax to the two who hurt him most), are on the shard for staff to place; the invasions themselves follow in a later update.

### Staff
- `[GenBlackthorn` places all of it on Trammel and Felucca (hitching posts, doors, blockers, the Felucca teleporters, 44 dungeon spawners and the 10 Agent spawners), deleting an earlier run first; `[DeleteBlackthorn` removes it. Nothing is placed at boot: rebuild the test shard and run it once there. The two regions register themselves at start.
- `[MinaxArtifact [budget]` puts a Minax artifact in your pack to test the Agent's turn-in (the kill roll is well under one percent).

## 2026.10.20.22

### Added
- Bows, staves, clubs and wooden shields crafted from Ironwood, Ghostwood, Emberbark, Frostbark, Shadowbark, Runewood, Voidwood or Starwood now carry that wood's bonuses, climbing with each wood: for weapons, Ironwood is sturdy, Ghostwood hits more often, Emberbark burns, Frostbark chills, Shadowbark swings faster, Runewood deals energy and restores mana, Voidwood drains life and Starwood brings luck. Wooden shields get a defensive line of their own (resists, defense chance, reflect, spell channeling on Runewood).
- An exceptional piece gets a second line on top: extra damage and a hit effect on weapons (Starwood also rolls one random Heartwood-style extra), extra resists or defense on shields. A normal piece gets the first line only.

### Staff
- The extended woods' "lower level requirement" (Ironwood 2, Starwood 5) is recorded but does nothing until levels exist. No wood ever gives Self Repair. Oak to Frostwood bows and Rootwood items are unchanged.

## 2026.10.20.21

### Added
- Halloween decorations at the banks: pumpkins, carved pumpkins, lanterns, candles, cobwebs, bones and the odd cauldron or scarecrow around the main banks of New Haven, Britain, Moonglow, Trinsic, Vesper, Minoc, Yew, Skara Brae and Jhelom (Trammel and Felucca), Luna and Umbra, Zento and the Royal City, up from October 24 to November 2.

### Staff
- [ClusterFHalloweenDecor status, preview, place and clear (a bank key or facet name limits the last three; clear dryrun lists) show, put up or take down the Halloween decor. It goes up and comes down by itself on clusterf.seasonal.halloween.start and .end (MM-DD, server-local dates, defaults 10-24 and 11-02); clusterf.seasonal.halloween.automatic false stops the automatic check. Clear removes only what it placed, tracked by serial across restarts.

## 2026.10.20.20

### Changed
- Upgrade, restore and rank-up menus (the Hammer of Hephaestus, Jacob's Pickaxe upgrades, the Miners' Compact liaison's restoration and satchel upgrade, the Civic Mule Exchange, the League's rank page) show every requirement the same way: green with a check when met, red with an X and how much more you need when not, and where your materials are in gray. A line at the bottom says "All requirements met" or how many are not.
- The Upgrade, Restore and Promote buttons in those menus are greyed out until every requirement is met.
- The Civic Mule Exchange shows one mule a page, with Next and Previous to move between them.

### Fixed
- Those menus are no longer see-through: the world and other windows behind them no longer show through the text.
- Section headings in those menus are no longer green, so green only ever means a requirement is met.

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
- The Tinker, a watchdog on the game PC, keeps every PC's Golem kit in step with the game PC's and every 10 minutes fixes the stalls it knows (a Golem's own unfinished work, a lost hand-off, a low-memory stop, a failing watch, a stale flag); anything else becomes one plain question on its card, after a short read-only reasoning session when needed.

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
- The Test Center no longer boosts everyone's skill gain: the test shard now trains at the real shard's rates. Each tester gets a Test Crystal in their bank instead ([TCFill puts one there); double-click it to set your own skill gain chance to Off, 2x, 5x, 10x or 25x. It affects only its owner, is saved with the character, and only works on a Test Center shard.
- [GiveTestCrystal puts one in a targeted player's bank. testCenter.skillGainMultiplier is retired and does nothing; remove it from the test shard's modernuo.json.
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
