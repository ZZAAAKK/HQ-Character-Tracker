using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HQ_Character_Tracker.Services;

internal static class WindowsFileDialog
{
    private const uint PathMustExist = 0x00000800;
    private const uint NoChangeDirectory = 0x00000008;
    private const uint OverwritePrompt = 0x00000002;
    private const uint Explorer = 0x00080000;

    public static string? ShowSaveRosterDialog()
    {
        const int fileNameCapacity = 32768;
        const string suggestedFileName = "roster.hqct";
        var fileBuffer = Marshal.AllocHGlobal(fileNameCapacity * sizeof(char));
        try
        {
            Marshal.Copy(suggestedFileName.ToCharArray(), 0, fileBuffer, suggestedFileName.Length);
            Marshal.WriteInt16(fileBuffer, suggestedFileName.Length * sizeof(char), 0);

            var dialog = new OpenFileName
            {
                Size = Marshal.SizeOf<OpenFileName>(),
                Owner = GetActiveWindow(),
                Filter = "HQ Character Tracker rosters (*.hqct)\0*.hqct\0\0",
                FilterIndex = 1,
                File = fileBuffer,
                MaxFile = fileNameCapacity,
                InitialDirectory = UserDataDirectory.Path,
                Title = "Save Roster",
                Flags = PathMustExist | NoChangeDirectory | OverwritePrompt | Explorer,
                DefaultExtension = "hqct"
            };

            if (GetSaveFileName(ref dialog))
                return Marshal.PtrToStringUni(dialog.File);

            var error = CommDlgExtendedError();
            if (error != 0)
                throw new Win32Exception((int)error);

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(ref OpenFileName dialog);

    [DllImport("comdlg32.dll", EntryPoint = "CommDlgExtendedError")]
    private static extern uint CommDlgExtendedError();

    [DllImport("user32.dll", EntryPoint = "GetActiveWindow")]
    private static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int Size;
        public IntPtr Owner;
        public IntPtr Instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Filter;
        [MarshalAs(UnmanagedType.LPWStr)] public string? CustomFilter;
        public uint MaxCustomFilter;
        public uint FilterIndex;
        public IntPtr File;
        public uint MaxFile;
        public IntPtr FileTitle;
        public uint MaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? InitialDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Title;
        public uint Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TemplateName;
        public IntPtr Reserved;
        public uint ReservedValue;
        public uint FlagsEx;
    }
}
