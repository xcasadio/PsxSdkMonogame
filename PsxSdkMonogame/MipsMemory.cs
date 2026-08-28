namespace PsxSdkMonogame;

// JUSTIFICATION: C# language bridge only
//
// The little-endian scalar loads and stores over a raw byte[] standing in for a PSX pointer — what
// `*(T *)(p + N)` is in the original. Every member here IS one MIPS memory instruction: `lh` / `lhu`
// / `lw` for the reads, `sh` / `sw` for the writes. This is the load/store half of what MipsMath.cs
// is the arithmetic half of, and it is named for that rather than for any one caller's buffer.
//
// THE BUFFERS THAT REACH IT are not one kind of thing: effect-record payloads (the C# shape splits
// the original's single `workData` pointer into a payload byte[] plus a payload-relative offset —
// see HighEffectParticles.cs's header), PE.IMG section buffers, PlayerState.raw, GPU primitive and
// ordering-table buffers. RENAMED 2026-08-11 from PayloadAccess for exactly that reason: only one of
// those five is a payload.
//
// These lived as 258 private copies across the 105 per-scene classes in EffectHandlers/, then as 63
// more across the effect/particle renderers, the eight HighEffect variants and the combat/menu HUD
// files — in seven spellings that differ only in surface form. This class is the single copy; the
// equivalences that let the seven collapse into one are stated on each member below.
//
// WHY THE MANUAL BYTE STORES rather than BitConverter: the PSX is little-endian and so are these
// buffers, unconditionally. `BitConverter.To*`/`GetBytes` are little-endian only when
// `BitConverter.IsLittleEndian`, and `GetBytes` allocates a fresh array per call — on a path that
// runs for every effect record every frame. The byte-at-a-time form is allocation-free and carries
// the buffers' endianness in the code rather than in the host's.
public static class MipsMemory
{
    // EQUIVALENCE: the spellings found were this one (43 copies), `BitConverter.ToInt16(p, o)` (10),
    // and the `unchecked((short)(...))` of the combat/HUD files (5, under the name ReadInt16). `p[o]`
    // and `p[o + 1]` promote to int, the OR lands in [0, 0xFFFF], and the `(short)` cast truncates to
    // the same bits ToInt16 reads on a little-endian host; the project does not build with /checked,
    // so the `unchecked` was already redundant.
    public static short ReadI16(byte[] p, int o) => (short)(p[o] | (p[o + 1] << 8));

    // EQUIVALENCE: 35 copies of this form, 3 of `BitConverter.ToUInt16`. Same reasoning as ReadI16.
    public static ushort ReadU16(byte[] p, int o) => (ushort)(p[o] | (p[o + 1] << 8));

    // EQUIVALENCE: 43 copies of this form, 7 of `BitConverter.ToInt32`. The top term `p[o + 3] << 24`
    // reaches 0xFF000000, which wraps to a negative int in C#'s default unchecked context — the same
    // value ToInt32 produces. The project does not build with /checked.
    public static int ReadI32(byte[] p, int o) =>
        p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24);

    // EQUIVALENCE: the EffectHandlers/ copies were `BitConverter.ToUInt32`; the combat/HUD files
    // spelled the same read out by hand under the name ReadUInt32 (5 copies), and CombatSystem
    // carried a sixth as ReadU32 over PlayerState.raw. All four expressions are this one.
    public static uint ReadU32(byte[] p, int o) => (uint)ReadI32(p, o);

    // `int v`, not `short`: the original's `sh` stores the low halfword of a 32-bit register, and the
    // call sites hand this whatever their arithmetic produced. Most of the local spellings already
    // took `int`; the HighEffect variants took `short`, which widens implicitly.
    //
    // EQUIVALENCE: the copies differed only in `(byte)(v >> 8)` vs `(byte)((uint)v >> 8)`. The shifts
    // disagree above bit 7 for negative v, and the `(byte)` cast discards exactly those bits.
    public static void WriteI16(byte[] p, int o, int v)
    {
        p[o] = (byte)v;
        p[o + 1] = (byte)((uint)v >> 8);
    }

    // The unsigned spelling of WriteI16, kept distinct because the original distinguishes
    // `*(ushort *)` from `*(short *)` at the call sites. Same two `sb`, same result.
    //
    // EQUIVALENCE: the copies took `int v` (29), `ushort v` through `BitConverter.GetBytes` (9), and
    // `ushort v` written out by hand (4). `int` is the wider contract and the one the call sites
    // need — they pass expressions like `ReadU16(payload, param_2 + 0x12) + 1`, which is int.
    public static void WriteU16(byte[] p, int o, int v)
    {
        p[o] = (byte)v;
        p[o + 1] = (byte)((uint)v >> 8);
    }

    // EQUIVALENCE: 27 copies with `(uint)v >>`, 4 with `v >>`. Byte-truncated at every step, so the
    // arithmetic-vs-logical difference is unobservable.
    public static void WriteI32(byte[] p, int o, int v)
    {
        p[o] = (byte)v;
        p[o + 1] = (byte)((uint)v >> 8);
        p[o + 2] = (byte)((uint)v >> 16);
        p[o + 3] = (byte)((uint)v >> 24);
    }

    // EQUIVALENCE: 13 copies via `BitConverter.GetBytes(uint)`, 6 written out byte by byte (five of
    // them named WriteUInt32 in the combat/HUD files).
    public static void WriteU32(byte[] p, int o, uint v) => WriteI32(p, o, (int)v);
}
