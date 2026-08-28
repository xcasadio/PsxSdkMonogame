using System;
using System.Collections.Generic;
using System.IO;

namespace PsxSdkMonogame;

public static class LibMcrd
{
    public class DIRENTRY
    {
        public int status;
        public int[] reserved = new int[3];
        public byte[] name = new byte[20];
        public byte pad;
    }

    public delegate void MemCB(uint cmds, uint result);

    // JUSTIFICATION: PSX hardware adaptation only — a real PSX memory card is 128 KB, addressed by
    // the BIOS kernel card driver (_card_read/_card_write in LibApi.cs) as 16 fixed-size "blocks"
    // (frame 0 is the card's own directory, frames 1-15 hold save data). This backend represents
    // each of the 2 memory-card ports as its own folder and each of its 16 blocks as its own flat
    // file, so the virtual cards are trivially inspectable/editable on disk instead of one opaque
    // card image. Both ports are always treated as present per the task: no "card removed/missing"
    // state exists on the desktop side.
    private const int BlockSizeBytes = 0x2000;
    private const int BlockCount = 16;

    // JUSTIFICATION: PSX hardware adaptation only — tracks which ports have already had their 16
    // slot files materialized on disk this run, so a present-but-untouched card doesn't re-check
    // 16 files' existence every time _card_info polls it (every frame while probing).
    private static readonly HashSet<int> MaterializedPorts = new();

    // JUSTIFICATION: PSX hardware adaptation only — folders live next to the executable so the
    // backend behaves the same whether the game is launched from an IDE or a double-click, not
    // wherever the current working directory happens to be.
    private static string GetCardFolder(int port)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, $"memorycard{port + 1}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string GetSlotPath(int port, int block) =>
        Path.Combine(GetCardFolder(port), $"slot{block}.bin");

