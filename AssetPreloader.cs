using MathQuizLocker.Models;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    /// <summary>Warms the asset cache at startup to reduce first-fight stutter.</summary>
    internal static class AssetPreloader
    {
        public static void Warmup(AppSettings settings, MonsterService monsterService)
        {
            var paths = new List<string>();

            for (int i = 1; i <= 10; i++)
                paths.Add(AssetPaths.Dice($"die_{i}.png"));
            paths.Add(AssetPaths.Dice("multiply.png"));

            int stage = Math.Clamp(settings.PlayerProgress.EquippedKnightStage, 1, 10);
            paths.Add(AssetPaths.KnightSprite(stage));
            paths.Add(AssetPaths.KnightAttack(stage));
            paths.Add(AssetPaths.KnightHit(stage));

            int level = settings.PlayerProgress.Level;
            string biomeBase = level switch
            {
                1 => "meadow_01",
                2 => "swamp_01",
                3 => "forest_01",
                4 => "cave_01",
                _ => "castle_01"
            };
            paths.Add(AssetPaths.BackgroundBase(biomeBase));
            paths.Add(AssetPaths.BackgroundBase($"{biomeBase}_boss"));
            paths.Add(AssetPaths.BackgroundBase("scroll_bg"));
            paths.Add(AssetPaths.Items("chest_01.png"));
            paths.Add(AssetPaths.Items("chest_open_01.png"));

            AddMonsterSprites(paths, monsterService.GetMonsterByLevel(level, false));
            AddMonsterSprites(paths, monsterService.GetMonsterByLevel(level, true));

            AssetCache.Preload(paths.ToArray());
            AssetCache.PreloadDisplayBackgrounds(
                AssetPaths.BackgroundBase(biomeBase),
                AssetPaths.BackgroundBase($"{biomeBase}_boss"),
                AssetPaths.BackgroundBase("scroll_bg"));
        }

        private static void AddMonsterSprites(List<string> paths, MonsterConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.SpritePath))
                return;

            string basePath = config.SpritePath;
            if (basePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                basePath = basePath[..^4];

            paths.Add(basePath + ".png");
            paths.Add(basePath + "_hit.png");
            paths.Add(basePath + "_attack.png");
        }
    }
}
