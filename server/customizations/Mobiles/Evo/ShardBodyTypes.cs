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
            (2048, BodyType.Monster, "burrow scamp slinger (the kobold archer), the body route's proof")
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
