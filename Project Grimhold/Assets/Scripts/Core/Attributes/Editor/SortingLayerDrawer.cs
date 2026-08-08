#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ProjectGrimhold.Core.Attributes.Editor
{
    [CustomPropertyDrawer(typeof(SortingLayerAttribute))]
    public class SortingLayerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer && property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "SortingLayer requires an integer or string property.", MessageType.Error);
                return;
            }

            var sortingLayers = SortingLayer.layers;
            var layerNames = sortingLayers.Select(l => l.name).ToArray();

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int currentLayerID = property.intValue;
                int currentIndex = -1;
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    if (sortingLayers[i].id == currentLayerID)
                    {
                        currentIndex = i;
                        break;
                    }
                }
                
                if (currentIndex == -1) currentIndex = 0;

                int newIndex = EditorGUI.Popup(position, label.text, currentIndex, layerNames);
                if (newIndex != currentIndex)
                {
                    property.intValue = sortingLayers[newIndex].id;
                }
            }
            else // String
            {
                string currentName = property.stringValue;
                int currentIndex = System.Array.IndexOf(layerNames, currentName);
                
                if (currentIndex == -1) currentIndex = 0;

                int newIndex = EditorGUI.Popup(position, label.text, currentIndex, layerNames);
                if (newIndex != currentIndex)
                {
                    property.stringValue = layerNames[newIndex];
                }
            }
        }
    }
}
#endif
