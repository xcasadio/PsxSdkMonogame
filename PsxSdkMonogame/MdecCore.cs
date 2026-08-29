using System;
using System.Collections.Generic;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only — the MDEC chip itself, the same role SpuCore plays
// for the SPU: a hardware model (dequantization, IDCT, YCbCr->RGB, macroblock assembly, 16px-wide
// vertical strip output), not a transliteration of a specific original runtime function. LibPress.cs
// is the game-facing DecDCT* API surface and owns all PSX-address/PsxRam plumbing; this class works
// entirely in plain byte[]/ushort[] and knows nothing about PSX memory addresses or the game. Nothing
// here reads or writes any game-layer state — this class is meant to be lifted into other PSX ports
// (any title using the standard "STR"/BS video format) as-is.
//
// Algorithm and constants below were cross-validated two ways before being trusted:
//   1. Bytes actually read out of data/disk-1/SLUS_006.62 at the RE'd table addresses (see the
//      IQ table note below) reproduce exactly from the public STR v2 spec's default matrix.
//   2. The macroblock scan order, block order, bitstream framing, VLC (Huffman) table, dequant
//      formula and RGB conversion formula are all independently documented in the public,
//      long-established PSX STR literature. The implementation follows the MIT-licensed
//      PlayStation1_STR_format specification and uses jPSXdec only as an independent behavioral
//      oracle. Bit-exactness with the console chip is explicitly NOT required; this is a
//      spec-based decoder.
public sealed class MdecCore
{
    // PARTIAL: hardcoded to the 320x240 resolution proven for the currently ported FMVs: all 47
    // Parasite Eve FMV1 movies and all 90 frames of DBZ Legends BANDAI.STR. A title with other
    // dimensions would need these to become per-frame state fed from the St sector header; the
    // demultiplexed BS v2/v3 header itself has no width/height fields.
    public const int FrameWidth = 320;
    public const int FrameHeight = 240;
    public const int MacroblockSize = 16;
    public const int MacroblockCols = FrameWidth / MacroblockSize; // 20
    public const int MacroblockRows = FrameHeight / MacroblockSize; // 15
    public const int MacroblockCount = MacroblockCols * MacroblockRows; // 300

    // One decoded strip = one macroblock column, full frame height, RGB15 (2 bytes/pixel).
    public const int StripPixelBytes = MacroblockSize * FrameHeight * 2; // 7680

    // 24-bit strip = same macroblock column/height, but 3 raw bytes/pixel (no 5:5:5 quantization).
    // This is exactly the byte count the driver's strip-width selector implies: a strip row is 24
    // HALFWORDS (48 bytes) for 16 pixels, so a full-height strip is 24*FrameHeight*2 = 11520 bytes
    // (0x2D00) — see the GHIDRA note on FeedRleList below for where that selector value comes from.
    public const int Strip24PixelBytes = MacroblockSize * FrameHeight * 3; // 11520 (== 24*240*2)

    // GHIDRA: default IQ table data observed at 0x8010DA0C is BSS in the ROM image (all zero bytes
    // on disk — confirmed by reading data/disk-1/SLUS_006.62 at the mapped file offset), so the
    // actual default values are written there at runteim by DecDCTReset, not stored as literal ROM
    // bytes. The RE'd byte fragment handed to this slice ("02 10 10 13 10 13 16 16...") is the
    // *zigzag-order* traversal of the classic MPEG-1 default intra quantizer matrix with DC=2
    // instead of DC=8 (the well-known "PSX MDEC default matrix" variant) — verified below by
    // deriving DefaultIntraMatrixZigZag[0..7] from DefaultIntraMatrixRaster via ZigZagRasterIndex
    // and confirming it reproduces exactly 2,16,16,19,16,19,22,22.
    private static readonly byte[] DefaultIntraMatrixRaster =
    {
        2, 16, 19, 22, 26, 27, 29, 34,
        16, 16, 22, 24, 27, 29, 34, 37,
        19, 22, 26, 27, 29, 34, 34, 38,
        22, 22, 26, 27, 29, 34, 37, 40,
        22, 26, 27, 29, 32, 35, 40, 48,
        26, 27, 29, 32, 35, 40, 48, 58,
        26, 27, 29, 34, 38, 46, 56, 69,
        27, 29, 35, 38, 46, 56, 69, 83,
    };

    // Standard 8x8 zigzag scan: ZigZagRasterIndex[zigzagPos] = raster index (row*8+col) that scan
    // position visits. Used both to build the default IQ table above and to place each decoded
    // (DC-then-AC, zigzag-ordered) coefficient into its natural raster position before the IDCT.
    private static readonly int[] ZigZagRasterIndex =
    {
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    };

    private static readonly byte[] DefaultIntraMatrixZigZag = BuildZigZagMatrix();

    private static byte[] BuildZigZagMatrix()
    {
        var result = new byte[64];
        for (int zz = 0; zz < 64; zz++)
        {
            result[zz] = DefaultIntraMatrixRaster[ZigZagRasterIndex[zz]];
        }

        // Cross-check against the RE'd fragment from the binary (see the field comment above).
        // This runs once at class load; a mismatch here means the zigzag/matrix tables above have
        // been mis-transcribed, so fail loudly rather than silently decoding garbage.
        Span<byte> expectedPrefix = stackalloc byte[] { 2, 16, 16, 19, 16, 19, 22, 22 };
        for (int i = 0; i < expectedPrefix.Length; i++)
        {
            if (result[i] != expectedPrefix[i])
            {
                throw new InvalidOperationException(
                    "MdecCore: default IQ table zigzag derivation does not match the RE'd binary " +
                    $"fragment at index {i} (got {result[i]}, expected {expectedPrefix[i]}).");
            }
        }

        return result;
    }

    // ------------------------------------------------------------------------------------------
    // AC coefficient VLC (Huffman) table — the standard MPEG-1 "DCT coefficients, table zero"
    // (ISO/IEC 11172-2 Annex B), also documented for MDEC by the MIT-licensed
    // PlayStation1_STR_format specification and behaviorally cross-checked with jPSXdec. Each
    // entry is (run, level); the level is a
    // magnitude only — a single sign bit follows every matched code (0=positive, 1=negative). Two
    // codes are handled separately, not through this table: End-of-block ("10") and the fixed-width
    // escape marker ("000001", followed by a raw 6-bit run + 10-bit signed level with no sign bit
    // of its own since the level field is already two's-complement).
    private sealed class AcVlcEntry
    {
        public string Bits;
        public int Run;
        public int Level;
    }

