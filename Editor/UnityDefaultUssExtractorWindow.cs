using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class UnityDefaultUssExtractorWindow : EditorWindow {
    VisualTreeAsset m_sourceUxmlAsset;

    [MenuItem( "Tools/UI Toolkit/USS Generator from UXML" )]
    public static void ShowWindow() {
        GetWindow<UnityDefaultUssExtractorWindow>( "USS Generator" );
    }

    private void OnGUI() {
        GUILayout.Label( "UXML to USS Class Extractor", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox( "Select a UXML file to parse. A new USS file will be generated next to it, containing all the unique style classes found in the UXML.", MessageType.Info );

        m_sourceUxmlAsset = (VisualTreeAsset)EditorGUILayout.ObjectField(
            "Source UXML File",
            m_sourceUxmlAsset,
            typeof(VisualTreeAsset),
            false
        );

        EditorGUI.BeginDisabledGroup( !m_sourceUxmlAsset );

        if ( GUILayout.Button( "Generate USS File" ) ) {
            GenerateUssFileFromUxml();
        }

        EditorGUI.EndDisabledGroup();
    }

    private void GenerateUssFileFromUxml() {
        if ( !m_sourceUxmlAsset ) {
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath( m_sourceUxmlAsset );
        VisualElement rootElement = m_sourceUxmlAsset.CloneTree();
        HashSet<string> classNames = new();
        List<VisualElement> allElements = rootElement.Query<VisualElement>().ToList();

        foreach (var element in allElements) {
            foreach (string className in element.GetClasses()) {
                classNames.Add( className );
            }
        }

        if ( classNames.Count == 0 ) {
            EditorUtility.DisplayDialog( "No Classes Found", $"The selected UXML file '{m_sourceUxmlAsset.name}.uxml' does not contain any style classes.", "OK" );
            return;
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine( $"/* Generated from: {m_sourceUxmlAsset.name}.uxml */" );
        stringBuilder.AppendLine( $"/* Generated on: {System.DateTime.Now} */" );
        stringBuilder.AppendLine();

        List<string> sortedClassNames = classNames.ToList();
        sortedClassNames.Sort();
        foreach (string className in sortedClassNames) {
            stringBuilder.AppendLine( $".{className} {{" );
            stringBuilder.AppendLine( "}" );
            stringBuilder.AppendLine();
        }

        string outputPath = Path.ChangeExtension( sourcePath, ".uss" );

        try {
            File.WriteAllText( outputPath, stringBuilder.ToString() );
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog( "USS Generation Successful", $"USS file was created successfully at:\n{outputPath}", "OK" );
        }
        catch (System.Exception e) {
            EditorUtility.DisplayDialog( "USS Generation Failed", $"Could not write USS file. See console for error details.", "OK" );
        }
    }
}