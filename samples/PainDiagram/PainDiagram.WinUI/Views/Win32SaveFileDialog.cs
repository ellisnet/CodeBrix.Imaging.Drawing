using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PainDiagram.WinUI.Views;

/// <summary>
/// A thin wrapper over the Win32 common "Save As" item dialog (<c>IFileSaveDialog</c>).
///
/// The WinRT <see cref="Windows.Storage.Pickers.FileSavePicker"/> always shows its own
/// "…already exists. Replace it?" confirmation and offers no way to turn it off, which would
/// double up with the app's own save-time overwrite prompt. This dialog lets us clear the
/// <c>FOS_OVERWRITEPROMPT</c> option so choosing an existing file is silent — exactly what the
/// WPF head does with <c>SaveFileDialog.OverwritePrompt = false</c>. Unlike the WinRT picker it
/// also does not create an empty placeholder file at the chosen path.
/// </summary>
internal static class Win32SaveFileDialog
{
    /// <summary>
    /// Shows the Save-As dialog and returns the full path the user chose, or <c>null</c> if they
    /// cancelled. No overwrite confirmation is shown; the caller confirms replacement itself.
    /// </summary>
    public static string PickSavePath(IntPtr ownerHwnd, string suggestedFileName, string title)
    {
        var dialog = (IFileDialog)new FileSaveDialog();
        try
        {
            //Start from the file system's real paths, and don't nag about overwriting.
            dialog.GetOptions(out var options);
            options |= FOS.FORCEFILESYSTEM;
            options &= ~FOS.OVERWRITEPROMPT;
            dialog.SetOptions(options);

            var filters = new[]
            {
                new COMDLG_FILTERSPEC { pszName = "PNG image", pszSpec = "*.png" },
                new COMDLG_FILTERSPEC { pszName = "All files", pszSpec = "*.*" }
            };
            dialog.SetFileTypes((uint)filters.Length, filters);
            dialog.SetFileTypeIndex(1);
            dialog.SetDefaultExtension("png");

            if (!string.IsNullOrWhiteSpace(title)) { dialog.SetTitle(title); }
            if (!string.IsNullOrWhiteSpace(suggestedFileName)) { dialog.SetFileName(suggestedFileName); }

            TrySetDefaultFolder(dialog, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            const int cancelledHr = unchecked((int)0x800704C7); //HRESULT_FROM_WIN32(ERROR_CANCELLED)
            var hr = dialog.Show(ownerHwnd);
            if (hr == cancelledHr) { return null; }
            if (hr < 0) { Marshal.ThrowExceptionForHR(hr); }

            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(SIGDN.FILESYSPATH, out var pathPtr);
                try { return Marshal.PtrToStringUni(pathPtr); }
                finally { Marshal.FreeCoTaskMem(pathPtr); }
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private static void TrySetDefaultFolder(IFileDialog dialog, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) { return; }

        try
        {
            var shellItemGuid = typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref shellItemGuid, out var folder);
            if (folder is not null)
            {
                dialog.SetDefaultFolder(folder);
                Marshal.ReleaseComObject(folder);
            }
        }
        catch
        {
            //A missing/inaccessible Pictures folder just means the dialog opens wherever it likes.
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);

    // ── COM interop definitions (Windows common item dialog) ─────────────

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialog { }

    [Flags]
    private enum FOS : uint
    {
        OVERWRITEPROMPT = 0x00000002,
        FORCEFILESYSTEM = 0x00000040,
        PATHMUSTEXIST = 0x00000800,
        FILEMUSTEXIST = 0x00001000,
        CREATEPROMPT = 0x00002000,
        NOREADONLYRETURN = 0x00008000
    }

    private enum SIGDN : uint
    {
        FILESYSPATH = 0x80058000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        //IModalWindow
        [PreserveSig] int Show(IntPtr parent);

        //IFileDialog
        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([MarshalAs(UnmanagedType.Error)] int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
