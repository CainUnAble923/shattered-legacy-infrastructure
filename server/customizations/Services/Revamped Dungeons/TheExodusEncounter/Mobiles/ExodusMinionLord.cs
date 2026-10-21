// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ExodusMinionLord.cs (cc-P195): the lord of the deep
// room, with the juggernaut. The barrier and the energy bolt counter are pinned's monster abilities (see
// ExodusDrone.cs). One loot roll in ten holds a ritual key. ServUO names him "Exodus Minion Lord" at birth and
// renames every loaded one "exodus minion lord" (its Deserialize), so the lowercase name is the one it means.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ExodusMinionLord : BaseCreature
{
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.MagicalBarrier, MonsterAbilities.EnergyBoltCounter };

    [Constructible]
    public ExodusMinionLord() : base(AIType.AI_Melee)
    {
        Body = 0x2FB;
        Hue = 0xA92;

        SetStr(1501, 1571);
        SetDex(74, 78);
        SetInt(66, 89);

        SetHits(903, 957);

        SetDamage(19, 25);

        SetResistance(ResistanceType.Physical, 65, 80);
        SetResistance(ResistanceType.Fire, 65, 80);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 99.3, 99.8);
        SetSkill(SkillName.Tactics, 99.4, 100.0);
        SetSkill(SkillName.Wrestling, 99.2, 99.7);

        Fame = 18000;
        Karma = -18000;
        VirtualArmor = 65;

        PackItem(new PowerCrystal());
        PackItem(new ArcaneGem());
        PackItem(new ClockworkAssembly());
    }

    public override string CorpseName => "a exodus minion lord's corpse";
    public override string DefaultName => "exodus minion lord";

    public override bool IsScaredOfScaryThings => false;
    public override bool IsScaryToPets => true;
    public override bool AutoDispel => true;
    public override bool BardImmune => !Core.AOS;
    public override Poison PoisonImmune => Poison.Lethal;

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override int GetIdleSound() => 0x218;
    public override int GetAngerSound() => 0x26C;
    public override int GetDeathSound() => 0x211;
    public override int GetAttackSound() => 0x232;
    public override int GetHurtSound() => 0x140;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Rich);

        if (Utility.RandomDouble() < 0.1)
        {
            PackItem(ExodusChest.RandomRitualItem());
        }
    }
}
