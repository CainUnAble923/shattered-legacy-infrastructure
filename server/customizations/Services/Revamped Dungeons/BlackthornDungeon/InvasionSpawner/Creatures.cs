// ServUO: Services/Revamped Dungeons/BlackthornDungeon/InvasionSpawner/Creatures.cs (cc-P196): Minax's invaders,
// the human and elven soldiers of the city invasions, and their captains.
//
// An Invader rolls a specialty from thirteen (Swords, Fencing, Macing, Archery, Magery, Mysticism, Spellweaving,
// Bushido twice, Ninjitsu, Chivalry, Necromancy, Poisoning), takes a human (three in four) or elven body (always
// elven for Archery and Spellweaving), a random name and the specialty's skill title ("the swordsman" /
// "the swordswoman"; a Bushido invader is "the sampire" half the time, a mage "the wizard"), 120-170 strength,
// 800-1,250 hits, skills 100-120 (105-130 for a captain), resists 10-20 (20-30), and the specialty's gear: swords,
// fencing, mace, archery, mage, samurai, ninja or assassin weapons, plate for the paladin, chain for fighters, a
// hat and robe for casters, the samurai's leather set with a lifeleech 100 blade when a sampire, the ninja's belt
// and poisoned fukiyas, the assassin's lethal-poisoned blade. A caster within four tiles has a one in ten chance
// each think, at most every 30-60 seconds, to blast everyone within four for 80-90 chaos damage under rings of
// black explosions; a sampire keeps Vampiric Embrace up and curses its weapon. Each wears an aura of energy and
// its invasion's name as a property line (1154196 + type). Ultra rich loot, twice.
//
// An InvaderCaptain is the same at 250 strength, 8,000-12,000 hits, 48,000 fame, super boss loot on top, and on
// death hands two Minax artifacts to the two highest damagers with looting rights within twenty tiles (1154530).
//
// Conversion notes (the port recipe's standard substitutions, each a deviation listed in the notes):
//   - AI: pinned has AI_Melee and AI_Mage only of the eight ServUO picks; Ninjitsu, Bushido and Chivalry run
//     AI_Melee, Magery, Mysticism, Spellweaving and Necromancy run AI_Mage (MageAI casts Necromancy from skill).
//     SpellCaster is decided from the specialty, so the stats, gear and the special keep ServUO's split.
//   - HealChance 1.0 on the melee and paladin AIs is CanHeal; the AbilityProfile.HasAbility(Heal) bandages are
//     packed for the same specialties. UseSmartAI and CanStealth (the ninja's stealth) have no pinned member.
//   - SetAreaEffect(AuraOfEnergy) is HasAura: ServUO's AreaEffect fires from OnThink at most every 30 seconds
//     (CooldownDuration) when the combatant is within 3 (MaxRange), on everyone within 5 (EffectRange), for 20-30
//     energy with the 1072073 line, the 0x374A particles and sound 0x51D (Pet Training/AreaEffects.cs:11-19,
//     234-249); pinned's aura runs on AuraInterval (30 s here) over AuraRange (5), gated here on the combatant
//     within 3. An uncontrolled creature pays no mana on either side.
//   - CurseWeaponSpell.IsCursed is BaseWeapon.Cursed here; DateTime.UtcNow is Core.Now; AOS.Damage with chaos is the
//     nine-argument overload; the special's rings capture the radius in a local; GetLootingRights is pinned's
//     static over DamageEntries, read before base.OnDeath.
//   - InvasionController.CreateItem is MinaxArtifacts.CreateForInvasion (Items/MinaxArtifacts.cs).
//   - Perception and speed take pinned's defaults (the port recipe).
//   - The saved fields (Specialty, InvasionType, Sampire) are generated; NextSpecial is reset after a load as
//     ServUO's Deserialize does.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Necromancy;

namespace Server.Engines.Blackthorn;

[SerializationGenerator(0, false)]
public partial class Invader : BaseCreature
{
    private static readonly SkillName[] _specialties =
    {
        SkillName.Swords,
        SkillName.Fencing,
        SkillName.Macing,
        SkillName.Archery,
        SkillName.Magery,
        SkillName.Mysticism,
        SkillName.Spellweaving,
        SkillName.Bushido,
        SkillName.Bushido,
        SkillName.Ninjitsu,
        SkillName.Chivalry,
        SkillName.Necromancy,
        SkillName.Poisoning
    };

    [SerializableField(0, setter: "private")]
    private SkillName _specialty;

