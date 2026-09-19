// ServUO: Mobiles/Normal/TrapdoorSpider.cs (CC6 batch 1). Values verbatim; serialization by the generator.
//
// ServUO's BaseCreature has a CanStealth virtual that its OnMove consults (Mobiles/Normal/BaseCreature.cs:4858-4880):
// a hidden creature with CanStealth and Stealth >= 25 spends AllowedStealthSteps and re-rolls Stealth.OnUse when
// they run out, instead of revealing on its first step. ModernUO has no CanStealth and no BaseCreature.OnMove;
// Mobile.OnMove (Server/Mobiles/Mobile.cs:4071) reveals any hidden Player-access mobile whose steps are spent,
// which for this creature is its first step after HideSelf. So ServUO's parent branch is inlined below as an
// OnMove override, the AGENTS.md "port the old parent's members" shape. Stealth.OnUse is the same handler on
// both emulators (UOContent/Skills/Stealth.cs:71).

using ModernUO.Serialization;
using Server.Items;
using Server.SkillHandlers;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TrapdoorSpider : BaseCreature
{
    [Constructible]
    public TrapdoorSpider() : base(AIType.AI_Melee)
    {
        Body = 737;
        Hidden = true;

        SetStr(100, 104);
        SetDex(162, 165);
        SetInt(29, 50);

        SetHits(125, 144);

        SetDamage(15, 18);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Poison, 80);

        SetResistance(ResistanceType.Physical, 0);
        SetResistance(ResistanceType.Fire, 30, 35);
        SetResistance(ResistanceType.Cold, 30, 35);
        SetResistance(ResistanceType.Poison, 40, 45);
        SetResistance(ResistanceType.Energy, 95, 100);

        SetSkill(SkillName.Anatomy, 2.0, 3.8);
        SetSkill(SkillName.MagicResist, 47.5, 57.9);
        SetSkill(SkillName.Poisoning, 70.5, 73.5);
        SetSkill(SkillName.Tactics, 73.3, 78.9);
        SetSkill(SkillName.Wrestling, 92.5, 94.6);
        SetSkill(SkillName.Hiding, 110.3, 119.9);
        SetSkill(SkillName.Stealth, 110.5, 119.6);
    }

    public override string CorpseName => "a trapdoor spider corpse";
    public override string DefaultName => "a trapdoor spider";

    // ServUO: public override bool CanStealth => true; consumed by the OnMove below.
    public bool CanStealth => true;

    public override int TreasureMapLevel => 2;

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        RevealingAction();
        base.OnDamage(amount, from, willKill);
    }

    // ServUO: OnDamagedBySpell(Mobile from); ModernUO's virtual also carries the damage (BaseCreature.cs:1625).
    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        RevealingAction();
        base.OnDamagedBySpell(from, damage);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
    }

    public override int GetIdleSound() => 1605;
    public override int GetAngerSound() => 1602;
    public override int GetHurtSound() => 1604;
    public override int GetDeathSound() => 1603;

    public override void OnThink()
    {
        if (!Alive || Deleted)
        {
            return;
        }

        if (!Hidden)
        {
            var chance = 0.05;

            if (Hits < 20)
            {
                chance = 0.1;
            }

            if (Poisoned)
            {
                chance = 0.01;
            }

            if (Utility.RandomDouble() < chance)
            {
                HideSelf();
            }

            base.OnThink();
        }
    }

    private void HideSelf()
    {
        if (Core.TickCount >= NextSkillTime)
        {
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023
            );

            PlaySound(0x22F);
            Hidden = true;

            UseSkill(SkillName.Stealth);
        }
    }

    // ServUO BaseCreature.OnMove, the Hidden branch, verbatim apart from the CanStealth gate being this type's own.
    protected override bool OnMove(Direction d)
    {
        if (Hidden)
        {
            if (!Mounted && Skills.Stealth.Value >= 25.0 && CanStealth)
            {
                var running = (d & Direction.Running) != 0;

                if (running)
                {
                    if ((AllowedStealthSteps -= 2) <= 0)
                    {
                        RevealingAction();
                    }
                }
                else if (AllowedStealthSteps-- <= 0)
                {
                    Stealth.OnUse(this);
                }
            }
            else
            {
                RevealingAction();
            }
        }

        return true;
    }
}
