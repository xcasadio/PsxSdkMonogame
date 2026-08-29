using System;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only — shared PSX-RAM address resolution hook. Several
// SDK modules (LibGpu's LoadImage(int) overload, LibCd's St* ring API) need to turn a raw PSX
// address stored as an int into the byte[] span the port uses to model that memory, without the
// SDK modeling PSX RAM as one flat addressable array. Which byte[] backs which address range is
// entirely game-specific, so the game installs this resolver once at startup (see the game's
// PsxSdkBridges). Previously LibGpu carried its own private copy of this hook
// (RamAddressResolver); it now delegates to this shared one so every module resolves PSX
// addresses through the same installed mapping.
public static class PsxRam
{
    public static Func<int, (byte[] buffer, int offset)?> AddressResolver;

    // JUSTIFICATION: desktop adaptation helper — reads `count` bytes starting at PSX address
    // `addr` through the installed resolver. Returns null when the address (or the full span)
    // doesn't resolve, rather than throwing, so callers can treat an unresolved ring/scratch
    // address the same way the rest of the SDK treats an unresolved LoadImage source.
    public static byte[] ReadBytes(int addr, int count)
    {
        var resolved = AddressResolver?.Invoke(addr);
        if (resolved == null)
        {
            return null;
        }

        var (buffer, offset) = resolved.Value;
        if (offset < 0 || offset + count > buffer.Length)
        {
            return null;
        }

        byte[] result = new byte[count];
        Array.Copy(buffer, offset, result, 0, count);
        return result;
    }

    // JUSTIFICATION: desktop adaptation helper — writes `data` starting at PSX address `addr`
    // through the installed resolver. Returns false (no-op) instead of throwing when the address
    // doesn't resolve or the write would run past the modeled span.
    public static bool WriteBytes(int addr, byte[] data)
    {
        var resolved = AddressResolver?.Invoke(addr);
        if (resolved == null)
        {
            return false;
        }

        var (buffer, offset) = resolved.Value;
        if (offset < 0 || offset + data.Length > buffer.Length)
        {
            return false;
        }

        Array.Copy(data, 0, buffer, offset, data.Length);
        return true;
    }

    public static ushort ReadU16(int addr)
    {
        byte[] b = ReadBytes(addr, 2);
        return b == null ? (ushort)0 : (ushort)(b[0] | (b[1] << 8));
    }

    public static void WriteU16(int addr, ushort value)
    {
        WriteBytes(addr, new[] { (byte)value, (byte)(value >> 8) });
    }
}
