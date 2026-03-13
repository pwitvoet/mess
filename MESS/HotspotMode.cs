using MESS.Logging;
using MESS.Mapping;
using System.Diagnostics;
using MESS.Util;
using MESS.Macros.Texturing;
using MESS.Common;
using System.IO.Compression;
using System.Text;

namespace MESS
{
    class HotspotModeSettings
    {
        // Hotspotting:
        public int? RandomSeed { get; set; }
        public HotspotSettings HotspotSettings { get; } = new HotspotSettings();

        public string? MapWadProperty { get; set; }                     // NOTE: Only required for rmf/jmf, and maps that don't store wad lists.
        public List<string> TextureDirectories { get; set; } = new();   // NOTE: Required.

        // Other settings:
        public LogLevel? LogLevel { get; set; }
        public string? LogPath { get; set; }

        // Input/output file paths: = "";
        public string InputPath { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }


    class HotspotMode
    {
        public static int Run(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();

            var settings = new HotspotModeSettings();
            var commandLineParser = GetCommandLineParser(settings);

            try
            {
                // Special commands:
                if (args.Contains("-help"))
                {
                    ShowHelp(commandLineParser);
                    return 0;
                }

                commandLineParser.Parse(args.Where(arg => arg != "-hotspot").ToArray());

                if (string.IsNullOrEmpty(settings.OutputPath))
                    settings.OutputPath = Path.ChangeExtension(settings.InputPath, ".map");


                var logPath = settings.LogPath ?? FileSystem.GetFullPath(string.IsNullOrEmpty(settings.InputPath) ? "mess-hotspot.log" : $"{settings.InputPath}.mess-hotspot.log");
                var logLevel = settings.LogLevel ?? LogLevel.Info;
                using (var logger = CreateLogger(logLevel, logPath))
                {
                    logger.Minimal($"MESS v{Program.MessVersion}: Macro Entity Scripting System");
                    logger.Minimal("----- TEXTURE HOTSPOTTING MODE -----");
                    logger.Minimal($"Command line: {Environment.CommandLine}");
                    logger.Minimal($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
                    logger.Minimal("");


                    Map map;
                    try
                    {
                        logger.Info($"Loading '{settings.InputPath}'.");

                        // Load input map:
                        map = MapFile.Load(settings.InputPath, logger: logger);

                        // Apply hotspotting:
                        ApplyTextureHotspotting(map, settings, logger);

                        // Save the result:
                        MapFile.Save(map, settings.OutputPath, logger: logger);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to apply hotspot texturing to '{settings.InputPath}'.", ex);
                        var innerException = ex.InnerException;
                        while (innerException != null)
                        {
                            logger.Error("Inner exception:", innerException);
                            innerException = innerException.InnerException;
                        }
                        return -1;
                    }
                    finally
                    {
                        logger.Minimal("");
                        logger.Minimal($"Finished in {stopwatch.ElapsedMilliseconds / 1000f:0.##} seconds.");
                        logger.Minimal("");
                        logger.Minimal("----- END MESS -----");
                    }
                }
            }
            catch (Exception ex)
            {
                var noLogFile = settings.LogLevel == LogLevel.Off;
                using (var errorLogger = CreateLogger(LogLevel.Error, noLogFile ? null : FileSystem.GetFullPath("mess-convert.log")))
                    errorLogger.Error($"A problem has occurred: {ex.GetType().Name}: '{ex.Message}'.");

                ShowHelp(commandLineParser);
                return -1;
            }

            return 0;
        }


        private static CommandLine GetCommandLineParser(HotspotModeSettings settings)
        {
            return new CommandLine()
                // Hotspotting:
                .Section("Hotspotting:")
                .Option(
                    "-wad",
                    s => settings.MapWadProperty = s,
                    "A semicolon-separated list of wad file paths. Only required for .rmf and .jmf files, ")
                .Option(
                    "-texturedirs",
                    s => settings.TextureDirectories.AddRange(s.Split(';').Select(dir => dir.Trim())),
                    "A semicolon-separated list of directories that contain the map's .wad files. This is used to resolve relative wad paths, and is needed for normalizing UV coordinates. If the input file does not contain wad information, use the -wad option to specify a list of wad files. Directories are tried in the specified order. The input map's directory is tried as fallback.")
                .Option(
                    "-seed",
                    s => settings.RandomSeed = int.Parse(s),
                    "Sets the random seed. The same seed and map input will always produce the same result. Without a seed, hotspotting may produce different results each time.")
                .Option(
                    "-scale",
                    s => settings.HotspotSettings.DefaultTextureScale = float.Parse(s),
                    "Default texture scale. Default value is 1.")
                .Switch(
                    "-norotation",
                    () => settings.HotspotSettings.AllowRotation = false,
                    "Disables rotation of rectangles.")
                .Switch(
                    "-nomirroring",
                    () => settings.HotspotSettings.AllowMirroring = false,
                    "Disables random mirroring of rectangles.")
                .Switch(
                    "-alternate",
                    () => settings.HotspotSettings.UseAlternateRectangles = true,
                    "Use alternate hotspot rectangles.")
                .Switch(
                    "-notiling",
                    () => settings.HotspotSettings.AllowTilingRectangles = false,
                    "Disables use of tiling hotspot rectangles.")
                .Switch(
                    "-notilingpreference",
                    () => settings.HotspotSettings.PreferNonTilingRectangles = false,
                    "Disables preference for non-tiling rectangles.")
                .Switch(
                    "-nonuniformtiling",
                    () => settings.HotspotSettings.UniformScalingForTilingRectangles = false,
                    "Allow non-uniform scaling of tiling rectangles.")
                .Option(
                    "-ignoretextures",
                    s =>
                    {
                        var textures = Macros.Util.ParseCommaSeparatedList(s);
                        foreach (var texture in textures)
                            settings.HotspotSettings.IgnoreTextures.Add(texture);
                    },
                    "Faces with these textures will not be hotspotted, and their edges are treated as concave, which affects hotspotting on neighboring faces.")

                // Logging:
                .Section("Logging:")
                .Option(
                    "-log",
                    s => { settings.LogLevel = ParseOption<LogLevel>(s); },
                    $"Sets the log level. Valid options are: {GetOptions<LogLevel>()}. Default value is {ToString(LogLevel.Info)}.")
                .Option(
                    "-log-path",
                    s => settings.LogPath = s,
                    "Sets the log file path. The default is INPUT_PATH.mess-convert.log.")

                // Input/output paths:
                .Argument(
                    s => settings.InputPath = FileSystem.GetFullPath(s, Directory.GetCurrentDirectory()),
                    "Input map file. Accepted formats are .map (valve220), .rmf and .jmf.")
                .Argument(
                    s => settings.OutputPath = FileSystem.GetFullPath(s, Directory.GetCurrentDirectory()),
                    "Output map file. Accepted formats are .map, .rmf and .jmf.");


            TEnum ParseOption<TEnum>(string input) where TEnum : struct, Enum
                => (TEnum)Enum.Parse(typeof(TEnum), input, true);

            string GetOptions<TEnum>() where TEnum : struct, Enum
                => string.Join(", ", Enum.GetValues<TEnum>().Select(value => value.ToString().ToLowerInvariant()));

            string ToString<TEnum>(TEnum value) where TEnum : struct, Enum
                => value.ToString().ToLowerInvariant();
        }

        private static ILogger CreateLogger(LogLevel logLevel, string? logPath)
        {
            if (logLevel == LogLevel.Off || string.IsNullOrEmpty(logPath))
                return new ConsoleLogger(logLevel);

            return new MultiLogger(new ConsoleLogger(logLevel), new FileLogger(logPath, logLevel));
        }

        private static void ShowHelp(CommandLine commandLine)
        {
            using (var output = Console.OpenStandardOutput())
            using (var writer = new StreamWriter(output, leaveOpen: true))
            {
                writer.WriteLine($"MESS v{Program.MessVersion}: Macro Entity Scripting System");
                writer.WriteLine();
                writer.WriteLine("Texture hotspotting mode");
                writer.WriteLine();
                commandLine.ShowDescriptions(writer);
            }
        }


        private static void ApplyTextureHotspotting(Map map, HotspotModeSettings settings, ILogger logger)
        {
            var random = settings.RandomSeed is null ? new Random() : new Random(settings.RandomSeed.Value);

            var hotspotData = new HotspotData();
            var wadPaths = GetAbsoluteWadPaths(map, settings);
            foreach (var wadPath in wadPaths)
            {
                try
                {
                    var wadRectsFilePath = wadPath + ".rects";
                    if (File.Exists(wadRectsFilePath))
                        ReadWadRectsFile(wadRectsFilePath, hotspotData, logger);
                    else if (Directory.Exists(wadRectsFilePath))
                        ReadWadRectsDirectory(wadRectsFilePath, hotspotData, logger);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to load '{wadPath}': {ex.GetType().Name}: '{ex.Message}'.");
                }
            }

            // Apply hotspots:
            var entityIndex = 0;
            foreach (var entity in map.Entities.Prepend(map.Worldspawn))
            {
                var brushIndex = 0;
                foreach (var brush in entity.Brushes)
                {
                    var faceIndex = 0;
                    foreach (var face in brush.Faces)
                    {
                        try
                        {
                            HotspotTexturing.ApplyHotspotTexturing(face, brush, hotspotData, settings.HotspotSettings, random);
                        }
                        catch (Exception ex)
                        {
                            logger.Warning($"Failed to apply hotspot texturing to entity #{entityIndex}, brush #{brushIndex}, face #{faceIndex}:", ex);
                        }

                        faceIndex += 1;
                    }

                    brushIndex += 1;
                }

                entityIndex += 1;
            }
        }

        private static string[] GetAbsoluteWadPaths(Map map, HotspotModeSettings settings)
        {
            return (settings.MapWadProperty ?? map.Properties.GetString(Attributes.Wad) ?? "")
                .Split(';')
                .Select(path => path.Trim())
                .Select(path => Path.IsPathRooted(path) ? path : ResolveWadPath(path, settings))
                .ToArray();
        }

        private static string ResolveWadPath(string relativePath, HotspotModeSettings settings)
        {
            foreach (var directory in settings.TextureDirectories)
            {
                var fullPath = Path.Combine(directory, relativePath);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            if (Path.GetDirectoryName(settings.InputPath) is string mapDirectory)
            {
                var fullPath = Path.Combine(mapDirectory, relativePath);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return relativePath;
        }

        private static void ReadWadRectsFile(string wadRectsFilePath, HotspotData hotspotData, ILogger logger)
        {
            logger.Info($"Reading hotspot data from '{wadRectsFilePath}' file.");

            using (var file = File.Open(wadRectsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zipArchive = new ZipArchive(file, ZipArchiveMode.Read, true, Encoding.UTF8))
            {
                var mappingEntry = zipArchive.GetEntry("rects.mapping");
                if (mappingEntry is null)
                {
                    logger.Warning($"'{wadRectsFilePath}' does not contain a 'rects.mapping' file that maps hotspot rectangle data to textures.");
                    return;
                }

                var hotspotRectanglesCache = new Dictionary<string, HotspotRectangle[]?>();

                var rectMappings = Array.Empty<(string, string)>();
                using (var mappingEntryStream = mappingEntry.Open())
                    rectMappings = ReadRectsMappingFile(mappingEntryStream, logger).ToArray();

                foreach ((var textureName, var rectFileName) in rectMappings)
                {
                    if (!hotspotRectanglesCache.TryGetValue(rectFileName, out var hotspotRectangles))
                    {
                        hotspotRectangles = LoadRectFile(zipArchive, rectFileName);
                        hotspotRectanglesCache[rectFileName] = hotspotRectangles;
                    }

                    if (hotspotRectangles != null)
                    {
                        logger.Verbose($"Found {hotspotRectangles.Length} hotspot rectangles for '{textureName}'.");
                        hotspotData.SetHotspotRectanglesForTexture(textureName, hotspotRectangles);
                    }
                }
            }


            HotspotRectangle[]? LoadRectFile(ZipArchive zipArchive, string rectFileName)
            {
                var rectEntry = zipArchive.GetEntry(rectFileName);
                if (rectEntry is null)
                {
                    logger.Warning($"'{wadRectsFilePath}' does not contain a '{rectFileName}' file. No hotspot data will be available for textures that reference this rect file.");
                    return null;
                }
                else
                {
                    // TODO: Improve this with error detection/reporting/logging!
                    using (var entryStream = rectEntry.Open())
                    using (var reader = new StreamReader(entryStream, Encoding.UTF8))
                    {
                        var data = reader.ReadToEnd();
                        return RectFileParser.Parse(data);
                    }
                }
            }
        }

        private static void ReadWadRectsDirectory(string wadRectsDirectory, HotspotData hotspotData, ILogger logger)
        {
            logger.Info($"Reading hotspot data from '{wadRectsDirectory}' directory.");

            var rectsMappingFilePath = Path.Combine(wadRectsDirectory, "rects.mapping");
            if (!File.Exists(rectsMappingFilePath))
            {
                logger.Info($"'{wadRectsDirectory}' does not contain a 'rects.mapping' file that maps hotspot rectangle data to textures.");
                return;
            }

            var hotspotRectanglesCache = new Dictionary<string, HotspotRectangle[]?>();

            var rectMappings = Array.Empty<(string, string)>();
            using (var rectsMappingFile = File.Open(rectsMappingFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                rectMappings = ReadRectsMappingFile(rectsMappingFile, logger).ToArray();

            foreach ((var textureName, var rectFileName) in rectMappings)
            {
                if (!hotspotRectanglesCache.TryGetValue(rectFileName, out var hotspotRectangles))
                {
                    hotspotRectangles = LoadRectFile(rectFileName);
                    hotspotRectanglesCache[rectFileName] = hotspotRectangles;
                }

                if (hotspotRectangles != null)
                {
                    logger.Verbose($"Found {hotspotRectangles.Length} hotspot rectangles for '{textureName}'.");
                    hotspotData.SetHotspotRectanglesForTexture(textureName, hotspotRectangles);
                }
            }


            HotspotRectangle[]? LoadRectFile(string rectFileName)
            {
                var fullRectFilePath = Path.Combine(wadRectsDirectory, rectFileName);
                if (!File.Exists(fullRectFilePath))
                {
                    logger.Warning($"'{wadRectsDirectory}' does not contain a '{rectFileName}' file. No hotspot data will be available for textures that reference this rect file.");
                    return null;
                }
                else
                {
                    // TODO: Improve this with error detection/reporting/logging!
                    using (var fileStream = File.Open(fullRectFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                    {
                        var data = reader.ReadToEnd();
                        return RectFileParser.Parse(data);
                    }
                }
            }
        }


        private static IEnumerable<(string, string)> ReadRectsMappingFile(Stream stream, ILogger logger)
        {
            var rectsMappingLines = ReadLines(stream);
            for (int i = 0; i < rectsMappingLines.Count; i++)
            {
                var line = rectsMappingLines[i].Trim();
                if (line.StartsWith("//") || string.IsNullOrEmpty(line))
                    continue;

                var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    logger.Warning($"rects.mapping line #{i + 1} has invalid format: '{line}'.");
                }
                else
                {
                    var textureName = parts[0];
                    var rectFileName = parts[1];

                    yield return (textureName, rectFileName);
                }
            }


            IReadOnlyList<string> ReadLines(Stream stream)
            {
                var lines = new List<string>();
                using (var reader = new StreamReader(stream))
                {
                    while (reader.ReadLine() is string line)
                        lines.Add(line);
                }
                return lines;
            }
        }
    }
}
