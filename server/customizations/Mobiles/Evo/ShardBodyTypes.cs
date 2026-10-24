// cc-P65, F-35 Part D: body types for the unused client bodies our creatures use; cc-P139 adds the first body that is
// ours alone (a third-party .vd shipped in the player package, F-46).
//
// Pinned types each body from Distribution/Data/bodyTable.cfg (Server/Mobiles/Body.cs:40-73; an unlisted body is
// Empty). A pet on a body typed Equipment or Empty can open doors (BaseCreature.CanOpenDoors => !Body.IsAnimal &&
// !Body.IsSea, BaseCreature.cs:1053), gets no idle, feed or anger animation, and gets no swing or hurt animation at all
// (BaseWeapon returns for a body it cannot type). Our evo pet bodies are such bodies (cc-P59 section 6).
//
// The route: Distribution/Data stays pinned (the shard's rule: ClusterFMineCampLayout.cs:6, AthenaeumIsle.cs:89-91), so
// the types are set here, in code, at Configure, into Body's own table. Body keeps that table private and read-only
// from outside (Body.cs:38), so this writes the array through reflection; if pinned renames or reshapes it, this
// throws at start and the server does not come up quietly wrong. Argued in the notes, section 4.

using System;
using System.Reflection;

namespace Server
{
    public static class ShardBodyTypes
    {
        /// <summary>
        /// Body, type. Each was unused by any pinned or shard creature when added (notes section 4 lists every check).
        /// </summary>
        public static readonly (int Body, BodyType Type, string What)[] Entries =
        {
            // cc-P65, evo pets (F-35). bodyTable.cfg had: 739 Monster, 832 Monster, 1416 Equipment, 1546 Equipment,
            // 1548 Equipment, 1549 Equipment, 1254 Animal (kept, listed for completeness), the rest missing.
            (739, BodyType.Animal, "leather wolf, evo dog stage 2"),
            (832, BodyType.Animal, "phoenix bird, evo bird stage 2"),
            (1309, BodyType.Animal, "baby tiger, evo cat stage 2"),
            (1254, BodyType.Animal, "tiger, evo cat stage 3 pick"),
            (1426, BodyType.Animal, "lion, evo cat stage 3 pick"),
            (1416, BodyType.Animal, "sabertooth tiger, evo cat stage 4 (also the sabertooth costume's body)"),
            (1573, BodyType.Animal, "manticore (large), evo cat stage 5"),
            (1423, BodyType.Animal, "small fox, evo fox stage 1"),
            (1546, BodyType.Animal, "Newfoundland, evo dog stage 3 pick"),
            (1547, BodyType.Animal, "Alaskan malamute, evo dog stage 3 pick"),
            (1548, BodyType.Animal, "Great Dane, evo dog stage 3 pick"),
            (1549, BodyType.Animal, "Saint Bernard, evo dog stage 3 pick"),
            (1551, BodyType.Animal, "Black Russian terrier, evo dog stage 3 pick"),
            (1552, BodyType.Animal, "Rottweiler, evo dog stage 3 pick"),
            (1410, BodyType.Animal, "windrunner, evo dog stage 5 (bodyTable.cfg: Equipment)"),
            // cc-P139, the creature body route (F-46): the first third-party creature body the package ships, past
            // every body bodyTable.cfg lists (its highest is 1973). Vert-I-Go's kobold archer .vd at anim7 slot 1;
            // BurrowScampSlingerBodyTest wears it, and F-45's Burrow Scamp Slinger (cc-P140) will.
            (2048, BodyType.Monster, "burrow scamp slinger (the kobold archer), the body route's proof"),
            // cc-P140, the Rootdeep Burrows lineup (F-45): registry.csv kind body, records.json bodyAnims. Animal for the
            // bodies in anim7's animal slots (the hog, the two Vanguard kobolds) and the crow (the stock bird's type).
            (2049, BodyType.Monster, "gourdling"),
            (2050, BodyType.Monster, "cackler scrapper"),
            (2051, BodyType.Monster, "cackler slasher"),
            (2052, BodyType.Monster, "cackler veteran"),
            (2053, BodyType.Monster, "cackler packleader"),
            (2054, BodyType.Animal, "burrow hog"),
            (2055, BodyType.Monster, "ledger bat"),
            (2056, BodyType.Monster, "hoarding rat"),
            (2057, BodyType.Monster, "the Overdraft"),
            (2058, BodyType.Monster, "burrow grub"),
            (2059, BodyType.Animal, "burrow scamp"),
            (2060, BodyType.Animal, "redcap scamp"),
            (2061, BodyType.Monster, "the Burrow Mother"),
            (2062, BodyType.Monster, "sporecap (red)"),
            (2063, BodyType.Monster, "sporecap (blue)"),
            (2064, BodyType.Monster, "bramblehulk"),
            (2065, BodyType.Monster, "dawnshard"),
            (2066, BodyType.Monster, "seamguard"),
            (2067, BodyType.Monster, "gloomling"),
            (2068, BodyType.Monster, "gloomspinner"),
            (2069, BodyType.Monster, "gloomweaver"),
            (2070, BodyType.Monster, "the Gloom Queen"),
            (2071, BodyType.Animal, "crop crow (the stock bird at 1.5x)"),
            // cc-P364, the League hunt bestiary, batch 2 (F-24's targets): registry.csv kind body, records.json bodyAnims.
            // Animal for the bodies in anim7's animal slots (the .vd files with the low layout: the goblins, the
            // werewolf and the lava golem); the rest Monster.
            (2072, BodyType.Animal, "hedge snatcher (goblin troop)"),
            (2073, BodyType.Animal, "snatcher outrider (goblin rider)"),
            (2074, BodyType.Monster, "hornback raider (goatman)"),
            (2075, BodyType.Monster, "ditch crawler"),
            (2076, BodyType.Monster, "mire croaker (bullywog)"),
            (2077, BodyType.Monster, "brine lurker (kuo-toa)"),
            (2078, BodyType.Animal, "moonmaw (werewolf)"),
            (2079, BodyType.Monster, "thornwisp (forest spirit)"),
            (2080, BodyType.Monster, "pale drifter (shadow)"),
            (2081, BodyType.Animal, "slagheart (lava golem)"),
            (2082, BodyType.Monster, "glasswarden (crystal golem)"),
            (2083, BodyType.Monster, "tunnelmaw (umber hulk)"),
            (2084, BodyType.Monster, "the Siegebreaker (war troll)"),
            (2085, BodyType.Monster, "the Hollow Oak (treant)"),
            (2086, BodyType.Animal, "red riding dragon (cc-P277: TheArt L-Ridable Red Dragon, a mount)"),
            (2087, BodyType.Animal, "riding dragon (cc-P277: TheArt L-Colorable Rideable Dragon, a mount)"),
            (2088, BodyType.Animal, "marsh raptor (cc-P365: reward mount: a green striped raptor)"),
            (2089, BodyType.Animal, "dusk raptor (cc-P365: reward mount: a dark violet raptor)"),
            (2090, BodyType.Animal, "sky raptor (cc-P365: reward mount: a blue raptor)"),
            (2091, BodyType.Animal, "chestnut draft horse (cc-P365: reward mount on the ground; ridden as 2104)"),
            (2092, BodyType.Animal, "bay draft horse (cc-P365: reward mount on the ground; ridden as 2105)"),
            (2093, BodyType.Animal, "grey draft horse (cc-P365: reward mount on the ground; ridden as 2106)"),
            (2094, BodyType.Animal, "palomino draft horse (cc-P365: reward mount on the ground; ridden as 2108)"),
            (2095, BodyType.Animal, "barded warhorse (cc-P365: reward mount: a dark horse in barding)"),
            (2096, BodyType.Animal, "gilded charger (cc-P365: reward mount: an armoured horse, burning hooves)"),
            (2097, BodyType.Animal, "moonlit unicorn (cc-P365: reward mount: a white unicorn)"),
            (2098, BodyType.Animal, "bonewalker steed (cc-P365: reward mount: a skeletal horse)"),
            (2099, BodyType.Animal, "emberhoof (cc-P365: reward mount: a black horse, burning hooves)"),
            (2100, BodyType.Animal, "bog strider (cc-P365: reward mount: an armoured swamp lizard)"),
            (2102, BodyType.Animal, "war wolf (cc-P365: reward mount: an armoured great wolf)"),
            (2104, BodyType.Animal, "chestnut draft horse, saddled (cc-P365: ridden look of 2091 (walk, run, stand, eat))"),
            (2105, BodyType.Animal, "bay draft horse, saddled (cc-P365: ridden look of 2092)"),
            (2106, BodyType.Animal, "grey draft horse, saddled (cc-P365: ridden look of 2093)"),
            (2108, BodyType.Animal, "palomino draft horse, saddled (cc-P365: ridden look of 2094)"),
            (2109, BodyType.Human, "riding drake (cc-P365: ridden look of the riding drake (people layout: the rider's mounted actions 23-29))"),
            (2110, BodyType.Human, "riding bear (cc-P365: ridden look of the riding bear (people layout))"),
            (2111, BodyType.Human, "riding wolf (cc-P365: ridden look of the riding wolf (people layout; prapilk_reitwolf))"),
            (2112, BodyType.Human, "saddled horse (cc-P365: ridden look of the saddled horse (people layout))"),
            (2113, BodyType.Animal, "dun foal (cc-P365: evo young stage: a foal (prapilk_colts L_01))"),
            (2114, BodyType.Animal, "grey foal (cc-P365: evo young stage: a foal (prapilk_colts L_02))"),
            (2115, BodyType.Animal, "brown foal (cc-P365: evo young stage: a foal (prapilk_colts L_03))"),
            (2118, BodyType.Animal, "white foal (cc-P365: evo young stage: a foal (prapilk_colts L_04))"),
            (2119, BodyType.Animal, "young lizard (cc-P365: evo young stage: a young lizard (prapilk_echsen L_01))"),
            (2125, BodyType.Animal, "young lizard (cc-P365: evo young stage: a young lizard (prapilk_echsen L_02))"),
            (2126, BodyType.Animal, "young lizard (cc-P365: evo young stage: a young lizard (prapilk_echsen L_03))"),
            (2129, BodyType.Animal, "brown calf (cc-P365: evo young stage: a calf (prapilk_rindviehcher L_01))"),
            (2130, BodyType.Animal, "spotted calf (cc-P365: evo young stage: a calf (prapilk_rindviehcher L_02))"),
            (2131, BodyType.Animal, "red calf (cc-P365: evo young stage: a calf (prapilk_rindviehcher L_03))"),
            (2132, BodyType.Animal, "piebald calf (cc-P365: evo young stage: a calf (prapilk_rindviehcher L_04))"),
            (2133, BodyType.Animal, "great grey wolf (cc-P365: the evo dog's top stage (kr-wolf; actions 0-10 and 12))")
        };

        public static void Configure() => Apply();

        public static void Apply()
        {
            // Touch Body first so its static constructor has read bodyTable.cfg before we write over it.
            _ = new Body(0).Type;

            var field = typeof(Body).GetField("m_Types", BindingFlags.NonPublic | BindingFlags.Static);

            if (field?.GetValue(null) is not BodyType[] types)
            {
                throw new InvalidOperationException(
                    "ShardBodyTypes: pinned Body has no static BodyType[] m_Types (Server/Mobiles/Body.cs:38). " +
                    "The body type table changed shape; fix ShardBodyTypes before starting the shard."
                );
            }

            // Body leaves the table empty when Data/bodyTable.cfg is missing (Body.cs:42-46); then nothing is typed at all.
            if (types.Length < 0x1000)
            {
                throw new InvalidOperationException(
                    "ShardBodyTypes: Body's type table is empty, so Data/bodyTable.cfg was not found from the current directory."
                );
            }

            foreach (var (body, type, _) in Entries)
            {
                types[body] = type;
            }
        }
    }
}
