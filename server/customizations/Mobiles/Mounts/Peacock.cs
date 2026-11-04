// cc-P793: the Peacock, a tamable mount of our own (Chase 11-03; P790's rank 1). A low tier ride for new tamers: an
// ostard's numbers on the client's own Bird_Peacock_Mount art. Every number is in PeacockTable (Configuration/peacock.json);
// a peacock takes them when it is made, so a saved one keeps what it was born with.
//
// Body 1440 and mount item 0x3ECF are the client's pair (TazUO Game/Data/Mounts.cs: 0x3ECF draws 0x05A0, height 9). Pinned's
// bodyTable.cfg has no row for 1440, so ShardBodyTypes types it Animal, and pinned's shrink.json has none, so
// server/data/shrink.json gives it the figurine 0xA0C0 (ShrinkTable-shard-rows.patch).
//
// After the idea of WiKKiD's peacock mount (reference only, D:\UO\third-party\code\peacock-mount); built here in our style.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Peacock : BaseMount
{
    public const int BodyId = 1440;
    public const int MountItemId = 0x3ECF;
    public const int ShrinkItemId = 0xA0C0;

    [Constructible]
    public Peacock() : base(BodyId, MountItemId, AIType.AI_Animal, FightMode.Aggressor)
    {
        var t = PeacockTable.Current;

        BaseSoundID = 0x270;

        SetStr(t.StrMin, t.StrMax);
        SetDex(t.DexMin, t.DexMax);
        SetInt(t.IntMin, t.IntMax);

        SetHits(t.HitsMin, t.HitsMax);
        SetMana(0);

        SetDamage(t.DamageMin, t.DamageMax);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, t.PhysicalResistMin, t.PhysicalResistMax);
        SetResistance(ResistanceType.Fire, t.FireResistMin, t.FireResistMax);

        SetSkill(SkillName.MagicResist, t.MagicResistMin, t.MagicResistMax);
        SetSkill(SkillName.Tactics, t.TacticsMin, t.TacticsMax);
        SetSkill(SkillName.Wrestling, t.WrestlingMin, t.WrestlingMax);

        Fame = t.Fame;
        Karma = 0;

        Tamable = true;
        ControlSlots = t.ControlSlots;
        MinTameSkill = t.MinTameSkill;
    }

    public override string DefaultName => "a peacock";
    public override string CorpseName => "a peacock corpse";

    public override int Meat => PeacockTable.Current.Meat;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;
}
