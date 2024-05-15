namespace FileGDB.Core;

public static class Extensions
{
	public static int? GetInt32(this RowsResult result, string fieldName)
	{
		if (result is null)
			throw new ArgumentNullException(nameof(result));
		var value = result.GetValue(fieldName);
		if (value is null) return null;
		if (value is DBNull) return null;
		return Convert.ToInt32(value);
	}

	public static string? GetString(this RowsResult result, string fieldName)
	{
		if (result is null)
			throw new ArgumentNullException(nameof(result));
		var value = result.GetValue(fieldName);
		if (value is null) return null;
		if (value is DBNull) return null;
		return Convert.ToString(value);
	}

	public static double? GetDouble(this RowsResult result, string fieldName)
	{
		if (result is null)
			throw new ArgumentNullException(nameof(result));
		var value = result.GetValue(fieldName);
		if (value is null) return null;
		if (value is DBNull) return null;
		return Convert.ToDouble(value);
	}
}