    // JUSTIFICATION: PSX hardware adaptation only — a freshly-present card's 16 blocks all exist
    // as real, inspectable slotX.bin files up front (blank/unformatted) rather than appearing only
    // once something happens to read/write them; existing files are left untouched so save data
    // already on disk survives across runs.
    private static void EnsureAllSlotsMaterialized(int port)
    {
        if (!MaterializedPorts.Add(port))
        {
            return;
        }

        for (int block = 0; block < BlockCount; block++)
        {
            string path = GetSlotPath(port, block);
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, new byte[BlockSizeBytes]);
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — a slot file that doesn't exist yet (e.g. it
    // was deleted on disk after materialization) represents blank/unformatted storage, not a
    // missing card; it's recreated zero-filled rather than treated as an error.
    public static byte[] ReadCardBlock(int port, int block)
    {
        string path = GetSlotPath(port, block);
        if (!File.Exists(path))
        {
            byte[] blank = new byte[BlockSizeBytes];
            File.WriteAllBytes(path, blank);
            return blank;
        }

        byte[] data = File.ReadAllBytes(path);
        if (data.Length == BlockSizeBytes)
        {
            return data;
        }

        byte[] fixedSize = new byte[BlockSizeBytes];
        Array.Copy(data, fixedSize, Math.Min(data.Length, BlockSizeBytes));
        return fixedSize;
    }

    // JUSTIFICATION: PSX hardware adaptation only — writes always produce a full BlockSizeBytes
    // file (short writes are zero-padded) so every slotX.bin on disk stays a fixed-size, directly
    // card-block-shaped file.
    public static void WriteCardBlock(int port, int block, byte[] data, int length)
    {
        byte[] full = new byte[BlockSizeBytes];
        Array.Copy(data, full, Math.Min(length, BlockSizeBytes));
        File.WriteAllBytes(GetSlotPath(port, block), full);
    }

    // JUSTIFICATION: PSX hardware adaptation only — both memory-card ports are unconditionally
    // considered present per the task; this also materializes the port's folder and all 16 slot
    // files on first check.
    public static bool CardIsPresent(int port)
    {
        EnsureAllSlotsMaterialized(port);
        return true;
    }

    // JUSTIFICATION: PSX hardware adaptation only — real _card_format erases all 16 blocks of a
    // card; here that's just rewriting each of its 16 slot files as blank.
    public static void FormatCard(int port)
    {
        byte[] blank = new byte[BlockSizeBytes];
        for (int block = 0; block < BlockCount; block++)
        {
            File.WriteAllBytes(GetSlotPath(port, block), blank);
        }
    }

    // =========================================================================
    // BIOS card file system (open/read/write/lseek/close/erase/format/firstfile/nextfile)
    // =========================================================================
    // JUSTIFICATION: PSX hardware adaptation only — the BIOS exposes the memory card BOTH as 16 raw
    // blocks (_card_read/_card_write above) and as a tiny named-file system on the "bu00:"/"bu10:"
    // devices. UpdateMemoryCardSlotStateMachine uses the FILE side exclusively: it enumerates saves
    // with firstfile/nextfile, and opens/reads/writes/erases them by name
    // ("BASLUS-00662000000<c><c>"). Every one of those calls was a `return default` stub in
    // LibApi.cs, so the browser had nothing to enumerate.
    // Each save is stored as its own file inside the port's existing memorycardN folder, next to
    // the slotX.bin blocks — same "trivially inspectable on disk" principle as the block backend,
    // and it keeps the two BIOS APIs modelled the way the BIOS itself presents them rather than
    // reimplementing the card's on-card directory format for no observable gain.
    private sealed class OpenFile
    {
        public string Path;
        public byte[] Data;
        public int Position;
        public bool Dirty;
    }

    private static readonly System.Collections.Generic.Dictionary<int, OpenFile> OpenFiles = new();
    private static int s_nextFd = 3;
    private static string[] s_dirScan;
    private static int s_dirScanIndex;

    // JUSTIFICATION: PSX hardware adaptation only — the device prefix "bu%d0:" carries the port in
    // its third character (the game writes it with `PTR_s_bu_0___80092230[2] = cardIndex + '0'`).
    // Returns -1 when the name does not address a memory card at all.
    private static int PortFromDeviceName(string name)
    {
        if (name == null || name.Length < 5 || !name.StartsWith("bu"))
        {
            return -1;
        }

        return name[2] == '1' ? 1 : 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only — strips the "buN0:" device prefix, leaving the
    // bare file name the card directory stores.
    private static string FileNameOnly(string name)
    {
        int colon = name.IndexOf(':');
        return colon < 0 ? name : name.Substring(colon + 1);
    }

    private static string SaveFilePath(int port, string fileName) =>
        Path.Combine(GetCardFolder(port), fileName);

    // JUSTIFICATION: PSX hardware adaptation only — the BIOS open() flags this game uses: bit0 read,
    // bit1 write, 0x200 create, with the file size in 8 KB blocks in the high half-word (so its
    // `open(name, 0x10200)` means "create a 1-block file"). Returns a descriptor >= 0, or -1.
    public static int CardFileOpen(string deviceAndName, int flag)
    {
        int port = PortFromDeviceName(deviceAndName);
        if (port < 0)
        {
            return -1;
        }

        CardIsPresent(port);
        string fileName = FileNameOnly(deviceAndName);
        string path = SaveFilePath(port, fileName);
        bool create = (flag & 0x200) != 0;
        if (!File.Exists(path))
        {
            if (!create)
            {
                return -1;
            }

            int blocks = (flag >> 16) & 0xffff;
            if (blocks <= 0)
            {
                blocks = 1;
            }

            File.WriteAllBytes(path, new byte[blocks * BlockSizeBytes]);
        }
        else if (create)
        {
            // The BIOS refuses to create a file that already exists; the game relies on that to
            // test whether a save slot is taken (state 4 opens with the create flags and treats
            // success as "the slot was free").
            return -1;
        }

        int fd = s_nextFd++;
        OpenFiles[fd] = new OpenFile { Path = path, Data = File.ReadAllBytes(path), Position = 0 };
        return fd;
    }

    public static int CardFileRead(int fd, byte[] dest, int destOffset, int length)
    {
        if (!OpenFiles.TryGetValue(fd, out OpenFile file) || dest == null || length <= 0)
        {
            return -1;
        }

        int available = file.Data.Length - file.Position;
        if (available <= 0)
        {
            return 0;
        }

        int count = Math.Min(length, available);
        if (destOffset < 0 || destOffset + count > dest.Length)
        {
            count = Math.Max(0, Math.Min(count, dest.Length - destOffset));
        }

        Array.Copy(file.Data, file.Position, dest, destOffset, count);
        file.Position += count;
        return count;
    }

    public static int CardFileWrite(int fd, byte[] source, int sourceOffset, int length)
    {
        if (!OpenFiles.TryGetValue(fd, out OpenFile file) || source == null || length <= 0)
        {
            return -1;
        }

        int end = file.Position + length;
        if (end > file.Data.Length)
        {
            Array.Resize(ref file.Data, end);
        }

        int count = Math.Min(length, source.Length - sourceOffset);
        if (count <= 0)
        {
            return -1;
        }

        Array.Copy(source, sourceOffset, file.Data, file.Position, count);
        file.Position += count;
        file.Dirty = true;
        return count;
    }

    public static int CardFileSeek(int fd, int offset, int whence)
    {
        if (!OpenFiles.TryGetValue(fd, out OpenFile file))
        {
            return -1;
        }

        file.Position = whence switch
        {
            1 => file.Position + offset,
            2 => file.Data.Length + offset,
            _ => offset,
        };
        if (file.Position < 0)
        {
            file.Position = 0;
        }

        return file.Position;
    }

    public static int CardFileClose(int fd)
    {
        if (!OpenFiles.TryGetValue(fd, out OpenFile file))
        {
            return -1;
        }

        if (file.Dirty)
        {
            File.WriteAllBytes(file.Path, file.Data);
        }

        OpenFiles.Remove(fd);
        return fd;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the BIOS erase() returns non-zero on success.
    public static int CardFileErase(string deviceAndName)
    {
        int port = PortFromDeviceName(deviceAndName);
        if (port < 0)
        {
            return 0;
        }

        string path = SaveFilePath(port, FileNameOnly(deviceAndName));
        if (!File.Exists(path))
        {
            return 0;
        }

        File.Delete(path);
        return 1;
    }

    // JUSTIFICATION: PSX hardware adaptation only — format() wipes the card. Removes every save
    // file and blanks the 16 raw blocks, so both BIOS views of the card agree afterwards.
    public static int CardFileFormat(string deviceName)
    {
        int port = PortFromDeviceName(deviceName);
        if (port < 0)
        {
            return 0;
        }

        foreach (string path in Directory.GetFiles(GetCardFolder(port)))
        {
            if (!Path.GetFileName(path).StartsWith("slot"))
            {
                File.Delete(path);
            }
        }

        FormatCard(port);
        return 1;
    }

    // JUSTIFICATION: PSX hardware adaptation only — firstfile/nextfile walk the card directory and
    // fill a DIRENTRY. The game only reads the 20-byte name and the entry's size; both are filled.
    // Returns 0 when the directory is exhausted, matching the BIOS's NULL return.
    public static int CardFileFirst(string devicePattern, DIRENTRY dir)
    {
        int port = PortFromDeviceName(devicePattern);
        if (port < 0)
        {
            s_dirScan = null;
            return 0;
        }

        CardIsPresent(port);
        s_dirScanPort = port;
        var names = new System.Collections.Generic.List<string>();
        foreach (string path in Directory.GetFiles(GetCardFolder(port)))
        {
            string name = Path.GetFileName(path);
            if (!name.StartsWith("slot"))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        s_dirScan = names.ToArray();
        s_dirScanIndex = 0;
        return CardFileNext(dir);
    }

    public static int CardFileNext(DIRENTRY dir)
    {
        if (s_dirScan == null || s_dirScanIndex >= s_dirScan.Length || dir == null)
        {
            return 0;
        }

        string name = s_dirScan[s_dirScanIndex++];
        Array.Clear(dir.name, 0, dir.name.Length);
        // The bound is name.Length, NOT name.Length - 1: DIRENTRY.name is `char name[20]` and the
        // BIOS fills all twenty when the name needs them — it does not reserve a terminator. Parasite
        // Eve's own save names are exactly 20 characters ("BASLUS-00662" + 8), so reserving one
        // dropped the LAST character, which is precisely the slot letter the card scan derives the
        // slot index from ('A' + slot). MarkScannedSlot then computed '0' - 'A' = -17, rejected it,
        // and every existing save stayed marked "unused file" — the save you had just written was
        // invisible when you came back. Shorter names still end up NUL-terminated by the Array.Clear
        // above.
        for (int i = 0; i < name.Length && i < dir.name.Length; i++)
        {
            dir.name[i] = (byte)name[i];
        }

        dir.status = 0;
        dir.reserved[0] = (int)new FileInfo(SaveFilePath(PortOfCurrentScan(), name)).Length;
        return 1;
    }

    // JUSTIFICATION: C# language bridge only — CardFileNext needs the port the current scan belongs
    // to; the BIOS keeps that in its own directory-walk state.
    private static int s_dirScanPort;
    private static int PortOfCurrentScan() => s_dirScanPort;

    public static int MemCardAccept(int chan)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static MemCB MemCardCallback(MemCB func)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static void MemCardClose()
    {
        // Do nothing PSX SDK
    }

    public static int MemCardCreateFile(int chan, object file, int blocks)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int MemCardDeleteFile(int chan, object file)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static void MemCardEnd()
    {
        // Do nothing PSX SDK
    }

    // JUSTIFICATION: PSX hardware adaptation only — both memory cards are unconditionally present
    // (see CardIsPresent above); chan encodes the port as (port<<4), matching every _card_* call
    // site in this port.
    public static int MemCardExist(int chan)
    {
        int port = (chan >> 4) & 1;
        return CardIsPresent(port) ? 1 : 0;
    }

    public static int MemCardFormat(int chan)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int MemCardGetDirentry(int chan, object name, object dir, object files, int ofs, int max)
    {
        // Do nothing PSX SDK
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation only — ensures both virtual cards' folders exist as
    // soon as the memory-card subsystem initializes, mirroring real hardware always having 2
    // physical card slots to probe.
    public static void MemCardInit(int val)
    {
        CardIsPresent(0);
        CardIsPresent(1);
    }

    public static int MemCardOpen(int chan, object file, int flag)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int MemCardReadData(object adrs, int offset, int bytes)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int MemCardReadFile(int chan, object file, object adrs, int offset, int bytes)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static void MemCardStart()
    {
        // Do nothing PSX SDK
    }

    public static void MemCardStop()
    {
        // Do nothing PSX SDK
    }

    public static int MemCardSync(int mode, object cmds, object result)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int MemCardUnformat(int chan)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int MemCardWriteData(object adrs, int offset, int bytes)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int MemCardWriteFile(int chan, object file, object adrs, int offset, int bytes)
    {
        // Do nothing PSX SDK
        return default;
    }
}
