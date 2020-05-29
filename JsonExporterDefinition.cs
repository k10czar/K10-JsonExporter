using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu( fileName = "JsonExporterDefinition", menuName = "K10/Json Exporter Definition", order = 51 )]
public class JsonExporterDefinition : ScriptableObject
{
	[SerializeField] string _url = "";
	[SerializeField] List<ExportField> _exportFields = new List<ExportField>();

	public int FieldsCount => _exportFields.Count;
	public ExportField GetField( int index ) => _exportFields[index];
}
