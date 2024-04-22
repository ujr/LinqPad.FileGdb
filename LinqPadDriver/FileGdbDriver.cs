using System.Diagnostics;
using System.IO;
using System.Reflection;
using LINQPad.Extensibility.DataContext;

namespace LinqPadDriver;

public class FileGdbDriver : StaticDataContextDriver
{
	static FileGdbDriver()
	{
		// Attach to Visual Studio's debugger when an exception is thrown:
		AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
		{
			if (args.Exception.StackTrace != null &&
			    args.Exception.StackTrace.Contains(nameof(FileGdbDriver)))
			{
				Debugger.Launch();
			}
		};
	}

	public override string Name => "FileGDB Driver";
	public override string Author => "ujr";

	public override string GetConnectionDescription(IConnectionInfo cxInfo)
	{
		var path = (string?)cxInfo.DriverData?.Element(Constants.DriverDataFolderPath);

		if (string.IsNullOrEmpty(path))
			return "File GDB (not configured)";

		try
		{
			return Directory.Exists(path) ? path : $"{path} (no such directory)";
		}
		catch (Exception ex)
		{
			return $"{path}: {ex.Message}";
		}
	}

	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions options)
	{
		//Debugger.Launch();

		var assembly = Assembly.GetExecutingAssembly();
		var dllFileName = Path.GetFileName(assembly.Location);
		// Note: assembly.Location is in a temp folder; use file name only!
		cxInfo.CustomTypeInfo.CustomAssemblyPath = dllFileName;
		cxInfo.CustomTypeInfo.CustomTypeName = typeof(FileGdbContext).FullName;

		var dialog = new ConnectionDialog(cxInfo);
		return dialog.ShowDialog() == true;
	}

	public override List<ExplorerItem> GetSchema(IConnectionInfo cxInfo, Type customType)
	{
		var catalog = new ExplorerItem("Catalog", ExplorerItemKind.QueryableObject, ExplorerIcon.Schema)
		{
			IsEnumerable = true,
			DragText = nameof(FileGdbContext.Catalog),
			ToolTipText = "Catalog system table (list of all tables)"
		};

		var dbTune = new ExplorerItem("DBTune", ExplorerItemKind.QueryableObject, ExplorerIcon.Parameter)
		{
			IsEnumerable = true,
			DragText = nameof(FileGdbContext.DBTune),
			ToolTipText = "DBTune system table (config keyword parameters)"
		};

		var sample = new ExplorerItem("Sample", ExplorerItemKind.QueryableObject, ExplorerIcon.Box)
		{
			IsEnumerable = true,
			ToolTipText = "Right-click me! Drag me to code editor!",
			DragText = nameof(FileGdbContext.FooBar) // text will be added to code editor!
		};

		return new List<ExplorerItem>
		{
			catalog, dbTune, sample
		};
	}
}
