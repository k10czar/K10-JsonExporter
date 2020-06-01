using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using K10.EditorGUIExtention;
using UnityEditor;

public static class ExportFieldUtility
{
	const float SPACING = 2;
	public const BindingFlags BindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	public static string GetFieldDefinition( object rootObject, System.Type type, ExportField export )
	{
		return "\"" + export.FieldName + "\":" + GetMemberValueSerialized( rootObject, type, export );
	}

	public static string GetFieldDefinition( this ExportField export )
	{
		var ro = export.RootObject;
		return GetFieldDefinition( ro, ro.GetType(), export );
	}


	public static string GetMemberValueSerialized( this ExportField export )
	{
		var ro = export.RootObject;
		return GetMemberValueSerialized( ro, ro.GetType(), export );
	}

	public static string GetMemberValueSerialized( object rootObject, System.Type type, ExportField export )
	{
		var obj = GetMemberObject( rootObject, type, export, out var memberName );
		if( obj == null ) return $"null({memberName})";
		return Serialize( obj, export );
	}

	public static object GetMemberObject( object rootObject, System.Type type, ExportField export, out string lastMemberName )
	{
		lastMemberName = "";
		var obj = rootObject;
		var stack = export.MemberPath.Split( '.' );
		var sLen = stack.Length;
		for( int i = 0; i < sLen; i++ )
		{
			lastMemberName = stack[i];
			obj = GetMember( lastMemberName, obj, ref type );
			if( obj == null ) return null;
		}
		return obj;
	}

	public static string Serialize( object obj, ExportField export )
	{
		switch( export.Serialization )
		{
			case EFieldSerializationType.ToJson: return string.Format( export.Format, JsonUtility.ToJson( obj ) );
			case EFieldSerializationType.ToString:
				{
					if( obj is bool b ) return b ? "true" : "false";
					return string.Format( export.Format, obj.ToString() );
				}
			case EFieldSerializationType.BoolToNumber:
				{
					if( obj is bool b ) return b ? "1" : "0";
					return string.Format( export.Format, obj.ToString() );
				}
			case EFieldSerializationType.InvertedBool:
				{
					if( obj is bool b ) return b ? "false" : "true";
					return string.Format( export.Format, obj.ToString() );
				}				
			case EFieldSerializationType.ToArray:
				{
					var SB = export.SB;
					SB.Clear();
					SB.Append( "[" );
					bool fisrt = true;
					if( obj is System.Collections.IEnumerable enu )
					{
						bool isDirectValue = export.CheckIfIsDirectValue();
						foreach( var o in enu )
						{
							if( !fisrt ) SB.Append( ", " );
							SB.Append( " " );
							fisrt = false;
							AppendFields( export, o, isDirectValue );
						}
					}
					if( !fisrt ) SB.Append( " " );
					SB.Append( "]" );
					return SB.ToString();
				}
			case EFieldSerializationType.Inherited:
				{
					var SB = export.SB;
					SB.Clear();
					AppendFields( export, obj );
					return SB.ToString();
				}
		}
		return "null";
	}

	static void AppendFields( ExportField export, object o, bool isDirectValue = false )
	{
		var SB = export.SB;
		if( !isDirectValue ) SB.Append( "{" );
		bool first = true;
		for( int i = 0; i < export.FieldsCount; i++ )
		{
			var f = export.GetField( i );
			if( !f.Selected ) continue;
			if( !isDirectValue )
			{
				if( first ) SB.Append( " " );
				else SB.Append( ", " );
				SB.Append( $"\"{f.FieldName}\":" );
				first = false;
			}
			if( o != null )
			{
				SB.Append( GetMemberValueSerialized( o, o.GetType(), f ) );
			}
			else SB.Append( "null" );
		}
		if( !first ) SB.Append( " " );
		if( !isDirectValue ) SB.Append( "}" );
	}

	public static object GetMember( string memberName, object element, ref System.Type type )
	{
		if( element == null ) return element;
		var field = type.GetField( memberName, BindingAttr );
		if( field != null )
		{
			type = field.FieldType;
			object ret = null;
			try { ret = field.GetValue( element ); }
			catch { }
			return ret;
			//return field.GetValue( element );
		}
		var property = type.GetProperty( memberName, BindingAttr );
		if( property != null )
		{
			type = property.PropertyType;
			object ret = null;
			try { ret = property.GetValue( element ); }
			catch { }
			return ret;
			// return property.GetValue( element );
		}
		type = type.BaseType;
		if( type.BaseType != null ) return GetMember( memberName, element, ref type );
		return element;
	}

	public static void GetMembers( System.Type type, ref List<MemberInfo> list )
	{
		if( type.BaseType != null && type.BaseType != typeof( ScriptableObject ) ) GetMembers( type.BaseType, ref list );
		list.AddRange( type.GetFields( BindingAttr ) );
		list.AddRange( type.GetProperties( BindingAttr ) );
	}

