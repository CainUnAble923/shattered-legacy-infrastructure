// cc-P199: ServUO pub57 Scripts/Items/Equipment/Talismans/TalismanofGoblinSlaying.cs. The Talisman of Goblin Slaying
// (1095011, art 0x2F58, name always shown): a talisman whose slayer is Goblin, with a 1200-second max charge time.
//
// NOT YET FUNCTIONAL, deliberately. ServUO sets Slayer = TalismanSlayerName.Goblin (TalismanofGoblinSlaying.cs:14).
// Pinned's TalismanSlayerName stops at Bovine and has no Goblin (Items/Talismans/TalismanSlayer.cs:7-19), and the
// slayer table pinned's BaseWeapon reads (TalismanSlayer.Slays, BaseWeapon.cs:2290) is private, so neither the value
// nor its creature list can be added from customizations. The slayer is left at None until a patch to pinned
// TalismanSlayer.cs adds them (Goblin = 10, after Bovine, outside GetRandomSlayer's 1-9; and a [Goblin] entry with
// ServUO's fourteen goblin types, ServUO TalismanSlayer.cs:135-145, all of which are our customizations). When it
// lands, set `Slayer = TalismanSlayerName.Goblin;` in the constructor below. Until then the talisman equips, names
// itself and does nothing else, which is what pinned's base gives a talisman with no slayer, summon or bonus.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class TalismanofGoblinSlaying : BaseTalisman
    {
        [Constructible]
        public TalismanofGoblinSlaying() : base(0x2F58)
        {
            // ServUO: Slayer = TalismanSlayerName.Goblin; needs the TalismanSlayer patch described above.
            MaxChargeTime = 1200;
        }

        public override int LabelNumber => 1095011; // Talisman of Goblin Slaying
        public override bool ForceShowName => true;
    }
}
