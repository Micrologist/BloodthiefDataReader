//https://github.com/chrismcmorran/LinuxProcessVMWrapper/tree/master
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct iovec
{
    public void* iov_base;
    public int iov_len;
}

public static class ProcessExtensions
{
    [DllImport("libc")]
    public static extern unsafe int process_vm_writev(int pid,
        iovec* local_iov,
        ulong liovcnt,
        iovec* remote_iov,
        ulong riovcnt,
        ulong flags);

    [DllImport("libc")]
    public static extern unsafe int process_vm_readv(int pid,
        iovec* local_iov,
        ulong liovcnt,
        iovec* remote_iov,
        ulong riovcnt,
        ulong flags);


    /// <summary>
    /// Reads bytes equal to the size of T and attempts to cast those bytes into a T object.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="address">The address to read from.</param>
    /// <typeparam name="T">The type to cast.</typeparam>
    /// <returns>An unmanaged T if successful, otherwise a AccessViolationException is thrown.</returns>
    public static unsafe T Read<T>(this Process process, int address) where T : unmanaged
    {
        return Read<T>(process, (IntPtr) address);
    }

    public static unsafe T Read<T>(this Process process, IntPtr address) where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var ptr = stackalloc byte[size];
        var localIo = new iovec
        {
            iov_base = ptr,
            iov_len = size
        };
        var remoteIo = new iovec
        {
            iov_base = address.ToPointer(),
            iov_len = size
        };

        var res = process_vm_readv(process.Id, &localIo, 1, &remoteIo, 1, 0);
        if (res == -1)
        {
            throw new AccessViolationException("Failed to read " + sizeof(T) + $" bytes from {address:x8}.");
        }

        return *(T*) ptr;
    }

    public static unsafe T ReadValue<T>(this Process process, IntPtr address) where T : unmanaged
    {
        return Read<T>(process, address);
    }


	public static unsafe bool ReadBytes(this Process process, IntPtr address, int size, out byte[] val)
    {
        val = new byte[size];

		byte[] buffer = new byte[size];
		var ptr = GCHandle.Alloc(buffer, GCHandleType.Pinned).AddrOfPinnedObject();

		var localIo = new iovec
		{
			iov_base = (void*)ptr,
			iov_len = size
		};
		var remoteIo = new iovec
		{
			iov_base = address.ToPointer(),
			iov_len = size
		};

		var res = process_vm_readv(process.Id, &localIo, 1, &remoteIo, 1, 0);
		if (res == -1)
		{
            throw new AccessViolationException("Failed to read " + size + $" bytes from {address:x8}.");
            return false;
		}

        val = buffer;

        return true;
	}

	/// <summary>
	/// Attempts to write the provided value of type T to the specified memory address in the process.
	/// </summary>
	/// <param name="process">The process.</param>
	/// <param name="address">The address to write to.</param>
	/// <param name="value">The value to write.</param>
	/// <typeparam name="T">An unmanaged type.</typeparam>
	/// <returns>True if successful; false if the write failed.</returns>
	public static unsafe bool Write<T>(this Process process, int address, T value) where T : unmanaged
    {
        return Write(process, (IntPtr) address, value);
    }

    /// <summary>
    /// Attempts to write the provided value of type T to the specified memory address in the process.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="address">The address to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <typeparam name="T">An unmanaged type.</typeparam>
    /// <returns>True if successful; false if the write failed.</returns>
    public static unsafe bool Write<T>(this Process process, IntPtr address, T value) where T : unmanaged
    {
        var ptr = &value;
        var size = Unsafe.SizeOf<T>();
        var localIo = new iovec
        {
            iov_base = ptr,
            iov_len = size
        };
        var remoteIo = new iovec
        {
            iov_base = address.ToPointer(),
            iov_len = size
        };
        var res = process_vm_writev(process.Id, &localIo, 1, &remoteIo, 1, 0);
        
        return res != -1;
    }

}