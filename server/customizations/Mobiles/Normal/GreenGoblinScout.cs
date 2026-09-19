// ServUO: Mobiles/Normal/GreenGoblinScout.cs (CC6 batch 1). Values verbatim; serialization by the generator.
//
// DEVIATION: ServUO runs this on AI_OrcScout (Mobiles/AI/OrcScoutAI.cs: melee that hides while it wanders, a 4%
// teleport-to-target per combat think, flees below 20% hits). ModernUO's AIType has no OrcScout, so it runs
// AI_Melee. iRangeFight 7 is carried (ServUO passes 7, not the default 1). ServUO's OnThink detect-hidden sweep
// is ported as is: m.IsPlayer() is m.AccessLevel == AccessLevel.Player, and GetMobilesInRange goes through the
// map. ServUO's file sets no VirtualArmor and packs no loot items; neither is added.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GreenGoblinScout : BaseCreature
{
    [Constructible]
    public GreenGoblinScout() : base(AIType.AI_Melee, FightMode.Closest, DefaultRangePerception, 7)
    {
        Body = 723;
        BaseSoundID = 0x600;

        SetStr(276, 309);
        SetDex(65, 79);
        SetInt(107, 146);

        SetHits(174, 198);
        SetMana(107, 146);
        SetStam(65, 79);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 41, 49);
        SetResistance(ResistanceType.Fire, 33, 39);
        SetResistance(ResistanceType.Cold, 26, 33);
        SetResistance(ResistanceType.Poison, 14, 20);
        SetResistance(ResistanceType.Energy, 11, 20);

        SetSkill(SkillName.MagicResist, 90.7, 98.8);
        SetSkill(SkillName.Tactics, 80.9, 86.3);
        SetSkill(SkillName.Wrestling, 107.7, 119.5);
        SetSkill(SkillName.Anatomy, 80.3, 88.2);

        Fame = 1500;
        Karma = -1500;
    }

    public override string CorpseName => "a goblin corpse";
    public override string DefaultName => "a green goblin scout";

    public override int GetAngerSound() => 0x600;
    public override int GetIdleSound() => 0x600;
    public override int GetAttackSound() => 0x5FD;
    public override int GetHurtSound() => 0x5FF;
    public override int GetDeathSound() => 0x5FE;

    public override bool CanRummageCorpses => true;
    public override int TreasureMapLevel => 1;
    public override int Meat => 1;
    // ServUO: TribeType.GreenGoblin. See Mobiles/AI/GoblinOppositionGroup.cs.
    public override OppositionGroup OppositionGroup => GoblinOppositionGroup.GrayAndGreenGoblins;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.01)
        {
            c.DropItem(new LuckyCoin());
        }
    }

    public override void OnThink()
    {
        if (Utility.RandomDouble() < 0.2)
        {
            TryToDetectHidden();
        }
    }

    private Mobile FindTarget()
    {
        if (Map == null)
        {
            return null;
        }

        foreach (var m in Map.GetMobilesInRange(Location, 10))
        {
            if (m.Player && m.Hidden && m.AccessLevel == AccessLevel.Player)
            {
                return m;
            }
        }

        return null;
    }

    private void TryToDetectHidden()
    {
        var m = FindTarget();

        if (m != null)
        {
            if (Core.TickCount >= NextSkillTime && UseSkill(SkillName.DetectHidden))
            {
                var targ = Target;

                targ?.Invoke(this, this);

                Effects.PlaySound(Location, Map, 0x340);
            }
        }
    }
}
