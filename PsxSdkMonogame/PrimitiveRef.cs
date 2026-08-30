namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: a primitive on the console is a packet in RAM, not an object. AddPrim writes the
// packet's real PSX address into an ordering-table bucket, and the rasterizer walks those links
// back to bytes, so a primitive that has no address cannot be drawn.
//
// These wrappers give the same field names the original source used, over real memory. A call site
// keeps reading `p.tpage = 0x50;` while the storage underneath is the byte packet the GPU sees.
//
// They also cover a case an object cannot express at all. TITLE.EXE's title task
// FUN_80021e28 @ 0x80021E28 takes a three-slot POLY_FT4 array as its task context and uses the
// third slot purely as scratch state: it reads r0 and g0 together as one 16-bit value, reads the
// high half of `tag` as a frame counter, and keeps a screen offset in x0. Those reads cross field
// boundaries, so only bytes can hold them.
//
// Offsets are the psyq packet layouts, cross-checked against LibGpu's own rasterizer, which reads
// POLY_GT4 vertices at +8/+20/+32/+44 and colours at +4/+16/+28/+40.

// POLY_FT4: 40 bytes, tag length 9.
public readonly struct POLY_FT4Ref
{
    public const int Size = 40;

    public readonly byte[] Buf;
    public readonly int Offset;

    public POLY_FT4Ref(byte[] buf, int offset)
    {
        Buf = buf;
        Offset = offset;
    }

    // The packet's PSX address, which is what AddPrim splices into the bucket.
    public int Address => LibGpu.RamAddressOf(Buf, Offset);

    // Pointer arithmetic: the original walks its context as `p + 1`, `p[2]`.
    public POLY_FT4Ref this[int index] => new(Buf, Offset + (index * Size));

    public uint tag
    {
        get => MipsMemory.ReadU32(Buf, Offset);
        set => MipsMemory.WriteU32(Buf, Offset, value);
    }

    public byte r0 { get => Buf[Offset + 4]; set => Buf[Offset + 4] = value; }
    public byte g0 { get => Buf[Offset + 5]; set => Buf[Offset + 5] = value; }
    public byte b0 { get => Buf[Offset + 6]; set => Buf[Offset + 6] = value; }
    public byte code { get => Buf[Offset + 7]; set => Buf[Offset + 7] = value; }

    public short x0 { get => MipsMemory.ReadI16(Buf, Offset + 8); set => MipsMemory.WriteI16(Buf, Offset + 8, value); }
    public short y0 { get => MipsMemory.ReadI16(Buf, Offset + 10); set => MipsMemory.WriteI16(Buf, Offset + 10, value); }

    public byte u0 { get => Buf[Offset + 12]; set => Buf[Offset + 12] = value; }
    public byte v0 { get => Buf[Offset + 13]; set => Buf[Offset + 13] = value; }
    public ushort clut { get => MipsMemory.ReadU16(Buf, Offset + 14); set => MipsMemory.WriteU16(Buf, Offset + 14, value); }

    public short x1 { get => MipsMemory.ReadI16(Buf, Offset + 16); set => MipsMemory.WriteI16(Buf, Offset + 16, value); }
    public short y1 { get => MipsMemory.ReadI16(Buf, Offset + 18); set => MipsMemory.WriteI16(Buf, Offset + 18, value); }

    public byte u1 { get => Buf[Offset + 20]; set => Buf[Offset + 20] = value; }
    public byte v1 { get => Buf[Offset + 21]; set => Buf[Offset + 21] = value; }
    public ushort tpage { get => MipsMemory.ReadU16(Buf, Offset + 22); set => MipsMemory.WriteU16(Buf, Offset + 22, value); }

    public short x2 { get => MipsMemory.ReadI16(Buf, Offset + 24); set => MipsMemory.WriteI16(Buf, Offset + 24, value); }
    public short y2 { get => MipsMemory.ReadI16(Buf, Offset + 26); set => MipsMemory.WriteI16(Buf, Offset + 26, value); }

    public byte u2 { get => Buf[Offset + 28]; set => Buf[Offset + 28] = value; }
    public byte v2 { get => Buf[Offset + 29]; set => Buf[Offset + 29] = value; }
    public ushort pad1 { get => MipsMemory.ReadU16(Buf, Offset + 30); set => MipsMemory.WriteU16(Buf, Offset + 30, value); }

    public short x3 { get => MipsMemory.ReadI16(Buf, Offset + 32); set => MipsMemory.WriteI16(Buf, Offset + 32, value); }
    public short y3 { get => MipsMemory.ReadI16(Buf, Offset + 34); set => MipsMemory.WriteI16(Buf, Offset + 34, value); }

    public byte u3 { get => Buf[Offset + 36]; set => Buf[Offset + 36] = value; }
    public byte v3 { get => Buf[Offset + 37]; set => Buf[Offset + 37] = value; }
    public ushort pad2 { get => MipsMemory.ReadU16(Buf, Offset + 38); set => MipsMemory.WriteU16(Buf, Offset + 38, value); }

    // JUSTIFICATION: C# language bridge only
    // RELATION: the original reads and writes halfwords that span two named fields, or that land on
    // a field's high half. C# cannot alias a byte pair as a short, so those go through the packet
    // directly. The byte offset is the one the original's own pointer arithmetic produces.
    public short ReadHalf(int byteOffset) => MipsMemory.ReadI16(Buf, Offset + byteOffset);

    public void WriteHalf(int byteOffset, int value) => MipsMemory.WriteI16(Buf, Offset + byteOffset, value);
}