    private static readonly AcVlcEntry[] AcVlcTable =
    {
        new() { Bits = "11", Run = 0, Level = 1 },
        new() { Bits = "011", Run = 1, Level = 1 },
        new() { Bits = "0100", Run = 0, Level = 2 },
        new() { Bits = "0101", Run = 2, Level = 1 },
        new() { Bits = "00101", Run = 0, Level = 3 },
        new() { Bits = "00110", Run = 4, Level = 1 },
        new() { Bits = "00111", Run = 3, Level = 1 },
        new() { Bits = "000100", Run = 7, Level = 1 },
        new() { Bits = "000101", Run = 6, Level = 1 },
        new() { Bits = "000110", Run = 1, Level = 2 },
        new() { Bits = "000111", Run = 5, Level = 1 },
        new() { Bits = "0000100", Run = 2, Level = 2 },
        new() { Bits = "0000101", Run = 9, Level = 1 },
        new() { Bits = "0000110", Run = 0, Level = 4 },
        new() { Bits = "0000111", Run = 8, Level = 1 },
        new() { Bits = "00100000", Run = 13, Level = 1 },
        new() { Bits = "00100001", Run = 0, Level = 6 },
        new() { Bits = "00100010", Run = 12, Level = 1 },
        new() { Bits = "00100011", Run = 11, Level = 1 },
        new() { Bits = "00100100", Run = 3, Level = 2 },
        new() { Bits = "00100101", Run = 1, Level = 3 },
        new() { Bits = "00100110", Run = 0, Level = 5 },
        new() { Bits = "00100111", Run = 10, Level = 1 },
        new() { Bits = "0000001000", Run = 16, Level = 1 },
        new() { Bits = "0000001001", Run = 5, Level = 2 },
        new() { Bits = "0000001010", Run = 0, Level = 7 },
        new() { Bits = "0000001011", Run = 2, Level = 3 },
        new() { Bits = "0000001100", Run = 1, Level = 4 },
        new() { Bits = "0000001101", Run = 15, Level = 1 },
        new() { Bits = "0000001110", Run = 14, Level = 1 },
        new() { Bits = "0000001111", Run = 4, Level = 2 },
        new() { Bits = "000000010000", Run = 0, Level = 11 },
        new() { Bits = "000000010001", Run = 8, Level = 2 },
        new() { Bits = "000000010010", Run = 4, Level = 3 },
        new() { Bits = "000000010011", Run = 0, Level = 10 },
        new() { Bits = "000000010100", Run = 2, Level = 4 },
        new() { Bits = "000000010101", Run = 7, Level = 2 },
        new() { Bits = "000000010110", Run = 21, Level = 1 },
        new() { Bits = "000000010111", Run = 20, Level = 1 },
        new() { Bits = "000000011000", Run = 0, Level = 9 },
        new() { Bits = "000000011001", Run = 19, Level = 1 },
        new() { Bits = "000000011010", Run = 18, Level = 1 },
        new() { Bits = "000000011011", Run = 1, Level = 5 },
        new() { Bits = "000000011100", Run = 3, Level = 3 },
        new() { Bits = "000000011101", Run = 0, Level = 8 },
        new() { Bits = "000000011110", Run = 6, Level = 2 },
        new() { Bits = "000000011111", Run = 17, Level = 1 },
        new() { Bits = "0000000010000", Run = 10, Level = 2 },
        new() { Bits = "0000000010001", Run = 9, Level = 2 },
        new() { Bits = "0000000010010", Run = 5, Level = 3 },
        new() { Bits = "0000000010011", Run = 3, Level = 4 },
        new() { Bits = "0000000010100", Run = 2, Level = 5 },
        new() { Bits = "0000000010101", Run = 1, Level = 7 },
        new() { Bits = "0000000010110", Run = 1, Level = 6 },
        new() { Bits = "0000000010111", Run = 0, Level = 15 },
        new() { Bits = "0000000011000", Run = 0, Level = 14 },
        new() { Bits = "0000000011001", Run = 0, Level = 13 },
        new() { Bits = "0000000011010", Run = 0, Level = 12 },
        new() { Bits = "0000000011011", Run = 26, Level = 1 },
        new() { Bits = "0000000011100", Run = 25, Level = 1 },
        new() { Bits = "0000000011101", Run = 24, Level = 1 },
        new() { Bits = "0000000011110", Run = 23, Level = 1 },
        new() { Bits = "0000000011111", Run = 22, Level = 1 },
        new() { Bits = "00000000010000", Run = 0, Level = 31 },
        new() { Bits = "00000000010001", Run = 0, Level = 30 },
        new() { Bits = "00000000010010", Run = 0, Level = 29 },
        new() { Bits = "00000000010011", Run = 0, Level = 28 },
        new() { Bits = "00000000010100", Run = 0, Level = 27 },
        new() { Bits = "00000000010101", Run = 0, Level = 26 },
        new() { Bits = "00000000010110", Run = 0, Level = 25 },
        new() { Bits = "00000000010111", Run = 0, Level = 24 },
        new() { Bits = "00000000011000", Run = 0, Level = 23 },
        new() { Bits = "00000000011001", Run = 0, Level = 22 },
        new() { Bits = "00000000011010", Run = 0, Level = 21 },
        new() { Bits = "00000000011011", Run = 0, Level = 20 },
        new() { Bits = "00000000011100", Run = 0, Level = 19 },
        new() { Bits = "00000000011101", Run = 0, Level = 18 },
        new() { Bits = "00000000011110", Run = 0, Level = 17 },
        new() { Bits = "00000000011111", Run = 0, Level = 16 },
        new() { Bits = "000000000010000", Run = 0, Level = 40 },
        new() { Bits = "000000000010001", Run = 0, Level = 39 },
        new() { Bits = "000000000010010", Run = 0, Level = 38 },
        new() { Bits = "000000000010011", Run = 0, Level = 37 },
        new() { Bits = "000000000010100", Run = 0, Level = 36 },
        new() { Bits = "000000000010101", Run = 0, Level = 35 },
        new() { Bits = "000000000010110", Run = 0, Level = 34 },
        new() { Bits = "000000000010111", Run = 0, Level = 33 },
        new() { Bits = "000000000011000", Run = 0, Level = 32 },
        new() { Bits = "000000000011001", Run = 1, Level = 14 },
        new() { Bits = "000000000011010", Run = 1, Level = 13 },
        new() { Bits = "000000000011011", Run = 1, Level = 12 },
        new() { Bits = "000000000011100", Run = 1, Level = 11 },
        new() { Bits = "000000000011101", Run = 1, Level = 10 },
        new() { Bits = "000000000011110", Run = 1, Level = 9 },
        new() { Bits = "000000000011111", Run = 1, Level = 8 },
        new() { Bits = "0000000000010000", Run = 1, Level = 18 },
        new() { Bits = "0000000000010001", Run = 1, Level = 17 },
        new() { Bits = "0000000000010010", Run = 1, Level = 16 },
        new() { Bits = "0000000000010011", Run = 1, Level = 15 },
        new() { Bits = "0000000000010100", Run = 6, Level = 3 },
        new() { Bits = "0000000000010101", Run = 16, Level = 2 },
        new() { Bits = "0000000000010110", Run = 15, Level = 2 },
        new() { Bits = "0000000000010111", Run = 14, Level = 2 },
        new() { Bits = "0000000000011000", Run = 13, Level = 2 },
        new() { Bits = "0000000000011001", Run = 12, Level = 2 },
        new() { Bits = "0000000000011010", Run = 11, Level = 2 },
        new() { Bits = "0000000000011011", Run = 31, Level = 1 },
        new() { Bits = "0000000000011100", Run = 30, Level = 1 },
        new() { Bits = "0000000000011101", Run = 29, Level = 1 },
        new() { Bits = "0000000000011110", Run = 28, Level = 1 },
        new() { Bits = "0000000000011111", Run = 27, Level = 1 },
    };

