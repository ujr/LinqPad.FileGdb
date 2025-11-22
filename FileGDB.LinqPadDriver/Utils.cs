using System;
using System.Text;
using FileGDB.Core;

namespace FileGDB.LinqPadDriver;

public static class Utils
{
	public static string FormatString(string text)
	{
		var sb = new StringBuilder();
		FormatString(text, sb);
		return sb.ToString();
	}

	/// <summary>
	/// Format the given <paramref name="value"/> as a valid C# string
	/// (surrogate pairs and non-printable Unicode stuff not handled)
	/// </summary>
	private static void FormatString(string value, StringBuilder result)
	{
		int len = value.Length;

		result.Append('"');

		for (int j = 0; j < len; j++)
		{
			char c = value[j];

			const string escapes = "\"\"\\\\\bb\ff\nn\rr\tt";
			int k = escapes.IndexOf(c);

			if (k >= 0 && k % 2 == 0)
			{
				result.Append('\\');
				result.Append(escapes[k + 1]);
			}
			else if (char.IsControl(c))
			{
				result.AppendFormat("\\u{0:x4}", (int)c);
			}
			else
			{
				result.Append(c);
			}
		}

		result.Append('"');
	}

	public static string GetDisplayName(GeometryDef geometryDef)
	{
		if (geometryDef is null)
			throw new ArgumentNullException(nameof(geometryDef));

		var sb = new StringBuilder();
		sb.Append(geometryDef.GeometryType);

		if (geometryDef.HasZ || geometryDef.HasM)
		{
			sb.Append(' ');

			if (geometryDef.HasZ)
			{
				sb.Append('Z');
			}

			if (geometryDef.HasM)
			{
				sb.Append('M');
			}
		}

		return sb.ToString();
	}
}
