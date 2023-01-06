﻿namespace MESS.EntityRewriting
{
    /// <summary>
    /// Entity rewrite rules are used to modify matching entities immediately after a map file is loaded.
    /// The intended use-case is to turn custom entities into macro entities that reference a specific template map.
    /// However, rewrite rules can be used to overwrite or delete any attribute, not just the classname.
    /// </summary>
    public class RewriteDirective
    {
        /// <summary>
        /// The entity classname(s) that this directive applies to. If empty, then this rewrite directive will be applied to all entities.
        /// </summary>
        public string[] ClassNames { get; internal set; } = Array.Empty<string>();

        /// <summary>
        /// If not null, then this rewrite directive will only be applied to an entity if it matches this condition.
        /// </summary>
        public string? Condition { get; internal set; }

        /// <summary>
        /// The directory (or .mtb path) of the .ted file where this rewrite directive was read from.
        /// </summary>
        public string Directory { get; internal set; } = "";

        public List<RuleGroup> RuleGroups { get; } = new();


        /// <summary>
        /// Rewrite rules are always grouped together.
        /// Conditional groups are only applied when their condition evaluates to true.
        /// The condition can (and typically will) contain MScript expressions.
        /// If the evaluated result is an empty string or a 0, then the alternate rules will be applied instead.
        /// </summary>
        public class RuleGroup
        {
            public string? Condition { get; }
            public bool HasCondition => Condition != null;

            public List<Rule> Rules { get; } = new();
            public List<Rule> AlternateRules { get; } = new();


            public RuleGroup(string? condition = null)
            {
                Condition = condition;
            }
        }


        /// <summary>
        /// Rewrite rules can either remove attributes or specify new values for them.
        /// Both the attribute name and value can contain MScript expressions.
        /// </summary>
        public class Rule
        {
            public string Attribute { get; }
            public string? NewValue { get; }
            public bool DeleteAttribute => NewValue == null;


            public Rule(string attribute, string? newValue)
            {
                Attribute = attribute;
                NewValue = newValue;
            }
        }
    }
}
