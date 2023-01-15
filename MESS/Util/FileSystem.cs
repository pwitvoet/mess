namespace MESS.Util
{
    public static class FileSystem
    {
        /// <summary>
        /// Returns the absolute path for the given path string. Environment variables are expanded,
        /// and relative paths are seen as relative to the MESS.exe directory (which is not necessarily the current working directory).
        /// </summary>
        public static string GetFullPath(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path);
            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppContext.BaseDirectory, path);
            return Path.GetFullPath(path);
        }
    }
}
