using System.Collections.Generic;

namespace MathQuizLocker
{
    internal sealed class BiomeDefinition
    {
        public string Id { get; set; } = "";
        public string BackgroundPath { get; set; } = "";

        // We will use these in Step 2 (positioning)
        public Anchor Knight { get; set; } = new Anchor();
        public List<Anchor> MonsterSlots { get; set; } = new List<Anchor>();
    }

    internal sealed class Anchor
    {
        // Normalized 0..1 coordinates relative to the client area
        public float X { get; set; } = 0.5f;
        public float Y { get; set; } = 0.8f;

        // We will use Scale in Step 2
        public float Scale { get; set; } = 1.0f;
    }
}
