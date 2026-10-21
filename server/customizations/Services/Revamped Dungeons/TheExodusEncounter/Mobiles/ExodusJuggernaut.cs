// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ExodusJuggernaut.cs (cc-P195): the heavy clockwork
// that holds the dungeon's deep room with the minion lord. The barrier and the energy bolt counter are pinned's
// monster abilities (see ExodusDrone.cs). One kill in ten hands the killer a ritual key.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ExodusJuggernaut : BaseCreature
{
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.MagicalBarrier, MonsterAbilities.EnergyBoltCounter };

    [Constructible]
    public ExodusJuggernaut() : base(AIType.AI_Melee)
    {
        Body = 0x2F0;
        Hue = 2702;

        SetStr(1506, 1565);
        SetDex(92, 99);
        SetInt(101, 126);

        SetHits(1012, 1069);
        SetDamage(19, 25);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 60, 80);
        SetResistance(ResistanceType.Fire, 60, 80);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 99.1, 100.0);
        SetSkill(SkillName.Tactics, 99.1, 100.0);
        SetSkill(SkillName.Wrestling, 99.1, 100.0);

        Fame = 18000;
        Karma = -18000;
        VirtualArmor = 65;

        PackItem(new PowerCrystal());
        PackItem(new ArcaneGem());
        PackItem(new ClockworkAssembly());
    }

    public override string CorpseName => "a juggernaut's corpse";
    public override string DefaultName => "Exodus Juggernaut";

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
