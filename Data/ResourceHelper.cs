using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ISTQBEmulator.Data
{
    public static class ResourceHelper
    {
        // Finds a specific file baked into the .exe by its name
        public static string ReadEmbeddedJson(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null) return null;

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Finds all baked files that contain a keyword (like "Sample Exam")
        public static List<string> ReadAllEmbeddedJsonsWithKeyword(string keyword)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(n => n.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            List<string> contents = new();
            foreach (var name in resourceNames)
            {
                using Stream stream = assembly.GetManifestResourceStream(name);
                using StreamReader reader = new StreamReader(stream);
                contents.Add(reader.ReadToEnd());
            }
            return contents;
        }

        // 🔥 NEW: Extracts an image directly from the compiled .exe memory!
        public static async Task<BitmapImage?> LoadEmbeddedImageAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            var assembly = Assembly.GetExecutingAssembly();
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null) return null;

            using Stream stream = assembly.GetManifestResourceStream(resourceName)!;

            // WinUI 3 requires a special "RandomAccessStream" to draw images
            using InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();
            using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
            {
                await stream.CopyToAsync(outputStream.AsStreamForWrite());
                await outputStream.FlushAsync();
            }

            var bitmap = new BitmapImage();
            randomAccessStream.Seek(0);
            await bitmap.SetSourceAsync(randomAccessStream);

            return bitmap;
        }
    }
}