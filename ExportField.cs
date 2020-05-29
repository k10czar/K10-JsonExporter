using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System;

public enum EFieldSerializationType { ToString, ToJson, ToArray, Inherited, BoolToNumber, InvertedBool }

[System.Serializable]
public class ExportField
{
	[SerializeField] string _fieldName;
	[SerializeField] string _memberPath;
	[SerializeField] EFieldSerializationType _serialization;
	[SerializeField] string _format = "{0}";
	[SerializeField] bool _editMode;
	[SerializeField] bool _selected;
	[SerializeField] int _inspectedElement;
	[SerializeField] bool _inspect;
	// [SerializeField] EValidation _validation;
	[SerializeField] List<ExportField> _fields;
	[SerializeField] ScriptableObject _rootObject;
	[SerializeField] string _inspection;

	#if UNITY_EDITOR
	public readonly StringBuilder SB = new StringBuilder();
	#endif

	public string FieldName => _fieldName;
	public bool Selected => _selected;
	public ScriptableObject RootObject => _rootObject;
	public string Inspection => _inspection;
	public bool Inspect => _inspect;
	public int InspectedElement => _inspectedElement;
	public bool EditMode => _editMode;
	public string Format => _format;
	public EFieldSerializationType Serialization => _serialization;
	public string MemberPath => _memberPath;

	public int FieldsCount => _fields.Count;
	public ExportField GetField( int index ) => _fields[index];

	public bool CheckIfIsDirectValue()
	{
		var fieldsCount = 0;
		for( int i = 0; i < _fields.Count; i++ )
		{
			var f = _fields[i];
			if( f.Selected )
			{
				fieldsCount++;
				if( fieldsCount > 1 || !string.IsNullOrWhiteSpace( f._fieldName ) ) return false;
			}
		}
		return true;
	}
}