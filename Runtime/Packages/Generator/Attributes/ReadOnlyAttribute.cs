using UnityEngine;
namespace TCS.UiToolkitUtils.Attributes {
    public class ReadOnlyAttribute : PropertyAttribute { }


#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer( typeof(ReadOnlyAttribute) )]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label) {
            bool previousGUIState = GUI.enabled;
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField( position, property, label );
            GUI.enabled = previousGUIState;
        }
    }
#endif
}
