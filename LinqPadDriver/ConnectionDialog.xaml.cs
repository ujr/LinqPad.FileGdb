using System.IO;
using System.Windows;
using LINQPad.Extensibility.DataContext;

namespace LinqPadDriver;

public partial class ConnectionDialog : Window
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
		// TODO Sadly, WPF has no Folder Dialog! Instead, let user choose a Mapfile:
		var dialog = new Microsoft.Win32.OpenFileDialog();

		dialog.DefaultExt = "*.gdbtable";
		dialog.Filter = ".gdbtable files|*.gdbtable|All files|*.*";
		dialog.Title = "Choose any .gdbtable file within a File GDB";

		bool? result = dialog.ShowDialog(this);
		if (result == true)
		{
			string filename = dialog.FileName;
			var directory = new FileInfo(filename).Directory;
			if (directory != null)
			{
				_props.FolderPath = directory.FullName;
			}
		}
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}
}