    private const string EndOfBlockBits = "10";
    private const string EscapeBits = "000001";

    // STR v3 DC Huffman tables from the MIT-licensed PlayStation 1 STR format specification.
    // Each tuple is (prefix bits, prefix length, signed differential payload length). Chroma uses
    // the MPEG-1 chroma table; all four luma blocks share the MPEG-1 luma table and one predictor.
    private static readonly (int Code, int CodeLength, int DifferentialLength)[] V3ChromaDcTable =
    {
        (0b00, 2, 0),
        (0b01, 2, 1),
        (0b10, 2, 2),
        (0b110, 3, 3),
        (0b1110, 4, 4),
        (0b11110, 5, 5),
        (0b111110, 6, 6),
        (0b1111110, 7, 7),
        (0b11111110, 8, 8),
    };

    private static readonly (int Code, int CodeLength, int DifferentialLength)[] V3LumaDcTable =
    {
        (0b00, 2, 1),
        (0b01, 2, 2),
        (0b100, 3, 0),
        (0b101, 3, 3),
        (0b110, 3, 4),
        (0b1110, 4, 5),
        (0b11110, 5, 6),
        (0b111110, 6, 7),
        (0b1111110, 7, 8),
    };

    // PERF: binary trie over the AC VLC codes (plus EOB/escape), built once from AcVlcTable — the
    // same codes, same bit strings, just addressed as an int-indexed tree instead of a per-bit
    // string concatenation + Dictionary<string,...> lookup. This is the "precomputed VLC lookup
    // table" optimization the perf pass calls for: it can only change HOW a code is found, never
    // WHICH run/level a given bit sequence decodes to, so RLE output is bit-for-bit identical to
    // the string/dictionary walk it replaces (verified by the perf pass's hash-identity check on
    // real FMV000/FMV001 frames). Node 0 is the root; -1 means "no such child" (a decode error —
    // the original string walk would have fallen through to its own "unmatched after
    // MaxAcCodeLength bits" failure in the same situation).
    private enum AcTrieKind : byte { Internal = 0, Code = 1, EndOfBlock = 2, Escape = 3 }

    private static readonly int[] AcTrieChild0;
    private static readonly int[] AcTrieChild1;
    private static readonly AcTrieKind[] AcTrieKindOf;
    private static readonly int[] AcTrieRun;
    private static readonly int[] AcTrieLevel;

    static MdecCore()
    {
        BuildAcTrie(out AcTrieChild0, out AcTrieChild1, out AcTrieKindOf, out AcTrieRun, out AcTrieLevel);
    }

    private static void BuildAcTrie(out int[] child0, out int[] child1, out AcTrieKind[] kind,
        out int[] run, out int[] level)
    {
        // Upper bound on trie nodes: one internal node per bit of every code, worst case (no shared
        // prefixes) is the sum of all code lengths. Cheap to over-allocate and trim.
        int capacity = 1;
        foreach (AcVlcEntry e in AcVlcTable) capacity += e.Bits.Length;
        capacity += EndOfBlockBits.Length + EscapeBits.Length;

        var c0 = new int[capacity];
        var c1 = new int[capacity];
        var k = new AcTrieKind[capacity];
        var r = new int[capacity];
        var lv = new int[capacity];
        Array.Fill(c0, -1);
        Array.Fill(c1, -1);

        int nodeCount = 1; // node 0 = root

        void Insert(string bits, AcTrieKind leafKind, int leafRun, int leafLevel)
        {
            int node = 0;
            for (int i = 0; i < bits.Length; i++)
            {
                bool last = i == bits.Length - 1;
                ref int childSlot = ref (bits[i] == '0' ? ref c0[node] : ref c1[node]);
                if (childSlot < 0)
                {
                    childSlot = nodeCount++;
                }
                else if (last || k[childSlot] != AcTrieKind.Internal)
                {
                    // A prefix collision means the code table isn't prefix-free (or this fragment
                    // was mis-transcribed) — fail loudly rather than silently mis-decoding, same
                    // spirit as the IQ-table cross-check above.
                    throw new InvalidOperationException(
                        $"MdecCore: AC VLC trie has a conflicting/non-prefix-free code at bit " +
                        $"string '{bits}' (node {childSlot} already terminal or reused).");
                }

                node = childSlot;
                if (last)
                {
                    k[node] = leafKind;
                    r[node] = leafRun;
                    lv[node] = leafLevel;
                }
            }
        }

        Insert(EndOfBlockBits, AcTrieKind.EndOfBlock, 0, 0);
        Insert(EscapeBits, AcTrieKind.Escape, 0, 0);
        foreach (AcVlcEntry e in AcVlcTable)
        {
            Insert(e.Bits, AcTrieKind.Code, e.Run, e.Level);
        }

        child0 = c0;
        child1 = c1;
        kind = k;
        run = r;
        level = lv;
    }

