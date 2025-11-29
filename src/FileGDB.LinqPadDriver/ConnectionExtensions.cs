using System;
using LINQPad.Extensibility.DataContext;

namespace FileGDB.LinqPadDriver;

public static class ConnectionExtensions
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
}
