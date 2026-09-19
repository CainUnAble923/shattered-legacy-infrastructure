// ServUO: Mobiles/Normal/DespiseGoodCreatures.cs (CC4 Despise). Eight types, one file, as ServUO keeps them.
//
// Every constructor pair (X() : this(1) / X(int powerLevel)) collapses to one optional parameter, the
// recipe's shape. `Name = "..."` becomes DefaultName. `SetWeaponAbility(x)` becomes GetWeaponAbility, the
// Q-013 transformation; WeaponAbilityChance is a stock virtual. Range perception 10 and the speed
// arguments are dropped per notes/port-recipe.md.
//
// FightMode.Evil is DespiseCreature.AlignedFightMode(Alignment.Good) == FightMode.Closest; see the
// header of DespiseCreature.cs for why that is the faithful value and not ModernUO's FightMode.Evil.
//
// Silenii: ServUO calls SetMagicalAbility(MagicalAbility.Discordance) and overrides GetBardTarget so the
// creature discords its enemies. That is Pet Training's MagicalAbility group (gap-register B3, WAIT),
// with no ModernUO counterpart; the call and the two methods that exist only to serve it are dropped
// (D-32, the Saurosaurus precedent).
//
// [TypeAlias("Server.Engines.Despise.Sagittari")] on Sagittarri is a ServUO save-upgrade alias for a
// misspelling that never reached this shard; dropped.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class Silenii : DespiseCreature
{
    [Constructible]
    public Silenii(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Good))
    {
        Body = 0x10F;
        BaseSoundID = 0x585;

        Fame = GetFame;
        Karma = GetKarmaGood;

        Power = powerLevel;
        // ServUO: SetMagicalAbility(MagicalAbility.Discordance); dropped, D-32.
    }

    public override string DefaultName => "Silenii";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override int StrStart => Utility.RandomMinMax(65, 75);
    public override int DexStart => Utility.RandomMinMax(100, 110);
    public override int IntStart => Utility.RandomMinMax(100, 110);
}

[SerializationGenerator(0, false)]
public partial class ForestNymph : DespiseCreature
{
    [Constructible]
    public ForestNymph(int powerLevel = 1) : base(AIType.AI_Mage, AlignedFightMode(Alignment.Good))
    {
        Body = 266;
        BaseSoundID = 0x467;

        Fame = GetFame;
        Karma = GetKarmaGood;
        Power = powerLevel;
    }

    public override string DefaultName => "Forest Nymph";

    protected override BaseAI ForcedAI => new DespiseMageAI(this);
    public override int StrStart => Utility.RandomMinMax(65, 80);
    public override int DexStart => Utility.RandomMinMax(70, 80);
    public override int IntStart => Utility.RandomMinMax(110, 150);
}

[SerializationGenerator(0, false)]
public partial class DespiseUnicorn : DespiseCreature
{
    [Constructible]
    public DespiseUnicorn(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Good))
    {
        Body = 0x7A;
        BaseSoundID = 0x4BC;

        Fame = GetFame;
        Karma = GetKarmaGood;
        Power = powerLevel;
    }

    public override string DefaultName => "Unicorn";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override int StrStart => Utility.RandomMinMax(80, 100);
    public override int DexStart => Utility.RandomMinMax(110, 115);
    public override int IntStart => Utility.RandomMinMax(100, 115);

    public override bool RaiseDamage => true;

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ArmorIgnore;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class Sagittarri : DespiseCreature
{
    [Constructible]
    public Sagittarri(int powerLevel = 1) : base(AIType.AI_Archer, AlignedFightMode(Alignment.Good))
    {
        Body = 101;
        BaseSoundID = 679;

        SetSkill(SkillName.Archery, SkillStart);

        Fame = GetFame;
        Karma = GetKarmaGood;

        AddItem(new Bow());
        PackItem(new Arrow(Utility.RandomMinMax(5, 10)));
        Power = powerLevel;

        RangeFight = 8;
    }

    public override string DefaultName => "Sagittarri";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override int StrStart => Utility.RandomMinMax(40, 55);
    public override int DexStart => Utility.RandomMinMax(160, 180);
    public override int IntStart => Utility.RandomMinMax(110, 120);

    public override bool RaiseDamage => true;
}