	public static bool IsRecurscive( EFieldSerializationType ser ) => ser == EFieldSerializationType.ToArray || ser == EFieldSerializationType.Inherited;
	public static bool DrawElement( this ExportField export, SerializedProperty element, Rect rect, float lineHeight, System.Type type, object oRef = null, bool? forceFold = null )
	{
		bool useSO = false;
		bool remove = false;
		var rootObject = element.FindPropertyRelative( "_rootObject" );
		var fieldName = element.FindPropertyRelative( "_fieldName" );

		if( type == null )
		{
			oRef = rootObject.objectReferenceValue;
			if( oRef != null ) type = oRef.GetType();
			useSO = true;
		}

		if( type == null ) GuiColorManager.New( K10GuiStyles.RED_TINT_COLOR );

		var memberPath = element.FindPropertyRelative( "_memberPath" );
		var serialization = element.FindPropertyRelative( "_serialization" );
		var serType = (EFieldSerializationType)serialization.enumValueIndex;
		var format = element.FindPropertyRelative( "_format" );

		var firstLine = rect.RequestTop( lineHeight );
		rect = rect.CutTop( lineHeight );

		bool canInpect = oRef != null;

		var inspect = element.FindPropertyRelative( "_inspect" );
		var inspectedElement = element.FindPropertyRelative( "_inspectedElement" );
		if( canInpect ) InspectionFields( oRef, serType, ref firstLine, inspect, inspectedElement );

		var editMode = element.FindPropertyRelative( "_editMode" );
		var selected = element.FindPropertyRelative( "_selected" );
		var fields = element.FindPropertyRelative( "_fields" );
		if( IsRecurscive( serType ) && fields.arraySize > 0 )
		{
			var edit = editMode.boolValue;
			var newEdit = EditorGUI.Foldout( firstLine.RequestLeft( 6 ), editMode.boolValue, "" );
			if( edit != newEdit )
			{
				editMode.boolValue = newEdit;
				if( Input.GetKey( KeyCode.LeftControl ) || Input.GetKey( KeyCode.LeftShift ) ) forceFold = editMode.boolValue;
			}
			if( forceFold.HasValue ) editMode.boolValue = forceFold.Value;
		}
		firstLine = firstLine.CutLeft( 6 );

		selected.boolValue = EditorGUI.ToggleLeft( firstLine.RequestLeft( 16 ), "", selected.boolValue );
		firstLine = firstLine.CutLeft( 16 );

		var prop = memberPath.stringValue;
		var stack = prop.Split( '.' );
		var sLen = stack.Length;

		var firstRect = firstLine.VerticalSlice( 0, 5, 2 );

		var fieldNameRect = firstRect.VerticalSlice( 0, 2 );
		if( !useSO )
		{
			fieldNameRect = fieldNameRect.CutRight( 16 );
			remove = IconButton.Draw( fieldNameRect.RequestRight( 16 ).MoveRight( 16 ), "minus" );
		}
		fieldName.stringValue = EditorGUI.TextField( fieldNameRect, fieldName.stringValue );

		var serializationRect = firstRect.VerticalSlice( 1, 2 );
		if( IsRecurscive( serType ) )
		{
			var add = IconButton.Draw( serializationRect.RequestRight( 16 ), "add" );
			serializationRect = serializationRect.CutRight( 16 );
			if( add ) fields.InsertArrayElementAtIndex( fields.arraySize );
		}
		var newSerType = (EFieldSerializationType)EditorGUI.EnumPopup( serializationRect, serType );
		serialization.enumValueIndex = (int)newSerType;

		var isInherited = ( newSerType == EFieldSerializationType.Inherited );
		var qType = type;

		var secondRect = firstLine.VerticalSlice( 2, 5, 3 );
		if( /*!isInherited &&*/ !useSO )
		{
			var elements = sLen + 1;
			if( !isInherited ) elements++;
			int elementId = 0;
			if( !isInherited ) {
				format.stringValue = EditorGUI.TextField( secondRect.VerticalSlice( elementId++, elements ), format.stringValue );
				if( string.IsNullOrEmpty( format.stringValue ) ) format.stringValue = "{0}";
			}

			var newProp = "";
			var newDebugProp = "";

			for( int i = 0; i < sLen; i++ )
			{
				var p = stack[i];
				var newP = MemberSelectionField( p, secondRect.VerticalSlice( elementId++, elements ), ref qType );
				newDebugProp += "." + qType.ToStringOrNull();
				if( !string.IsNullOrWhiteSpace( newP ) && newP != "-" )
				{
					if( !string.IsNullOrWhiteSpace( newProp ) ) newProp += ".";
					newProp += newP;
					if( p != newP ) break;
				}
			}

			var lastP = MemberSelectionField( "-", secondRect.VerticalSlice( elementId++, elements ), ref qType );
			if( !string.IsNullOrWhiteSpace( lastP ) && lastP != "-" )
			{
				if( !string.IsNullOrWhiteSpace( newProp ) ) newProp += ".";
				newProp += lastP;
			}

			memberPath.stringValue = newProp;
		}

		if( useSO )
		{
			EditorGUI.ObjectField( secondRect, rootObject, typeof( ScriptableObject ), GUIContent.none );
		}

		if( IsRecurscive( serType ) && editMode.boolValue )
		{
			rect = rect.CutLeft( lineHeight );
			var toRemove = new HashSet<int>();
			var iniQType = qType;
			for( int i = 0; i < fields.arraySize; i++ )
			{
				qType = iniQType;
				var f = fields.GetArrayElementAtIndex( i );
				var height = CalculateHeight( f, lineHeight, false );
				var lineRect = rect.GetLineTop( height, 0 );
				if( serType == EFieldSerializationType.ToArray && qType != null )
				{
					var bt = qType;
					System.Type elementType = null;
					do
					{
						if( bt.IsGenericType )
						{
							elementType = bt.GetGenericArguments().Single();
							
						}
						else if( bt.IsArray )
						{
							elementType = bt.GetElementType();
						}
						bt = bt.BaseType;
					} while( elementType == null && bt != null );
					qType = elementType;
				}
				if( i < export.FieldsCount && i >= 0 )
				{
					var removeInner = export.GetField(i).DrawElement( f, lineRect, lineHeight, qType, null, forceFold );
					if( removeInner ) toRemove.Add( i );
				}
			}

			foreach( var id in toRemove ) fields.DeleteArrayElementAtIndex( id );
		}

		if( canInpect && oRef != null )
		{
			try {
				export.InspectionBox( element, rect, type, oRef, serType, inspect.boolValue, inspectedElement );
			} catch { }
		}

		if( type == null ) GuiColorManager.Revert();

		return remove;
	}

