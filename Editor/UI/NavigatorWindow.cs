// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectNavigator
{
    /// <summary>
    /// The Navigator window: Pinned / Frequent / Recent / Recent files / Colored assets.
    /// Folder rows: select on click, open in the Project window on double-click (or on single click,
    /// see Preferences). File rows: select on click, open the asset on double-click.
    /// Right-click for actions. Drop folders or files onto the window to pin them; drop assets onto
    /// a folder row to move them into that folder.
    ///
    /// Two kinds of instance: a normal (dockable) window and a floating one. The floating one is a
    /// utility window when "stay on top" is enabled, remembers its position and compact state, and
    /// is reopened on startup because Unity does not restore utility windows itself.
    /// </summary>
    internal sealed class NavigatorWindow : EditorWindow
    {
        enum RowKind
        {
            Pin,
            Frequent,
            Recent,
            RecentFile,
            Color
        }

        sealed class Row
        {
            public string guid;
            public string path;
            public string name;
            public string label;
            public string group;
            public string note;
            public bool isFolder;
            public bool exists;
            public bool pinned;
            public bool shared;
            public bool hasColor;
            public Color color;
            public Texture icon;
            public double score;
        }

        const string k_PreferencesPath = "Preferences/Project Navigator";
        const string k_ProjectSettingsPath = "Project/Project Navigator";
        const float k_DragThreshold = 6f;

        static bool s_Quitting;
        static double s_QuitRequestedAt;

        [SerializeField] bool m_IsFloating;

        // ------------------------------------------------------------------ opening

        [MenuItem("Window/Project Navigator/Navigator Window", false, 10)]
        public static void Open()
        {
            var existing = Resources.FindObjectsOfTypeAll<NavigatorWindow>().FirstOrDefault(w => !w.m_IsFloating);
            if (existing != null)
            {
                existing.Show();
                existing.Focus();
                return;
            }
            var window = CreateWindow<NavigatorWindow>();
            window.titleContent = MakeTitle();
            window.Show();
            window.Focus();
        }

        [MenuItem("Window/Project Navigator/Floating Navigator", false, 11)]
        public static void OpenFloating()
        {
            var existing = Resources.FindObjectsOfTypeAll<NavigatorWindow>().FirstOrDefault(w => w.m_IsFloating);
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var user = NavigatorUserState.instance;
            var window = CreateInstance<NavigatorWindow>();
            window.m_IsFloating = true;
            window.titleContent = MakeTitle();
            if (user.floatingOnTop) window.ShowUtility();
            else window.Show();

            var rect = user.floatingRect;
            if (rect.width >= 120f && rect.height >= 60f) window.position = rect;

            user.floatingWasOpen = true;
            Navigator.MarkDirty(user: true);
        }

        static GUIContent MakeTitle()
        {
            return new GUIContent("Navigator", EditorGUIUtility.IconContent("Folder Icon").image);
        }

        [InitializeOnLoadMethod]
        static void RegisterStartupHooks()
        {
            EditorApplication.wantsToQuit -= OnWantsToQuit;
            EditorApplication.wantsToQuit += OnWantsToQuit;
            EditorApplication.update -= QuitFlagWatchdog;
            EditorApplication.update += QuitFlagWatchdog;
            EditorApplication.delayCall += ReopenFloatingIfNeeded;
        }

        static void ReopenFloatingIfNeeded()
        {
            var user = NavigatorUserState.instance;
            if (!user.reopenFloatingOnStartup || !user.floatingWasOpen) return;
            if (Resources.FindObjectsOfTypeAll<NavigatorWindow>().Any(w => w.m_IsFloating)) return;
            OpenFloating();
        }

        static bool OnWantsToQuit()
        {
            // Remember the floating window across restarts: its OnDestroy must not clear the flag.
            s_Quitting = true;
            s_QuitRequestedAt = EditorApplication.timeSinceStartup;
            foreach (var window in Resources.FindObjectsOfTypeAll<NavigatorWindow>())
                if (window.m_IsFloating) NavigatorUserState.instance.floatingRect = window.position;
            Navigator.MarkDirty(user: true);
            Navigator.FlushSaves();
            return true;
        }

        static void QuitFlagWatchdog()
        {
            // If the editor is still running a few seconds after a quit request, the quit was cancelled.
            if (s_Quitting && EditorApplication.timeSinceStartup - s_QuitRequestedAt > 5.0)
                s_Quitting = false;
        }

        // ------------------------------------------------------------------ fields

        ToolbarButton m_Back;
        ToolbarButton m_Forward;
        ToolbarToggle m_Compact;
        ToolbarSearchField m_Search;
        Label m_Current;
        ScrollView m_Scroll;
        Foldout m_PinsFold;
        Foldout m_FrequentFold;
        Foldout m_RecentFold;
        Foldout m_RecentFilesFold;
        Foldout m_ColorsFold;

        string m_Filter = "";
        Row m_DragRow;
        Vector2 m_DragStart;

        bool Compact
        {
            get => m_IsFloating ? NavigatorUserState.instance.floatingCompact : NavigatorUserState.instance.compactMode;
            set
            {
                if (m_IsFloating) NavigatorUserState.instance.floatingCompact = value;
                else NavigatorUserState.instance.compactMode = value;
                Navigator.MarkDirty(user: true);
            }
        }

        // ------------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            Navigator.Changed += OnNavigatorChanged;
        }

        void OnDisable()
        {
            Navigator.Changed -= OnNavigatorChanged;
            if (m_IsFloating)
            {
                NavigatorUserState.instance.floatingRect = position;
                Navigator.MarkDirty(user: true);
            }
        }

        void OnDestroy()
        {
            // Fires on a real close (not on domain reload). Skip during quit so the window comes back next time.
            if (m_IsFloating && !s_Quitting)
            {
                NavigatorUserState.instance.floatingWasOpen = false;
                Navigator.MarkDirty(user: true);
            }
        }

        void OnNavigatorChanged()
        {
            if (m_PinsFold != null) Refresh();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("pn-root");
            root.style.flexGrow = 1;
            LoadStyles(root);

            BuildToolbar(root);

            m_Current = new Label();
            m_Current.AddToClassList("pn-current");
            m_Current.style.marginLeft = 6;
            m_Current.style.marginTop = 2;
            m_Current.style.marginBottom = 2;
            m_Current.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(m_Current);

            m_Scroll = new ScrollView(ScrollViewMode.Vertical);
            m_Scroll.AddToClassList("pn-scroll");
            m_Scroll.style.flexGrow = 1;
            root.Add(m_Scroll);

            m_PinsFold = MakeFoldout("Pinned", true);
            m_FrequentFold = MakeFoldout("Frequent", true);
            m_RecentFold = MakeFoldout("Recent", true);
            m_RecentFilesFold = MakeFoldout("Recent files", true);
            m_ColorsFold = MakeFoldout("Colored", false);
            m_Scroll.Add(m_PinsFold);
            m_Scroll.Add(m_FrequentFold);
            m_Scroll.Add(m_RecentFold);
            m_Scroll.Add(m_RecentFilesFold);
            m_Scroll.Add(m_ColorsFold);

            SetupPinDropTarget(root);
            ApplyCompact(Compact);
            Refresh();
        }

        // ------------------------------------------------------------------ building blocks

        static void LoadStyles(VisualElement root)
        {
            foreach (var guid in AssetDatabase.FindAssets("NavigatorWindow t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("NavigatorWindow.uss", StringComparison.Ordinal)) continue;
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet == null) continue;
                root.styleSheets.Add(sheet);
                return;
            }
        }

        static Foldout MakeFoldout(string text, bool open)
        {
            var foldout = new Foldout { text = text, value = open };
            foldout.AddToClassList("pn-fold");
            return foldout;
        }

        void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();

            m_Back = Fixed(new ToolbarButton(() => Navigator.Back()) { text = "←", tooltip = "Back (Alt+Left)" }, 26f);
            m_Forward = Fixed(new ToolbarButton(() => Navigator.Forward()) { text = "→", tooltip = "Forward (Alt+Right)" }, 26f);
            var up = Fixed(new ToolbarButton(() => Navigator.GoToParent()) { text = "↑", tooltip = "Parent folder (Alt+Up)" }, 26f);
            var pin = Padded(new ToolbarButton(() => Navigator.PinCurrentFolder(false)) { text = "Pin", tooltip = "Pin / unpin the current folder (Alt+Shift+P)" });
            toolbar.Add(m_Back);
            toolbar.Add(m_Forward);
            toolbar.Add(up);
            toolbar.Add(pin);

            m_Search = new ToolbarSearchField();
            m_Search.AddToClassList("pn-search");
            m_Search.style.flexGrow = 1;
            m_Search.style.flexShrink = 1;
            m_Search.style.flexBasis = 120;
            m_Search.style.minWidth = 60;
            m_Search.style.maxWidth = 260;
            m_Search.style.marginLeft = 4;
            m_Search.style.marginRight = 4;
            m_Search.RegisterValueChangedCallback(evt =>
            {
                m_Filter = evt.newValue ?? "";
                Refresh();
            });
            toolbar.Add(m_Search);

            m_Compact = Padded(new ToolbarToggle { text = "Compact", tooltip = "Show only the pinned list" });
            m_Compact.SetValueWithoutNotify(Compact);
            m_Compact.RegisterValueChangedCallback(evt =>
            {
                Compact = evt.newValue;
                ApplyCompact(evt.newValue);
            });
            toolbar.Add(m_Compact);

            if (!m_IsFloating)
                toolbar.Add(Padded(new ToolbarButton(OpenFloating) { text = "Float", tooltip = "Open a floating copy (stays on top when enabled in preferences)" }));

            toolbar.Add(Padded(new ToolbarButton(() => SettingsService.OpenUserPreferences(k_PreferencesPath)) { text = "Prefs", tooltip = "Open preferences (Project Navigator " + NavigatorVersion.Current + ")" }));

            // A gap at the right edge, so the last button never touches the window border.
            var spacer = new VisualElement();
            spacer.style.width = 2;
            toolbar.Add(spacer);

            root.Add(toolbar);
        }

        /// <summary>Text buttons get symmetric padding and never shrink; the search field yields instead.</summary>
        static T Padded<T>(T element) where T : VisualElement
        {
            element.style.paddingLeft = 8;
            element.style.paddingRight = 8;
            element.style.flexShrink = 0;
            element.style.unityTextAlign = TextAnchor.MiddleCenter;
            return element;
        }

        static T Fixed<T>(T element, float width) where T : VisualElement
        {
            element.style.width = width;
            element.style.minWidth = width;
            element.style.flexShrink = 0;
            element.style.unityTextAlign = TextAnchor.MiddleCenter;
            return element;
        }

        void ApplyCompact(bool compact)
        {
            var display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (m_FrequentFold != null) m_FrequentFold.style.display = display;
            if (m_RecentFold != null) m_RecentFold.style.display = display;
            if (m_ColorsFold != null) m_ColorsFold.style.display = display;
            if (m_RecentFilesFold != null)
                m_RecentFilesFold.style.display = compact || !NavigatorUserState.instance.trackFiles ? DisplayStyle.None : DisplayStyle.Flex;
            minSize = compact ? new Vector2(200f, 80f) : new Vector2(260f, 200f);
        }

        void SetupPinDropTarget(VisualElement root)
        {
            root.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DraggedAssetPaths(foldersToo: true).Count > 0)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });
            root.RegisterCallback<DragPerformEvent>(evt =>
            {
                var paths = DraggedAssetPaths(foldersToo: true);
                if (paths.Count == 0) return;
                DragAndDrop.AcceptDrag();
                foreach (var path in paths)
                    Navigator.Pin(Navigator.GuidOf(path), false);
            });
        }

        /// <summary>Asset paths in the current drag. Without <paramref name="foldersToo"/> only movable files under Assets/.</summary>
        static List<string> DraggedAssetPaths(bool foldersToo)
        {
            var paths = DragAndDrop.paths ?? Array.Empty<string>();
            var result = new List<string>();
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) continue;
                bool isFolder = AssetDatabase.IsValidFolder(path);
                if (foldersToo) result.Add(path);
                else if (!isFolder && path.StartsWith("Assets/", StringComparison.Ordinal)) result.Add(path);
            }
            return result;
        }

        // ------------------------------------------------------------------ refresh

        void Refresh()
        {
            var user = NavigatorUserState.instance;

            m_Back.SetEnabled(Navigator.CanGoBack);
            m_Forward.SetEnabled(Navigator.CanGoForward);

            var currentInfo = Navigator.GetInfo(Navigator.CurrentFolderGuid());
            m_Current.text = currentInfo.Exists ? currentInfo.path : "—";
            m_Current.tooltip = m_Current.text;

            FillSection(m_PinsFold, "Pinned", BuildPinRows(), RowKind.Pin,
                "Drop folders or files here, or right-click an asset → Project Navigator → Pin.");
            FillSection(m_FrequentFold, "Frequent", BuildFrequentRows(user.frequentCount), RowKind.Frequent,
                "Folders you use most will show up here.");
            FillSection(m_RecentFold, "Recent", BuildRecentRows(user.recentCount), RowKind.Recent,
                "Nothing visited yet.");
            FillSection(m_RecentFilesFold, "Recent files", BuildRecentFileRows(user.recentFilesCount), RowKind.RecentFile,
                "Files you select will show up here.");
            m_RecentFilesFold.style.display = Compact || !user.trackFiles ? DisplayStyle.None : DisplayStyle.Flex;
            FillColors();
        }

        void FillSection(Foldout foldout, string title, List<Row> rows, RowKind kind, string emptyText)
        {
            foldout.Clear();
            int shown = 0;
            foreach (var row in rows)
            {
                if (!PassesFilter(row)) continue;
                foldout.Add(MakeRow(row, kind));
                shown++;
            }
            if (shown == 0)
            {
                var empty = new Label(emptyText);
                empty.AddToClassList("pn-empty");
                empty.style.opacity = 0.6f;
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.paddingLeft = 8;
                empty.style.paddingBottom = 4;
                foldout.Add(empty);
            }
            foldout.text = $"{title} ({shown})";
        }

        bool PassesFilter(Row row)
        {
            if (string.IsNullOrEmpty(m_Filter)) return true;
            var label = string.IsNullOrEmpty(row.label) ? row.name : row.label;
            return FuzzyMatcher.Score(m_Filter, label) >= 0
                || FuzzyMatcher.Score(m_Filter, row.path ?? "") >= 0
                || (!string.IsNullOrEmpty(row.note) && FuzzyMatcher.Score(m_Filter, row.note) >= 0);
        }

        List<Row> BuildPinRows()
        {
            var rows = new List<Row>();
            foreach (var (pin, shared) in Navigator.AllPins())
            {
                var row = MakeRowData(Navigator.GetInfo(pin.guid), shared, 0);
                row.label = pin.label;
                row.group = pin.group;
                rows.Add(row);
            }
            return rows;
        }

        List<Row> BuildFrequentRows(int max)
        {
            return Navigator.Frequent(max).Select(t => MakeRowData(t.info, false, t.score)).ToList();
        }

        List<Row> BuildRecentRows(int max)
        {
            return Navigator.Recent(max).Select(info => MakeRowData(info, false, 0)).ToList();
        }

        List<Row> BuildRecentFileRows(int max)
        {
            return Navigator.RecentFiles(max).Select(info => MakeRowData(info, false, 0)).ToList();
        }

        static Row MakeRowData(AssetInfo info, bool shared, double score)
        {
            var row = new Row
            {
                guid = info.guid,
                path = info.path,
                name = info.name,
                isFolder = info.isFolder,
                exists = info.Exists,
                shared = shared,
                score = score,
                pinned = Navigator.IsPinned(info.guid),
                note = Navigator.GetNote(info.guid)
            };
            if (info.Exists)
            {
                if (FolderColorResolver.TryGet(info.guid, out var style) && style.hasColor)
                {
                    row.hasColor = true;
                    row.color = style.color;
                }
                row.icon = AssetDatabase.GetCachedIcon(info.path);
            }
            return row;
        }

        // ------------------------------------------------------------------ rows

        VisualElement MakeRow(Row row, RowKind kind)
        {
            var element = new VisualElement();
            element.AddToClassList("pn-row");
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.height = 22;
            element.style.paddingLeft = 4;
            element.style.paddingRight = 6;
            element.userData = row;
            element.tooltip = row.exists
                ? (string.IsNullOrEmpty(row.note) ? row.path : row.path + "\n" + row.note)
                : "Missing asset (GUID " + row.guid + ")";
            if (!row.exists) element.style.opacity = 0.45f;

            var swatch = new VisualElement();
            swatch.AddToClassList("pn-swatch");
            swatch.style.width = 4;
            swatch.style.height = 16;
            swatch.style.marginRight = 4;
            swatch.style.backgroundColor = row.hasColor ? row.color : new Color(0f, 0f, 0f, 0f);
            element.Add(swatch);

            var icon = new Image { image = row.icon, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("pn-icon");
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 4;
            icon.style.flexShrink = 0;
            if (row.hasColor && row.isFolder) icon.tintColor = row.color;
            element.Add(icon);

            var name = new Label(string.IsNullOrEmpty(row.label) ? row.name : row.label);
            name.AddToClassList("pn-name");
            name.style.flexShrink = 0;
            element.Add(name);

            if (!string.IsNullOrEmpty(row.group)) element.Add(MakeChip(row.group));
            if (row.shared) element.Add(MakeChip("team"));
            if (kind != RowKind.Pin && row.pinned) element.Add(MakeChip("pinned"));

            if (!string.IsNullOrEmpty(row.note))
            {
                var note = new Label(row.note);
                note.AddToClassList("pn-note");
                note.style.flexShrink = 1;
                note.style.marginLeft = 8;
                note.style.opacity = 0.7f;
                note.style.fontSize = 10;
                note.style.unityFontStyleAndWeight = FontStyle.Italic;
                note.style.maxWidth = Length.Percent(45f);
                note.style.overflow = Overflow.Hidden;
                note.style.whiteSpace = WhiteSpace.NoWrap;
                note.style.textOverflow = TextOverflow.Ellipsis;
                element.Add(note);
            }

            var path = new Label(row.exists ? Navigator.ParentOf(row.path) : "");
            path.AddToClassList("pn-path");
            path.style.flexGrow = 1;
            path.style.flexShrink = 1;
            path.style.marginLeft = 8;
            path.style.opacity = 0.55f;
            path.style.fontSize = 10;
            path.style.unityTextAlign = TextAnchor.MiddleRight;
            path.style.overflow = Overflow.Hidden;
            path.style.whiteSpace = WhiteSpace.NoWrap;
            path.style.textOverflow = TextOverflow.Ellipsis;
            element.Add(path);

            element.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || !row.exists) return;
                if (row.isFolder)
                {
                    bool openNow = NavigatorUserState.instance.folderClickAction == FolderClickAction.SingleClickOpens || evt.clickCount >= 2;
                    if (openNow) Navigator.Open(row.guid);
                    else Navigator.SelectAndPing(row.guid);
                }
                else if (evt.clickCount >= 2) Navigator.OpenAsset(row.guid);
                else Navigator.SelectAndPing(row.guid);
            });

            element.AddManipulator(new ContextualMenuManipulator(evt => BuildRowMenu(evt.menu, row, kind)));

            // Drag the asset out (into object fields, the Hierarchy, another Project window, ...).
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !row.exists) return;
                m_DragRow = row;
                m_DragStart = evt.position;
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (m_DragRow != row || (evt.pressedButtons & 1) == 0) return;
                if (Vector2.Distance((Vector2)evt.position, m_DragStart) < k_DragThreshold) return;
                m_DragRow = null;
                StartDrag(row);
            });
            element.RegisterCallback<PointerUpEvent>(evt => m_DragRow = null);

            // Drop assets onto a folder row to move them into that folder.
            if (row.exists && row.isFolder)
            {
                element.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    if (DraggedAssetPaths(foldersToo: false).Count == 0) return;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    evt.StopPropagation();
                });
                element.RegisterCallback<DragPerformEvent>(evt =>
                {
                    var assets = DraggedAssetPaths(foldersToo: false);
                    if (assets.Count == 0) return;
                    DragAndDrop.AcceptDrag();
                    evt.StopPropagation();
                    MoveAssetsInto(assets, row.path);
                });
            }

            return element;
        }

        static Label MakeChip(string text)
        {
            var chip = new Label(text);
            chip.AddToClassList("pn-chip");
            chip.style.marginLeft = 6;
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;
            chip.style.fontSize = 9;
            chip.style.opacity = 0.8f;
            return chip;
        }

        static void StartDrag(Row row)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(row.path);
            if (obj == null) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { obj };
            DragAndDrop.paths = new[] { row.path };
            DragAndDrop.StartDrag(row.name);
        }

        static void MoveAssetsInto(List<string> assetPaths, string folderPath)
        {
            string message = assetPaths.Count == 1
                ? $"Move\n{assetPaths[0]}\ninto\n{folderPath}?"
                : $"Move {assetPaths.Count} assets into\n{folderPath}?";
            if (!EditorUtility.DisplayDialog("Move assets", message, "Move", "Cancel")) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var source in assetPaths)
                {
                    var destination = folderPath + "/" + Navigator.NameOf(source);
                    if (destination == source) continue;
                    var error = AssetDatabase.ValidateMoveAsset(source, destination);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"[Project Navigator] Cannot move {source}: {error}");
                        continue;
                    }
                    AssetDatabase.MoveAsset(source, destination);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        void BuildRowMenu(DropdownMenu menu, Row row, RowKind kind)
        {
            var shared = NavigatorSharedSettings.instance;
            var user = NavigatorUserState.instance;

            if (row.exists)
            {
                if (row.isFolder)
                {
                    menu.AppendAction("Open", _ => Navigator.Open(row.guid));
                    menu.AppendAction("Select and Ping", _ => Navigator.SelectAndPing(row.guid));
                }
                else
                {
                    menu.AppendAction("Select and Ping", _ => Navigator.SelectAndPing(row.guid));
                    menu.AppendAction("Open Asset", _ => Navigator.OpenAsset(row.guid));
                }
                menu.AppendAction("Reveal in Explorer / Finder", _ => EditorUtility.RevealInFinder(row.path));
                menu.AppendAction("Copy Path", _ => EditorGUIUtility.systemCopyBuffer = row.path);
                menu.AppendSeparator();
            }

            if (row.pinned)
            {
                menu.AppendAction("Unpin", _ => Navigator.Unpin(row.guid));
            }
            else if (row.exists)
            {
                menu.AppendAction("Pin (personal)", _ => Navigator.Pin(row.guid, false));
                menu.AppendAction("Pin (team)", _ => Navigator.Pin(row.guid, true));
            }

            if (kind == RowKind.Pin)
            {
                menu.AppendAction("Pin label... (display only)", _ => TextPromptWindow.Prompt("Pin label", "Label", row.label, v => Navigator.SetPinLabel(row.guid, v)));
                menu.AppendAction("Pin group...", _ => TextPromptWindow.Prompt("Pin group", "Group", row.group, v => Navigator.SetPinGroup(row.guid, v)));
                menu.AppendAction("Move up", _ => Navigator.MovePin(row.guid, -1));
                menu.AppendAction("Move down", _ => Navigator.MovePin(row.guid, 1));
            }

            if (row.exists)
            {
                menu.AppendSeparator();
                menu.AppendAction("Note...", _ => TextPromptWindow.Prompt("Note", "Note", row.note,
                    v => Navigator.SetNote(row.guid, v, user.colorScopeShared)));
                if (!string.IsNullOrEmpty(row.note))
                    menu.AppendAction("Clear note", _ => Navigator.ClearNote(row.guid));

                var explicitColor = Navigator.FindExplicitColor(row.guid, out _);
                foreach (var tag in shared.tags)
                {
                    var tagId = tag.id;
                    bool isCurrent = explicitColor != null && explicitColor.tagId == tagId;
                    menu.AppendAction("Color/" + tag.name, _ => Navigator.SetColor(row.guid, tagId, user.colorScopeShared),
                        isCurrent ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
                menu.AppendAction("Color/None (block inheritance)", _ => Navigator.SetColor(row.guid, "", user.colorScopeShared),
                    explicitColor != null && string.IsNullOrEmpty(explicitColor.tagId) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                menu.AppendAction("Color/Clear (use rules and inheritance)", _ => Navigator.ClearColor(row.guid));

                menu.AppendSeparator();
                menu.AppendAction("Rename on disk...", _ => TextPromptWindow.Prompt("Rename asset on disk", "New name", row.name, v =>
                {
                    if (v == row.name) return;
                    if (!EditorUtility.DisplayDialog("Rename on disk", $"Rename\n{row.path}\nto \"{v}\"?\n\nThis renames the real asset (references are kept by GUID).", "Rename", "Cancel")) return;
                    if (!Navigator.RenameAsset(row.guid, v, out var error))
                        EditorUtility.DisplayDialog("Rename failed", error, "OK");
                }));
            }

            if (kind == RowKind.Frequent || kind == RowKind.Recent || kind == RowKind.RecentFile)
            {
                menu.AppendSeparator();
                menu.AppendAction("Forget this entry", _ => Navigator.Forget(row.guid));
            }
        }

        // ------------------------------------------------------------------ colored assets section

        void FillColors()
        {
            m_ColorsFold.Clear();
            var shared = NavigatorSharedSettings.instance;
            var user = NavigatorUserState.instance;

            var entries = shared.folderColors.Select(fc => (fc, isShared: true))
                .Concat(user.folderColors.Select(fc => (fc, isShared: false)))
                .ToList();

            int shown = 0;
            foreach (var group in entries.GroupBy(e => e.fc.tagId))
            {
                var tag = shared.FindTag(group.Key);
                string title = tag != null ? tag.name : (string.IsNullOrEmpty(group.Key) ? "None (blocked)" : "Unknown tag");

                var header = new VisualElement();
                header.AddToClassList("pn-tagheader");
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.marginTop = 6;
                header.style.marginBottom = 2;
                var swatch = new VisualElement();
                swatch.style.width = 10;
                swatch.style.height = 10;
                swatch.style.marginLeft = 4;
                swatch.style.marginRight = 6;
                swatch.style.backgroundColor = tag != null ? tag.color : new Color(0.5f, 0.5f, 0.5f, 0.4f);
                header.Add(swatch);
                var headerLabel = new Label(title);
                headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.Add(headerLabel);
                m_ColorsFold.Add(header);

                foreach (var entry in group)
                {
                    var row = MakeRowData(Navigator.GetInfo(entry.fc.guid), entry.isShared, 0);
                    if (!PassesFilter(row)) continue;
                    m_ColorsFold.Add(MakeRow(row, RowKind.Color));
                    shown++;
                }
            }

            if (shown == 0)
            {
                var empty = new Label("No colored assets yet. Right-click a folder or file → Project Navigator → Color.");
                empty.style.opacity = 0.6f;
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.paddingLeft = 8;
                m_ColorsFold.Add(empty);
            }

            var manage = new Button(() => SettingsService.OpenProjectSettings(k_ProjectSettingsPath)) { text = "Manage tags & rules..." };
            manage.style.alignSelf = Align.FlexStart;
            manage.style.marginTop = 6;
            manage.style.marginLeft = 4;
            m_ColorsFold.Add(manage);

            m_ColorsFold.text = $"Colored ({shown})";
        }
    }
}