    // ------------------------------------------------------------------------------------------
    // Chip state: IQ tables (zigzag order, matching the wire/env layout), the scale (cosine) table
    // (captured for GetEnv/PutEnv round-trip fidelity only — see the JUSTIFICATION on the IDCT
    // below for why this port's IDCT does not actually consume it), the vlc output-size guard, and
    // the queue of fully-decoded strips waiting to be drained by DecDCTout.
    // ------------------------------------------------------------------------------------------

    private byte[] _iqY = new byte[64];
    private byte[] _iqC = new byte[64];
    private short[] _scale = new short[64];
    private int _vlcMaxWords;

    private readonly Queue<byte[]> _pendingStrips = new();

    public MdecCore()
    {
        Reset();
    }

    // GHIDRA: DecDCTReset core @ 0x8010C0FC (LibPress.DecDCTReset wraps this at 0x8010BE3C)
    // PROOF: CERTAIN interface — re-arms the default IQ + scale tables and clears any in-flight
    // decode state. mode==0 additionally clearing DMA callbacks is LibPress's job (LibEtc owns the
    // callback registry, not this chip model).
    public void Reset()
    {
        Array.Copy(DefaultIntraMatrixZigZag, _iqY, 64);
        Array.Copy(DefaultIntraMatrixZigZag, _iqC, 64);
        Array.Clear(_scale, 0, _scale.Length);
        _vlcMaxWords = 0;
        _pendingStrips.Clear();
    }

    public void SetVlcMaxWords(int n) => _vlcMaxWords = n;

    public void GetEnv(out byte[] iqY, out byte[] iqC, out short[] scale)
    {
        iqY = (byte[])_iqY.Clone();
        iqC = (byte[])_iqC.Clone();
        scale = (short[])_scale.Clone();
    }

    public void PutEnv(byte[] iqY, byte[] iqC, short[] scale)
    {
        if (iqY != null) Array.Copy(iqY, _iqY, Math.Min(64, iqY.Length));
        if (iqC != null) Array.Copy(iqC, _iqC, Math.Min(64, iqC.Length));
        if (scale != null) Array.Copy(scale, _scale, Math.Min(64, scale.Length));
    }

    // ------------------------------------------------------------------------------------------
    // MSB-first bit reader over 16-bit little-endian words — the standard STR-bitstream convention
    // (re-derived from jPSXdec's ArrayBitReader / LITTLE_ENDIAN_SHORT_ORDER during this slice's
    // discovery): the byte stream is read two bytes at a time as a little-endian ushort, and bits
    // are consumed from that ushort's bit 15 down to bit 0 before the next word is fetched.
    // ------------------------------------------------------------------------------------------
    private sealed class BitReader
    {
        private readonly byte[] _data;
        private int _byteOffset;
        private uint _current;
        private int _bitsLeftInWord;
        public bool Overrun { get; private set; }

        public BitReader(byte[] data, int startOffset)
        {
            _data = data;
            _byteOffset = startOffset;
            _bitsLeftInWord = 0;
        }

        private void FetchWord()
        {
            if (_byteOffset + 1 >= _data.Length)
            {
                Overrun = true;
                _current = 0;
                _bitsLeftInWord = 16;
                return;
            }

            _current = (uint)(_data[_byteOffset] | (_data[_byteOffset + 1] << 8));
            _byteOffset += 2;
            _bitsLeftInWord = 16;
        }

        public int ReadBit()
        {
            if (_bitsLeftInWord == 0)
            {
                FetchWord();
            }

            _bitsLeftInWord--;
            return (int)((_current >> _bitsLeftInWord) & 1);
        }

        // S2 ADVISORY (2026-08-29): bitstream-exhaustion check, scoped to the CURRENTLY BUFFERED
        // 16-bit word only. `_data` is not "this frame's bytes and nothing else" — DecDCTvlc2 hands
        // this reader the whole rest of its backing scratch region (see LibPress.DecDCTvlc2's own
        // note: "no length this call needs up front", so the caller doesn't trim the buffer to the
        // frame's real end), which on real ring/scratch memory contains unrelated bytes (a later
        // frame already demuxed into the same ring, or stale scratch data) past the true end of
        // THIS frame's bitstream. Scanning all the way to `_data.Length` therefore false-positived
        // on real frames (confirmed empirically this slice on FMV000 frame 150 and FMV001 frame
        // 400 — both legitimate frames, not corrupt). What real MDEC hardware actually guarantees is
        // word-alignment padding: once the last macroblock's last code is read, any bits still
        // sitting in the word already fetched off the wire must be padding (zero) — bits beyond
        // that boundary were never fetched from this frame's stream at all, so they carry no
        // meaning to check. Re-verified against every frame of both FMV000 and FMV001 with this
        // narrower scope that it never legitimately fires on real data.
        public bool HasNonZeroTrailingData()
        {
            if (_bitsLeftInWord <= 0)
            {
                return false;
            }

            uint mask = (1u << _bitsLeftInWord) - 1;
            return (_current & mask) != 0;
        }

        public int ReadBits(int count)
        {
            int v = 0;
            for (int i = 0; i < count; i++)
            {
                v = (v << 1) | ReadBit();
            }

            return v;
        }

        public int ReadSigned(int bitCount)
        {
            int v = ReadBits(bitCount);
            int signBit = 1 << (bitCount - 1);
            return (v & signBit) != 0 ? v - (signBit << 1) : v;
        }
    }