	private static void InspectionFields( object oRef, EFieldSerializationType serType, ref Rect firstLine, SerializedProperty inspect, SerializedProperty inspectedElement )
	{
		var inspectChange = IconButton.Draw( firstLine.GetColumnRight( 16, SPACING ), inspect.boolValue ? "spy" : "visibleOff" );
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
				inspectedElement.intValue = EditorGUI.IntField( firstLine.GetColumnRight( 32, SPACING ), inspectedElement.intValue );
				inspectedElement.intValue = Mathf.Min( inspectedElement.intValue, count - 1 );
			}
		}
	}

	private static void InspectionBox( this ExportField export, SerializedProperty element, Rect rect, Type type, object oRef, EFieldSerializationType serType, bool inspect, SerializedProperty inspectedElement )
	{
		if( inspect && oRef != null )
		{
			var inspection = element.FindPropertyRelative( "_inspection" );
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
					AppendFields( export, elementObj, export.CheckIfIsDirectValue() );
					inspection.stringValue = SB.ToString().FormatAsJson( "    " );
					SB.Clear();
				}
				else inspection.stringValue = "null";
			}
			else inspection.stringValue = GetFieldDefinition( oRef, type, export ).FormatAsJson( "    " );
			EditorGUI.TextArea( rect, inspection.stringValue );
		}
	}

	public static float CalculateHeight( SerializedProperty element, float lineHeight, bool canInspect = true )
	{
		var size = lineHeight;
		var inspect = element.FindPropertyRelative( "_inspect" );
		if( inspect.boolValue && canInspect )
		{
			var inspection = element.FindPropertyRelative( "_inspection" );
			size += ( lineHeight - 2.5f ) * ( inspection.stringValue.Count( ( c ) => c == '\n' ) + 1 );
		}
		var editMode = element.FindPropertyRelative( "_editMode" );
		if( editMode.boolValue )
		{
			var serialization = element.FindPropertyRelative( "_serialization" );
			if( IsRecurscive( (EFieldSerializationType)serialization.enumValueIndex ) )
			{
				var fields = element.FindPropertyRelative( "_fields" );
				for( int i = 0; i < fields.arraySize; i++ ) size += CalculateHeight( fields.GetArrayElementAtIndex( i ), lineHeight, false );
			}
		}
		return size;
	}

	public static string MemberSelectionField( string current, Rect rect, ref System.Type type )
	{
		var list = new List<MemberInfo>();
		GetMembers( type, ref list );
		var names = list.ConvertAll( ( m ) => m.Name );
		names.Insert( 0, "-" );
		var id = names.IndexOf( current );
		var newId = EditorGUI.Popup( rect, id, names.ToArray() );

		if( newId == 0 ) return "";
		if( newId <= 0 ) return current;

		var element = list[newId - 1];
		if( element is FieldInfo fi ) type = fi.FieldType;
		if( element is PropertyInfo pi ) type = pi.PropertyType;

		return names[newId];
	}
}
