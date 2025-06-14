/*using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Xml.Linq;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.UIElements;

public class FirstUxmlToCSharpConverter : EditorWindow {
    VisualTreeAsset m_uxmlFile;
    TextField m_outputClassNameField;
    TextField m_namespaceField;
    TextField m_outputFolderPathField;
    TextField m_csharpPreviewField;
    HelpBox m_statusHelpBox;
    Button m_generateButton;

    string m_generatedCSharpContent;

    class ElementInfo {
        public XElement XmlElement { get; set; }
        public string FieldName { get; set; }
        public string ElementType { get; set; }
        public ElementInfo Parent { get; set; }
        // ReSharper disable once CollectionNeverQueried.Local
        public List<ElementInfo> Children { get; } = new();
    }

    [MenuItem( "Tools/UXML to C# Class Converter" )]
    public static void ShowWindow() {
        var window = GetWindow<FirstUxmlToCSharpConverter>( "UXML to C# Converter" );
        window.minSize = new Vector2( 520, 600 );
    }

    public void CreateGUI() {
        var root = rootVisualElement;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;

        var headerLabel = new Label( "UXML to C# Class Converter" ) {
            style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 5 }
        };
        root.Add( headerLabel );
        root.Add( new Label( "Select a UXML file to generate a C# class that programmatically builds its hierarchy." ) );

        var configContainer = new VisualElement { style = { marginTop = 15, marginBottom = 15 } };
        var uxmlField = new ObjectField( "Source UXML File" ) { objectType = typeof(VisualTreeAsset), allowSceneObjects = false };
        uxmlField.RegisterValueChangedCallback( OnUxmlFileChanged );

        var pathContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5 } };
        m_outputFolderPathField = new TextField( "Output Folder" ) { isReadOnly = true, style = { flexGrow = 1 } };
        var browseButton = new Button( BrowseForSaveFolder ) { text = "Browse..." };
        pathContainer.Add( m_outputFolderPathField );
        pathContainer.Add( browseButton );

        m_namespaceField = new TextField( "Namespace" ) { value = "MyProject.UI" };
        m_outputClassNameField = new TextField( "Output Class Name" );

        configContainer.Add( uxmlField );
        configContainer.Add( pathContainer );
        configContainer.Add( m_namespaceField );
        configContainer.Add( m_outputClassNameField );
        root.Add( configContainer );

        root.Add( new Label( "C# Class Preview" ) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } } );
        m_csharpPreviewField = new TextField { multiline = true, isReadOnly = true };
        var previewScrollView = new ScrollView( ScrollViewMode.VerticalAndHorizontal ) { style = { flexGrow = 1, minHeight = 200 } };
        previewScrollView.Add( m_csharpPreviewField );
        root.Add( previewScrollView );

        m_statusHelpBox = new HelpBox( "", HelpBoxMessageType.None ) { style = { display = DisplayStyle.None, marginTop = 10 } };
        root.Add( m_statusHelpBox );
        m_generateButton = new Button( GenerateCSharpFile ) { text = "Generate C# File", style = { height = 30, marginTop = 5 } };
        root.Add( m_generateButton );
        root.style.paddingBottom = 10;

        SetInitialState();
    }

    void SetInitialState() {
        m_outputFolderPathField.SetEnabled( false );
        m_outputClassNameField.SetEnabled( false );
        m_namespaceField.SetEnabled( false );
        m_generateButton.SetEnabled( false );
        m_csharpPreviewField.value = "Select a UXML file to see a preview of the generated C# class.";
    }

    void BrowseForSaveFolder() {
        string startPath = string.IsNullOrEmpty( m_outputFolderPathField.value ) ? "Assets" : m_outputFolderPathField.value;
        string chosenPath = EditorUtility.SaveFolderPanel( "Choose a location for the C# file", startPath, "" );
        if ( !string.IsNullOrEmpty( chosenPath ) ) {
            m_outputFolderPathField.value = "Assets" + chosenPath.Substring( Application.dataPath.Length );
        }
    }

    void OnUxmlFileChanged(ChangeEvent<Object> evt) {
        m_uxmlFile = evt.newValue as VisualTreeAsset;
        bool hasFile = m_uxmlFile;

        m_outputFolderPathField.SetEnabled( hasFile );
        m_outputClassNameField.SetEnabled( hasFile );
        m_namespaceField.SetEnabled( hasFile );
        m_generateButton.SetEnabled( hasFile );

        if ( hasFile ) {
            string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
            m_outputFolderPathField.value = Path.GetDirectoryName( uxmlPath );
            string baseName = Path.GetFileNameWithoutExtension( uxmlPath ).Replace( " ", "" );
            m_outputClassNameField.value = $"{baseName}Element";
            GenerateAndDisplayPreview();
            ShowStatus( "Ready to generate C# class file.", HelpBoxMessageType.Info );
        }
        else {
            SetInitialState();
            ShowStatus( "", HelpBoxMessageType.None );
        }
    }

    void GenerateAndDisplayPreview() {
        if ( !m_uxmlFile ) {
            return;
        }

        try {
            string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
            var doc = XDocument.Load( uxmlPath );
            XNamespace ui = "UnityEngine.UIElements";

            List<XElement> instantiableElements = doc.Descendants()
                .Where( d => d.Name.Namespace == ui && d.Name.LocalName != "UXML" && d.Name.LocalName != "Style" )
                .ToList();

            if ( !instantiableElements.Any() ) {
                ShowStatus( "No valid VisualElements found in UXML to generate a class from.", HelpBoxMessageType.Error );
                return;
            }

            Dictionary<XElement, ElementInfo> elementMap = new();
            Dictionary<string, int> nameCounters = new();

            //var rootInfo = new ElementInfo { XmlElement = instantiableElements.First() };

            foreach (var element in instantiableElements) {
                var info = new ElementInfo { XmlElement = element, ElementType = element.Name.LocalName };
                info.FieldName = GenerateFieldName( info, nameCounters );

                var parentXml = element.Parent;

                if ( parentXml != null && elementMap.TryGetValue( parentXml, out var parentInfo ) ) {
                    info.Parent = parentInfo;
                    parentInfo.Children.Add( info );
                }

                elementMap[element] = info;
            }

            var sb = new StringBuilder();
            string className = m_outputClassNameField.value;
            string namespaceName = m_namespaceField.value;

            sb.AppendLine( "using UnityEngine.UIElements;" );
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrWhiteSpace( namespaceName );
            if ( hasNamespace ) {
                sb.AppendLine( $"namespace {namespaceName}\n{{" );
            }

            string indent = hasNamespace ? "    " : "";

            sb.AppendLine( $"{indent}[UxmlElement] public partial class {className} : VisualElement\n{indent}{{" );

            // Field Declarations
            foreach (var info in elementMap.Values) {
                string nameInQuotes = info.XmlElement.Attribute( "name" )?.Value ?? info.FieldName.Substring( 2 );
                sb.AppendLine( $"{indent}    [USSName] readonly {info.ElementType} {info.FieldName} = new() {{ name = \"{nameInQuotes}\" }};" );
            }

            sb.AppendLine();

            sb.AppendLine( $"{indent}    public {className}()\n{indent}    {{" );
            sb.AppendLine( $"{indent}        SetElementClassNames(); // Assumes a Source Generator handles this." );
            sb.AppendLine();
            sb.AppendLine( $"{indent}        // --- Build Hierarchy ---" );

            foreach (var info in elementMap.Values) {
                if ( info.Parent == null ) {
                    sb.AppendLine( $"{indent}        hierarchy.Add({info.FieldName});" );
                }
                else {
                    sb.AppendLine( $"{indent}        {info.Parent.FieldName}.Add({info.FieldName});" );
                }
            }

            sb.AppendLine( $"{indent}    }}" );
            sb.AppendLine( $"{indent}}}" );
            if ( hasNamespace ) {
                sb.AppendLine( "}" );
            }

            m_generatedCSharpContent = sb.ToString();
            m_csharpPreviewField.value = m_generatedCSharpContent;
        }
        catch (System.Exception ex) {
            m_generatedCSharpContent = "";
            m_csharpPreviewField.value = $"/* Error generating preview: {ex.Message} #1#";
            ShowStatus( "Error during preview generation. Check console.", HelpBoxMessageType.Error );
            Debug.LogError( ex );
        }
    }

    string GenerateFieldName(ElementInfo info, Dictionary<string, int> counters) {
        string baseName = info.XmlElement.Attribute( "name" )?.Value ?? info.ElementType;

        baseName = Regex.Replace( baseName, @"[^\w]", " " );
        baseName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase( baseName ).Replace( " ", "" );

        if ( !counters.TryAdd( baseName, 1 ) ) {
            counters[baseName]++;
        }

        if ( counters[baseName] > 1 ) {
            baseName += counters[baseName];
        }

        return $"m_{char.ToLowerInvariant( baseName[0] )}{baseName.Substring( 1 )}";
    }

    void ShowStatus(string message, HelpBoxMessageType type) {
        m_statusHelpBox.text = message;
        m_statusHelpBox.messageType = type;
        m_statusHelpBox.style.display = string.IsNullOrEmpty( message ) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void GenerateCSharpFile() {
        string className = m_outputClassNameField.value;
        if ( string.IsNullOrWhiteSpace( className ) ) {
            ShowStatus( "Error: Class Name cannot be empty.", HelpBoxMessageType.Error );
            return;
        }

        string fullPath = Path.Combine( m_outputFolderPathField.value, $"{className}.cs" );

        if ( File.Exists( fullPath ) ) {
            if ( !EditorUtility.DisplayDialog( "File Exists", $"The file '{className}.cs' already exists. Overwrite it?", "Overwrite", "Cancel" ) ) {
                ShowStatus( "Generation cancelled by user.", HelpBoxMessageType.Warning );
                return;
            }
        }

        try {
            if ( string.IsNullOrEmpty( m_generatedCSharpContent ) ) {
                ShowStatus( "Nothing to save. Generate a preview first.", HelpBoxMessageType.Error );
                return;
            }

            File.WriteAllText( fullPath, m_generatedCSharpContent );
            AssetDatabase.Refresh();
            ShowStatus( $"Success! C# class saved to:\n{fullPath}", HelpBoxMessageType.Info );
        }
        catch (System.Exception ex) {
            ShowStatus( $"An error occurred while saving file: {ex.Message}", HelpBoxMessageType.Error );
            Debug.LogError( $"[UxmlToCSharpConverter] Failed to save file: {ex}" );
        }
    }
}*/