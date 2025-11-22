using System;
using System.Text;
using FileGDB.Core;
using LINQPad.Extensibility.DataContext;

namespace FileGDB.LinqPadDriver;

public static class Utils
{
	// Custom connection properties are in the ConnectionInfo.DriverData
	// XElement and will be persisted by LINQPad.

	public static string? GetGdbFolderPath(this IConnectionInfo cxInfo)
	{
		if (cxInfo is null)
			throw new ArgumentNullException(nameof(cxInfo));
		var driverData = cxInfo.DriverData;
		var path = (string?)driverData?.Element(Constants.DriverDataFolderPath);
		return path?.Trim();
	}

	public static void SetGdbFolderPath(this IConnectionInfo cxInfo, string? value)
	{
		if (cxInfo is null)
			throw new ArgumentNullException(nameof(cxInfo));
		var driverData = cxInfo.DriverData;
		if (driverData is null)
			throw new InvalidOperationException($"{nameof(cxInfo.DriverData)} is null");
		driverData.SetElementValue(Constants.DriverDataFolderPath, value?.Trim() ?? string.Empty);
	}

	public static bool GetDebugMode(this IConnectionInfo cxInfo)
	{
		if (cxInfo is null)
			throw new ArgumentNullException(nameof(cxInfo));
		var driverData = cxInfo.DriverData;
		var value = (bool?)driverData?.Element(Constants.DriverDataDebugMode);
		return value ?? false;
	}

	public static void SetDebugMode(this IConnectionInfo cxInfo, bool enable)
	{
		if (cxInfo is null)
			throw new ArgumentNullException(nameof(cxInfo));
		var driverData = cxInfo.DriverData;
		if (driverData is null)
			throw new InvalidOperationException($"{nameof(cxInfo.DriverData)} is null");
		driverData.SetElementValue(Constants.DriverDataDebugMode, enable);
	}

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
