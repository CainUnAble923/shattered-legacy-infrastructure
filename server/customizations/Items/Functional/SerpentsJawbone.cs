// cc-P199: ServUO pub57 Scripts/Items/Functional/SerpentsJawbone.cs. The Serpent's Jawbone (art 0x9F74), a house
// teleporter to Ilshenar: locked down or secured in a house, a double-click from within 2 tiles opens a list of 17
// Ilshenar places (Lakeshire, the Rat Fort, Reg Volom, Twin Oaks Tavern, the Oasis, Blood, Cyclops and Exodus dungeons,
// the Kirin Passage, the Pass of Karnaugh, the Rat Cave, Terort Skitas, the Twisted Weald, Wisp Dungeon, Gwenno's
// Memorial, the Desert Gypsy Camp, Rock Dungeon). A button moves the player and their pets there, hidden and out of
// war mode, unless they are too far, a murderer, carrying a faction sigil, criminal, in combat, casting, or already
// there. A secure-level context entry sets who may use it. ServUO sells or awards it elsewhere (see the report).
//
// Differences from ServUO:
//   - ServUO's gump is a classic Gump with its own User property and AddGumpLayout; here it is pinned's DynamicGump
//     (the CircuitTrap.cs pattern). Layout values are ServUO's. ColUtility.For (a ServUO helper pinned lacks) is the
//     plain indexed loop over the same dictionary, in the same order.
//   - Locations is built by a static initializer, where ServUO fills it in Initialize() (SerpentsJawbone.cs:15-36):
//     same 17 entries in the same insertion order, so the buttons come out in ServUO's order.
//   - SpellHelper.RestrictRedTravel (ServUO SpellHelper.cs:793, Config "General.RestrictRedsToFel", shipped True in
//     ServUO's Config/General.cfg:20) does not exist in pinned; the check is ServUO's shipped setting, so a murderer is
//     refused, as on ServUO.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Factions;
using Server.Gumps;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Spells;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SerpentsJawbone : Item, ISecurable
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private SecureLevel _level;

    [Constructible]
    public SerpentsJawbone() : base(0x9F74)
    {
    }

    public static Dictionary<int, Point3D> Locations { get; set; } = new()
    {
        [1157135] = new Point3D(1156, 1143, -24), // The Village of Lakeshire
        [1157619] = new Point3D(644, 854, -56),   // The Rat Fort
        [1157620] = new Point3D(1363, 1075, -13), // Reg Volom
        [1016410] = new Point3D(1572, 1046, -8),  // Twin Oaks Tavern
        [1157621] = new Point3D(984, 622, -80),   // The Oasis
        [1078308] = new Point3D(1746, 1221, -1),  // Blood Dungeon
        [1111764] = new Point3D(912, 1362, -21),  // Cyclops Dungeon
        [1111765] = new Point3D(824, 774, -80),   // Exodus Dungeon
        [1111766] = new Point3D(349, 1434, 16),   // The Kirin Passage
        [1157622] = new Point3D(971, 303, 54),    // Pass of Karnaugh
        [1157623] = new Point3D(1033, 1154, -24), // The Rat Cave
        [1078315] = new Point3D(541, 466, -72),   // Terort Skitas
        [1111825] = new Point3D(1450, 1477, -29), // Twisted Weald
        [1113002] = new Point3D(642, 1307, -55),  // Wisp Dungeon
        [1157624] = new Point3D(753, 497, -62),   // Gwenno's Memorial
        [1157625] = new Point3D(1504, 628, -14),  // Desert Gypsy Camp
        [1113000] = new Point3D(1785, 573, 71)    // Rock Dungeon
    };

    public override int LabelNumber => 1157654; // Serpent's Jawbone

    public override bool ForceShowProperties => true;

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        SetSecureLevelEntry.AddTo(from, this, ref list);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if ((IsLockedDown || IsSecure) && from.InRange(GetWorldLocation(), 2))
        {
            from.SendGump(new InternalGump(from as PlayerMobile, this));
        }
        else if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500295); // You are too far away to do that.
        }
        else
        {
            from.SendLocalizedMessage(502692); // This must be in a house and be locked down to work.
        }
    }

    private class InternalGump : DynamicGump
    {
        public InternalGump(PlayerMobile pm, Item jawbone) : base(100, 100)
        {
            Jawbone = jawbone;
            User = pm;
        }

        public Item Jawbone { get; }
        public PlayerMobile User { get; }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddBackground(0, 0, 370, 428, 0x1400);

            builder.AddHtmlLocalized(10, 10, 350, 18, 1114513, "#1156704", 0x56BA); // <DIV ALIGN=CENTER>~1_TOKEN~</DIV>

            var i = 0;

            foreach (var key in Locations.Keys)
            {
                builder.AddButton(10, 41 + i * 20, 1209, 1210, key);
                builder.AddHtmlLocalized(50, 41 + i * 20, 150, 20, key, 0x7FFF);
                i++;
            }
        }

        public override void OnResponse(NetState state, in RelayInfo info)
        {
            if (info.ButtonID > 0)
            {
                var id = info.ButtonID;

                if (Locations.TryGetValue(id, out var p) && CheckTravel(p))
                {
                    BaseCreature.TeleportPets(User, p, Map.Ilshenar);
                    User.Combatant = null;
                    User.Warmode = false;
                    User.Hidden = true;

                    User.MoveToWorld(p, Map.Ilshenar);

                    Effects.PlaySound(p, Map.Ilshenar, 0x1FE);
                }
            }
        }

        private bool CheckTravel(Point3D p)
        {
            if (!User.InRange(Jawbone.GetWorldLocation(), 2) || User.Map != Jawbone.Map)
            {
                User.SendLocalizedMessage(500295); // You are too far away to do that.
            }
            else if (User.Murderer) // ServUO: SpellHelper.RestrictRedTravel && User.Murderer (see the header)
            {
                User.SendLocalizedMessage(1019004); // You are not allowed to travel there.
            }
            else if (Sigil.ExistsOn(User))
            {
                User.SendLocalizedMessage(1019004); // You are not allowed to travel there.
            }
            else if (User.Criminal)
            {
                User.SendLocalizedMessage(1005561, "", 0x22); // Thou'rt a criminal and cannot escape so easily.
            }
            else if (SpellHelper.CheckCombat(User))
            {
                User.SendLocalizedMessage(1005564, "", 0x22); // Wouldst thou flee during the heat of battle??
            }
            else if (User.Spell != null)
            {
                User.SendLocalizedMessage(1049616); // You are too busy to do that at the moment.
            }
            else if (User.Map == Map.Ilshenar && User.InRange(p, 1))
            {
                User.SendLocalizedMessage(1019003); // You are already there.
            }
            else
            {
                return true;
            }

            return false;
        }
    }
}
