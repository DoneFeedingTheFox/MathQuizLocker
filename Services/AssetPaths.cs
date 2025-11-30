using System;
using System.IO;

namespace MathQuizLocker.Services
{
    public static class AssetPaths
    {
        private static readonly string BaseDir = AppContext.BaseDirectory;

        public static string GfxRoot =>
            Path.Combine(BaseDir, "GFX");

        public static string KnightSprite(int stage) =>
            Path.Combine(GfxRoot, "KnightSprites", $"knight_stage_{stage}.png");

        public static string KnightSprite(string fileName) =>
            Path.Combine(GfxRoot, "KnightSprites", fileName);

        public static string Background(string fileName) =>
            Path.Combine(GfxRoot, "Background", fileName);
    }
}
