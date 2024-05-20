using MESS.Common;
using MESS.Formats;
using MESS.Formats.JMF;
using MESS.Formats.MAP;
using MESS.Formats.MAP.Trenchbroom;
using MESS.Formats.RMF;
using MESS.Geometry;
using MESS.Logging;
using MESS.Mapping;
using MESS.Mathematics.Spatial;
using MESS.Util;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MESS
{
    class ConvertSettings
    {
        // Input:
        public DuplicateKeyHandling? DuplicateKeys { get; set; }
        public RmfSpawnflagsPropertyHandling? RmfSpawnflagProperty { get; set; }
        public TrenchbroomGroupHandling? TrenchBroomFuncGroup { get; set; }

        // Output:
        public ValueTooLongHandling? KeyValueTooLong { get; set; }
        public ValueTooLongHandling? TextureNameTooLong { get; set; }
        public InvalidCharacterHandling? InvalidKeyValue { get; set; }
        public string KeyValueReplacement { get; set; } = "'";
        public InvalidCharacterHandling? InvalidTextureName { get; set; }
        public string TextureNameReplacement { get; set; } = "_";
        public TooManyVisGroupsHandling? VisGroups { get; set; }

        // Jmf-specific:
        public JmfFileVersion? JmfVersion { get; set; }

        // Rmf-specific:
        public RmfFileVersion? RmfVersion { get; set; }

        // Map-specific:
        public int? MapDecimals { get; set; }
        public MapFileVariant? MapFormat { get; set; }
        public string? TrenchBroomGameName { get; set; }
        public string? MapWadProperty { get; set; }

        // VIS group filtering:
        public string[]? OnlyVisGroups { get; set; }
        public string[]? NotVisGroups { get; set; }
        public bool OnlyVisGroupObjects { get; set; }
        public bool NoInvisibleVisGroups { get; set; }
        public bool NoVisibleVisGroups { get; set; }
        public bool NoOmittedLayers { get; set; }

        // Cordoning:
        public BoundingBox? CordonArea { get; set; }
        public bool UseJmfCordonArea { get; set; }
        public string? CordonTexture { get; set; }
        public float? CordonPadding { get; set; }
        public float? CordonThickness { get; set; }

        // Other settings:
        public LogLevel? LogLevel { get; set; }

        // Input/output file paths: = "";
        public string InputPath { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }

    class ConvertMode
    {
        public static int Run(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();

            var settings = new ConvertSettings();
            var commandLineParser = GetCommandLineParser(settings);

            try
            {
                // Special commands:
                if (args.Contains("-help"))
                {
                    ShowHelp(commandLineParser);
                    return 0;
                }

                commandLineParser.Parse(args.Where(arg => arg != "-convert").ToArray());

                var logPath = FileSystem.GetFullPath(string.IsNullOrEmpty(settings.InputPath) ? "mess-convert.log" : $"{settings.InputPath}.mess-convert.log");
                var logLevel = settings.LogLevel ?? LogLevel.Info;
                using (var logger = new MultiLogger(new ConsoleLogger(logLevel), new FileLogger(logPath, logLevel)))
                {
                    logger.Important($"MESS v{Program.MessVersion}: Macro Entity Substitution System");
                    logger.Important("----- CONVERT MODE -----");
                    logger.Important($"Command line: {Environment.CommandLine}");
                    logger.Important($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
                    logger.Important("");

                    try
                    {
                        // Load input map:
                        Map map;
                        try
                        {
                            logger.Info($"Loading '{settings.InputPath}'.");

                            var fileLoadSettings = CreateFileLoadSettings(settings);
                            map = MapFile.Load(settings.InputPath, fileLoadSettings, logger);
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Failed to load '{settings.InputPath}'.", ex);
                            var innerException = ex.InnerException;
                            while (innerException != null)
                            {
                                logger.Error("Inner exception:", innerException);
                                innerException = innerException.InnerException;
                            }
                            return -1;
                        }


                        // VIS group filtering:
                        ApplyVisGroupFiltering(map, settings, logger);

                        // Cordon area:
                        ApplyCordonSettings(map, settings, logger);

                        // Wad property:
                        if (!string.IsNullOrEmpty(settings.MapWadProperty))
                            map.Properties["wad"] = settings.MapWadProperty;


                        // Save output map:
                        try
                        {
                            logger.Info($"Saving to '{settings.OutputPath}'.");

                            var fileSaveSettings = CreateFileSaveSettings(settings);
                            MapFile.Save(map, settings.OutputPath, fileSaveSettings, logger);

                            logger.Info($"Map saved. Map contains {map.WorldGeometry.Count} brushes and {map.Entities.Count} entities.");
                            return 0;
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Failed to save '{settings.OutputPath}'.", ex);
                            var innerException = ex.InnerException;
                            while (innerException != null)
                            {
                                logger.Error("Inner exception:", innerException);
                                innerException = innerException.InnerException;
                            }
                            return -1;
                        }
                    }
                    finally
                    {
                        logger.Important("");
                        logger.Important($"Finished in {stopwatch.ElapsedMilliseconds / 1000f:0.##} seconds.");
                        logger.Important("");
                        logger.Important("----- END MESS -----");
                    }
                }
            }
            catch (Exception ex)
            {
                using (var errorLogger = new MultiLogger(new ConsoleLogger(LogLevel.Error), new FileLogger(FileSystem.GetFullPath("mess-convert.log"), LogLevel.Error)))
                    errorLogger.Error($"A problem has occurred: {ex.GetType().Name}: '{ex.Message}'.");

                ShowHelp(commandLineParser);
                return -1;
            }
        }

        /// <summary>
        /// Returns a command-line parser that will fill the given settings object when it parses command-line arguments.
        /// </summary>
        private static CommandLine GetCommandLineParser(ConvertSettings settings)
        {
            return new CommandLine()
                // Input-related options:
                .Section("Input:")
                .Option(
                    "-duplicatekeys",
                    s => settings.DuplicateKeys =  ParseOption<DuplicateKeyHandling>(s),
                    $"How to handle duplicate keys in the input file. Valid options are: {GetOptions<DuplicateKeyHandling>()}. Default behavior is {ToString(DuplicateKeyHandling.UseFirst)}.")
                .Option(
                    "-rmfspawnflagproperty",
                    s => settings.RmfSpawnflagProperty = ParseOption<RmfSpawnflagsPropertyHandling>(s),
                    $"Decides whether 'spawnflags' properties in .rmf files should be ignored (Hammer) or used (J.A.C.K.). Valid options are: {GetOptions<RmfSpawnflagsPropertyHandling>()}. Default behavior is {ToString(RmfSpawnflagsPropertyHandling.Use)}.")
                .Option(
                    "-tbfuncgroup",
                    s => settings.TrenchBroomFuncGroup = ParseOption<TrenchbroomGroupHandling>(s),
                    $"Decides whether func_group entities in TrenchBroom .map files should be read as groups and VIS groups, or left as entities. Valid options are: {GetOptions<TrenchbroomGroupHandling>()}. Default behavior is {ToString(TrenchbroomGroupHandling.ConvertToGroup)}.")

                // Output-related options:
                .Section("Output:")
                .Option(
                    "-keyvaluetoolong",
                    s => settings.KeyValueTooLong = ParseOption<ValueTooLongHandling>(s),
                    $"How to handle keys or values that are too long for the output format. Valid options are: {GetOptions<ValueTooLongHandling>()}. Default behavior is {ToString(ValueTooLongHandling.Fail)}.")
                .Option(
                    "-texturenametoolong",
                    s => settings.TextureNameTooLong = ParseOption<ValueTooLongHandling>(s),
                    $"How to handle texture names that are too long for the output format. Valid options are: {GetOptions<ValueTooLongHandling>()}. Default behavior is {ToString(ValueTooLongHandling.Fail)}.")
                .Option(
                    "-invalidkeyvalue",
                    s => settings.InvalidKeyValue = ParseOption<InvalidCharacterHandling>(s),
                    $"How to handle keys or values that contain invalid characters (double quotes). Valid options are: {GetOptions<InvalidCharacterHandling>()}. Default behavior is {ToString(InvalidCharacterHandling.Replace)}.")
                .Option(
                    "-keyvaluereplacement",
                    s => settings.KeyValueReplacement = s,
                    "The replacement for invalid characters in keys or values. Default value is ' (single quote).")
                .Option(
                    "-invalidtexturename",
                    s => settings.InvalidTextureName = ParseOption<InvalidCharacterHandling>(s),
                    $"How to handle texture names that contain invalid characters (spaces). Valid options are: {GetOptions<InvalidCharacterHandling>()}. Default behavior is {ToString(InvalidCharacterHandling.Replace)}.")
                .Option(
                    "-texturenamereplacement",
                    s => settings.TextureNameReplacement = s,
                    "The replacement for invalid characters in texture names. Default value is _ (underscore).")
                .Option(
                    "-visgroups",
                    s => settings.VisGroups = ParseOption<TooManyVisGroupsHandling>(s),
                    $"How to handle VIS group assignment if an object is part of multiple VIS groups, but the output format only supports one. Valid options are: {GetOptions<TooManyVisGroupsHandling>()}. Default behavior is {ToString(TooManyVisGroupsHandling.UseFirst)}.")

                // Jmf output:
                .Section("Jmf format output:")
                .Option(
                    "-jmfversion",
                    s => settings.JmfVersion = ParseOption<JmfFileVersion>(s),
                    $"The output .jmf file version. Valid options are: {GetOptions<JmfFileVersion>()}. Default version is {ToString(JmfFileVersion.V122)}.")

                // Rmf output:
                .Section("Rmf format output:")
                .Option(
                    "-rmfversion",
                    s => settings.RmfVersion = ParseOption<RmfFileVersion>(s),
                    $"The output .rmf file version. Valid options are: {GetOptions<RmfFileVersion>()}. Default version is {ToString(RmfFileVersion.V2_2)}.")

                // Map output:
                .Section("Map format output:")
                .Option(
                    "-mapdecimals",
                    s => settings.MapDecimals = Math.Max(0, int.Parse(s)),
                    "The precision of numbers in the output .map file. Should be a positive number. When omitted, the default roundtrip format is used, which may use scientific notation.")
                .Option(
                    "-mapformat",
                    s => settings.MapFormat = ParseOption<MapFileVariant>(s),
                    $"The output .map format. Valid options are: {GetOptions<MapFileVariant>()}. Default format is {ToString(MapFileVariant.Valve220)}.")
                .Option(
                    "-tbgame",
                    s => settings.TrenchBroomGameName = s,
                    "The game name in the output .map file (TrenchBroom format).")
                .Option(
                    "-wad",
                    s => settings.MapWadProperty = s,
                    "This sets the special 'wad' map property in the output .map file.")

                // VIS group filtering:
                .Section("VIS group filtering:")
                .Option(
                    "-onlyvisgroups",
                    s => settings.OnlyVisGroups = ParseCommaSeparatedList(s),
                    "Only objects that belong to one of the listed VIS groups are included in the output map. VIS group names may include wildcards (*). Multiple VIS group names must be separated by commas.")
                .Option(
                    "-notvisgroups",
                    s => settings.NotVisGroups = ParseCommaSeparatedList(s),
                    "Objects that belong to one of the listed VIS groups are excluded from the output map. VIS group names may include wildcards (*). Multiple VIS group names must be separated by commas.")
                .Switch(
                    "-onlyvisgroupobjects",
                    () => settings.OnlyVisGroupObjects = true,
                    "Objects that do not belong to any VIS group are excluded from the output map.")
                .Switch(
                    "-noinvisiblevisgroups",
                    () => settings.NoInvisibleVisGroups = true,
                    "Objects that belong to an invisible VIS group are excluded from the output map.")
                .Switch(
                    "-novisiblevisgroups",
                    () => settings.NoVisibleVisGroups = true,
                    "Objects that belong to a visible VIS group are excluded from the output map.")
                .Switch(
                    "-noomittedlayers",
                    () => settings.NoOmittedLayers = true,
                    "Objects that belong to a TrenchBroom layer that is set to be omitted from export are excluded from the output map.")

                // Cordoning:
                .Section("Cordon area:")
                .Option(
                    "-cordonarea",
                    s => settings.CordonArea = ParseBoundingBox(s),
                    "Exclude anything outside the specified cordon area. Format is \"x1 y1 z1 x2 y2 z2\".")
                .Switch(
                    "-jmfcordonarea",
                    () => settings.UseJmfCordonArea = true,
                    "Exclude anything outside the cordon area defined in the input .jmf file, if it contains a cordon area.")
                .Option(
                    "-cordontexture",
                    s => settings.CordonTexture = s,
                    "The texture that will be applied to the cordon brushes. Default value is \"BLACK\".")
                .Option(
                    "-cordonpadding",
                    s => settings.CordonPadding = float.Parse(s),
                    "How far the cordon brushes should extend beyond the map's leftover content. Default value is 16. This is the default behavior, which matches how Hammer creates cordon brushes.")
                .Option(
                    "-cordonthickness",
                    s => settings.CordonThickness = float.Parse(s),
                    "This setting gives cordon brushes a fixed thickness. This may cause leaks because not all of the map's leftover content may be covered. This matches J.A.C.K.'s behavior.")

                // Logging:
                .Section("Logging:")
                .Option(
                    "-log",
                    s => { settings.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), s, true); },
                    $"Sets the log level. Valid options are: {GetOptions<LogLevel>()}. Default value is {ToString(LogLevel.Info)}.")

                // Input/output paths:
                .Argument(
                    s => settings.InputPath = FileSystem.GetFullPath(s, Directory.GetCurrentDirectory()),
                    "Input map file. Accepted formats are .map (valve220), .rmf and .jmf.")
                .Argument(
                    s => settings.OutputPath = FileSystem.GetFullPath(s, Directory.GetCurrentDirectory()),
                    "Output map file. If not specified, the input path is used, with the extension changed to .map.");


            string[] ParseCommaSeparatedList(string input)
            {
                return input
                    .Split(',')
                    .Select(part => part.Trim())
                    .ToArray();
            }

            BoundingBox ParseBoundingBox(string input)
            {
                var values = input.Split()
                    .Select(float.Parse)
                    .ToArray();

                return new BoundingBox(
                    new Vector3D(Math.Min(values[0], values[3]), Math.Min(values[1], values[4]), Math.Min(values[2], values[5])),
                    new Vector3D(Math.Max(values[0], values[3]), Math.Max(values[1], values[4]), Math.Max(values[2], values[5])));
            }

            TEnum ParseOption<TEnum>(string input) where TEnum : struct, Enum
                => (TEnum)Enum.Parse(typeof(TEnum), input, true);

            string GetOptions<TEnum>() where TEnum : struct, Enum
                => string.Join(", ", Enum.GetValues<TEnum>().Select(value => value.ToString().ToLowerInvariant()));

            string ToString<TEnum>(TEnum value) where TEnum : struct, Enum
                => value.ToString().ToLowerInvariant();
        }

        private static void ShowHelp(CommandLine commandLine)
        {
            using (var output = Console.OpenStandardOutput())
            using (var writer = new StreamWriter(output, leaveOpen: true))
            {
                writer.WriteLine($"MESS v{Program.MessVersion}: Macro Entity Substitution System");
                writer.WriteLine();
                writer.WriteLine("Conversion mode");
                writer.WriteLine();
                commandLine.ShowDescriptions(writer);
            }
        }


        private static void ApplyVisGroupFiltering(Map map, ConvertSettings settings, ILogger logger)
        {
            var includeNamePattern = CreateWildcardNamesPattern(settings.OnlyVisGroups);
            var excludeNamePattern = CreateWildcardNamesPattern(settings.NotVisGroups);

            var excludedVisGroups = new List<VisGroup>();
            foreach (var visGroup in map.VisGroups)
            {
                if ((excludeNamePattern != null && Regex.IsMatch(visGroup.Name ?? "", excludeNamePattern)) ||
                    (includeNamePattern != null && !Regex.IsMatch(visGroup.Name ?? "", includeNamePattern)) ||
                    (settings.NoInvisibleVisGroups && !visGroup.IsVisible) ||
                    (settings.NoVisibleVisGroups && visGroup.IsVisible) ||
                    (settings.NoOmittedLayers && visGroup is TBLayer tbLayer && tbLayer.IsOmittedFromExport))
                {
                    excludedVisGroups.Add(visGroup);
                }
            }

            foreach (var visGroup in excludedVisGroups)
            {
                logger.Info($"Excluding VIS group '{visGroup.Name}'.");
                map.RemoveVisGroup(visGroup, removeContent: true);
            }

            if (settings.OnlyVisGroupObjects)
            {
                var excludedBrushes = map.WorldGeometry
                    .Where(brush => !brush.GetVisGroups(map.VisGroupAssignment).Any())
                    .ToArray();
                map.RemoveBrushes(excludedBrushes);

                var excludedEntities = map.Entities
                    .Where(entity => !entity.GetVisGroups(map.VisGroupAssignment).Any())
                    .ToArray();
                map.RemoveEntities(excludedEntities);

                logger.Info($"Excluded {excludedBrushes.Length} brushes and {excludedEntities.Length} entities that did not belong to any VIS group.");
            }


            string? CreateWildcardNamesPattern(string[]? wildcardNames)
            {
                if (wildcardNames == null)
                    return null;

                return string.Join("|", wildcardNames.Select(CreateSingleNamePattern));
            }

            string CreateSingleNamePattern(string wildcardName)
            {
                var pattern = Regex.Replace(
                    wildcardName,
                    @"\\\*|\*|[^*]+",
                    match => match.Value switch
                    {
                        @"\*" => Regex.Escape("*"),
                        "*" => ".*",
                        _ => Regex.Escape(match.Value)
                    });

                return "^" + pattern + "$";
            }
        }

        private static void ApplyCordonSettings(Map map, ConvertSettings settings, ILogger logger)
        {
            var cordonArea = settings.CordonArea;
            if (settings.UseJmfCordonArea)
            {
                if (map is JmfMap jmfMap && jmfMap.CordonArea != null)
                {
                    logger.Info("Using cordon area from .jmf input map.");
                    cordonArea = jmfMap.CordonArea;
                }
                else
                {
                    logger.Info("No cordon area found in input map.");
                }
            }

            if (cordonArea == null)
                return;


            logger.Info($"Applying cordon area ({cordonArea.Value.Min} to {cordonArea.Value.Max}).");

            // Remove all brushes and entities whose bounding boxes are completely outside the cordon area.
            // NOTE: This also removes point entities that are exactly at the cordon area's boundaries. Hammer also does this, JACK does not.
            map.RemoveBrushes(map.WorldGeometry.Where(brush => !cordonArea.Value.Touches(brush.BoundingBox)));
            map.RemoveEntities(map.Entities.Where(entity => !cordonArea.Value.Touches(entity.BoundingBox)));


            // Seal off the cordon area:
            BoundingBox cordonOuterArea;
            if (settings.CordonThickness != null)
            {
                // This behavior adds cordon brushes with a fixed thickness. This matches JACK's behavior, but it may cause leaks because not all content may be covered:
                cordonOuterArea = cordonArea.Value.ExpandBy(Math.Max(1, settings.CordonThickness.Value));
            }
            else
            {
                // The default behavior is to cover all leftover map content with cordon brushes, which prevents brush entities
                // that are partially outside the cordon area from causing leaks. This matches Hammer's behavior:
                var mapBoundingBox = cordonArea.Value
                    .CombineWith(map.Worldspawn.BoundingBox)
                    .CombineWith(BoundingBox.FromBoundingBoxes(map.Entities.Select(entity => entity.BoundingBox)));
                cordonOuterArea = mapBoundingBox.ExpandBy(Math.Max(0, settings.CordonPadding ?? 16f));
            }
            var cordonTexture = settings.CordonTexture ?? Textures.Black;
            AddCordonSealingBrushes(map, cordonArea.Value, cordonOuterArea, cordonTexture);
        }

        private static void AddCordonSealingBrushes(Map map, BoundingBox cordonArea, BoundingBox cordonOuterArea, string cordonTexture)
        {
            var inner = cordonArea;
            var outer = cordonOuterArea;

            var cordonBrushes = new[] {
                Shapes.Block(new BoundingBox(new Vector3D(outer.Min.X, outer.Min.Y, inner.Max.Z), new Vector3D(outer.Max.X, outer.Max.Y, outer.Max.Z)), cordonTexture), // Top
                Shapes.Block(new BoundingBox(new Vector3D(outer.Min.X, outer.Min.Y, outer.Min.Z), new Vector3D(outer.Max.X, outer.Max.Y, inner.Min.Z)), cordonTexture), // Bottom
                Shapes.Block(new BoundingBox(new Vector3D(outer.Min.X, inner.Max.Y, inner.Min.Z), new Vector3D(outer.Max.X, outer.Max.Y, inner.Max.Z)), cordonTexture), // Back
                Shapes.Block(new BoundingBox(new Vector3D(outer.Min.X, outer.Min.Y, inner.Min.Z), new Vector3D(outer.Max.X, inner.Min.Y, inner.Max.Z)), cordonTexture), // Front
                Shapes.Block(new BoundingBox(new Vector3D(inner.Max.X, inner.Min.Y, inner.Min.Z), new Vector3D(outer.Max.X, inner.Max.Y, inner.Max.Z)), cordonTexture), // Right
                Shapes.Block(new BoundingBox(new Vector3D(outer.Min.X, inner.Min.Y, inner.Min.Z), new Vector3D(inner.Min.X, inner.Max.Y, inner.Max.Z)), cordonTexture), // Left
            };
            map.AddBrushes(cordonBrushes);
        }


        private static FileLoadSettings CreateFileLoadSettings(ConvertSettings settings)
        {
            var mapFormat = MapFile.GetMapFileFormat(settings.InputPath);
            switch (mapFormat)
            {
                case MapFileFormat.Map: return CreateMapFileLoadSettings(settings);
                case MapFileFormat.Rmf: return CreateRmfFileLoadSettings(settings);
            }

            var fileLoadSettings = new FileLoadSettings();
            SetFileLoadSettings(fileLoadSettings, settings);
            return fileLoadSettings;
        }

        private static MapFileLoadSettings CreateMapFileLoadSettings(ConvertSettings settings)
        {
            var fileLoadSettings = new MapFileLoadSettings();
            SetFileLoadSettings(fileLoadSettings, settings);

            if (settings.TrenchBroomFuncGroup != null) fileLoadSettings.TrenchbroomGroupHandling = settings.TrenchBroomFuncGroup.Value;
            return fileLoadSettings;
        }

        private static RmfFileLoadSettings CreateRmfFileLoadSettings(ConvertSettings settings)
        {
            var fileLoadSettings = new RmfFileLoadSettings();
            SetFileLoadSettings(fileLoadSettings, settings);

            if (settings.RmfSpawnflagProperty != null) fileLoadSettings.SpawnflagsPropertyHandling = settings.RmfSpawnflagProperty.Value;
            return fileLoadSettings;
        }

        private static void SetFileLoadSettings(FileLoadSettings fileLoadSettings, ConvertSettings settings)
        {
            if (settings.DuplicateKeys != null) fileLoadSettings.DuplicateKeyHandling = settings.DuplicateKeys.Value;
        }


        private static FileSaveSettings CreateFileSaveSettings(ConvertSettings settings)
        {
            var mapFormat = MapFile.GetMapFileFormat(settings.OutputPath);
            switch (mapFormat)
            {
                case MapFileFormat.Map: return CreateMapFileSaveSettings(settings);
                case MapFileFormat.Rmf: return CreateRmfFileSaveSettings(settings);
                case MapFileFormat.Jmf: return CreateJmfFileSaveSettings(settings);
            }

            var fileSaveSettings = new FileSaveSettings();
            SetFileSaveSettings(fileSaveSettings, settings);
            return fileSaveSettings;
        }

        private static MapFileSaveSettings CreateMapFileSaveSettings(ConvertSettings settings)
        {
            var fileSaveSettings = new MapFileSaveSettings();
            SetFileSaveSettings(fileSaveSettings, settings);

            if (settings.MapDecimals != null) fileSaveSettings.DecimalsCount = settings.MapDecimals.Value;
            if (settings.MapFormat != null) fileSaveSettings.FileVariant = settings.MapFormat.Value;
            if (settings.TrenchBroomGameName != null) fileSaveSettings.TrenchbroomGameName = settings.TrenchBroomGameName;
            return fileSaveSettings;
        }

        private static RmfFileSaveSettings CreateRmfFileSaveSettings(ConvertSettings settings)
        {
            var fileSaveSettings = new RmfFileSaveSettings();
            SetFileSaveSettings(fileSaveSettings, settings);

            if (settings.RmfVersion != null) fileSaveSettings.FileVersion = settings.RmfVersion.Value;
            return fileSaveSettings;
        }

        private static JmfFileSaveSettings CreateJmfFileSaveSettings(ConvertSettings settings)
        {
            var fileSaveSettings = new JmfFileSaveSettings();
            SetFileSaveSettings(fileSaveSettings, settings);

            if (settings.JmfVersion != null) fileSaveSettings.FileVersion = settings.JmfVersion.Value;
            return fileSaveSettings;
        }

        private static void SetFileSaveSettings(FileSaveSettings fileSaveSettings, ConvertSettings settings)
        {
            if (settings.KeyValueTooLong != null) fileSaveSettings.KeyValueTooLongHandling = settings.KeyValueTooLong.Value;
            if (settings.TextureNameTooLong != null) fileSaveSettings.TextureNameTooLongHandling = settings.TextureNameTooLong.Value;
            if (settings.InvalidKeyValue != null) fileSaveSettings.KeyValueInvalidCharacterHandling = settings.InvalidKeyValue.Value;
            if (settings.KeyValueReplacement != null) fileSaveSettings.KeyValueInvalidCharacterReplacement = settings.KeyValueReplacement;
            if (settings.InvalidTextureName != null) fileSaveSettings.TextureNameInvalidCharacterHandling = settings.InvalidTextureName.Value;
            if (settings.TextureNameReplacement != null) fileSaveSettings.TextureNameInvalidCharacterReplacement = settings.TextureNameReplacement;
            if (settings.VisGroups != null) fileSaveSettings.TooManyVisGroupsHandling = settings.VisGroups.Value;
        }
    }
}
