/*// Place this script in a folder named "Editor" in your Assets folder.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Xml.Linq;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;

public class FirstUxmlStyleExtractor : EditorWindow {
    // --- Member Variables ---
    VisualTreeAsset m_uxmlFile;
    TextField m_outputFileNameField;
    TextField m_outputFolderPathField;
    TextField m_ussPreviewField;
    ScrollView m_previewScrollView;
    HelpBox m_statusHelpBox;
    Button m_extractButton;

    string m_generatedUssContent;

    [MenuItem( "Tools/UXML Style Extractor (Read-Only)" )]
    public static void ShowWindow() {
        var window = GetWindow<FirstUxmlStyleExtractor>( "UXML Style Extractor" );
        window.minSize = new Vector2( 480, 500 ); // Increased window size for new elements
    }

    public void CreateGUI() {
        var root = rootVisualElement;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;

        // --- Header Section ---
        var headerLabel = new Label( "UXML Style Extractor" ) {
            style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 5 },
        };
        root.Add( headerLabel );
        // Updated description to reflect read-only behavior
        root.Add( new Label( "Select a UXML file to parse its inline styles into a new USS file. The original UXML file will not be changed." ) );

        // --- Configuration Section ---
        var configContainer = new VisualElement { style = { marginTop = 15, marginBottom = 15 } };

        var uxmlField = new ObjectField( "Source UXML File" ) {
            objectType = typeof(VisualTreeAsset),
            allowSceneObjects = false,
        };
        uxmlField.RegisterValueChangedCallback( OnUxmlFileChanged );

        // --- Output Path Selection ---
        var pathContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5 } };
        m_outputFolderPathField = new TextField( "Output Folder" ) { isReadOnly = true, style = { flexGrow = 1 } };
        var browseButton = new Button( BrowseForSaveFolder ) { text = "Browse..." };
        pathContainer.Add( m_outputFolderPathField );
        pathContainer.Add( browseButton );

        m_outputFileNameField = new TextField( "Output USS File Name" ) {
            tooltip = "The name of the new stylesheet.",
        };

        configContainer.Add( uxmlField );
        configContainer.Add( pathContainer );
        configContainer.Add( m_outputFileNameField );
        root.Add( configContainer );

        // --- USS Preview Section ---
        root.Add( new Label( "USS Preview" ) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } } );
        m_ussPreviewField = new TextField {
            multiline = true,
            isReadOnly = true,
            value = "Select a UXML file to see a preview of the generated USS.",
        };
        m_previewScrollView = new ScrollView( ScrollViewMode.VerticalAndHorizontal ) {
            style = { height = 150, marginTop = 5 }, // Fixed height for the preview
        };
        m_previewScrollView.Add( m_ussPreviewField );
        root.Add( m_previewScrollView );

        // --- Footer Section ---
        m_statusHelpBox = new HelpBox( "", HelpBoxMessageType.None ) { style = { display = DisplayStyle.None, marginTop = 10 } };
        root.Add( m_statusHelpBox );
        root.Add( new VisualElement() { style = { flexGrow = 1 } } );
        m_extractButton = new Button( ExtractStyles ) { text = "Save USS File", style = { height = 30 } };
        root.Add( m_extractButton );
        root.style.paddingBottom = 10;

        // --- Initial State ---
        SetInitialState();
    }

    /// <summary>
    /// Sets the initial disabled/hidden state for UI elements.
    /// </summary>
    void SetInitialState() {
        m_outputFolderPathField.SetEnabled( false );
        m_outputFileNameField.SetEnabled( false );
        m_extractButton.SetEnabled( false );
        m_previewScrollView.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Opens a folder selection dialog and updates the output path.
    /// </summary>
    void BrowseForSaveFolder() {
        string startPath = string.IsNullOrEmpty( m_outputFolderPathField.value ) ? "Assets" : m_outputFolderPathField.value;
        string chosenPath = EditorUtility.SaveFolderPanel( "Choose a location for the USS file", startPath, "" );
        if ( !string.IsNullOrEmpty( chosenPath ) ) {
            // We need a relative path for Unity project context
            m_outputFolderPathField.value = "Assets" + chosenPath.Substring( Application.dataPath.Length );
        }
    }

    void OnUxmlFileChanged(ChangeEvent<Object> evt) {
        m_uxmlFile = evt.newValue as VisualTreeAsset;
        bool hasFile = m_uxmlFile;

        m_outputFolderPathField.SetEnabled( hasFile );
        m_outputFileNameField.SetEnabled( hasFile );
        m_extractButton.SetEnabled( hasFile );
        m_previewScrollView.style.display = hasFile ? DisplayStyle.Flex : DisplayStyle.None;

        if ( hasFile ) {
            string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
            m_outputFolderPathField.value = Path.GetDirectoryName( uxmlPath );
            m_outputFileNameField.value = $"{Path.GetFileNameWithoutExtension( uxmlPath )}_styles.uss";
            GenerateAndDisplayUssPreview();
            ShowStatus( "Ready to save the generated USS file.", HelpBoxMessageType.Info );
        }
        else {
            m_outputFolderPathField.value = "";
            m_outputFileNameField.value = "";
            m_ussPreviewField.value = "Select a UXML file to see a preview of the generated USS.";
            m_generatedUssContent = "";
            ShowStatus( "", HelpBoxMessageType.None );
        }
    }

    void GenerateAndDisplayUssPreview() {
        if ( !m_uxmlFile ) return;

        try {
            string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
            var doc = XDocument.Load( uxmlPath );
            List<XElement> elementsWithStyle = doc.Descendants().Where( e => e.Attribute( "style" ) != null ).ToList();

            if ( elementsWithStyle.Count == 0 ) {
                m_generatedUssContent = "";
                m_ussPreviewField.value = "/* No inline styles found in the selected file. #1#";
                return;
            }

            var ussContent = new StringBuilder();
            Dictionary<string, int> classCounters = new Dictionary<string, int>();

            foreach (var element in elementsWithStyle) {
                string localName = element.Name.LocalName.ToLower();
                classCounters.TryAdd( localName, 0 );
                classCounters[localName]++;
                var newClassName = $"{localName}-{classCounters[localName]}";

                string styleValue = element.Attribute( "style" ).Value;
                ussContent.AppendLine( $".{newClassName} {{" );
                string[] styles = styleValue.Split( ';' );
                foreach (string style in styles) {
                    if ( !string.IsNullOrWhiteSpace( style ) )
                        ussContent.AppendLine( $"    {style.Trim()};" );
                }

                ussContent.AppendLine( "}\n" );
            }

            m_generatedUssContent = ussContent.ToString();
            m_ussPreviewField.value = m_generatedUssContent;
        }
        catch (System.Exception ex) {
            m_generatedUssContent = "";
            m_ussPreviewField.value = $"/* Error generating preview: {ex.Message} #1#";
        }
    }

    void ShowStatus(string message, HelpBoxMessageType type) {
        m_statusHelpBox.text = message;
        m_statusHelpBox.messageType = type;
        m_statusHelpBox.style.display = string.IsNullOrEmpty( message ) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>
    /// This method now ONLY saves the generated USS file and does NOT touch the UXML file.
    /// </summary>
    void ExtractStyles() {
        if ( !m_uxmlFile ) {
            /* Should not happen due to button state #1#
            return;
        }

        string ussFileName = m_outputFileNameField.value;
        if ( string.IsNullOrWhiteSpace( ussFileName ) || !ussFileName.ToLower().EndsWith( ".uss" ) ) {
            ShowStatus( "Error: Output file name must end with .uss", HelpBoxMessageType.Error );
            return;
        }

        string fullUssPath = Path.Combine( m_outputFolderPathField.value, ussFileName );

        if ( File.Exists( fullUssPath ) ) {
            if ( !EditorUtility.DisplayDialog( "File Exists", $"The file '{ussFileName}' already exists at the chosen path. Overwrite it?", "Overwrite", "Cancel" ) ) {
                ShowStatus( "Save operation cancelled by user.", HelpBoxMessageType.Warning );
                return;
            }
        }

        // --- Core Logic: Write the pre-generated USS content to a file ---
        try {
            // Check if there is any content to save.
            if ( string.IsNullOrEmpty( m_generatedUssContent ) ) {
                ShowStatus( "No inline styles were found to save.", HelpBoxMessageType.Info );
                return;
            }

            // Write the generated content to the specified path.
            File.WriteAllText( fullUssPath, m_generatedUssContent );

            // Refresh the asset database to make the new file visible in Unity.
            AssetDatabase.Refresh();

            // Update status with a success message.
            ShowStatus( $"Success! USS file saved to:\n{fullUssPath}", HelpBoxMessageType.Info );
        }
        catch (System.Exception ex) {
            ShowStatus( $"An error occurred while saving the file: {ex.Message}", HelpBoxMessageType.Error );
            Debug.LogError( $"[UxmlStyleExtractor] Failed to save USS file: {ex}" );
        }
    }
}*/