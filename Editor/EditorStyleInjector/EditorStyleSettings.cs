public struct OriginalColors {
    public Color Normal, Hover, Active, Focused;
    public Color OnNormal, OnHover, OnActive, OnFocused;
}

[CreateAssetMenu(fileName = "EditorStyleSettings", menuName = "UI Toolkit/Editor Style Settings")]
public class EditorStyleSettings : ScriptableObject
{
    
    [SerializeField] public StyleSheet[] m_styleToTakeFrom;
    [SerializeField] public bool m_autoApplyOnCompile;

    [Header( "IMGUI Font" )]
    [SerializeField]
    public Font m_fontToInject;

    [Header( "IMGUI Text Colours" )]
    [SerializeField]
    public bool m_overrideTextColors;
    [SerializeField] public Color m_normalColor = Color.white;
    [SerializeField] public Color m_hoverColor = Color.white;
    [SerializeField] public Color m_activeColor = Color.white;
    [SerializeField] public Color m_focusedColor = Color.white;

    [SerializeField] public bool m_overrideScrollViewBackground;
    [SerializeField] public Texture2D m_scrollViewBackground;

   [NonSerialized] public Dictionary<GUIStyle, Font> m_originalIMGUIFonts = new();
   [NonSerialized] public Dictionary<GUIStyle, OriginalColors> m_originalIMGUIColors = new();
   [NonSerialized] public Dictionary<GUIStyle, bool> m_originalRichText = new();
   [NonSerialized] public Dictionary<GUIStyle, Texture2D> m_originalScrollViewBackgrounds = new();
}