using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Object = UnityEngine.Object;

namespace TCS.UiToolkitUtils.Editor {
    //TODO: Clean up front end UX.
    //TODO: Add support for setting up RegisteredPropertyChangedCallbacks for the properties
    //TODO: Fix Property Naming on front end, it should handle edge cases like spaces, special characters, etc.
    //TODO: Add Refresh when a value changes in the properties
    public class UxmlToCSharpConverter : EditorWindow {
        #region Fields
        // --- Enums ---
        enum PropertyDataType { Float, Int, Bool, String, Color, Vector2, Vector3 }

        // --- UI Fields ---
        VisualTreeAsset m_uxmlFile;
        TextField m_outputClassNameField, m_namespaceField, m_outputFolderPathField;
        TextField m_csharpPreviewField, m_ussPreviewField, m_bindDtoPreviewField;
        HelpBox m_statusHelpBox;
        Button m_generateButton;
        Toggle m_extractUssToggle, m_allowDisplayNoneToggle, m_setTextFieldsToggle, m_isBindableToggle, m_createBindDtoToggle;

        // --- Bindable Properties UI ---
        Foldout m_propertiesFoldout;
        Button m_notifyTypeButton;
        Label m_notifyTypeLabel;
        ScrollView m_propertiesScrollView;
        readonly List<PropertyDefinition> m_propertyDefinitions = new();

        PopupField<string> m_notifyValueTargetElementField;
        PopupField<string> m_notifyValueTargetAttributeField;
        EnumField m_propertyTypeField;
        VisualElement m_notifyValueTargetFieldsContainer;

        // --- Data ---
        string m_generatedCSharpContent, m_generatedUssContent, m_generatedBindDtoContent;
        string m_notifyValueType = "float";
        readonly List<string> m_elementFieldNames = new();
        readonly Dictionary<string, string> m_elementFieldTypes = new();

        // EventCallback<ChangeEvent<string>> m_namespaceChangeCallback;
        // EventCallback<ChangeEvent<string>> m_classNameChangeCallback;
        // EventCallback<ChangeEvent<bool>> m_extractUssToggleCallback;
        // EventCallback<ChangeEvent<bool>> m_setTextFieldsCallback;
        // EventCallback<ChangeEvent<bool>> m_allowDisplayNoneCallback;
        // EventCallback<ChangeEvent<bool>> m_isBindableCallback;
        // EventCallback<ChangeEvent<bool>> m_createBindDtoCallback;
        //
        // EventCallback<ChangeEvent<string>> m_notifyValueTargetElementChangeCallback;
        // EventCallback<ChangeEvent<string>> m_notifyValueTargetAttributeChangeCallback;
        // EventCallback<ChangeEvent<Enum>> m_propertyTypeChangeCallback;
        #endregion

        #region Data Structures
        // --- Data Structures ---
        class PropertyDefinition {
            public VisualElement RowElement;
            public TextField NameField;
            public EnumField TypeField;
            public BindingMode BindingMode;

            public PopupField<string> SourceElementField;
            public PopupField<string> TargetAttributeField;

            public VisualElement ValueFieldContainer;
            public IValueField ValueField;
            public VisualElement CurrentValueElement;
            public EnumField BindingModeField;
            public object StoredValueChangedCallback { get; set; }
            public PropertyDataType? StoredCallbackType { get; set; }
        }

        // --- Interfaces for type-safe value retrieval ---
        interface IValueField {
            object GetValue();
        }

        interface IValueField<out T> : IValueField {
            new T GetValue();
        }

        record ElementInfo {
            public XElement XmlElement { get; init; }
            public string FieldName { get; set; }
            public string ElementType { get; init; }
            public ElementInfo Parent { get; set; }
            public List<ElementInfo> Children { get; } = new();
        }
        #endregion

        #region Window Management
        [MenuItem( "Tools/TCS/UXML to C# Class Converter" )]
        public static void ShowWindow() {
            var window = GetWindow<UxmlToCSharpConverter>( "UXML to C# Converter" );
            window.titleContent = new GUIContent( "UXML to C# Converter" );
            window.minSize = new Vector2( 800, 400 );
        }
        
        void OnNamespaceFieldValueChanged(ChangeEvent<string> evt) => GenerateAndDisplayPreview();
        void OnClassNameFieldValueChanged(ChangeEvent<string> evt) => GenerateAndDisplayPreview();
        void OnExtractUssToggleValueChanged(ChangeEvent<bool> evt) => GenerateAndDisplayPreview();
        void OnSetTextFieldsToggleValueChanged(ChangeEvent<bool> evt) => GenerateAndDisplayPreview();
        void OnAllowDisplayNoneToggleValueChanged(ChangeEvent<bool> evt) => GenerateAndDisplayPreview();

        void OnIsBindableToggleValueChanged(ChangeEvent<bool> evt) {
            m_propertiesFoldout.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            GenerateAndDisplayPreview();
        }

        void OnCreateBindDtoToggleValueChanged(ChangeEvent<bool> evt) => GenerateAndDisplayPreview();

        void OnNotifyValueTargetElementFieldValueChanged(ChangeEvent<string> evt) {
            UpdateNotifyValueTargetControlsLogic();
            GenerateAndDisplayPreview();
        }

        void OnNotifyValueTargetAttributeFieldValueChanged(ChangeEvent<string> evt) => GenerateAndDisplayPreview();

        void OnPropertyTypeFieldValueChanged(ChangeEvent<Enum> evt) {
            if (m_notifyValueTargetAttributeField.style.display == DisplayStyle.Flex) {
                GenerateAndDisplayPreview();
            }
        }

        void RegisterCallbacks() {
            m_namespaceField.RegisterValueChangedCallback(OnNamespaceFieldValueChanged);
            m_outputClassNameField.RegisterValueChangedCallback(OnClassNameFieldValueChanged);
            m_extractUssToggle.RegisterValueChangedCallback(OnExtractUssToggleValueChanged);
            m_setTextFieldsToggle.RegisterValueChangedCallback(OnSetTextFieldsToggleValueChanged);
            m_allowDisplayNoneToggle.RegisterValueChangedCallback(OnAllowDisplayNoneToggleValueChanged);
            m_isBindableToggle.RegisterValueChangedCallback(OnIsBindableToggleValueChanged);
            m_createBindDtoToggle.RegisterValueChangedCallback(OnCreateBindDtoToggleValueChanged);

            m_notifyValueTargetElementField.RegisterValueChangedCallback(OnNotifyValueTargetElementFieldValueChanged);
            m_notifyValueTargetAttributeField.RegisterValueChangedCallback(OnNotifyValueTargetAttributeFieldValueChanged);
            m_propertyTypeField.RegisterValueChangedCallback(OnPropertyTypeFieldValueChanged);
        }

        void OnEnable() {
            m_uxmlFile = null;
            InitializeAttributeMap();
        }

        void OnDisable() {
            m_namespaceField?.UnregisterValueChangedCallback(OnNamespaceFieldValueChanged);
            m_outputClassNameField?.UnregisterValueChangedCallback(OnClassNameFieldValueChanged);
            m_extractUssToggle?.UnregisterValueChangedCallback(OnExtractUssToggleValueChanged);
            m_setTextFieldsToggle?.UnregisterValueChangedCallback(OnSetTextFieldsToggleValueChanged);
            m_allowDisplayNoneToggle?.UnregisterValueChangedCallback(OnAllowDisplayNoneToggleValueChanged);
            m_isBindableToggle?.UnregisterValueChangedCallback(OnIsBindableToggleValueChanged);
            m_createBindDtoToggle?.UnregisterValueChangedCallback(OnCreateBindDtoToggleValueChanged);

            m_notifyValueTargetElementField?.UnregisterValueChangedCallback(OnNotifyValueTargetElementFieldValueChanged);
            m_notifyValueTargetAttributeField?.UnregisterValueChangedCallback(OnNotifyValueTargetAttributeFieldValueChanged);
            m_propertyTypeField?.UnregisterValueChangedCallback(OnPropertyTypeFieldValueChanged);

            foreach (var definition in m_propertyDefinitions) {
                if (definition.CurrentValueElement != null && definition.StoredValueChangedCallback != null && definition.StoredCallbackType.HasValue) {
                    UnregisterCallback(definition.CurrentValueElement, definition.StoredCallbackType.Value, definition.StoredValueChangedCallback);
                    definition.StoredValueChangedCallback = null;
                    definition.StoredCallbackType = null;
                }
            }
            m_propertyDefinitions.Clear();
        }

        public void CreateGUI() {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            var styleSheetPallet = Resources.Load<StyleSheet>( "StyleSheets/StylePallet_MysticSage" );
            if ( styleSheetPallet != null ) {
                root.styleSheets.Add( styleSheetPallet );
            }

            var leftPane = new VisualElement {
                style = {
                    width = new Length( 50, LengthUnit.Percent ),
                    minWidth = 380,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 10,
                    paddingBottom = 10,
                },
            };
            var rightPane = new VisualElement { style = { flexGrow = 1, borderLeftWidth = 1, borderLeftColor = new Color( 0.18f, 0.18f, 0.18f ) } };

            root.Add( leftPane );
            root.Add( rightPane );

            // --- Left Pane Content ---
            var settingsScrollView = new ScrollView( ScrollViewMode.Vertical ) { style = { flexGrow = 1 } };
            leftPane.Add( settingsScrollView );

            CreateHeader( settingsScrollView );
            CreateConfigurationSection( settingsScrollView );
            CreateTogglesSection( settingsScrollView );
            CreatePropertiesSection( settingsScrollView );
            CreateFooter( leftPane );

            // --- Right Pane Content ---
            CreatePreviewTabs( rightPane );

            RegisterCallbacks();
            SetInitialState();
        }
        #endregion

