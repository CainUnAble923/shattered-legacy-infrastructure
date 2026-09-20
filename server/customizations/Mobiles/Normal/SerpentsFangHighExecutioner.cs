// ServUO: Mobiles/Normal/SerpentsFangHighExecutioner.cs (CC6 batch 4). Values verbatim; serialization by the generator.
// Spawned by one Citadel spawner (shared/malas/Citadel.json, entry "serpentsfanghighexecutioner").
//
// THE FIRST SUBCLASS OF ONE OF OUR OWN PORTED CREATURES. Its parent is SerpentsFangAssassin (CC6 batch 2), itself
// [SerializationGenerator(0, false)]. The rule, read off the generator's emitted code for stock Saliva : Harpy
// (notes/cc6-creatures-batch4.md section 2): every level that carries the attribute gets its own (Serial) constructor,
// its own `SerializationVersion` constant and a Serialize that calls base.Serialize first and then writes its own
// version and fields. So the child carries its own [SerializationGenerator(0, false)] and is `partial`, its version is
// independent of the parent's, and the stream is parent-version, parent-fields, child-version, child-fields. A
// subclass WITHOUT the attribute would have no (Serial) constructor and no version slot of its own.
//
// The parameterless constructor runs the assassin's first (title, race, hair, the eight 0x51D-dyed pieces and the sai,
// the assassin's stats and 13000 fame) and then overwrites what ServUO's does: stats, 800 hits, fame 25000, armour 60.
// base.OnDeath(c) is the assassin's, so a badge drops at the parent's 30% and then again at this file's 50%, as on
// ServUO. The SerpentFangKey drops on every death; it is a PeerlessKey (P9), blessed, one week, nothing consumes it here.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SerpentsFangHighExecutioner : SerpentsFangAssassin
{
    [Constructible]
    public SerpentsFangHighExecutioner()
    {
        Title = "of the Serpent's Fang Sect";
        SetStr(545, 560);
        SetDex(160, 175);
        SetInt(160, 175);

        SetHits(800);
        SetStam(190, 205);

        SetDamage(15, 20);

        Fame = 25000;
        Karma = -25000;

        VirtualArmor = 60;
    }

    public override string CorpseName => "a black order high executioner corpse";
    public override string DefaultName => "Black Order High Executioner";

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosFilthyRich, 6);
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        from?.Damage(damage / 2, from);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new SerpentFangKey());

        if (Utility.RandomDouble() < 0.5)
        {
            c.DropItem(new SerpentFangSectBadge());
        }
    }
}
