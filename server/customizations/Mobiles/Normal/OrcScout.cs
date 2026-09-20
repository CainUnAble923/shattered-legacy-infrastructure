// ServUO: Mobiles/Normal/OrcScout.cs (CC6 batch 3). Values verbatim; serialization by the generator. iRangeFight 7 is
// carried (ServUO passes 7, not the default 1), as batch 1's GreenGoblinScout does. ServUO's OnThink does not call the
// base, so no ML shout; copied. m.IsPlayer() is AccessLevel == Player; GetMobilesInRange goes through the map.
//
// DEVIATION D-44 (batch 1's number, the same loss): ServUO runs this on AI_OrcScout (Mobiles/AI/OrcScoutAI.cs, 329
// lines: melee that hides while it wanders, a 4% teleport-to-target per combat think, flees below 20% hits). Pinned
// ModernUO's AIType has no OrcScout (checked Mobiles/AI/ directly: no Mysticism, Spellweaving or OrcScout AI), so it
// runs AI_Melee. CanStealth => true belongs to that AI (only OrcScoutAI hides it) and has nothing to inline.
//
// Two ServUO members that are pinned ModernUO's own under other names, so nothing is dropped:
//   HealChance => 1.0: ServUO's InitializeAbilities (BaseCreature.cs:620) turns it into SpecialAbility.Heal, whose
//     effect is CheckHeal (:6808): HealStart(this) when Hits < .78 HitsMax or poisoned. ModernUO's CanHeal
//     (BaseCreature.cs:1095) does the same from OnThink (:3624) with HealTrigger 0.78 (:1101), same HealStart/Heal code.
//   TribeType.Orc: OppositionGroup.SavagesAndOrcs, which the file also sets (batch 1 §4).
// Yeast is ours (Services/New Magincia/Distillation/Items/Yeast.cs, this batch); OrcishBow was already ours (CC9
// batch 3, Items/Equipment/Weapons/OrcishBow.cs).

using ModernUO.Serialization;
using Server.Items;
using Server.Misc;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class OrcScout : BaseCreature
{
    [Constructible]
    public OrcScout() : base(AIType.AI_Melee, FightMode.Closest, DefaultRangePerception, 7)
    {
        Body = 0xB5;
        BaseSoundID = 0x45A;

        SetStr(96, 120);
        SetDex(101, 130);
        SetInt(36, 60);

        SetHits(58, 72);
        SetMana(30, 60);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 25, 35);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 15, 25);
        SetResistance(ResistanceType.Poison, 15, 20);
        SetResistance(ResistanceType.Energy, 25, 30);

        SetSkill(SkillName.MagicResist, 50.1, 75.0);
        SetSkill(SkillName.Tactics, 55.1, 80.0);

        SetSkill(SkillName.Fencing, 50.1, 70.0);
        SetSkill(SkillName.Archery, 80.1, 120.0);
        SetSkill(SkillName.Parry, 40.1, 60.0);
        SetSkill(SkillName.Healing, 80.1, 100.0);
        SetSkill(SkillName.Anatomy, 50.1, 90.0);
        SetSkill(SkillName.DetectHidden, 100.1, 120.0);
        SetSkill(SkillName.Hiding, 100.0, 120.0);
        SetSkill(SkillName.Stealth, 80.1, 120.0);

        Fame = 1500;
        Karma = -1500;

        PackItem(new Apple(Utility.RandomMinMax(3, 5)));
        PackItem(new Arrow(Utility.RandomMinMax(60, 70)));
        PackItem(new Bandage(Utility.RandomMinMax(1, 15)));

        if (Utility.RandomDouble() < 0.1)
        {
            AddItem(new OrcishBow());
        }
        else
        {
            AddItem(new Bow());
        }

        if (Utility.RandomDouble() < 0.5)
        {
            PackItem(new Yeast());
        }
    }

    public override string CorpseName => "an orcish corpse";
    public override string DefaultName => "an orc scout";

    public override bool CanRummageCorpses => true;
    // ServUO: HealChance => 1.0 (see the header).
    public override bool CanHeal => true;
    public override int Meat => 1;

    public override InhumanSpeech SpeechType => InhumanSpeech.Orc;
    public override OppositionGroup OppositionGroup => OppositionGroup.SavagesAndOrcs;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
    }

    public override bool IsEnemy(Mobile m)
    {
        if (m.Player && m.FindItemOnLayer(Layer.Helm) is OrcishKinMask)
        {
            return false;
        }

        return base.IsEnemy(m);
    }

    public override void AggressiveAction(Mobile aggressor, bool criminal)
    {
        base.AggressiveAction(aggressor, criminal);

        var item = aggressor.FindItemOnLayer(Layer.Helm);

        if (item is OrcishKinMask)
        {
            AOS.Damage(aggressor, 50, 0, 100, 0, 0, 0);
            item.Delete();
            aggressor.FixedParticles(0x36BD, 20, 10, 5044, EffectLayer.Head);
            aggressor.PlaySound(0x307);
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
