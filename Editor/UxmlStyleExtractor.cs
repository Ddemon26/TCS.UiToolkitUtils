using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TCS.UiToolkitUtils.Editor {
    public class UxmlStyleExtractor : EditorWindow {
        VisualTreeAsset m_uxmlFile;
        TextField m_outputFileNameField;
        TextField m_outputFolderPathField;
        TextField m_ussPreviewField;
        ScrollView m_previewScrollView;
        HelpBox m_statusHelpBox;
        Button m_extractButton;

        string m_generatedUssContent;

        class ElementInfo {
            public XElement XmlElement { get; set; }
            public string FieldName { get; set; }
            public string ElementType { get; set; }
        }

        [MenuItem( "Window/UI Toolkit/UXML Style Extractor" )]
        public static void ShowWindow() {
            var window = GetWindow<UxmlStyleExtractor>( "UXML Style Extractor" );
            window.minSize = new Vector2( 480, 500 );
        }

        public void CreateGUI() {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            var headerLabel = new Label( "UXML Style Extractor" ) {
                style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 5 },
            };
            root.Add( headerLabel );
            root.Add( new Label( "Select a UXML file to parse its inline styles into a new USS file. The original UXML file will not be changed." ) );

            var configContainer = new VisualElement { style = { marginTop = 15, marginBottom = 15 } };

            var uxmlField = new ObjectField( "Source UXML File" ) {
                objectType = typeof(VisualTreeAsset),
                allowSceneObjects = false,
            };
            uxmlField.RegisterValueChangedCallback( OnUxmlFileChanged );

            var pathContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5 } };
            m_outputFolderPathField = new TextField( "Output Folder" ) { isReadOnly = true, style = { flexGrow = 1 } };
            var browseButton = new Button( BrowseForSaveFolder ) { text = "Browse..." };
            pathContainer.Add( m_outputFolderPathField );
            pathContainer.Add( browseButton );

            m_outputFileNameField = new TextField( "Output USS File Name" ) {
                tooltip = "The name of the new stylesheet. This will be used as the BEM 'block' name.",
            };
            m_outputFileNameField.RegisterValueChangedCallback( _ => GenerateAndDisplayUssPreview() );

            configContainer.Add( uxmlField );
            configContainer.Add( pathContainer );
            configContainer.Add( m_outputFileNameField );
            root.Add( configContainer );

            root.Add( new Label( "USS Preview" ) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } } );
            m_ussPreviewField = new TextField {
                multiline = true,
                isReadOnly = true,
                value = "Select a UXML file to see a preview of the generated USS.",
            };
            m_previewScrollView = new ScrollView( ScrollViewMode.VerticalAndHorizontal ) {
                style = { flexGrow = 1, marginTop = 5 },
            };
            m_previewScrollView.Add( m_ussPreviewField );
            root.Add( m_previewScrollView );

            m_statusHelpBox = new HelpBox( "", HelpBoxMessageType.None ) { style = { display = DisplayStyle.None, marginTop = 10 } };
            root.Add( m_statusHelpBox );
            m_extractButton = new Button( ExtractStyles ) { text = "Save USS File", style = { height = 30, marginTop = 5 } };
            root.Add( m_extractButton );
            root.style.paddingBottom = 10;

            SetInitialState();
        }

        void SetInitialState() {
            m_outputFolderPathField.SetEnabled( false );
            m_outputFileNameField.SetEnabled( false );
            m_extractButton.SetEnabled( false );
            m_previewScrollView.style.display = DisplayStyle.None;
            ShowStatus( "", HelpBoxMessageType.None );
        }

        void BrowseForSaveFolder() {
            string startPath = string.IsNullOrEmpty( m_outputFolderPathField.value ) ? "Assets" : m_outputFolderPathField.value;
            string chosenPath = EditorUtility.SaveFolderPanel( "Choose a location for the USS file", startPath, "" );
            if ( !string.IsNullOrEmpty( chosenPath ) ) {
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
                // Default USS name is now cleaner, e.g., MyComponent.uss
                m_outputFileNameField.value = $"{Path.GetFileNameWithoutExtension( uxmlPath )}.uss";
                GenerateAndDisplayUssPreview();
                ShowStatus( "Ready to save the generated USS file.", HelpBoxMessageType.Info );
            }
            else {
                m_outputFolderPathField.value = "";
                m_outputFileNameField.value = "";
                m_ussPreviewField.value = "Select a UXML file to see a preview of the generated USS.";
                m_generatedUssContent = "";
                SetInitialState();
            }
        }

        void GenerateAndDisplayUssPreview() {
            if ( !m_uxmlFile || string.IsNullOrWhiteSpace( m_outputFileNameField.value ) ) {
                m_ussPreviewField.value = "Select a UXML file and provide an output name.";
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
                    m_generatedUssContent = "";
                    m_ussPreviewField.value = "/* No visual elements found in the selected file. */";
                    ShowStatus( "No visual elements found to process.", HelpBoxMessageType.Warning );
                    return;
                }

                List<ElementInfo> elementInfos = new();
                Dictionary<string, int> nameCounters = new();
                foreach (var element in instantiableElements) {
                    var info = new ElementInfo { XmlElement = element, ElementType = element.Name.LocalName };
                    info.FieldName = GenerateFieldName( info, nameCounters );
                    elementInfos.Add( info );
                }

                m_generatedUssContent = GenerateUssContent( elementInfos );
                m_ussPreviewField.value = m_generatedUssContent;
                ShowStatus( "Preview generated. Ready to save.", HelpBoxMessageType.Info );
            }
            catch (Exception ex) {
                m_generatedUssContent = "";
                m_ussPreviewField.value = $"/* Error generating preview: {ex.Message} */";
                ShowStatus( "Error during preview generation. See console for details.", HelpBoxMessageType.Error );
                Debug.LogError( ex );
            }
        }

        string GenerateUssContent(IEnumerable<ElementInfo> elements) {
            var sb = new StringBuilder();
            string baseClassName = ToKebabCase( Path.GetFileNameWithoutExtension( m_outputFileNameField.value ) );

            foreach (var info in elements) {
                string rawFieldName = info.FieldName.Substring( 2 );
                string elementName = ToKebabCase( rawFieldName );

                sb.AppendLine( $".{baseClassName}_{elementName} {{" );

                var styleAttr = info.XmlElement.Attribute( "style" );
                if ( styleAttr != null && !string.IsNullOrWhiteSpace( styleAttr.Value ) ) {
                    string[] styles = styleAttr.Value.Split( new[] { ';' }, StringSplitOptions.RemoveEmptyEntries );
                    foreach (string style in styles) {
                        if ( !string.IsNullOrWhiteSpace( style ) ) {
                            sb.AppendLine( $"    {style.Trim()};" );
                        }
                    }
                }

                sb.AppendLine( "}\n" );
            }

            return sb.ToString();
        }

        // private string GenerateFieldName(ElementInfo info, Dictionary<string, int> counters) {
        //     string baseName = info.XmlElement.Attribute( "name" )?.Value ?? info.ElementType;
        //     baseName = Regex.Replace( baseName, @"[^\w]", " " );
        //     baseName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase( baseName ).Replace( " ", "" );
        //     if ( !counters.TryAdd( baseName, 1 ) ) counters[baseName]++;
        //     if ( counters[baseName] > 1 ) baseName += counters[baseName];
        //     return $"m_{char.ToLowerInvariant( baseName[0] )}{baseName.Substring( 1 )}";
        // }

        string GenerateFieldName(ElementInfo info, Dictionary<string, int> counters) {
            string baseName = info.XmlElement.Attribute("name")?.Value ?? info.ElementType;

            string[] words = Regex.Split(baseName, @"[^a-zA-Z0-9]+|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (words.Length == 0) {
                words = new[] { "element" }; 
            }

            var pascalCasedNameBuilder = new StringBuilder();
            foreach (string word in words) {
                if (string.IsNullOrEmpty(word)) continue;
                pascalCasedNameBuilder.Append(char.ToUpperInvariant(word[0]));
                pascalCasedNameBuilder.Append(word.Substring(1));
            }
            baseName = pascalCasedNameBuilder.ToString();

            if (!counters.TryAdd(baseName, 1)) {
                counters[baseName]++;
            }
            if (counters[baseName] > 1) {
                baseName += counters[baseName];
            }

            string finalFieldNamePart = baseName.Length switch {
                > 0 when baseName.All( c => !char.IsLetter( c ) || char.IsUpper( c ) ) => baseName.ToLowerInvariant(),
                > 0 => char.ToLowerInvariant( baseName[0] ) + baseName.Substring( 1 ),
                _ => baseName,
            };

            return $"m_{finalFieldNamePart}";
        }

        static string ToKebabCase(string str) {
            if ( string.IsNullOrEmpty( str ) ) return str;
            // Converts PascalCase or camelCase to kebab-case.
            return Regex.Replace( str, "(?<!^)([A-Z])", "-$1" ).ToLower();
        }

        void ShowStatus(string message, HelpBoxMessageType type) {
            m_statusHelpBox.text = message;
            m_statusHelpBox.messageType = type;
            m_statusHelpBox.style.display = string.IsNullOrEmpty( message ) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void ExtractStyles() {
            if ( !m_uxmlFile ) return;

            string ussFileName = m_outputFileNameField.value;
            if ( string.IsNullOrWhiteSpace( ussFileName ) || !ussFileName.ToLower().EndsWith( ".uss" ) ) {
                ShowStatus( "Error: Output file name must be valid and end with .uss", HelpBoxMessageType.Error );
                return;
            }

            string fullUssPath = Path.Combine( m_outputFolderPathField.value, ussFileName );

            if ( File.Exists( fullUssPath ) ) {
                if ( !EditorUtility.DisplayDialog( "File Exists", $"The file '{ussFileName}' already exists. Overwrite it?", "Overwrite", "Cancel" ) ) {
                    ShowStatus( "Save operation cancelled by user.", HelpBoxMessageType.Warning );
                    return;
                }
            }

            try {
                if ( string.IsNullOrEmpty( m_generatedUssContent ) ) {
                    ShowStatus( "There is no content to save. Generate a preview first.", HelpBoxMessageType.Warning );
                    return;
                }

                File.WriteAllText( fullUssPath, m_generatedUssContent );
                AssetDatabase.Refresh();
                ShowStatus( $"Success! USS file saved to:\n{fullUssPath}", HelpBoxMessageType.Info );
            }
            catch (Exception ex) {
                ShowStatus( $"An error occurred while saving the file: {ex.Message}", HelpBoxMessageType.Error );
                Debug.LogError( $"[UxmlStyleExtractor] Failed to save USS file: {ex}" );
            }
        }
    }
}