    // GHIDRA: DecDCTvlc2 @ 0x8010C89C (LibPress-facing; this method is the actual entropy decoder
    // LibPress.DecDCTvlc2 calls into after resolving PSX addresses to plain bytes)
    // PROOF: spec-based, not a transliteration — see the class header JUSTIFICATION. `frameData`
    // is the full demuxed BS frame (8-byte header included, exactly as the St ring hands frames to
    // the game and as this slice's FmvDecode harness feeds it straight through).
    //
    // BS v2/v3 8-byte header, confirmed against real PE v2 and DBZ BANDAI v3 frames: u16 length (an
    // MDEC-code-count hint used for buffer sizing on console), u16 magic 0x3800, u16 frame qscale,
    // and u16 version. Bytes 8+ are the entropy-coded bitstream. Version 2 stores absolute 10-bit
    // DC values; version 3 uses the standard differential DC tables below.
    public ushort[] VlcDecode(byte[] frameData, out int macroblocksDecoded, out string error)
    {
        macroblocksDecoded = 0;
        error = null;

        if (frameData == null || frameData.Length < 8)
        {
            error = "frame data too short for the 8-byte BS header";
            return null;
        }

        int length = frameData[0] | (frameData[1] << 8);
        int magic = frameData[2] | (frameData[3] << 8);
        int qscale = frameData[4] | (frameData[5] << 8);
        int version = frameData[6] | (frameData[7] << 8);

        if (magic != 0x3800)
        {
            error = $"BS header magic mismatch: got 0x{magic:x4}, expected 0x3800";
            return null;
        }

        if (version != 2 && version != 3)
        {
            error = $"unsupported BS version {version} (only v2 and v3 are implemented)";
            return null;
        }

        var reader = new BitReader(frameData, 8);
        var output = new List<ushort>();
        int previousCrDc = 0;
        int previousCbDc = 0;
        int previousYDc = 0;

        // Macroblock scan order: column-major (confirmed against jPSXdec's ParsedMdecImage.java
        // during this slice's discovery: outer loop over X/columns, inner loop over Y/rows) — this
        // is also exactly what makes DecDCTout's 16px-wide vertical strip contract free: each
        // completed macroblock column IS one strip, in decode order, with no reassembly needed.
        for (int col = 0; col < MacroblockCols; col++)
        {
            for (int row = 0; row < MacroblockRows; row++)
            {
                // Block order within a macroblock: Cr, Cb, Y1(TL), Y2(TR), Y3(BL), Y4(BR).
                for (int blockIndex = 0; blockIndex < 6; blockIndex++)
                {
                    if (!DecodeBlockToRle(reader, qscale, version, blockIndex,
                            ref previousCrDc, ref previousCbDc, ref previousYDc, output))
                    {
                        error = $"VLC decode failed at macroblock (col={col},row={row}) block {blockIndex}, " +
                                $"after {output.Count} RLE words (header length hint was {length})";
                        macroblocksDecoded = col * MacroblockRows + row;
                        return null;
                    }

                    if (_vlcMaxWords > 0 && output.Count > _vlcMaxWords)
                    {
                        error = $"VLC output exceeded DecDCTvlcSize cap ({_vlcMaxWords} words) at " +
                            $"macroblock (col={col},row={row}) block {blockIndex}";
                        macroblocksDecoded = col * MacroblockRows + row;
                        return null;
                    }
                }
            }
        }

        // S2 ADVISORY (2026-08-29): bitstream-exhaustion check — see BitReader.
        // HasNonZeroTrailingData's own note. MEASURED AND DELIBERATELY NOT HARD-FAILED: on every
        // real frame sampled from both movies, the bits immediately following the last macroblock's
        // last code are non-zero (e.g. FMV000 frame 2: word 0x049f with 6 bits still unread; FMV000
        // frame 75: word 0x3a7f with 8 bits unread) — `frameData` is not trimmed to this frame's own
        // encoded length (LibPress.DecDCTvlc2 hands this decoder the whole rest of its backing
        // scratch region, by design — see that method's own header note), so what follows the
        // picture's last code is genuine subsequent stream content (the next frame's own data
        // already sitting in the same scratch/ring buffer), not this frame's padding. There is no
        // reliable all-zero invariant to assert here without the true per-frame byte length, which
        // this self-terminating (macroblock-count-driven) decoder does not have and, per the class
        // header's own JUSTIFICATION, deliberately does not require. Hard-failing on this would
        // reject every real frame tested — logged as a diagnostic only, never a decode failure.
        if (reader.HasNonZeroTrailingData())
        {
            Console.WriteLine("[MdecCore] NOTE: non-zero data follows the last macroblock's last " +
                               "code (expected — see VlcDecode's bitstream-exhaustion note).");
        }

        macroblocksDecoded = MacroblockCount;
        return output.ToArray();
    }

