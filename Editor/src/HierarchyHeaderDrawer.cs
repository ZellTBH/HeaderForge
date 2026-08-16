using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BreakingFrame.HeaderForge.Editor
{
    /// <summary>
    /// Draws specially named GameObjects as colored separator bars in the Hierarchy window.
    /// Naming format: "#RRGGBB=Label" for a flat bar, "#RRGGBB&gt;#RRGGBB=Label" for a
    /// horizontal gradient. Any other name is left to Unity's default drawing.
    /// Parsed entries are cached per instance id and rebuilt only when the hierarchy
    /// changes, so no parsing and no allocation happen while the window repaints.
    /// Gradient textures are cached by color key and destroyed before assembly reload.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyHeaderDrawer
    {
        #region Unity API

        static HierarchyHeaderDrawer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyChanged += ClearEntries;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseBackgrounds;
        }

        #endregion


        #region Tools and Utilities

        private static void OnHierarchyWindowItemOnGUI(int instanceId, Rect selectionRect)
        {
            // Selection, renaming and drag and drop keep working: only the repaint pass
            // is overridden, no event is ever consumed here.
            if (Event.current.type != EventType.Repaint)
                return;

            HeaderEntry entry = ResolveEntry(instanceId);
            if (!entry.m_isHeader)
                return;

            Debug.Assert(entry.m_background != null, "A header entry must carry a background texture.");
            Debug.Assert(!string.IsNullOrEmpty(entry.m_label), "A header entry must carry a label.");

            Rect row = new Rect(
                selectionRect.x - ROW_LEFT_EXTENSION,
                selectionRect.y,
                selectionRect.width + ROW_LEFT_EXTENSION + ROW_RIGHT_EXTENSION,
                selectionRect.height);

            // The opaque bar is drawn over the default row, which hides the icon and the
            // raw name, then the clean label is drawn centered on top of it.
            GUI.DrawTexture(row, entry.m_background, ScaleMode.StretchToFill);
            EditorGUI.LabelField(row, entry.m_label, ResolveLabelStyle());
        }

        private static HeaderEntry ResolveEntry(int instanceId)
        {
            if (_entries.TryGetValue(instanceId, out HeaderEntry cached))
                return cached;

            // EditorUtility.EntityIdToObject replaces this call from Unity 6000.3, but the
            // package floor is 6000.0 where that API does not exist. Scoped to this single
            // call so the assembly keeps reporting every other obsolete API.
#pragma warning disable CS0618
            GameObject owner = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618

            HeaderEntry entry = owner != null ? BuildEntry(owner.name) : default;
            _entries[instanceId] = entry;
            return entry;
        }

        /// <summary>
        /// Parses "#RRGGBB=Label" or "#RRGGBB&gt;#RRGGBB=Label". Returns a non-header
        /// entry as soon as any part of the format does not match, so a regular name
        /// starting with a hash is never mistaken for a header.
        /// </summary>
        private static HeaderEntry BuildEntry(string objectName)
        {
            HeaderEntry entry = default;
            if (string.IsNullOrEmpty(objectName) || objectName[0] != COLOR_PREFIX)
                return entry;

            int labelIndex = objectName.IndexOf(LABEL_SEPARATOR);
            if (labelIndex <= 0 || labelIndex == objectName.Length - 1)
                return entry;

            string colorKey = objectName.Substring(0, labelIndex);
            int gradientIndex = colorKey.IndexOf(GRADIENT_SEPARATOR);
            string leftHex = gradientIndex > 0 ? colorKey.Substring(0, gradientIndex) : colorKey;
            string rightHex = gradientIndex > 0 ? colorKey.Substring(gradientIndex + 1) : leftHex;

            if (!ColorUtility.TryParseHtmlString(leftHex, out Color leftColor))
                return entry;
            if (!ColorUtility.TryParseHtmlString(rightHex, out Color rightColor))
                return entry;

            entry.m_isHeader = true;
            entry.m_label = objectName.Substring(labelIndex + 1);
            entry.m_background = ResolveBackground(colorKey, leftColor, rightColor);
            return entry;
        }

        private static Texture2D ResolveBackground(string colorKey, Color leftColor, Color rightColor)
        {
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

        /// <summary>
        /// Built lazily: EditorStyles is not available yet when the static constructor
        /// runs on domain load.
        /// </summary>
        private static GUIStyle ResolveLabelStyle()
        {
            if (_labelStyle != null)
                return _labelStyle;

            _labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _labelStyle.normal.textColor = LABEL_COLOR;
            return _labelStyle;
        }

        /// <summary>Drops the parsed names; textures stay cached by color key.</summary>
        private static void ClearEntries() => _entries.Clear();

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

        /// <summary>Parsed drawing data for one hierarchy row.</summary>
        private struct HeaderEntry
        {
            public bool m_isHeader;
            public string m_label;
            public Texture2D m_background;
        }

        private const char COLOR_PREFIX = '#';
        private const char LABEL_SEPARATOR = '=';
        private const char GRADIENT_SEPARATOR = '>';
        private const int GRADIENT_WIDTH = 64;
        private const int INITIAL_ENTRY_CAPACITY = 64;
        private const int INITIAL_BACKGROUND_CAPACITY = 8;
        private const float ROW_LEFT_EXTENSION = 32f;
        private const float ROW_RIGHT_EXTENSION = 16f;

        private static readonly Color LABEL_COLOR = Color.white;

        private static readonly Dictionary<int, HeaderEntry> _entries =
            new Dictionary<int, HeaderEntry>(INITIAL_ENTRY_CAPACITY);

        private static readonly Dictionary<string, Texture2D> _backgrounds =
            new Dictionary<string, Texture2D>(INITIAL_BACKGROUND_CAPACITY);

        private static GUIStyle _labelStyle;

        #endregion
    }
}
