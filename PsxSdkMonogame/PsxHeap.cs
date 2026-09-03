using System;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: desktop stand-in for the PsyQ heap that InitHeap, malloc and free operate on.
//
// This is deliberately NOT a transliteration of the PsyQ allocator. malloc @ 0x800591A0 is 464
// bytes leaning on _ExpAllocArea, _expand and several MALLOC_OBJ_* fragments that Ghidra splits
// badly, and rule 13 of the port mandate says not to transliterate PSX SDK routines as if they
// were game runtime. What the game actually depends on is the observable contract, which this
// reproduces:
//
//   - InitHeap(base, size) arms a heap of `size` bytes at PSX address `base`;
//   - malloc returns a 4-aligned address inside that heap, or 0 when it cannot serve the request;
//   - free returns a block to the heap;
//   - the memory handed back is addressable through PsxRam like any other modelled PSX range.
//
// The one accepted difference is that the addresses handed out are not the same ones the console
// would hand out. No observed call site depends on their value: CreateTask @ 0x80049504 only
// checks the result against 0 and -1 before storing it.
//
// Block layout, 8-byte header followed by the payload:
//   +0x00  int  payload size in bytes, always a multiple of 4
//   +0x04  int  1 when the block is handed out, 0 when it is free
//   +0x08  payload, whose address is what malloc returns
public static class PsxHeap
{
    private const int HeaderSize = 8;
    private const int MinPayload = 4;

    private static byte[] s_storage = Array.Empty<byte>();
    private static int s_baseAddress;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: lets the game's address resolver map the heap range like any other PSX span.
    public static (byte[] buffer, int offset)? Resolve(int address)
    {
        if (s_storage.Length == 0)
        {
            return null;
        }

        int offset = address - s_baseAddress;
        return offset >= 0 && offset < s_storage.Length ? (s_storage, offset) : null;
    }

    // GHIDRA: InitHeap @ 0x80059160 (TITLE.EXE)
    // The original records the head pointer, zeroes its first word, and derives an end pointer and
    // a remaining size. Here that collapses to arming one span and laying a single free block over
    // it, which is the same observable starting state: everything available, nothing handed out.
    public static void InitHeap(int baseAddress, int size)
    {
        if (size < HeaderSize + MinPayload)
        {
            // THE PREVIOUS SPAN MUST NOT SURVIVE IN THE REGISTRY. This path emptied s_storage and
            // returned, leaving the old heap's RamRegion row registered with nobody holding the
            // buffer. On the game path SELECT.EXE arms 0x78ED5C bytes at 0x800692A0, then VS.EXE's
            // start arms with a negative size and lands here: SELECT's heap stayed as a ZOMBIE row
            // in the global registry, and since VS_EXE's resolver consults RamResolve first, every
            // address above 0x800692A0 that no higher-based region claimed — the image's tables,
            // FighterSetup's slot, FileIo's buffers — was served zeros out of a dead overlay's heap.
            // Found by a fresh-context refutation that replayed the production InitHeap arguments;
            // the same release the re-arm path below already does.
            LibGpu.RamRelease(s_storage);
            s_storage = Array.Empty<byte>();
            s_baseAddress = 0;
            return;
        }

        s_baseAddress = baseAddress;

        // JUSTIFICATION: PSX hardware adaptation only
        // RELATION: InitHeap allocates nothing on the console - it records a base and a size over
        // RAM that already exists - so re-arming must not leave the previous span registered.
        // Keeping the same array when the size is unchanged is the whole of it for TITLE.EXE,
        // which arms 0x10000 bytes both times; the release covers a size change.
        if (s_storage.Length == size)
        {
            System.Array.Clear(s_storage, 0, s_storage.Length);
        }
        else
        {
            LibGpu.RamRelease(s_storage);
            s_storage = new byte[size];
        }

        // JUSTIFICATION: PSX hardware adaptation only
        // RELATION: the heap is a span of PSX RAM like any other, so it declares its address to
        // LibGpu's registry too. Without this a primitive that malloc handed out has no address
        // AddPrim can splice into an ordering-table bucket, and it silently never draws. TITLE.EXE
        // reaches exactly that case: the title task FUN_80021e28 @ 0x80021E28 keeps its two
        // background quads in its heap-allocated task context.
        LibGpu.RamRegion(baseAddress, s_storage);

        MipsMemory.WriteI32(s_storage, 0, size - HeaderSize);
        MipsMemory.WriteI32(s_storage, 4, 0);
    }

    // GHIDRA: malloc @ 0x800591A0 (TITLE.EXE) — observable contract only, see the remark above.
    // First fit, splitting the block when the remainder can still carry a header and a payload.
    // Returns 0 when no block fits, which is the failure value every observed call site tests.
    public static int Malloc(int size)
    {
        if (s_storage.Length == 0 || size < 0)
        {
            return 0;
        }

        int wanted = (size + 3) & ~3;
        if (wanted < MinPayload)
        {
            wanted = MinPayload;
        }

        int block = 0;
        while (block + HeaderSize <= s_storage.Length)
        {
            int payload = MipsMemory.ReadI32(s_storage, block);
            bool inUse = MipsMemory.ReadI32(s_storage, block + 4) != 0;

            if (!inUse && payload >= wanted)
            {
                int leftover = payload - wanted;
                if (leftover >= HeaderSize + MinPayload)
                {
                    int split = block + HeaderSize + wanted;
                    MipsMemory.WriteI32(s_storage, split, leftover - HeaderSize);
                    MipsMemory.WriteI32(s_storage, split + 4, 0);
                    MipsMemory.WriteI32(s_storage, block, wanted);
                }

                MipsMemory.WriteI32(s_storage, block + 4, 1);
                return s_baseAddress + block + HeaderSize;
            }

            block += HeaderSize + payload;
            if (payload <= 0)
            {
                break;
            }
        }

        return 0;
    }

    // GHIDRA: free @ 0x800593D4 (TITLE.EXE) — observable contract only.
    // Marks the block free, then coalesces every run of adjacent free blocks so a long
    // create/delete cycle cannot fragment the heap into unusable slivers.
    public static void Free(int address)
    {
        if (s_storage.Length == 0 || address == 0)
        {
            return;
        }

        int block = address - s_baseAddress - HeaderSize;
        if (block < 0 || block + HeaderSize > s_storage.Length)
        {
            return;
        }

        MipsMemory.WriteI32(s_storage, block + 4, 0);
        Coalesce();
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: merges adjacent free blocks. The PsyQ allocator keeps its own free list; this port
    // walks the span instead, which is cheap at this heap size and keeps the block layout simple.
    private static void Coalesce()
    {
        int block = 0;
        while (block + HeaderSize <= s_storage.Length)
        {
            int payload = MipsMemory.ReadI32(s_storage, block);
            if (payload <= 0)
            {
                break;
            }

            if (MipsMemory.ReadI32(s_storage, block + 4) == 0)
            {
                int next = block + HeaderSize + payload;
                while (next + HeaderSize <= s_storage.Length
                       && MipsMemory.ReadI32(s_storage, next) > 0
                       && MipsMemory.ReadI32(s_storage, next + 4) == 0)
                {
                    payload += HeaderSize + MipsMemory.ReadI32(s_storage, next);
                    MipsMemory.WriteI32(s_storage, block, payload);
                    next = block + HeaderSize + payload;
                }
            }

            block += HeaderSize + MipsMemory.ReadI32(s_storage, block);
        }
    }
}
