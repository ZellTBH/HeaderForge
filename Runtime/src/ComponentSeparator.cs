using UnityEngine;

namespace BreakingFrame.HeaderForge.Runtime
{
    /// <summary>
    /// Category banner in the component stack. Carries no logic and no Unity callback: it
    /// exists only to occupy one line in the Inspector, which its custom editor then draws
    /// as a full width colored bar. Unity offers no way to paint between two components,
    /// so a component that draws nothing but its own header is the only clean route.
    /// Several are expected on the same GameObject, which is why multiplicity is allowed.
    /// A flat bar is the degenerate case of a gradient: leave both colors equal and no
    /// toggle is needed to express it.
    /// Reordering the stack only makes sense in prefab mode. Moving a component on a scene
    /// instance records an ordering override that does not travel back to the prefab and
    /// leaves the two out of sync, so the separators are meant to be arranged on the asset.
    /// The component ships in builds. It is empty and costs nothing at runtime, while
    /// stripping it conditionally would break the reference and show a missing script on
    /// the prefab.
    /// </summary>
    [AddComponentMenu("Breaking Frame/Inspector Header")]
    public class ComponentSeparator : MonoBehaviour
    {
        #region Public

        [Header("Banner")]
        public string m_label = DEFAULT_LABEL;

        [Space(30), Header("Colors")]
        [Tooltip("Left end of the horizontal gradient.")]
        public Color m_leftColor = DEFAULT_LEFT_COLOR;

        [Tooltip("Right end of the horizontal gradient. Same value as the left one gives a flat bar.")]
        public Color m_rightColor = DEFAULT_RIGHT_COLOR;

        #endregion


        #region Private and Protected

        private const string DEFAULT_LABEL = "CATEGORY";

        private static readonly Color DEFAULT_LEFT_COLOR = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color DEFAULT_RIGHT_COLOR = new Color(0.28f, 0.31f, 0.38f, 1f);

        #endregion
    }
}
