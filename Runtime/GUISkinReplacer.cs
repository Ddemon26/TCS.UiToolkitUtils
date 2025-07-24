using UnityEditor;
    using UnityEngine;
    using System.Linq;
    
    public class GUISkinReplacer : EditorWindow
    {
        public GUISkin customSkin;
    
        [MenuItem("Tools/GUISkin Replacer")]
        static void ShowWindow()
        {
            GetWindow<GUISkinReplacer>("GUISkin Replacer");
        }
    
        private void OnGUI()
        {
            customSkin = EditorGUILayout.ObjectField("Custom Skin", customSkin, typeof(GUISkin), false) as GUISkin;
    
            if (GUILayout.Button("Apply to DarkSkin"))
            {
                if (customSkin != null)
                {
                    InjectSkin();
                }
                else
                {
                    Debug.LogWarning("No custom skin selected.");
                }
            }
        }
    
        void InjectSkin()
        {
            var darkSkin = Resources.FindObjectsOfTypeAll<GUISkin>().FirstOrDefault(s => s.name == "DarkSkin");
    
            if (darkSkin != null)
            {
                EditorUtility.CopySerialized(customSkin, darkSkin);
                Debug.Log($"Applied '{customSkin.name}' to 'DarkSkin'.");
                
            }
            else
            {
                Debug.LogWarning("Could not find 'DarkSkin'.");
            }
        }
    }