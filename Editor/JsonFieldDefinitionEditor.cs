using UnityEngine;
using UnityEditor;
using System;
using K10.EditorGUIExtention;

[CustomEditor( typeof( JsonFieldDefinition ) )]
public class JsonFieldDefinitionEditor : Editor
{
	private SerializedProperty _inspect;
	private SerializedProperty _definition;
	private SerializedProperty _inspectedElement;

	void OnEnable()
	{
		_inspect = serializedObject.FindProperty( "_inspect" );
		_definition = serializedObject.FindProperty( "_definition" );
		_inspectedElement = serializedObject.FindProperty( "_inspectedElement" );
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		GUILayout.BeginVertical( GUI.skin.box );
		var data = target as JsonFieldDefinition;
		float lineHeight = EditorGUIUtility.singleLineHeight;
		var rect = EditorGUILayout.GetControlRect( true, ExportFieldUtility.CalculateHeight( _definition, lineHeight ) );
		data.Definition.DrawElement( _definition, rect, lineHeight, null );

		var serialization = _definition.FindPropertyRelative( "_serialization" );
		var serType = (EFieldSerializationType)serialization.enumValueIndex;
		GUILayout.BeginHorizontal( GUI.skin.box );

		var oRef = data.Definition.RootObject;
		bool canInpect = oRef != null;
		if( canInpect ) InspectionFields( oRef, serType, _inspect, _inspectedElement );

		// var rootObject = _definition.FindPropertyRelative( "_rootObject" );
		// var oRef = rootObject.objectReferenceValue;
		var inspectionText = "";

		if( _inspectedElement.boolValue && oRef != null )
		{
			try
			{
				var type = oRef.GetType();
				inspectionText = GetInspectionText( data.Definition, type, oRef, serType, _inspectedElement );
			}
			catch { }
		}

		EditorGUILayout.TextArea( inspectionText );
		GUILayout.EndHorizontal();

		GUILayout.EndVertical();

		serializedObject.ApplyModifiedProperties();
	}

	private static void InspectionFields( object oRef, EFieldSerializationType serType, SerializedProperty inspect, SerializedProperty inspectedElement )
	{
		var inspectChange = IconButton.Layout( 16, inspect.boolValue ? "spy" : "visibleOff" );
		if( inspectChange )
		{
			inspect.boolValue = !inspect.boolValue;
		}

		if( inspect.boolValue )
		{
			var count = 0;
			if( serType == EFieldSerializationType.ToArray )
			{
				var enu = oRef as System.Collections.IEnumerable;
				if( enu != null ) foreach( var o in enu ) count++;
			}

			if( count > 0 )
			{
				var value = inspectedElement.intValue;
				value = EditorGUILayout.IntField( value, GUILayout.Width( 32 ) );
				bool canGoUp = value < ( count - 1 );
				EditorGUI.BeginDisabledGroup( !canGoUp );
				var up = IconButton.Layout( "upTriangle", 16, '▲', "", Color.white );
				EditorGUI.EndDisabledGroup();
				bool canGoDown = value > 0;
				EditorGUI.BeginDisabledGroup( !canGoDown );
				var down = IconButton.Layout( "downTriangle", 16, '▼', "", Color.white );
				EditorGUI.EndDisabledGroup();
				if( up ) value++;
				if( down ) value--;
				//value = Mathf.Clamp( value, 0, count - 1 );
				value = Mathf.Max( value, 0 );
				inspectedElement.intValue = value;
			}
		}
	}

	public static string GetInspectionText( ExportField export, Type type, object oRef, EFieldSerializationType serType, SerializedProperty inspectedElement )
	{
		if( serType == EFieldSerializationType.ToArray )
		{
			object elementObj = null;
			var id = inspectedElement.intValue;
			if( oRef is IHashedSOCollection hcol ) elementObj = hcol.GetElementBase( id );
			else if( oRef is System.Collections.IList list ) elementObj = list[id];
			else if( oRef is System.Array array ) elementObj = array.GetValue( id );
			if( elementObj != null )
			{
				var SB = export.SB;
				SB.Clear();
				SB.Append( $"{export.FieldName}[{id}]: " );
				export.AppendFields( elementObj, export.CheckIfIsDirectValue() );
				var str = SB.ToString().FormatAsJson( "    " );
				SB.Clear();
				return str;
			}
			return "null";
		}
		return ExportFieldUtility.GetFieldDefinition( oRef, type, export ).FormatAsJson( "    " );
	}
}
