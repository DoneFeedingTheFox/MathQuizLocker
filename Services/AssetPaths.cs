using System;
using System.IO;

namespace MathQuizLocker.Services
{
    /// <summary>Central place for paths under the Assets folder (next to the executable).</summary>
    public static class AssetPaths
    {
        private static readonly string BaseDir = AppContext.BaseDirectory;
        public static string AssetsRoot => Path.Combine(BaseDir, "Assets");

        public static string KnightSprite(int stage) => Path.Combine(AssetsRoot, "KnightSprites", $"knight_stage_{stage}.png");
        public static string KnightAttack(int stage) => Path.Combine(AssetsRoot, "KnightSprites", $"knight_stage_{stage}_attack.png");
        public static string KnightHit(int stage) => Path.Combine(AssetsRoot, "KnightSprites", $"knight_stage_{stage}_hit.png");
        public static string Background(string fileName) => Path.Combine(AssetsRoot, "Backgrounds", fileName);

        /// <summary>Resolves a background by base name, preferring .jpg then .png.</summary>
        public static string BackgroundBase(string baseNameWithoutExtension)
        {
            string jpg = Path.Combine(AssetsRoot, "Backgrounds", baseNameWithoutExtension + ".jpg");
            if (File.Exists(jpg)) return jpg;
            return Path.Combine(AssetsRoot, "Backgrounds", baseNameWithoutExtension + ".png");
        }

        /// <summary>Resolves any asset path, preferring .jpg then .png when no extension is given.</summary>
        public static string ResolveImagePath(string relativePathWithoutExtension)
        {
            string jpg = Path.Combine(AssetsRoot, relativePathWithoutExtension + ".jpg");
            if (File.Exists(jpg)) return jpg;
            return Path.Combine(AssetsRoot, relativePathWithoutExtension + ".png");
        }

        /// <summary>If path missing, tries the same base name with .png/.jpg (PNG preferred).</summary>
        public static string ResolveExistingPath(string path)
        {
            if (File.Exists(path)) return path;
            string png = Path.ChangeExtension(path, ".png");
            if (File.Exists(png)) return png;
            string jpg = Path.ChangeExtension(path, ".jpg");
            if (File.Exists(jpg)) return jpg;
            return path;
        }
        public static string Dice(string fileName) => Path.Combine(AssetsRoot, "Dice", fileName);
        public static string Monsters(string fileName) => Path.Combine(AssetsRoot, "Monsters", fileName);
        public static string Items(string file) => Path.Combine(AssetsRoot, "Items", file);
    }
}