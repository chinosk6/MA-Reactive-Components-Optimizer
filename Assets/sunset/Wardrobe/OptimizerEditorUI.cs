#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace sunset.Wardrobe
{
    public class OptimizerEditorUI : EditorWindow
    {
        private const string IconAssetPath = "Assets/sunset/icon/sunset.png";
        private const string LangPrefKey   = "MAOptimizer_Lang";

        private enum Language { English = 0, Chinese = 1 }
        private Language _lang;

        private GameObject _avatar;
        private Vector2    _scrollPos;

        private readonly List<LighterMAToggle.LighterMaToggle>      _maToggles   = new();
        private readonly List<MaterialChanger.MaterialChangerModel> _matChangers = new();

        #region ── Descriptions ──────────────────────────────────────────────
        private readonly string _descEn = @"This tool mitigates lag caused by MA reactive components occupying too many Animator layers.

Applicable scope: components where MA Object Toggle and MA Material Setter are driven **only** by MA Menu Items. Do NOT use it in other setups.

Usage:
---- Menu Object Toggle Optimizer ----
Place the component anywhere under the avatar (only **one** per avatar).
• Excludes With Empty Clip – Adds the specified objects to the exclusion list without deleting their animations. A blank clip placeholder is inserted in the generated Blend Tree (recommended).
• Excludes With No Clip – Adds the specified objects to the exclusion list, keeps their animations, and inserts **no** placeholder in the Blend Tree.

---- Material Setter Optimizer ----
Attach it to objects that contain **both** MA Menu Item and MA Material Setter; the plugin optimises them automatically.";

        private readonly string _descZh = @"本工具用于优化 MA 响应式组件占用过多 Animator Layer 导致模型卡顿的问题。

适用范围：仅通过 MA Menu Item 控制 MA Object Toggle 与 MA Material Setter 的组件。其他场景 **不适用** 本工具。

使用方法：
---- 菜单对象开关优化组件 ----
放置在模型任意位置。每个模型 **只能** 有一个该组件。
• Excludes With Empty Clip：将指定对象加入排除名单，不删除其动画。在生成的 Blend Tree 中以空动画占位（推荐）。
• Excludes With No Clip：将指定对象加入排除名单，不删除其动画，也 **不** 在 Blend Tree 中生成占位动画。

---- 材质设置优化组件 ----
放置在同时存在 MA Menu Item 和 MA Material Setter 的对象上，插件会自动优化。";
        #endregion

        #region ── Window lifecycle ──────────────────────────────────────────
        [MenuItem("Tools/sunset/MA Optimizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<OptimizerEditorUI>();
            window.SetWindowTitleAndIcon();
        }

        private void OnEnable()
        {
            _lang = (Language)EditorPrefs.GetInt(LangPrefKey, 0);
            SetWindowTitleAndIcon();
            Undo.undoRedoPerformed += RefreshLists;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= RefreshLists;
        private void OnHierarchyChange() { if (_avatar) RefreshLists(); }
        #endregion

        #region ── GUI ───────────────────────────────────────────────────────
        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _avatar = (GameObject)EditorGUILayout.ObjectField(L("Avatar Root", "Avatar根节点"), _avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) RefreshLists();

            using (new EditorGUI.DisabledScope(_avatar == null))
            {
                DrawComponentLists();
                GUILayout.Space(10);
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_avatar == null))
            {
                DrawDescription();
                DrawButtons();
            }

            DrawLanguageSelector();
        }

        private void DrawComponentLists()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // ── Menu Object Toggle Optimizer list ──
            GUILayout.Label(L("Menu Object Toggle Optimizer", "菜单对象开关优化组件"), EditorStyles.boldLabel);
            int toggleCount = _maToggles.Count(x => x);
            if (toggleCount > 1) EditorGUILayout.HelpBox(L($"Detected {toggleCount}; only one allowed.", $"检测到 {toggleCount} 个组件，预期仅一个。"), MessageType.Error);
            foreach (var t in _maToggles) if (t) DrawReadOnlyObjectField(t.gameObject);

            GUILayout.Space(8);

            // ── Material Setter Optimizer list ──
            GUILayout.Label(L("Material Setter Optimizer", "材质设置优化组件"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("Select All", "全选"), GUILayout.Width(100))) SelectAllMatChangers();
            if (GUILayout.Button(L("Find matching objects", "寻找符合条件的部件"), GUILayout.Width(160))) SelectMenuSetterObjects();
            if (GUILayout.Button(L("Find optimisable objects", "寻找所有可优化部件"), GUILayout.Width(180))) SelectMenuSetterObjectsWithoutOptimizer();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            foreach (var c in _matChangers) if (c) DrawReadOnlyObjectField(c.gameObject);

            EditorGUILayout.EndScrollView();
        }

        private void DrawButtons()
        {
            bool hasToggle = _maToggles.Any(x => x);
            using (new EditorGUI.DisabledScope(hasToggle))
            {
                if (GUILayout.Button(L("Create Menu Object Toggle Optimizer", "创建菜单对象开关优化组件"))) CreateToggleHolder();
            }
            if (GUILayout.Button(L("Add Material Setter Optimizer to all menu+setter", "为所有菜单+Setter添加材质优化组件"))) AddMaterialChangerModels();
            if (GUILayout.Button(L("Remove all Material Setter Optimizers", "移除所有材质优化组件"))) RemoveAllMaterialChangerModels();
        }

        private void DrawDescription() => EditorGUILayout.HelpBox(L(_descEn, _descZh), MessageType.None);

        private void DrawLanguageSelector()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
            GUILayout.FlexibleSpace();
            var newLang = (Language)EditorGUILayout.EnumPopup(_lang, GUILayout.MaxWidth(140));
            EditorGUILayout.EndHorizontal();
            if (newLang != _lang) { _lang = newLang; EditorPrefs.SetInt(LangPrefKey, (int)_lang); }
        }
        #endregion

        #region ── Helpers & Utility ─────────────────────────────────────────
        private string L(string en, string zh) => _lang == Language.English ? en : zh;

        private void DrawReadOnlyObjectField(GameObject obj)
        {
            Rect r = EditorGUILayout.GetControlRect(false);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.ObjectField(r, obj, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { SelectObject(obj); Event.current.Use(); }
        }

        private void SetWindowTitleAndIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath) ?? (Texture2D)EditorGUIUtility.IconContent("d_FilterByType").image;
            titleContent = new GUIContent("MA Optimizer", icon);
        }

        private void RefreshLists()
        {
            _maToggles.Clear();
            _matChangers.Clear();
            if (!_avatar) { Repaint(); return; }
            _maToggles.AddRange(_avatar.GetComponentsInChildren<LighterMAToggle.LighterMaToggle>(true));
            _matChangers.AddRange(_avatar.GetComponentsInChildren<MaterialChanger.MaterialChangerModel>(true));
            Repaint();
        }

        private static void SelectObject(GameObject go) { Selection.activeObject = go; EditorGUIUtility.PingObject(go); }

        private void SelectAllMatChangers() => Selection.objects = _matChangers.Where(c => c).Select(c => (Object)c.gameObject).ToArray();

        private void SelectMenuSetterObjects()
        {
            Selection.objects = FindMenuSetterObjects(includeOptimised: true);
        }

        private void SelectMenuSetterObjectsWithoutOptimizer()
        {
            Selection.objects = FindMenuSetterObjects(includeOptimised: false);
        }

        private Object[] FindMenuSetterObjects(bool includeOptimised)
        {
            if (!_avatar) return System.Array.Empty<Object>();
            var setters = _avatar.GetComponentsInChildren<ModularAvatarMaterialSetter>(true);
            IEnumerable<GameObject> targets = setters.Select(s => s.GetComponent<ModularAvatarMenuItem>())
                                                    .Where(mi => mi)
                                                    .Select(mi => mi.gameObject);
            if (!includeOptimised)
            {
                targets = targets.Where(go => !go.GetComponent<MaterialChanger.MaterialChangerModel>());
            }
            return targets.Distinct().Cast<Object>().ToArray();
        }

        private void CreateToggleHolder()
        {
            var holder = new GameObject("Menu Object Toggle Optimizer");
            Undo.RegisterCreatedObjectUndo(holder, "Create Menu Toggle Optimizer");
            holder.transform.SetParent(_avatar.transform, false);
            holder.AddComponent<LighterMAToggle.LighterMaToggle>();
            RefreshLists();
        }

        private void AddMaterialChangerModels()
        {
            if (!_avatar) return;
            var menuItems = _avatar.GetComponentsInChildren<ModularAvatarMenuItem>(true);
            int added = 0;
            foreach (var mi in menuItems)
            {
                if (!mi.GetComponent<ModularAvatarMaterialSetter>()) continue;
                if (!mi.GetComponent<MaterialChanger.MaterialChangerModel>()) { Undo.AddComponent<MaterialChanger.MaterialChangerModel>(mi.gameObject); added++; }
            }
            Debug.Log(L($"[MA Optimizer] Added {added} Material Setter Optimizers.", $"[MA Optimizer] 已添加 {added} 个材质优化组件"));
            RefreshLists();
        }

        private void RemoveAllMaterialChangerModels()
        {
            if (!_avatar) return;
            var toRemove = _avatar.GetComponentsInChildren<MaterialChanger.MaterialChangerModel>(true);
            int removed = 0;
            foreach (var c in toRemove) { Undo.DestroyObjectImmediate(c); removed++; }
            Debug.Log(L($"[MA Optimizer] Removed {removed} Material Setter Optimizers.", $"[MA Optimizer] 已移除 {removed} 个材质优化组件"));
            RefreshLists();
        }
        #endregion
    }
}
#endif
