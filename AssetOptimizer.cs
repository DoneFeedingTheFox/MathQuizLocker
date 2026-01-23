using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class AssetOptimizer
{
    public static void CreateLowResAssets(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLower();
            if (ext != ".png" && ext != ".jpg") continue;

            using (Image sourceImage = Image.FromFile(file))
            {
                // Determine new size: 50% for sprites, 1280 wide for backgrounds
                int newWidth, newHeight;
                if (sourceImage.Width >= 1920) // Backgrounds
                {
                    newWidth = 1280;
                    newHeight = (int)(sourceImage.Height * (1280.0 / sourceImage.Width));
                }
                else // Sprites (512x512 -> 256x256)
                {
                    newWidth = sourceImage.Width / 2;
                    newHeight = sourceImage.Height / 2;
                }

                using (Bitmap lowResBuffer = new Bitmap(newWidth, newHeight))
                using (Graphics g = Graphics.FromImage(lowResBuffer))
                {
                    // High-speed settings for the "Bake" process
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    g.DrawImage(sourceImage, 0, 0, newWidth, newHeight);

                    // Save to the new directory
                    string relativePath = Path.GetRelativePath(sourceDir, file);
                    string destPath = Path.Combine(targetDir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                    lowResBuffer.Save(destPath, ImageFormat.Png);
                }
            }
        }
    }
}