    [SerializableField(1, setter: "private")]
    private InvasionType _invasionType;

    [SerializableField(2, setter: "private")]
    private bool _sampire;

    private DateTime _nextSpecial;

    [Constructible]
    public Invader(InvasionType type) : this(RandomSpecialty(), type)
    {
    }

    [Constructible]
    public Invader(SkillName specialty, InvasionType type) : base(GetAI(specialty))
    {
        _specialty = specialty;
        _invasionType = type;

        if (_specialty == SkillName.Bushido && Utility.RandomBool())
        {
            _sampire = true;
        }

        Female = Utility.RandomBool();
        Name = NameList.RandomName(Female ? "female" : "male");

        SetBody();

        string title;

        if (_sampire)
        {
            title = "the sampire";
        }
        else if (specialty == SkillName.Magery)
        {
            title = "the wizard";
        }
        else
        {
            title = $"the {Skills[specialty].Info.Title}";

            if (Female && title.EndsWith("man", StringComparison.Ordinal))
            {
                title = title[..^3] + "woman";
            }
        }

        Title = title;

        SetStr(120, 170);
        SetDex(SpellCaster ? 75 : 150);
        SetInt(SpellCaster ? 1800 : 500);

        SetHits(800, 1250);

        if (IsFighter(_specialty))
        {
            SetDamage(15, 28);
        }
        else if (!SpellCaster)
        {
            SetDamage(12, 22);
        }
        else
        {
            SetDamage(8, 18);
        }

        Fame = 8000;
        Karma = -8000;

        SetResists();
        SetSkills();
        EquipSpecialty();

        _nextSpecial = Core.Now;

        if (_sampire)
        {
            Timer.DelayCall(TimeSpan.FromSeconds(1), () => new VampiricEmbraceSpell(this).Cast());
        }
    }

    public static SkillName RandomSpecialty() => _specialties.RandomElement();

    /// <summary>ServUO GetAI: the eight AIs, folded onto pinned's two (see the header).</summary>
    public static AIType GetAI(SkillName skill) =>
        skill switch
        {
            SkillName.Magery or SkillName.Necromancy or SkillName.Spellweaving or SkillName.Mysticism => AIType.AI_Mage,
            _ => AIType.AI_Melee
        };

    /// <summary>ServUO's "AI == AI_Melee || AI == AI_Paladin": the specialties that heal and use weapon abilities.</summary>
    public static bool IsFighter(SkillName skill) =>
        skill is SkillName.Swords or SkillName.Fencing or SkillName.Macing or SkillName.Archery or SkillName.Poisoning or SkillName.Chivalry;

    public override bool AlwaysMurderer => true;

    // ServUO HealChance: 1.0 on the melee and paladin AIs, 0 otherwise.
    public override bool CanHeal => IsFighter(_specialty);

    public override double WeaponAbilityChance => IsFighter(_specialty) ? 0.4 : 0.1;

    public override WeaponAbility GetWeaponAbility()
    {
        if (Weapon is BaseWeapon wep)
        {
            return 0.6 > Utility.RandomDouble() ? wep.PrimaryAbility : wep.SecondaryAbility;
        }

        return null;
    }

    public virtual bool CanDoSpecial => SpellCaster;

    public virtual double MinSkill => 100.0;
    public virtual double MaxSkill => 120.0;
    public virtual int MinResist => 10;
    public virtual int MaxResist => 20;

    // ServUO: AI != Melee && != Ninja && != Samurai && != Paladin, i.e. the four casting specialties.
    public bool SpellCaster => GetAI(_specialty) == AIType.AI_Mage;

    // ServUO SetAreaEffect(AreaEffect.AuraOfEnergy), see the header.
    public override bool HasAura => true;
    public override TimeSpan AuraInterval => TimeSpan.FromSeconds(30);
    public override int AuraRange => 5;
    public override int AuraBaseDamage => Utility.RandomMinMax(20, 30);
    public override int AuraFireDamage => 0;
    public override int AuraEnergyDamage => 100;

    public override void AuraDamage()
    {
        if (Combatant is not { Alive: true, Deleted: false } c || !InRange(c, 3) || c.Map != Map || !InLOS(c))
        {
            return;
        }

        base.AuraDamage();
    }

    public override void AuraEffect(Mobile m)
    {
        m.SendLocalizedMessage(1072073, false, Name); //  : The creature's aura of energy is damaging you!
        m.FixedParticles(0x374A, 10, 30, 5052, 1278, 0, EffectLayer.Waist);
        m.PlaySound(0x51D);
    }

