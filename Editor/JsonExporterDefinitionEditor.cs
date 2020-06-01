using UnityEngine;
using UnityEditor;
using K10.EditorGUIExtention;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using SimpleJson2;

[CustomEditor(typeof(JsonExporterDefinition))]
public class JsonExporterDefinitionEditor : Editor
{
	public enum EExportStep { Idle, Ignored, Queue, Sent, Fail, Succeeded }

	KReorderableList _exportList;
	bool exporting = false;
	List<ExportationElement> _exportation = new List<ExportationElement>();
	private SerializedProperty _urlField;
	private Persistent<string> _author;

	static string AuthorPath => Application.persistentDataPath + "Temp/Author.name";

	void OnEnable()
	{
		_author = Persistent<string>.At( AuthorPath );
		_urlField = serializedObject.FindProperty( "_url" );
		var exportFields = serializedObject.FindProperty( "_exportFields" );
		_exportList = new KReorderableList( serializedObject, exportFields, "Configurations", IconCache.Get( "gear" ).Texture );

		var lineHeight = EditorGUIUtility.singleLineHeight;
		var exporter = target as JsonExporterDefinition;

		_exportList.List.elementHeightCallback = ( int index ) =>
		{
			var element = exportFields.GetArrayElementAtIndex( index );
			return ExportFieldUtility.CalculateHeight( element, lineHeight );
		};

		_exportList.List.drawElementCallback = ( Rect rect, int index, bool isActive, bool isFocused ) =>
		{
			var element = exportFields.GetArrayElementAtIndex( index );
			exporter.GetField( index ).DrawElement( element, rect.CutLeft( 12 ), lineHeight, null );
		};
	}

	private bool IsValidAuthorName( string name )
	{
		if( name == null ) return false;
		return name.Count( ( c ) => c != ' ' ) >= 3;
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		exporting = false;
		for( int i = 0; i < _exportation.Count && !exporting; i++ )
		{
			var ex = _exportation[i];
			exporting |= ex.Exporting;
		}

		GUILayout.BeginVertical( "HelpBox" );
		GUILayout.BeginVertical( "HelpBox" );
		GuiLabelWidthManager.New( 25 );
		EditorGUI.BeginDisabledGroup( exporting );
		var authorName = _author.Get;
		bool authorIsValid = IsValidAuthorName( authorName );
		if( !authorIsValid ) GuiColorManager.New( K10GuiStyles.RED_TINT_COLOR );
		GUILayout.BeginHorizontal( "HelpBox" );
		EditorGUILayout.LabelField( "Author", GUILayout.MaxWidth( 40 ) );
		authorName = EditorGUILayout.TextArea( authorName, GUILayout.MaxWidth( 100 ) );
		_author.Set = authorName;
		bool canSend = !exporting && IsValidAuthorName( authorName );
		EditorGUILayout.PropertyField( _urlField );
		GUILayout.EndHorizontal();
		if( !authorIsValid ) GuiColorManager.Revert();
		EditorGUI.EndDisabledGroup();
		GuiLabelWidthManager.Revert();
		GUILayout.BeginVertical( "HelpBox" );
		if( _exportation.Count > 0 )
		{
			for( int i = 0; i < _exportation.Count; i++ )
			{
				var color = _exportation[i].GetColor();
				if( color.HasValue ) GuiColorManager.New( color.Value );
				GUILayout.BeginHorizontal( "HelpBox" );
				if( color.HasValue ) GuiColorManager.Revert();
				GUILayout.BeginHorizontal( "HelpBox" );
				GUILayout.Label( _exportation[i].Name );
				GUILayout.EndHorizontal();
				if( color.HasValue ) GuiColorManager.New( color.Value );
				GUILayout.BeginHorizontal( "HelpBox" );
				GUILayout.Label( _exportation[i].Message );
				GUILayout.EndHorizontal();
				if( color.HasValue ) GuiColorManager.Revert();
				GUILayout.EndHorizontal(  );
			}
		}
		else
		{
			GUILayout.BeginHorizontal( "HelpBox" );
			GUILayout.Label( "No export data" );
			GUILayout.EndHorizontal();
		}
		GUILayout.EndVertical();
		GUILayout.EndVertical();
		EditorGUI.BeginDisabledGroup( !canSend );
		var send = GUILayout.Button( "Export", K10GuiStyles.bigbuttonStyle ) && canSend;
		EditorGUI.EndDisabledGroup();
		GUILayout.EndVertical();

		EditorGUI.BeginDisabledGroup( exporting );
		_exportList.DoLayoutList();
		EditorGUI.EndDisabledGroup();
		serializedObject.ApplyModifiedProperties();

		if( !exporting )
		{
			var exporter = target as JsonExporterDefinition;
			int count = exporter.FieldsCount;
			while( _exportation.Count < count ) _exportation.Add( new ExportationElement() );
			while( _exportation.Count > count ) _exportation.RemoveAt( _exportation.Count - 1 );
			for( int i = 0; i < count; i++ )
			{
				_exportation[i].Set( exporter.GetField( i ) );
			}

			if( send )
			{
				if( _exportation.Count > 0 )
				{
					var url = _urlField.stringValue;
					IEventTrigger sendNext = new CallOnce( SetDataDirty );
					for( int i = _exportation.Count - 1; i >= 0; i-- )
					{
						var exp = _exportation[i];
						if( exp.State == EExportStep.Ignored ) continue;
						var field = exporter.GetField( i );
						var fieldData = $"{{ \"tableName\": \"{field.FieldName}\", \"data\": {field.GetMemberValueSerialized()} }}";
						var nextTrigger = sendNext;
						exp.SetState( EExportStep.Queue );
						sendNext = new CallOnce( () =>
						{
							exp.Trigger( url, fieldData, nextTrigger, authorName );
							SetDataDirty();
						} );
					}

					sendNext.Trigger();
				}
			}
		}
	}

