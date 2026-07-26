using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Free chat fields (successKeyword/answerKey/hint) only matter when startsFreeChat is
// ticked, so they're hidden until then — keeps the per-line Inspector from listing fields
// that don't apply to that line.
[CustomPropertyDrawer(typeof(DialogueLine))]
public class DialogueLineDrawer : PropertyDrawer
{
    const float Gap = 2f;
    const float SectionGap = 8f;

    struct Row
    {
        public string field;
        public float gapBefore;
        public int indent;
        public Row(string field, float gapBefore, int indent = 0)
        {
            this.field = field;
            this.gapBefore = gapBefore;
            this.indent = indent;
        }
    }

    IEnumerable<Row> BuildRows(SerializedProperty property)
    {
        yield return new Row("speakerName", 0f);
        yield return new Row("dialogueText", Gap);
        yield return new Row("npcAnimationTrigger", Gap);

        yield return new Row("startsFreeChat", SectionGap);
        if (property.FindPropertyRelative("startsFreeChat").boolValue)
        {
            yield return new Row("freeChatSuccessKeyword", Gap, 1);
            yield return new Row("freeChatAnswerKey", Gap, 1);
            yield return new Row("freeChatHint", Gap, 1);
        }

        yield return new Row("requiresInteraction", SectionGap);

        yield return new Row("taskTextPlayer1", SectionGap);
        yield return new Row("taskTextPlayer2", Gap);

        yield return new Row("objectsToShow", SectionGap);
        yield return new Row("objectsToHide", Gap);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(headerRect, label, EditorStyles.boldLabel);
        float y = headerRect.y + headerRect.height;

        int baseIndent = EditorGUI.indentLevel;
        foreach (var row in BuildRows(property))
        {
            var field = property.FindPropertyRelative(row.field);
            float h = EditorGUI.GetPropertyHeight(field, true);
            y += row.gapBefore;

            EditorGUI.indentLevel = baseIndent + row.indent;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), field, true);
            EditorGUI.indentLevel = baseIndent;

            y += h;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        foreach (var row in BuildRows(property))
        {
            h += row.gapBefore;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(row.field), true);
        }
        return h;
    }
}