        #region GUI Creation
        static void CreateHeader(VisualElement parent) {
            parent.Add(
                new Label(
                    "UXML to C# Converter"
                ) {
                    style = {
                        fontSize = 16,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        marginBottom = 5,
                    },
                }
            );
            parent.Add(
                new Label(
                    "Select a UXML file to generate a C# class."
                    // "This tool converts UXML files to C# classes, allowing you to generate UI elements with properties and styles directly from UXML.\n\n" +
                    // "1. Select a UXML file.\n" +
                    // "2. Specify the output folder and class name.\n" +
                    // "3. Configure generation options.\n" +
                    // "4. Add bindable properties if needed.\n" +
                    // "5. Click 'Generate Files' to create the C# class and USS styles.\n\n" +
                    // "The generated class will include properties for each element in the UXML, allowing you to easily manipulate the UI in your scripts.\n" +
                    // "The USS styles will be extracted from inline styles in the UXML, and a Bindable DTO can be created for data binding.\n\n" +
                    // "Note: Ensure the UXML file is well-formed and contains valid UI elements. The tool supports basic UXML structures and inline styles."
                ) {
                    style = {
                        whiteSpace = WhiteSpace.Normal,
                        marginBottom = 10,
                    },
                }
            );
        }

        void CreateConfigurationSection(VisualElement parent) {
            var foldout = new Foldout { text = "File Settings", value = true, style = { marginTop = 15 } };
            var uxmlField = new ObjectField( "Source UXML File" ) { objectType = typeof(VisualTreeAsset), allowSceneObjects = false };
            uxmlField.RegisterValueChangedCallback( OnUxmlFileChanged );

            var pathContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5 } };
            m_outputFolderPathField = new TextField( "Output Folder" ) { isReadOnly = true, style = { flexGrow = 1 } };
            var browseButton = new Button( BrowseForSaveFolder ) { text = "Browse..." };
            pathContainer.Add( m_outputFolderPathField );
            pathContainer.Add( browseButton );

            m_namespaceField = new TextField( "Namespace" ) { value = "" };
            m_outputClassNameField = new TextField( "Output Class Name" );

