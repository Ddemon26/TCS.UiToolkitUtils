using System.Reflection;
[DefaultExecutionOrder(9999)]
[InitializeOnLoad] public static class EditorStyleReloader {
    static int s_lastKnownViewCount;
    static readonly Type GUIViewType;
    
    //static EditorStyleSettings s_settings;

    static EditorStyleReloader() {
        GUIViewType = typeof(EditorWindow).Assembly.GetType( "UnityEditor.GUIView" );
        if ( GUIViewType == null ) {
            return;
        }
        
        // s_settings = Resources.FindObjectsOfTypeAll<EditorStyleSettings>().FirstOrDefault();
        // if ( s_settings == null ) {
        //     Debug.LogError( "EditorStyleInjector: EditorStyleSettings not found in the project. Please create one." );
        //     return;
        // }

        EditorApplication.update += OnEditorUpdate;
        s_lastKnownViewCount = GetViewCount();
        //ApplyStyles();
    }

    static void OnEditorUpdate() {
        int currentViewCount = GetViewCount();
        if ( currentViewCount == s_lastKnownViewCount ) {
            return;
        }

        s_lastKnownViewCount = currentViewCount;
        ApplyStyles();
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    static void OnScriptsReloaded() {
        
        //EditorApplication.delayCall += ApplyStyles;
    }

    static int GetViewCount() {
        return GUIViewType != null ? Resources.FindObjectsOfTypeAll( GUIViewType ).Length : 0;
    }

    static void ApplyStyles() {
        EditorStyleInjector[] windows = Resources.FindObjectsOfTypeAll<EditorStyleInjector>();
        if ( windows.Length == 0 ) {
            Debug.LogWarning( "EditorStyleInjector: No EditorStyleInjector windows found. Please open the EditorStyleInjector window." );
            return;
        }
        foreach (var window in windows) {
            window.InjectStyle();
        }
    }
}

public class EditorStyleInjector : EditorWindow {
    // struct OriginalColors {
    //     public Color Normal, Hover, Active, Focused;
    //     public Color OnNormal, OnHover, OnActive, OnFocused;
    // }

    /*[SerializeField] StyleSheet[] m_styleToTakeFrom;
    [SerializeField] internal bool m_autoApplyOnCompile;

    [Header( "IMGUI Font" )]
    [SerializeField]
    Font m_fontToInject;

    [Header( "IMGUI Text Colours" )]
    [SerializeField]
    bool m_overrideTextColors;
    [SerializeField] Color m_normalColor = Color.white;
    [SerializeField] Color m_hoverColor = Color.white;
    [SerializeField] Color m_activeColor = Color.white;
    [SerializeField] Color m_focusedColor = Color.white;

    [SerializeField] bool m_overrideScrollViewBackground;
    [SerializeField] Texture2D m_scrollViewBackground;

    readonly Dictionary<GUIStyle, Font> m_originalIMGUIFonts = new();
    readonly Dictionary<GUIStyle, OriginalColors> m_originalIMGUIColors = new();
    readonly Dictionary<GUIStyle, bool> m_originalRichText = new();
    readonly Dictionary<GUIStyle, Texture2D> m_originalScrollViewBackgrounds = new();*/
    
    [SerializeField] EditorStyleSettings settings;

    // public void SetSettings(EditorStyleSettings newSettings) {
    //     settings = newSettings;
    // }

    [MenuItem( "Window/UI Toolkit/EditorStyleInjector" )]
    public static void ShowWindow() {
        var wnd = GetWindow<EditorStyleInjector>();
        wnd.titleContent = new GUIContent( "EditorStyleInjector" );
        
    }

    public void CreateGUI() {
        var root = rootVisualElement;
        var so = new SerializedObject( this );

        /*root.Add( new PropertyField( so.FindProperty( nameof(m_styleToTakeFrom) ), "Styles to take from" ) );
        root.Add( new PropertyField( so.FindProperty( nameof(m_autoApplyOnCompile) ), "Auto Apply On Compile" ) );
        root.Add( new PropertyField( so.FindProperty( nameof(m_fontToInject) ), "Font to Inject" ) );

        var fold = new Foldout { text = "IMGUI Text Colour Overrides" };
        fold.Add( new PropertyField( so.FindProperty( nameof(m_overrideTextColors) ) ) );
        fold.Add( new PropertyField( so.FindProperty( nameof(m_normalColor) ) ) );
        fold.Add( new PropertyField( so.FindProperty( nameof(m_hoverColor) ) ) );
        fold.Add( new PropertyField( so.FindProperty( nameof(m_activeColor) ) ) );
        fold.Add( new PropertyField( so.FindProperty( nameof(m_focusedColor) ) ) );
        root.Add( fold );

        var scrollViewFold = new Foldout { text = "IMGUI ScrollView" };
        scrollViewFold.Add( new PropertyField( so.FindProperty( nameof(m_overrideScrollViewBackground) ), "Override Background" ) );
        scrollViewFold.Add( new PropertyField( so.FindProperty( nameof(m_scrollViewBackground) ), "ScrollView Background Texture" ) );
        root.Add( scrollViewFold );*/
        
        root.Add((new PropertyField((so.FindProperty( nameof(settings) )))));

        root.Add( new Button( InjectStyle ) { text = "Inject Style" } );
        root.Add( new Button( RemoveStyle ) { text = "Remove Style" } );

        root.Bind( so );
    }

    internal void InjectStyle() {
        if (!settings.m_autoApplyOnCompile) return;
        // 1) UIElements style‑sheets
        if ( settings.m_styleToTakeFrom is { Length: > 0 } ) {
            InjectStyleSheets();
        }

        // 2) Collect every GUIStyle we can find (skins + static fields)
        HashSet<GUIStyle> styles = CollectSkinStyles();
        styles.UnionWith( CollectStaticStyles() );

        // 3) Inject font / colours / richText
        foreach (var style in styles) {
            // remember original richText flag
            if ( !settings.m_originalRichText.ContainsKey( style ) ) {
                settings.m_originalRichText[style] = style.richText;
            }

            style.richText = true;

            // font
            if ( settings.m_fontToInject != null ) {
                if ( !settings.m_originalIMGUIFonts.ContainsKey( style ) ) {
                    settings.m_originalIMGUIFonts[style] = style.font;
                }

                style.font = settings.m_fontToInject;
            }

            // colours
            if ( settings.m_overrideTextColors ) {
                if ( !settings.m_originalIMGUIColors.ContainsKey( style ) ) {
                    settings.m_originalIMGUIColors[style] = new OriginalColors {
                        Normal = style.normal.textColor,
                        Hover = style.hover.textColor,
                        Active = style.active.textColor,
                        Focused = style.focused.textColor,
                        OnNormal = style.onNormal.textColor,
                        OnHover = style.onHover.textColor,
                        OnActive = style.onActive.textColor,
                        OnFocused = style.onFocused.textColor
                    };
                }

                style.normal.textColor = settings.m_normalColor;
                style.hover.textColor = settings.m_hoverColor;
                style.active.textColor = settings.m_activeColor;
                style.focused.textColor = settings.m_focusedColor;
                style.onNormal.textColor = settings.m_normalColor;
                style.onHover.textColor = settings.m_hoverColor;
                style.onActive.textColor = settings.m_activeColor;
                style.onFocused.textColor = settings.m_focusedColor;
            }
        }

        // 4) Specific style overrides
        if (settings.m_overrideScrollViewBackground && settings.m_scrollViewBackground != null) {
            GUISkin[] skins = Resources.FindObjectsOfTypeAll<GUISkin>();
            foreach (var skin in skins) {
                if ( skin.scrollView == null ) {
                    continue;
                }

                var style = skin.scrollView;
                if ( !settings.m_originalScrollViewBackgrounds.ContainsKey( style ) ) {
                    settings.m_originalScrollViewBackgrounds[style] = style.normal.background;
                }

                var tex = settings.m_scrollViewBackground;
                style.normal.background = tex;
            }
        }
    }

    public void InjectStyleSheets() {
        var guiViewType = typeof(EditorWindow).Assembly.GetType( "UnityEditor.GUIView" );
        var visualTreeProperty = guiViewType?.GetProperty( "visualTree", BindingFlags.NonPublic | BindingFlags.Instance );
        if ( visualTreeProperty == null ) {
            return;
        }

        foreach (var view in Resources.FindObjectsOfTypeAll( guiViewType )) {
            if ( visualTreeProperty.GetValue( view ) is not VisualElement vt ) {
                continue;
            }

            foreach (var sheet in settings.m_styleToTakeFrom) {
                if ( sheet == null ) {
                    continue;
                }

                vt.styleSheets.Remove( sheet );
                vt.styleSheets.Add( sheet );
            }
        }
    }

    // Collect every GUIStyle referenced by every GUISkin
    static HashSet<GUIStyle> CollectSkinStyles() {
        HashSet<GUIStyle> set = new();
        GUISkin[] skins = Resources.FindObjectsOfTypeAll<GUISkin>();

        foreach (var skin in skins) {
            if ( skin == null ) {
                continue;
            }

            Add( skin.box );
            Add( skin.button );
            Add( skin.toggle );
            Add( skin.label );
            Add( skin.textField );
            Add( skin.textArea );
            Add( skin.window );

            Add( skin.horizontalSlider );
            Add( skin.horizontalSliderThumb );
            Add( skin.verticalSlider );
            Add( skin.verticalSliderThumb );

            Add( skin.horizontalScrollbar );
            Add( skin.horizontalScrollbarThumb );
            Add( skin.horizontalScrollbarLeftButton );
            Add( skin.horizontalScrollbarRightButton );

            Add( skin.verticalScrollbar );
            Add( skin.verticalScrollbarThumb );
            Add( skin.verticalScrollbarUpButton );
            Add( skin.verticalScrollbarDownButton );

            Add( skin.scrollView );

            if ( skin.customStyles != null ) {
                foreach (var c in skin.customStyles) {
                    Add( c );
                }
            }

            continue;

            void Add(GUIStyle s) {
                if ( s != null ) {
                    set.Add( s );
                }
            }
        }

        return set;
    }

    static HashSet<GUIStyle> CollectStaticStyles() {
        HashSet<GUIStyle> set = new();
        var asm = typeof(EditorGUIUtility).Assembly; // UnityEditor assembly

        IEnumerable<FieldInfo> fields = asm.GetTypes()
            .Where( t => !t.ContainsGenericParameters ) // avoid InvalidOperationException
            .SelectMany( t => t.GetFields(
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic
                         )
            )
            .Where( f => f.FieldType == typeof(GUIStyle) );

        foreach (var f in fields) {
            GUIStyle s;
            try { s = (GUIStyle)f.GetValue( null ); }
            catch { continue; } // open generic, etc.

            if ( s != null ) {
                set.Add( s );
            }
        }

        return set;
    }

    void RemoveStyle() {
        // UIElements style‑sheets
        if ( settings.m_styleToTakeFrom is { Length: > 0 } ) {
            var guiViewType = typeof(EditorWindow).Assembly.GetType( "UnityEditor.GUIView" );
            var visualTreeProperty = guiViewType?.GetProperty( "visualTree", BindingFlags.NonPublic | BindingFlags.Instance );
            if ( visualTreeProperty != null ) {
                foreach (var view in Resources.FindObjectsOfTypeAll( guiViewType )) {
                    if ( visualTreeProperty.GetValue( view ) is not VisualElement vt ) {
                        continue;
                    }

                    foreach (var sheet in settings.m_styleToTakeFrom) {
                        if ( sheet != null && vt.styleSheets.Contains( sheet ) ) {
                            vt.styleSheets.Remove( sheet );
                        }
                    }
                }
            }
        }

        // scrollView background
        foreach (var (style, tex) in settings.m_originalScrollViewBackgrounds) {
            if ( style != null ) {
                style.normal.background = tex;
            }
        }
        
        settings.m_originalScrollViewBackgrounds.Clear();

        // fonts
        foreach (var (style, font) in settings.m_originalIMGUIFonts) {
            if ( style != null ) {
                style.font = font;
            }
        }

        settings.m_originalIMGUIFonts.Clear();

        // colours
        foreach (var (style, c) in settings.m_originalIMGUIColors) {
            if ( style != null ) {
                style.normal.textColor = c.Normal;
                style.hover.textColor = c.Hover;
                style.active.textColor = c.Active;
                style.focused.textColor = c.Focused;
                style.onNormal.textColor = c.OnNormal;
                style.onHover.textColor = c.OnHover;
                style.onActive.textColor = c.OnActive;
                style.onFocused.textColor = c.OnFocused;
            }
        }

        settings.m_originalIMGUIColors.Clear();

        // richText
        foreach ((var style, bool wasRich) in settings.m_originalRichText) {
            if ( style != null ) {
                style.richText = wasRich;
            }
        }

        settings.m_originalRichText.Clear();
    }
}