[SerializationGenerator(0, false)]
public partial class Ursadane : DespiseCreature
{
    [Constructible]
    public Ursadane(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Good))
    {
        Body = 212;
        BaseSoundID = 0xA3;

        Fame = GetFame;
        Karma = GetKarmaGood;
        Power = powerLevel;
    }

    public override string DefaultName => "Ursadane";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override PackInstinct PackInstinct => PackInstinct.Bear;

    public override bool RaiseDamage => true;

    public override int StrStart => Utility.RandomMinMax(150, 175);
    public override int DexStart => Utility.RandomMinMax(90, 105);
    public override int IntStart => Utility.RandomMinMax(30, 40);

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.CrushingBlow;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class DivineGuardian : DespiseCreature
{
    [Constructible]
    public DivineGuardian(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Good))
    {
        Body = 123;
        BaseSoundID = 0x2F7;

        Fame = GetFame;
        Karma = GetKarmaGood;
        Power = powerLevel;
    }

    public override string DefaultName => "Divine Guardian";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);

    public override bool RaiseDamage => true;

    public override int StrStart => Utility.RandomMinMax(150, 175);
    public override int DexStart => Utility.RandomMinMax(120, 130);
    public override int IntStart => Utility.RandomMinMax(50, 60);

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ConcussionBlow;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class Dendrite : DespiseCreature
{
    [Constructible]
    public Dendrite(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Good))
    {
        Body = 301;

        Fame = GetFame;
        Karma = GetKarmaGood;
        Power = powerLevel;
    }

    public override string DefaultName => "Dendrite";

    public override int GetIdleSound() => 443;
    public override int GetDeathSound() => 31;
    public override int GetAttackSound() => 672;

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);

    public override bool RaiseDamage => true;

    public override int StrStart => Utility.RandomMinMax(100, 120);
    public override int DexStart => Utility.RandomMinMax(140, 155);
    public override int IntStart => Utility.RandomMinMax(30, 50);

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.DoubleStrike;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class Fairy : DespiseCreature
{
    private const double HealThreshold = 0.60;

    // ServUO does not serialize this and resets it to now on load; the default here is DateTime.MinValue,
    // which is also "due now".
    private DateTime _nextHeal;

    [Constructible]
    public Fairy(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Good))
    {
        Body = 0x108;
        BaseSoundID = 0x467;

        Fame = GetFame;
        Karma = GetKarmaGood;

        _nextHeal = Core.Now;
        Power = powerLevel;
    }

    public override string DefaultName => "Fairy";

    public virtual int MinHeal => Math.Max(10, Power * 3);
    public virtual int MaxHeal => Math.Max(25, Power * 5);

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);

    public override int StrStart => Utility.RandomMinMax(85, 100);
    public override int DexStart => Utility.RandomMinMax(110, 125);
    public override int IntStart => Utility.RandomMinMax(130, 150);

    public override void OnThink()
    {
        base.OnThink();

        if (_nextHeal < Core.Now && Map != null && Map != Map.Internal)
        {
            var eligible = new List<Mobile>();

            foreach (var m in Map.GetMobilesInRange(Location, 8))
            {
                if (m.Alive && m.Hits <= (int)(m.HitsMax * HealThreshold) && CanDoHeal(m))
                {
                    eligible.Add(m);
                }
            }

            if (eligible.Count > 0)
            {
                var m = eligible[Utility.Random(eligible.Count)];

                Direction = GetDirectionTo(m);

                SpellHelper.Heal(Utility.RandomMinMax(MinHeal, MaxHeal), m, this);
                m.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
                m.PlaySound(0x202);

                var nextHeal = Utility.RandomMinMax(20 - Power, 30 - Power);
                _nextHeal = Core.Now + TimeSpan.FromSeconds(nextHeal);
                return;
            }

            _nextHeal = Core.Now + TimeSpan.FromSeconds(5);
        }
    }

    private bool CanDoHeal(Mobile toHeal)
    {
        if (toHeal is DespiseCreature dc && dc.Alignment == Alignment)
        {
            return true;
        }

        return toHeal is PlayerMobile &&
               (toHeal.Karma < 0 && Alignment == Alignment.Evil || toHeal.Karma > 0 && Alignment == Alignment.Good);
    }
}
