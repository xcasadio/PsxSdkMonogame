using System;
using System.IO;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: desktop stand-in for what LoadExec does to main RAM — it copies the executable's body
// from the disc to its load address, and from then on every .data, .rodata and .sdata table the
// program reads is simply RAM that happens to hold the bytes of the file.
//
// WHY THIS EXISTS. Before it, no path anywhere in the port opened a .EXE and copied bytes into a
// modelled region. Every region declared as (address, length) was a zero-filled buffer, so every
// table the game reads out of its own image read zero — the battle scene always chose scene 0
// and always loaded index 0, and a record the roster builder ORs its flags into could keep
// nothing because there was nothing there to keep. Measured, not inferred: 436 code-formed
// addresses in VS.EXE, 417 in TITLE.EXE and 369 in SELECT.EXE land on non-zero initialised data
// that no region covers. Those are floors.
//
// WHAT THE MEASUREMENT FIXED IN THE DESIGN, each point against a byte:
//
//   * The image is WRITTEN by the game. 0x80082164 in VS.EXE has a lock bit raised by
//     lbu / ori 0x80 / sb at 0x80035428..38 and cleared by lbu / andi 0x7F / sb at 0x800354B4..C0.
//     So this is a MUTABLE COPY, re-armed on every overlay switch — never a static readonly view
//     of the file. The console reloads the image on every LoadExec; so does Arm().
//
//   * The load address is read from the header, never assumed. Eight of the nine images on the
//     disc load at 0x80020000; ENDING.EXE loads at 0x80010000, and a hard-coded formula would
//     have been silently wrong for it.
//
//   * This is NOT a LibGpu.RamRegion, and must never become one. RamResolve elects the region
//     with the HIGHEST base, so a whole-image row at 0x80020000 would lose to every declared
//     region — which is the fallback semantics wanted — but VS_EXE's resolver chain calls
//     RamResolve FIRST and only then FileIo, FighterSetup, AnimVm, SharedHighRam and the heap,
//     some of whose spans lie inside the image extent (0x8008DA48). As a region the image would
//     have shadowed them. As the LAST link of every overlay's ?? chain it answers only where
//     nothing else does, and only four of the five overlays consult RamResolve at all.
//
//   * The region registry is capped at 64 rows and overflows in silence. One image, one buffer,
//     zero rows.
//
//   * The crt0 .bss clear is a no-op against these bytes on all three overlays measured — every
//     byte the clear loop writes zero over is already zero in the file — so nothing here models
//     it, and a start() that clears .bss through PsxRam simply writes zeros over zeros.
//
// ONE HAZARD THIS INTRODUCES, found by the first run of its bench. A region declared by a static
// field initialiser does not exist until its class is touched. Before this file, an address in
// such a region read as unresolved during that window; now the image answers for it, and once
// the class runs the region shadows the image — two storages for one address, in that window
// only. The port's convention is that an overlay's main touches the owning class before reading,
// and its benches do the same by hand (VsRamValidation touches AnimVm; ExeImageValidation touches
// Roster). Nothing here can close the window; it can only be named.
//
// WHAT IS NOT MODELLED. The heap. VS.EXE's start arms InitHeap at 0x800C3DD8 and main re-arms it
// at 0x00010000; PsxHeap sits BEFORE this in every chain and wins over its own span, exactly as
// the heap overwrote the image on the console. The 16 KB block at [0x80101800, 0x80105800) that
// VS, TITLE, GAME and SP share byte for byte lies inside start's heap window; whether anything
// ever reads it is not closed and this file takes no position.
public static class PsxExeImage
{
    private const int HeaderSize = 0x800;
    private const int LoadAddressOffset = 0x18;
    private const int BodySizeOffset = 0x1C;

    private static byte[] s_body = Array.Empty<byte>();
    private static int s_loadAddress;
    private static string s_armedPath = string.Empty;

    // The PSX-EXE load address of the image currently armed, or 0 when none is.
    public static int LoadAddress => s_loadAddress;

    // The number of bytes the armed image covers from LoadAddress, or 0 when none is.
    public static int BodySize => s_body.Length;

    // The path the current copy was made from, for diagnostics.
    public static string ArmedPath => s_armedPath;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: LoadExec. Reads the header, takes a fresh mutable copy of the body, and makes it
    // answer for [t_addr, t_addr + t_size). Arming again — the same file or another — replaces the
    // copy outright, which is what the console's reload does to any writes the previous overlay
    // left in its image.
    public static void Arm(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Disarm();
            return;
        }

        byte[] file = File.ReadAllBytes(filePath);
        if (file.Length < HeaderSize
            || file[0] != (byte)'P' || file[1] != (byte)'S' || file[2] != (byte)'-' || file[3] != (byte)'X'
            || file[4] != (byte)' ' || file[5] != (byte)'E' || file[6] != (byte)'X' || file[7] != (byte)'E')
        {
            Disarm();
            return;
        }

        int loadAddress = BitConverter.ToInt32(file, LoadAddressOffset);
        int bodySize = BitConverter.ToInt32(file, BodySizeOffset);
        if (bodySize <= 0 || HeaderSize + bodySize > file.Length)
        {
            Disarm();
            return;
        }

        // A COPY, not a slice of the file array: the game writes into this and the next Arm must
        // start from the file again.
        byte[] body = new byte[bodySize];
        Buffer.BlockCopy(file, HeaderSize, body, 0, bodySize);

        s_body = body;
        s_loadAddress = loadAddress;
        s_armedPath = filePath;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    public static void Disarm()
    {
        s_body = Array.Empty<byte>();
        s_loadAddress = 0;
        s_armedPath = string.Empty;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the last link of an overlay's address-resolver chain. Answers only inside the
    // armed extent; callers chain it after every declared region, buffer and the heap.
    public static (byte[] buffer, int offset)? Resolve(int address)
    {
        if (s_body.Length == 0)
        {
            return null;
        }

        long offset = (long)address - s_loadAddress;
        return offset >= 0 && offset < s_body.Length ? (s_body, (int)offset) : null;
    }

    // JUSTIFICATION: backend MonoGame only
    // RELATION: lets a bench read the file's own bytes at an address without going through the
    // resolver, so it can tell "the resolver handed back the image" from "the resolver handed back
    // something else that happens to hold the same values".
    public static bool IsImageBuffer(byte[] buffer)
    {
        return s_body.Length != 0 && ReferenceEquals(buffer, s_body);
    }
}
