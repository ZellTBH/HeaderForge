using System.Collections.Generic;
using BreakingFrame.HeaderForge.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace BreakingFrame.HeaderForge.Editor
{
    /// <summary>
    /// Draws the separator as a full width colored banner in the Inspector.
    /// The Unity 6 Inspector is UI Toolkit, and OnHeaderGUI is never called for a component
    /// there, so the banner is built in CreateInspectorGUI and the default grey header is
    /// hidden afterwards by walking one level up the visual tree.
    /// That walk is the fragile part of this file, and it is deliberate. The header is an
    /// IMGUIContainer sitting next to the returned root inside the EditorElement Unity
    /// builds around every component: internal structure, undocumented, free to change in
    /// any future version. Every step of the search is guarded and a failure does nothing at
    /// all, not even a log. The worst case is the default header showing above the banner,
    /// which is ugly and still usable, instead of an exception on every repaint.
    /// The alternative was drawing the banner inside OnInspectorGUI and leaving the header
    /// alone, which costs two lines per separator and makes the stack noisier than it was
    /// without the tool.
    /// Hiding is symmetric: the container is kept and released in OnDisable. The Inspector
    /// recycles its EditorElement across rebuilds, so a slot left hidden comes back assigned
    /// to another component, which then disappears from the stack until the selection changes.
    /// Gradient textures are shared by color key across every separator and destroyed before
    /// an assembly reload. Alpha is forced to one before the key and before the interpolation:
    /// a banner is a background, and it also makes the RGB key exact by construction.
    /// The editing state lives in SessionState, keyed by instance id, so toggling it never
    /// dirties the prefab and it resets when the editor restarts.
    /// </summary>
    [InitializeOnLoad]
    [CustomEditor(typeof(ComponentSeparator))]
    public class ComponentSeparatorEditor : UnityEditor.Editor
    {
        #region Unity API

        static ComponentSeparatorEditor() => AssemblyReloadEvents.beforeAssemblyReload += ReleaseBackgrounds;

        private void OnEnable()
        {
            _labelProperty = serializedObject.FindProperty(LABEL_PROPERTY);
            _leftColorProperty = serializedObject.FindProperty(LEFT_COLOR_PROPERTY);
            _rightColorProperty = serializedObject.FindProperty(RIGHT_COLOR_PROPERTY);

            Debug.Assert(_labelProperty != null, "[ComponentSeparatorEditor] The label property was not found.", target);
            Debug.Assert(_leftColorProperty != null && _rightColorProperty != null,
                "[ComponentSeparatorEditor] A color property was not found.", target);
        }

        /// <summary>
        /// Gives the hidden header back to the USS instead of forcing a value on it: the
        /// EditorElement owns that container, and a StyleKeyword.Null lets whatever rule was
        /// driving it apply again once the slot is reused by another component.
        /// The null guard is not defensive noise: the element belongs to a tree the Inspector
        /// is free to tear down before this editor is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (_hiddenHeader == null)
                return;

            _hiddenHeader.style.display = StyleKeyword.Null;
            _hiddenHeader = null;
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            _banner = BuildBanner();
            _editingFields = BuildEditingFields();
            root.Add(_banner);
            root.Add(_editingFields);

            // The banner mirrors the serialized values, so an edit repaints it without
            // going through a selection change.
            root.TrackPropertyValue(_labelProperty, _ => RefreshLabel());
            root.TrackPropertyValue(_leftColorProperty, _ => RefreshBackground());
            root.TrackPropertyValue(_rightColorProperty, _ => RefreshBackground());

            // The default header does not exist yet at this point: it is created by the
            // EditorElement that will host this root. The first layout pass is the earliest
            // moment the sibling can be found.
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            return root;
        }

        public override bool HasPreviewGUI() => false;

        #endregion


        #region Tools and Utilities

        private VisualElement BuildBanner()
        {
            VisualElement banner = new VisualElement();
            // No flexGrow here: the root lays out in column, so growing would fight the
            // fixed height. Full width already comes from the default stretch alignment.
            banner.style.height = BANNER_HEIGHT;
            banner.style.marginLeft = -SIDE_BLEED;
            banner.style.marginRight = -SIDE_BLEED;
            banner.style.justifyContent = Justify.Center;

            _bannerLabel = new Label(ResolveLabel(_labelProperty.stringValue));
            _bannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _bannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bannerLabel.style.color = LABEL_COLOR;
            _bannerLabel.style.flexGrow = 1f;
            banner.Add(_bannerLabel);

            banner.AddManipulator(new ContextualMenuManipulator(PopulateContextMenu));
            ApplyBackground(banner);
            return banner;
        }

        /// <summary>
        /// Hidden by default and toggled by the context menu. The fields bind themselves:
        /// the Inspector binds the whole returned tree to the serializedObject.
        /// </summary>
        private VisualElement BuildEditingFields()
        {
            VisualElement fields = new VisualElement();
            fields.Add(new PropertyField(_labelProperty));
            fields.Add(new PropertyField(_leftColorProperty));
            fields.Add(new PropertyField(_rightColorProperty));
            fields.style.display = ResolveEditingDisplay();
            return fields;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            VisualElement root = evt.target as VisualElement;
            if (root == null)
                return;

            root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            HideDefaultHeader(root);
        }

        /// <summary>
        /// Hides the grey component header by climbing the ancestors of the returned root
        /// and, at each level, looking backwards among the siblings for the closest
        /// IMGUIContainer.
        /// The climb is the whole point and must not be flattened back into a single pass.
        /// What CreateInspectorGUI returns is not a direct child of the EditorElement: the
        /// Inspector wraps it in an InspectorElement first, so the header is a sibling of
        /// that wrapper, never of the root. Searching one level finds the root at index zero
        /// and gives up without ever examining a single container.
        /// The climb stops after MAX_ANCESTOR_CLIMB levels, which is exactly what it takes to
        /// reach the EditorElement through the InspectorElement wrapper. A third level would
        /// step out of the EditorElement and search the list of editors instead, where the
        /// closest IMGUIContainer backwards belongs to another component entirely.
        /// The container is stored so OnDisable can hand it back: hiding a slot the Inspector
        /// recycles is only safe as long as the hiding is undone.
        /// Silent on every miss: a null parent, a node sitting first among its siblings, or a
        /// tree shaped differently by a future Unity all leave the header visible and nothing
        /// else. Never log here, this runs on layout.
        /// </summary>
        private void HideDefaultHeader(VisualElement root)
        {
            VisualElement current = root;

            for (int level = 0; level < MAX_ANCESTOR_CLIMB; level++)
            {
                VisualElement parent = current.parent;
                if (parent == null)
                    return;

                int currentIndex = parent.IndexOf(current);
                for (int i = currentIndex - 1; i >= 0; i--)
                {
                    if (parent[i] is IMGUIContainer header)
                    {
                        header.style.display = DisplayStyle.None;
                        _hiddenHeader = header;
                        return;
                    }
                }

                current = parent;
            }
        }

        private void PopulateContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction(EDIT_ITEM, _ => ToggleEditing(), _ => ResolveEditingStatus());
            evt.menu.AppendAction(MOVE_UP_ITEM, _ => MoveUp());
            evt.menu.AppendAction(MOVE_DOWN_ITEM, _ => MoveDown());
            evt.menu.AppendAction(REMOVE_ITEM, _ => Remove());
        }

        private DropdownMenuAction.Status ResolveEditingStatus() =>
            IsEditing() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;

        private DisplayStyle ResolveEditingDisplay() => IsEditing() ? DisplayStyle.Flex : DisplayStyle.None;

        private bool IsEditing() => SessionState.GetBool(ResolveSessionKey(), false);

        private string ResolveSessionKey() => SESSION_KEY_PREFIX + target.GetInstanceID();

        private void ToggleEditing()
        {
            SessionState.SetBool(ResolveSessionKey(), !IsEditing());

            if (_editingFields != null)
                _editingFields.style.display = ResolveEditingDisplay();
        }

        private void MoveUp()
        {
            // Return ignored: false only means the separator already sits at the top of the
            // stack, which is not a failure and needs no feedback.
            ComponentUtility.MoveComponentUp(target as ComponentSeparator);
            RebuildInspector();
        }

        private void MoveDown()
        {
            // Return ignored: see MoveUp.
            ComponentUtility.MoveComponentDown(target as ComponentSeparator);
            RebuildInspector();
        }

        private void Remove()
        {
            Undo.DestroyObjectImmediate(target);
            RebuildInspector();
        }

        /// <summary>
        /// Without this the component list keeps drawing in its previous order, or keeps a
        /// destroyed separator on screen, until the selection changes.
        /// Deferred to delayCall because the caller is a ContextualMenuManipulator action:
        /// rebuilding straight away tears down the visual tree that is still dispatching the
        /// event, banner and menu included. The repaint is explicit because a rebuild alone
        /// does not mark the other open windows dirty.
        /// </summary>
        private static void RebuildInspector() => EditorApplication.delayCall += () =>
        {
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            InternalEditorUtility.RepaintAllViews();
        };

        private void RefreshLabel()
        {
            if (_bannerLabel == null || _labelProperty == null)
                return;

            _bannerLabel.text = ResolveLabel(_labelProperty.stringValue);
        }

        private void RefreshBackground()
        {
            if (_banner == null)
                return;

            ApplyBackground(_banner);
        }

        /// <summary>
        /// Read from the serialized properties rather than from the component: they are the
        /// values the Inspector is showing, whatever the write to the object has done yet.
        /// The size is an explicit full stretch, otherwise the 64 by 1 gradient tiles.
        /// </summary>
        private void ApplyBackground(VisualElement banner)
        {
            if (_leftColorProperty == null || _rightColorProperty == null)
                return;

            Texture2D texture = ResolveBackground(_leftColorProperty.colorValue, _rightColorProperty.colorValue);
            banner.style.backgroundImage = Background.FromTexture2D(texture);
            banner.style.backgroundSize =
                new BackgroundSize(Length.Percent(FULL_STRETCH_PERCENT), Length.Percent(FULL_STRETCH_PERCENT));
        }

        private static string ResolveLabel(string label) =>
            string.IsNullOrEmpty(label) ? FALLBACK_LABEL : label;

        /// <summary>
        /// Memoized per editor so a refresh never builds the color key string. The shared
        /// dictionary still holds one texture per color pair for every separator alive.
        /// Alpha is forced to one before anything else: a banner is a background, and a
        /// designer dragging the alpha slider would otherwise let the component list show
        /// through it. It also makes the RGB cache key exact by construction, since two
        /// colors that differ only in alpha now build the very same texture.
        /// The memo still compares the authored colors, so an alpha edit costs one lookup
        /// and resolves back to the same shared texture.
        /// </summary>
        private Texture2D ResolveBackground(Color leftColor, Color rightColor)
        {
            if (_background != null && _cachedLeftColor == leftColor && _cachedRightColor == rightColor)
                return _background;

            Color opaqueLeft = Opaque(leftColor);
            Color opaqueRight = Opaque(rightColor);

            string colorKey = ColorUtility.ToHtmlStringRGB(opaqueLeft) + ColorUtility.ToHtmlStringRGB(opaqueRight);
            _background = ResolveSharedBackground(colorKey, opaqueLeft, opaqueRight);
            _cachedLeftColor = leftColor;
            _cachedRightColor = rightColor;
            return _background;
        }

        private static Color Opaque(Color color) => new Color(color.r, color.g, color.b, 1f);

        /// <summary>Both colors are expected opaque already; the key is their RGB pair.</summary>
        private static Texture2D ResolveSharedBackground(string colorKey, Color leftColor, Color rightColor)
        {
            Debug.Assert(Mathf.Approximately(leftColor.a, 1f), "[ComponentSeparatorEditor] The left color must be opaque.");
            Debug.Assert(Mathf.Approximately(rightColor.a, 1f), "[ComponentSeparatorEditor] The right color must be opaque.");

            if (_backgrounds.TryGetValue(colorKey, out Texture2D cached) && cached != null)
                return cached;

            Texture2D texture = new Texture2D(GRADIENT_WIDTH, 1, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < GRADIENT_WIDTH; x++)
                texture.SetPixel(x, 0, Color.Lerp(leftColor, rightColor, x / (float)(GRADIENT_WIDTH - 1)));

            texture.Apply();
            _backgrounds[colorKey] = texture;
            return texture;
        }

        private static void ReleaseBackgrounds()
        {
            foreach (KeyValuePair<string, Texture2D> pair in _backgrounds)
            {
                if (pair.Value != null)
                    Object.DestroyImmediate(pair.Value);
            }
            _backgrounds.Clear();
        }

        #endregion


        #region Private and Protected

        private const string LABEL_PROPERTY = "m_label";
        private const string LEFT_COLOR_PROPERTY = "m_leftColor";
        private const string RIGHT_COLOR_PROPERTY = "m_rightColor";
        private const string SESSION_KEY_PREFIX = "HeaderForge.Editing.";
        private const string FALLBACK_LABEL = "HEADER";
        private const string EDIT_ITEM = "Edit";
        private const string MOVE_UP_ITEM = "Move Up";
        private const string MOVE_DOWN_ITEM = "Move Down";
        private const string REMOVE_ITEM = "Remove Header";

        // Two levels reach the EditorElement through the InspectorElement wrapper, which is
        // where the default header sits. A third would leave the EditorElement and let the
        // backwards search land on a foreign IMGUIContainer in the list of editors, hiding
        // another component instead of this header.
        private const int MAX_ANCESTOR_CLIMB = 2;

        private const int GRADIENT_WIDTH = 64;
        private const int INITIAL_BACKGROUND_CAPACITY = 8;
        private const float BANNER_HEIGHT = 22f;
        private const float SIDE_BLEED = 15f;
        private const float FULL_STRETCH_PERCENT = 100f;

        private static readonly Color LABEL_COLOR = Color.white;

        private static readonly Dictionary<string, Texture2D> _backgrounds =
            new Dictionary<string, Texture2D>(INITIAL_BACKGROUND_CAPACITY);

        private SerializedProperty _labelProperty;
        private SerializedProperty _leftColorProperty;
        private SerializedProperty _rightColorProperty;
        private VisualElement _banner;
        private VisualElement _editingFields;
        private Label _bannerLabel;
        private IMGUIContainer _hiddenHeader;
        private Texture2D _background;
        private Color _cachedLeftColor;
        private Color _cachedRightColor;

        #endregion
    }
}
