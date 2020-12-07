using UnityEngine;
using UnityEditor;

[CustomEditor( typeof( JsonFieldDefinition ) )]
public class JsonFieldDefinitionEditor : Editor
{
	private SerializedProperty _definition;

	void OnEnable()
	{
		_definition = serializedObject.FindProperty( "_definition" );
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		GUILayout.BeginVertical( GUI.skin.box );
		var data = target as JsonFieldDefinition;
		float lineHeight = EditorGUIUtility.singleLineHeight;
		var rect = EditorGUILayout.GetControlRect( true, ExportFieldUtility.CalculateHeight( _definition, lineHeight ) );
		data.Definition.DrawElement( _definition, rect, lineHeight, null );
		GUILayout.EndVertical();

		serializedObject.ApplyModifiedProperties();
	}
}