	void SetDataDirty()
	{
		EditorUtility.SetDirty( this );
		EditorUtility.SetDirty( target );
	}

	public class ExportationElement
	{
		string _name;
		string _data;
		string _errorMessage;
		string _errorCode;
		EExportStep _state;
		UnityWebRequest _request;
		UnityWebRequestAsyncOperation _asyncOp;

		public string Name => _name;
		public EExportStep State => _state;
		public bool Exporting => _state == EExportStep.Queue || _state == EExportStep.Sent;

		public string Message
		{
			get
			{
				var stateStr = _state.ToString();
				if( _state != EExportStep.Succeeded && _state != EExportStep.Fail ) return stateStr;
				if( string.IsNullOrWhiteSpace( _errorMessage ) ) return stateStr;
				return $"{stateStr}: {_errorMessage}";
			}
		}

		public void SetState( EExportStep state )
		{
			_state = state;
		}

		public void Trigger( string url, string data, IEventTrigger onComplete, string authorName )
		{
			_errorMessage = null;
			_errorCode = null;
			WWWForm form = new WWWForm();
			var realData = data;
			form.AddField( "updateTable", realData );
			if( authorName != null ) form.AddField( "author", authorName );
			var source = SystemInfo.deviceName;
			form.AddField( "source", source );

			Debug.Log( $"Post to {url}\nwith data:\n{realData.FormatAsJson( "    " )}" );
			realData.FormatAsJson().LogToJsonFile( "export", _name );

			UnityWebRequest www = UnityWebRequest.Post( url, form );
			_asyncOp = www.SendWebRequest();
			_state = EExportStep.Sent;
			_asyncOp.completed += ( aOp ) =>
			{
				_state = EExportStep.Fail;
				if( www.isNetworkError || www.isHttpError )
				{
					var error = $"{( www.isNetworkError ? "isNetworkError " : "" )}{( www.isHttpError ? "isHttpError" : "" )}({www.error})";
					Debug.Log( "!!Error!! " + error );
					_errorMessage = error;
					_errorCode = www.error;
				}
				else
				{
					var dh = www.downloadHandler;
					Debug.Log( "dh.text:\n" + dh.text );
					SimpleJson2.SimpleJson2.TryDeserializeObject( dh.text, out var jObj );
					if( jObj is JsonObject jo )
					{
						var hasErrors = jo.TryGetValue( "errors", out var errors );
						if( hasErrors )
						{
							Debug.Log( "Errors:\n" + errors.ToStringOrNull().FormatAsJson( "    " ) );
							if( errors is JsonObject ejo )
							{
								ejo.TryGetValue( "errorMessage", out var errorMessage );
								ejo.TryGetValue( "errorCode", out var errorCode );
								_errorMessage = errorMessage as string;
								_errorCode = errorCode as string;
							}
						}
						else
						{
							var hasResponse = jo.TryGetValue( "response", out var response );
							var hasData = jo.TryGetValue( "newData", out var dataBack );
							if( hasResponse ) _state = EExportStep.Succeeded;
							Debug.Log( $"Succeeded! {hasResponse} \n{response.ToStringOrNull().FormatAsJson( "    " )}" );
							Debug.Log( $"hasData? {hasData} \n{dataBack.ToStringOrNull().FormatAsJson( "    " )}" );
						}
					}
					else
					{
						Debug.Log( "Malformed response\n" + dh.text );
					}
				}

				onComplete.Trigger();
				www.Dispose();
			};
		}

		public Color? GetColor()
		{
			switch( _state )
			{
				case EExportStep.Idle: return K10GuiStyles.GREY_TINT_COLOR;
				case EExportStep.Ignored: return K10GuiStyles.DARKER_TINT_COLOR;
				case EExportStep.Queue: return K10GuiStyles.CYAN_TINT_COLOR;
				case EExportStep.Sent: return K10GuiStyles.YELLOW_TINT_COLOR;
				case EExportStep.Fail: return K10GuiStyles.RED_TINT_COLOR;
				case EExportStep.Succeeded: return K10GuiStyles.GREEN_TINT_COLOR;
			}
			return null;
		}

		public void Set( ExportField exportField )
		{
			_name = exportField.FieldName;
			if( !exportField.Selected ) _state = EExportStep.Ignored;
			else if( _state == EExportStep.Ignored ) _state = EExportStep.Idle;
		}
	}
}