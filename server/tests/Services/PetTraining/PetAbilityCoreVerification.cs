// cc-P121: Pet Training part 1, the ability core (customizations/Services/Pet Training/, five upstream patches). Chase's
// decisions of 2026-10-15: 1a, one ability set (the twins run through pinned's classes and a stock creature's profile
// lists what its stock abilities give it); 2a, profiles in a side table with their own save file.
//
// What these facts pin:
//  - the tables: ServUO's 29 special abilities and 7 area effects, in ServUO's order (not the 30 the brief states);
//  - each of the 29 and the 7 does its effect on a sample creature (one fact per ability, effect called directly);
//  - each trigger the patches add reaches the profile: a hit given and taken (AOS.Damage), spell damage told apart from
//    melee (SpellHelper's mark), a think (BaseCreature.OnThink, special and area), an approach (OnMovement), the trained
//    hit poison (OnGaveMeleeAttack), the debuff penalties (AosAttributes.GetValue), Searing Wounds on a heal
//    (SpellHelper.Heal), SmartAI (MageAI) and the regeneration bonus (RegenRates, ours);
//  - the twins once: a stock creature lists its natural twin once and the profile never dispatches it;
//  - storage: a round trip through the store's own Serialize/Deserialize, through stabling and transfer, a deleted
//    creature's entry gone, a natural-only profile not written, and a save folder without the file loading nothing;
//  - evo pets and pack mules get no stored profile.
//
// Test-host facts this file meets (notes/port-recipe.md, "Writing a shard test fact"): hit points regenerate one per 8 ms
// wheel tick, so damage over time is read as the lowest value while advancing in 8 ms steps (LowestHits); everything with
// a timer is made after ShardTestClock.Arm(). Each fact deletes what it made.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Spells;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PetAbilityCoreVerification
{
    private readonly ITestOutputHelper _out;

    public PetAbilityCoreVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Origin = new(1100, 1100, 0); // Ter Mur is 1280 wide

    private static Point3D At(int dx, int dy) => new(Origin.X + dx, Origin.Y + dy, Origin.Z);

    private static PlayerMobile NewPlayer(Point3D loc)
    {
        // Not Young: a young player is immune to poison (PlayerMobile.CheckPoisonImmunity), and a new one is young here.
        var pm = new PlayerMobile { Player = true, Young = false, RawStr = 100, RawDex = 100, RawInt = 100 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, Map.TerMur);
        pm.Hits = pm.HitsMax;
        pm.Mana = pm.ManaMax;
        pm.Stam = pm.StamMax;
        return pm;
    }

    private static T Place<T>(T bc, Point3D loc) where T : BaseCreature
    {
        bc.SetMana(500);
        bc.Mana = 500;
        bc.MoveToWorld(loc, Map.TerMur);
        return bc;
    }

    private static GrizzlyBear NewBear(Point3D loc) => Place(new GrizzlyBear(), loc);

    // A defender with resistances, for the abilities that lower one.
    private static GrizzlyBear NewArmouredBear(Point3D loc)
    {
        var bear = NewBear(loc);
        bear.SetResistance(ResistanceType.Physical, 40);
        bear.SetResistance(ResistanceType.Fire, 40);
        bear.SetResistance(ResistanceType.Cold, 40);
        bear.SetResistance(ResistanceType.Poison, 40);
        bear.SetResistance(ResistanceType.Energy, 40);
        return bear;
    }

    // The lowest hit points seen while the clock advances in 8 ms steps (the host regenerates one hit per step).
    private static int LowestHits(Mobile m, double seconds)
    {
        var low = m.Alive ? m.Hits : 0;
        var steps = (int)Math.Ceiling(seconds * 1000 / 8);

        for (var i = 0; i < steps; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(8));
            low = Math.Min(low, m.Alive ? m.Hits : 0);
        }

        return low;
    }

    private static void DeleteAll(params Mobile[] mobiles)
    {
        foreach (var m in mobiles)
        {
            m?.Delete();
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The tables
    // ---------------------------------------------------------------------------------------------------------------

    // ServUO SpecialAbility.cs:236-268, in its order.
    public static readonly string[] SpecialNames =
    {
        "AngryFire", "ConductiveBlast", "DragonBreath", "GraspingClaw", "Inferno", "LightningForce", "ManaDrain",
        "RagingBreath", "Repel", "SearingWounds", "StealLife", "VenomousBite", "ViciousBite", "RuneCorruption",
        "LifeLeech", "StickySkin", "TailSwipe", "FlurryForce", "Rage", "Heal", "HowlOfCacophony", "Webbing", "Anemia",
        "BloodDisease", "PoisonSpit", "TrueFear", "ColossalBlow", "LifeDrain", "ColossalRage"
    };

    // ServUO AreaEffects.cs:166-174, in its order.
    public static readonly string[] AreaNames =
    {
        "AuraOfEnergy", "AuraOfNausea", "EssenceOfDisease", "EssenceOfEarth", "ExplosiveGoo", "AuraDamage",
        "PoisonBreath"
    };

    public static IEnumerable<object[]> Specials => SpecialNames.Select(n => new object[] { n });
    public static IEnumerable<object[]> Areas => AreaNames.Select(n => new object[] { n });

    [Fact]
    public void TheTablesHoldServUOs29SpecialAbilitiesAnd7AreaEffectsInItsOrder()
    {
        Assert.Equal(29, SpecialAbility.Abilities.Length);
        Assert.Equal(SpecialNames, SpecialAbility.Abilities.Select(a => a.Name));
        Assert.Equal(7, AreaEffect.Effects.Length);
        Assert.Equal(AreaNames, AreaEffect.Effects.Select(a => a.Name));

        foreach (var name in SpecialNames)
        {
            Assert.Same(SpecialAbility.Abilities[Array.IndexOf(SpecialNames, name)], SpecialAbility.GetByName(name));
        }

        foreach (var name in AreaNames)
        {
            Assert.Same(AreaEffect.Effects[Array.IndexOf(AreaNames, name)], AreaEffect.GetByName(name));
        }

        // The seven twins of decision 1a, plus Heal, run through pinned.
        var twins = SpecialAbility.Abilities.OfType<PinnedTwinAbility>().Select(a => a.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "ColossalBlow", "DragonBreath", "GraspingClaw", "Heal", "LifeLeech", "RuneCorruption", "TailSwipe" },
            twins
        );
        Assert.IsType<AuraDamageTwin>(AreaEffect.AuraDamage);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Each special ability's effect
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Specials))]
    public void EachSpecialAbilityDoesItsEffect(string name)
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();

        var ability = SpecialAbility.GetByName(name);
        Assert.NotNull(ability);

        var made = new List<Mobile>();
        T Keep<T>(T m) where T : Mobile
        {
            made.Add(m);
            return m;
        }

        try
        {
            var creature = Keep(NewBear(At(0, 0)));
            var player = Keep(NewPlayer(At(1, 0)));
            var damage = 20;

            switch (name)
            {
                case "AngryFire":
                case "LightningForce":
                case "Inferno":
                    {
                        var before = player.Hits;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(player.Hits < before, $"{name}: no damage");

                        if (name == "Inferno")
                        {
                            Assert.NotNull(player.GetResistanceMod("PetInferno"));
                        }

                        break;
                    }
                case "ConductiveBlast":
                    {
                        var target = Keep(NewArmouredBear(At(1, 1)));
                        Assert.Equal(40, target.EnergyResistance);
                        ability.DoEffects(creature, target, ref damage);
                        Assert.Equal(20, target.EnergyResistance);
                        break;
                    }
                case "FlurryForce":
                case "GraspingClaw":
                    {
                        var target = Keep(NewArmouredBear(At(1, 1)));
                        ability.DoEffects(creature, target, ref damage);
                        Assert.Equal(40 - 40 * 15 / 100, target.PhysicalResistance);
                        break;
                    }
                case "RuneCorruption":
                    {
                        var target = Keep(NewArmouredBear(At(1, 1)));
                        ability.DoEffects(creature, target, ref damage);
                        Assert.NotNull(target.GetResistanceMod("RuneCorruption")); // pinned's class, by its own name
                        Assert.True(target.FireResistance < 40);
                        break;
                    }
                case "DragonBreath":
                    {
                        // The bear has no stock breath, so the twin uses ServUO's default definition: pinned FireBreath.
                        Assert.IsType<FireBreath>(((PinnedTwinAbility)ability).GetPinned(creature));
                        var before = player.Hits;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(LowestHits(player, 3.0) < before, "no breath landed");
                        break;
                    }
                case "ManaDrain":
                    {
                        var before = player.Mana;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(player.Mana <= before - 40, $"mana {before} -> {player.Mana}");
                        break;
                    }
                case "RagingBreath":
                    {
                        var before = player.Hits;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(RagingBreath.IsUnderEffects(player));
                        Assert.True(LowestHits(player, 1.2) < before, "no burn");
                        break;
                    }
                case "Repel":
                    {
                        var before = player.Hits;
                        damage = 40;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.Equal(0, damage); // the hit is cancelled
                        Assert.Equal(before - 20, player.Hits); // half of it, direct, back at the attacker
                        break;
                    }
                case "SearingWounds":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(SearingWounds.IsUnderEffects(player));

                        // SpellHelper.Heal (SpellHelper-pet-ability-hooks.patch): a player's heal on another halves.
                        var healer = Keep(NewPlayer(At(2, 0)));
                        player.Hits = 10;
                        SpellHelper.Heal(40, player, healer, false);
                        Assert.Equal(30, player.Hits);
                        break;
                    }
                case "StealLife":
                    {
                        var before = player.Hits;
                        damage = 10;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.Equal(before - 10, player.Hits); // the hit again, as energy; the player has no resist
                        break;
                    }
                case "VenomousBite":
                    {
                        var spider = Keep(Place(new GiantSpider(), At(0, 1))); // HitPoison Regular
                        ability.DoEffects(spider, player, ref damage);
                        Assert.True(player.Poisoned);
                        break;
                    }
                case "ViciousBite":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(ViciousBite.IsUnderEffects(player));
                        break;
                    }
                case "LifeLeech":
                    {
                        var before = player.Mana;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.Equal(before - 15, player.Mana); // pinned DrainLifeAttack's first drain
                        break;
                    }
                case "StickySkin":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(StickySkin.IsUnderEffects(player));
                        Assert.Equal(-30, AosAttributes.GetValue(player, AosAttribute.WeaponSpeed)); // AOS patch
                        break;
                    }
                case "TailSwipe":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(
                            player.Paralyzed || player.GetStatMod("[TailSwipe] Dex") != null,
                            "neither stunned nor confused"
                        );
                        break;
                    }
                case "Rage":
                    {
                        var before = player.Hits;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(Rage.IsUnderEffects(player));
                        Assert.True(LowestHits(player, 1.2) < before, "no rage damage");
                        break;
                    }
                case "Heal":
                    {
                        creature.Hits = creature.HitsMax / 4;
                        ability.DoEffects(creature, creature, ref damage);
                        Assert.True(creature.IsHealing); // pinned's HealStart
                        break;
                    }
                case "HowlOfCacophony":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(HowlOfCacophony.IsUnderEffects(player));
                        Assert.Equal(-60, AosAttributes.GetValue(player, AosAttribute.WeaponSpeed));
                        Assert.Equal(-5, AosAttributes.GetValue(player, AosAttribute.CastSpeed));
                        Assert.Equal(-5, AosAttributes.GetValue(player, AosAttribute.CastRecovery));
                        break;
                    }
                case "Webbing":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(player.Frozen);
                        Assert.True(SpiderWebbing.IsTrapped(player));
                        ShardTestClock.AdvanceSeconds(11); // the web falls apart after ten seconds
                        Assert.False(SpiderWebbing.IsTrapped(player));
                        Assert.False(player.Frozen);
                        break;
                    }
                case "Anemia":
                    {
                        creature.Hits = 1;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(creature.Hits >= 51, $"drank {creature.Hits - 1}"); // 50-70
                        break;
                    }
                case "BloodDisease":
                    {
                        // A 25% infection per touch.
                        for (var i = 0; i < 200 && !BloodDisease.IsUnderEffects(player); i++)
                        {
                            ability.DoEffects(creature, player, ref damage);
                        }

                        Assert.True(BloodDisease.IsUnderEffects(player));
                        break;
                    }
                case "PoisonSpit":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        ShardTestClock.AdvanceSeconds(1.1);
                        Assert.True(player.Poisoned);
                        break;
                    }
                case "TrueFear":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(player.Frozen);
                        break;
                    }
                case "ColossalBlow":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(player.Frozen); // pinned ColossalBlow (StunAttack)
                        break;
                    }
                case "LifeDrain":
                    {
                        var before = player.Hits;
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(player.Hits < before, "no drain");
                        break;
                    }
                case "ColossalRage":
                    {
                        ability.DoEffects(creature, player, ref damage);
                        Assert.True(ColossalRage.HasRage(creature));
                        Assert.Equal(1157, creature.HueMod);
                        ShardTestClock.AdvanceSeconds(10.5);
                        Assert.False(ColossalRage.HasRage(creature));
                        break;
                    }
                default:
                    Assert.Fail($"no fact for {name}");
                    break;
            }
        }
        finally
        {
            DeleteAll(made.ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void EachAreaEffectDoesItsEffect(string name)
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();

        var effect = AreaEffect.GetByName(name);
        Assert.NotNull(effect);

        var creature = NewBear(At(0, 0));
        var player = NewPlayer(At(1, 0));

        try
        {
            var before = player.Hits;

            switch (name)
            {
                case "AuraOfEnergy":
                case "EssenceOfDisease":
                case "EssenceOfEarth":
                case "AuraDamage":
                    effect.DoEffects(creature, player);
                    Assert.True(player.Hits < before, $"{name}: no damage");
                    break;
                case "AuraOfNausea":
                    effect.DoEffects(creature, player);
                    Assert.True(AuraOfNausea.UnderNausea(player));
                    Assert.Equal(-60, AosAttributes.GetValue(player, AosAttribute.AttackChance));
                    Assert.Equal(-60, AosAttributes.GetValue(player, AosAttribute.DefendChance));
                    Assert.Equal(-60, AosAttributes.GetValue(player, AosAttribute.WeaponSpeed));
                    break;
                case "ExplosiveGoo":
                    effect.DoEffects(creature, player);
                    Assert.True(LowestHits(player, 2.2) < before, "the goo never burst");
                    break;
                case "PoisonBreath":
                    effect.DoEffects(creature, player);
                    Assert.True(player.Poisoned);
                    break;
                default:
                    Assert.Fail($"no fact for {name}");
                    break;
            }
        }
        finally
        {
            DeleteAll(creature, player);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The triggers the patches add
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AHitGivenFiresADoMeleeAbilityThroughAOSDamage()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var bear = NewBear(At(0, 0));
        var player = NewPlayer(At(1, 0));

        try
        {
            bear.SetSpecialAbility(SpecialAbility.AngryFire);

            var tries = 0;
            for (; tries < 500 && !SpecialAbility.AngryFire.IsInCooldown(bear); tries++)
            {
                player.Hits = player.HitsMax;
                bear.Mana = 500;
                AOS.Damage(player, bear, 1, 100, 0, 0, 0, 0);
            }

            _out.WriteLine($"Angry Fire fired after {tries} hits (10% each)");
            Assert.True(SpecialAbility.AngryFire.IsInCooldown(bear));
        }
        finally
        {
            DeleteAll(bear, player);
        }
    }

    [Fact]
    public void AMeleeAbilityInCooldownIsNotPicked()
    {
        // ServUO's precedence slip let melee abilities ignore their cooldown (SpecialAbility.cs:48-50); fixed, D-4 rule.
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var bear = NewBear(At(0, 0));
        var player = NewPlayer(At(1, 0));

        try
        {
            bear.SetSpecialAbility(SpecialAbility.AngryFire);
            SpecialAbility.AngryFire.AddToCooldown(bear);

            for (var i = 0; i < 300; i++)
            {
                player.Hits = player.HitsMax;
                bear.Mana = 500;
                AOS.Damage(player, bear, 1, 100, 0, 0, 0, 0);
                Assert.Equal(500, bear.Mana); // Angry Fire costs 30; it never fired
            }
        }
        finally
        {
            DeleteAll(bear, player);
        }
    }

    [Fact]
    public void SpellDamageIsToldApartFromMelee()
    {
        // ManaDrain triggers on melee given or taken, never on spell damage. Without SpellHelper's mark every AOS.Damage
        // reads as melee (pinned's has no damage type), and the spell loop below would drain.
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var bear = NewBear(At(0, 0));
        var caster = NewPlayer(At(1, 0));

        try
        {
            bear.SetSpecialAbility(SpecialAbility.ManaDrain);

            for (var i = 0; i < 300; i++)
            {
                bear.Hits = bear.HitsMax;
                bear.Mana = 100;
                SpellHelper.Damage(TimeSpan.Zero, bear, caster, 1, 0, 100, 0, 0, 0);
            }

            Assert.False(SpecialAbility.ManaDrain.IsInCooldown(bear), "Mana Drain fired on spell damage");

            var tries = 0;
            for (; tries < 500 && !SpecialAbility.ManaDrain.IsInCooldown(bear); tries++)
            {
                bear.Hits = bear.HitsMax;
                bear.Mana = 100;
                AOS.Damage(bear, caster, 1, 100, 0, 0, 0, 0); // melee taken
            }

            _out.WriteLine($"Mana Drain fired after {tries} melee hits taken");
            Assert.True(SpecialAbility.ManaDrain.IsInCooldown(bear));
        }
        finally
        {
            DeleteAll(bear, caster);
        }
    }

    [Fact]
    public void SpellDamageTakenFiresRepelAndCancelsTheHit()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var bear = NewBear(At(0, 0));
        var caster = NewPlayer(At(1, 0));

        try
        {
            bear.SetSpecialAbility(SpecialAbility.Repel);

            var tries = 0;
            for (; tries < 500 && !SpecialAbility.Repel.IsInCooldown(bear); tries++)
            {
                bear.Hits = bear.HitsMax;
                caster.Hits = caster.HitsMax;
                bear.Mana = 500;
                SpellHelper.Damage(TimeSpan.Zero, bear, caster, 20, 0, 100, 0, 0, 0);
            }

            _out.WriteLine($"Repel fired after {tries} spells");
            Assert.True(SpecialAbility.Repel.IsInCooldown(bear));
            Assert.Equal(bear.HitsMax, bear.Hits); // the repelled spell did nothing to the bear
            Assert.True(caster.Hits < caster.HitsMax); // and half of it came back
        }
        finally
        {
            DeleteAll(bear, caster);
        }
    }

    [Fact]
    public void AThinkFiresAnAreaEffectAndASpecialAbility()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var bear = NewBear(At(0, 0));
        var player = NewPlayer(At(1, 0));

        try
        {
            // Area effect: chance 1, no mana needed by a wild creature, combatant within 3.
            bear.SetAreaEffect(AreaEffect.AuraOfEnergy);
            bear.Combatant = player;
            var before = player.Hits;
            bear.OnThink();
            Assert.True(player.Hits < before, "no aura on think");
            Assert.True(AreaEffect.AuraOfEnergy.IsInCooldown(bear));

            // Special ability: Heal, chance 1, on a creature that is not CanHeal by nature.
            Assert.False(bear.CanHeal);
            bear.SetSpecialAbility(SpecialAbility.Heal);
            bear.Hits = bear.HitsMax / 4;
            bear.OnThink();
            Assert.True(bear.IsHealing);
        }
        finally
        {
            DeleteAll(bear, player);
        }
    }

    [Fact]
    public void AnApproachFiresTrueFear()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var bear = NewBear(At(0, 0));
        var player = NewPlayer(At(5, 0));

        try
        {
            bear.SetSpecialAbility(SpecialAbility.TrueFear);

            var tries = 0;
            for (; tries < 500 && !player.Frozen; tries++)
            {
                bear.OnMovement(player, At(12, 0)); // came from outside 8 tiles to inside
            }

            _out.WriteLine($"True Fear fired after {tries} approaches");
            Assert.True(player.Frozen);

            // Moving about inside the range is not an approach.
            player.Frozen = false;
            SpecialAbility.TrueFear.RemoveFromCooldown(bear);

            for (var i = 0; i < 300; i++)
            {
                bear.OnMovement(player, At(4, 0));
            }

            Assert.False(player.Frozen);
        }
        finally
        {
            DeleteAll(bear, player);
        }
    }

    [Fact]
    public void ATrainedPoisonerPoisonsByItsSkill()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var owner = NewPlayer(At(0, 2));
        var spider = Place(new GiantSpider(), At(0, 0)); // HitPoison Regular, Poisoning 60-80
        var victim = NewPlayer(At(1, 0));

        try
        {
            // Wild: its own poison and chance (ServUO GetHitPoison returns HitPoison unless controlled).
            Assert.NotNull(Poison.Regular);
            Assert.Same(Poison.Regular, PetTrainingHooks.GetHitPoison(spider));

            spider.SetControlMaster(owner);
            Assert.True(spider.Controlled);

            // Tamed, with Poisoning by nature (it has the skill): the poison rises with the skill.
            Assert.True(AbilityProfile.HasMagicalAbility(spider, MagicalAbility.Poisoning));
            spider.Skills.Poisoning.Base = 100;
            Assert.Same(Poison.Lethal, PetTrainingHooks.GetHitPoison(spider));

            // Through OnGaveMeleeAttack (BaseCreature-pet-ability-hooks.patch): skill/300 a hit.
            var tries = 0;
            for (; tries < 500 && !victim.Poisoned; tries++)
            {
                spider.OnGaveMeleeAttack(victim, 0);
            }

            _out.WriteLine($"poisoned after {tries} hits, with {victim.Poison?.Name}");
            Assert.True(victim.Poisoned);
            Assert.Same(Poison.Lethal, victim.Poison);
        }
        finally
        {
            DeleteAll(spider, victim, owner);
        }
    }

    [Fact]
    public void MageAIIsSmartWithTheMageryMasterySchool()
    {
        var lich = new Lich();

        try
        {
            var ai = Assert.IsType<MageAI>(lich.AIObject);
            Assert.False(ai.SmartAI);

            lich.SetMagicalAbility(MagicalAbility.MageryMastery);
            Assert.True(ai.SmartAI);
        }
        finally
        {
            lich.Delete();
        }
    }

    [Fact]
    public void TheProfilesRegenerationBonusReachesRegenRates()
    {
        // RegenRates is ours (a replacement); its handlers are private and not installed in the test host (installing
        // them would change every other fact's regeneration, D15), so they are called by reflection.
        var hitsRate = typeof(RegenRates).GetMethod("Mobile_HitsRegenRate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(hitsRate);

        var bear = new GrizzlyBear();

        try
        {
            var plain = (TimeSpan)hitsRate.Invoke(null, new object[] { bear });
            PetTrainingHelper.GetAbilityProfile(bear, true).RegenHits = 6;
            var trained = (TimeSpan)hitsRate.Invoke(null, new object[] { bear });

            _out.WriteLine($"hits regen interval {plain} -> {trained}");
            Assert.Equal(TimeSpan.FromSeconds(10.0 / (1 + 4)), plain); // the creature's +4
            Assert.Equal(TimeSpan.FromSeconds(10.0 / (1 + 4 + 6)), trained);
        }
        finally
        {
            bear.Delete();
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The twins, once (decision 1a)
    // ---------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> NaturalTwins =>
        new[]
        {
            new object[] { typeof(Dragon), "DragonBreath" },
            new object[] { typeof(Hiryu), "GraspingClaw" },
            new object[] { typeof(StoneSlith), "TailSwipe" },
            new object[] { typeof(RuneBeetle), "RuneCorruption" },
            new object[] { typeof(Juggernaut), "ColossalBlow" },
            new object[] { typeof(Kappa), "LifeLeech" },
            new object[] { typeof(CuSidhe), "Heal" }
        };

    [Theory]
    [MemberData(nameof(NaturalTwins))]
    public void AStockCreatureListsItsNaturalTwinOnceAndNeverDispatchesIt(Type type, string twinName)
    {
        var bc = (BaseCreature)Activator.CreateInstance(type);
        var twin = (PinnedTwinAbility)SpecialAbility.GetByName(twinName);

        try
        {
            var profile = PetTrainingHelper.GetAbilityProfile(bc, true);

            Assert.True(twin.IsNatural(bc));
            Assert.Single(profile.SpecialAbilities, a => a == twin);
            Assert.DoesNotContain(twin, profile.EnumerateDispatchableSpecialAbilities());

            // Setting it again (a ported constructor, or training) does not store a second copy.
            bc.SetSpecialAbility(twin);
            Assert.Single(profile.SpecialAbilities, a => a == twin);
            Assert.DoesNotContain(twin, profile.EnumerateDispatchableSpecialAbilities());
            Assert.True(profile.IsEmpty, "a natural ability was stored");
        }
        finally
        {
            bc.Delete();
        }
    }

    [Fact]
    public void ATamedDragonBreathesThroughPinnedOnly()
    {
        var owner = NewPlayer(At(0, 3));
        var dragon = new Dragon();

        try
        {
            dragon.SetControlMaster(owner);
            var profile = PetTrainingHelper.GetAbilityProfile(dragon, true);

            // One breath in the profile, the stock one; the stock pipeline holds exactly one FireBreath.
            Assert.Equal(new[] { SpecialAbility.DragonBreath }, profile.SpecialAbilities);
            Assert.Single(dragon.GetMonsterAbilities(), a => a is FireBreath);
            Assert.Empty(profile.EnumerateDispatchableSpecialAbilities());

            // It counts toward the three-ability limit training will apply, as in ServUO (DragonBreath is not a
            // NaturalAbility there), with Magery from its AI.
            Assert.Equal(2, profile.AbilityCount());
            Assert.True(profile.HasAbility(MagicalAbility.Magery));
        }
        finally
        {
            DeleteAll(dragon, owner);
        }
    }

    [Fact]
    public void AStockAuraIsTheNaturalAuraDamage()
    {
        var pyre = new Pyre();

        try
        {
            Assert.True(pyre.HasAura);
            var profile = PetTrainingHelper.GetAbilityProfile(pyre, true);
            Assert.Single(profile.AreaEffects, a => a == AreaEffect.AuraDamage);
            Assert.Empty(profile.EnumerateDispatchableAreaEffects());
        }
        finally
        {
            pyre.Delete();
        }
    }

    [Fact]
    public void AStockCreaturesProfileListsItsNaturalAbilities()
    {
        var dragon = new Dragon();
        var spider = new GiantSpider();

        try
        {
            var dp = PetTrainingHelper.GetAbilityProfile(dragon, true);
            _out.WriteLine($"Dragon: {dp.AbilityNames}");
            Assert.Equal(MagicalAbility.Magery, dp.MagicalAbility); // AI_Mage, ServUO InitializeAbilities
            Assert.Equal(new[] { SpecialAbility.DragonBreath }, dp.SpecialAbilities);

            var sp = PetTrainingHelper.GetAbilityProfile(spider, true);
            _out.WriteLine($"GiantSpider: {sp.AbilityNames}");
            Assert.Equal(MagicalAbility.Poisoning, sp.MagicalAbility); // Poisoning skill, ServUO SetSkill
            Assert.Empty(sp.SpecialAbilities);
        }
        finally
        {
            DeleteAll(dragon, spider);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Storage (decision 2a)
    // ---------------------------------------------------------------------------------------------------------------

    private static byte[] SaveStore()
    {
        AbilityProfileStore.Configure();
        var buffer = new byte[1 << 20];
        var writer = new BufferWriter(buffer, true);
        AbilityProfileStore.Instance.Serialize(writer);
        writer.Flush();
        return buffer;
    }

    private static void LoadStore(byte[] buffer) => AbilityProfileStore.Instance.Deserialize(new BufferReader(buffer));

    private static void Train(BaseCreature bc)
    {
        bc.SetSpecialAbility(SpecialAbility.AngryFire);
        bc.SetAreaEffect(AreaEffect.ExplosiveGoo);
        bc.SetMagicalAbility(MagicalAbility.Chivalry);
        var p = PetTrainingHelper.GetAbilityProfile(bc);
        p.RegenHits = 3;
        p.RegenMana = 2;
        p.TokunoTame = true;
    }

    private static void AssertTrained(BaseCreature bc)
    {
        Assert.True(AbilityProfileStore.TryGet(bc, out var p), "the profile is gone");
        Assert.Equal(new[] { SpecialAbility.AngryFire }, p.SpecialAbilities);
        Assert.Equal(new[] { AreaEffect.ExplosiveGoo }, p.AreaEffects);
        Assert.True(p.HasAbility(MagicalAbility.Chivalry));
        Assert.Equal(3, p.RegenHits);
        Assert.Equal(2, p.RegenMana);
        Assert.True(p.TokunoTame);
        Assert.Same(bc, p.Creature);
    }

    [Fact]
    public void AProfileSurvivesSaveAndLoad()
    {
        var bear = new GrizzlyBear();

        try
        {
            Train(bear);
            var buffer = SaveStore();

            // Load replaces the table with what was saved; the restored profile is a new object.
            Assert.True(AbilityProfileStore.TryGet(bear, out var before));
            LoadStore(buffer);
            Assert.True(AbilityProfileStore.TryGet(bear, out var after));
            Assert.NotSame(before, after);
            AssertTrained(bear);
        }
        finally
        {
            bear.Delete();
        }
    }

    [Fact]
    public void AProfileSurvivesStablingAndTransfer()
    {
        var owner = NewPlayer(At(0, 4));
        var buyer = NewPlayer(At(1, 4));
        var bear = NewBear(At(0, 5));

        try
        {
            bear.SetControlMaster(owner);
            Train(bear);
            var trained = bear.AbilityProfile;

            // Stabled as pinned AnimalTrainer.Stable does it (AnimalTrainer.cs:252-264), and saved and loaded there.
            bear.ControlTarget = null;
            bear.ControlOrder = OrderType.Stay;
            bear.Internalize();
            bear.SetControlMaster(null);
            bear.IsStabled = true;
            bear.StabledBy = owner;
            owner.AddStabled(bear);

            Assert.Same(trained, bear.AbilityProfile);
            LoadStore(SaveStore());
            AssertTrained(bear);

            // Claimed, then given to someone else.
            owner.RemoveStabled(bear);
            bear.IsStabled = false;
            bear.SetControlMaster(owner);
            bear.MoveToWorld(At(0, 5), Map.TerMur);
            AssertTrained(bear);

            bear.SetControlMaster(buyer);
            AssertTrained(bear);
            LoadStore(SaveStore());
            AssertTrained(bear);
        }
        finally
        {
            DeleteAll(bear, owner, buyer);
        }
    }

    [Fact]
    public void ADeletedCreaturesEntryIsGone()
    {
        var bear = new GrizzlyBear();
        Train(bear);
        Assert.True(AbilityProfileStore.TryGet(bear, out _));

        bear.Delete();
        Assert.False(AbilityProfileStore.TryGet(bear, out _));

        LoadStore(SaveStore());
        Assert.False(AbilityProfileStore.TryGet(bear, out _));
    }

    [Fact]
    public void ANaturalOnlyProfileIsNotWritten()
    {
        var dragon = new Dragon();

        try
        {
            Assert.True(PetTrainingHelper.GetAbilityProfile(dragon, true).IsEmpty);
            LoadStore(SaveStore());
            Assert.False(AbilityProfileStore.TryGet(dragon, out _));

            // And still answers from its nature when asked again.
            Assert.Equal(new[] { SpecialAbility.DragonBreath }, PetTrainingHelper.GetAbilityProfile(dragon, true).SpecialAbilities);
        }
        finally
        {
            dragon.Delete();
        }
    }

    [Fact]
    public void ASaveFolderWithoutTheFileLoadsNothingAndKeepsGoing()
    {
        // A world saved before cc-P121 has no PetAbilityProfiles/PetAbilityProfiles.bin; pinned's GenericPersistence
        // returns without reading (GenericPersistence.cs:80-88). Proved here through the real file path.
        AbilityProfileStore.Configure();
        var bear = new GrizzlyBear();
        var dir = Path.Combine(Path.GetTempPath(), $"cc-p121-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            Train(bear);
            AbilityProfileStore.Instance.Deserialize(dir, new Dictionary<ulong, string>());
            AssertTrained(bear); // nothing was read, nothing was cleared
        }
        finally
        {
            bear.Delete();
            Directory.Delete(dir, true);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Evo pets and pack mules (decision 4: excluded; no profile changes)
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EvoPetsAndPackMulesGetNoStoredProfile()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsurePoisons();
        var owner = NewPlayer(At(0, 6));
        var evo = Place(new EvoPet("dog"), At(1, 6));
        var mule = Place(new PackMule(), At(2, 6));
        var target = NewPlayer(At(1, 7));

        try
        {
            evo.SetControlMaster(owner);
            mule.SetControlMaster(owner);

            for (var i = 0; i < 50; i++)
            {
                target.Hits = target.HitsMax;
                evo.Hits = evo.HitsMax; // a dead evo pet is deleted, and a deleted creature has no profile to look at
                AOS.Damage(target, evo, 1, 100, 0, 0, 0, 0);
                AOS.Damage(evo, target, 1, 100, 0, 0, 0, 0);
                evo.OnThink();
                mule.OnThink();
            }

            Assert.False(AbilityProfileStore.TryGet(evo, out _));
            Assert.False(AbilityProfileStore.TryGet(mule, out _));

            // Looking ([props AbilityProfile) makes one, with nothing to save: neither has any ability by nature.
            Assert.True(evo.AbilityProfile.IsEmpty);
            Assert.Empty(evo.AbilityProfile.EnumerateAllAbilities());
            Assert.True(mule.AbilityProfile.IsEmpty);
            Assert.Empty(mule.AbilityProfile.EnumerateAllAbilities());
        }
        finally
        {
            DeleteAll(evo, mule, owner, target);
        }
    }
}
