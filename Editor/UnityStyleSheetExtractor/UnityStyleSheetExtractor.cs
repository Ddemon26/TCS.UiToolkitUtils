using System.IO;
using System.Reflection;
using StyleSheet = UnityEngine.UIElements.StyleSheet;

public class UnityStyleSheetExtractor : EditorWindow {
    public enum NameType {
        Dark,
        Light,
        InspectorWindow,
        MainToolbar,
        EditorToolbarCommon,
        ToolbarDarkSystemFont,
        OverlayCommon,
        OverlayDark,
        SceneViewToolbarElements,
    }

    static readonly string[] k_DarkLookupNames = {
        "DefaultCommonDark_system font.uss", "DefaultCommonDark_system_font.uss", "DefaultCommonDark.uss",
        "DefaultCommonDark.uss.asset"
    };

    static readonly string[] k_LightLookupNames = {
        "DefaultCommonLight_system font.uss", "DefaultCommonLight_system_font.uss", "DefaultCommonLight.uss",
        "DefaultCommonLight.uss.asset"
    };

    static readonly string[] k_InspectorWindowNames = {
        "InspectorWindow_system font.uss", "InspectorWindow_system_font.uss", "InspectorWindow.uss",
        "InspectorWindow.uss.asset"
    };

    static readonly string[] k_MainToolbarNames = {
        "MainToolbar_system font.uss", "MainToolbar_system_font.uss", "MainToolbar.uss", "MainToolbar.uss.asset"
    };

    static readonly string[] k_EditorToolbarCommonNames = {
        "EditorToolbarCommon_system font.uss", "EditorToolbarCommon_system_font.uss", "EditorToolbarCommon.uss",
        "EditorToolbarCommon.uss.asset"
    };

    static readonly string[] k_ToolbarDarkSystemFontNames = {
        "ToolbarDark_system font.uss", "ToolbarDark_system_font.uss", "ToolbarDark.uss",
        "ToolbarDark.uss.asset"
    };

    static readonly string[] k_OverlayCommonNames = {
        "OverlayCommon_system font.uss", "OverlayCommon_system_font.uss", "OverlayCommon.uss",
        "OverlayCommon.uss.asset"
    };

    static readonly string[] k_OverlayDarkNames = {
        "OverlayDark_system font.uss", "OverlayDark_system_font.uss", "OverlayDark.uss", "OverlayDark.uss.asset"
    };

    static readonly string[] k_SceneViewToolbarElementsNames = {
        "SceneViewToolbarElements_system font.uss", "SceneViewToolbarElements_system_font.uss",
        "SceneViewToolbarElements.uss", "SceneViewToolbarElements.uss.asset"
    };

    static readonly Dictionary<NameType, string[]> k_NameLookups = new Dictionary<NameType, string[]> {
        { NameType.Dark, k_DarkLookupNames },
        { NameType.Light, k_LightLookupNames },
        { NameType.InspectorWindow, k_InspectorWindowNames },
        { NameType.MainToolbar, k_MainToolbarNames },
        { NameType.EditorToolbarCommon, k_EditorToolbarCommonNames },
        { NameType.ToolbarDarkSystemFont, k_ToolbarDarkSystemFontNames },
        { NameType.OverlayCommon, k_OverlayCommonNames },
        { NameType.OverlayDark, k_OverlayDarkNames },
        { NameType.SceneViewToolbarElements, k_SceneViewToolbarElementsNames }
    };

    [SerializeField] StyleSheet m_styleToTakeFrom;
    [SerializeField] StyleSheet m_styleToInsert;
    [SerializeField] NameType m_nameType = NameType.Dark;

    [MenuItem( "Window/UI Toolkit/Unity StyleSheet Extractor" )]
    public static void ShowWindow() {
        var wnd = GetWindow<UnityStyleSheetExtractor>();
        wnd.titleContent = new GUIContent( "Unity StyleSheet Extractor" );
    }

    void OnEnable() {
        if ( m_styleToTakeFrom == null )
            m_styleToTakeFrom = GetDefaultSystemFontSheet( m_nameType );
    }

