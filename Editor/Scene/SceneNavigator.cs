// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectNavigator
{
    /// <summary>Resolved view of a pinned/marked scene object. Not serialized.</summary>
    internal sealed class SceneObjectInfo
    {
        public string id;
        public GameObject gameObject;
        public string name = "";
        public string hierarchyPath = "";
        public string sceneGuid = "";
        public string scenePath = "";
        public string sceneName = "";
        public bool sceneLoaded;
        public bool Exists => gameObject != null;
    }

    /// <summary>What the Hierarchy column and the rows need to know about one GameObject.</summary>
    internal sealed class SceneStyle
    {
        public static readonly SceneStyle None = new SceneStyle();

        public bool pinned;
        public bool hasColor;
        public Color color;
        /// <summary>null = no explicit color, "" = explicit "none" (blocks inheritance), else the tag id.</summary>
        public string tagId;
        public string note = "";

        public bool HasNote => !string.IsNullOrEmpty(note);
        public bool Visible => pinned || hasColor || HasNote;
    }

    /// <summary>
    /// Scene objects: pins, colors and notes keyed by <see cref="GlobalObjectId"/> strings
    /// (scene GUID + local file id), which survive scene reloads, editor restarts and Play mode.
    /// Resolution to live GameObjects is done in one batch and cached until the hierarchy changes,
    /// so the Hierarchy column only does dictionary lookups per row.
    /// </summary>
    internal static class SceneNavigator
    {
        static NavigatorUserState User => NavigatorUserState.instance;
        static NavigatorSharedSettings Shared => NavigatorSharedSettings.instance;

        static int s_Stamp;
        static int s_ResolvedStamp = -1;
        static readonly Dictionary<string, GameObject> s_ObjectsById = new Dictionary<string, GameObject>();
        static readonly Dictionary<int, string> s_IdsByInstance = new Dictionary<int, string>();
        static readonly Dictionary<int, SceneStyle> s_ExplicitByInstance = new Dictionary<int, SceneStyle>();

        /// <summary>Increases whenever scenes, the hierarchy or the scene marks changed; UI uses it to refresh lazily.</summary>
        public static int Stamp => s_Stamp;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Invalidate();
        static void OnSceneClosed(Scene scene) => Invalidate();
        static void OnPlayModeChanged(PlayModeStateChange change) => Invalidate();

        public static void Invalidate()
        {
            s_Stamp++;
            EditorApplication.RepaintHierarchyWindow();
        }

        // ------------------------------------------------------------------ identity

        /// <summary>Stable id for a scene object, or an explanation why it cannot be pinned.</summary>
        public static bool TryGetId(GameObject gameObject, out string id, out string error)
        {
            id = null;
            error = null;
            if (gameObject == null)
            {
                error = "No GameObject.";
                return false;
            }
            if (EditorUtility.IsPersistent(gameObject))
            {
                error = "That is an asset, not a scene object; pin it from the Project window.";
                return false;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(gameObject))
            {
                error = "Objects inside an open prefab can't be pinned yet; pin the prefab asset instead.";
                return false;
            }
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            if (gid.identifierType != 2 || gid.targetObjectId == 0)
            {
                error = $"\"{gameObject.name}\" has no stable identity (created at runtime?).";
                return false;
            }
            if (gid.assetGUID.Empty())
            {
                error = "Save the scene first, so its objects get stable ids.";
                return false;
            }
            id = gid.ToString();
            return true;
        }

        static bool TryParse(string id, out GlobalObjectId gid)
        {
            return GlobalObjectId.TryParse(id ?? "", out gid);
        }

        public static string SceneGuidOf(string id)
        {
            return TryParse(id, out var gid) ? gid.assetGUID.ToString() : "";
        }

        // ------------------------------------------------------------------ resolution

        static IEnumerable<string> AllIds()
        {
            return Shared.scenePins.Select(p => p.id)
                .Concat(User.scenePins.Select(p => p.id))
                .Concat(Shared.sceneColors.Select(c => c.guid))
                .Concat(User.sceneColors.Select(c => c.guid))
                .Concat(Shared.sceneNotes.Select(n => n.guid))
                .Concat(User.sceneNotes.Select(n => n.guid));
        }

        static void EnsureResolved()
        {
            if (s_ResolvedStamp == s_Stamp) return;
            s_ResolvedStamp = s_Stamp;
            s_ObjectsById.Clear();
            s_IdsByInstance.Clear();
            s_ExplicitByInstance.Clear();

            var ids = new List<string>();
            var gids = new List<GlobalObjectId>();
            foreach (var id in AllIds().Distinct())
            {
                if (!TryParse(id, out var gid)) continue;
                ids.Add(id);
                gids.Add(gid);
            }
            if (gids.Count == 0) return;

            var outputs = new Object[gids.Count];
            GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(gids.ToArray(), outputs);
            for (int i = 0; i < outputs.Length; i++)
            {
                var go = outputs[i] as GameObject;
                if (go == null) continue;
                s_ObjectsById[ids[i]] = go;
                s_IdsByInstance[go.GetHashCode()] = ids[i];
            }

            foreach (var pair in s_ObjectsById)
            {
                s_ExplicitByInstance[pair.Value.GetHashCode()] = BuildExplicitStyle(pair.Key);
                // Keep the last known name current for the "scene not loaded" display.
                var pin = FindPin(pair.Key, out _);
                if (pin != null && pin.lastName != pair.Value.name) pin.lastName = pair.Value.name;
            }
        }

        static SceneStyle BuildExplicitStyle(string id)
        {
            var style = new SceneStyle { pinned = IsPinned(id), note = GetNote(id) };
            var color = FindExplicitColor(id, out _);
            if (color == null) return style;
            if (string.IsNullOrEmpty(color.tagId))
            {
                style.tagId = "";
                return style;
            }
            var tag = Shared.FindTag(color.tagId);
            if (tag == null) return style;
            style.hasColor = true;
            style.color = tag.color;
            style.tagId = tag.id;
            return style;
        }

        public static GameObject Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureResolved();
            return s_ObjectsById.TryGetValue(id, out var go) && go != null ? go : null;
        }

        /// <summary>Id of a pinned/marked object (only those are indexed); null for any other object.</summary>
        public static string KnownIdOf(GameObject gameObject)
        {
            if (gameObject == null) return null;
            EnsureResolved();
            return s_IdsByInstance.TryGetValue(gameObject.GetHashCode(), out var id) ? id : null;
        }

        /// <summary>Style for a Hierarchy row: explicit marks on the object, color inherited from the nearest colored ancestor.</summary>
        public static SceneStyle StyleFor(GameObject gameObject)
        {
            if (gameObject == null) return SceneStyle.None;
            EnsureResolved();
            if (s_ExplicitByInstance.Count == 0) return SceneStyle.None;

            s_ExplicitByInstance.TryGetValue(gameObject.GetHashCode(), out var explicitStyle);
            if (explicitStyle != null && (explicitStyle.hasColor || explicitStyle.tagId == "")) return explicitStyle;

            var inherited = InheritedColor(gameObject.transform.parent);
            if (inherited == null) return explicitStyle ?? SceneStyle.None;

            return new SceneStyle
            {
                pinned = explicitStyle != null && explicitStyle.pinned,
                note = explicitStyle != null ? explicitStyle.note : "",
                hasColor = true,
                color = inherited.Value
            };
        }

        static Color? InheritedColor(Transform parent)
        {
            while (parent != null)
            {
                if (s_ExplicitByInstance.TryGetValue(parent.gameObject.GetHashCode(), out var style))
                {
                    if (style.tagId == "") return null; // explicit "none" stops inheritance
                    if (style.hasColor)
                    {
                        var tag = Shared.FindTag(style.tagId);
                        return tag != null && tag.inheritToChildren ? style.color : (Color?)null;
                    }
                }
                parent = parent.parent;
            }
            return null;
        }

        public static SceneObjectInfo GetInfo(string id, string fallbackName = "")
        {
            var info = new SceneObjectInfo { id = id, name = fallbackName ?? "" };
            if (TryParse(id, out var gid))
            {
                info.sceneGuid = gid.assetGUID.ToString();
                info.scenePath = AssetDatabase.GUIDToAssetPath(info.sceneGuid);
                info.sceneName = string.IsNullOrEmpty(info.scenePath) ? "(missing scene)" : System.IO.Path.GetFileNameWithoutExtension(info.scenePath);
                info.sceneLoaded = IsSceneLoaded(info.scenePath);
            }
            var go = Resolve(id);
            if (go != null)
            {
                info.gameObject = go;
                info.name = go.name;
                info.hierarchyPath = HierarchyPath(go.transform.parent);
            }
            return info;
        }

        public static bool IsSceneLoaded(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == scenePath) return true;
            }
            return false;
        }

        public static string HierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            while (transform != null)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ------------------------------------------------------------------ actions

        public static void Select(string id)
        {
            var go = Resolve(id);
            if (go == null) return;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        public static void Frame(string id)
        {
            var go = Resolve(id);
            if (go == null) return;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.FrameLastActiveSceneView();
        }

        /// <summary>Opens the object's scene (asking to save first) and selects the object once it is loaded.</summary>
        public static void OpenSceneAndSelect(string id, bool additive)
        {
            var info = GetInfo(id);
            if (string.IsNullOrEmpty(info.scenePath))
            {
                Debug.LogWarning("[Project Navigator] The scene of this pin no longer exists.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Project Navigator] Scenes are not opened from pins during Play mode.");
                return;
            }
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(info.scenePath, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            Invalidate();
            EditorApplication.delayCall += () => Select(id);
        }

        // ------------------------------------------------------------------ pins

        public static IEnumerable<(ScenePin pin, bool shared)> AllPins()
        {
            return Shared.scenePins.Select(p => (p, true)).Concat(User.scenePins.Select(p => (p, false)));
        }

        public static bool IsPinned(string id)
        {
            return !string.IsNullOrEmpty(id) && (Shared.scenePins.Exists(p => p.id == id) || User.scenePins.Exists(p => p.id == id));
        }

        public static ScenePin FindPin(string id, out bool shared)
        {
            var pin = Shared.scenePins.Find(p => p.id == id);
            shared = pin != null;
            return pin ?? User.scenePins.Find(p => p.id == id);
        }

        public static bool Pin(GameObject gameObject, bool shared, out string error)
        {
            if (!TryGetId(gameObject, out var id, out error)) return false;
            if (IsPinned(id)) return true;
            var pin = new ScenePin { id = id, lastName = gameObject.name, sceneGuid = SceneGuidOf(id) };
            (shared ? Shared.scenePins : User.scenePins).Add(pin);
            Commit(shared);
            return true;
        }

        /// <summary>Pins the selected GameObjects; reports the first problem in a dialog.</summary>
        public static void PinSelection(bool shared)
        {
            string firstError = null;
            foreach (var go in Selection.gameObjects)
                if (!Pin(go, shared, out var error) && firstError == null) firstError = error;
            if (firstError != null)
                EditorUtility.DisplayDialog("Project Navigator", firstError, "OK");
        }

        public static void Unpin(string id)
        {
            bool shared = Shared.scenePins.RemoveAll(p => p.id == id) > 0;
            bool user = User.scenePins.RemoveAll(p => p.id == id) > 0;
            if (!shared && !user) return;
            Navigator.MarkDirty(user: user, shared: shared);
            Invalidate();
            Navigator.NotifyChanged();
        }

        public static void SetPinLabel(string id, string label)
        {
            var pin = FindPin(id, out bool shared);
            if (pin == null) return;
            pin.label = (label ?? "").Trim();
            Commit(shared);
        }

        public static void SetPinGroup(string id, string group)
        {
            var pin = FindPin(id, out bool shared);
            if (pin == null) return;
            pin.group = (group ?? "").Trim();
            Commit(shared);
        }

        public static void MovePin(string id, int delta)
        {
            var pin = FindPin(id, out bool shared);
            if (pin == null) return;
            var list = shared ? Shared.scenePins : User.scenePins;
            int from = list.IndexOf(pin);
            int to = Mathf.Clamp(from + delta, 0, list.Count - 1);
            if (from < 0 || to == from) return;
            list.RemoveAt(from);
            list.Insert(to, pin);
            Commit(shared);
        }

        // ------------------------------------------------------------------ colors (tags, like folders)

        public static AssetColor FindExplicitColor(string id, out bool shared)
        {
            var personal = User.sceneColors.Find(c => c.guid == id);
            if (personal != null)
            {
                shared = false;
                return personal;
            }
            shared = true;
            return Shared.sceneColors.Find(c => c.guid == id);
        }

        public static void SetColor(string id, string tagId, bool shared)
        {
            if (string.IsNullOrEmpty(id)) return;
            tagId = tagId ?? "";
            var list = shared ? Shared.sceneColors : User.sceneColors;
            var entry = list.Find(c => c.guid == id);
            if (entry == null) list.Add(new AssetColor { guid = id, tagId = tagId });
            else entry.tagId = tagId;
            bool userTouched = !shared;
            if (shared && User.sceneColors.RemoveAll(c => c.guid == id) > 0) userTouched = true;
            Navigator.MarkDirty(user: userTouched, shared: shared);
            Invalidate();
            Navigator.NotifyChanged();
        }

        public static void ClearColor(string id)
        {
            bool user = User.sceneColors.RemoveAll(c => c.guid == id) > 0;
            bool shared = Shared.sceneColors.RemoveAll(c => c.guid == id) > 0;
            if (!user && !shared) return;
            Navigator.MarkDirty(user: user, shared: shared);
            Invalidate();
            Navigator.NotifyChanged();
        }

        /// <summary>Applies a tag to the selected GameObjects (pins are not required for a color).</summary>
        public static void ColorSelection(string tagId, bool shared)
        {
            string firstError = null;
            foreach (var go in Selection.gameObjects)
            {
                if (TryGetId(go, out var id, out var error)) SetColor(id, tagId, shared);
                else if (firstError == null) firstError = error;
            }
            if (firstError != null) EditorUtility.DisplayDialog("Project Navigator", firstError, "OK");
        }

        public static void ClearColorOnSelection()
        {
            foreach (var go in Selection.gameObjects)
                if (TryGetId(go, out var id, out _)) ClearColor(id);
        }

        // ------------------------------------------------------------------ notes

        public static string GetNote(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var note = User.sceneNotes.Find(n => n.guid == id) ?? Shared.sceneNotes.Find(n => n.guid == id);
            return note == null ? "" : note.text ?? "";
        }

        public static void SetNote(string id, string text, bool shared)
        {
            if (string.IsNullOrEmpty(id)) return;
            text = (text ?? "").Trim();
            if (text.Length == 0)
            {
                ClearNote(id);
                return;
            }
            var list = shared ? Shared.sceneNotes : User.sceneNotes;
            var note = list.Find(n => n.guid == id);
            if (note == null) list.Add(new AssetNote { guid = id, text = text });
            else note.text = text;
            bool userTouched = !shared;
            if (shared && User.sceneNotes.RemoveAll(n => n.guid == id) > 0) userTouched = true;
            Navigator.MarkDirty(user: userTouched, shared: shared);
            Invalidate();
            Navigator.NotifyChanged();
        }

        public static void ClearNote(string id)
        {
            bool user = User.sceneNotes.RemoveAll(n => n.guid == id) > 0;
            bool shared = Shared.sceneNotes.RemoveAll(n => n.guid == id) > 0;
            if (!user && !shared) return;
            Navigator.MarkDirty(user: user, shared: shared);
            Invalidate();
            Navigator.NotifyChanged();
        }

        // ------------------------------------------------------------------ maintenance

        /// <summary>Drops personal entries whose scene asset is gone, or whose object is missing from a loaded scene.</summary>
        public static int PruneMissing()
        {
            EnsureResolved();
            int removed = 0;
            removed += User.scenePins.RemoveAll(p => IsGone(p.id));
            removed += User.sceneColors.RemoveAll(c => IsGone(c.guid));
            removed += User.sceneNotes.RemoveAll(n => IsGone(n.guid));
            if (removed > 0) Commit(false);
            return removed;
        }

        public static int PruneMissingShared()
        {
            EnsureResolved();
            int removed = 0;
            removed += Shared.scenePins.RemoveAll(p => IsGone(p.id));
            removed += Shared.sceneColors.RemoveAll(c => IsGone(c.guid));
            removed += Shared.sceneNotes.RemoveAll(n => IsGone(n.guid));
            if (removed > 0) Commit(true);
            return removed;
        }

        static bool IsGone(string id)
        {
            var info = GetInfo(id);
            if (string.IsNullOrEmpty(info.scenePath)) return true;      // scene asset deleted
            return info.sceneLoaded && !info.Exists;                   // scene open, object deleted
        }

        static void Commit(bool shared)
        {
            Navigator.MarkDirty(user: !shared, shared: shared);
            Invalidate();
            Navigator.NotifyChanged();
        }
    }
}
