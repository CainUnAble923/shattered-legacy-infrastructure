using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items
{
    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class TreatiseonAlchemyTalisman : BaseTalisman
    {
        [Constructible]
        public TreatiseonAlchemyTalisman() : base(0x2F58)
        {
            Skill = SkillName.Alchemy;
            SuccessBonus = GetRandomSuccessful();
            Blessed = GetRandomBlessed();
            Attributes.EnhancePotions = 15;
            SkillBonuses.SetValues(0, SkillName.Magery, 5.0);
        }

        public override int LabelNumber => 1073353;
        public override bool ForceShowName => true;
    }

    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class PrimerOnArmsTalisman : BaseTalisman
    {
        [Constructible]
        public PrimerOnArmsTalisman() : base(0x2F59)
        {
            Blessed = GetRandomBlessed();
            Attributes.BonusStr = 1;
            Attributes.RegenHits = 2;
            Attributes.WeaponDamage = 20;
            Removal = TalismanRemoval.Damage;
            MaxChargeTime = 1200;
        }

        public override int LabelNumber => 1073354;
        public override bool ForceShowName => true;
    }

    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class MyBookTalisman : BaseTalisman
    {
        [Constructible]
        public MyBookTalisman() : base(0x2F5A)
        {
            Blessed = GetRandomBlessed();
            Skill = SkillName.Inscribe;
            SuccessBonus = GetRandomSuccessful();
            ExceptionalBonus = GetRandomExceptional();
            Attributes.BonusInt = 5;
            Attributes.BonusMana = 2;
        }

        public override int LabelNumber => 1073355;
        public override bool ForceShowName => true;
    }

    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class TalkingtoWispsTalisman : BaseTalisman
    {
        [Constructible]
        public TalkingtoWispsTalisman() : base(0x2F5B)
        {
            Blessed = GetRandomBlessed();
            SkillBonuses.SetValues(0, SkillName.SpiritSpeak, 3.0);
            SkillBonuses.SetValues(1, SkillName.EvalInt, 5.0);
            Removal = TalismanRemoval.Ward;
            MaxChargeTime = 1200;
        }

        public override int LabelNumber => 1073356;
        public override bool ForceShowName => true;
    }

    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class GrammarOfOrchishTalisman : BaseTalisman
    {
        [Constructible]
        public GrammarOfOrchishTalisman() : base(0x2F59)
        {
            Blessed = GetRandomBlessed();
            Protection = GetRandomProtection();
            Summoner = new TalismanAttribute(typeof(SummonedOrcBrute), 1072414);
            SkillBonuses.SetValues(0, SkillName.MagicResist, 5.0);
            SkillBonuses.SetValues(1, SkillName.Anatomy, 7.0);
            MaxChargeTime = 1800;
        }

        public override int LabelNumber => 1073358;
        public override bool ForceShowName => true;
    }

    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class BirdsofBritanniaTalisman : BaseTalisman
    {
        [Constructible]
        public BirdsofBritanniaTalisman() : base(0x2F5A)
        {
            Blessed = GetRandomBlessed();
            Slayer = TalismanSlayerName.Bird;
            SkillBonuses.SetValues(0, SkillName.AnimalTaming, 5.0);
            SkillBonuses.SetValues(1, SkillName.AnimalLore, 5.0);
            MaxChargeTime = 1800;
        }

        public override int LabelNumber => 1074892;
        public override bool ForceShowName => true;

        public override Type GetSummoner() => GetRandomSummonType();
    }

    // ServUO: Items/Equipment/Talismans/CollectionsBritLibraryTalismans.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO's TalismanSkill enum is ModernUO's SkillName on BaseTalisman.Skill (same craft-bonus check, CraftItem.cs:803);
    // TalismanSkill.Inscription is SkillName.Inscribe. TalismanAttribute(type, amount, name) is (type, name, amount) here.
    [SerializationGenerator(0, false)]
    public partial class TheLifeOfTravelingMinstrelTalisman : BaseTalisman
    {
        [Constructible]
        public TheLifeOfTravelingMinstrelTalisman() : base(0x2F5B)
        {
            Blessed = GetRandomBlessed();
            Protection = GetRandomProtection();
            SkillBonuses.SetValues(0, SkillName.Provocation, 5.0);
            SkillBonuses.SetValues(1, SkillName.Musicianship, 5.0);
            Removal = TalismanRemoval.Curse;
            MaxChargeTime = 1200;
        }

        public override int LabelNumber => 1073360;
        public override bool ForceShowName => true;
    }
}
