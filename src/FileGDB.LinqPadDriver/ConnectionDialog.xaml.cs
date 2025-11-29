using LINQPad.Extensibility.DataContext;
using System.Windows;

namespace FileGDB.LinqPadDriver;

public partial class ConnectionDialog
{
	private readonly ConnectionProperties _props;

	public ConnectionDialog(IConnectionInfo cxInfo)
	{
		// ConnectionProperties is our view model:
		_props = new ConnectionProperties(cxInfo);
		DataContext = _props;

		InitializeComponent();
	}

	private void BrowseFolder(object sender, RoutedEventArgs e)
	{
		// Use FolderBrowserDialog from WinForms (WPF has none)

		var owner = WinFormsUtils.GetIWin32Window(this);
		var folderPath = WinFormsUtils.BrowseFolder(owner);

		if (!string.IsNullOrEmpty(folderPath))
		{
			_props.FolderPath = folderPath;
		}
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}
}