    public virtual void SetBody()
    {
        switch (_specialty)
        {
            case SkillName.Archery:
            case SkillName.Spellweaving:
                Race = Race.Elf;
                break;
            default:
                Race = 0.75 > Utility.RandomDouble() ? Race.Human : Race.Elf;
                break;
        }

        HairItemID = Race.RandomHair(Female);
        HairHue = Race.RandomHairHue();

        FacialHairItemID = Race.RandomFacialHair(Female);
        FacialHairHue = Race.RandomHairHue();

        Hue = Race.RandomSkinHue();
    }

    public virtual void SetResists()
    {
        SetResistance(ResistanceType.Physical, MinResist, MaxResist);
        SetResistance(ResistanceType.Fire, MinResist, MaxResist);
        SetResistance(ResistanceType.Cold, MinResist, MaxResist);
        SetResistance(ResistanceType.Poison, MinResist, MaxResist);
        SetResistance(ResistanceType.Energy, MinResist, MaxResist);
    }

    public virtual void SetSkills()
    {
        SetSkill(SkillName.Fencing, MinSkill, MaxSkill);
        SetSkill(SkillName.Macing, MinSkill, MaxSkill);
        SetSkill(SkillName.MagicResist, MinSkill, MaxSkill);
        SetSkill(SkillName.Swords, MinSkill, MaxSkill);
        SetSkill(SkillName.Tactics, MinSkill, MaxSkill);
        SetSkill(SkillName.Wrestling, MinSkill, MaxSkill);
        SetSkill(SkillName.Archery, MinSkill, MaxSkill);
        SetSkill(SkillName.Parry, MinSkill, MaxSkill);

        if (SpellCaster)
        {
            SetSkill(SkillName.Magery, MinSkill, MaxSkill);
            SetSkill(SkillName.EvalInt, MinSkill, MaxSkill);
        }

        if (Skills[_specialty].Base < MinSkill)
        {
            SetSkill(_specialty, MinSkill, MaxSkill);
        }

        if (_sampire)
        {
            SetSkill(SkillName.Necromancy, MinSkill, MaxSkill);
        }

        if (_sampire || _specialty == SkillName.Necromancy)
        {
            SetSkill(SkillName.SpiritSpeak, MinSkill, MaxSkill);
        }

        if (_specialty == SkillName.Ninjitsu)
        {
            SetSkill(SkillName.Hiding, 100);
            SetSkill(SkillName.Stealth, 100);
        }
    }

    public virtual void EquipSpecialty()
    {
        if (CanHeal)
        {
            PackItem(new Bandage(Utility.RandomMinMax(10, 25)));
        }

        this.SetWearable(new ThighBoots());
        this.SetWearable(new BodySash(), Utility.RandomSlimeHue());

        switch (_specialty)
        {
            case SkillName.Chivalry:
                this.SetWearable(RandomSwordWeapon());
                PaladinEquip();
                break;
            case SkillName.Swords:
                this.SetWearable(RandomSwordWeapon());
                StandardMeleeEquip();
                break;
            case SkillName.Fencing:
                this.SetWearable(RandomFencingWeapon());
                StandardMeleeEquip();
                break;
            case SkillName.Macing:
                this.SetWearable(RandomMaceWeapon());
                StandardMeleeEquip();
                break;
            case SkillName.Archery:
                this.SetWearable(RandomArcheryWeapon());
                StandardMeleeEquip();
                break;
            case SkillName.Magery:
            case SkillName.Mysticism:
            case SkillName.Spellweaving:
            case SkillName.Necromancy:
                this.SetWearable(RandomMageWeapon());
                StandardMageEquip();
                break;
            case SkillName.Bushido:
                {
                    var w = RandomSamuraiWeapon() as BaseWeapon;
                    this.SetWearable(w);

                    this.SetWearable(new LeatherSuneate());
                    this.SetWearable(new LeatherJingasa());
                    this.SetWearable(new LeatherDo());
                    this.SetWearable(new LeatherHiroSode());
                    this.SetWearable(new SamuraiTabi(Utility.RandomNondyedHue()));

                    if (_sampire && w != null)
                    {
                        w.WeaponAttributes.HitLeechHits = 100;
                    }

                    SetSkill(SkillName.Parry, 120);
                    break;
                }
            case SkillName.Ninjitsu:
                {
                    this.SetWearable(RandomNinjaWeapon());

                    var belt = new LeatherNinjaBelt
                    {
                        UsesRemaining = 20,
                        Poison = Poison.Greater,
                        PoisonCharges = 20
                    };
                    this.SetWearable(belt);

                    for (var i = 0; i < 2; i++)
                    {
                        PackItem(new Fukiya
                        {
                            UsesRemaining = 10,
                            Poison = Poison.Greater,
                            PoisonCharges = 10,
                            Movable = false
                        });
                    }

                    this.SetWearable(new NinjaTabi());
                    this.SetWearable(new LeatherNinjaJacket());
                    this.SetWearable(new LeatherNinjaHood());
                    this.SetWearable(new LeatherNinjaPants());
                    this.SetWearable(new LeatherNinjaMitts());
                    break;
                }
            case SkillName.Poisoning:
                {
                    var wep = RandomAssassinWeapon() as BaseWeapon;

                    if (wep != null)
                    {
                        wep.Poison = Poison.Lethal;
                        wep.PoisonCharges = 100;
                        this.SetWearable(wep);
                    }

                    this.SetWearable(new LeatherChest());
                    this.SetWearable(new LeatherLegs());
                    this.SetWearable(new LeatherGloves());
                    this.SetWearable(new LeatherGorget());
                    break;
                }
        }
    }

