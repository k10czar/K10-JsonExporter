using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using K10.EditorGUIExtention;
using UnityEditor;

public static class ExportFieldUtility
{
	[System.Flags]
	public enum ElementAction { None, Remove = 0b1, MoveUp = 0b10, MoveDown = 0b100 }

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

	public static string GetMemberValueSerialized( this ExportField export, int beginId = 0, int count = 0 )
	{
		var ro = export.RootObject;
		return GetMemberValueSerialized( ro, ro.GetType(), export, beginId, count );
	}

	public static int GetCount( this ExportField export )
	{
		var ro = export.RootObject;
		var obj = GetMemberObject( ro, ro.GetType(), export, out var memberName );
		switch( export.Serialization )
		{
			case EFieldSerializationType.ToArray:
				{
					int count = 0;
					if( obj is System.Collections.IEnumerable enu )
					{
						foreach( var o in enu ) count++;
					}
					return count;
				}
		}
		return 0;
	}

	public static string GetMemberValueSerialized( object rootObject, System.Type type, ExportField export, int beginId = 0, int count = 0 )
	{
		var obj = GetMemberObject( rootObject, type, export, out var memberName );
		if( obj == null ) return "null";
		// if( obj == null ) return $"null({memberName})";
		return Serialize( obj, export, beginId, count );
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

	public static string Serialize( object obj, ExportField export, int beginId/* = 0*/, int count/* = 0*/ )
	{
		switch( export.Serialization )
		{
			case EFieldSerializationType.ToJson: return string.Format( export.Format, JsonUtility.ToJson( obj ) );
			case EFieldSerializationType.ToString:
				{
					if( obj is bool b ) return b ? "true" : "false";
					if( obj is float f ) return f.ToString( System.Globalization.CultureInfo.InvariantCulture );
					if( obj is double d ) return d.ToString( System.Globalization.CultureInfo.InvariantCulture );
					return string.Format( export.Format, obj.ToString() );
				}
			case EFieldSerializationType.ToStringToLower: return string.Format( export.Format, obj.ToString().ToLower() );
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
						int id = -1;
						int endId = ( count > 0 ) ? ( beginId + count ) : int.MaxValue;
						foreach( var o in enu )
						{
							if (o is IExportIgnorable eI && eI.Ignore) continue;
							
							id++;
							if( id < beginId ) continue;
							else if( id >= endId ) break;
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
			case EFieldSerializationType.AsBitMaskToValues:
				{
					var SB = export.SB;
					SB.Clear();
					SB.Append( "[" );
					if( obj is IConvertible conv )
					{
						bool fisrt = true;
						var value = conv.ToInt32( null );
						int id = 0;
						while( value > 0 )
						{
							if( ( value % 2 ) == 1 )
							{
								if( !fisrt ) SB.Append( ", " );
								fisrt = false;
								SB.Append( id );
							}
							id++;
							value = value >> 1;
						}
						
						if( !fisrt ) SB.Append( " " );
					}
					SB.Append( "]" );
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
	public static ElementAction DrawElement( this ExportField export, SerializedProperty element, Rect rect, float lineHeight, System.Type type, object oRef = null, bool? forceFold = null, ElementAction validActions = 0 )
	{
		var returnFlag = 0;
		bool useSO = false;
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

		var firstLine = rect.GetLineTop( lineHeight );

		var canMoveUp = validActions.AsMaskContains( ElementAction.MoveUp );
		var canMoveDown = validActions.AsMaskContains( ElementAction.MoveDown );

		if( canMoveUp || canMoveDown )
		{
			var buttons = firstLine.GetColumnRight( firstLine.height / 2 );
			if( canMoveUp && IconButton.Draw( buttons.HorizontalSlice( 0, 2 ), "upTriangle", '▲' ) ) returnFlag.AsMaskWith( ElementAction.MoveUp );
			if( canMoveDown && IconButton.Draw( buttons.HorizontalSlice( 1, 2 ), "downTriangle", '▼' ) ) returnFlag.AsMaskWith( ElementAction.MoveDown );
		}

		bool canInpect = oRef != null;

		var inspect = element.FindPropertyRelative( "_inspect" );
		var inspectedElement = element.FindPropertyRelative( "_inspectedElement" );
		if( canInpect ) InspectionFields( oRef, serType, ref firstLine, inspect, inspectedElement );

		var selected = element.FindPropertyRelative( "_selected" );
		var fields = element.FindPropertyRelative( "_fields" );
		if( IsRecurscive( serType ) && fields.arraySize > 0 )
		{
			var edit = element.isExpanded;
			var newEdit = EditorGUI.Foldout( firstLine.RequestLeft( 6 ), element.isExpanded, "" );
			if( edit != newEdit )
			{
				element.isExpanded = newEdit;
				if( Input.GetKey( KeyCode.LeftControl ) || Input.GetKey( KeyCode.LeftShift ) ) forceFold = element.isExpanded;
			}
			if( forceFold.HasValue ) element.isExpanded = forceFold.Value;
		}
		firstLine = firstLine.CutLeft( 6 );

		selected.boolValue = EditorGUI.ToggleLeft( firstLine.RequestLeft( 16 ), "", selected.boolValue );
		firstLine = firstLine.CutLeft( 16 );

		var prop = memberPath.stringValue;
		var stack = prop.Split( '.' );
		var sLen = stack.Length;

		var firstRect = firstLine.VerticalSlice( 0, 5, 2 );

		var fieldNameRect = firstRect.VerticalSlice( 0, 2 );
		if( validActions.AsMaskContains( ElementAction.Remove ) )
		{
			fieldNameRect = fieldNameRect.CutRight( 16 );
			if( IconButton.Draw( fieldNameRect.RequestRight( 16 ).MoveRight( 16 ), "minus" ) ) returnFlag.AsMaskWith( ElementAction.Remove );
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

		bool needDefineBatchSize = useSO && ( newSerType == EFieldSerializationType.ToArray );
		if( needDefineBatchSize )
		{
			var batchRect = secondRect.RequestLeft( 56 );
			secondRect = secondRect.CutLeft( 56 );
			var batchSize = element.FindPropertyRelative( "_batchSize" );
			batchSize.intValue = Mathf.Max( EditorGUI.IntField( batchRect, batchSize.intValue ), 0 );
			EditorGUI.LabelField( batchRect, batchSize.intValue == 0 ? "(∞)batch" : "batch", K10GuiStyles.unitStyle );
		}

		if( useSO )
		{
			EditorGUI.ObjectField( secondRect, rootObject, typeof( ScriptableObject ), GUIContent.none );
		}

		if( IsRecurscive( serType ) && element.isExpanded )
		{
			rect = rect.CutLeft( lineHeight );
			var toRemove = new HashSet<int>();
			var iniQType = qType;
			var fCount = fields.arraySize;
			for( int i = 0; i < fCount; i++ )
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
				var acts = (int)ElementAction.Remove;
				if( i > 0 ) acts |= (int)ElementAction.MoveUp;
				if( i + 1 < fCount ) acts |= (int)ElementAction.MoveDown;
				var fl = export?.GetField( i ) ?? null;
				if( f != null )
				{
					var actions = fl.DrawElement( f, lineRect, lineHeight, qType, null, forceFold, (ElementAction)acts );
					if( actions.AsMaskContains( ElementAction.Remove ) ) toRemove.Add( i );
					if( actions.AsMaskContains( ElementAction.MoveDown ) ) fields.MoveArrayElement( i, i + 1 );
					if( actions.AsMaskContains( ElementAction.MoveUp ) ) fields.MoveArrayElement( i, i - 1 );
				}
			}

			foreach( var idToRemove in toRemove ) fields.DeleteArrayElementAtIndex( idToRemove );
		}

		if( canInpect && oRef != null )
		{
			try {
				export.InspectionBox( element, rect, type, oRef, serType, inspect.boolValue, inspectedElement );
			} catch { }
		}

		if( type == null ) GuiColorManager.Revert();

		return (ElementAction)returnFlag;
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
				var value = inspectedElement.intValue;
				value = EditorGUI.IntField( firstLine.GetColumnRight( 32, SPACING ), value );
				var buttons = firstLine.GetColumnRight( firstLine.height / 2, SPACING );
				bool canGoUp = value < ( count - 1 );
				EditorGUI.BeginDisabledGroup( !canGoUp );
				var up = IconButton.Draw( buttons.HorizontalSlice( 0, 2 ), "upTriangle", '▲', "", Color.white );
				EditorGUI.EndDisabledGroup();
				bool canGoDown = value > 0;
				EditorGUI.BeginDisabledGroup( !canGoDown );
				var down = IconButton.Draw( buttons.HorizontalSlice( 1, 2 ), "downTriangle", '▼', "", Color.white );
				EditorGUI.EndDisabledGroup();
				if( up ) value++;
				if( down ) value--;
				//value = Mathf.Clamp( value, 0, count - 1 );
				value = Mathf.Max(value, 0);
				inspectedElement.intValue = value;
			}
		}
	}

	private static void InspectionBox( this ExportField export, SerializedProperty element, Rect rect, Type type, object oRef, EFieldSerializationType serType, bool inspect, SerializedProperty inspectedElement )
	{
		if( inspect && oRef != null )
		{
			// EditorGUI.BeginDisabledGroup( true );
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
			// EditorGUI.EndDisabledGroup();
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
		if( element.isExpanded )
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
