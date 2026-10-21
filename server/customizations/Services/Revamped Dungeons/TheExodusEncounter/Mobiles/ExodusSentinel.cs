// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ExodusSentinel.cs (cc-P195): the dungeon's clockwork
// sentinel, three to a spawner. The barrier and the energy bolt counter are pinned's monster abilities (see
// ExodusDrone.cs). One kill in ten hands the killer a ritual key.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ExodusSentinel : BaseCreature
{
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.MagicalBarrier, MonsterAbilities.EnergyBoltCounter };

    [Constructible]
    public ExodusSentinel() : base(AIType.AI_Melee)
    {
        Body = 0x2FB;
        Hue = 0xA92;

        SetStr(854, 934);
        SetDex(81, 90);
        SetInt(81, 105);

        SetHits(516, 564);

        SetDamage(16, 22);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 25, 35);
        SetResistance(ResistanceType.Poison, 25, 35);
        SetResistance(ResistanceType.Energy, 25, 35);

        SetSkill(SkillName.MagicResist, 91.5, 99.6);
        SetSkill(SkillName.Tactics, 91.9, 99.4);
        SetSkill(SkillName.Wrestling, 90.1, 98.9);

        Fame = 18000;
        Karma = -18000;
        VirtualArmor = 65;

        PackItem(new PowerCrystal());
        PackItem(new ArcaneGem());
        PackItem(new ClockworkAssembly());
    }

    public override string CorpseName => "a sentinel's corpse";
    public override string DefaultName => "Exodus Sentinel";

    public override bool IsScaredOfScaryThings => false;
    public override bool IsScaryToPets => true;
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
        AddLoot(LootPack.Rich);
    }

    public override void OnKilledBy(Mobile m)
    {
        base.OnKilledBy(m);

        if (Utility.RandomDouble() < 0.1)
        {
            ExodusChest.GiveRituelItem(m);
        }
    }
}
