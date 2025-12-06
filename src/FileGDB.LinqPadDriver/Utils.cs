using System;
using System.Text;

namespace FileGDB.LinqPadDriver;

public static class Utils
{
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
