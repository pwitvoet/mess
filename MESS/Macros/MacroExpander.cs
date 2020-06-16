using MESS.Formats;
using MESS.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MESS.Macros
{
    /// <summary>
    /// Handles macro entity expansion.
    /// </summary>
    public class MacroExpander
    {
        public static Map ExpandMacros(string path)
        {
            var expander = new MacroExpander();
            return expander.ExpandMacros(path, new Dictionary<string, string>());
        }


        private Dictionary<string, Map> _mapTemplateCache = new Dictionary<string, Map>();
        private int _nextInstanceID = 1;

        private MacroExpander()
        {
        }

        /// <summary>
        /// Loads the given map, expands any macros and substitutes placeholders, and returns the result.
        /// </summary>
        public Map ExpandMacros(string mapPath, Dictionary<string, string> substitutions)
        {
            var mapTemplate = GetTemplate(mapPath);

            var instanceID = _nextInstanceID;
            _nextInstanceID += 1;

            // TODO: Substitutions, instance ID and other things should be combined into a single 'context' object!
            var expandedMap = new Map();
            CopyProperties(mapTemplate.Properties, expandedMap.Properties, substitutions, instanceID);

            // NOTE: Because MESS currently only supports outputting to MAP format, there's no need to copy groups, VIS-groups and cameras.
            //       We'll also expand paths into their actual entities, just in case they generate macro entities.

            // Brushwork can be copied as-is:
            expandedMap.WorldGeometry.AddRange(mapTemplate.WorldGeometry.Select(CopyBrush));

            // Entities and paths require handling:
            var allEntities = mapTemplate.Entities
                .Concat(mapTemplate.Paths.SelectMany(path => path.GenerateEntities()))
                .Select(entity => CopyEntity(entity, substitutions, instanceID));

            var workingDirectory = System.IO.Path.GetDirectoryName(NormalizePath(mapPath));
            foreach (var entity in allEntities)
                GetEntityHandler(entity).Process(entity, expandedMap, workingDirectory, this);

            return expandedMap;
        }


        /// <summary>
        /// Returns a template map. Templates are cached, and should not be modified directly.
        /// </summary>
        private Map GetTemplate(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (!_mapTemplateCache.TryGetValue(normalizedPath, out var mapTemplate))
            {
                mapTemplate = MapFile.Load(normalizedPath);
                _mapTemplateCache[normalizedPath] = mapTemplate;
            }
            return mapTemplate;
        }


        private static Dictionary<string, IMacroEntityHandler> _macroEntityHandlers;
        private static IMacroEntityHandler _defaultEntityHandler;

        static MacroExpander()
        {
            _macroEntityHandlers = typeof(IMacroEntityHandler).Assembly.GetTypes()
                .Where(type => !type.IsInterface && !type.IsAbstract && typeof(IMacroEntityHandler).IsAssignableFrom(type))
                .Select(type => (IMacroEntityHandler)Activator.CreateInstance(type))
                .Where(handler => !string.IsNullOrEmpty(handler.EntityName))
                .ToDictionary(handler => handler.EntityName, handler => handler);

            _defaultEntityHandler = new DefaultEntityHandler();
        }

        private static IMacroEntityHandler GetEntityHandler(Entity entity) => _macroEntityHandlers.TryGetValue(entity.ClassName, out var handler) ? handler : _defaultEntityHandler;

        private static void CopyProperties(Dictionary<string, string> source, Dictionary<string, string> destination, Dictionary<string, string> substitutions, int instanceID)
        {
            foreach (var property in source)
            {
                var key = SubstitutePlaceholders(property.Key, substitutions, instanceID);
                var value = SubstitutePlaceholders(property.Value, substitutions, instanceID);

                destination[key] = value;
            }
        }

        private static Entity CopyEntity(Entity entity, Dictionary<string, string> substitutions, int instanceID)
        {
            var copy = new Entity();

            CopyProperties(entity.Properties, copy.Properties, substitutions, instanceID);
            copy.Brushes.AddRange(entity.Brushes.Select(CopyBrush));

            return copy;
        }

        private static Brush CopyBrush(Brush brush)
        {
            // NOTE: Group and VIS-group are not copied because we're only outputting to MAP format. Copying them cannot be done in this method anyway, it'll require an extra pass.
            return new Brush(brush.Faces.Select(CopyFace)) {
                Color = brush.Color,
            };
        }

        private static Face CopyFace(Face face)
        {
            var copy = new Face();

            copy.Vertices.AddRange(face.Vertices);
            copy.PlanePoints = face.PlanePoints.ToArray();
            copy.Plane = face.Plane;

            copy.TextureName = face.TextureName;
            copy.TextureRightAxis = face.TextureRightAxis;
            copy.TextureDownAxis = face.TextureDownAxis;
            copy.TextureShift = face.TextureShift;
            copy.TextureAngle = face.TextureAngle;
            copy.TextureScale = face.TextureScale;

            return copy;
        }

        private static string NormalizePath(string path) => System.IO.Path.GetFullPath(path);

        // TODO: Case-sensitive? (by default, yes -- could be turned into a setting?).
        // TODO: Whitespace-sensitive? (by default, also yes).
        /// <summary>
        /// Replaces placeholders in a given property key or value. If the substitutions dictionary does not contain a placeholder's name,
        /// the placeholder's default value is used, or an empty string if no default value is specified.
        /// <para>
        /// Placeholders are deliniated by curly braces. For example, "{placeholder-name}_01" is turned into "substitution-value_01".
        /// Default values can be specified after an = sign, for example "{placeholder-name=default-value}".
        /// </para>
        /// </summary>
        private static string SubstitutePlaceholders(string input, Dictionary<string, string> substitutions, int instanceID)
        {
            return Regex.Replace(input, @"{(?<key>[^=}]+)(?:=(?<defaultvalue>[^}]+))?}", match =>
            {
                var key = match.Groups["key"].Value;
                var defaultValue = match.Groups["defaultvalue"].Value;

                // TODO: Replace this with a proper expression evaluation system! For now, just checking for 'id()' will do,
                //       but later it'll need to be a function that returns either the targetname of the inserting entity or the instance ID:
                var id = substitutions.TryGetValue("targetname", out var targetname) ? targetname : instanceID.ToString();
                if (key == "id()")
                    return id;

                if (defaultValue == "id()")
                    defaultValue = id;

                return substitutions.TryGetValue(key, out var value) ? value : defaultValue;
            });
        }
    }
}
