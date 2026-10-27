// OPTIONAL FILE. Delete it (or define PROJECT_NAVIGATOR_NO_INTERNALS) for Asset Store builds:
// the store's submission guidelines (2.5.g) forbid reflection into Unity Editor internals.
// Without this file the tool still works; it only loses "which folder is the Project window
// browsing" tracking (folder visits are then recorded from selection changes) and uses the
// public OpenAsset path to enter folders.
// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

#if !PROJECT_NAVIGATOR_NO_INTERNALS
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectNavigator
{
    internal static partial class ProjectBrowserBridge
    {
        const BindingFlags k_Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static Type s_BrowserType;
        static FieldInfo s_LastBrowser;
        static MethodInfo s_ShowFolderContents;     // ProjectBrowser.ShowFolderContents(EntityId, bool)
        static MethodInfo s_TryGetActiveFolderPath; // ProjectWindowUtil.TryGetActiveFolderPath(out string)
        static bool s_WarnedShowFolder;
        static bool s_WarnedActivePath;

        static partial void QueryInternals(ref bool canShowFolder, ref bool canReadActiveFolder)
        {
            try
            {
                s_BrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
                if (s_BrowserType != null)
                {
                    s_LastBrowser = s_BrowserType.GetField("s_LastInteractedProjectBrowser", k_Any);
                    s_ShowFolderContents = s_BrowserType.GetMethods(k_Any).FirstOrDefault(m =>
                        m.Name == "ShowFolderContents" &&
                        m.GetParameters().Length == 2 &&
                        m.GetParameters()[1].ParameterType == typeof(bool));
                }

                s_TryGetActiveFolderPath = typeof(ProjectWindowUtil).GetMethods(k_Any).FirstOrDefault(m =>
                    m.Name == "TryGetActiveFolderPath" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].IsOut);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Navigator] Could not resolve editor internals: {e.Message}");
            }

            canShowFolder = s_ShowFolderContents != null;
            canReadActiveFolder = s_TryGetActiveFolderPath != null;
        }

        static partial void TryGetActiveFolderPathInternal(ref string path, ref bool ok)
        {
            if (s_TryGetActiveFolderPath == null) return;
            try
            {
                var args = new object[] { null };
                ok = (bool)s_TryGetActiveFolderPath.Invoke(null, args);
                path = args[0] as string;
            }
            catch (Exception e)
            {
                ok = false;
                if (!s_WarnedActivePath)
                {
                    s_WarnedActivePath = true;
                    Debug.LogWarning($"[Project Navigator] Active-folder tracking unavailable on this Unity version: {e.Message}");
                }
            }
        }

        static partial void ShowFolderInternal(Object folder, ref bool handled)
        {
            if (s_ShowFolderContents == null) return;
            try
            {
                var browser = s_LastBrowser?.GetValue(null) as EditorWindow;
                if (browser == null && s_BrowserType != null)
                    browser = Resources.FindObjectsOfTypeAll(s_BrowserType).FirstOrDefault() as EditorWindow;
                if (browser == null) return;

                var idType = s_ShowFolderContents.GetParameters()[0].ParameterType;
                var idArg = ConvertIdArgument(folder, idType);
                if (idArg == null) return;

                s_ShowFolderContents.Invoke(browser, new[] { idArg, (object)true });
                handled = true;
            }
            catch (Exception e)
            {
                if (!s_WarnedShowFolder)
                {
                    s_WarnedShowFolder = true;
                    Debug.LogWarning($"[Project Navigator] Internal folder navigation failed, using the public path instead: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Unity replaced int instance ids with the EntityId struct (GetInstanceID is an error-level
        /// obsolete from 6000.6). The internal method may still be declared with either type, so the
        /// id is converted to whatever the current signature expects, via the struct's own operators.
        /// </summary>
        static object ConvertIdArgument(Object obj, Type parameterType)
        {
#if UNITY_6000_3_OR_NEWER
            object id = obj.GetEntityId();
#else
            object id = obj.GetInstanceID();
#endif
            var idType = id.GetType();
            if (parameterType.IsAssignableFrom(idType)) return id;

            foreach (var type in new[] { parameterType, idType })
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (method.Name != "op_Implicit" && method.Name != "op_Explicit") continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == idType && method.ReturnType == parameterType)
                        return method.Invoke(null, new[] { id });
                }
            }

            var ctor = parameterType.GetConstructor(new[] { idType });
            return ctor != null ? ctor.Invoke(new[] { id }) : null;
        }
    }
}
#endif
