using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

string assetsRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets"));

if (!Directory.Exists(assetsRoot))
{
    Console.Error.WriteLine($"Assets folder not found: {assetsRoot}");
    return 1;
}

long before = DirSize(assetsRoot);
int converted = 0;
int optimized = 0;

foreach (string file in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
{
    string ext = Path.GetExtension(file).ToLowerInvariant();
    if (ext is not (".png" or ".jpg" or ".jpeg"))
        continue;

    bool useJpeg = ShouldConvertToJpeg(file, assetsRoot);
    try
    {
        using var image = Image.Load(file);
        if (useJpeg)
        {
            string jpgPath = Path.ChangeExtension(file, ".jpg");
            await image.SaveAsJpegAsync(jpgPath, new JpegEncoder { Quality = 82 });
            if (!jpgPath.Equals(file, StringComparison.OrdinalIgnoreCase) && File.Exists(file))
                File.Delete(file);
            converted++;
        }
        else
        {
            ResizeIfOversized(image, file, assetsRoot);
            long oldLen = new FileInfo(file).Length;
            string tempPath = file + ".tmp";
            await image.SaveAsPngAsync(tempPath, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            });
            File.Move(tempPath, file, true);
            long newLen = new FileInfo(file).Length;
            if (newLen < oldLen)
                optimized++;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Skip {file}: {ex.Message}");
    }
}

long after = DirSize(assetsRoot);
Console.WriteLine($"Assets: {before / 1024 / 1024:F1} MB -> {after / 1024 / 1024:F1} MB");
Console.WriteLine($"Converted to JPEG: {converted}, PNG re-encoded: {optimized}");
return 0;

static bool ShouldConvertToJpeg(string file, string assetsRoot)
{
    string relative = Path.GetRelativePath(assetsRoot, file).Replace('\\', '/');
    if (relative.StartsWith("Backgrounds/", StringComparison.OrdinalIgnoreCase))
        return true;
    if (relative.StartsWith("UI/", StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
}

static void ResizeIfOversized(Image image, string file, string assetsRoot)
{
    string relative = Path.GetRelativePath(assetsRoot, file).Replace('\\', '/');
    if (!relative.StartsWith("KnightSprites/", StringComparison.OrdinalIgnoreCase)
        && !relative.StartsWith("Monsters/", StringComparison.OrdinalIgnoreCase))
        return;

    const int maxDim = 1024;
    int longest = Math.Max(image.Width, image.Height);
    if (longest <= maxDim)
        return;

    float scale = maxDim / (float)longest;
    int w = Math.Max(1, (int)Math.Round(image.Width * scale));
    int h = Math.Max(1, (int)Math.Round(image.Height * scale));
    image.Mutate(x => x.Resize(w, h));
}

static long DirSize(string path) =>
    Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
