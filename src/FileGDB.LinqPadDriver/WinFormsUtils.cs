using System;
using System.Windows.Forms;
using System.Windows.Media;

namespace FileGDB.LinqPadDriver;

public static class WinFormsUtils
{
	public static string? BrowseFolder(IWin32Window? owner)
	{
		var dialog = new FolderBrowserDialog();

		var result = owner is null
			? dialog.ShowDialog()
			: dialog.ShowDialog(owner);

		return result == DialogResult.OK ? dialog.SelectedPath : null;
	}

	public static IWin32Window GetIWin32Window(Visual visual)
	{
		// IWin32Window from System.Windows.Forms, NOT from System.Windows.Interop!
		var source = System.Windows.PresentationSource.FromVisual(visual);

		if (source is System.Windows.Interop.HwndSource hwndSource)
		{
			return new OldWindowAdapter(hwndSource.Handle);
		}

		return null!;
	}

	private class OldWindowAdapter : IWin32Window
	{
		private readonly IntPtr _handle;

		public OldWindowAdapter(IntPtr handle)
		{
			_handle = handle;
		}

		IntPtr IWin32Window.Handle => _handle;
	}
}
