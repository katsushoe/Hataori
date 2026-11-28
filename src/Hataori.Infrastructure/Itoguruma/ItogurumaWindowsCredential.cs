using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hataori.Infrastructure.Itoguruma;

/// <summary>現在のWindowsユーザーが登録したItogurumaの資格情報を読み取ります。</summary>
public static class ItogurumaWindowsCredential
{
    private const string Target = "Itoguruma/McpBearerToken";
    private const int GenericCredential = 1;
    private const int NotFound = 1168;

    /// <summary>標準のローカルItogurumaに限り、保存済みトークンを返します。</summary>
    public static string? Read(Uri endpoint)
    {
        if (!OperatingSystem.IsWindows() || endpoint != new Uri("http://127.0.0.1:47631/mcp")) return null;
        if (!CredRead(Target, GenericCredential, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NotFound) return null;
            throw new Win32Exception(error, "Could not read the current user's Itoguruma credential.");
        }

        try
        {
            var native = Marshal.PtrToStructure<Credential>(pointer);
            if (native.Blob == IntPtr.Zero || native.BlobSize is < 64 or > 512 || native.BlobSize % sizeof(char) != 0)
                return null;
            var token = Marshal.PtrToStringUni(native.Blob, native.BlobSize / sizeof(char));
            return token is not null && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? token : null;
        }
        finally { CredFree(pointer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public FILETIME LastWritten;
        public int BlobSize;
        public IntPtr Blob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