            foldout.Add( uxmlField );
            foldout.Add( pathContainer );
            foldout.Add( m_namespaceField );
            foldout.Add( m_outputClassNameField );
            parent.Add( foldout );
        }

        void CreateTogglesSection(VisualElement parent) {
            var foldout = new Foldout { text = "Generation Options", value = true, style = { marginTop = 5 } };
            m_extractUssToggle = new Toggle( "Extract Inline Styles" ) { value = true };
            m_setTextFieldsToggle = new Toggle( "Set Text Fields" ) { value = true };
            m_allowDisplayNoneToggle = new Toggle( "Allow (display: none)" ) { value = false };
            m_isBindableToggle = new Toggle( "Generate as BindableElement" ) { value = false };
            m_createBindDtoToggle = new Toggle( "Create Bindable DTO" ) { value = false };
            foldout.Add( m_extractUssToggle );
            foldout.Add( m_setTextFieldsToggle );
            foldout.Add( m_allowDisplayNoneToggle );
            foldout.Add( m_isBindableToggle );
            foldout.Add( m_createBindDtoToggle );
            parent.Add( foldout );
        }

        void CreatePropertiesSection(VisualElement parent) {
            m_propertiesFoldout = new Foldout { text = "Bindable Properties", value = true, style = { display = DisplayStyle.None, marginTop = 5 } };
            var notifyTypeContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 5 } };
            notifyTypeContainer.Add( new Label( "INotifyValueChanged Type (T)" ) { style = { marginRight = 10, minWidth = 180 } } );
            m_notifyTypeLabel = new Label( m_notifyValueType ) { style = { unityFontStyleAndWeight = FontStyle.Bold, minWidth = 60, unityTextAlign = TextAnchor.MiddleLeft } };
            m_notifyTypeButton = new Button( ShowNotifyTypeMenu ) { text = "Select..." };

            //popup field to select the element bound to value
            notifyTypeContainer.Add( m_notifyTypeLabel );
            notifyTypeContainer.Add( m_notifyTypeButton );
            m_propertiesFoldout.Add( notifyTypeContainer );

            // Container for INotifyValueChanged target selection
            m_notifyValueTargetFieldsContainer = new VisualElement {
                name = "notify-value-target-fields-container",
                style = {
                    flexDirection = FlexDirection.Column,
                    marginTop = 5,
                    display = DisplayStyle.None, // Initial state, updated by UpdateNotifyValueTargetControlsLogic
                },
            };

            var notifyTargetElementRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 3, marginBottom = 2 } };
            notifyTargetElementRow.Add( new Label( "Value Target Element" ) { style = { minWidth = 180, marginRight = 10 } } );
            m_notifyValueTargetElementField = new PopupField<string>( new List<string> { "Backing Field" }, 0 ) { style = { flexGrow = 1 } };
            notifyTargetElementRow.Add( m_notifyValueTargetElementField );
            m_notifyValueTargetFieldsContainer.Add( notifyTargetElementRow );

            var notifyTargetAttributeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2, marginBottom = 3 } };
            notifyTargetAttributeRow.Add( new Label( "Value Target Attribute" ) { style = { minWidth = 180, marginRight = 10 } } );
            m_notifyValueTargetAttributeField = new PopupField<string>( new List<string>(), -1 ) { style = { flexGrow = 1, display = DisplayStyle.None } };
            notifyTargetAttributeRow.Add( m_notifyValueTargetAttributeField );
            m_notifyValueTargetFieldsContainer.Add( notifyTargetAttributeRow );

            // Add a BindMode field to the notify value target fields container
            var notifyTargetBindModeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2, marginBottom = 3 } };
            notifyTargetBindModeRow.Add( new Label( "Bind Mode" ) { style = { minWidth = 180, marginRight = 10 } } );
            m_propertyTypeField = new EnumField( BindingMode.ToTarget ) { style = { width = 100, marginRight = 10 } };
            // m_propertyTypeField.RegisterValueChangedCallback( evt => {
            //     if ( m_notifyValueTargetAttributeField.style.display == DisplayStyle.Flex ) {
            //         GenerateAndDisplayPreview();
            //     }
            // } );
            notifyTargetBindModeRow.Add( m_propertyTypeField );
            m_notifyValueTargetFieldsContainer.Add( notifyTargetBindModeRow );

            m_propertiesFoldout.Add( m_notifyValueTargetFieldsContainer );


            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8, paddingBottom = 4, borderBottomWidth = 1, borderBottomColor = new Color( 0.3f, 0.3f, 0.3f ) } };
            header.Add( new Label( "Property Name" ) { style = { flexGrow = 1.5f, flexBasis = 0 } } );
            header.Add( new Label( "Type" ) { style = { flexGrow = 1, flexBasis = 0 } } );
            header.Add( new Label( "Source" ) { style = { flexGrow = 1.5f, flexBasis = 0 } } );
            header.Add( new Label( "Attribute / Value" ) { style = { flexGrow = 2, flexBasis = 0 } } );
            header.Add( new Label( "Bind Mode" ) { style = { width = 60, flexShrink = 0 } } );
            header.Add( new VisualElement { style = { width = 65, flexShrink = 0 } } );
            m_propertiesFoldout.Add( header );

            m_propertiesScrollView = new ScrollView( ScrollViewMode.Vertical ) { style = { maxHeight = 120, minHeight = 50 } };
            m_propertiesFoldout.Add( m_propertiesScrollView );

            var addButton = new Button( AddPropertyRow ) { text = "Add Property", style = { marginTop = 5 } };
            m_propertiesFoldout.Add( addButton );

            parent.Add( m_propertiesFoldout );
        }

        void CreatePreviewTabs(VisualElement parent) {
            var previewTabs = new TabView { style = { flexGrow = 1, height = new Length( 100, LengthUnit.Percent ) } };

            var csharpTab = new Tab( "C# Preview" );
            var csharpScrollView = new ScrollView( ScrollViewMode.VerticalAndHorizontal );
            m_csharpPreviewField = new TextField( null, -1, true, false, '*' ) { style = { flexGrow = 1 } };
            csharpScrollView.Add( m_csharpPreviewField );
            csharpTab.Add( csharpScrollView );

            var ussTab = new Tab( "USS Preview" );
            var ussScrollView = new ScrollView( ScrollViewMode.VerticalAndHorizontal );
            m_ussPreviewField = new TextField( null, -1, true, false, '*' ) { style = { flexGrow = 1 } };
            ussScrollView.Add( m_ussPreviewField );
            ussTab.Add( ussScrollView );

            var bindDtoTab = new Tab( "Bindable DTO Preview" );
            var bindDtoScrollView = new ScrollView( ScrollViewMode.VerticalAndHorizontal );
            m_bindDtoPreviewField = new TextField( null, -1, true, false, '*' ) { style = { flexGrow = 1 } };
            bindDtoScrollView.Add( m_bindDtoPreviewField );
            bindDtoTab.Add( bindDtoScrollView );

            previewTabs.Add( csharpTab );
            previewTabs.Add( ussTab );
            previewTabs.Add( bindDtoTab );
            parent.Add( previewTabs );
        }

        void CreateFooter(VisualElement parent) {
            var footer = new VisualElement { style = { flexShrink = 0, paddingTop = 5, borderTopWidth = 1, borderTopColor = new Color( 0.18f, 0.18f, 0.18f ) } };
            m_statusHelpBox = new HelpBox( "", HelpBoxMessageType.None ) { style = { display = DisplayStyle.None } };
            m_generateButton = new Button( GenerateFiles ) { text = "Generate Files", style = { height = 30, marginTop = 5 } };
            footer.Add( m_statusHelpBox );
            footer.Add( m_generateButton );
            parent.Add( footer );
        }
        #endregion

        #region Callbacks and UI Logic
        void ShowNotifyTypeMenu() {
            var menu = new GenericMenu();
            GenericMenu.MenuFunction2 setTypeHandler = (userData) => SetNotifyType( (string)userData );

            AddMenuItem( "float" );
            AddMenuItem( "int" );
            AddMenuItem( "bool" );
            AddMenuItem( "string" );
            AddMenuItem( "Color" );
            AddMenuItem( "Vector2" );
            AddMenuItem( "Vector3" );
            menu.AddSeparator( "" );
            menu.AddItem( new GUIContent( "None" ), string.IsNullOrEmpty( m_notifyValueType ), setTypeHandler, "" );
            menu.ShowAsContext();
            return;

            void AddMenuItem(string type)
                => menu.AddItem( new GUIContent( type ), m_notifyValueType == type, setTypeHandler, type );
        }

        void SetNotifyType(string typeName) {
            m_notifyValueType = typeName;
            m_notifyTypeLabel.text = string.IsNullOrEmpty( typeName ) ? "None" : typeName;
            UpdateNotifyValueTargetControlsLogic();
            GenerateAndDisplayPreview();
        }

        void AddPropertyRow() {
            var definition = new PropertyDefinition();
            var row = new VisualElement { name = "property-row", style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
            definition.RowElement = row;

            int propertyNumber = m_propertyDefinitions.Count + 1;
            definition.NameField = new TextField { value = $"Property{propertyNumber}", style = { flexGrow = 1.5f, flexBasis = 0, marginRight = 5 } };
            definition.NameField.RegisterValueChangedCallback( _ => GenerateAndDisplayPreview() );

            definition.TypeField = new EnumField( PropertyDataType.Float ) { style = { flexGrow = 1, flexBasis = 0, marginRight = 5 } };

            List<string> sourceChoices = new() { "Backing Field" };
            sourceChoices.AddRange( m_elementFieldNames );
            definition.SourceElementField = new PopupField<string>( sourceChoices, 0 ) { style = { flexGrow = 1.5f, flexBasis = 0, marginRight = 5 } };

            definition.ValueFieldContainer = new VisualElement { style = { flexGrow = 2, flexBasis = 0 } };
            definition.TargetAttributeField = new PopupField<string>( new List<string>(), 0 ) { style = { display = DisplayStyle.None, flexGrow = 1 } };
            definition.ValueFieldContainer.Add( definition.TargetAttributeField );

            definition.BindingMode = BindingMode.ToTarget; // Default binding mode
            definition.BindingModeField = new EnumField( definition.BindingMode ) { style = { width = 60, flexShrink = 0, marginLeft = 5 } };

            row.Add( definition.NameField );
            row.Add( definition.TypeField );
            row.Add( definition.SourceElementField );
            row.Add( definition.ValueFieldContainer );
            row.Add( definition.BindingModeField );

            definition.SourceElementField.RegisterValueChangedCallback( evt => {
                    UpdateAttributeTargetField( definition, evt.newValue );
                    GenerateAndDisplayPreview();
                }
            );

            definition.TypeField.RegisterValueChangedCallback( evt => {
                    var type = (PropertyDataType)evt.newValue;
                    UpdateValueField( definition, type );
                    UpdateAttributeTargetField( definition, definition.SourceElementField.value );
                    GenerateAndDisplayPreview();
                }
            );

            definition.BindingModeField.RegisterValueChangedCallback( evt => {
                    definition.BindingMode = (BindingMode)evt.newValue;
                    GenerateAndDisplayPreview();
                }
            );

            definition.TargetAttributeField.RegisterValueChangedCallback( _ => GenerateAndDisplayPreview() );

            UpdateValueField( definition, PropertyDataType.Float );

            var removeButton = new Button( () => RemovePropertyRow( definition ) ) { text = "Remove", style = { width = 60, flexShrink = 0, marginLeft = 5 } };
            row.Add( removeButton );

            m_propertiesScrollView.Add( row );
            m_propertyDefinitions.Add( definition );
            GenerateAndDisplayPreview();
        }

        /*void UpdateAttributeTargetField(PropertyDefinition definition, string sourceElementName) {
            bool isProxy = sourceElementName != "Backing Field";
            definition.TargetAttributeField.style.display = isProxy ? DisplayStyle.Flex : DisplayStyle.None;
            if ( definition.ValueField is VisualElement ve ) ve.style.display = isProxy ? DisplayStyle.None : DisplayStyle.Flex;

            if ( isProxy ) {
                var currentType = (PropertyDataType)definition.TypeField.value;
                if ( m_elementFieldTypes.TryGetValue( sourceElementName, out string elementTypeName ) ) {
                    definition.TargetAttributeField.choices = GetValidAttributesForType( elementTypeName, currentType );
                    if ( definition.TargetAttributeField.choices.Any() )
                        definition.TargetAttributeField.index = 0;
                    else
                        definition.TargetAttributeField.index = -1;
                }
            }
        }

        void UpdateValueField(PropertyDefinition definition, PropertyDataType type) {
            // 1. Unregister callback from the PREVIOUS CurrentValueElement
            if ( definition.CurrentValueElement != null && definition.StoredValueChangedCallback != null && definition.StoredCallbackType.HasValue ) {
                UnregisterCallback( definition.CurrentValueElement, definition.StoredCallbackType.Value, definition.StoredValueChangedCallback );
                definition.StoredValueChangedCallback = null;
                definition.StoredCallbackType = null;
            }

            // 2. Remove the PREVIOUS CurrentValueElement if it exists and is part of the container
            if ( definition.CurrentValueElement != null && definition.ValueFieldContainer.Contains( definition.CurrentValueElement ) ) {
                definition.ValueFieldContainer.Remove( definition.CurrentValueElement );
            }

            definition.CurrentValueElement = null; // Clear the reference to the old UI field

            VisualElement newFieldElement;
            switch (type) {
                case PropertyDataType.Float:
                    var floatField = new FloatField { value = 0f };
                    definition.ValueField = new ValueFieldWrapper<float>( floatField );
                    newFieldElement = floatField;
                    break;
                case PropertyDataType.Int:
                    var intField = new IntegerField { value = 0 };
                    definition.ValueField = new ValueFieldWrapper<int>( intField );
                    newFieldElement = intField;
                    break;
                case PropertyDataType.Bool:
                    var boolField = new Toggle { value = false };
                    definition.ValueField = new ValueFieldWrapper<bool>( boolField );
                    newFieldElement = boolField;
                    break;
                case PropertyDataType.String:
                    var stringField = new TextField { value = "" };
                    definition.ValueField = new ValueFieldWrapper<string>( stringField );
                    newFieldElement = stringField;
                    break;
                case PropertyDataType.Color:
                    var colorField = new ColorField { value = Color.white };
                    definition.ValueField = new ValueFieldWrapper<Color>( colorField );
                    newFieldElement = colorField;
                    break;
                case PropertyDataType.Vector2:
                    var vector2Field = new Vector2Field();
                    definition.ValueField = new ValueFieldWrapper<Vector2>( vector2Field );
                    newFieldElement = vector2Field;
                    break;
                case PropertyDataType.Vector3:
                    var vector3Field = new Vector3Field();
                    definition.ValueField = new ValueFieldWrapper<Vector3>( vector3Field );
                    newFieldElement = vector3Field;
                    break;
                default:
                    var defaultField = new TextField();
                    definition.ValueField = new ValueFieldWrapper<string>( defaultField );
                    newFieldElement = defaultField;
                    break;
            }

            newFieldElement.style.flexGrow = 1;
            definition.ValueFieldContainer.Insert( 0, newFieldElement );
            definition.CurrentValueElement = newFieldElement;

            // 3. Register ValueChanged callbacks for the new field and store the callback
            RegisterAndStoreCallback( definition, newFieldElement, type );
        }
        void UpdateNotifyValueTargetControlsLogic() {
            if ( m_notifyValueTargetFieldsContainer == null || m_notifyValueTargetElementField == null || m_notifyValueTargetAttributeField == null ) return;

            bool isNotifyActive = m_isBindableToggle.value && !string.IsNullOrEmpty( m_notifyValueType );
            m_notifyValueTargetFieldsContainer.style.display = isNotifyActive ? DisplayStyle.Flex : DisplayStyle.None;

            if ( isNotifyActive ) {
                // Update Element Choices
                var currentElementSelection = m_notifyValueTargetElementField.value;
                List<string> elementChoices = new List<string> { "Backing Field" };
                elementChoices.AddRange( m_elementFieldNames );
                m_notifyValueTargetElementField.choices = elementChoices;
                if ( elementChoices.Contains( currentElementSelection ) ) {
                    m_notifyValueTargetElementField.SetValueWithoutNotify( currentElementSelection );
                }
                else if ( elementChoices.Any() ) {
                    m_notifyValueTargetElementField.index = 0;
                }
                else {
                    m_notifyValueTargetElementField.index = -1;
                }

                // Update Attribute Choices
                string selectedElementName = m_notifyValueTargetElementField.value;
                if ( selectedElementName != "Backing Field" && !string.IsNullOrEmpty( selectedElementName ) && m_elementFieldTypes.ContainsKey( selectedElementName ) ) {
                    m_notifyValueTargetAttributeField.style.display = DisplayStyle.Flex;
                    var currentAttributeSelection = m_notifyValueTargetAttributeField.value;
                    PropertyDataType? notifyPropertyType = StringToPropertyDataType( m_notifyValueType );

                    if ( notifyPropertyType.HasValue ) {
                        List<string> attributeChoices = GetValidAttributesForType( m_elementFieldTypes[selectedElementName], notifyPropertyType.Value );
                        m_notifyValueTargetAttributeField.choices = attributeChoices;
                        if ( attributeChoices.Contains( currentAttributeSelection ) ) {
                            m_notifyValueTargetAttributeField.SetValueWithoutNotify( currentAttributeSelection );
                        }
                        else if ( attributeChoices.Any() ) {
                            m_notifyValueTargetAttributeField.index = 0;
                        }
                        else {
                            m_notifyValueTargetAttributeField.index = -1;
                        }
                    }
                    else {
                        m_notifyValueTargetAttributeField.choices = new List<string>();
                        m_notifyValueTargetAttributeField.index = -1;
                    }
                }
                else {
                    m_notifyValueTargetAttributeField.style.display = DisplayStyle.None;
                    m_notifyValueTargetAttributeField.choices = new List<string>();
                    m_notifyValueTargetAttributeField.index = -1;
                }
            }
            else {
                m_notifyValueTargetAttributeField.style.display = DisplayStyle.None;
            }
        }*/

        void UpdateAttributeTargetField(PropertyDefinition definition, string sourceElementName) {
            bool isProxy = sourceElementName != "Backing Field";
            definition.TargetAttributeField.style.display = isProxy ? DisplayStyle.Flex : DisplayStyle.None;
            if ( definition.ValueField is VisualElement ve ) ve.style.display = isProxy ? DisplayStyle.None : DisplayStyle.Flex;

            if ( isProxy ) {
                var currentType = (PropertyDataType)definition.TypeField.value;
                List<string> validAttributes = new();
                if ( m_elementFieldTypes.TryGetValue( sourceElementName, out string elementTypeName ) ) {
                    validAttributes = GetValidAttributesForType( elementTypeName, currentType );
                }

                string previousValue = definition.TargetAttributeField.value;
                definition.TargetAttributeField.choices = validAttributes;

                if ( validAttributes.Contains( previousValue ) ) {
                    definition.TargetAttributeField.SetValueWithoutNotify( previousValue );
                }
                else if ( validAttributes.Any() ) {
                    definition.TargetAttributeField.index = 0;
                }
                else {
                    definition.TargetAttributeField.index = -1;
                }
            }
        }

        void UpdateValueField(PropertyDefinition definition, PropertyDataType type) {
            // 1. Unregister callback from the PREVIOUS CurrentValueElement
            if ( definition.CurrentValueElement != null && definition.StoredValueChangedCallback != null && definition.StoredCallbackType.HasValue ) {
                UnregisterCallback( definition.CurrentValueElement, definition.StoredCallbackType.Value, definition.StoredValueChangedCallback );
                definition.StoredValueChangedCallback = null;
                definition.StoredCallbackType = null;
            }

            // 2. Remove the PREVIOUS CurrentValueElement if it exists and is part of the container
            if ( definition.CurrentValueElement != null && definition.ValueFieldContainer.Contains( definition.CurrentValueElement ) ) {
                definition.ValueFieldContainer.Remove( definition.CurrentValueElement );
            }

            definition.CurrentValueElement = null; // Clear the reference to the old UI field

            VisualElement newFieldElement;
            switch (type) {
                case PropertyDataType.Float:
                    var floatField = new FloatField { value = 0f };
                    definition.ValueField = new ValueFieldWrapper<float>( floatField );
                    newFieldElement = floatField;
                    break;
                case PropertyDataType.Int:
                    var intField = new IntegerField { value = 0 };
                    definition.ValueField = new ValueFieldWrapper<int>( intField );
                    newFieldElement = intField;
                    break;
                case PropertyDataType.Bool:
                    var boolField = new Toggle { value = false };
                    definition.ValueField = new ValueFieldWrapper<bool>( boolField );
                    newFieldElement = boolField;
                    break;
                case PropertyDataType.String:
                    var stringField = new TextField { value = "" };
                    definition.ValueField = new ValueFieldWrapper<string>( stringField );
                    newFieldElement = stringField;
                    break;
                case PropertyDataType.Color:
                    var colorField = new ColorField { value = Color.white };
                    definition.ValueField = new ValueFieldWrapper<Color>( colorField );
                    newFieldElement = colorField;
                    break;
                case PropertyDataType.Vector2:
                    var vector2Field = new Vector2Field();
                    definition.ValueField = new ValueFieldWrapper<Vector2>( vector2Field );
                    newFieldElement = vector2Field;
                    break;
                case PropertyDataType.Vector3:
                    var vector3Field = new Vector3Field();
                    definition.ValueField = new ValueFieldWrapper<Vector3>( vector3Field );
                    newFieldElement = vector3Field;
                    break;
                default:
                    var defaultField = new TextField();
                    definition.ValueField = new ValueFieldWrapper<string>( defaultField );
                    newFieldElement = defaultField;
                    break;
            }

            newFieldElement.style.flexGrow = 1;
            definition.ValueFieldContainer.Insert( 0, newFieldElement );
            definition.CurrentValueElement = newFieldElement;

            // 3. Register ValueChanged callbacks for the new field and store the callback
            RegisterAndStoreCallback( definition, newFieldElement, type );
        }
        void UpdateNotifyValueTargetControlsLogic() {
            if ( m_notifyValueTargetFieldsContainer == null || m_notifyValueTargetElementField == null || m_notifyValueTargetAttributeField == null ) return;

            bool isNotifyActive = m_isBindableToggle.value && !string.IsNullOrEmpty( m_notifyValueType );
            m_notifyValueTargetFieldsContainer.style.display = isNotifyActive ? DisplayStyle.Flex : DisplayStyle.None;

            if ( isNotifyActive ) {
                string currentElementSelection = m_notifyValueTargetElementField.value;
                List<string> elementChoices = new() { "Backing Field" };
                elementChoices.AddRange( m_elementFieldNames );
                m_notifyValueTargetElementField.choices = elementChoices;
                if ( elementChoices.Contains( currentElementSelection ) ) {
                    m_notifyValueTargetElementField.SetValueWithoutNotify( currentElementSelection );
                }
                else if ( elementChoices.Any() ) {
                    m_notifyValueTargetElementField.index = 0;
                }
                else {
                    m_notifyValueTargetElementField.index = -1;
                }

                string selectedElementName = m_notifyValueTargetElementField.value;
                if ( selectedElementName != "Backing Field" && !string.IsNullOrEmpty( selectedElementName ) && m_elementFieldTypes.TryGetValue( selectedElementName, out string elementTypeName ) ) {
                    m_notifyValueTargetAttributeField.style.display = DisplayStyle.Flex;
                    string currentAttributeSelection = m_notifyValueTargetAttributeField.value;
                    PropertyDataType? notifyPropertyType = StringToPropertyDataType( m_notifyValueType );
                    List<string> attributes = new();

                    if ( notifyPropertyType.HasValue ) {
                        attributes = GetValidAttributesForType( elementTypeName, notifyPropertyType.Value );
                    }

                    m_notifyValueTargetAttributeField.choices = attributes;

                    if ( attributes.Contains( currentAttributeSelection ) ) {
                        m_notifyValueTargetAttributeField.SetValueWithoutNotify( currentAttributeSelection );
                    }
                    else if ( attributes.Any() ) {
                        m_notifyValueTargetAttributeField.index = 0;
                    }
                    else {
                        m_notifyValueTargetAttributeField.index = -1;
                    }
                }
                else {
                    m_notifyValueTargetAttributeField.style.display = DisplayStyle.None;
                    m_notifyValueTargetAttributeField.choices = new List<string>();
                    m_notifyValueTargetAttributeField.index = -1;
                }
            }
            else {
                m_notifyValueTargetAttributeField.style.display = DisplayStyle.None;
                m_notifyValueTargetAttributeField.choices = new List<string>();
                m_notifyValueTargetAttributeField.index = -1;
            }
        }

        static void UnregisterCallback(VisualElement element, PropertyDataType dataType, object callbackInstance) {
            switch (dataType) {
                case PropertyDataType.Float:
                    (element as INotifyValueChanged<float>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<float>>)callbackInstance );
                    break;
                case PropertyDataType.Int:
                    (element as INotifyValueChanged<int>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<int>>)callbackInstance );
                    break;
                case PropertyDataType.Bool:
                    (element as INotifyValueChanged<bool>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<bool>>)callbackInstance );
                    break;
                case PropertyDataType.String:
                    (element as INotifyValueChanged<string>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<string>>)callbackInstance );
                    break;
                case PropertyDataType.Color:
                    (element as INotifyValueChanged<Color>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<Color>>)callbackInstance );
                    break;
                case PropertyDataType.Vector2:
                    (element as INotifyValueChanged<Vector2>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<Vector2>>)callbackInstance );
                    break;
                case PropertyDataType.Vector3:
                    (element as INotifyValueChanged<Vector3>)?.UnregisterValueChangedCallback( (EventCallback<ChangeEvent<Vector3>>)callbackInstance );
                    break;
                default:
                    throw new ArgumentOutOfRangeException( nameof(dataType), dataType, null );
            }
        }

        void RegisterAndStoreCallback(PropertyDefinition definition, VisualElement fieldElement, PropertyDataType dataType) {
            object newCallback;
            switch (dataType) {
                case PropertyDataType.Float:
                    EventCallback<ChangeEvent<float>> floatCb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<float>)?.RegisterValueChangedCallback( floatCb );
                    newCallback = floatCb;
                    break;
                case PropertyDataType.Int:
                    EventCallback<ChangeEvent<int>> intCb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<int>)?.RegisterValueChangedCallback( intCb );
                    newCallback = intCb;
                    break;
                case PropertyDataType.Bool:
                    EventCallback<ChangeEvent<bool>> boolCb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<bool>)?.RegisterValueChangedCallback( boolCb );
                    newCallback = boolCb;
                    break;
                case PropertyDataType.String:
                    EventCallback<ChangeEvent<string>> stringCb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<string>)?.RegisterValueChangedCallback( stringCb );
                    newCallback = stringCb;
                    break;
                case PropertyDataType.Color:
                    EventCallback<ChangeEvent<Color>> colorCb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<Color>)?.RegisterValueChangedCallback( colorCb );
                    newCallback = colorCb;
                    break;
                case PropertyDataType.Vector2:
                    EventCallback<ChangeEvent<Vector2>> v2Cb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<Vector2>)?.RegisterValueChangedCallback( v2Cb );
                    newCallback = v2Cb;
                    break;
                case PropertyDataType.Vector3:
                    EventCallback<ChangeEvent<Vector3>> v3Cb = _ => GenerateAndDisplayPreview();
                    (fieldElement as INotifyValueChanged<Vector3>)?.RegisterValueChangedCallback( v3Cb );
                    newCallback = v3Cb;
                    break;
                default:
                    throw new ArgumentOutOfRangeException( nameof(dataType), dataType, null );
            }

            definition.StoredValueChangedCallback = newCallback;
            definition.StoredCallbackType = dataType;
        }

        void RemovePropertyRow(PropertyDefinition definition) {
            if ( definition.CurrentValueElement != null && definition.StoredValueChangedCallback != null && definition.StoredCallbackType.HasValue ) {
                UnregisterCallback( definition.CurrentValueElement, definition.StoredCallbackType.Value, definition.StoredValueChangedCallback );
                definition.StoredValueChangedCallback = null;
                definition.StoredCallbackType = null;
            }

            if ( m_propertyDefinitions.Remove( definition ) ) {
                m_propertiesScrollView.Remove( definition.RowElement );
                GenerateAndDisplayPreview();
            }
        }
        #endregion

        #region Core Logic
        void SetInitialState() {
            SetNotifyType( "" ); // Initialize to "None"

            bool hasFile = m_uxmlFile;
            m_outputFolderPathField.SetEnabled( hasFile );
            m_outputClassNameField.SetEnabled( hasFile );
            m_namespaceField.SetEnabled( hasFile );
            m_generateButton.SetEnabled( hasFile );
            m_extractUssToggle.SetEnabled( hasFile );
            m_setTextFieldsToggle.SetEnabled( hasFile );
            m_allowDisplayNoneToggle.SetEnabled( hasFile );
            m_isBindableToggle.SetEnabled( hasFile );
            m_createBindDtoToggle.SetEnabled( hasFile );
            m_propertiesFoldout.style.display = hasFile && m_isBindableToggle.value ? DisplayStyle.Flex : DisplayStyle.None;
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
            SetInitialState();

            if ( m_uxmlFile ) {
                string uxmlPath = AssetDatabase.GetAssetPath( m_uxmlFile );
                m_outputFolderPathField.value = Path.GetDirectoryName( uxmlPath );
                string baseName = Path.GetFileNameWithoutExtension( uxmlPath ).Replace( " ", "" );
                m_outputClassNameField.value = $"{baseName}Element";
                GenerateAndDisplayPreview();
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
                                 && d.Name.LocalName != "UXML" && d.Name.LocalName != "Style"
                                 && (m_allowDisplayNoneToggle.value || d.AncestorsAndSelf().All( a =>
                                                                                                     a.Attribute( "style" )?.Value.Contains( "display: none" ) != true
                                 ))
                    ).ToList();

                m_elementFieldNames.Clear();
                m_elementFieldTypes.Clear();

                if ( !instantiableElements.Any() ) {
                    ShowStatus( "No valid VisualElements found in UXML.", HelpBoxMessageType.Warning );
                    m_generatedCSharpContent = "/* No instantiable elements found. */";
                    m_generatedUssContent = "";
                }
                else {
                    Dictionary<XElement, ElementInfo> elementMap = new();
                    Dictionary<string, int> nameCounters = new();

                    foreach (var element in instantiableElements) {
                        var info = new ElementInfo { XmlElement = element, ElementType = element.Name.LocalName };
                        info.FieldName = GenerateFieldName( info, nameCounters );
                        m_elementFieldNames.Add( info.FieldName );
                        m_elementFieldTypes[info.FieldName] = info.ElementType;

                        var parentXml = element.Parent;
                        if ( parentXml != null && elementMap.TryGetValue( parentXml, out var parentInfo ) ) {
                            info.Parent = parentInfo;
                            parentInfo.Children.Add( info );
                        }

                        elementMap[element] = info;
                    }

                    // Update choices for target elements before generating C#
                    UpdateNotifyValueTargetControlsLogic();

                    m_generatedCSharpContent = GenerateCSharpContent( elementMap.Values );
                    m_generatedUssContent = m_extractUssToggle.value ? GenerateUssContent( elementMap.Values ) : "";
                    m_generatedBindDtoContent = m_createBindDtoToggle.value ? GenerateBindableDtoContent() : "";
                    ShowStatus( "Preview generated.", HelpBoxMessageType.Info );
                }

                m_csharpPreviewField.value = m_generatedCSharpContent;
                m_ussPreviewField.value = m_extractUssToggle.value
                    ? (string.IsNullOrEmpty( m_generatedUssContent ) ? "/* No inline styles to extract. */" : m_generatedUssContent)
                    : "Enable 'Extract Inline Styles' to see a preview.";
                m_bindDtoPreviewField.value = m_createBindDtoToggle.value
                    ? (string.IsNullOrEmpty( m_generatedBindDtoContent ) ? "/* No properties defined for DTO generation." : m_generatedBindDtoContent)
                    : "Enable 'Create Bindable DTO' to see a preview.";
            }
            catch (Exception ex) {
                m_generatedCSharpContent = m_generatedUssContent = "";
                m_csharpPreviewField.value = m_ussPreviewField.value = $"/* Error generating preview: {ex.Message} */";
                m_bindDtoPreviewField.value = $"/* Error generating Bindable DTO preview: {ex.Message} */";
                ShowStatus( "Error during preview generation. Check console.", HelpBoxMessageType.Error );
                Debug.LogError( ex );
            }
        }

        string GenerateBindableDtoContent() {

            #region ExampleBindDTO
            /*public class ExampleBindDTO{
                [AutoBind, DontCreateProperty, SerializeField] float m_value;
                [AutoBind, DontCreateProperty, SerializeField] bool m_showEffectBar;

                [AutoBind, DontCreateProperty, SerializeField] string m_statusEffectHeader;
                [AutoBind, DontCreateProperty, SerializeField] Texture2D m_icon;
                [AutoBind, DontCreateProperty, SerializeField] ValuePrefixType m_prefixType = ValuePrefixType.None;

                [AutoBind, DontCreateProperty, SerializeField] Color m_statusColor;
                [AutoBind, DontCreateProperty, SerializeField] Color m_iconColor;
                [AutoBind, DontCreateProperty, SerializeField] Color m_textColor;
            }*/
            #endregion

            if ( !m_createBindDtoToggle.value ) return "";

            var sb = new StringBuilder();
            var className = $"{m_outputClassNameField.value}DTO";
            string namespaceName = m_namespaceField.value;
            bool hasNamespace = !string.IsNullOrWhiteSpace( namespaceName );
            string indent = hasNamespace ? "    " : "";
            bool isBindable = m_isBindableToggle.value;
            bool hasNotifyInterface = isBindable && !string.IsNullOrWhiteSpace( m_notifyValueType );

            sb.AppendLine( "using System;" );
            sb.AppendLine( "using UnityEngine;" );
            sb.AppendLine( "using Unity.Properties;" );
            sb.AppendLine( "using TCS.UiToolkitUtils.Attributes;" );
            sb.AppendLine( "" );

            if ( hasNamespace ) {
                sb.AppendLine( $"namespace {namespaceName}" );
                sb.AppendLine( "{" );
            }

            sb.AppendLine( $"{indent}[Serializable]" );
            sb.AppendLine( $"{indent}public partial class {className}" );
            sb.AppendLine( $"{indent}{{" );

            string innerIndent = indent + "    ";

            List<PropertyDefinition> validProperties = m_propertyDefinitions
                .Where( p => !string.IsNullOrWhiteSpace( p.NameField.value ) )
                .ToList();

            var hasAnyFields = false;

            if ( hasNotifyInterface ) {
                sb.AppendLine( $"{innerIndent}[AutoBind, DontCreateProperty, SerializeField]" );
                sb.AppendLine( $"{innerIndent}private {m_notifyValueType} m_value;" );
                sb.AppendLine();
                hasAnyFields = true;
            }

            if ( validProperties.Any() ) {
                foreach (var propDef in validProperties) {
                    string propName = propDef.NameField.value;
                    var fieldName = $"m_{char.ToLowerInvariant( propName[0] ) + propName.Substring( 1 )}";
                    string propType = GetTypeString( (PropertyDataType)propDef.TypeField.value );

                    sb.AppendLine( $"{innerIndent}[AutoBind, DontCreateProperty, SerializeField]" );
                    sb.AppendLine( $"{innerIndent}private {propType} {fieldName};" );
                    sb.AppendLine();
                    hasAnyFields = true;
                }
            }

            if ( !hasAnyFields ) {
                sb.AppendLine( $"{innerIndent}// No properties defined for DTO generation." );
            }

            sb.AppendLine( $"{indent}}}" );

            if ( hasNamespace ) {
                sb.AppendLine( "}" );
            }

            return sb.ToString();
        }

        string GenerateCSharpContent(IEnumerable<ElementInfo> elements) {
            var sb = new StringBuilder();
            List<ElementInfo> elementInfos = elements.ToList();
            string className = m_outputClassNameField.value;
            string namespaceName = m_namespaceField.value;
            bool hasNamespace = !string.IsNullOrWhiteSpace( namespaceName );
            string indent = hasNamespace ? "    " : "";
            bool isBindable = m_isBindableToggle.value;
            bool hasNotifyInterface = isBindable && !string.IsNullOrWhiteSpace( m_notifyValueType );

            sb.AppendLine( "using System;" );
            sb.AppendLine( "using System.Collections.Generic;" );
            sb.AppendLine( "using UnityEngine;" );
            sb.AppendLine( "using UnityEngine.UIElements;" );
            sb.AppendLine( "using TCS.UiToolkitUtils;" );
            sb.AppendLine( "using TCS.UiToolkitUtils.Attributes;" );
            if ( isBindable ) sb.AppendLine( "using Unity.Properties;" );

            if ( hasNamespace ) sb.AppendLine( $"\nnamespace {namespaceName}\n{{" );

            string baseClass = isBindable ? "BindableElement" : "VisualElement";
            string interfaces = hasNotifyInterface ? $", INotifyValueChanged<{m_notifyValueType}>" : "";
            sb.AppendLine( $"{indent}[UxmlElement] public partial class {className} : {baseClass}{interfaces}" );
            sb.AppendLine( $"{indent}{{" );

            string innerIndent = indent + "    ";

            sb.AppendLine( $"{innerIndent}#region UI Elements" );
            foreach (var info in elementInfos) {
                string nameInQuotes = info.XmlElement.Attribute( "name" )?.Value ?? info.FieldName.Substring( 2 );
                sb.AppendLine( $"{innerIndent}[USSName] readonly {info.ElementType} {info.FieldName} = new() {{ name = \"{nameInQuotes}\" }};" );
            }

            sb.AppendLine( $"{innerIndent}#endregion\n" );

            List<PropertyDefinition> validProperties = m_propertyDefinitions
                .Where( p => !string.IsNullOrWhiteSpace( p.NameField.value ) )
                .ToList();

            if ( isBindable && (validProperties.Any() || hasNotifyInterface) ) {
                sb.AppendLine( $"{innerIndent}#region Backing Fields" );
                if ( hasNotifyInterface ) sb.AppendLine( $"{innerIndent}private {m_notifyValueType} m_value;" );
                foreach (var propDef in validProperties) {
                    sb.AppendLine( $"{innerIndent}private {GetTypeString( (PropertyDataType)propDef.TypeField.value )} m_{propDef.NameField.value.ToLowerInvariant()};" );
                }

                sb.AppendLine( $"{innerIndent}#endregion\n" );
            }

            sb.AppendLine( $"{innerIndent}public {className}()" );
            sb.AppendLine( $"{innerIndent}{{" );
            sb.AppendLine( $"{innerIndent}    SetElementClassNames();" );
            if ( m_setTextFieldsToggle.value ) SetTextFields( elementInfos, sb, innerIndent );
            sb.AppendLine();
            sb.AppendLine( $"{innerIndent}    // Build Hierarchy" );
            foreach (var info in elementInfos) {
                sb.AppendLine( $"{innerIndent}    {(info.Parent == null ? "hierarchy" : info.Parent.FieldName)}.Add({info.FieldName});" );
            }

            List<PropertyDefinition> initializableProperties = validProperties
                .Where( p => p.ValueField != null )
                .ToList();
            if ( isBindable && initializableProperties.Any() ) {
                sb.AppendLine();
                sb.AppendLine( $"{innerIndent}    // Initialize Properties" );
                foreach (var propDef in initializableProperties) {
                    sb.AppendLine( $"{innerIndent}    {propDef.NameField.value} = {FormatDefaultValue( propDef )};" );
                }
            }

            sb.AppendLine( $"{innerIndent}}}" );

            if ( isBindable ) {
                sb.AppendLine();
                sb.AppendLine( $"{innerIndent}#region Bindable Properties" );

                if ( hasNotifyInterface ) {
                    sb.AppendLine( $"{innerIndent}[CreateProperty, UxmlAttribute(\"value\"), CreateBindID]" );
                    sb.AppendLine( $"{innerIndent}public {m_notifyValueType} value" );
                    sb.AppendLine( $"{innerIndent}{{" );
                    sb.AppendLine( $"{innerIndent}    get => m_value;" );
                    sb.AppendLine( $"{innerIndent}    set" );
                    sb.AppendLine( $"{innerIndent}    {{" );
                    sb.AppendLine( $"{innerIndent}        if (EqualityComparer<{m_notifyValueType}>.Default.Equals(m_value, value)) return;" );
                    sb.AppendLine( $"{innerIndent}        if (panel != null) {{" );
                    sb.AppendLine( $"{innerIndent}            using var pooled = ChangeEvent<{m_notifyValueType}>.GetPooled(m_value, value);" );
                    sb.AppendLine( $"{innerIndent}            pooled.target = this;" );
                    sb.AppendLine( $"{innerIndent}            SetValueWithoutNotify(value);" );
                    sb.AppendLine( $"{innerIndent}            SendEvent(pooled);" );
                    sb.AppendLine( $"{innerIndent}        }} else SetValueWithoutNotify(value);" );
                    sb.AppendLine( $"{innerIndent}        NotifyPropertyChanged(valueProperty);" );
                    sb.AppendLine( $"{innerIndent}    }}" );
                    sb.AppendLine( $"{innerIndent}}}\n" );
                }

                foreach (var propDef in validProperties) {
                    string propName = propDef.NameField.value;
                    string propType = GetTypeString( (PropertyDataType)propDef.TypeField.value );
                    var backingFieldName = $"m_{propName.ToLowerInvariant()}";

                    sb.AppendLine( $"{innerIndent}[CreateProperty, UxmlAttribute(\"{ToKebabCase( propName )}\"), CreateBindID]" );
                    sb.AppendLine( $"{innerIndent}public {propType} {propName}" );
                    sb.AppendLine( $"{innerIndent}{{" );
                    sb.AppendLine( $"{innerIndent}    get => {backingFieldName};" );
                    sb.AppendLine( $"{innerIndent}    set" );
                    sb.AppendLine( $"{innerIndent}    {{" );
                    sb.AppendLine( $"{innerIndent}        if (EqualityComparer<{propType}>.Default.Equals({backingFieldName}, value)) return;" );
                    sb.AppendLine( $"{innerIndent}        {backingFieldName} = value;" );

                    if ( propDef.SourceElementField.value != "Backing Field" ) {
                        string targetElement = propDef.SourceElementField.value;
                        string targetAttribute = propDef.TargetAttributeField.value;
                        sb.AppendLine( $"{innerIndent}        if ({targetElement} != null) {targetElement}.{targetAttribute} = value;" );
                    }

                    sb.AppendLine( $"{innerIndent}        NotifyPropertyChanged({propName}Property);" );
                    sb.AppendLine( $"{innerIndent}    }}" );

                    sb.AppendLine( $"{innerIndent}}}\n" );
                }

                if ( hasNotifyInterface ) {
                    sb.AppendLine( $"{innerIndent}public void SetValueWithoutNotify({m_notifyValueType} newValue)" );
                    sb.AppendLine( $"{innerIndent}{{" );
                    sb.AppendLine( $"{innerIndent}    m_value = newValue;" );

                    string notifyTargetElementName = m_notifyValueTargetElementField?.value;
                    string notifyTargetAttributeName = m_notifyValueTargetAttributeField?.value;

                    if ( !string.IsNullOrEmpty( notifyTargetElementName ) &&
                         notifyTargetElementName != "Backing Field" &&
                         !string.IsNullOrEmpty( notifyTargetAttributeName ) &&
                         m_elementFieldTypes.ContainsKey( notifyTargetElementName ) ) {
                        sb.AppendLine( $"{innerIndent}    if ({notifyTargetElementName} != null) {notifyTargetElementName}.{notifyTargetAttributeName} = newValue;" );
                    }

                    sb.AppendLine( $"{innerIndent}}}" );
                }

                GenerateDataObject( sb, innerIndent, className, hasNotifyInterface, validProperties );

                sb.AppendLine( $"{innerIndent}#endregion\n" );
            }

            sb.AppendLine( $"{indent}}}" );
            if ( hasNamespace ) sb.AppendLine( "}" );

            return sb.ToString();
        }

        void GenerateDataObject(
            StringBuilder sb,
            string innerIndent,
            string className,
            bool hasNotifyInterface,
            List<PropertyDefinition> validProperties
        ) {

            #region Example Bind Method for Element
            /*public class ExampleBindMethodForElement : BindableElement {
                    public void BindElement(StatusEffectDataDTO dto) {
                        dataSource = dto;
                        var progressBind = new DataBinding().Configure( () => dto.Value, BindingMode.ToTargetOnce );
                        SetBinding( valueProperty, progressBind );
                        var colorBind = new DataBinding().Configure( () => dto.StatusColor, BindingMode.ToSource );
                        SetBinding( StatusColorProperty, colorBind );
                        var iconBind = new DataBinding().Configure( () => dto.Icon, BindingMode.ToSource );
                        SetBinding( StatusIconProperty, iconBind );
                        var iconColorBind = new DataBinding().Configure( () => dto.IconColor, BindingMode.ToTarget );
                        SetBinding( IconColorProperty, iconColorBind );
                        var headerBind = new DataBinding().Configure( () => dto.StatusEffectHeader, BindingMode.ToTarget );
                        SetBinding( StatusEffectHeaderProperty, headerBind );
                        var textColorBind = new DataBinding().Configure( () => dto.TextColor, BindingMode.ToTarget );
                        SetBinding( TextColorProperty, textColorBind );
                        var prefixTypeBind = new DataBinding().Configure( () => dto.PrefixType, BindingMode.TwoWay );
                        SetBinding( StatusValuePrefixTypeProperty, prefixTypeBind );
                        var showEffectBarBind = new DataBinding().Configure( () => dto.ShowEffectBar, BindingMode.TwoWay );
                        SetBinding( ShowEffectBarProperty, showEffectBarBind );
                    }
                }*/
            #endregion

            if ( m_createBindDtoToggle.value ) {
                sb.AppendLine( $"\n{innerIndent}public void BindDTO({className}DTO dto)" );
                sb.AppendLine( $"{innerIndent}{{" );
                sb.AppendLine( $"{innerIndent}    dataSource = dto;" );

                if ( hasNotifyInterface ) {
                    BindingMode mainValueBindingMode = (BindingMode)m_propertyTypeField.value;
                    sb.AppendLine( $"{innerIndent}    var valueBind = new DataBinding().Configure( () => dto.Value, BindingMode.{mainValueBindingMode} );" );
                    sb.AppendLine( $"{innerIndent}    SetBinding( valueProperty, valueBind );" );
                }

                foreach (var propDef in validProperties) {
                    string propName = propDef.NameField.value;
                    var bindVariableName = $"{propName.ToLowerInvariant()}Bind";
                    // Use the BindingMode from propDef
                    sb.AppendLine(
                        $"{innerIndent}    var {bindVariableName} = new DataBinding().Configure( () => dto.{propName}, BindingMode.{propDef.BindingMode} );"
                    );
                    sb.AppendLine(
                        $"{innerIndent}    SetBinding( {propName}Property, {bindVariableName} );"
                    );
                }

                sb.AppendLine( $"{innerIndent}}}" );
            }
        }

        static string GetTypeString(PropertyDataType type) {
            return type switch {
                PropertyDataType.Float => "float",
                PropertyDataType.Int => "int",
                PropertyDataType.Bool => "bool",
                PropertyDataType.String => "string",
                _ => type.ToString(),
            };
        }

        static string FormatDefaultValue(PropertyDefinition propDef) {
            if ( propDef.ValueField == null ) return "default";
            object value = propDef.ValueField.GetValue();

            return value switch {
                string s => $"\"{Escape( s )}\"",
                float f => $"{f}f",
                int i => i.ToString(),
                bool b => b.ToString().ToLower(),
                Color c => GetColorString( c ),
                Vector2 v2 => GetVector2String( v2 ),
                Vector3 v3 => GetVector3String( v3 ),
                _ => "default",
            };
        }
        #endregion

        #region Helpers
        static PropertyDataType? StringToPropertyDataType(string typeName) {
            return typeName switch {
                "float" => PropertyDataType.Float,
                "int" => PropertyDataType.Int,
                "bool" => PropertyDataType.Bool,
                "string" => PropertyDataType.String,
                "Color" => PropertyDataType.Color,
                "Vector2" => PropertyDataType.Vector2,
                "Vector3" => PropertyDataType.Vector3,
                _ => null,
            };
        }
        static void SetTextFields(IEnumerable<ElementInfo> elementInfos, StringBuilder sb, string indent) {
            List<ElementInfo> textElements = elementInfos.Where( info => info.XmlElement.Attribute( "text" )?.Value != null || info.XmlElement.Attribute( "label" )?.Value != null ).ToList();
            if ( !textElements.Any() ) return;

            sb.AppendLine( $"\n{indent}    // Set Text Fields" );
            foreach (var info in textElements) {
                string raw = info.XmlElement.Attribute( "text" )?.Value ?? info.XmlElement.Attribute( "label" )?.Value;
                if ( string.IsNullOrWhiteSpace( raw ) ) continue;

                if ( LabelProps.Contains( info.ElementType ) || info.XmlElement.Attribute( "label" ) != null )
                    sb.AppendLine( $"{indent}    {info.FieldName}.label = \"{Escape( raw )}\";" );
                else if ( TextProps.Contains( info.ElementType ) )
                    sb.AppendLine( $"{indent}    {info.FieldName}.text = \"{Escape( raw )}\";" );
            }
        }

        string GenerateUssContent(IEnumerable<ElementInfo> elements) {
            var sb = new StringBuilder();
            string baseClassName = ToKebabCase( m_outputClassNameField.value );

            foreach (var info in elements) {
                var styleAttr = info.XmlElement.Attribute( "style" );
                if ( styleAttr == null || string.IsNullOrWhiteSpace( styleAttr.Value ) ) continue;

                string rawFieldName = info.FieldName.Substring( 2 );
                string elementName = ToKebabCase( rawFieldName );
                sb.AppendLine( $".{baseClassName}_{elementName} {{" );

                string[] styles = styleAttr.Value.Split( new[] { ';' }, StringSplitOptions.RemoveEmptyEntries );
                foreach (string style in styles) sb.AppendLine( $"    {style.Trim()};" );
                sb.AppendLine( "}\n" );
            }

            return sb.ToString();
        }

        static string GenerateFieldName(ElementInfo info, IDictionary<string, int> counters) {
            string baseName = info.XmlElement.Attribute( "name" )?.Value ?? info.ElementType;
            string[] words = Regex.Split( baseName, @"[^a-zA-Z0-9]+|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])" )
                .Where( s => !string.IsNullOrWhiteSpace( s ) ).ToArray();

            if ( words.Length == 0 ) words = new[] { "element" };

            var pascalCasedNameBuilder = new StringBuilder();
            foreach (string word in words) {
                if ( string.IsNullOrEmpty( word ) ) continue;
                pascalCasedNameBuilder.Append( char.ToUpperInvariant( word[0] ) );
                pascalCasedNameBuilder.Append( word.Substring( 1 ).ToLower() );
            }

            baseName = pascalCasedNameBuilder.ToString();

            if ( !counters.TryAdd( baseName, 1 ) ) counters[baseName]++;
            if ( counters[baseName] > 1 ) baseName += counters[baseName];

            string finalFieldNamePart = char.ToLowerInvariant( baseName[0] ) + baseName.Substring( 1 );
            return $"m_{finalFieldNamePart}";
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
            if ( File.Exists( csPath ) && !EditorUtility.DisplayDialog( "File Exists", $"The file '{className}.cs' already exists. Overwrite it?", "Overwrite", "Cancel" ) ) {
                ShowStatus( "Generation cancelled by user.", HelpBoxMessageType.Warning );
                return;
            }

            File.WriteAllText( csPath, m_generatedCSharpContent );

            if ( m_extractUssToggle.value && !string.IsNullOrEmpty( m_generatedUssContent ) ) {
                var ussFileName = $"{className}.uss";
                string ussPath = Path.Combine( m_outputFolderPathField.value, ussFileName );
                if ( File.Exists( ussPath ) && !EditorUtility.DisplayDialog( "File Exists", $"The file '{ussFileName}' already exists. Overwrite it?", "Overwrite", "Cancel" ) )
                    ShowStatus( "Generation cancelled by user (USS file).", HelpBoxMessageType.Warning );
                else
                    File.WriteAllText( ussPath, m_generatedUssContent );
            }

            if ( m_createBindDtoToggle.value && !string.IsNullOrEmpty( m_generatedBindDtoContent ) ) {
                var dtoFileName = $"{className}DTO.cs";
                string dtoPath = Path.Combine( m_outputFolderPathField.value, dtoFileName );
                if ( File.Exists( dtoPath ) && !EditorUtility.DisplayDialog( "File Exists", $"The file '{dtoFileName}' already exists. Overwrite it?", "Overwrite", "Cancel" ) )
                    ShowStatus( "Generation cancelled by user (DTO file).", HelpBoxMessageType.Warning );
                else
                    File.WriteAllText( dtoPath, m_generatedBindDtoContent );
            }

            AssetDatabase.Refresh();
            ShowStatus( $"Success! Files generated in:\n{m_outputFolderPathField.value}", HelpBoxMessageType.Info );
        }

        static string GetColorString(Color c) => $"new Color({c.r}f, {c.g}f, {c.b}f, {c.a}f)";
        static string GetVector2String(Vector2 v) => $"new Vector2({v.x}f, {v.y}f)";
        static string GetVector3String(Vector3 v) => $"new Vector3({v.x}f, {v.y}f, {v.z}f)";

        static readonly HashSet<string> TextProps = new() { "Label", "Button", "Foldout", "TextElement", "Toggle", "RadioButton" };

        static readonly HashSet<string> LabelProps = new() {
            "TextField", "Slider", "SliderInt", "MinMaxSlider", "DropdownField", "EnumField", "RadioButtonGroup",
            "IntegerField", "FloatField", "LongField", "DoubleField", "Hash128Field", "Vector2Field", "Vector3Field",
            "Vector4Field", "RectField", "BoundsField", "UnsignedIntegerField", "UnsignedLongField", "Vector2IntField",
            "Vector3IntField", "RectIntField", "BoundsIntField",
        };
        static string Escape(string s) => s.Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" );
        static string ToKebabCase(string str) => string.IsNullOrEmpty( str ) ? str : Regex
            .Replace( str, "(?<!^)([A-Z])", "-$1" )
            .ToLower();

        class ValueFieldWrapper<T> : IValueField<T> {
            readonly BaseField<T> m_field;
            public ValueFieldWrapper(BaseField<T> field) => m_field = field;
            public T GetValue() => m_field.value;
            object IValueField.GetValue() => GetValue();
        }

        readonly Dictionary<string, Dictionary<PropertyDataType, List<string>>> m_dynamicAttributeMap = new();

        static PropertyDataType? ConvertTypeToPropertyDataType(Type type) {
            if ( type == typeof(string) ) return PropertyDataType.String;
            if ( type == typeof(bool) ) return PropertyDataType.Bool;
            if ( type == typeof(float) ) return PropertyDataType.Float;
            if ( type == typeof(int) ) return PropertyDataType.Int;
            // if ( type == typeof(double) ) return PropertyDataType.Double;
            // if ( type == typeof(long) ) return PropertyDataType.Long;
            // if ( type == typeof(Rect) ) return PropertyDataType.Rect;
            // if ( type == typeof(Bounds) ) return PropertyDataType.Bounds;
            // if ( type == typeof(BoundsInt) ) return PropertyDataType.BoundsInt;
            // if ( type == typeof(Hash128) ) return PropertyDataType.Hash128;
            if ( type == typeof(Color) ) return PropertyDataType.Color;
            if ( type == typeof(Vector2) ) return PropertyDataType.Vector2;
            if ( type == typeof(Vector3) ) return PropertyDataType.Vector3;
            // if ( type == typeof(Vector4) ) return PropertyDataType.Vector4;
            return null;
        }

        void InitializeAttributeMap() {
            m_dynamicAttributeMap.Clear();
            var visualElementBaseType = typeof(VisualElement);
            List<Type> allRelevantTypes = new();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                try {
                    if ( assembly.IsDynamic ) continue; // Skip dynamic assemblies
                    allRelevantTypes.AddRange(
                        assembly.GetTypes()
                            .Where( t => t.IsPublic && !t.IsAbstract && visualElementBaseType.IsAssignableFrom( t ) )
                    );
                }
                catch (ReflectionTypeLoadException) {
                    /* Ignore assemblies that fail to load types */
                }
                catch (Exception ex) {
                    Debug.LogWarning( $"[UxmlToCSharpConverter] Error reflecting assembly {assembly.FullName}: {ex.Message}" );
                }
            }

            allRelevantTypes = allRelevantTypes.Distinct().ToList();

            foreach (var type in allRelevantTypes) {
                string elementTypeName = type.Name;
                if ( !m_dynamicAttributeMap.ContainsKey( elementTypeName ) ) {
                    m_dynamicAttributeMap[elementTypeName] = new Dictionary<PropertyDataType, List<string>>();
                }

                Dictionary<PropertyDataType, List<string>> currentTypeAttributes = m_dynamicAttributeMap[elementTypeName];

                // Using BindingFlags.Public | BindingFlags.Instance gets all public instance properties, including inherited ones.
                PropertyInfo[] properties = type.GetProperties( BindingFlags.Public | BindingFlags.Instance );
                foreach (var propInfo in properties) {
                    if ( !propInfo.CanRead || propInfo.GetSetMethod( false ) == null ) continue;

                    // Skip the 'style' property itself to avoid reflection on it
                    if ( propInfo.Name == "style" ) continue;

                    // Handle all other properties via reflection
                    PropertyDataType? pdt = ConvertTypeToPropertyDataType( propInfo.PropertyType );
                    if ( pdt.HasValue ) {
                        if ( !currentTypeAttributes.ContainsKey( pdt.Value ) ) {
                            currentTypeAttributes[pdt.Value] = new List<string>();
                        }

                        if ( !currentTypeAttributes[pdt.Value].Contains( propInfo.Name ) ) {
                            currentTypeAttributes[pdt.Value].Add( propInfo.Name );
                        }
                    }
                }

                // Manually add the desired style properties for any VisualElement type
                if ( visualElementBaseType.IsAssignableFrom( type ) ) {
                    // Add Color styles
                    if ( !currentTypeAttributes.ContainsKey( PropertyDataType.Color ) ) {
                        currentTypeAttributes[PropertyDataType.Color] = new List<string>();
                    }

                    List<string> colorStyles = new() {
                        "style.backgroundColor",
                        "style.color",
                        "style.borderTopColor",
                        "style.borderRightColor",
                        "style.borderBottomColor",
                        "style.borderLeftColor",
                        "style.unityBackgroundImageTintColor",
                        "style.unityTextOutlineColor",
                    };
                    currentTypeAttributes[PropertyDataType.Color].AddRange( colorStyles.Except( currentTypeAttributes[PropertyDataType.Color] ) );

                    // Add Float styles
                    if ( !currentTypeAttributes.ContainsKey( PropertyDataType.Float ) ) {
                        currentTypeAttributes[PropertyDataType.Float] = new List<string>();
                    }

                    List<string> floatStyles = new() {
                        "style.opacity",
                        "style.flexGrow",
                        "style.flexShrink",
                        "style.borderBottomWidth",
                        "style.borderLeftWidth",
                        "style.borderRightWidth",
                        "style.borderTopWidth",
                        "style.unitySliceScale",
                        "style.unityTextOutlineWidth",
                    };
                    currentTypeAttributes[PropertyDataType.Float].AddRange( floatStyles.Except( currentTypeAttributes[PropertyDataType.Float] ) );
                }
            }
        }

        List<string> GetValidAttributesForType(string elementTypeName, PropertyDataType propertyType) {
            List<string> attributes = new();

            // The map for elementTypeName should already contain all inherited properties.
            if ( m_dynamicAttributeMap.TryGetValue( elementTypeName, out Dictionary<PropertyDataType, List<string>> elementAttributes ) ) {
                if ( elementAttributes.TryGetValue( propertyType, out List<string> propertyList ) ) {
                    attributes.AddRange( propertyList );
                }

                // If the property type is Int, also consider Float attributes as they can often be assigned.
                if ( propertyType == PropertyDataType.Int && elementAttributes.TryGetValue( PropertyDataType.Float, out List<string> floatPropertyList ) ) {
                    attributes.AddRange( floatPropertyList );
                }
            }

            return attributes.Distinct().OrderBy( a => a ).ToList();
        }

        /*static readonly Dictionary<string, Dictionary<PropertyDataType, List<string>>> AttributeMap = new() {
            {
                "VisualElement", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "name", "tooltip" } },
                    { PropertyDataType.Bool, new List<string> { "visible", "enabledSelf" } },
                    { PropertyDataType.Color, new List<string> { "style.backgroundColor", "style.color", "style.borderTopColor", "style.borderRightColor", "style.borderBottomColor", "style.borderLeftColor" } },
                    { PropertyDataType.Float, new List<string> { "style.opacity", "style.flexGrow", "style.flexShrink" } },
                }
            }, {
                "Label", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "text" } },
                }
            }, {
                "Button", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "text" } },
                }
            }, {
                "TextField", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "label", "value" } },
                    { PropertyDataType.Bool, new List<string> { "isReadOnly", "isPasswordField" } },
                }
            }, {
                "Toggle", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "label", "text" } },
                    { PropertyDataType.Bool, new List<string> { "value" } },
                }
            }, {
                "ProgressBar", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "title" } },
                    { PropertyDataType.Float, new List<string> { "lowValue", "highValue", "value" } },
                }
            }, {
                "Slider", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "label" } },
                    { PropertyDataType.Float, new List<string> { "lowValue", "highValue", "value" } },
                }
            }, {
                "SliderInt", new Dictionary<PropertyDataType, List<string>> {
                    { PropertyDataType.String, new List<string> { "label" } },
                    { PropertyDataType.Int, new List<string> { "lowValue", "highValue", "value" } },
                }
            },
        };*/

        /*static List<string> GetValidAttributesForType(string elementTypeName, PropertyDataType propertyType) {
            List<string> attributes = new();

            // Add base VisualElement attributes first
            if ( AttributeMap.TryGetValue( "VisualElement", out Dictionary<PropertyDataType, List<string>> baseAttributes ) ) {
                if ( baseAttributes.TryGetValue( propertyType, out List<string> baseList ) ) {
                    attributes.AddRange( baseList );
                }

                // If the property type is Int, also consider Float attributes for VisualElement
                if ( propertyType == PropertyDataType.Int && baseAttributes.TryGetValue( PropertyDataType.Float, out List<string> floatBaseList ) ) {
                    attributes.AddRange( floatBaseList );
                }
            }

            // Add specific element attributes
            if ( elementTypeName != "VisualElement" && AttributeMap.TryGetValue( elementTypeName, out Dictionary<PropertyDataType, List<string>> specificAttributes ) ) {
                if ( specificAttributes.TryGetValue( propertyType, out List<string> specificList ) ) {
                    attributes.AddRange( specificList );
                }

                // If the property type is Int, also consider Float attributes for the specific element
                if ( propertyType == PropertyDataType.Int && specificAttributes.TryGetValue( PropertyDataType.Float, out List<string> floatSpecificList ) ) {
                    attributes.AddRange( floatSpecificList );
                }
            }

            return attributes.Distinct().OrderBy( a => a ).ToList();
        }*/
        #endregion
    }
}