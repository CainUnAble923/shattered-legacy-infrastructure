// cc-P65, F-35: the evo pet's two choices, made once by the owner: its hue at stage 1, and its body at a stage that
// offers a pick. A gump cannot draw a creature body, so the pick is by name; the hue picker shows the line's stock
// statuette in each hue where one exists (dog 0x2588 and cat 0x211B, pinned Distribution/Data/shrink.json).

using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public class EvoHueGump : DynamicGump
    {
        private const int LabelHue = 0x480;
        private readonly EvoPet _pet;

        public override bool Singleton => true;

        public EvoHueGump(EvoPet pet) : base(60, 60) => _pet = pet;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var hues = EvoPetData.Hues;
            var statue = _pet.Line?.StatueItemId ?? 0;
            var height = 80 + hues.Length * 34;

            builder.AddPage();
            builder.AddBackground(0, 0, 300, height, 9200);
            builder.AddAlphaRegion(10, 10, 280, height - 20);
            builder.AddLabel(20, 16, LabelHue, $"Choose {_pet.Name}'s color");
            builder.AddLabel(20, 36, 0x3B2, "It keeps this color at every stage.");

            for (var i = 0; i < hues.Length; i++)
            {
                var y = 64 + i * 34;
                builder.AddButton(20, y + 4, 4005, 4007, 1 + i);
                builder.AddLabel(56, y + 6, LabelHue, hues[i].Name);

                if (statue != 0)
                {
                    builder.AddItem(200, y - 4, statue, hues[i].Hue);
                }
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 1;

            if (i >= 0 && i < EvoPetData.Hues.Length && _pet is { Deleted: false } &&
                _pet.ChooseHue(sender.Mobile, EvoPetData.Hues[i].Hue))
            {
                sender.Mobile.SendMessage(EvoPet.EchoHue, $"The Echo: {EvoPetData.Hues[i].Name}. Good choice. You always pick that one.");
            }
        }
    }

    public class EvoPickGump : DynamicGump
    {
        private const int LabelHue = 0x480;
        private readonly EvoPet _pet;
        private readonly int _stage;

        public override bool Singleton => true;

        public EvoPickGump(EvoPet pet, int stage) : base(60, 60)
        {
            _pet = pet;
            _stage = stage;
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var stage = _pet.Line?.Stage(_stage);
            var count = stage?.Bodies.Length ?? 0;
            var height = 96 + count * 28;

            builder.AddPage();
            builder.AddBackground(0, 0, 320, height, 9200);
            builder.AddAlphaRegion(10, 10, 300, height - 20);
            builder.AddLabel(20, 16, LabelHue, $"What will {_pet.Name} become?");
            builder.AddLabel(20, 36, 0x22, "This choice is for good.");

            for (var i = 0; i < count; i++)
            {
                var y = 64 + i * 28;
                builder.AddButton(20, y, 4005, 4007, 1 + i);
                builder.AddLabel(56, y + 2, LabelHue, stage!.BodyNames[i]);
            }

            builder.AddLabel(20, height - 28, 0x3B2, "Close this to decide later; it waits.");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var stage = _pet.Line?.Stage(_stage);
            var i = info.ButtonID - 1;

            if (stage == null || i < 0 || i >= stage.Bodies.Length || _pet.Deleted)
            {
                return;
            }

            if (!_pet.ChoosePick(sender.Mobile, _stage, stage.Bodies[i]))
            {
                sender.Mobile.SendMessage("That choice cannot be made now.");
            }
        }
    }
}
