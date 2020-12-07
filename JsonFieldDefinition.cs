using UnityEngine;
using System;

[CreateAssetMenu( fileName = "JsonFieldDefinition", menuName = "K10/Json Field Definition", order = 51 )]
public class JsonFieldDefinition : ScriptableObject
{
	[SerializeField] ExportField _definition;

	public ExportField Definition => _definition;

	public void SetData( ExportField f ) { _definition = f; }
}
