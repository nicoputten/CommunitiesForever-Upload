using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// The Start menu shortcut, carrying the app's AppUserModelId. Windows takes a notification's header (name and icon)
/// from it: without it the header shows the raw id (found when Nico tried UP-08). Velopack's installer creates the
/// same shortcut (UP-10); this keeps it right for any build.
/// </summary>
public static class StartMenuShortcut
{
    // PKEY_AppUserModel_ID
    private static readonly PropertyKey AppUserModelId = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), $"{AppIdentity.DisplayName}.lnk");

    /// <summary>Writes the shortcut when it is missing or points elsewhere.</summary>
    public static void Ensure(string exePath, string? shortcutPath = null)
    {
        string path = shortcutPath ?? DefaultPath;
        if (File.Exists(path) && Read(path) is { } current && current.Target == exePath && current.AppId == AppIdentity.AppId)
        {
            return;
        }

        var link = (IShellLinkW)(object)new ShellLink();
        try
        {
            link.SetPath(exePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");
            link.SetDescription(AppIdentity.DisplayName);
            var store = (IPropertyStore)link;
            var value = PropVariant.FromString(AppIdentity.AppId);
            try
            {
                var key = AppUserModelId;
                store.SetValue(ref key, ref value);
                store.Commit();
            }
            finally
            {
                value.Free();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ((IPersistFile)link).Save(path, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>The shortcut's target and AppUserModelId, or null when it can't be read.</summary>
    /// <summary>Deletes the shortcut (uninstall, UP-10).</summary>
    public static void Remove(string? shortcutPath = null) => File.Delete(shortcutPath ?? DefaultPath);

    public static (string Target, string? AppId)? Read(string shortcutPath)
    {
        var link = (IShellLinkW)(object)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(shortcutPath, 0);
            char[] target = new char[1024];
            link.GetPath(target, target.Length, IntPtr.Zero, 0);
            var key = AppUserModelId;
            ((IPropertyStore)link).GetValue(ref key, out var value);
            try
            {
                return (new string(target).TrimEnd('\0'), value.AsString());
            }
            finally
            {
                value.Free();
            }
        }
        catch (Exception e) when (e is COMException or FileNotFoundException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink;

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] file, int max, IntPtr findData, int flags);

        void GetIDList(out IntPtr idList);

        void SetIDList(IntPtr idList);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] name, int max);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] dir, int max);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] args, int max);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCmd);

        void SetShowCmd(int showCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] path, int max, out int icon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int icon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, int reserved);

        void Resolve(IntPtr hwnd, int flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);

        void GetAt(uint index, out PropertyKey key);

        void GetValue(ref PropertyKey key, out PropVariant value);

        void SetValue(ref PropertyKey key, ref PropVariant value);

        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey(Guid format, uint id)
    {
        private readonly Guid _format = format;
        private readonly uint _id = id;
    }

    // A PROPVARIANT holding a string (VT_LPWSTR) or nothing.
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        private const ushort LpwStr = 31;

        private ushort _type;
        private readonly ushort _reserved1;
        private readonly ushort _reserved2;
        private readonly ushort _reserved3;
        private IntPtr _value;
        private readonly IntPtr _padding;

        public static PropVariant FromString(string text) => new() { _type = LpwStr, _value = Marshal.StringToCoTaskMemUni(text) };

        public readonly string? AsString() => _type == LpwStr ? Marshal.PtrToStringUni(_value) : null;

        public void Free()
        {
            if (_type == LpwStr && _value != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_value);
            }

            _type = 0;
            _value = IntPtr.Zero;
        }
    }
}
