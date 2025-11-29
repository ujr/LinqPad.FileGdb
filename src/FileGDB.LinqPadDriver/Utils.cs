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

	/// <summary>
	/// Remove leading and trailing white space (or the given
	/// <paramref name="trimChars"/>) from <paramref name="sb"/>.
	/// Lifted from <see href="https://github.com/ujr/csutils"/>
	/// </summary>
	/// <param name="sb">The <see cref="StringBuilder"/> to trim</param>
	/// <param name="trimChars">The characters to trim; defaults to white space if <c>null</c></param>
	public static StringBuilder Trim(this StringBuilder sb, string? trimChars = null)
	{
		if (sb is null)
			throw new ArgumentNullException(nameof(sb));

		const string whiteSpace = " \t\n\v\f\r\u0085\u00A0"; // SP HT LF VT FF CR NextLine NoBreakSpace

		trimChars ??= whiteSpace;

		int length = sb.Length;
		int start, end;

		for (start = 0; start < length; start++)
		{
			if (trimChars.IndexOf(sb[start]) < 0)
				break;
		}

		for (end = length - 1; end >= start; end--)
		{
			if (trimChars.IndexOf(sb[end]) < 0)
				break;
		}

		if (end < length - 1)
		{
			sb.Remove(end + 1, length - end - 1);
		}

		if (start > 0)
		{
			sb.Remove(0, start);
		}

		return sb;
	}
}
