namespace Isle.Core.Util
{
    /// <summary>
    /// The one character rule shared by definition IDs and tag segments: SYS-CORE-01 spells both
    /// as <c>[a-z0-9_]+</c>. Kept in one place so the two never drift apart.
    /// </summary>
    public static class IdText
    {
        /// <summary>True when <paramref name="text"/> is a non-empty run of <c>[a-z0-9_]</c>.</summary>
        public static bool IsSegment(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (var i = 0; i < text.Length; i++)
                if (!IsSegmentChar(text[i]))
                    return false;
            return true;
        }

        public static bool IsSegmentChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary>
        /// The first character that breaks the rule, or <c>-1</c>. Error messages quote the
        /// offender rather than restating the pattern — SYS-CORE-01 §Load pipeline asks for
        /// messages a modder can act on.
        /// </summary>
        public static int FirstBadChar(string text)
        {
            for (var i = 0; i < text.Length; i++)
                if (!IsSegmentChar(text[i]))
                    return i;
            return -1;
        }
    }
}
