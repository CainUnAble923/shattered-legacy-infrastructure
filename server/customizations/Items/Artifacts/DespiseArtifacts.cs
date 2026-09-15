using System;
using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class CompassionsEye : GoldRing
    {
        [Constructible]
        public CompassionsEye()
        {
            Hue = 1174;
            Attributes.BonusInt = 10;
            Attributes.BonusMana = 10;
            Attributes.RegenMana = 2;
            Attributes.Luck = 250;
            Attributes.SpellDamage = 20;
            Attributes.LowerRegCost = 20;
        }

        public override int LabelNumber => 1153288;
    }

    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // ServUO derives from Sandals and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that and reproduces stock Sandals's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 4 for the set-armour bucket.
    // [Flippable] is copied from Sandals: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x170d, 0x170e)]
    [SerializationGenerator(0, false)]
    public partial class UnicornManeWovenSandals : BaseSetClothing
    {
        [Constructible]
        public UnicornManeWovenSandals() : base(0x170D, Layer.Shoes)
        {
            Hue = 1154;
            switch(Utility.Random(6))
            {
                case 0: AbsorptionAttributes.EaterKinetic = 2; break;
                case 1: AbsorptionAttributes.EaterFire = 2; break;
                case 2: AbsorptionAttributes.EaterCold = 2; break;
                case 3: AbsorptionAttributes.EaterPoison = 2; break;
                case 4: AbsorptionAttributes.EaterEnergy = 2; break;
                case 5: AbsorptionAttributes.EaterDamage = 2; break;
            }
            Attributes.NightSight = 1;
        }

        public override int LabelNumber => 1153289;

        // Stock Sandals members, reproduced because the parent changed.
        public override double DefaultWeight => 1.0;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;

        // Stock Sandals.Dye and BaseShoes.Scissor, reproduced because the parent changed.
        public override bool Dye(Mobile from, DyeTub sender) => false;

        public override bool Scissor(Mobile from, Scissors scissors)
        {
            if (DefaultResource == CraftResource.None)
            {
                return base.Scissor(from, scissors);
            }

            from.SendLocalizedMessage(502440); // Scissors can not be used on that to produce anything.
            return false;
        }
    }

    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // LeatherTalons (this batch) is on BaseSetClothing, so AbsorptionAttributes resolves.
    [SerializationGenerator(0, false)]
    public partial class UnicornManeWovenTalons : LeatherTalons
    {
        [Constructible]
        public UnicornManeWovenTalons()
        {
            Hue = 1154;
            switch(Utility.Random(6))
            {
                case 0: AbsorptionAttributes.EaterKinetic = 2; break;
                case 1: AbsorptionAttributes.EaterFire = 2; break;
                case 2: AbsorptionAttributes.EaterCold = 2; break;
                case 3: AbsorptionAttributes.EaterPoison = 2; break;
                case 4: AbsorptionAttributes.EaterEnergy = 2; break;
                case 5: AbsorptionAttributes.EaterDamage = 2; break;
            }
            Attributes.NightSight = 1;
        }

        public override int LabelNumber => 1153314;
    }

    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); SkillBonuses.SetValues(0, SkillName.Archery, 5.0) (D-23: nor a SkillBonuses block); the random block in the constructor (D-23: ModernUO's BaseQuiver has no Resistances block).
    [SerializationGenerator(0, false)]
    public partial class DespicableQuiver : BaseQuiver
    {
        [Constructible]
        public DespicableQuiver() : base(0x2B02)
        {
            Hue = 2671;
            DamageIncrease = 10;
            WeightReduction = 30;
            Attributes.BonusDex = 5;
            Attributes.ReflectPhysical = 5;
            Attributes.AttackChance = 5;
            LowerAmmoCost = 30;
        }

        public override int LabelNumber => 1153290;
    }

    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class UnforgivenVeil : GargishLeatherWingArmor
    {
        [Constructible]
        public UnforgivenVeil()
        {
            Hue = 2671;
            Attributes.BonusDex = 5;
            SkillBonuses.SetValues(0, SkillName.Throwing, 5.0);
            Attributes.ReflectPhysical = 5;
            Attributes.AttackChance = 5;
            switch(Utility.Random(5))
            {
                case 0: PhysicalBonus = 10; break;
                case 1: FireBonus = 10; break;
                case 2: ColdBonus = 10; break;
                case 3: PoisonBonus = 10; break;
                case 4: EnergyBonus = 10; break;
            }
        }

        public override int LabelNumber => 1153291;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int PhysicalResistance => PhysicalBonus;
        public override int FireResistance => FireBonus;
        public override int ColdResistance => ColdBonus;
        public override int PoisonResistance => PoisonBonus;
        public override int EnergyResistance => EnergyBonus;
    }

    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class HailstormHuman : WarFork
    {
        [Constructible]
        public HailstormHuman()
        {
            Hue = 2714;
            WeaponAttributes.HitLightning = 15;
            WeaponAttributes.HitColdArea = 100;
            WeaponAttributes.HitLeechMana = 30;
            Attributes.AttackChance = 20;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 50;
            AosElementDamages.Cold = 100;
        }

        public override int LabelNumber => 1153292;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/DespiseArtifacts.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class HailstormGargoyle : GargishWarFork
    {
        [Constructible]
        public HailstormGargoyle()
        {
            Hue = 2714;
            WeaponAttributes.HitLightning = 15;
            WeaponAttributes.HitColdArea = 100;
            WeaponAttributes.HitLeechMana = 30;
            Attributes.AttackChance = 20;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 50;
            AosElementDamages.Cold = 100;
        }

        public override int LabelNumber => 1153292;
    }
}
