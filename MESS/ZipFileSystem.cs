using System.IO.Compression;

namespace MESS
{
    /// <summary>
    /// This class contains file-reading functions that can read from both directories and zip files.
    /// </summary>
    public static class ZipFileSystem
    {
        /// <summary>
        /// Opens the specified file and calls the given file reading function.
        /// <para>
        /// If the specified file cannot be found, then this function will also try looking inside .zip files.
        /// For example, if "C:\modding\maps\mymap.map" does not exist, then it will look inside "C:\modding\maps.zip" for a "mymap.map" file.
        /// If that .zip file does not exist, or if it does not contain the specified file, it will look inside "C:\modding.zip" for a "maps\mymap.map" file, and so on.
        /// </para>
        /// <para>
        /// If a .zip path is given (for example, "C:\directory\bundle.zip\file.ext") then the file will be read from that specific .zip file only.
        /// An exception will be thrown if the .zip file does not exist or if it does not contain the specified file. No .zip fallback search will be performed.
        /// </para>
        /// </summary>
        /// <exception cref="FileNotFoundException"/>
        public static TResult ReadFile<TResult>(string path, Func<Stream, TResult> readFile)
        {
            if (File.Exists(path))
            {
                // Normal file paths take priority:
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return readFile(file);
            }
            else if (path.Contains(".zip"))
            {
                // We'll skip the .zip search if a path specifically points to a file inside a .zip file ("C:\directory\bundle.zip\file.ext"):
                var zipPath = path.Substring(0, path.IndexOf(".zip") + 4);
                var entryPath = path.Substring(zipPath.Length + 1);

                if (!File.Exists(zipPath))
                    throw new FileNotFoundException($"The specified .zip archive '{zipPath}' does not exist.", path);

                using (var file = File.Open(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true))
                {
                    var matchingEntry = GetEntry(zip, entryPath);
                    if (matchingEntry != null)
                    {
                        using (var entryStream = matchingEntry.Open())
                            return readFile(entryStream);
                    }
                }

                throw new FileNotFoundException($"Unable to find '{entryPath}' in .zip archive '{zipPath}'.", path);
            }
            else
            {
                // If the specified file does not exist in the normal directory structure, try looking inside .zip files:
                var zipEquivalentDirectory = Path.GetDirectoryName(path);
                var entryPath = Path.GetFileName(path);
                while (zipEquivalentDirectory != null)
                {
                    var zipPath = zipEquivalentDirectory + ".zip";
                    if (File.Exists(zipPath))
                    {
                        // We found a .zip file, but does it contain our file? If not, keep looking.
                        using (var file = File.Open(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true))
                        {
                            var matchingEntry = GetEntry(zip, entryPath);
                            if (matchingEntry != null)
                            {
                                using (var entryStream = matchingEntry.Open())
                                    return readFile(entryStream);
                            }
                        }
                    }

                    entryPath = Path.Combine(Path.GetFileName(zipEquivalentDirectory), entryPath);
                    zipEquivalentDirectory = Path.GetDirectoryName(zipEquivalentDirectory);
                }

                throw new FileNotFoundException($"Unable to find '{path}' after .zip archive search.", path);
            }
        }

        /// <summary>
        /// Opens all matching files in the given directory (and its sub-directories) and calls the given file reading function for each.
        /// This will also look for matching files inside .zip files.
        /// <para>
        /// The file reading function is given a file content stream and a file path, for identification purposes.
        /// Files that are loaded from .zip files can be recognized by their path format: "C:\directory\bundle.zip\file.ext" (note the .zip extension).
        /// </para>
        /// </summary>
        public static IEnumerable<TResult> ReadFiles<TResult>(string directory, string extension, Func<Stream, string, TResult> readFile)
        {
            extension = "." + extension.TrimStart('.');

            var visitedFiles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var results = new List<TResult>();

            ReadFiles(directory);
            return results;


            void ReadFiles(string currentDirectory)
            {
                // First visit sub-directories - because real files take priority over files inside .zip files, and .zip files in sub-directories take priority over .zip files in parent directories:
                foreach (var subDirectory in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly).OrderBy(path => path))
                    ReadFiles(subDirectory);

                // Then look for matching files:
                foreach (var path in Directory.EnumerateFiles(currentDirectory, "*" + extension, SearchOption.TopDirectoryOnly).OrderBy(path => path))
                {
                    visitedFiles.Add(path);
                    // Real files always take priority, so we don't need to check whether they've been visited already:
                    using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        results.Add(readFile(file, path));
                }

                // And finally, look inside .zip files:
                foreach (var zipPath in Directory.EnumerateFiles(currentDirectory, "*.zip", SearchOption.TopDirectoryOnly).OrderBy(path => path))
                {
                    var zipDirPath = zipPath.Replace(".zip", "", StringComparison.InvariantCultureIgnoreCase);

                    using (var file = File.Open(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries.OrderBy(entry => entry.FullName))
                        {
                            if (!entry.FullName.EndsWith(extension))
                                continue;

                            var normalizedFullname = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                            var equivalentPath = Path.Combine(zipDirPath, normalizedFullname);
                            if (visitedFiles.Contains(equivalentPath))
                                continue;

                            visitedFiles.Add(equivalentPath);
                            using (var entryStream = entry.Open())
                                results.Add(readFile(entryStream, Path.Combine(zipPath, normalizedFullname)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Takes a path that points to an entry in a .zip file, and returns a normalized equivalent path.
        /// For example, it will return "C:\directory\bundle\file.ext" when given "C:\directory\bundle.zip\file.ext".
        /// </summary>
        public static string GetNormalizedPath(string zipPath)
            => zipPath.Replace(".zip" + Path.DirectorySeparatorChar, "" + Path.DirectorySeparatorChar);


        private static ZipArchiveEntry? GetEntry(ZipArchive zipArchive, string entryPath)
            => zipArchive.Entries.FirstOrDefault(entry => string.Equals(entry.FullName.Replace('/', Path.DirectorySeparatorChar), entryPath, StringComparison.InvariantCultureIgnoreCase));
    }
}
