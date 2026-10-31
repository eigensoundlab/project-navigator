// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor.PackageManager;

namespace ProjectNavigator
{
    /// <summary>Version string for the UI: read from package.json when installed as a package, else the fallback.</summary>
    internal static class NavigatorVersion
    {
        /// <summary>Keep in sync with package.json for installs that copy the Editor folder into Assets/.</summary>
        public const string Fallback = "0.4.0";
        public const string Repository = "https://github.com/eigensoundlab/project-navigator";

        static string s_Cached;

        public static string Current
        {
            get
            {
                if (s_Cached != null) return s_Cached;
                try
                {
                    var info = PackageInfo.FindForAssembly(typeof(NavigatorVersion).Assembly);
                    s_Cached = info != null && !string.IsNullOrEmpty(info.version) ? info.version : Fallback;
                }
                catch
                {
                    s_Cached = Fallback;
                }
                return s_Cached;
            }
        }
    }
}
