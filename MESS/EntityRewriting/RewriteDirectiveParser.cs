﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MESS.EntityRewriting
{
    /// <summary>
    /// Searches .fgd files for MESS entity rewrite rules.
    /// Rewrite rules are embedded in comments, so they do not cause conflicts with other tools.
    /// They are associated with the first entity definition that follows.
    /// <para>
    /// Rewrite directives start with a '@MESS REWRITE:' line and end with a '@MESS;' line, or when the entity definition starts.
    /// Directives can contain rewrite rules and if/else blocks.
    /// Rewrite rules take the form '"attribute-name": "new-value"' or 'delete "attribute-name"'.
    /// If/else blocks start with an '@IF "condition":' line and end with an '@ENDIF;' line - else blocks start with an '@ELSE:' line.
    /// Anything else on a line, and any line that does not contain one of the above patterns, is discarded as a comment.
    /// </para>
    /// </summary>
    public static class RewriteDirectiveParser
    {
        public static IEnumerable<RewriteDirective> ParseRewriteDirectives(string path)
        {
            var input = File.ReadAllText(path);
            var tokens = FgdTokenizer.Tokenize(input);

            using (var context = new Context(tokens.Where(token => token.Type != FgdTokenizer.TokenType.Comment)))
            {
                while (!context.IsExhausted)
                {
                    foreach (var rewriteDirective in ParseEntityRewriteDirectives(context))
                        yield return rewriteDirective;
                }
            }
        }


        /// <summary>
        /// Parses the next entity definition, but only returns the MESS rewrite directives that are associated with it.
        /// Returns an empty sequence if there are no directives associated with the entity definition.
        /// </summary>
        private static IEnumerable<RewriteDirective> ParseEntityRewriteDirectives(Context context)
        {
            // Rewrite directives (optional, MESS-specific):
            var rewriteDirectives = new List<RewriteDirective>();
            while (context.Current.Type == FgdTokenizer.TokenType.MessDirective)
                rewriteDirectives.Add(ParseRewriteDirective(context));


            // Entity type (@BaseClass, @SolidClass or @PointClass):
            Expect(context, FgdTokenizer.TokenType.Name);

            // Entity properties:
            while (context.Current.Type == FgdTokenizer.TokenType.Name)
            {
                // Property name:
                Expect(context, FgdTokenizer.TokenType.Name);

                // Property arguments:
                Expect(context, FgdTokenizer.TokenType.ParensOpen);
                while (context.Current.Type == FgdTokenizer.TokenType.Minus ||
                    context.Current.Type == FgdTokenizer.TokenType.Integer ||
                    context.Current.Type == FgdTokenizer.TokenType.String ||
                    context.Current.Type == FgdTokenizer.TokenType.Name ||
                    context.Current.Type == FgdTokenizer.TokenType.Comma)
                {
                    context.MoveNext();
                }
                Expect(context, FgdTokenizer.TokenType.ParensClose);
            }

            // Entity name:
            Expect(context, FgdTokenizer.TokenType.Assignment);
            var entityName = Expect(context, FgdTokenizer.TokenType.Name).Value;

            // Description (optional):
            if (context.Current.Type == FgdTokenizer.TokenType.Colon)
            {
                Expect(context, FgdTokenizer.TokenType.Colon);
                Expect(context, FgdTokenizer.TokenType.String);
            }

            // Entity attributes:
            Expect(context, FgdTokenizer.TokenType.BracketOpen);
            while (context.Current.Type != FgdTokenizer.TokenType.BracketClose)
            {
                // Attribute name & data type:
                Expect(context, FgdTokenizer.TokenType.Name);
                Expect(context, FgdTokenizer.TokenType.ParensOpen);
                Expect(context, FgdTokenizer.TokenType.Name);
                Expect(context, FgdTokenizer.TokenType.ParensClose);

                // Description (optional):
                if (context.Current.Type == FgdTokenizer.TokenType.Colon)
                {
                    Expect(context, FgdTokenizer.TokenType.Colon);
                    Expect(context, FgdTokenizer.TokenType.String);
                }

                // Default value (optional):
                if (context.Current.Type == FgdTokenizer.TokenType.Colon)
                {
                    Expect(context, FgdTokenizer.TokenType.Colon);
                    ExpectValue(context);
                }

                // Options list (optional):
                if (context.Current.Type == FgdTokenizer.TokenType.Assignment)
                {
                    Expect(context, FgdTokenizer.TokenType.Assignment);
                    Expect(context, FgdTokenizer.TokenType.BracketOpen);
                    while (context.Current.Type != FgdTokenizer.TokenType.BracketClose)
                    {
                        // Value and description:
                        ExpectValue(context);
                        Expect(context, FgdTokenizer.TokenType.Colon);
                        Expect(context, FgdTokenizer.TokenType.String);

                        // Initial state (optional):
                        if (context.Current.Type == FgdTokenizer.TokenType.Colon)
                        {
                            Expect(context, FgdTokenizer.TokenType.Colon);
                            Expect(context, FgdTokenizer.TokenType.Integer);
                        }
                    }
                    Expect(context, FgdTokenizer.TokenType.BracketClose);
                }
            }
            Expect(context, FgdTokenizer.TokenType.BracketClose);

            foreach (var directive in rewriteDirectives)
                directive.ClassName = entityName;
            return rewriteDirectives;
        }

        private static RewriteDirective ParseRewriteDirective(Context context)
        {
            Assert(context.Current.Type == FgdTokenizer.TokenType.MessDirective);

            var directiveMatch = Regex.Match(context.Current.Value, @"@MESS\s+(?<directive>\w+)\s*:").Groups["directive"];
            if (!directiveMatch.Success)
                throw ParseError(context, $"Invalid MESS directive format: '{context.Current.Value}'.");

            if (directiveMatch.Value != "REWRITE")
                throw ParseError(context, $"Unknown MESS directive: '{directiveMatch.Value}'.");

            var rewriteDirective = new RewriteDirective();
            RewriteDirective.RuleGroup currentGroup = null;
            var insideElseBlock = false;

            context.MoveNext();
            while (!context.IsExhausted && context.Current.Type == FgdTokenizer.TokenType.MessDirective)
            {
                var currentLine = context.Current.Value;
                context.MoveNext();

                if (Regex.IsMatch(currentLine, @"^\s*@MESS\s*;"))
                {
                    // @MESS; marks the end of a directive:
                    break;
                }
                else if (Regex.IsMatch(currentLine, @"^\s*@ELSE\s*:"))
                {
                    // @ELSE: marks an alternate rules block:
                    if (currentGroup?.Condition == null)
                        throw ParseError(context, $"@ELSE token found outside IF block!");
                    if (insideElseBlock)
                        throw ParseError(context, $"@ELSE token found in ELSE block!");

                    insideElseBlock = true;
                }
                else if (Regex.IsMatch(currentLine, @"^\s*@ENDIF\s*;"))
                {
                    // @ENDIF; marks the end of an IF or IF/ELSE block:
                    if (currentGroup?.Condition == null)
                        throw ParseError(context, $"@ENDIF token found outside IF block!");

                    rewriteDirective.RuleGroups.Add(currentGroup);
                    currentGroup = null;
                    insideElseBlock = false;
                }
                else
                {
                    // @IF "condition": marks the start of an IF block:
                    var ifConditionMatch = Regex.Match(currentLine, @"^\s*@IF\s+""(?<condition>[^""]*)""\s*:");
                    if (ifConditionMatch.Success)
                    {
                        if (currentGroup?.Condition != null)
                            throw ParseError(context, $"@IF token found inside IF block!");

                        if (currentGroup?.Rules.Any() == true || currentGroup?.AlternateRules.Any() == true)
                            rewriteDirective.RuleGroups.Add(currentGroup);

                        var condition = ifConditionMatch.Groups["condition"].Value;
                        currentGroup = new RewriteDirective.RuleGroup(condition);
                        insideElseBlock = false;
                        continue;
                    }

                    // "attributename": "value" marks a rewrite rule:
                    var attributeNameValueMatch = Regex.Match(currentLine, @"^\s*""(?<attributename>[^""]*)""\s*:\s*""(?<value>[^""]*)""");
                    if (attributeNameValueMatch.Success)
                    {
                        if (currentGroup == null)
                            currentGroup = new RewriteDirective.RuleGroup();

                        var rewriteRule = new RewriteDirective.Rule(
                            attributeNameValueMatch.Groups["attributename"].Value,
                            attributeNameValueMatch.Groups["value"].Value,
                            false);
                        (insideElseBlock ? currentGroup.AlternateRules : currentGroup.Rules).Add(rewriteRule);
                        continue;
                    }

                    // delete "attributename" marks a delete-attribute rule:
                    var deleteAttributeNameMatch = Regex.Match(currentLine, @"^\s*delete\s+""(?<attributename>[^""]*)""");
                    if (deleteAttributeNameMatch.Success)
                    {
                        if (currentGroup == null)
                            currentGroup = new RewriteDirective.RuleGroup();

                        var rewriteRule = new RewriteDirective.Rule(
                            deleteAttributeNameMatch.Groups["attributename"].Value,
                            null,
                            true);
                        (insideElseBlock ? currentGroup.AlternateRules : currentGroup.Rules).Add(rewriteRule);
                        continue;
                    }
                }
            }

            if (currentGroup?.Condition != null || currentGroup?.Rules.Any() == true || currentGroup?.AlternateRules.Any() == true)
                rewriteDirective.RuleGroups.Add(currentGroup);

            return rewriteDirective;
        }

        private static void ExpectValue(Context context)
        {
            var token = Expect(context, FgdTokenizer.TokenType.Integer, FgdTokenizer.TokenType.String, FgdTokenizer.TokenType.Minus);
            if (token.Type == FgdTokenizer.TokenType.Minus)
                Expect(context, FgdTokenizer.TokenType.Integer);
        }


        private static void Assert(bool condition)
        {
            if (!condition)
                throw new InvalidOperationException();
        }

        private static FgdTokenizer.Token Expect(Context context, FgdTokenizer.TokenType expectedType)
        {
            if (context.IsExhausted)
                throw ParseError(context, $"Expected a {expectedType} but found end of input.");

            if (context.Current.Type != expectedType)
                throw ParseError(context, $"Expected a {expectedType} but found a {context.Current.Type}.");

            var token = context.Current;
            context.MoveNext();
            return token;
        }

        private static FgdTokenizer.Token Expect(Context context, params FgdTokenizer.TokenType[] expectedTypes)
        {
            if (context.IsExhausted)
                throw ParseError(context, $"Expected a {string.Join(" or ", expectedTypes)} but found end of input.");

            if (!expectedTypes.Contains(context.Current.Type))
                throw ParseError(context, $"Expected a {string.Join(" or ", expectedTypes)} but found a {context.Current.Type}.");

            var token = context.Current;
            context.MoveNext();
            return token;
        }

        private static Exception ParseError(Context context, string message) => new InvalidDataException(message);


        class Context : IDisposable
        {
            public FgdTokenizer.Token Current { get; private set; }
            public bool IsExhausted { get; private set; }

            private IEnumerator<FgdTokenizer.Token> _tokens;


            public Context(IEnumerable<FgdTokenizer.Token> tokens)
            {
                _tokens = tokens.GetEnumerator();
                MoveNext();
            }

            public void Dispose()
            {
                _tokens?.Dispose();
                _tokens = null;
            }

            public bool MoveNext()
            {
                if (_tokens.MoveNext())
                {
                    Current = _tokens.Current;
                    return true;
                }
                else
                {
                    IsExhausted = true;
                    return false;
                }
            }
        }
    }
}
