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
	private SerializedProperty _exportFields;
	private Persistent<string> _author;

	static string AuthorPath => "/Temp/Author.name";

	void OnEnable()
	{
		_author = Persistent<string>.At( AuthorPath );
		_urlField = serializedObject.FindProperty( "_url" );
		_exportFields = serializedObject.FindProperty( "_exportFields" );
		_exportList = new KReorderableList( serializedObject, _exportFields, "Configurations", IconCache.Get( "gear" ).Texture );

		var lineHeight = EditorGUIUtility.singleLineHeight;
		var exporter = target as JsonExporterDefinition;

		_exportList.List.elementHeightCallback = ( int index ) =>
		{
			var element = _exportFields.GetArrayElementAtIndex( index );
			return ExportFieldUtility.CalculateHeight( element, lineHeight );
		};

		_exportList.List.drawElementCallback = ( Rect rect, int index, bool isActive, bool isFocused ) =>
		{
			var element = _exportFields.GetArrayElementAtIndex( index );
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
		GUILayout.BeginVertical( "HelpBox" );
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField( "Author", GUILayout.MaxWidth( 40 ) );
		authorName = EditorGUILayout.TextArea( authorName );
		EditorGUILayout.EndHorizontal();
		_author.Set = authorName;
		bool canSend = !exporting && IsValidAuthorName( authorName );
		EditorGUILayout.PropertyField( _urlField );
		EditorGUI.BeginDisabledGroup( !canSend );
		var send = GUILayout.Button( "Export", K10GuiStyles.bigbuttonStyle ) && canSend;
		EditorGUI.EndDisabledGroup();
		GUILayout.EndVertical();
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
				GUILayout.Label( "\"" + _exportation[i].Name + "\"" );
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
		GUILayout.EndVertical();

		GUILayout.BeginVertical( "HelpBox" );
		bool allSelected = true;
		bool allSelectedAreOpen = true;
		bool allUnselectedAreClosed = true;
		for( int i = 0; i < _exportFields.arraySize; i++ )
		{
			var element = _exportFields.GetArrayElementAtIndex( i );
			var selected = element.FindPropertyRelative( "_selected" ).boolValue;
			allSelected &= selected;
			var editMode = element.FindPropertyRelative( "_editMode" ).boolValue;
			if( selected ) allSelectedAreOpen &= editMode;
			else allUnselectedAreClosed &= !editMode;
		}
		GUILayout.BeginHorizontal( "HelpBox" );
		EditorGUI.BeginDisabledGroup( exporting );
		if( GUILayout.Button( $"{( allSelected ? "Unselect" : "Select" )} All" ) )
		{
			for( int i = 0; i < _exportFields.arraySize; i++ )
			{
				var element = _exportFields.GetArrayElementAtIndex( i );
				element.FindPropertyRelative( "_selected" ).boolValue = !allSelected;
			}
		}
		if( GUILayout.Button( $"Invert Selection" ) )
		{
			for( int i = 0; i < _exportFields.arraySize; i++ )
			{
				var element = _exportFields.GetArrayElementAtIndex( i );
				var selected = element.FindPropertyRelative( "_selected" );
				selected.boolValue = !selected.boolValue;
			}
		}
		EditorGUI.EndDisabledGroup();
		if( GUILayout.Button( $"{( allSelectedAreOpen ? "Close" : "Open" )} all selected(s)" ) ) ToggleElements( _exportFields, !allSelectedAreOpen, true );
		if( GUILayout.Button( $"{( allUnselectedAreClosed ? "Open" : "Close" )} all unselected(s)" ) ) ToggleElements( _exportFields, allUnselectedAreClosed, false );
		GUILayout.EndHorizontal();

		EditorGUI.BeginDisabledGroup( exporting );
		_exportList.DoLayoutList();
		EditorGUI.EndDisabledGroup();
		GUILayout.EndVertical();

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
						if( exp.Ignored ) continue;
						var field = exporter.GetField( i );
						var batchSize = field.BatchSize;
						var nextTrigger = sendNext;
						var datas = new List<string>();
						Debug.Log( $"batchSize: {batchSize}" );
						if( batchSize == 0 ) datas.Add( $"{{ \"tableName\": \"{field.FieldName}\", \"data\": {field.GetMemberValueSerialized()} }}" );
						else
						{
							var elementsCount = field.GetCount();
							Debug.Log( $"elementsCount: {elementsCount}" );
							for( int e = 0; e < elementsCount; e += batchSize )
								datas.Add( $"{{ \"tableName\": \"{field.FieldName}\"," +
											$" \"ignoreClear\": {((e!=0)?"true":"false")}," +
											$"\"data\": {field.GetMemberValueSerialized( e, batchSize )} }}" );
						}
						exp.SetState( EExportStep.Queue );
						sendNext = new CallOnce( () =>
						{
							exp.Trigger( url, datas, nextTrigger, authorName );
							SetDataDirty();
						} );
					}

					sendNext.Trigger();
				}
			}
		}
	}

	void ToggleElements( SerializedProperty prop, bool open, bool? onlyIfSelected = null )
	{
		for( int i = 0; i < prop.arraySize; i++ )
		{
			var element = prop.GetArrayElementAtIndex( i );
			bool valid = true;
			if( onlyIfSelected.HasValue ) 
			{
				var selected = element.FindPropertyRelative( "_selected" ).boolValue;
				valid = ( onlyIfSelected.Value == selected );
			}
			if( !valid ) continue;
			element.FindPropertyRelative( "_editMode" ).boolValue = open;
			var fields = element.FindPropertyRelative( "_fields" );
			ToggleElements( fields, open, onlyIfSelected );
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
		int _count;
		List<string> _errorMessage = new List<string>();
		List<EExportStep> _state = new List<EExportStep>();
		UnityWebRequest _request;

		public string Name => _name;
		public bool Ignored => _state.Contains( EExportStep.Ignored );
		public bool Exporting => _state.Contains( EExportStep.Queue ) || _state.Contains( EExportStep.Sent );
		public bool AllSucceeded => _state.All( ( st ) => st == EExportStep.Succeeded );
		public bool Finished => _state.All( ( st ) => st == EExportStep.Succeeded || st == EExportStep.Fail );
		public bool HasErrors => _state.Contains( EExportStep.Fail );

		public string Message
		{
			get
			{
				var countAppend = ( _count > 0 ) ? $" {_state.FindAll( ( st ) => st == EExportStep.Succeeded || st == EExportStep.Fail ).Count}/{_count}" : string.Empty;
				if( Exporting && _count > 0 ) return "Exporting" + countAppend;
				if( !Finished || AllSucceeded )
				{
					if( _state.Count > 0 ) return _state[0].ToString() + countAppend;
					return "NULL";
				}
				return $"{string.Join( "\n", _errorMessage.ToArray() )}";
			}
		}

		public void SetState( EExportStep state )
		{
			_state.Clear();
			_state.Add( state );
		}

		public class BypassCertificate : CertificateHandler
		{
			protected override bool ValidateCertificate( byte[] certificateData )
			{
				// var certificateDebug = string.Join( "|", certificateData.ToList().ConvertAll( ( b ) => b.ToString() ) );
				// Debug.Log( "Certificate: " + certificateDebug );
				return true;
			}
		}

		public void Trigger( string url, List<string> data, IEventTrigger onComplete, string authorName )
		{
			_errorMessage.Clear();
			_state.Clear();

			_count = data.Count;

			var startTrigger = new EventSlot();
			var currentTrigger = startTrigger;

			for( int i = 0; i < data.Count; i++ )
			{
				_state.Add( EExportStep.Queue );
				var thisID = i;
				WWWForm form = new WWWForm();
				var realData = data[thisID];
				form.AddField( "updateTable", realData );
				if( !string.IsNullOrEmpty( authorName ) ) form.AddField( "author", authorName );
				form.AddField( "source", SystemInfo.deviceName );

				Debug.Log( $"Post to {url}\nwith data:\n{realData.FormatAsJson( "    " )}" );
				var exportName = data.Count > 1 ? $"export_{i}_of_{data.Count}" : "export";
				realData.FormatAsJson().LogToJsonFile( exportName, _name );

				UnityWebRequest www = UnityWebRequest.Post( url, form );
				www.certificateHandler = new BypassCertificate();

				var nextTrigger = new EventSlot();
				currentTrigger.Register( () => 
				{
					var asyncOp = www.SendWebRequest();
					_state[thisID] = EExportStep.Sent;
					asyncOp.completed += ( aOp ) =>
					{
						_state[thisID] = EExportStep.Fail;
						if( www.isNetworkError || www.isHttpError )
						{
							var error = $"{( www.isNetworkError ? "isNetworkError " : "" )}{( www.isHttpError ? "isHttpError" : "" )}({www.error}:{www.responseCode})";
							Debug.Log( "!!Error!! " + error );

							var json = Newtonsoft.Json.JsonConvert.SerializeObject( www );
							json.FormatAsJson().LogToJsonFile( exportName, _name + "_FAIL" );

							_errorMessage.Add( $"[{thisID}]:{error}" );
							// _errorCode[thisID] = www.error;
						}
						else
						{
							var dh = www.downloadHandler;
							dh.text.FormatAsJson().LogToJsonFile( exportName, _name + "_RESPONSE" );
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
										_errorMessage.Add( $"[{thisID}]:{errorMessage as string}" );
										// _errorCode[thisID] = errorCode as string;
									}
								}
								else
								{
									var hasResponse = jo.TryGetValue( "response", out var response );
									var hasData = jo.TryGetValue( "newData", out var dataBack );
									if( hasResponse ) _state[thisID] = EExportStep.Succeeded;
									// Debug.Log( $"Succeeded! {hasResponse} \n{response.ToStringOrNull().FormatAsJson( "    " )}" );
									// Debug.Log( $"hasData? {hasData} \n{dataBack.ToStringOrNull().FormatAsJson( "    " )}" );
								}
							}
							else
							{
								Debug.Log( "Malformed response\n" + dh.text );
							}
						}

						nextTrigger.Trigger();
						www.Dispose();
					};
				} );

				currentTrigger = nextTrigger;
			}
			currentTrigger.Register( onComplete );
			startTrigger.Trigger();
		}

		public Color? GetColor()
		{
			if( _state.Contains( EExportStep.Idle ) ) return K10GuiStyles.GREY_TINT_COLOR;
			if( _state.Contains( EExportStep.Ignored ) ) return K10GuiStyles.DARKER_TINT_COLOR;
			if( _state.Contains( EExportStep.Queue ) ) return K10GuiStyles.CYAN_TINT_COLOR;
			if( _state.Contains( EExportStep.Sent ) ) return K10GuiStyles.YELLOW_TINT_COLOR;
			if( _state.Contains( EExportStep.Fail ) ) return K10GuiStyles.RED_TINT_COLOR;
			if( _state.Contains( EExportStep.Succeeded ) ) return K10GuiStyles.GREEN_TINT_COLOR;
			return null;
		}

		public void Set( ExportField exportField )
		{
			_name = exportField.FieldName;
			if( !exportField.Selected ) SetState( EExportStep.Ignored );
			else if( _state.Contains( EExportStep.Ignored ) ) SetState( EExportStep.Idle );
		}
	}
}