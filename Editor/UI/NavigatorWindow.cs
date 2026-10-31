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
    /// The Navigator window. Sections: Pinned / Frequent / Recent / Recent files / Colored / Tools / Windows,
    /// each one switchable per window kind (docked, floating) and per mode (normal, compact), shown
    /// stacked or as tabs (⋮ menu). Folder rows: select on click, open in the Project window on
    /// double-click (or single click, see Preferences). File rows: select on click, open on double-click.
    /// Tool rows launch; window rows focus.
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
            Color,
            Tool,
            Window,
            RecentWindow
        }

        sealed class Row
        {
            public RowKind kind;
            public string label;
            public string detail;
            public string note;
            public string group;
            public bool exists = true;
            public bool pinned;
            public bool shared;
            public bool hasColor;
            public Color color;
            public Texture icon;
            public bool tintIcon;
            // asset rows
            public string guid;
            public string path;
            public string name;
            public bool isFolder;
            // tool / window rows
            public ToolPin tool;
            public EditorWindow window;
            public RecentWindow recent;
        }

        const string k_PreferencesPath = "Preferences/Project Navigator";
        const string k_ProjectSettingsPath = "Project/Project Navigator";
        const float k_DragThreshold = 6f;

        static bool s_Quitting;
        static double s_QuitRequestedAt;

        [SerializeField] bool m_IsFloating;
        /// <summary>When set, this window shows exactly one section as a small floating panel.</summary>
        [SerializeField] string m_TearOffSection = "";

        bool IsTearOff => !string.IsNullOrEmpty(m_TearOffSection);

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

        /// <summary>Opens (or focuses) a floating panel that shows only one section.</summary>
        public static void OpenTearOff(string sectionId)
        {
            if (Array.IndexOf(NavigatorSections.All, sectionId) < 0) return;
            var existing = Resources.FindObjectsOfTypeAll<NavigatorWindow>().FirstOrDefault(w => w.m_TearOffSection == sectionId);
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var data = NavigatorGlobal.Data;
            var panel = data.tearOffs.Find(t => t.sectionId == sectionId);
            if (panel == null)
            {
                panel = new TearOffPanel { sectionId = sectionId };
                data.tearOffs.Add(panel);
            }

            var window = CreateInstance<NavigatorWindow>();
            window.m_IsFloating = true;
            window.m_TearOffSection = sectionId;
            window.titleContent = new GUIContent(NavigatorSections.Title(sectionId), EditorGUIUtility.IconContent("Folder Icon").image);
            if (NavigatorUserState.instance.floatingOnTop) window.ShowUtility();
            else window.Show();
            if (panel.rect.width >= 120f && panel.rect.height >= 60f) window.position = panel.rect;

            panel.open = true;
            NavigatorGlobal.MarkDirty();
        }

        TearOffPanel TearOffPanel => IsTearOff ? NavigatorGlobal.Data.tearOffs.Find(t => t.sectionId == m_TearOffSection) : null;

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
            if (!user.reopenFloatingOnStartup) return;
            var existing = Resources.FindObjectsOfTypeAll<NavigatorWindow>();
            if (user.floatingWasOpen && !existing.Any(w => w.m_IsFloating && !w.IsTearOff))
                OpenFloating();
            foreach (var panel in NavigatorGlobal.Data.tearOffs.ToList())
                if (panel.open && !existing.Any(w => w.m_TearOffSection == panel.sectionId))
                    OpenTearOff(panel.sectionId);
        }

        static bool OnWantsToQuit()
        {
            s_Quitting = true;
            s_QuitRequestedAt = EditorApplication.timeSinceStartup;
            foreach (var window in Resources.FindObjectsOfTypeAll<NavigatorWindow>())
                window.StoreRect();
            Navigator.MarkDirty(user: true, global: true);
            Navigator.FlushSaves();
            return true;
        }

        void StoreRect()
        {
            if (!m_IsFloating) return;
            if (IsTearOff)
            {
                var panel = TearOffPanel;
                if (panel != null) panel.rect = position;
            }
            else
            {
                NavigatorUserState.instance.floatingRect = position;
            }
        }

        static void QuitFlagWatchdog()
        {
            if (s_Quitting && EditorApplication.timeSinceStartup - s_QuitRequestedAt > 5.0)
                s_Quitting = false;
        }

        // ------------------------------------------------------------------ fields

        ToolbarButton m_Back;
        ToolbarButton m_Forward;
        ToolbarToggle m_Compact;
        ToolbarSearchField m_Search;
        ToolbarMenu m_More;
        Toolbar m_TabBar;
        Label m_Current;
        ScrollView m_Scroll;
        readonly Dictionary<string, Foldout> m_Sections = new Dictionary<string, Foldout>();
        readonly List<string> m_VisibleSections = new List<string>();

        string m_Filter = "";
        Row m_DragRow;
        Vector2 m_DragStart;
        double m_NextWindowsRefresh;
        string m_WindowsSignature;

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

        SectionLayout Layout
        {
            get => m_IsFloating ? NavigatorGlobal.Data.floatingLayout : NavigatorGlobal.Data.dockedLayout;
            set
            {
                if (m_IsFloating) NavigatorGlobal.Data.floatingLayout = value;
                else NavigatorGlobal.Data.dockedLayout = value;
                NavigatorGlobal.MarkDirty();
            }
        }

        string ActiveTab
        {
            get => m_IsFloating ? NavigatorGlobal.Data.floatingActiveTab : NavigatorGlobal.Data.dockedActiveTab;
            set
            {
                if (m_IsFloating) NavigatorGlobal.Data.floatingActiveTab = value;
                else NavigatorGlobal.Data.dockedActiveTab = value;
                NavigatorGlobal.MarkDirty();
            }
        }

        // ------------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            Navigator.Changed += OnNavigatorChanged;
            EditorApplication.update += PeriodicRefresh;
        }

        void OnDisable()
        {
            Navigator.Changed -= OnNavigatorChanged;
            EditorApplication.update -= PeriodicRefresh;
            if (m_IsFloating)
            {
                StoreRect();
                Navigator.MarkDirty(user: !IsTearOff, global: IsTearOff);
            }
        }

        void OnDestroy()
        {
            if (!m_IsFloating && !s_Quitting && NavigatorGlobal.Data.closeTearOffsWithMain)
            {
                EditorApplication.delayCall += () =>
                {
                    foreach (var window in Resources.FindObjectsOfTypeAll<NavigatorWindow>())
                        if (window != null && window.IsTearOff) window.Close();
                };
            }
            if (!m_IsFloating || s_Quitting) return;
            if (IsTearOff)
            {
                var panel = TearOffPanel;
                if (panel != null) panel.open = false;
                NavigatorGlobal.MarkDirty();
            }
            else
            {
                NavigatorUserState.instance.floatingWasOpen = false;
                Navigator.MarkDirty(user: true);
            }
        }

        void OnNavigatorChanged()
        {
            if (m_Scroll != null) Refresh();
        }

        /// <summary>The Windows section mirrors live editor state (focus, open/closed); it refreshes only when that state changed.</summary>
        void PeriodicRefresh()
        {
            if (m_Scroll == null || !Visible(NavigatorSections.Windows)) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < m_NextWindowsRefresh) return;
            m_NextWindowsRefresh = now + 1.0;
            var open = NavigatorTools.OpenWindows();
            var signature = NavigatorTools.WindowsSignature(open);
            if (signature == m_WindowsSignature) return;
            FillWindows(open, signature);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("pn-root");
            root.style.flexGrow = 1;
            LoadStyles(root);

            BuildToolbar(root);

            m_TabBar = new Toolbar();
            m_TabBar.AddToClassList("pn-tabs");
            root.Add(m_TabBar);

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

            m_Sections.Clear();
            foreach (var id in NavigatorSections.All)
            {
                var sectionId = id;
                var foldout = new Foldout { text = NavigatorSections.Title(id), value = id != NavigatorSections.Colored };
                foldout.AddToClassList("pn-fold");
                var header = foldout.Q<Toggle>();
                header?.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Open in floating window", _ => OpenTearOff(sectionId));
                    if (!IsTearOff)
                    {
                        evt.menu.AppendAction("Hide section (this mode)", _ =>
                        {
                            NavigatorSections.SetVisible(Setting(sectionId), m_IsFloating, Compact, false);
                            NavigatorGlobal.MarkDirty();
                            RebuildLayout();
                            Refresh();
                        });
                    }
                }));
                m_Sections[id] = foldout;
                m_Scroll.Add(foldout);
            }

            SetupPinDropTarget(root);
            RebuildLayout();
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

            m_Compact = Padded(new ToolbarToggle { text = "Compact", tooltip = "Show only the sections chosen for compact mode (⋮ menu)" });
            m_Compact.SetValueWithoutNotify(Compact);
            m_Compact.RegisterValueChangedCallback(evt =>
            {
                Compact = evt.newValue;
                RebuildLayout();
                Refresh();
            });
            if (!IsTearOff) toolbar.Add(m_Compact);

            if (!m_IsFloating)
                toolbar.Add(Padded(new ToolbarButton(OpenFloating) { text = "Float", tooltip = "Open a floating copy (stays on top when enabled in preferences)" }));

            m_More = new ToolbarMenu { text = "⋮", tooltip = "Layout, sections, preferences (Project Navigator " + NavigatorVersion.Current + ")" };
            m_More.style.flexShrink = 0;
            m_More.style.paddingLeft = 4;
            m_More.style.paddingRight = 4;
            m_More.menu.AppendAction("Layout/Stacked sections", _ => { Layout = SectionLayout.Stacked; RebuildLayout(); Refresh(); },
                _ => Layout == SectionLayout.Stacked ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            m_More.menu.AppendAction("Layout/Tabs", _ => { Layout = SectionLayout.Tabs; RebuildLayout(); Refresh(); },
                _ => Layout == SectionLayout.Tabs ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            foreach (var id in NavigatorSections.All)
            {
                var sectionId = id;
                m_More.menu.AppendAction("Sections/" + NavigatorSections.Title(id), _ =>
                {
                    var setting = Setting(sectionId);
                    NavigatorSections.SetVisible(setting, m_IsFloating, Compact, !NavigatorSections.IsVisible(setting, m_IsFloating, Compact));
                    NavigatorGlobal.MarkDirty();
                    RebuildLayout();
                    Refresh();
                }, _ => NavigatorSections.IsVisible(Setting(sectionId), m_IsFloating, Compact) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            m_More.menu.AppendAction("Sections/Order and defaults...", _ => SettingsService.OpenUserPreferences(k_PreferencesPath));
            foreach (var id in NavigatorSections.All)
            {
                var sectionId = id;
                m_More.menu.AppendAction("Float section as window/" + NavigatorSections.Title(id), _ => OpenTearOff(sectionId));
            }
            m_More.menu.AppendSeparator();
            m_More.menu.AppendAction("Pin a tool...", _ => ToolPickerWindow.Open());
            m_More.menu.AppendAction("Window switcher (Alt+Shift+W)", _ => NavigatorPopup.Open(NavigatorPopup.Mode.Windows));
            m_More.menu.AppendSeparator();
            m_More.menu.AppendAction("Preferences...", _ => SettingsService.OpenUserPreferences(k_PreferencesPath));
            m_More.menu.AppendAction("Team settings (tags, rules)...", _ => SettingsService.OpenProjectSettings(k_ProjectSettingsPath));
            toolbar.Add(m_More);

            var spacer = new VisualElement();
            spacer.style.width = 2;
            toolbar.Add(spacer);

            root.Add(toolbar);
        }

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

        SectionSetting Setting(string id)
        {
            var list = NavigatorGlobal.Data.sections;
            var setting = list.Find(s => s.id == id);
            if (setting == null)
            {
                setting = NavigatorSections.Default(id);
                list.Add(setting);
            }
            return setting;
        }

        /// <summary>Called from Preferences after the section configuration changed.</summary>
        public void RebuildFromSettings()
        {
            if (m_Scroll == null) return;
            RebuildLayout();
            Refresh();
        }

        /// <summary>Applies section order, visibility for the current mode, and stacked/tabs layout.</summary>
        void RebuildLayout()
        {
            var data = NavigatorGlobal.Data;
            NavigatorSections.EnsureDefaults(data.sections);
            bool compact = Compact;
            bool trackFiles = NavigatorUserState.instance.trackFiles;

            m_VisibleSections.Clear();
            foreach (var setting in data.sections)
            {
                if (!m_Sections.TryGetValue(setting.id, out var foldout)) continue;
                foldout.RemoveFromHierarchy();
                m_Scroll.Add(foldout); // appended in configured order
                bool visible = IsTearOff ? setting.id == m_TearOffSection : NavigatorSections.IsVisible(setting, m_IsFloating, compact);
                if (setting.id == NavigatorSections.RecentFiles && !trackFiles && !IsTearOff) visible = false;
                if (visible) m_VisibleSections.Add(setting.id);
                foldout.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (IsTearOff && m_VisibleSections.Count == 1)
            {
                var only = m_Sections[m_VisibleSections[0]];
                only.value = true;
                HideFoldoutHeader(only, true);
            }

            bool tabs = !IsTearOff && Layout == SectionLayout.Tabs && m_VisibleSections.Count > 1;
            m_TabBar.Clear();
            m_TabBar.style.display = tabs ? DisplayStyle.Flex : DisplayStyle.None;

            if (tabs)
            {
                var active = ActiveTab;
                if (!m_VisibleSections.Contains(active)) active = m_VisibleSections[0];
                foreach (var id in m_VisibleSections)
                {
                    var sectionId = id;
                    var toggle = new ToolbarToggle { text = NavigatorSections.Title(id) };
                    toggle.style.flexShrink = 1;
                    toggle.style.paddingLeft = 6;
                    toggle.style.paddingRight = 6;
                    toggle.SetValueWithoutNotify(id == active);
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (!evt.newValue)
                        {
                            toggle.SetValueWithoutNotify(true); // the active tab cannot be unselected
                            return;
                        }
                        ActiveTab = sectionId;
                        RebuildLayout();
                        Refresh();
                    });
                    toggle.AddManipulator(new ContextualMenuManipulator(evt =>
                    {
                        evt.menu.AppendAction("Open in floating window", _ => OpenTearOff(sectionId));
                        evt.menu.AppendAction("Hide section", _ =>
                        {
                            NavigatorSections.SetVisible(Setting(sectionId), m_IsFloating, Compact, false);
                            NavigatorGlobal.MarkDirty();
                            RebuildLayout();
                            Refresh();
                        });
                    }));
                    m_TabBar.Add(toggle);
                }
                foreach (var id in m_VisibleSections)
                {
                    var foldout = m_Sections[id];
                    foldout.style.display = id == active ? DisplayStyle.Flex : DisplayStyle.None;
                    foldout.value = true;
                    HideFoldoutHeader(foldout, true);
                }
            }
            else
            {
                foreach (var id in m_VisibleSections)
                    HideFoldoutHeader(m_Sections[id], false);
            }

            minSize = compact || IsTearOff ? new Vector2(200f, 80f) : new Vector2(260f, 200f);
        }

        static void HideFoldoutHeader(Foldout foldout, bool hide)
        {
            var toggle = foldout.Q<Toggle>();
            if (toggle != null) toggle.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
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

            if (Visible(NavigatorSections.Pinned))
                FillSection(NavigatorSections.Pinned, BuildPinRows(), "Drop folders or files here, or right-click an asset → Project Navigator → Pin.");
            if (Visible(NavigatorSections.Frequent))
                FillSection(NavigatorSections.Frequent, BuildFrequentRows(user.frequentCount), "Folders you use most will show up here.");
            if (Visible(NavigatorSections.Recent))
                FillSection(NavigatorSections.Recent, BuildRecentRows(user.recentCount), "Nothing visited yet.");
            if (Visible(NavigatorSections.RecentFiles))
                FillSection(NavigatorSections.RecentFiles, BuildRecentFileRows(user.recentFilesCount), "Files you select will show up here.");
            if (Visible(NavigatorSections.Colored))
                FillColors();
            if (Visible(NavigatorSections.Tools))
                FillTools();
            if (Visible(NavigatorSections.Windows))
            {
                var open = NavigatorTools.OpenWindows();
                FillWindows(open, NavigatorTools.WindowsSignature(open));
            }
        }

        bool Visible(string id)
        {
            return m_VisibleSections.Contains(id) && m_Sections[id].style.display != DisplayStyle.None;
        }

        void FillSection(string id, List<Row> rows, string emptyText)
        {
            var foldout = m_Sections[id];
            foldout.Clear();
            int shown = 0;
            foreach (var row in rows)
            {
                if (!PassesFilter(row)) continue;
                foldout.Add(MakeRow(row));
                shown++;
            }
            if (shown == 0) foldout.Add(MakeEmpty(emptyText));
            foldout.text = $"{NavigatorSections.Title(id)} ({shown})";
        }

        static Label MakeEmpty(string text)
        {
            var empty = new Label(text);
            empty.AddToClassList("pn-empty");
            empty.style.opacity = 0.6f;
            empty.style.whiteSpace = WhiteSpace.Normal;
            empty.style.paddingLeft = 8;
            empty.style.paddingBottom = 4;
            return empty;
        }

        bool PassesFilter(Row row)
        {
            if (string.IsNullOrEmpty(m_Filter)) return true;
            return FuzzyMatcher.Score(m_Filter, row.label ?? "") >= 0
                || FuzzyMatcher.Score(m_Filter, row.detail ?? "") >= 0
                || FuzzyMatcher.Score(m_Filter, row.path ?? "") >= 0
                || (!string.IsNullOrEmpty(row.note) && FuzzyMatcher.Score(m_Filter, row.note) >= 0);
        }

        // ------------------------------------------------------------------ row data

        List<Row> BuildPinRows()
        {
            var rows = new List<Row>();
            foreach (var (pin, shared) in Navigator.AllPins())
            {
                var row = MakeAssetRow(Navigator.GetInfo(pin.guid), RowKind.Pin, shared);
                if (!string.IsNullOrEmpty(pin.label)) row.label = pin.label;
                row.group = pin.group;
                rows.Add(row);
            }
            return rows;
        }

        List<Row> BuildFrequentRows(int max)
        {
            return Navigator.Frequent(max).Select(t => MakeAssetRow(t.info, RowKind.Frequent, false)).ToList();
        }

        List<Row> BuildRecentRows(int max)
        {
            return Navigator.Recent(max).Select(info => MakeAssetRow(info, RowKind.Recent, false)).ToList();
        }

        List<Row> BuildRecentFileRows(int max)
        {
            return Navigator.RecentFiles(max).Select(info => MakeAssetRow(info, RowKind.RecentFile, false)).ToList();
        }

        static Row MakeAssetRow(AssetInfo info, RowKind kind, bool shared)
        {
            var row = new Row
            {
                kind = kind,
                guid = info.guid,
                path = info.path,
                name = info.name,
                label = info.name,
                detail = info.Exists ? Navigator.ParentOf(info.path) : "",
                isFolder = info.isFolder,
                exists = info.Exists,
                shared = shared,
                pinned = Navigator.IsPinned(info.guid),
                note = Navigator.GetNote(info.guid)
            };
            if (info.Exists)
            {
                if (FolderColorResolver.TryGet(info.guid, out var style) && style.hasColor)
                {
                    row.hasColor = true;
                    row.color = style.color;
                    row.tintIcon = info.isFolder;
                }
                row.icon = AssetDatabase.GetCachedIcon(info.path);
            }
            return row;
        }

        static Row MakeToolRow(ToolPin pin, bool shared)
        {
            return new Row
            {
                kind = RowKind.Tool,
                tool = pin,
                shared = shared,
                label = NavigatorTools.Label(pin),
                detail = NavigatorTools.Detail(pin),
                group = pin.group,
                pinned = true,
                hasColor = pin.hasColor,
                color = pin.color,
                icon = NavigatorTools.Icon(pin)
            };
        }

        static Row MakeWindowRow(EditorWindow window, IReadOnlyList<EditorWindow> open)
        {
            var mark = NavigatorTools.FindMark(window, open);
            var type = window.GetType();
            bool hasColor = NavigatorTools.TryGetEffectiveColor(window, open, out var color);
            return new Row
            {
                kind = RowKind.Window,
                window = window,
                label = NavigatorTools.DisplayName(window),
                detail = type.Name + (window.docked ? " · docked" : " · floating") + (window.hasFocus ? " · focused" : ""),
                note = mark != null && !string.IsNullOrEmpty(mark.label) ? window.titleContent.text : "",
                pinned = NavigatorTools.IsWindowTypePinned(type),
                hasColor = hasColor,
                color = color,
                icon = ToolTracker.OriginalIcon(window)
            };
        }

        static Row MakeRecentWindowRow(RecentWindow recent)
        {
            return new Row
            {
                kind = RowKind.RecentWindow,
                recent = recent,
                label = recent.title,
                detail = NavigatorTools.ShortTypeName(recent.windowType) + " · closed",
                icon = EditorGUIUtility.FindTexture("_Popup")
            };
        }

        // ------------------------------------------------------------------ rows

        VisualElement MakeRow(Row row)
        {
            var element = new VisualElement();
            element.AddToClassList("pn-row");
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.height = 22;
            element.style.paddingLeft = 4;
            element.style.paddingRight = 6;
            element.userData = row;
            element.tooltip = row.path != null
                ? (row.exists ? (string.IsNullOrEmpty(row.note) ? row.path : row.path + "\n" + row.note) : "Missing asset (GUID " + row.guid + ")")
                : row.detail;
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
            if (row.hasColor && row.tintIcon) icon.tintColor = row.color;
            element.Add(icon);

            var name = new Label(row.label);
            name.AddToClassList("pn-name");
            name.style.flexShrink = 0;
            element.Add(name);

            if (!string.IsNullOrEmpty(row.group)) element.Add(MakeChip(row.group));
            if (row.shared) element.Add(MakeChip("team"));
            if (row.kind != RowKind.Pin && row.kind != RowKind.Tool && row.pinned) element.Add(MakeChip("pinned"));

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

            var detail = new Label(row.detail ?? "");
            detail.AddToClassList("pn-path");
            detail.style.flexGrow = 1;
            detail.style.flexShrink = 1;
            detail.style.marginLeft = 8;
            detail.style.opacity = 0.55f;
            detail.style.fontSize = 10;
            detail.style.unityTextAlign = TextAnchor.MiddleRight;
            detail.style.overflow = Overflow.Hidden;
            detail.style.whiteSpace = WhiteSpace.NoWrap;
            detail.style.textOverflow = TextOverflow.Ellipsis;
            element.Add(detail);

            element.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || !row.exists) return;
                Activate(row, evt.clickCount >= 2);
            });

            element.AddManipulator(new ContextualMenuManipulator(evt => BuildRowMenu(evt.menu, row)));

            if (row.path != null)
            {
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
            }

            return element;
        }

        void Activate(Row row, bool doubleClick)
        {
            switch (row.kind)
            {
                case RowKind.Tool:
                    NavigatorTools.Launch(row.tool);
                    break;
                case RowKind.Window:
                    NavigatorTools.Focus(row.window);
                    break;
                case RowKind.RecentWindow:
                    var type = NavigatorTools.ResolveType(row.recent.windowType);
                    if (type != null) NavigatorTools.FocusOrOpen(type);
                    break;
                default:
                    if (row.isFolder)
                    {
                        bool openNow = NavigatorUserState.instance.folderClickAction == FolderClickAction.SingleClickOpens || doubleClick;
                        if (openNow) Navigator.Open(row.guid);
                        else Navigator.SelectAndPing(row.guid);
                    }
                    else if (doubleClick) Navigator.OpenAsset(row.guid);
                    else Navigator.SelectAndPing(row.guid);
                    break;
            }
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

        // ------------------------------------------------------------------ context menus

        void BuildRowMenu(DropdownMenu menu, Row row)
        {
            switch (row.kind)
            {
                case RowKind.Tool: BuildToolMenu(menu, row); return;
                case RowKind.Window: BuildWindowMenu(menu, row); return;
                case RowKind.RecentWindow:
                    menu.AppendAction("Open", _ => Activate(row, false));
                    menu.AppendAction("Pin window type", _ =>
                    {
                        var type = NavigatorTools.ResolveType(row.recent.windowType);
                        if (type != null) NavigatorTools.PinWindowType(type, false);
                    });
                    menu.AppendAction("Forget", _ => NavigatorTools.ForgetRecent(row.recent));
                    return;
            }
            BuildAssetMenu(menu, row);
        }

        void BuildAssetMenu(DropdownMenu menu, Row row)
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

            if (row.kind == RowKind.Pin)
            {
                var pin = Navigator.FindPin(row.guid, out _);
                menu.AppendAction("Pin label... (display only)", _ => TextPromptWindow.Prompt("Pin label", "Label", pin?.label, v => Navigator.SetPinLabel(row.guid, v)));
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

            if (row.kind == RowKind.Frequent || row.kind == RowKind.Recent || row.kind == RowKind.RecentFile)
            {
                menu.AppendSeparator();
                menu.AppendAction("Forget this entry", _ => Navigator.Forget(row.guid));
            }
        }

        void BuildToolMenu(DropdownMenu menu, Row row)
        {
            var pin = row.tool;
            menu.AppendAction("Launch", _ => NavigatorTools.Launch(pin));
            if (pin.kind == ToolPinKind.MenuItem)
                menu.AppendAction("Copy menu path", _ => EditorGUIUtility.systemCopyBuffer = pin.menuPath);
            menu.AppendSeparator();
            menu.AppendAction("Label...", _ => TextPromptWindow.Prompt("Tool label", "Label", pin.label, v => NavigatorTools.SetToolLabel(pin.id, v)));
            menu.AppendAction("Group...", _ => TextPromptWindow.Prompt("Tool group", "Group", pin.group, v => NavigatorTools.SetToolGroup(pin.id, v)));
            menu.AppendAction("Color...", _ => ColorPromptWindow.Prompt("Tool color", pin.hasColor ? pin.color : NavigatorTools.PresetColor(2), c => NavigatorTools.SetToolColor(pin.id, c)));
            menu.AppendAction("Move up", _ => NavigatorTools.MoveToolPin(pin.id, -1));
            menu.AppendAction("Move down", _ => NavigatorTools.MoveToolPin(pin.id, 1));
            menu.AppendSeparator();
            menu.AppendAction("Unpin", _ => NavigatorTools.UnpinTool(pin.id));
        }

        void BuildWindowMenu(DropdownMenu menu, Row row)
        {
            var window = row.window;
            if (window == null) return;
            var type = window.GetType();
            menu.AppendAction("Focus", _ => NavigatorTools.Focus(window));
            menu.AppendAction(row.pinned ? "Unpin window type" : "Pin window type", _ =>
            {
                if (row.pinned)
                {
                    var pin = NavigatorTools.AllToolPins().FirstOrDefault(t => t.pin.kind == ToolPinKind.Window && t.pin.windowType == NavigatorTools.TypeKey(type)).pin;
                    if (pin != null) NavigatorTools.UnpinTool(pin.id);
                }
                else NavigatorTools.PinWindowType(type, false);
            });
            menu.AppendSeparator();
            var mark = NavigatorTools.FindMark(window);
            menu.AppendAction("Label...", _ => TextPromptWindow.Prompt("Window label", "Label", mark?.label, v => NavigatorTools.SetWindowLabel(window, v)));
            menu.AppendAction("Color this window...", _ => ColorPromptWindow.Prompt("Window color", mark != null && mark.hasColor ? mark.color : NavigatorTools.PresetColor(2), c => NavigatorTools.SetWindowColor(window, false, c)));
            menu.AppendAction("Color all " + type.Name + " windows...", _ => ColorPromptWindow.Prompt("Window type color", mark != null && mark.hasColor ? mark.color : NavigatorTools.PresetColor(2), c => NavigatorTools.SetWindowColor(window, true, c)));
            if (mark != null && mark.hasColor)
                menu.AppendAction("Clear color", _ => NavigatorTools.SetWindowColor(window, mark.byType, null));
            menu.AppendSeparator();
            menu.AppendAction("Close window", _ => window.Close());
        }

        // ------------------------------------------------------------------ sections with their own layout

        void FillColors()
        {
            var foldout = m_Sections[NavigatorSections.Colored];
            foldout.Clear();
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
                foldout.Add(MakeGroupHeader(title, tag != null ? tag.color : new Color(0.5f, 0.5f, 0.5f, 0.4f)));

                foreach (var entry in group)
                {
                    var row = MakeAssetRow(Navigator.GetInfo(entry.fc.guid), RowKind.Color, entry.isShared);
                    if (!PassesFilter(row)) continue;
                    foldout.Add(MakeRow(row));
                    shown++;
                }
            }

            if (shown == 0) foldout.Add(MakeEmpty("No colored assets yet. Right-click a folder or file → Project Navigator → Color."));

            var manage = new Button(() => SettingsService.OpenProjectSettings(k_ProjectSettingsPath)) { text = "Manage tags & rules..." };
            manage.style.alignSelf = Align.FlexStart;
            manage.style.marginTop = 6;
            manage.style.marginLeft = 4;
            foldout.Add(manage);

            foldout.text = $"Colored ({shown})";
        }

        void FillTools()
        {
            var foldout = m_Sections[NavigatorSections.Tools];
            foldout.Clear();
            int shown = 0;
            string lastGroup = null;
            foreach (var (pin, shared) in NavigatorTools.AllToolPins().OrderBy(t => t.pin.group ?? "", StringComparer.OrdinalIgnoreCase))
            {
                var row = MakeToolRow(pin, shared);
                if (!PassesFilter(row)) continue;
                var group = pin.group ?? "";
                if (group.Length > 0 && group != lastGroup)
                    foldout.Add(MakeGroupHeader(group, null));
                lastGroup = group;
                row.group = ""; // shown as a header instead of a chip here
                foldout.Add(MakeRow(row));
                shown++;
            }
            if (shown == 0) foldout.Add(MakeEmpty("Pin editor windows and menu items for one-click access. Any Unity menu item works."));

            var add = new Button(ToolPickerWindow.Open) { text = "Pin a tool...", tooltip = "Search all menu items and open windows" };
            add.style.alignSelf = Align.FlexStart;
            add.style.marginTop = 6;
            add.style.marginLeft = 4;
            foldout.Add(add);

            foldout.text = $"Tools ({shown})";
        }

        void FillWindows(IReadOnlyList<EditorWindow> open, string signature)
        {
            m_WindowsSignature = signature;
            var foldout = m_Sections[NavigatorSections.Windows];
            foldout.Clear();
            int shown = 0;
            foreach (var window in open)
            {
                var row = MakeWindowRow(window, open);
                if (!PassesFilter(row)) continue;
                foldout.Add(MakeRow(row));
                shown++;
            }
            if (shown == 0) foldout.Add(MakeEmpty("No open windows."));

            var recent = NavigatorTools.RecentWindows(NavigatorGlobal.Data.recentWindowsCount, closedOnly: true)
                .Select(MakeRecentWindowRow).Where(PassesFilter).ToList();
            if (recent.Count > 0)
            {
                foldout.Add(MakeGroupHeader("Recently used", null));
                foreach (var row in recent) foldout.Add(MakeRow(row));
            }

            foldout.text = $"Windows ({shown})";
        }

        static VisualElement MakeGroupHeader(string title, Color? swatchColor)
        {
            var header = new VisualElement();
            header.AddToClassList("pn-tagheader");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 6;
            header.style.marginBottom = 2;
            if (swatchColor.HasValue)
            {
                var swatch = new VisualElement();
                swatch.style.width = 10;
                swatch.style.height = 10;
                swatch.style.marginLeft = 4;
                swatch.style.marginRight = 6;
                swatch.style.backgroundColor = swatchColor.Value;
                header.Add(swatch);
            }
            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginLeft = swatchColor.HasValue ? 0 : 6;
            header.Add(label);
            return header;
        }
    }
}
