using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace MathQuizLocker
{
    /// <summary>
    /// Simple in-memory asset cache for Bitmaps.
    /// - Loads from disk once per unique path.
    /// - Keeps a master Bitmap in RAM.
    /// - Optionally scales backgrounds to the current display size.
    /// </summary>
    internal static class AssetCache
    {
        private static readonly ConcurrentDictionary<string, Lazy<Bitmap?>> _bitmaps =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, Lazy<Bitmap?>> _scaledBackgrounds =
            new(StringComparer.OrdinalIgnoreCase);

        private static Size _displaySize;

        /// <summary>
        /// Returns a CLONE of the cached bitmap for safe use in UI controls.
        /// Caller owns the returned image and should Dispose() it when replaced.
        /// </summary>
        public static Image? GetImageClone(string path)
        {
            var master = GetMasterBitmap(path);
            if (master == null) return null;

            try
            {
                return (Image)master.Clone();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the master bitmap (DO NOT Dispose this; cache owns it).
        /// Useful for draw-only scenarios where you will not dispose it.
        /// </summary>
        public static Bitmap? GetMasterBitmap(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var lazy = _bitmaps.GetOrAdd(path, p => new Lazy<Bitmap?>(() => LoadBitmapFromDisk(p)));
            return lazy.Value;
        }

        /// <summary>True if the image is a cache-owned master bitmap (must not be disposed by callers).</summary>
        public static bool IsCachedMaster(Image? image)
        {
            if (image == null) return false;
            if (IsInDictionary(_bitmaps, image)) return true;
            return IsInDictionary(_scaledBackgrounds, image);
        }

        private static bool IsInDictionary(ConcurrentDictionary<string, Lazy<Bitmap?>> dict, Image image)
        {
            foreach (var kvp in dict)
            {
                if (kvp.Value.IsValueCreated && ReferenceEquals(kvp.Value.Value, image))
                    return true;
            }
            return false;
        }

        /// <summary>Disposes only if the image is not a cache-owned master.</summary>
        public static void DisposeIfOwned(Image? image)
        {
            if (image != null && !IsCachedMaster(image))
            {
                try { image.Dispose(); } catch { /* ignore */ }
            }
        }

        /// <summary>Sets the target display size for background scaling (call on startup and resize).</summary>
        public static void ConfigureDisplaySize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            var next = new Size(width, height);
            if (_displaySize == next)
                return;

            _displaySize = next;
            ClearScaledBackgrounds();
        }

        /// <summary>Returns a display-sized background bitmap (scaled once and cached).</summary>
        public static Bitmap? GetBackgroundForDisplay(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (_displaySize.Width <= 0 || _displaySize.Height <= 0)
                return GetMasterBitmap(path);

            string key = $"{path}|{_displaySize.Width}x{_displaySize.Height}";
            var lazy = _scaledBackgrounds.GetOrAdd(key, _ => new Lazy<Bitmap?>(() => CreateScaledBackground(path, _displaySize)));
            return lazy.Value;
        }

        /// <summary>Preloads display-sized backgrounds for the current display size.</summary>
        public static void PreloadDisplayBackgrounds(params string[] paths)
        {
            if (paths == null)
                return;
            foreach (var path in paths)
                _ = GetBackgroundForDisplay(path);
        }

        /// <summary>Loads master assets into the cache on the calling thread (call during startup).</summary>
        public static void Preload(params string[] paths)
        {
            if (paths == null) return;
            foreach (var path in paths)
                _ = GetMasterBitmap(path);
        }

        /// <summary>
        /// Dispose all cached bitmaps (call once on app shutdown).
        /// </summary>
        public static void DisposeAll()
        {
            ClearScaledBackgrounds();
            ClearMasterBitmaps();
        }

        private static void ClearMasterBitmaps()
        {
            foreach (var kvp in _bitmaps)
            {
                try
                {
                    if (kvp.Value.IsValueCreated)
                        kvp.Value.Value?.Dispose();
                }
                catch { /* ignore */ }
            }
            _bitmaps.Clear();
        }

        private static void ClearScaledBackgrounds()
        {
            foreach (var kvp in _scaledBackgrounds)
            {
                try
                {
                    if (kvp.Value.IsValueCreated)
                        kvp.Value.Value?.Dispose();
                }
                catch { /* ignore */ }
            }
            _scaledBackgrounds.Clear();
        }

        private static Bitmap? CreateScaledBackground(string path, Size size)
        {
            var master = GetMasterBitmap(path);
            if (master == null)
                return null;

            var scaled = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.Low;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.DrawImage(master, 0, 0, size.Width, size.Height);
            }
            return scaled;
        }

        private static Bitmap? LoadBitmapFromDisk(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                // Read into memory so the file is not locked.
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);

                // Important: create a new Bitmap that fully detaches from the stream
                // (some GDI+ behaviors keep a reference otherwise).
                using var temp = new Bitmap(ms);
                return new Bitmap(temp);
            }
            catch
            {
                return null;
            }
        }
    }
}
