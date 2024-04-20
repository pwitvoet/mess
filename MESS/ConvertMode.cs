using MESS.Formats;
using MESS.Formats.JMF;
using MESS.Formats.MAP;
using MESS.Formats.RMF;
using MESS.Logging;
using MESS.Mapping;
using MESS.Util;
using System.Diagnostics;

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


                        // Wad property:
                        if (!string.IsNullOrEmpty(settings.MapWadProperty))
                            map.Properties["wad"] = settings.MapWadProperty;


                        // Save output map:
                        try
                        {
                            logger.Info($"Saving to '{settings.OutputPath}'.");

                            var fileSaveSettings = CreateFileSaveSettings(settings);
                            MapFile.Save(map, settings.OutputPath, fileSaveSettings, logger);
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
                .Option(
                    "-jmfversion",
                    s => settings.JmfVersion = ParseOption<JmfFileVersion>(s),
                    $"The output .jmf file version. Valid options are: {GetOptions<JmfFileVersion>()}. Default version is {ToString(JmfFileVersion.V122)}.")

                // Rmf output:
                .Option(
                    "-rmfversion",
                    s => settings.RmfVersion = ParseOption<RmfFileVersion>(s),
                    $"The output .rmf file version. Valid options are: {GetOptions<RmfFileVersion>()}. Default version is {ToString(RmfFileVersion.V2_2)}.")

                // Map output:
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

                // Other settings:
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
                commandLine.ShowDescriptions(writer);
            }
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
