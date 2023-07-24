using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu( fileName = "JsonExporterData", menuName = "K10/Json Exporter Data ", order = 51 )]
public class JsonExporterData : ScriptableObject
{
	public static Action OnPreExport;

	[SerializeField] string _targetBranch = "";
	[SerializeField] List<Element> _exportFields = new List<Element>();

	public int FieldsCount => _exportFields.Count;
	public Element GetField( int index ) => _exportFields[index];

	public string TargetBranch { get => _targetBranch; set => _targetBranch = value; }

	public void SetFields( List<JsonFieldDefinition> fields )
	{
		_exportFields.Clear();
		for( int i = 0; i < fields.Count; i++ )
		{
			var f = fields[i];
			var element = new Element( f?.Definition?.Selected ?? false, f?.Definition?.BatchSize ?? 0, f );
			_exportFields.Add( element );
		}
	}

	[System.Serializable]
	public class Element
	{
		[SerializeField] bool _selected;
		[SerializeField] int _batch;
		[InlineProperties(true),SerializeField] JsonFieldDefinition _reference;

		public bool Selected => _selected;
		public int BatchSize => _batch;
		public JsonFieldDefinition Reference => _reference;

		public Element() {}
		public Element( bool selected, int batch, JsonFieldDefinition data ) : this()
		{
			_selected = selected;
			_batch = batch;
			_reference = data;
		}

		public void SortFieldsByName()
        {
			_reference.Definition.SortFieldsByName();
        }
	}
}
