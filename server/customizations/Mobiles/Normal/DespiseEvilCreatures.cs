// ServUO: Mobiles/Normal/DespiseEvilCreatures.cs (CC4 Despise). Eight types, one file, as ServUO keeps them.
// Same transformations as DespiseGoodCreatures.cs; FightMode.Good is
// DespiseCreature.AlignedFightMode(Alignment.Evil) == FightMode.Closest (header of DespiseCreature.cs).
//
// Phantom: SetMagicalAbility(MagicalAbility.Discordance) and the GetBardTarget override dropped (D-32),
// exactly as Silenii.
// BirlingBlades: [TypeAlias("Server.Engines.Despise.BerlingBlades")] dropped, a ServUO save-upgrade alias.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class Phantom : DespiseCreature
{
    [Constructible]
    public Phantom(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Evil))
    {
        Body = 0xFC;
        BaseSoundID = 0x482;
        Hue = 2671;

        Fame = GetFame;
        Karma = GetKarmaEvil;

        Power = powerLevel;
        // ServUO: SetMagicalAbility(MagicalAbility.Discordance); dropped, D-32.
    }

    public override string DefaultName => "Phantom";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override int StrStart => Utility.RandomMinMax(65, 75);
    public override int DexStart => Utility.RandomMinMax(100, 110);
    public override int IntStart => Utility.RandomMinMax(100, 110);
}

[SerializationGenerator(0, false)]
public partial class Naba : DespiseCreature
{
    [Constructible]
    public Naba(int powerLevel = 1) : base(AIType.AI_Mage, AlignedFightMode(Alignment.Evil))
    {
        Body = 0x88;
        BaseSoundID = 639;
        Hue = 2707;

        Fame = GetFame;
        Karma = GetKarmaEvil;
        Power = powerLevel;
    }

    public override string DefaultName => "Naba";

    protected override BaseAI ForcedAI => new DespiseMageAI(this);
    public override int StrStart => Utility.RandomMinMax(65, 80);
    public override int DexStart => Utility.RandomMinMax(70, 80);
    public override int IntStart => Utility.RandomMinMax(110, 150);
}

[SerializationGenerator(0, false)]
public partial class Darkmane : DespiseCreature
{
    [Constructible]
    public Darkmane(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Evil))
    {
        Body = 0xCC;
        Hue = 1910;
        BaseSoundID = 0xA8;

        Fame = GetFame;
        Karma = GetKarmaEvil;
        Power = powerLevel;
    }

    public override string DefaultName => "Darkmane";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override int StrStart => Utility.RandomMinMax(80, 100);
    public override int DexStart => Utility.RandomMinMax(110, 115);
    public override int IntStart => Utility.RandomMinMax(100, 115);

    public override bool RaiseDamage => true;

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ArmorIgnore;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class Skeletrex : DespiseCreature
{
    [Constructible]
    public Skeletrex(int powerLevel = 1) : base(AIType.AI_Archer, AlignedFightMode(Alignment.Evil))
    {
        Body = 147;
        BaseSoundID = 451;
        Hue = 2075;

        SetSkill(SkillName.Archery, SkillStart);

        Fame = GetFame;
        Karma = GetKarmaEvil;

        AddItem(new Bow());
        PackItem(new Arrow(Utility.RandomMinMax(5, 10))); // OSI: in a sub backpack; ServUO's own note
        Power = powerLevel;
    }

    public override string DefaultName => "Skeletrex";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override int StrStart => Utility.RandomMinMax(40, 55);
    public override int DexStart => Utility.RandomMinMax(160, 180);
    public override int IntStart => Utility.RandomMinMax(110, 120);

    public override bool RaiseDamage => true;
}

[SerializationGenerator(0, false)]
public partial class Hellion : DespiseCreature
{
    [Constructible]
    public Hellion(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Evil))
    {
        Body = 4;
        BaseSoundID = 0x174;
        Hue = 2671;

        Fame = GetFame;
        Karma = GetKarmaEvil;
        Power = powerLevel;
    }

    public override string DefaultName => "Hellion";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);
    public override PackInstinct PackInstinct => PackInstinct.Bear;
    public override int MinDamMax => 15;
    public override int MaxDamMax => 26;
    public override bool RaiseDamage => true;

    public override int StrStart => Utility.RandomMinMax(150, 175);
    public override int DexStart => Utility.RandomMinMax(90, 105);
    public override int IntStart => Utility.RandomMinMax(30, 40);

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.CrushingBlow;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class Echidnite : DespiseCreature
{
    [Constructible]
    public Echidnite(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Evil))
    {
        Body = 250;
        BaseSoundID = 0x52A;
        Hue = 2671;

        Fame = GetFame;
        Karma = GetKarmaEvil;
        Power = powerLevel;
    }

    public override string DefaultName => "Echidnite";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);

    public override bool RaiseDamage => true;

    public override int StrStart => Utility.RandomMinMax(150, 175);
    public override int DexStart => Utility.RandomMinMax(120, 130);
    public override int IntStart => Utility.RandomMinMax(50, 60);

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ConcussionBlow;
    public override double WeaponAbilityChance => 0.5;
}

[SerializationGenerator(0, false)]
public partial class BirlingBlades : DespiseCreature
{
    [Constructible]
    public BirlingBlades(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Evil))
    {
        Body = 574;
        BaseSoundID = 224;
        Hue = 2672;

        Fame = GetFame;
        Karma = GetKarmaEvil;
        Power = powerLevel;
    }

    public override string DefaultName => "Birling Blades";

    protected override BaseAI ForcedAI => new DespiseMeleeAI(this);

    public override bool RaiseDamage => true;

    public override int StrStart => Utility.RandomMinMax(100, 120);
    public override int DexStart => Utility.RandomMinMax(140, 155);
    public override int IntStart => Utility.RandomMinMax(30, 50);

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.DoubleStrike;
    public override double WeaponAbilityChance => 0.5;

    public override int GetAngerSound() => 0x23A;
    public override int GetAttackSound() => 0x3B8;
    public override int GetHurtSound() => 0x23A;
}

[SerializationGenerator(0, false)]
public partial class Prometheoid : DespiseCreature
{
    private const double HealThreshold = 0.5;

    private DateTime _nextHeal;

    [Constructible]
    public Prometheoid(int powerLevel = 1) : base(AIType.AI_Melee, AlignedFightMode(Alignment.Evil))
    {
        Body = 305;
        BaseSoundID = 224;
        Hue = 2671;

        Fame = GetFame;
        Karma = GetKarmaEvil;

        _nextHeal = Core.Now;
        Power = powerLevel;
    }

    public override string DefaultName => "Prometheoid";

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
