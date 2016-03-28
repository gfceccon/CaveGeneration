using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomPropertyDrawer(typeof(Matrix))]
public class MatrixProperty : PropertyDrawer
{
    const float rowHeight = 18f;
    const float rowOffset = 4f;
    const float elementOffset = 8f;
    const bool odd = true;

    GUIContent sizeContent = new GUIContent("Size");
    GUIStyle labelStyle = new GUIStyle();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        labelStyle.fontStyle = FontStyle.Bold;
        EditorGUI.PrefixLabel(position, label, labelStyle);

        Rect elementPosition = position;
        elementPosition.y += (rowHeight + rowOffset) * 2f;

        SerializedProperty sizeProperty = property.FindPropertyRelative("size");
        Rect sizePosition = position;
        sizePosition.y += rowHeight;
        sizePosition.height = rowHeight;
        sizePosition.width = position.width / 2f - elementOffset;

        EditorGUI.LabelField(sizePosition, sizeContent);

        sizePosition.x += sizePosition.width + elementOffset;
        EditorGUI.PropertyField(sizePosition, sizeProperty, GUIContent.none);

        int size = sizeProperty.intValue;
        if (size < 1)
            size = 1;
        if ((size % 2 == 0 && odd) || (size % 2 == 1 && !odd))
            size++;
        sizeProperty.intValue = size;

        SerializedProperty data = property.FindPropertyRelative("rows");
        if (data.arraySize != size)
            data.arraySize = size;
        for (int j = 0; j < size; j++)
        {
            SerializedProperty elements = data.GetArrayElementAtIndex(j).FindPropertyRelative("elements");
            if (elements.arraySize != size)
                elements.arraySize = size;

            elementPosition.height = rowHeight;
            elementPosition.width = position.width / size - elementOffset;

            for (int i = 0; i < size; i++)
            {
                EditorGUI.PropertyField(elementPosition, elements.GetArrayElementAtIndex(i), GUIContent.none);
                elementPosition.x += elementPosition.width + elementOffset;
            }

            elementPosition.x = position.x;
            elementPosition.y += rowHeight + rowOffset;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int size = property.FindPropertyRelative("size").intValue;
        return (rowHeight + rowOffset) * (size + 2);
    }
}
