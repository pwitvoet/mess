using MESS.Logging;
using MESS.Mapping;
using System.Diagnostics;
using MESS.Util;
using MESS.Macros.Texturing;
using MESS.Common;
using System.Text;
using MESS.Mathematics.Spatial;

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
                    s => settings.HotspotSettings.DefaultTextureScale = double.Parse(s),
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

            var hotspotDataCollection = new HotspotDataCollection();
            var wadPaths = GetAbsoluteWadPaths(map, settings);
            foreach (var wadPath in wadPaths)
            {
                try
                {
                    var wadHotspotFilePath = wadPath + ".hotspot";
                    if (File.Exists(wadHotspotFilePath))
                        ReadWadHotspotFile(wadHotspotFilePath, hotspotDataCollection, logger);
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
                            // Do we need to hotspot this face?
                            if (settings.HotspotSettings.IgnoreTextures.Contains(face.TextureName))
                                continue;

                            // Does this texture have hotspot data?
                            var hotspotData = hotspotDataCollection.GetHotspotDataForTexture(face.TextureName);
                            if (hotspotData == null)
                                continue;

                            var score = HotspotTexturing.ApplyHotspotTexturing(face, brush, hotspotData, settings.HotspotSettings, random);


                            // Do we need to try fallback textures?
                            if (score < hotspotData.FallbackScoreThreshold && !string.IsNullOrEmpty(hotspotData.FallbackTextureName))
                            {
                                var previousTextures = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { face.TextureName };
                                var bestScore = score;
                                var lastScore = score;
                                var bestTextureName = face.TextureName;
                                var ignoreBestMatch = false;

                                while (lastScore < hotspotData.FallbackScoreThreshold &&
                                    !string.IsNullOrEmpty(hotspotData.FallbackTextureName) &&
                                    !previousTextures.Contains(hotspotData.FallbackTextureName))
                                {
                                    var fallbackTextureName = hotspotData.FallbackTextureName;
                                    face.TextureName = fallbackTextureName;
                                    previousTextures.Add(fallbackTextureName);

                                    hotspotData = hotspotDataCollection.GetHotspotDataForTexture(fallbackTextureName);
                                    if (hotspotData == null)
                                    {
                                        // If a fallback texture doesn't have hotspot data, treat it as a tiling texture and use default scale/offset:
                                        (var rightAxis, var downAxis) = HotspotTexturing.GetBestTextureAxis(face);
                                        face.TextureRightAxis = rightAxis;
                                        face.TextureDownAxis = downAxis;
                                        face.TextureScale = new Vector2D(settings.HotspotSettings.DefaultTextureScale, settings.HotspotSettings.DefaultTextureScale);
                                        face.TextureShift = new Vector2D(0, 0);
                                        face.TextureAngle = 0;

                                        ignoreBestMatch = true;
                                        break;
                                    }

                                    lastScore = HotspotTexturing.ApplyHotspotTexturing(face, brush, hotspotData, settings.HotspotSettings, random);
                                    if (lastScore > bestScore)
                                    {
                                        bestScore = lastScore;
                                        bestTextureName = fallbackTextureName;
                                    }
                                }

                                // Pick the best match if we didn't find a good-enough match:
                                if (!ignoreBestMatch && lastScore < bestScore)
                                {
                                    face.TextureName = bestTextureName;
                                    hotspotData = hotspotDataCollection.GetHotspotDataForTexture(bestTextureName);
                                    HotspotTexturing.ApplyHotspotTexturing(face, brush, hotspotData!, settings.HotspotSettings, random);
                                }
                            }
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

        private static void ReadWadHotspotFile(string wadHotspotFilePath, HotspotDataCollection hotspotDataCollection, ILogger logger)
        {
            logger.Info($"Reading hotspot data from '{wadHotspotFilePath}' file.");

            using (var file = File.Open(wadHotspotFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    var hotspotData = HotspotFileParser.Parse(file);
                    foreach (var entry in hotspotData)
                        hotspotDataCollection.SetHotspotDataForTexture(entry.Key, entry.Value);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to read hotspot data from '{wadHotspotFilePath}':", ex);
                }
            }
        }
    }
}
