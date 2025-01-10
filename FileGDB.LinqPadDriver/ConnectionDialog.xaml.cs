using System.Windows;
using System.Windows.Forms; // use FolderBrowserDialog from WinForms (WPF has none)
using LINQPad.Extensibility.DataContext;

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
		var dialog = new FolderBrowserDialog();
		var owner = this.GetIWin32Window();
		var result = dialog.ShowDialog(owner);
		if (result == System.Windows.Forms.DialogResult.OK)
		{
			var folderPath = dialog.SelectedPath;
			if (!string.IsNullOrEmpty(folderPath))
			{
				_props.FolderPath = folderPath;
			}
		}

		//// TODO Sadly, WPF has no Folder Dialog! Instead, let user choose a Mapfile:
		//// TODO Or: use the folder dialog from WinForms...
		//var dialog = new Microsoft.Win32.OpenFileDialog();

		//dialog.DefaultExt = "*.gdbtable";
		//dialog.Filter = ".gdbtable files|*.gdbtable|All files|*.*";
		//dialog.Title = "Choose any .gdbtable file within a File GDB";

		//bool? result = dialog.ShowDialog(this);
		//if (result == true)
		//{
		//	string filename = dialog.FileName;
		//	var directory = new FileInfo(filename).Directory;
		//	if (directory != null)
		//	{
		//		_props.FolderPath = directory.FullName;
		//	}
		//}
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}
}
