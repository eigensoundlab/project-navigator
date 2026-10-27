// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectNavigator
{
    /// <summary>
    /// Glob matching for color rules. Supported syntax:
    /// <c>*</c> = any run of characters except '/', <c>?</c> = one character except '/',
    /// <c>**/</c> = zero or more directories, <c>**</c> = anything. Case-insensitive.
    /// </summary>
    internal static class GlobMatcher
    {
        static readonly Dictionary<string, Regex> s_Cache = new Dictionary<string, Regex>();

        public static bool IsMatch(string pattern, string path)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(path)) return false;
            pattern = pattern.Replace('\\', '/').Trim();
            if (!s_Cache.TryGetValue(pattern, out var regex))
            {
                regex = Compile(pattern);
                s_Cache[pattern] = regex;
            }
            return regex != null && regex.IsMatch(path.Replace('\\', '/'));
        }

        public static void ClearCache()
        {
            s_Cache.Clear();
        }

        static Regex Compile(string glob)
        {
            try
            {
                var sb = new StringBuilder("^");
                for (int i = 0; i < glob.Length; i++)
                {
                    char c = glob[i];
                    if (c == '*')
                    {
                        if (i + 1 < glob.Length && glob[i + 1] == '*')
                        {
                            if (i + 2 < glob.Length && glob[i + 2] == '/')
                            {
                                sb.Append("(?:.*/)?");
                                i += 2;
                            }
                            else
                            {
                                sb.Append(".*");
                                i += 1;
                            }
                        }
                        else
                        {
                            sb.Append("[^/]*");
                        }
                    }
                    else if (c == '?')
                    {
                        sb.Append("[^/]");
                    }
                    else
                    {
                        sb.Append(Regex.Escape(c.ToString()));
                    }
                }
                sb.Append('$');
                return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch
            {
                return null;
            }
        }
    }
}
