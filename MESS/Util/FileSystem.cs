namespace MESS.Util
{
    public static class FileSystem
    {
        /// <summary>
        /// Returns the absolute path for the given path string. This also expands environment variables.
        /// If <paramref name="relativeTo"/> is null, relative paths are seen as relative to the MESS.exe directory.
        /// </summary>
        public static string GetFullPath(string path, string? relativeTo = null)
        {
            path = Environment.ExpandEnvironmentVariables(path);
            if (!Path.IsPathRooted(path))
                path = Path.Combine(relativeTo ?? AppContext.BaseDirectory, path);
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Returns true if the given path is a parent of the given child path.
        /// </summary>
        public static bool IsParentPath(string path, string childPath)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            return childPath.StartsWith(path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