    private void PaladinEquip()
    {
        this.SetWearable(Loot.Construct(typeof(Bascinet), typeof(Helmet), typeof(PlateHelm)), 1153);

        this.SetWearable(new PlateChest());
        this.SetWearable(new PlateLegs());
        this.SetWearable(new PlateGloves());
        this.SetWearable(new PlateGorget());
        this.SetWearable(new PlateArms());
        this.SetWearable(new MetalKiteShield());

        SetSkill(SkillName.Parry, 120);
    }

    private void StandardMeleeEquip()
    {
        this.SetWearable(Loot.Construct(typeof(Bascinet), typeof(Helmet), typeof(LeatherCap), typeof(RoyalCirclet)));

        this.SetWearable(new ChainChest());
        this.SetWearable(new ChainLegs());
        this.SetWearable(new RingmailGloves());
        this.SetWearable(new LeatherGorget());
    }

    private void StandardMageEquip()
    {
        var mage = _specialty == SkillName.Magery; // ServUO: AI == AI_Mage, which only Magery maps to there

        this.SetWearable(new WizardsHat(), mage ? Utility.RandomBlueHue() : Utility.RandomRedHue());
        this.SetWearable(new Robe(), mage ? Utility.RandomBlueHue() : Utility.RandomRedHue());
        this.SetWearable(new LeatherGloves());
    }

    public Item RandomSwordWeapon() =>
        Race == Race.Elf
            ? Loot.Construct(typeof(ElvenMachete), typeof(RadiantScimitar))
            : Loot.Construct(typeof(Broadsword), typeof(Longsword), typeof(Katana), typeof(Halberd), typeof(Bardiche), typeof(VikingSword));

    public Item RandomFencingWeapon() =>
        Race == Race.Elf
            ? Loot.Construct(typeof(Leafblade), typeof(WarCleaver), typeof(AssassinSpike))
            : Loot.Construct(typeof(Kryss), typeof(Spear), typeof(ShortSpear), typeof(Lance), typeof(Pike));

    public Item RandomMaceWeapon() =>
        Loot.Construct(typeof(Mace), typeof(WarHammer), typeof(WarAxe), typeof(BlackStaff), typeof(QuarterStaff), typeof(WarMace), typeof(DiamondMace), typeof(Scepter));

    public Item RandomArcheryWeapon() =>
        Race == Race.Elf
            ? Loot.Construct(typeof(MagicalShortbow), typeof(ElvenCompositeLongbow))
            : Loot.Construct(typeof(Bow), typeof(Crossbow), typeof(HeavyCrossbow), typeof(CompositeBow), typeof(RepeatingCrossbow));

    public Item RandomMageWeapon() =>
        Loot.Construct(typeof(Spellbook), typeof(GnarledStaff), typeof(BlackStaff), typeof(QuarterStaff), typeof(WildStaff));

    public Item RandomSamuraiWeapon() => Loot.Construct(typeof(Lajatang), typeof(Wakizashi), typeof(NoDachi));

    public Item RandomNinjaWeapon() =>
        Loot.Construct(typeof(Wakizashi), typeof(Tessen), typeof(Nunchaku), typeof(Daisho), typeof(Sai), typeof(Tekagi), typeof(Kama), typeof(Katana));

