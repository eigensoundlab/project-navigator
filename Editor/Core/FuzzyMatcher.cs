// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

namespace ProjectNavigator
{
    /// <summary>
    /// Small subsequence matcher used by the window filter and the search provider.
    /// Every character of the pattern must appear in order in the text; consecutive matches and
    /// word starts score higher. Returns -1 when the pattern does not match.
    /// </summary>
    internal static class FuzzyMatcher
    {
        public static int Score(string pattern, string text)
        {
            if (string.IsNullOrEmpty(pattern)) return 0;
            if (string.IsNullOrEmpty(text)) return -1;

            int score = 0;
            int pi = 0;
            int lastMatch = -2;

            for (int ti = 0; ti < text.Length && pi < pattern.Length; ti++)
            {
                char tc = char.ToLowerInvariant(text[ti]);
                char pc = char.ToLowerInvariant(pattern[pi]);
                if (tc != pc) continue;

                int bonus = 1;
                if (ti == lastMatch + 1) bonus += 3;                                  // consecutive
                if (ti == 0 || IsSeparator(text[ti - 1])) bonus += 4;                 // word start
                else if (char.IsUpper(text[ti]) && !char.IsUpper(text[ti - 1])) bonus += 2; // camelCase boundary

                score += bonus;
                lastMatch = ti;
                pi++;
            }

            return pi < pattern.Length ? -1 : score;
        }

        static bool IsSeparator(char c)
        {
            return c == '/' || c == '\\' || c == ' ' || c == '_' || c == '-' || c == '.';
        }
    }
}
