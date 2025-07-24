/*using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Object = UnityEngine.Object;

namespace TCS.UiToolkitUtils.Editor {
    public class New_UxmlToCSharpConverter : EditorWindow {
        VisualTreeAsset m_uxmlFile;
        TextField m_outputClassNameField;
        TextField m_namespaceField;
        TextField m_outputFolderPathField;
        TextField m_csharpPreviewField;
        TextField m_ussPreviewField;
        HelpBox m_statusHelpBox;
        Button m_generateButton;
        Toggle m_extractUssToggle;
        Toggle m_allowDisplayNoneToggle;
        Toggle m_setTextFieldsToggle;
        Toggle m_isBindableToggle;

        string m_generatedCSharpContent;
        string m_generatedUssContent;

        EventCallback<ChangeEvent<string>> m_namespaceChangeCallback;
        EventCallback<ChangeEvent<string>> m_classNameChangeCallback;
        EventCallback<ChangeEvent<bool>> m_extractUssToggleCallback;
        EventCallback<ChangeEvent<bool>> m_setTextFieldsCallback;
        EventCallback<ChangeEvent<bool>> m_allowDisplayNoneCallback;
        EventCallback<ChangeEvent<bool>> m_isBindableCallback;

        record ElementInfo {
            public XElement XmlElement { get; init; }
            public string FieldName { get; set; }
            public string ElementType { get; init; }
            public ElementInfo Parent { get; set; }
            public List<ElementInfo> Children { get; } = new();
        }

        [MenuItem( "Tools/TCS/UXML to C# Class Converter" )]
        public static void ShowWindow() {
            var window = GetWindow<New_UxmlToCSharpConverter>( "UXML to C# Converter" );
            window.minSize = new Vector2( 520, 600 );
        }

        public void CreateGUI() {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            var headerLabel = new Label( "UXML to C# Class Converter" ) {
                style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 5 },
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

            var togglesContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5, flexWrap = Wrap.Wrap } };

            m_namespaceField = new TextField( "Namespace" ) { value = "" };
            m_namespaceChangeCallback = _ => GenerateAndDisplayPreview();
            m_namespaceField.RegisterCallback( m_namespaceChangeCallback );

            m_outputClassNameField = new TextField( "Output Class Name" );
            m_classNameChangeCallback = _ => GenerateAndDisplayPreview();
            m_outputClassNameField.RegisterCallback( m_classNameChangeCallback );

            m_extractUssToggle = new Toggle( "Extract Inline Styles to USS file" ) { value = true, style = { marginTop = 8, width = 250 } };
            m_extractUssToggleCallback = _ => GenerateAndDisplayPreview();
            m_extractUssToggle.RegisterValueChangedCallback( m_extractUssToggleCallback );

            m_setTextFieldsToggle = new Toggle( "Set Text Fields" ) { value = true, style = { marginTop = 8, width = 250 } };
            m_setTextFieldsCallback = _ => GenerateAndDisplayPreview();
            m_setTextFieldsToggle.RegisterValueChangedCallback( m_setTextFieldsCallback );

            m_allowDisplayNoneToggle = new Toggle( "Allow (display: none)" ) { value = false, style = { marginTop = 8, width = 250 } };
            m_allowDisplayNoneCallback = _ => GenerateAndDisplayPreview();
            m_allowDisplayNoneToggle.RegisterValueChangedCallback( m_allowDisplayNoneCallback );

            m_isBindableToggle = new Toggle( "Generate as BindableElement" ) { value = false, style = { marginTop = 8, width = 250 } };
            m_isBindableCallback = _ => GenerateAndDisplayPreview();
            m_isBindableToggle.RegisterValueChangedCallback( m_isBindableCallback );

            togglesContainer.Add( m_extractUssToggle );
            togglesContainer.Add( m_setTextFieldsToggle );
            togglesContainer.Add( m_allowDisplayNoneToggle );
            togglesContainer.Add( m_isBindableToggle );

            configContainer.Add( uxmlField );
            configContainer.Add( pathContainer );
            configContainer.Add( m_namespaceField );
            configContainer.Add( m_outputClassNameField );
            configContainer.Add( togglesContainer );
            root.Add( configContainer );

            var previewTabs = new TabView();
            var csharpTab = new Tab( "C# Preview" );
            var ussTab = new Tab( "USS Preview" );

            m_csharpPreviewField = new TextField { multiline = true, isReadOnly = true };
            csharpTab.Add( new ScrollView( ScrollViewMode.VerticalAndHorizontal ) { contentContainer = { style = { height = 250 } } }.AddFluent( m_csharpPreviewField ) );

            m_ussPreviewField = new TextField { multiline = true, isReadOnly = true };
            ussTab.Add( new ScrollView( ScrollViewMode.VerticalAndHorizontal ) { contentContainer = { style = { height = 250 } } }.AddFluent( m_ussPreviewField ) );

            previewTabs.Add( csharpTab );
            previewTabs.Add( ussTab );
            previewTabs.style.flexGrow = 1;
            root.Add( previewTabs );

            m_statusHelpBox = new HelpBox( "", HelpBoxMessageType.None ) { style = { display = DisplayStyle.None, marginTop = 10 } };
            root.Add( m_statusHelpBox );
            m_generateButton = new Button( GenerateFiles ) { text = "Generate Files", style = { height = 30, marginTop = 5 } };
            root.Add( m_generateButton );
            root.style.paddingBottom = 10;

            SetInitialState();
        }

        void OnDestroy() {
            if ( m_namespaceField != null && m_namespaceChangeCallback != null ) m_namespaceField.UnregisterCallback( m_namespaceChangeCallback );
            if ( m_outputClassNameField != null && m_classNameChangeCallback != null ) m_outputClassNameField.UnregisterCallback( m_classNameChangeCallback );
            if ( m_extractUssToggle != null && m_extractUssToggleCallback != null ) m_extractUssToggle.UnregisterValueChangedCallback( m_extractUssToggleCallback );
            if ( m_setTextFieldsToggle != null && m_setTextFieldsCallback != null ) m_setTextFieldsToggle.UnregisterValueChangedCallback( m_setTextFieldsCallback );
            if ( m_allowDisplayNoneToggle != null && m_allowDisplayNoneCallback != null ) m_allowDisplayNoneToggle.UnregisterValueChangedCallback( m_allowDisplayNoneCallback );
            if ( m_isBindableToggle != null && m_isBindableCallback != null ) m_isBindableToggle.UnregisterValueChangedCallback( m_isBindableCallback );
        }

        void SetInitialState() {
            m_outputFolderPathField.SetEnabled( false );
            m_outputClassNameField.SetEnabled( false );
            m_namespaceField.SetEnabled( false );
            m_generateButton.SetEnabled( false );
            m_extractUssToggle.SetEnabled( false );
            m_setTextFieldsToggle.SetEnabled( false );
            m_allowDisplayNoneToggle.SetEnabled( false );
            m_isBindableToggle.SetEnabled( false );
            m_csharpPreviewField.value = "Select a UXML file to see a preview of the generated C# class.";
            m_ussPreviewField.value = "Enable 'Extract Inline Styles' and select a UXML file for a preview.";
        }

        void BrowseForSaveFolder() {
            string startPath = string.IsNullOrEmpty( m_outputFolderPathField.value ) ? "Assets" : m_outputFolderPathField.value;
            string chosenPath = EditorUtility.SaveFolderPanel( "Choose a location for generated files", startPath, "" );
            if ( !string.IsNullOrEmpty( chosenPath ) ) {
                m_outputFolderPathField.value = "Assets" + chosenPath.Substring( Application.dataPath.Length );
            }
        }

        void OnUxmlFileChanged(ChangeEvent<Object> evt) {
            m_uxmlFile = evt.newValue as VisualTreeAsset;
            bool hasFile = m_uxmlFile != null;

            m_outputFolderPathField.SetEnabled( hasFile );
            m_outputClassNameField.SetEnabled( hasFile );
            m_namespaceField.SetEnabled( hasFile );
            m_generateButton.SetEnabled( hasFile );
            m_extractUssToggle.SetEnabled( hasFile );
            m_setTextFieldsToggle.SetEnabled( hasFile );
            m_allowDisplayNoneToggle.SetEnabled( hasFile );
            m_isBindableToggle.SetEnabled( hasFile );

            if ( hasFile ) {
                string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
                m_outputFolderPathField.value = Path.GetDirectoryName( uxmlPath );
                string baseName = Path.GetFileNameWithoutExtension( uxmlPath ).Replace( " ", "" );
                m_outputClassNameField.value = $"{baseName}Element";
                GenerateAndDisplayPreview();
                ShowStatus( "Ready to generate files.", HelpBoxMessageType.Info );
            }
            else {
                SetInitialState();
                ShowStatus( "", HelpBoxMessageType.None );
            }
        }

        void GenerateAndDisplayPreview() {
            if ( !m_uxmlFile ) return;

            try {
                string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
                var doc = XDocument.Load( uxmlPath );
                XNamespace ui = "UnityEngine.UIElements";

                List<XElement> instantiableElements = doc.Descendants()
                    .Where( d => d.Name.Namespace == ui
                                 && d.Name.LocalName != "UXML"
                                 && d.Name.LocalName != "Style"
                                 && (m_allowDisplayNoneToggle.value ||
                                     d.AncestorsAndSelf().All( a => a.Attribute( "style" )?.Value.Contains( "display: none" ) != true ))
                    )
                    .ToList();

                if ( !instantiableElements.Any() ) {
                    ShowStatus( "No valid VisualElements found in UXML. Check 'Allow Display: None' if elements are hidden via inline styles.", HelpBoxMessageType.Warning );
                    m_csharpPreviewField.value = "/* No instantiable elements found. #1#";
                    m_ussPreviewField.value = "/* No instantiable elements found. #1#";
                    m_generatedCSharpContent = "";
                    m_generatedUssContent = "";
                    return;
                }

                Dictionary<XElement, ElementInfo> elementMap = new();
                Dictionary<string, int> nameCounters = new();

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

                m_generatedCSharpContent = GenerateCSharpContent( elementMap.Values );
                m_csharpPreviewField.value = m_generatedCSharpContent;

                if ( m_extractUssToggle.value ) {
                    m_generatedUssContent = GenerateUssContent( elementMap.Values );
                    m_ussPreviewField.value = string.IsNullOrEmpty( m_generatedUssContent )
                        ? "/* No elements found to generate USS for (or no inline styles to extract). #1#"
                        : m_generatedUssContent;
                }
                else {
                    m_generatedUssContent = "";
                    m_ussPreviewField.value = "Enable 'Extract Inline Styles' to see a preview.";
                }

                ShowStatus( "Preview generated.", HelpBoxMessageType.Info );
            }
            catch (Exception ex) {
                m_generatedCSharpContent = m_generatedUssContent = "";
                m_csharpPreviewField.value = m_ussPreviewField.value = $"/* Error generating preview: {ex.Message} #1#";
                ShowStatus( "Error during preview generation. Check console.", HelpBoxMessageType.Error );
                Debug.LogError( ex );
            }
        }

        string GenerateCSharpContent(IEnumerable<ElementInfo> elements) {
            var sb = new StringBuilder();
            string className = m_outputClassNameField.value;
            string namespaceName = m_namespaceField.value;
            bool hasNamespace = !string.IsNullOrWhiteSpace( namespaceName );
            string indent = hasNamespace ? "    " : "";
            bool isBindable = m_isBindableToggle.value;

            sb.AppendLine( "using UnityEngine.UIElements;" );
            sb.AppendLine( "using TCS.UiToolkitUtils.Attributes;" );
            if ( isBindable ) {
                sb.AppendLine( "using Unity.Properties;" );
            }

            if ( hasNamespace ) sb.AppendLine( $"\nnamespace {namespaceName}{{" );

            string baseClass = isBindable ? "BindableElement" : "VisualElement";
            sb.AppendLine( $"{indent}[UxmlElement] public partial class {className} : {baseClass}{{" );

            sb.AppendLine( $"{indent}    #region Fields" );

            IEnumerable<ElementInfo> elementInfos = elements.ToList();
            foreach (var info in elementInfos) {
                string nameInQuotes = info.XmlElement.Attribute( "name" )?.Value ?? info.FieldName.Substring( 2 );
                sb.AppendLine( $"{indent}    [USSName] readonly {info.ElementType} {info.FieldName} = new() {{ name = \"{nameInQuotes}\" }};" );
            }

            sb.AppendLine( $"{indent}    #endregion\n" );
            sb.AppendLine( $"{indent}    #region Constructor" );
            sb.AppendLine( $"{indent}    public {className}(){{" );
            sb.AppendLine( $"{indent}        SetElementClassNames();" );

            if ( m_setTextFieldsToggle.value ) {
                sb.AppendLine( $"\n{indent}        // --- Set Text Fields ---" );
                SetTextFields( elementInfos, sb, indent );
            }

            sb.AppendLine( $"\n{indent}        // --- Build Hierarchy ---" );
            foreach (var info in elementInfos) {
                sb.AppendLine( $"{indent}        {(info.Parent == null ? "hierarchy" : info.Parent.FieldName)}.Add({info.FieldName});" );
            }

            sb.AppendLine( $"{indent}    }}" );
            sb.AppendLine( $"{indent}    #endregion" );
            sb.AppendLine( $"{indent}}}" );
            if ( hasNamespace ) sb.AppendLine( "}" );

            return sb.ToString();
        }

        #region Subject To Change
        static string Escape(string s) =>
            s.Replace( "\\", "\\\\" )
                .Replace( "\"", "\\\"" )
                .Replace( "\n", "\\n" )
                .Replace( "\r", "\\r" );

        static readonly HashSet<string> TextProps = new() { "Label", "Button", "Foldout", "TextElement", "Toggle", "RadioButton" };
        static readonly HashSet<string> LabelProps = new() {
            "TextField", "Slider", "SliderInt", "MinMaxSlider", "DropdownField", "EnumField",
            "RadioButtonGroup", "IntegerField", "FloatField", "LongField", "DoubleField", "Hash128Field",
            "Vector2Field", "Vector3Field", "Vector4Field", "RectField", "BoundsField",
            "UnsignedIntegerField", "UnsignedLongField", "Vector2IntField", "Vector3IntField",
            "RectIntField", "BoundsIntField",
        };

        static void SetTextFields(IEnumerable<ElementInfo> elementInfos, StringBuilder sb, string indent) {
            foreach (var info in elementInfos) {
                string raw = info.XmlElement.Attribute( "text" )?.Value
                             ?? info.XmlElement.Attribute( "label" )?.Value
                             ?? (info.ElementType == "ProgressBar"
                                 ? info.XmlElement.Attribute( "title" )?.Value
                                 : null);

                if ( string.IsNullOrWhiteSpace( raw ) ) continue;

                string escaped = Escape( raw );

                if ( TextProps.Contains( info.ElementType )
                     || info.ElementType == "TextField" && info.XmlElement.Attribute( "text" ) != null ) {
                    sb.AppendLine( $"{indent}        {info.FieldName}.text = \"{escaped}\";" );
                }
                else if ( LabelProps.Contains( info.ElementType ) ) {
                    sb.AppendLine( $"{indent}        {info.FieldName}.label = \"{escaped}\";" );
                }
                else if ( info.ElementType == "ProgressBar" ) {
                    sb.AppendLine( $"{indent}        {info.FieldName}.title = \"{escaped}\";" );
                }
            }
        }
        #endregion

        string GenerateUssContent(IEnumerable<ElementInfo> elements) {
            var sb = new StringBuilder();
            string baseClassName = ToKebabCase( m_outputClassNameField.value );

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

        static string GenerateFieldName(ElementInfo info, Dictionary<string, int> counters) {
            string baseName = info.XmlElement.Attribute( "name" )?.Value ?? info.ElementType;
            string[] words = Regex.Split( baseName, @"[^a-zA-Z0-9]+|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])" )
                .Where( s => !string.IsNullOrWhiteSpace( s ) )
                .ToArray();

            if ( words.Length == 0 ) words = new[] { "element" };

            var pascalCasedNameBuilder = new StringBuilder();
            foreach (string word in words) {
                if ( string.IsNullOrEmpty( word ) ) continue;
                pascalCasedNameBuilder.Append( char.ToUpperInvariant( word[0] ) );
                pascalCasedNameBuilder.Append( word.Substring( 1 ) );
            }

            baseName = pascalCasedNameBuilder.ToString();

            if ( !counters.TryAdd( baseName, 1 ) ) counters[baseName]++;
            if ( counters[baseName] > 1 ) baseName += counters[baseName];

            string finalFieldNamePart;
            if ( baseName.Length > 0 && baseName.All( c => !char.IsLetter( c ) || char.IsUpper( c ) ) ) {
                finalFieldNamePart = baseName.ToLowerInvariant();
            }
            else if ( baseName.Length > 0 ) {
                finalFieldNamePart = char.ToLowerInvariant( baseName[0] ) + baseName.Substring( 1 );
            }
            else {
                finalFieldNamePart = baseName;
            }

            return $"m_{finalFieldNamePart}";
        }

        string ToKebabCase(string str) {
            if ( string.IsNullOrEmpty( str ) ) return str;
            return Regex.Replace( str, "(?<!^)([A-Z])", "-$1" ).ToLower();
        }

        void ShowStatus(string message, HelpBoxMessageType type) {
            m_statusHelpBox.text = message;
            m_statusHelpBox.messageType = type;
            m_statusHelpBox.style.display = string.IsNullOrEmpty( message ) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void GenerateFiles() {
            string className = m_outputClassNameField.value;
            if ( string.IsNullOrWhiteSpace( className ) ) {
                ShowStatus( "Error: Class Name cannot be empty.", HelpBoxMessageType.Error );
                return;
            }

            string csPath = Path.Combine( m_outputFolderPathField.value, $"{className}.cs" );
            if ( File.Exists( csPath ) ) {
                if ( !EditorUtility.DisplayDialog( "File Exists", $"The file '{className}.cs' already exists. Overwrite it?", "Overwrite", "Cancel" ) ) {
                    ShowStatus( "Generation cancelled by user.", HelpBoxMessageType.Warning );
                    return;
                }
            }

            File.WriteAllText( csPath, m_generatedCSharpContent );

            if ( m_extractUssToggle.value && !string.IsNullOrEmpty( m_generatedUssContent ) ) {
                var ussFileName = $"{className}.uss";
                string ussPath = Path.Combine( m_outputFolderPathField.value, ussFileName );
                if ( File.Exists( ussPath ) ) {
                    if ( !EditorUtility.DisplayDialog( "File Exists", $"The file '{ussFileName}' already exists. Overwrite it?", "Overwrite", "Cancel" ) ) {
                        ShowStatus( "Generation cancelled by user (USS file).", HelpBoxMessageType.Warning );
                        AssetDatabase.Refresh();
                        return;
                    }
                }

                File.WriteAllText( ussPath, m_generatedUssContent );
            }

            AssetDatabase.Refresh();
            ShowStatus( $"Success! Files generated in:\n{m_outputFolderPathField.value}", HelpBoxMessageType.Info );
        }
    }

    public static class VisualElementExtensions {
        public static T AddFluent<T>(this T parent, VisualElement child) where T : VisualElement {
            parent.Add( child );
            return parent;
        }
    }
}*/