using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System;

public enum EFieldSerializationType { ToString, ToJson, ToArray, Inherited, BoolToNumber, InvertedBool, ToStringToLower, AsBitMaskToValues }

[System.Serializable]
public class ExportField
{
	[SerializeField] string _fieldName;
	[SerializeField] string _memberPath;
	[SerializeField] EFieldSerializationType _serialization;
	[SerializeField] string _format = "{0}";
	[SerializeField] bool _selected;
	[SerializeField] int _batchSize;
	[SerializeField] List<ExportField> _fields;
	[SerializeField] ScriptableObject _rootObject;

	#if UNITY_EDITOR
	public readonly StringBuilder SB = new StringBuilder();
	#endif

	public string FieldName => _fieldName;
	public bool Selected => _selected;
	public ScriptableObject RootObject => _rootObject;
	public int BatchSize => _serialization == EFieldSerializationType.ToArray ? _batchSize : 0;
	public string Format => _format;
	public EFieldSerializationType Serialization => _serialization;
	public string MemberPath => _memberPath;

	public int FieldsCount => _fields.Count;

	public bool ShowBatchSize => _serialization == EFieldSerializationType.ToArray;

	public ExportField GetField( int index ) => ( index < _fields.Count ) ? _fields[index] : null;

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
