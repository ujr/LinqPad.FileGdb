using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LINQPad.Extensibility.DataContext;

namespace FileGDB.LinqPadDriver;

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

	public string? FolderPath
	{
		get => ConnectionInfo.GetGdbFolderPath();
		set
		{
			if (!string.Equals(value, FolderPath))
			{
				ConnectionInfo.SetGdbFolderPath(value);
				OnPropertyChanged();
			}
		}
	}

	public bool DebugMode
	{
		get => ConnectionInfo.GetDebugMode();
		set
		{
			if (value != DebugMode)
			{
				ConnectionInfo.SetDebugMode(value);
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
