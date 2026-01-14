using System;
using System.Collections.Generic;

namespace MathQuizLocker
{
    internal sealed class BiomeManager
    {
        private readonly List<BiomeDefinition> _biomes;

        public BiomeDefinition Current { get; private set; }

        public BiomeManager(List<BiomeDefinition> biomes)
        {
            _biomes = biomes ?? new List<BiomeDefinition>();
            Current = _biomes.Count > 0 ? _biomes[0] : new BiomeDefinition();
        }

        // Simple mapping: biome changes every N levels
        public void SetForLevel(int level, int levelsPerBiome = 3)
        {
            if (_biomes.Count == 0) return;

            if (levelsPerBiome < 1) levelsPerBiome = 1;
            if (level < 1) level = 1;

            int idx = (level - 1) / levelsPerBiome;
            if (idx < 0) idx = 0;
            if (idx >= _biomes.Count) idx = _biomes.Count - 1;

            Current = _biomes[idx];
        }

        public BiomeDefinition GetCurrent() => Current;
    }
}
