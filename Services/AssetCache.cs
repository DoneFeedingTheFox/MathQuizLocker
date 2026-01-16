using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace MathQuizLocker.Services
{
    public static class AssetCache
    {
        private static readonly Dictionary<string, Bitmap> _cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a clone of an image from the cache. Clones are safe for PictureBoxes to own and dispose.
        /// </summary>
        public static Bitmap? GetImageClone(string path)
        {
            var master = GetMasterBitmap(path);
            return master != null ? new Bitmap(master) : null;
        }

        /// <summary>
        /// Retrieves the master bitmap from the cache, loading it if necessary.
        /// </summary>
        public static Bitmap? GetMasterBitmap(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            if (_cache.TryGetValue(path, out var existing))
            {
                return existing;
            }

            try
            {
                // Use LoadImageNoLock logic to prevent file-in-use errors
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                var bmp = new Bitmap(ms);
                _cache[path] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Preloads a set of assets into memory to prevent stuttering during gameplay.
        /// </summary>
        public static void Preload(params string[] paths)
        {
            foreach (var path in paths)
            {
                GetMasterBitmap(path);
            }
        }

        /// <summary>
        /// Disposes all cached bitmaps. Call this only on application exit.
        /// </summary>
        public static void DisposeAll()
        {
            foreach (var bmp in _cache.Values)
            {
                bmp.Dispose();
            }
            _cache.Clear();
        }
    }
}