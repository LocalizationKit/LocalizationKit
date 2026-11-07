using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LocalizationKit.VsExtension
{
    /// <summary>One culture's resource file, as the editor needs to see it.</summary>
    internal sealed class ResourceFile
    {
        public ResourceFile(string path, string culture, string text)
        {
            Path = path;
            Culture = culture;
            Text = text;
        }

        public string Path { get; }

        public string Culture { get; }

        public string Text { get; }

        public string Name => System.IO.Path.GetFileName(Path);
    }

    /// <summary>Finds the resource files of the solution, and writes them back.</summary>
    /// <remarks>
    /// The convention is the one the rest of the kit uses, because it is the one a WinUI project
    /// already follows: <c>Strings/&lt;culture&gt;/Resources.resw</c>, where the folder names the
    /// culture. Looking for that shape means the window can open the right two files without being
    /// told, which is the difference between a tool somebody uses and one they have to set up.
    /// </remarks>
    internal static class ResourceFiles
    {
        private static readonly string[] SkippedDirectories = { "bin", "obj", ".vs", ".git", "node_modules", "packages" };

        /// <summary>Looks for resource files under a directory.</summary>
        /// <param name="root">Where to start, normally the solution's folder.</param>
        /// <param name="limit">How many files at most, newest first.</param>
        /// <returns>The files found, the default culture first where one can be told.</returns>
        public static IReadOnlyList<ResourceFile> Find(string root, int limit = 8)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return Array.Empty<ResourceFile>();
            }

            List<ResourceFile> found = new List<ResourceFile>();

            foreach (string path in Enumerate(root))
            {
                if (found.Count >= limit)
                {
                    break;
                }

                string culture = CultureOf(path);
                if (culture == null)
                {
                    continue;
                }

                try
                {
                    found.Add(new ResourceFile(path, culture, File.ReadAllText(path)));
                }
                catch (IOException)
                {
                    // Being written by something else; the window can open it later.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            // English first when it is there, so the window opens with the culture the keys are
            // authored in on the left.
            return found
                .OrderByDescending(file => file.Culture.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                .ThenBy(file => file.Culture, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Writes a file back, keeping whatever the editor decided its bytes should be.</summary>
        /// <param name="path">The file to write.</param>
        /// <param name="text">The text, which carries its own byte order mark when it had one.</param>
        public static void Save(string path, string text)
        {
            // No byte order mark of our own: the editor preserves the one the file had, as the first
            // character of this string, and adding another would corrupt the file.
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        /// <summary>The culture a resource file's folder names, or null when the path says nothing.</summary>
        /// <param name="path">The file path.</param>
        /// <returns>The culture name, such as <c>it-IT</c>.</returns>
        public static string CultureOf(string path)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);

            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            // A culture folder, not just any folder: two or three letters, optionally with a region.
            string[] parts = folder.Split('-');
            if (parts[0].Length < 2 || parts[0].Length > 3 || !parts[0].All(char.IsLetter))
            {
                return null;
            }

            return folder;
        }

        private static IEnumerable<string> Enumerate(string directory)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.resw");
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory);
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (string child in children)
            {
                if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (string file in Enumerate(child))
                {
                    yield return file;
                }
            }
        }
    }
}
