using System.Collections.Generic;

namespace DinoNet
{
    /// <summary>
    /// The networking ideas the game teaches. The strings are also what the teacher dashboard
    /// shows, so they must stay identical to the names in dashboard/lib/insights.js.
    /// </summary>
    public static class ConceptCatalog
    {
        public const string Https = "HTTP vs HTTPS";
        public const string GetPost = "GET vs POST";
        public const string SafeData = "Safe data vs private data";
        public const string Passwords = "Passwords are private";
        public const string Pieces = "Packets in pieces";
        public const string Signal = "Signal strength";
        public const string Acks = "Got it! replies";
        public const string Backup = "Backup roads";
        public const string Address = "Addresses";
        public const string Dns = "Phone book (DNS)";
        public const string Fake = "Fake friends";
        public const string Strong = "Strong passwords";
        public const string Firewall = "Firewall";
        public const string Codes = "Secret codes";

        static readonly Dictionary<string, int> s_Level = new Dictionary<string, int>
        {
            { Https, 1 }, { GetPost, 2 }, { SafeData, 3 }, { Passwords, 3 },
            { Pieces, 5 }, { Signal, 5 }, { Acks, 6 }, { Backup, 6 },
            { Address, 7 }, { Dns, 7 }, { Fake, 8 }, { Strong, 8 },
            { Firewall, 9 }, { Codes, 9 },
        };

        static readonly Dictionary<string, string> s_Friendly = new Dictionary<string, string>
        {
            { Https, "choosing the safe connection" },
            { GetPost, "asking and sending" },
            { SafeData, "what is safe to share" },
            { Passwords, "keeping passwords private" },
            { Pieces, "messages travelling in pieces" },
            { Signal, "signal strength" },
            { Acks, "saying \"got it!\"" },
            { Backup, "backup roads" },
            { Address, "addresses" },
            { Dns, "the phone book" },
            { Fake, "fake friends" },
            { Strong, "strong passwords" },
            { Firewall, "firewalls" },
            { Codes, "secret codes" },
        };

        /// <summary>The level that teaches a concept, or 0 if unknown.</summary>
        public static int LevelFor(string concept) => concept != null && s_Level.TryGetValue(concept, out var level) ? level : 0;

        /// <summary>Kid-friendly phrase for a concept, used by the coach.</summary>
        public static string Friendly(string concept) => concept != null && s_Friendly.TryGetValue(concept, out var text) ? text : concept ?? string.Empty;
    }
}
