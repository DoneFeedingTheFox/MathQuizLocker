using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;

namespace MathQuizLocker
{
    /// <summary>
    /// Simple in-memory asset cache for Bitmaps.
    /// - Loads from disk once per unique path.
    /// - Keeps a master Bitmap in RAM.
    /// - Returns clones to callers so UI code can Dispose() freely.
    /// </summary>
    internal static class AssetCache
    {
        private static readonly ConcurrentDictionary<string, Lazy<Bitmap?>> _bitmaps =
            new(StringComparer.OrdinalIgnoreCase);

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

        public static void Preload(params string[] paths)
        {
            if (paths == null) return;
            foreach (var p in paths)
                _ = GetMasterBitmap(p);
        }

        /// <summary>
        /// Dispose all cached master bitmaps (call once on app shutdown).
        /// </summary>
        public static void DisposeAll()
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
