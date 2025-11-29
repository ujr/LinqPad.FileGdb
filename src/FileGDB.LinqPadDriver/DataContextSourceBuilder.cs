using System;
using System.IO;
using System.Text;
using FileGDB.Core;

namespace FileGDB.LinqPadDriver;

internal class DataContextSourceBuilder
{
	private readonly string _nameSpace;
	private readonly string _typeName;

	public DataContextSourceBuilder(string nameSpace, string typeName)
	{
		if (string.IsNullOrEmpty(nameSpace))
			throw new ArgumentNullException(nameof(nameSpace));
		if (string.IsNullOrEmpty(typeName))
			throw new ArgumentNullException(nameof(typeName));

		_nameSpace = nameSpace;
		_typeName = typeName;
	}

	private const string MainSourceTemplate = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FileGDB.LinqPadDriver;

namespace $$Namespace$$;

public class $$TypeName$$ : DataContextBase
{
    public $$TypeName$$(string gdbFolderPath, bool debugMode) : base(gdbFolderPath, debugMode)
    {
        Tables = new TableContainer(GDB, debugMode);
    }

    public TableContainer Tables { get; }

    public class TableContainer : TableContainerBase
    {
        public TableContainer(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

        // public @FooTable @Foo => GetTable<@FooTable>(""Foo"");
$$TableProperties$$
    }

$$PerTableClasses$$
}
";

	private const string PerTableTemplate = @"
public class $$TableName$$_Table : TableBase<$$TableName$$_Table.Row>
{
    public $$TableName$$_Table(FileGDB.Core.FileGDB gdb, string tableName, bool debugMode = false)
        : base(gdb, tableName, debugMode) { }

    protected override Row CreateRow()
    {
        return new Row();
    }

    public class Row : RowBase
    {
        // property per field, e.g., public BarType Bar { get; set; }
$$FieldProperties$$
    }
}
";

	public string Build(Core.FileGDB? gdb)
	{
		var tableProps = new StringBuilder();
		var tableClasses = new StringBuilder();

		if (gdb is not null)
		{
			foreach (var entry in gdb.Catalog)
			{
				BuildTableCode(gdb, entry, tableProps, tableClasses);
			}
		}

		var sourceCode = new StringBuilder(MainSourceTemplate)
			.Replace("$$Namespace$$", _nameSpace)
			.Replace("$$TypeName$$", _typeName)
			.Replace("$$TableProperties$$", tableProps.Trim().ToString())
			.Replace("$$PerTableClasses$$", tableClasses.Trim().ToString())
			.Trim().AppendLine().ToString();

		return sourceCode;
	}

	private static void BuildTableCode(Core.FileGDB gdb, CatalogEntry entry, StringBuilder tableProps, StringBuilder tableClasses)
	{
		var tableName = entry.Name;
		var tableClassName = MakeIdentifier(tableName);

		tableProps.Append($"public @{tableClassName}_Table @{tableClassName} => ");
		tableProps.AppendLine($"GetTable<@{tableClassName}_Table>(\"{tableClassName}\");");

		try
		{
			using var table = gdb.OpenTable(tableName);

			var fieldProperties = BuildFieldsCode(table);

			var perTableCode = PerTableTemplate
				.Replace("$$TableName$$", tableClassName)
				.Replace("$$FieldProperties$$", fieldProperties);

			tableClasses.AppendLine(perTableCode.Trim()).AppendLine();
		}
		catch (IOException)
		{
			// Could not open table: assume it has no fields and generate
			// code accordingly; the error will pop up again when enumerated
			var perTableCode = PerTableTemplate
				.Replace("$$TableName$$", tableClassName)
				.Replace("$$FieldProperties$$", string.Empty);
			tableClasses.AppendLine(perTableCode.Trim()).AppendLine();
		}
	}

	private static string BuildFieldsCode(Table table)
	{
		var fieldProperties = new StringBuilder();

		foreach (var field in table.Fields)
		{
			var fieldName = field.Name;
			var propName = MakeIdentifier(fieldName);
			var escaped = EscapeForString(fieldName);
			var fieldType = Table.GetDataType(field.Type);
			var fieldTypeName = GetPropertyTypeName(fieldType);

			fieldProperties.AppendLine($"[{nameof(DatabaseFieldAttribute)}(\"{escaped}\")]");
			fieldProperties.AppendLine($"public {fieldTypeName} @{propName} {{ get; set; }}");
		}

		return fieldProperties.Trim().ToString();
	}

	private static string MakeIdentifier(string name)
	{
		// must start with '_' or letter
		// can contain '_' or letter or digit
		if (name.Length < 1) return "_";
		var sb = new StringBuilder();
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			if (i == 0 && c != '_' && !char.IsLetter(c))
			{
				sb.Append('_'); // prepend to make a valid ident
			}

			if (c == '_' || char.IsLetterOrDigit(c))
			{
				sb.Append(c);
			}
			else
			{
				sb.Append('_');
			}
		}

		return sb.ToString();
	}

	private static string EscapeForString(string name)
	{
		var sb = new StringBuilder();

		foreach (char c in name)
		{
			switch (c)
			{
				case '\\': sb.Append("\\\\"); break;
				case '"': sb.Append("\\\""); break;
				case '\0': sb.Append("\\0"); break;
				case '\a': sb.Append("\\a"); break;
				case '\b': sb.Append("\\b"); break;
				case '\f': sb.Append("\\f"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\t': sb.Append("\\t"); break;
				case '\v': sb.Append("\\v"); break;
				default:
					sb.Append(c);
					break;
			}
		}

		return sb.ToString();
	}

	private static string GetPropertyTypeName(Type fieldType)
	{
		if (fieldType == typeof(short))
			return "short";
		if (fieldType == typeof(short?))
			return "short?";
		if (fieldType == typeof(int))
			return "int";
		if (fieldType == typeof(int?))
			return "int?";
		if (fieldType == typeof(long))
			return "long";
		if (fieldType == typeof(long?))
			return "long?";
		if (fieldType == typeof(float))
			return "float";
		if (fieldType == typeof(float?))
			return "float?";
		if (fieldType == typeof(double))
			return "double";
		if (fieldType == typeof(double?))
			return "double?";
		if (fieldType == typeof(string))
			return "string";
		if (fieldType == typeof(byte[]))
			return "byte[]";
		if (fieldType == typeof(object))
			return "object";

		if (IsNullable(fieldType, out var underlyingType))
		{
			return $"{underlyingType.FullName}?";
		}

		return fieldType.FullName!;
	}

	private static bool IsNullable(Type type, out Type underlyingType)
	{
		var baseType = Nullable.GetUnderlyingType(type);
		underlyingType = baseType!;
		return baseType != null;
	}
}