    public void CreateGUI() {
        var root = rootVisualElement;
        root.Clear();

        var srcField = new ObjectField( "Style to take from" ) {
            objectType = typeof(StyleSheet),
            allowSceneObjects = false,
            value = m_styleToTakeFrom
        };
        srcField.RegisterValueChangedCallback( evt => m_styleToTakeFrom = evt.newValue as StyleSheet );

        var nameTypeField = new EnumField( "Style Name", m_nameType );
        nameTypeField.tooltip = "Select a built-in style sheet name to search for.";
        nameTypeField.RegisterValueChangedCallback( evt => {
                m_nameType = (NameType)evt.newValue;
                m_styleToTakeFrom = GetDefaultSystemFontSheet( m_nameType );
                srcField.value = m_styleToTakeFrom;
            }
        );
        root.Add( nameTypeField );
        root.Add( srcField );

        var dstField = new ObjectField( "Style to insert" ) {
            objectType = typeof(StyleSheet),
            allowSceneObjects = false,
            value = m_styleToInsert
        };
        dstField.RegisterValueChangedCallback( evt => m_styleToInsert = evt.newValue as StyleSheet );
        root.Add( dstField );

        root.Add( new Button( CloneStyles ) { text = "Clone Styles" } );

        root.Add(
            new Button( () => {
                    var sheet = CopySystemFontSheetIntoProject();
                    if ( sheet != null ) {
                        m_styleToTakeFrom = sheet;
                        srcField.value = sheet;
                    }
                }
            ) { text = "Copy built-in sheet into project" }
        );

        root.Add(
            new Button( () => {
                    string path = EditorUtility.OpenFilePanel( "Locate a .uss style sheet", "", "uss" );
                    if ( !string.IsNullOrEmpty( path ) ) {
                        string projectPath = "Assets/Editor/DefaultStyles/" + Path.GetFileName( path );
                        Directory.CreateDirectory( Path.GetDirectoryName( projectPath ) );
                        File.Copy( path, projectPath, true );
                        AssetDatabase.ImportAsset( projectPath );
                        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>( projectPath );
                        m_styleToTakeFrom = sheet;
                        srcField.value = sheet;
                    }
                }
            ) { text = "Manually pick .uss" }
        );
    }

    static StyleSheet GetDefaultSystemFontSheet(NameType nameType) {
        var folders = new[] { "StyleSheets/Generated/", "StyleSheets/" };
        if ( !k_NameLookups.TryGetValue( nameType, out var names ) )
            return null;

        foreach (string folder in folders) {
            foreach (string name in names) {
                var ss = EditorGUIUtility.Load( folder + name ) as StyleSheet;
                if ( ss != null ) return ss;
            }
        }

        return null;
    }

    string FindSystemFontSheetPath() {
        var roots = new List<string> { EditorApplication.applicationContentsPath };

        string localCache = Path.Combine(
            Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
            "Unity", "cache", "packages"
        );
        string commonCache = Path.Combine(
            Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ),
            "Unity", "cache", "packages"
        );
        if ( Directory.Exists( localCache ) ) roots.Add( localCache );
        if ( Directory.Exists( commonCache ) ) roots.Add( commonCache );

        if ( !k_NameLookups.TryGetValue( m_nameType, out var names ) )
            return null;

        string wildcardFragment = m_nameType.ToString();

        foreach (string root in roots) {
            try {
                foreach (string name in names) {
                    string found = Directory.EnumerateFiles( root, name, SearchOption.AllDirectories )
                        .FirstOrDefault();
                    if ( !string.IsNullOrEmpty( found ) ) return found.Replace( "\\", "/" );
                }

                string wildcard = Directory.EnumerateFiles( root, "*.uss", SearchOption.AllDirectories )
                    .FirstOrDefault( p => p.IndexOf( wildcardFragment, StringComparison.OrdinalIgnoreCase ) >= 0 &&
                                          p.IndexOf( "font", StringComparison.OrdinalIgnoreCase ) >= 0
                    );
                if ( !string.IsNullOrEmpty( wildcard ) ) return wildcard.Replace( "\\", "/" );
            }
            catch (Exception) { }
        }

        return null;
    }

    StyleSheet CopySystemFontSheetIntoProject() {
        string src = FindSystemFontSheetPath();
        if ( string.IsNullOrEmpty( src ) ) {
            EditorUtility.DisplayDialog(
                "Style sheet not found",
                $"Could not locate a matching {m_nameType}*font*.uss in this Unity install or cache.",
                "OK"
            );
            return null;
        }

        const string dstFolder = "Assets/Editor/DefaultStyles";
        Directory.CreateDirectory( dstFolder );
        string dst = Path.Combine( dstFolder, Path.GetFileName( src ) );

        File.Copy( src, dst, overwrite: true );
        AssetDatabase.ImportAsset( dst, ImportAssetOptions.ForceUpdate );
        return AssetDatabase.LoadAssetAtPath<StyleSheet>( dst );
    }

    void CloneStyles() {
        if ( m_styleToTakeFrom == null || m_styleToInsert == null ) {
            EditorUtility.DisplayDialog( "Error", "Both source and destination StyleSheets must be assigned.", "OK" );
            return;
        }

        if ( m_styleToTakeFrom == m_styleToInsert ) {
            EditorUtility.DisplayDialog( "Error", "Source and destination StyleSheets cannot be the same.", "OK" );
            return;
        }

        var type = typeof(StyleSheet);
        var fields = type.GetFields( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .Where( f => Attribute.IsDefined( f, typeof(SerializeField) ) );

        foreach (var field in fields)
            field.SetValue( m_styleToInsert, field.GetValue( m_styleToTakeFrom ) );

        type.GetMethod( "SetupReferences", BindingFlags.Instance | BindingFlags.NonPublic )
            ?.Invoke( m_styleToInsert, null );
        type.GetMethod(
            "FlattenImportedStyleSheetsRecursive", BindingFlags.Instance | BindingFlags.NonPublic, null,
            Type.EmptyTypes, null
        )?.Invoke( m_styleToInsert, null );

        EditorUtility.SetDirty( m_styleToInsert );
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Success",
            $"Cloned styles from '{m_styleToTakeFrom.name}' to '{m_styleToInsert.name}'.", "OK"
        );
    }
}