using System.IO.Compression;

namespace MESS
{
    /// <summary>
    /// This class contains .mtb (MESS Template Bundle)-aware file reading functions.
    /// </summary>
    public static class MtbFileSystem
    {
        /// <summary>
        /// Opens the specified file and calls the given file reading function.
        /// <para>
        /// If the specified file cannot be found, then this will try looking inside .mtb files (zip files that are used for distributing template entities).
        /// For example, if "C:\modding\maps\mymap.map" does not exist, then "C:\modding\maps.mtb" is checked for a "mymap.map" file.
        /// If that .mtb file does not exist, or if it does not contain the specified file, "C:\modding.mtb" is checked for a "maps\mymap.map" file, and so on.
        /// </para>
        /// <para>
        /// If an .mtb path is given (for example, "C:\directory\bundle.mtb\file.ext") then the file will be read from that specific .mtb file only.
        /// An exception will be thrown if the .mtb file does not exist or if it does not contain the specified file. No .mtb fallback search will be performed.
        /// </para>
        /// </summary>
        public static TResult ReadFile<TResult>(string path, Func<Stream, TResult> readFile)
        {
            if (File.Exists(path))
            {
                // Normal file paths take priority:
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return readFile(file);
            }
            else if (path.Contains(".mtb"))
            {
                // We'll skip the .mtb search if a path specifically points to a file inside an .mtb file ("C:\directory\bundle.mtb\file.ext"):
                var mtbPath = path.Substring(0, path.IndexOf(".mtb") + 4);
                var entryPath = path.Substring(mtbPath.Length + 1);

                if (!File.Exists(mtbPath))
                    throw new FileNotFoundException($"The specified .mtb archive '{mtbPath}' does not exist.", path);

                using (var file = File.Open(mtbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true))
                {
                    var matchingEntry = GetEntry(zip, entryPath);
                    if (matchingEntry != null)
                    {
                        using (var entryStream = matchingEntry.Open())
                            return readFile(entryStream);
                    }
                }

                throw new FileNotFoundException($"Unable to find '{entryPath}' in .mtb archive '{mtbPath}'.", path);
            }
            else
            {
                // If the specified file does not exist in the normal directory structure, try looking inside .mtb files:
                var mtbEquivalentDirectory = Path.GetDirectoryName(path);
                var entryPath = Path.GetFileName(path);
                while (mtbEquivalentDirectory != null)
                {
                    var mtbPath = mtbEquivalentDirectory + ".mtb";
                    if (File.Exists(mtbPath))
                    {
                        // We found an .mtb file, but does it contain our file? If not, keep looking.
                        using (var file = File.Open(mtbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
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

                    entryPath = Path.Combine(Path.GetFileName(mtbEquivalentDirectory), entryPath);
                    mtbEquivalentDirectory = Path.GetDirectoryName(mtbEquivalentDirectory);
                }

                throw new FileNotFoundException($"Unable to find '{path}' after .mtb archive search.", path);
            }
        }

        /// <summary>
        /// Opens all matching files in the given directory (and its sub-directories) and calls the given file reading function for each.
        /// This will also look for matching files inside .mtb files.
        /// <para>
        /// The file reading function is given a file content stream and a file path, for identification purposes.
        /// Files that are loaded from .mtb files can be recognized by their path format: "C:\directory\bundle.mtb\file.ext" (note the .mtb extension).
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
                // First visit sub-directories - because real files take priority over files inside .mtb files, and .mtb files in sub-directories take priority over .mtb files in parent directories:
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

                // And finally, look inside .mtb files:
                foreach (var mtbPath in Directory.EnumerateFiles(currentDirectory, "*.mtb", SearchOption.TopDirectoryOnly).OrderBy(path => path))
                {
                    var mtbDirPath = mtbPath.Replace(".mtb", "", StringComparison.InvariantCultureIgnoreCase);

                    using (var file = File.Open(mtbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries.OrderBy(entry => entry.FullName))
                        {
                            if (!entry.FullName.EndsWith(extension))
                                continue;

                            var normalizedFullname = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                            var equivalentPath = Path.Combine(mtbDirPath, normalizedFullname);
                            if (visitedFiles.Contains(equivalentPath))
                                continue;

                            visitedFiles.Add(equivalentPath);
                            using (var entryStream = entry.Open())
                                results.Add(readFile(entryStream, Path.Combine(mtbPath, normalizedFullname)));
                        }
                    }
                }
            }
        }


        private static ZipArchiveEntry? GetEntry(ZipArchive zipArchive, string entryPath)
            => zipArchive.Entries.FirstOrDefault(entry => string.Equals(entry.FullName.Replace('/', Path.DirectorySeparatorChar), entryPath, StringComparison.InvariantCultureIgnoreCase));
    }
}
