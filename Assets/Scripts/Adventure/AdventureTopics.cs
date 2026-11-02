using System;
using System.Collections.Generic;
using System.Linq;

namespace RiscVAdventure.Adventure
{
    // Save IDs are stable strings, not enum ordinals. Unknown future topics are retained.
    public static class TopicIds
    {
        public const string Registers = "registers";
        public const string Multiplexer = "multiplexer";
        public const string Alu = "alu";
        public const string Memory = "memory";
        public const string Extender = "extender";
        public const string RegisterFile = "register-file";
        public const string Eintaktprozessor = "eintaktprozessor";
        public const string Mehrtaktprozessor = "mehrtaktprozessor";
    }

    public sealed class TopicDefinition
    {
        public string Id { get; }
        public bool IsRequired { get; }

        public TopicDefinition(string id, bool isRequired = true)
        {
            Id = Guard.Text(id, nameof(id));
            IsRequired = isRequired;
        }
    }

    public sealed class TopicCatalog
    {
        public IReadOnlyList<TopicDefinition> Topics { get; }

        public TopicCatalog(IEnumerable<TopicDefinition> topics)
        {
            if (topics == null) throw new ArgumentNullException(nameof(topics));
            var items = topics.ToArray();
            if (items.Length == 0 || items.Any(t => t == null) ||
                items.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("A catalog requires non-null, unique topics.", nameof(topics));
            Topics = Array.AsReadOnly(items);
        }

        public static TopicCatalog Default => new TopicCatalog(new[]
        {
            new TopicDefinition(TopicIds.Registers), new TopicDefinition(TopicIds.Multiplexer),
            new TopicDefinition(TopicIds.Alu), new TopicDefinition(TopicIds.Memory),
            new TopicDefinition(TopicIds.Extender), new TopicDefinition(TopicIds.RegisterFile),
            new TopicDefinition(TopicIds.Eintaktprozessor), new TopicDefinition(TopicIds.Mehrtaktprozessor)
        });
    }

    internal static class Guard
    {
        internal static string Text(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameter);
            return value.Trim();
        }

        internal static double Percent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Knowledge must be finite.");
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
