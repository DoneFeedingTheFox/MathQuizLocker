using System;
using System.IO;

namespace MathQuizLocker.Services
{
    public static class AssetPaths
    {
        private static readonly string BaseDir = AppContext.BaseDirectory;

        // Points to the "Assets" folder shown in your screenshot
        public static string AssetsRoot => Path.Combine(BaseDir, "Assets");

        public static string KnightSprite(int stage) =>
            Path.Combine(AssetsRoot, "KnightSprites", $"knight_stage_{stage}.png");

        public static string Background(string fileName) =>
            Path.Combine(AssetsRoot, "Backgrounds", fileName);

        public static string Ui(string fileName) =>
            Path.Combine(AssetsRoot, "UI", fileName);

        public static string Dice(string fileName) =>
        Path.Combine(AssetsRoot, "Dice", fileName);
    }
}