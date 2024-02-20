using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;

namespace LinqPadDriver;

/// <summary>
/// Wrapper to read/write connection properties.
/// Also acts as the ViewModel: will bind to it in ConnectionDialog.xaml
/// </summary>
public class ConnectionProperties : INotifyPropertyChanged
{
	public ConnectionProperties(IConnectionInfo cxInfo)
	{
		ConnectionInfo = cxInfo ?? throw new ArgumentNullException(nameof(cxInfo));
	}

	public IConnectionInfo ConnectionInfo { get; }

	private XElement DriverData => ConnectionInfo.DriverData;

	// Custom connection properties are in the ConnectionInfo.DriverData
	// XElement and will be persisted by LINQPad.

	public string? FolderPath
	{
		get => (string?)DriverData.Element(Constants.DriverDataFolderPath) ?? string.Empty;
		set
		{
			if (!string.Equals(value, FolderPath))
			{
				DriverData.SetElementValue(Constants.DriverDataFolderPath, value ?? string.Empty);
				OnPropertyChanged();
			}
		}
	}

	#region INotifyPropertyChanged

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	#endregion
}