    // One block: DC (10-bit signed absolute value for v2, differential Huffman value for v3) then
    // AC run/level pairs
    // (MPEG-1 table zero VLC, PSX 6-bit-run/10-bit-level escape, "10" end-of-block), each coefficient
    // packed into the same 16-bit MdecCode wire format DecDCTin consumes: bits 15-10 = run (DC word:
    // qscale, per the real chip's own convention — see LibPress.DecDCTin's header note), bits 9-0 =
    // value (two's complement). Block is terminated in the output list with the sentinel 0xFE00
    // (top6=0x3F, bottom10=0x200) — the well-established PSX MDEC end-of-block marker value.
    private bool DecodeBlockToRle(BitReader reader, int qscale, int version, int blockIndex,
        ref int previousCrDc, ref int previousCbDc, ref int previousYDc, List<ushort> output)
    {
        int dc;
        if (version == 2)
        {
            dc = reader.ReadSigned(10);
            if (reader.Overrun) return false;
        }
        else
        {
            bool chroma = blockIndex < 2;
            if (!TryReadV3DcDifference(reader, chroma, out int difference))
            {
                return false;
            }

            if (blockIndex == 0)
            {
                previousCrDc += difference;
                dc = previousCrDc;
            }
            else if (blockIndex == 1)
            {
                previousCbDc += difference;
                dc = previousCbDc;
            }
            else
            {
                previousYDc += difference;
                dc = previousYDc;
            }

            if (dc < -512 || dc > 511)
            {
                return false;
            }
        }

        output.Add((ushort)(((qscale & 0x3F) << 10) | (dc & 0x3FF)));

        while (true)
        {
            // PERF: walk the precomputed trie one bit at a time (int-indexed array hops) instead of
            // building a growing string and hashing it against a Dictionary<string,...> after every
            // bit — see the trie's own header note for why this can't change which code matches.
            int node = 0;
            while (true)
            {
                int bit = reader.ReadBit();
                if (reader.Overrun) return false;

                node = bit == 0 ? AcTrieChild0[node] : AcTrieChild1[node];
                if (node < 0)
                {
                    // No such code — mirrors the original's "unmatched after MaxAcCodeLength bits"
                    // failure (a non-prefix-free bit sequence can only mean a corrupt/unsupported
                    // stream, never a longer valid code: the table is verified prefix-free at
                    // startup).
                    return false;
                }

                if (AcTrieKindOf[node] != AcTrieKind.Internal)
                {
                    break;
                }
            }

            switch (AcTrieKindOf[node])
            {
                case AcTrieKind.EndOfBlock:
                    output.Add(0xFE00);
                    return true;

                case AcTrieKind.Escape:
                {
                    int run = reader.ReadBits(6);
                    int level = reader.ReadSigned(10);
                    if (reader.Overrun) return false;
                    output.Add((ushort)(((run & 0x3F) << 10) | (level & 0x3FF)));
                    break;
                }

                default: // AcTrieKind.Code
                {
                    int sign = reader.ReadBit();
                    if (reader.Overrun) return false;
                    int level = sign != 0 ? -AcTrieLevel[node] : AcTrieLevel[node];
                    output.Add((ushort)(((AcTrieRun[node] & 0x3F) << 10) | (level & 0x3FF)));
                    break;
                }
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: standard STR v3 DC Huffman expansion performed by the software VLC stage before
    // the resulting qscale/DC word is sent to MDEC. The differential is shifted from 8-bit to
    // 10-bit precision exactly as specified by the STR format.
    private static bool TryReadV3DcDifference(BitReader reader, bool chroma, out int difference)
    {
        difference = 0;
        var table = chroma ? V3ChromaDcTable : V3LumaDcTable;
        int maxCodeLength = chroma ? 8 : 7;
        int code = 0;

        for (int codeLength = 1; codeLength <= maxCodeLength; codeLength++)
        {
            code = (code << 1) | reader.ReadBit();
            if (reader.Overrun)
            {
                return false;
            }

            for (int tableIndex = 0; tableIndex < table.Length; tableIndex++)
            {
                var entry = table[tableIndex];
                if (entry.CodeLength != codeLength || entry.Code != code)
                {
                    continue;
                }

                if (entry.DifferentialLength == 0)
                {
                    return true;
                }

                int encoded = reader.ReadBits(entry.DifferentialLength);
                if (reader.Overrun)
                {
                    return false;
                }

                int signBit = 1 << (entry.DifferentialLength - 1);
                int differential = (encoded & signBit) == 0
                    ? encoded - ((1 << entry.DifferentialLength) - 1)
                    : encoded;
                difference = differential * 4;
                return true;
            }
        }

        return false;
    }

    // GHIDRA: DecDCTin @ 0x8010BFA0
    // PROOF: interface CERTAIN (feeds an RLE command list into the chip) — ADAPTED internals: real
    // hardware streams macroblocks through a small internal FIFO as DecDCTout is pumped, decoding
    // just-in-time. This port decodes the WHOLE picture eagerly right here (dequant + IDCT + YCbCr
    // ->RGB for all 300 macroblocks), queuing all MacroblockCols strips at once, because (a) this
    // slice's acceptance is visual/statistical correctness, not DMA/IRQ timing, and (b) it makes
    // DecDCTout trivially re-entrancy-safe (a callback calling DecDCTout again from inside DecDCTout
    // just dequeues the next already-decoded strip — see LibPress.DecDCTout).
    //
    // `rleWords` is the RLE command list AFTER the caller (LibPress) has already stripped the u16
    // length prefix — see LibPress.DecDCTin for that framing. mode bit0 selects 15-bit (0) vs
    // 24-bit (1) output depth; mode bit1 sets bit15 (STP) on every output pixel when set (matches
    // "bits 15 = 0 unless cmd bit25 set" from the plan) — STP has no meaning for 24-bit pixels (no
    // spare bit in a packed 3-byte-per-pixel stream) so it is only applied on the 15-bit path.
    //
    // GHIDRA: the script-path FMV DMA callback's first-frame branch (overlay 0x44 @0x80121638-
    // 0x80121690) is the evidence this 24-bit path implements: it `lbu`s g_transitionDirection
    // (0x800B0DBB), `xori`s it 1, `sb`s it back, sets the strip-width selector (OVL_DAT_801228f8 in
    // FmvStream.cs) to 0x18 (24 halfwords/row, matching Strip24PixelBytes below), and re-runs the
    // display-env setup with mode=1 (isrgb24=1) — i.e. every script-path movie (all in-game
    // cinematics) actually plays back in 24-bit on console. mode bit0 is exactly the flag that
    // selector flip is meant to steer here.
    public bool FeedRleList(ushort[] rleWords, int mode, out int macroblocksParsed, out string error)
    {
        macroblocksParsed = 0;
        error = null;
        _pendingStrips.Clear();

        bool stp = (mode & 0x2) != 0;
        bool is24Bit = (mode & 0x1) != 0;
        if ((mode & ~0x3) != 0)
        {
            // This port's DecDCTin abstraction only ever models two independent bits (depth, STP —
            // see the header note above); a real MDEC command additionally distinguishes 4-bit and
            // 8-bit (CLUT) output depths, which no caller in this title uses (every FMV1 movie is
            // either 15-bit or 24-bit — see MdecCore's class header) and which this decoder does
            // not implement. Log and fall through on the depth bit0 already extracted rather than
            // silently ignore the extra bits.
            Console.WriteLine($"[MdecCore] WARNING: DecDCTin mode 0x{mode:x} has bits set beyond " +
                               "the depth/STP selectors this port models (4-bit/8-bit output are " +
                               $"unsupported) — decoding as {(is24Bit ? "24-bit" : "15-bit")} anyway.");
        }

        int pos = 0;

        for (int col = 0; col < MacroblockCols; col++)
        {
            var strip = new byte[is24Bit ? Strip24PixelBytes : StripPixelBytes];

            for (int row = 0; row < MacroblockRows; row++)
            {
                for (int b = 0; b < 6; b++)
                {
                    byte[] iq = b < 2 ? _iqC : _iqY; // Cr, Cb use the chroma table; Y1..Y4 use luma.
                    // PERF: writes straight into the b-th of 6 persistent per-instance scratch
                    // buffers instead of allocating a fresh double[64] for every block (1800/frame)
                    // — see the buffers' own field comment. Numerically identical: same dequant/
                    // IDCT math, just no allocation.
                    if (!ReadOneBlock(rleWords, ref pos, iq, _blockScratch[b]))
                    {
                        error = $"DecDCTin: malformed RLE list at macroblock (col={col},row={row}) " +
                                $"block {b}, word {pos}/{rleWords.Length}";
                        macroblocksParsed = col * MacroblockRows + row;
                        return false;
                    }
                }

                if (is24Bit)
                {
                    AssembleMacroblockIntoStrip24(strip, row, _blockScratch[0], _blockScratch[1],
                        _blockScratch[2], _blockScratch[3], _blockScratch[4], _blockScratch[5]);
                }
                else
                {
                    AssembleMacroblockIntoStrip(strip, row, _blockScratch[0], _blockScratch[1],
                        _blockScratch[2], _blockScratch[3], _blockScratch[4], _blockScratch[5], stp);
                }
            }

            _pendingStrips.Enqueue(strip);
        }

        macroblocksParsed = MacroblockCount;

        if (pos != rleWords.Length)
        {
            // S2 ADVISORY PROMOTION (2026-08-29): was a non-fatal NOTE ("expected an exact match
            // with DecDCTvlc2's output"). DecDCTvlc2 and DecDCTin are the two ends of the same RLE
            // list this port's own convention defines (see the class header) — any length mismatch
            // means one of them mis-decoded, which silently produced a wrong image before this was
            // promoted to a hard failure. Re-verified against real FMV000/FMV001 frames (every
            // frame of both movies) that this never legitimately fires on real data.
            error = $"DecDCTin consumed {pos}/{rleWords.Length} RLE words — DecDCTvlc2 and DecDCTin " +
                    "disagree on RLE list length";
            return false;
        }

        return true;
    }

    // PERF: six persistent per-instance scratch buffers — one per macroblock block index (0=Cr,
    // 1=Cb, 2..5=Y1..Y4) — holding that block's IDCT-spatial output. AssembleMacroblockIntoStrip
    // needs all six simultaneously (it interleaves luma/chroma per output pixel), so these can't
    // collapse to one buffer, but they DO stay live only within a single macroblock's processing
    // and are safely overwritten by the next macroblock (this chip model is already single-
    // threaded/re-entrancy-scoped to one in-flight decode — see the class header and DecDCTout's
    // queueing note). Replaces 1800 double[64] allocations/frame (one per block) with 6 total.
    private readonly double[][] _blockScratch =
    {
        new double[64], new double[64], new double[64], new double[64], new double[64], new double[64],
    };

    // PERF: scratch buffers for ReadOneBlock/Idct8x8, reused across every block of every macroblock
    // instead of allocating fresh double[64]/double[8] arrays per call (7 arrays/block * 1800
    // blocks/frame). Safe because a single block is always fully dequantized+transformed before the
    // next one starts (same single-threaded, sequential-decode assumption as _blockScratch above).
    private readonly double[] _coeffZigZagScratch = new double[64];
    private readonly double[] _rasterScratch = new double[64];
    private readonly double[] _idctTmpScratch = new double[64];
    private readonly double[] _idctColScratch = new double[8];
    private readonly double[] _idctColOutScratch = new double[8];
    private readonly double[] _idctRowScratch = new double[8];
    private readonly double[] _idctRowOutScratch = new double[8];

    private bool ReadOneBlock(ushort[] words, ref int pos, byte[] iqTable, double[] spatialOut)
    {
        double[] coeffZigZag = _coeffZigZagScratch;
        // Only index 0 and the AC positions actually visited below are meaningful; every other
        // position must read as zero (an implicit "no coefficient here"), so — unlike a fresh
        // `new double[64]` which is already zeroed — a reused buffer needs an explicit clear first.
        Array.Clear(coeffZigZag, 0, 64);

        if (pos >= words.Length) return false;
        ushort dcWord = words[pos++];
        int dcRaw = (short)(dcWord << 6) >> 6; // sign-extend the low 10 bits
        // GHIDRA/spec: DC dequant has no qscale multiplier (unlike AC) — matches jPSXdec's
        // QuantizationDcReader_STRv12 / MdecDecoder_double: DC = code * iqTable[0].
        coeffZigZag[0] = dcRaw * iqTable[0];

        int qscale = (dcWord >> 10) & 0x3F;
        int zz = 1;

        while (true)
        {
            if (pos >= words.Length) return false;
            ushort w = words[pos++];
            if (w == 0xFE00)
            {
                break; // end of block
            }

            int run = (w >> 10) & 0x3F;
            int levelRaw = (short)(w << 6) >> 6;
            zz += run;
            if (zz >= 64) return false;

            // AC dequant: code * iqTable[pos] * qscale / 8.0 (jPSXdec MdecDecoder_double, confirmed
            // against the plan's own stated formula).
            coeffZigZag[zz] = levelRaw * iqTable[zz] * qscale / 8.0;
            zz++;
        }

        // Un-zigzag into raster (row-major, row=vertical frequency) order for the IDCT.
        // ZigZagRasterIndex is a permutation of 0..63, so every raster position is written exactly
        // once below — no clear needed (unlike coeffZigZag above).
        double[] raster = _rasterScratch;
        for (int i = 0; i < 64; i++)
        {
            raster[ZigZagRasterIndex[i]] = coeffZigZag[i];
        }

        Idct8x8(raster, spatialOut);
        return true;
    }

    // ------------------------------------------------------------------------------------------
    // Standard separable 8x8 IDCT (JPEG/MPEG formula). JUSTIFICATION: the plan explicitly does not
    // require bit-exactness with the console's fixed-point cosine table (@0x8010DA90, first entry
    // 0x5A82 = sqrt(2)/2 in Q15) at this slice — this is a floating-point implementation of the same
    // standard transform, structured (separate 1D pass + precomputed cosine basis) so a fixed-point
    // rounding-matched version can replace Idct1D later without touching the rest of the pipeline.
    // ------------------------------------------------------------------------------------------
    private static readonly double[,] CosBasis = BuildCosBasis();

    private static double[,] BuildCosBasis()
    {
        var c = new double[8, 8];
        for (int i = 0; i < 8; i++)
        {
            for (int k = 0; k < 8; k++)
            {
                c[i, k] = Math.Cos((2 * i + 1) * k * Math.PI / 16.0);
            }
        }

        return c;
    }

    private static double CoeffScale(int k) => k == 0 ? 1.0 / Math.Sqrt(2) : 1.0;

    private static void Idct1D(double[] input, double[] output)
    {
        for (int i = 0; i < 8; i++)
        {
            double sum = 0;
            for (int k = 0; k < 8; k++)
            {
                sum += CoeffScale(k) * input[k] * CosBasis[i, k];
            }

            output[i] = 0.5 * sum;
        }
    }

    // PERF: writes into caller-supplied `result` using this instance's scratch buffers instead of
    // allocating tmp/col/colOut/row/rowOut/result fresh on every call (6 arrays * 1800 blocks/frame
    // eliminated). Every element of every scratch buffer is fully overwritten before it is read on
    // each call (see the loop bounds below), so reusing them carries no stale-data risk. Arithmetic
    // is untouched — same operations in the same order, so results are bit-for-bit identical to the
    // original.
    private void Idct8x8(double[] raster, double[] result)
    {
        double[] tmp = _idctTmpScratch;
        double[] col = _idctColScratch;
        double[] colOut = _idctColOutScratch;

        // Pass 1: IDCT along columns (vertical frequency axis).
        for (int u = 0; u < 8; u++)
        {
            for (int v = 0; v < 8; v++) col[v] = raster[v * 8 + u];
            Idct1D(col, colOut);
            for (int y = 0; y < 8; y++) tmp[y * 8 + u] = colOut[y];
        }

        double[] row = _idctRowScratch;
        double[] rowOut = _idctRowOutScratch;

        // Pass 2: IDCT along rows (horizontal frequency axis).
        for (int y = 0; y < 8; y++)
        {
            for (int u = 0; u < 8; u++) row[u] = tmp[y * 8 + u];
            Idct1D(row, rowOut);
            for (int x = 0; x < 8; x++) result[y * 8 + x] = rowOut[x];
        }
    }

    // ------------------------------------------------------------------------------------------
    // YCbCr(4:2:0) -> RGB15 macroblock assembly. Y1..Y4 are the four 8x8 luma quadrants
    // (TL,TR,BL,BR); Cb/Cr are 8x8 at half resolution, nearest-neighbour upsampled 2x (PARTIAL:
    // psx-spx/jPSXdec also document bilinear/bicubic upsampling variants; nearest is the simplest
    // that satisfies this slice's visual-acceptance bar and keeps the pipeline easy to reason about).
    // Color formula (plan-specified, matches jPSXdec's PsxYCbCr.toRgb exactly): R=Y+128+1.402*Cr,
    // G=Y+128-0.3437*Cb-0.7143*Cr, B=Y+128+1.772*Cb, each clamped to [0,255] then packed 5:5:5 with
    // bit15=stp (matches this SDK's existing BGR555 convention — see LibGpu's pixel-packing notes:
    // bits0-4=R, bits5-9=G, bits10-14=B).
    // ------------------------------------------------------------------------------------------
    private static void AssembleMacroblockIntoStrip(byte[] strip, int mbRow, double[] cr, double[] cb,
        double[] y1, double[] y2, double[] y3, double[] y4, bool stp)
    {
        int baseY = mbRow * MacroblockSize;

        for (int ly = 0; ly < MacroblockSize; ly++)
        {
            for (int lx = 0; lx < MacroblockSize; lx++)
            {
                double[] yBlock = (lx < 8, ly < 8) switch
                {
                    (true, true) => y1,
                    (false, true) => y2,
                    (true, false) => y3,
                    _ => y4,
                };

                int by = ly & 7, bx = lx & 7;
                double y = yBlock[by * 8 + bx];

                int cby = ly / 2, cbx = lx / 2;
                double cbv = cb[cby * 8 + cbx];
                double crv = cr[cby * 8 + cbx];

                double yShift = y + 128.0;
                int r = Clamp255(yShift + 1.402 * crv);
                int g = Clamp255(yShift - 0.3437 * cbv - 0.7143 * crv);
                int b = Clamp255(yShift + 1.772 * cbv);

                ushort pixel = (ushort)((r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10) | (stp ? 0x8000 : 0));

                int py = baseY + ly;
                int outOff = (py * MacroblockSize + lx) * 2;
                strip[outOff] = (byte)pixel;
                strip[outOff + 1] = (byte)(pixel >> 8);
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // YCbCr(4:2:0) -> RGB24 macroblock assembly — same colour math and 4:2:0 nearest-neighbour
    // upsampling as AssembleMacroblockIntoStrip above (see that method's header note), just packed
    // as 3 full 8-bit bytes/pixel instead of quantized-and-packed 5:5:5 halfwords: this IS the
    // whole point of 24-bit output (see FeedRleList's header note on the console always using it
    // for script-path FMVs — full 8-bit channels, no 5-bit banding).
    //
    // Byte order: R, G, B (matches this SDK's own 24-bit VRAM convention, cross-checked against
    // LibGpu.ReadDisplayRgb24 — the port's other working 24-bit consumer, used by the title
    // screen's 24-bit background — which reads `byteInRow`/`+1`/`+2` as R/G/B respectively from the
    // same raw VRAM byte stream that LoadImage DMAs verbatim from a strip like this one).
    // ------------------------------------------------------------------------------------------
    private static void AssembleMacroblockIntoStrip24(byte[] strip, int mbRow, double[] cr, double[] cb,
        double[] y1, double[] y2, double[] y3, double[] y4)
    {
        int baseY = mbRow * MacroblockSize;

        for (int ly = 0; ly < MacroblockSize; ly++)
        {
            for (int lx = 0; lx < MacroblockSize; lx++)
            {
                double[] yBlock = (lx < 8, ly < 8) switch
                {
                    (true, true) => y1,
                    (false, true) => y2,
                    (true, false) => y3,
                    _ => y4,
                };

                int by = ly & 7, bx = lx & 7;
                double y = yBlock[by * 8 + bx];

                int cby = ly / 2, cbx = lx / 2;
                double cbv = cb[cby * 8 + cbx];
                double crv = cr[cby * 8 + cbx];

                double yShift = y + 128.0;
                int r = Clamp255(yShift + 1.402 * crv);
                int g = Clamp255(yShift - 0.3437 * cbv - 0.7143 * crv);
                int b = Clamp255(yShift + 1.772 * cbv);

                int py = baseY + ly;
                int outOff = (py * MacroblockSize + lx) * 3;
                strip[outOff] = (byte)r;
                strip[outOff + 1] = (byte)g;
                strip[outOff + 2] = (byte)b;
            }
        }
    }

    private static int Clamp255(double v) => v < 0 ? 0 : v > 255 ? 255 : (int)v;

    public bool TryDequeueStrip(out byte[] strip)
    {
        return _pendingStrips.TryDequeue(out strip);
    }

    public int PendingStripCount => _pendingStrips.Count;
}