// POLY_GT4: 52 bytes, tag length 12.
public readonly struct POLY_GT4Ref
{
    public const int Size = 52;

    public readonly byte[] Buf;
    public readonly int Offset;

    public POLY_GT4Ref(byte[] buf, int offset)
    {
        Buf = buf;
        Offset = offset;
    }

    public int Address => LibGpu.RamAddressOf(Buf, Offset);

    public POLY_GT4Ref this[int index] => new(Buf, Offset + (index * Size));

    public uint tag
    {
        get => MipsMemory.ReadU32(Buf, Offset);
        set => MipsMemory.WriteU32(Buf, Offset, value);
    }

    public byte r0 { get => Buf[Offset + 4]; set => Buf[Offset + 4] = value; }
    public byte g0 { get => Buf[Offset + 5]; set => Buf[Offset + 5] = value; }
    public byte b0 { get => Buf[Offset + 6]; set => Buf[Offset + 6] = value; }
    public byte code { get => Buf[Offset + 7]; set => Buf[Offset + 7] = value; }

    public short x0 { get => MipsMemory.ReadI16(Buf, Offset + 8); set => MipsMemory.WriteI16(Buf, Offset + 8, value); }
    public short y0 { get => MipsMemory.ReadI16(Buf, Offset + 10); set => MipsMemory.WriteI16(Buf, Offset + 10, value); }

    public byte u0 { get => Buf[Offset + 12]; set => Buf[Offset + 12] = value; }
    public byte v0 { get => Buf[Offset + 13]; set => Buf[Offset + 13] = value; }
    public ushort clut { get => MipsMemory.ReadU16(Buf, Offset + 14); set => MipsMemory.WriteU16(Buf, Offset + 14, value); }

    public byte r1 { get => Buf[Offset + 16]; set => Buf[Offset + 16] = value; }
    public byte g1 { get => Buf[Offset + 17]; set => Buf[Offset + 17] = value; }
    public byte b1 { get => Buf[Offset + 18]; set => Buf[Offset + 18] = value; }
    public byte p1 { get => Buf[Offset + 19]; set => Buf[Offset + 19] = value; }

    public short x1 { get => MipsMemory.ReadI16(Buf, Offset + 20); set => MipsMemory.WriteI16(Buf, Offset + 20, value); }
    public short y1 { get => MipsMemory.ReadI16(Buf, Offset + 22); set => MipsMemory.WriteI16(Buf, Offset + 22, value); }

    public byte u1 { get => Buf[Offset + 24]; set => Buf[Offset + 24] = value; }
    public byte v1 { get => Buf[Offset + 25]; set => Buf[Offset + 25] = value; }
    public ushort tpage { get => MipsMemory.ReadU16(Buf, Offset + 26); set => MipsMemory.WriteU16(Buf, Offset + 26, value); }

    public byte r2 { get => Buf[Offset + 28]; set => Buf[Offset + 28] = value; }
    public byte g2 { get => Buf[Offset + 29]; set => Buf[Offset + 29] = value; }
    public byte b2 { get => Buf[Offset + 30]; set => Buf[Offset + 30] = value; }
    public byte p2 { get => Buf[Offset + 31]; set => Buf[Offset + 31] = value; }

    public short x2 { get => MipsMemory.ReadI16(Buf, Offset + 32); set => MipsMemory.WriteI16(Buf, Offset + 32, value); }
    public short y2 { get => MipsMemory.ReadI16(Buf, Offset + 34); set => MipsMemory.WriteI16(Buf, Offset + 34, value); }

    public byte u2 { get => Buf[Offset + 36]; set => Buf[Offset + 36] = value; }
    public byte v2 { get => Buf[Offset + 37]; set => Buf[Offset + 37] = value; }
    public ushort pad2 { get => MipsMemory.ReadU16(Buf, Offset + 38); set => MipsMemory.WriteU16(Buf, Offset + 38, value); }

    public byte r3 { get => Buf[Offset + 40]; set => Buf[Offset + 40] = value; }
    public byte g3 { get => Buf[Offset + 41]; set => Buf[Offset + 41] = value; }
    public byte b3 { get => Buf[Offset + 42]; set => Buf[Offset + 42] = value; }
    public byte p3 { get => Buf[Offset + 43]; set => Buf[Offset + 43] = value; }

    public short x3 { get => MipsMemory.ReadI16(Buf, Offset + 44); set => MipsMemory.WriteI16(Buf, Offset + 44, value); }
    public short y3 { get => MipsMemory.ReadI16(Buf, Offset + 46); set => MipsMemory.WriteI16(Buf, Offset + 46, value); }

    public byte u3 { get => Buf[Offset + 48]; set => Buf[Offset + 48] = value; }
    public byte v3 { get => Buf[Offset + 49]; set => Buf[Offset + 49] = value; }
    public ushort pad3 { get => MipsMemory.ReadU16(Buf, Offset + 50); set => MipsMemory.WriteU16(Buf, Offset + 50, value); }

    public short ReadHalf(int byteOffset) => MipsMemory.ReadI16(Buf, Offset + byteOffset);

    public void WriteHalf(int byteOffset, int value) => MipsMemory.WriteI16(Buf, Offset + byteOffset, value);
}
