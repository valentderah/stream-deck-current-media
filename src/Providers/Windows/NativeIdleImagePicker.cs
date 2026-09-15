using System.Runtime.InteropServices;

namespace CurrentMedia.Imaging;

static class NativeIdleImagePicker
{
    public static string? Pick()
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return ShowDialog();
        }

        string? path = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                path = ShowDialog();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (error != null)
        {
            throw error;
        }

        return path;
    }

    private static string? ShowDialog()
    {
        OleInitialize(IntPtr.Zero);

        const int maxChars = 1024;
        var fileBuffer = Marshal.AllocHGlobal(maxChars * 2);
        var filterBuffer = AllocDoubleNullString(
            "Images (*.png;*.jpg;*.jpeg;*.webp)\0*.png;*.jpg;*.jpeg;*.webp\0");
        var titleBuffer = Marshal.StringToHGlobalUni("Idle Background");
        var defExtBuffer = Marshal.StringToHGlobalUni("png");
        var owner = CreateOwnerWindow();
        try
        {
            Marshal.Copy(new byte[maxChars * 2], 0, fileBuffer, maxChars * 2);

            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = filterBuffer,
                lpstrFile = fileBuffer,
                nMaxFile = maxChars,
                lpstrTitle = titleBuffer,
                lpstrDefExt = defExtBuffer,
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadOnly
            };

            if (GetOpenFileNameW(ref ofn))
            {
                return Marshal.PtrToStringUni(fileBuffer);
            }

            var commDlgError = CommDlgExtendedError();
            if (commDlgError != 0)
            {
                throw new InvalidOperationException($"GetOpenFileName failed (0x{commDlgError:X8})");
            }

            return null;
        }
        finally
        {
            if (owner != IntPtr.Zero)
            {
                DestroyWindow(owner);
            }

            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filterBuffer);
            Marshal.FreeHGlobal(titleBuffer);
            Marshal.FreeHGlobal(defExtBuffer);
            OleUninitialize();
        }
    }

    private static IntPtr AllocDoubleNullString(string value)
    {
        var chars = (value.TrimEnd('\0') + "\0\0").ToCharArray();
        var ptr = Marshal.AllocHGlobal(chars.Length * 2);
        Marshal.Copy(chars, 0, ptr, chars.Length);
        return ptr;
    }

    private static IntPtr CreateOwnerWindow()
    {
        return CreateWindowExW(
            WsExTopmost | WsExToolWindow,
            "STATIC",
            "Idle Background",
            WsPopup | WsVisible,
            0,
            0,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandleW(null),
            IntPtr.Zero);
    }

    private const int OfnHideReadOnly = 0x00000004;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;
    private const uint WsPopup = 0x80000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExToolWindow = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetOpenFileNameW")]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);
}
