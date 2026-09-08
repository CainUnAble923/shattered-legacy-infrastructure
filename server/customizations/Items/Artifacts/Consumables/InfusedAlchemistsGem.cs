using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/InfusedAlchemistsGem.cs (CC9). Ported unchanged,
    // including the English message and the unconditional +1 to Alchemy base skill.
    [SerializationGenerator(0, false)]
    public partial class InfusedAlchemistsGem : Item
    {
        [Constructible]
        public InfusedAlchemistsGem() : base(0x1EA7)
        {
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1113006;

        public override void AddNameProperties(IPropertyList list)
        {
            base.AddNameProperties(list);

            list.Add(1070722, "Alchemy Skill Increaser + 1");
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.Skills[SkillName.Alchemy].Base += 1;
            from.SendMessage("You have increased your Alchemy Skill by 1 Point !.");
            Delete();
        }
    }
}