    public Item RandomAssassinWeapon() => Loot.Construct(typeof(Cleaver), typeof(ButcherKnife), typeof(Kryss), typeof(Dagger));

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1154196 + (int)_invasionType); // Dragon Invasion .. Nature Invasion
    }

    public override void OnThink()
    {
        base.OnThink();

        if (Combatant == null)
        {
            return;
        }

        if (CanDoSpecial && InRange(Combatant, 4) && 0.1 > Utility.RandomDouble() && _nextSpecial < Core.Now)
        {
            DoSpecial();

            _nextSpecial = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
        }
        else if (_sampire)
        {
            if (0.1 > Utility.RandomDouble() && Weapon is BaseWeapon { Cursed: false })
            {
                new CurseWeaponSpell(this).Cast();
            }
            else if (!TransformationSpellHelper.UnderTransformation(this, typeof(VampiricEmbraceSpell)))
            {
                new VampiricEmbraceSpell(this).Cast();
            }
        }
    }

    public void DoSpecial()
    {
        if (Map == null || Map == Map.Internal)
        {
            return;
        }

        var map = Map;
        var loc = Location;

        for (var i = 0; i < 4; i++)
        {
            var radius = i;

            Timer.DelayCall(
                TimeSpan.FromMilliseconds(i * 50),
                () => Geometry.Circle2D(
                    loc,
                    map,
                    radius,
                    (pnt, m) => Effects.SendLocationEffect(pnt, m, Utility.RandomBool() ? 14000 : 14013, 14, 20, 2018, 0)
                )
            );
        }

        Timer.DelayCall(
            TimeSpan.FromMilliseconds(200),
            () =>
            {
                if (Deleted || Map == null || Map == Map.Internal)
                {
                    return;
                }

                var list = new List<Mobile>();

                foreach (var mob in Map.GetMobilesInRange(Location, 4))
                {
                    if (mob.AccessLevel > AccessLevel.Player)
                    {
                        continue;
                    }

                    if ((mob is PlayerMobile || mob is BaseCreature bc && bc.GetMaster() is PlayerMobile) && CanBeHarmful(mob))
                    {
                        list.Add(mob);
                    }
                }

                foreach (var mob in list)
                {
                    AOS.Damage(mob, this, Utility.RandomMinMax(80, 90), 0, 0, 0, 0, 0, 100);
                }
            }
        );
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _nextSpecial = Core.Now;
    }
}

[SerializationGenerator(0, false)]
public partial class InvaderCaptain : Invader
{
    [Constructible]
    public InvaderCaptain(InvasionType type) : base(type)
    {
        SetStr(250);
        SetDex(SpellCaster ? 150 : 200);
        SetInt(SpellCaster ? 1000 : 5000);

        SetHits(8000, 12000);

        if (IsFighter(Specialty))
        {
            SetDamage(22, 30);
        }
        else if (!SpellCaster)
        {
            SetDamage(20, 28);
        }
        else
        {
            SetDamage(10, 20);
        }

        Fame = 48000;
        Karma = -48000;
    }

    public override double MinSkill => 105.0;
    public override double MaxSkill => 130.0;
    public override int MinResist => 20;
    public override int MaxResist => 30;

    public override void OnDeath(Container c)
    {
        var rights = GetLootingRights(DamageEntries, HitsMax);
        rights.Sort();

        base.OnDeath(c);

        var list = new List<Mobile>();

        foreach (var ds in rights)
        {
            if (ds.m_Mobile != null && ds.m_Mobile.InRange(c.Location, 20))
            {
                list.Add(ds.m_Mobile);
            }
        }

        if (list.Count == 0)
        {
            return;
        }

        for (var i = 0; i < 2; i++)
        {
            var item = MinaxArtifacts.CreateForInvasion(list[0]);

            if (item == null)
            {
                continue;
            }

            var drop = list.Count == 1 || i >= list.Count ? list[0] : list[i];

            drop.SendLocalizedMessage(1154530); // You notice the crest of Minax on your fallen foe's equipment and decide it may be of some value...

            if (drop.Backpack?.TryDropItem(drop, item, false) != true)
            {
                drop.BankBox.DropItem(item);
                drop.SendLocalizedMessage(1079730); // The item has been placed into your bank box.
            }
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
        AddLoot(LootPack.SuperBoss, 1);
    }
}
