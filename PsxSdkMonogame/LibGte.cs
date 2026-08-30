using System;

namespace PsxSdkMonogame;

// GTE (Geometry Transformation Engine, PSX coprocessor 2) — a real, minimal register-file-backed
// emulation of exactly the pseudo-ops this port's call graph uses (ProcessAnimationMatrices,
// ComputeVertexLighting). NOT a general/complete GTE emulator — only the registers and opcodes
// actually reached from InitEntityFirstFrameRender's tree are modeled.
//
// EVIDENCE (2026-07-03): the two "shape" opcodes at the heart of this tree — `gte_rtir()` and
// `gte_rt()` — were closed via mcp__pcsx-redux__pcsx_disassemble raw COP2 instruction-word decode
// at their exact call sites in ProcessAnimationMatrices (0x8003a248 and 0x8003a334/0x8003a5a0):
// both decode to the real MVMVA opcode with distinct operand-field encodings —
// `gte::mvmva<sf:1,lm:0,mx:rt,v:v3,cv:zr>` (rtir: rotate the vector CURRENTLY in IR1-3 by the loaded
// rotation matrix, no translation added) and `gte::mvmva<sf:1,lm:0,mx:rt,v:v0,cv:tr>` (rt: rotate
// V0 by the loaded rotation matrix and add the loaded translation vector) — not guessed from the
// pseudo-op name alone (per the standard PSX GTE MVMVA opcode, e.g. Nocash PSX Specifications /
// PSYQ LIBGTE.H). The load/store pseudo-ops (SetRotMatrix/SetTransMatrix/ReadRotMatrix/ldclmv/
// stclmv/ldlv0/ldv0/stlvl/stlvnl/stsv) were each closed the same way: raw mtc2/mfc2/lwc2/swc2/ctc2/
// cfc2 register-field decode at their call sites (see ProcessAnimationMatrices' own analysis notes;
// e.g. ReadRotMatrix -> cfc2 $r11r12/$r13r21/$r22r23/$r31r32/$r33, confirming the GTE's packed
// 5-word rotation-matrix register format maps byte-for-byte onto MATRIX.m — no internal packing
// needed in this C# port, R is just kept as short[9] directly).
//
// CLOSED 2026-07-30: the lighting-pipeline opcodes (SetLightMatrix, ldv3, ldrgb, ncct, strgb3)
// reached from ComputeVertexLighting were previously PARTIAL — implemented against the public NCCT
// formula with the intermediate shifts unverified. They are now closed the same way rtir/rt were,
// by raw instruction-word decode of the real bytes:
//   * ncct  -> 0x4B18043F at 0x8003bbdc AND 0x8003bc64 (the ONLY two NCCT sites in the whole
//     0x80010000-0x800c0000 image, per a full COP2-opcode census): funct 0x3F, sf=1, lm=1;
//   * SetBackColor -> `sll rN,rM,4` + `ctc2 rN,$13/$14/$15` at 0x8003b9f8, i.e. the macro
//     pre-shifts its 0..255 arguments by 4.
// Two shift constants were wrong as a result and are corrected in this file (see gte_ncct and
// gte_SetBackColor); the higher-level structure was and remains CERTAIN (matches the decompiled
// call sequence exactly: SetLightMatrix then ldv3+ldrgb+ncct+strgb3 in a loop).
public static class LibGte
{
    public class MATRIX
    {
        public short[] m = new short[9];
        public int[] t = new int[3];
    }
    public class VECTOR
    {
        public int vx;
        public int vy;
        public int vz;
        public int pad;
    }

    public class SVECTOR
    {
        public short vx;
        public short vy;
        public short vz;
        public short pad;
    }

    // GHIDRA: CVECTOR — official PSYQ LIBGTE.H struct, 4 bytes {r,g,b,cd}. Standard GTE color format.
    public class CVECTOR
    {
        public byte r;
        public byte g;
        public byte b;
        public byte cd;
    }

    // === GTE register file (JUSTIFICATION: PSX hardware adaptation — models exactly the COP2
    // registers this port's call graph reads/writes; not a general emulator). ===
    private static readonly short[] gteR = new short[9];   // RT: current rotation matrix (row-major, matches MATRIX.m)
    private static readonly int[] gteTR = new int[3];      // RT: current translation vector (matches MATRIX.t)
    private static readonly short[] gteLLM = new short[9]; // Light matrix (gte_SetLightMatrix)
    private static readonly short[] gteLCM = new short[9]; // Color matrix (gte_SetColorMatrix, was the SetColorMatrix no-op)
    private static readonly int[] gteBK = new int[3];      // Background color add vector (gte_SetBackColor, was the SetBackColor no-op)
    private static short gteIR1, gteIR2, gteIR3;            // Current IR1-3 (saturated 16-bit vector regs)
    private static int gteMAC1, gteMAC2, gteMAC3;           // Current MAC1-3 (32-bit accumulators, pre-saturation)
    private static readonly short[] gteV0 = new short[3];   // V0 (vx,vy,vz) — set by ldlv0/ldv0
    private static readonly short[][] gteV3 = { new short[3], new short[3], new short[3] }; // V0,V1,V2 as loaded by ldv3
    private static byte gteRgbcR, gteRgbcG, gteRgbcB, gteRgbcCd; // RGBC "material color" code register (gte_ldrgb)
    private static readonly byte[][] gteRgbFifo = { new byte[4], new byte[4], new byte[4] }; // RGB0-2 output FIFO (gte_strgb3 reads from here)

    private static short Sat16(int v) => (short)(v > short.MaxValue ? short.MaxValue : v < short.MinValue ? short.MinValue : v);
    private static byte Sat8(int v) => (byte)(v > 255 ? 255 : v < 0 ? 0 : v);

    // GHIDRA: gte_SetBackColor — loads the RBK/GBK/BBK control registers (COP2 $13/$14/$15), read by
    // gte_ncct's color stage as the additive ambient term.
    // CERTAIN (closed 2026-07-30 via raw MIPS decode at ComputeVertexLighting's only call site,
    // 0x8003b9f8-0x8003ba0c): `sll $t4,$s4,4` / `sll $t5,$s5,4` / `sll $t6,$s6,4` immediately before
    // `ctc2 $t4,$13` / `ctc2 $t5,$14` / `ctc2 $t6,$15` — the SDK macro LEFT-SHIFTS its plain 0..255
    // arguments by 4 before loading the control registers, exactly like gte_SetGeomOffset's <<16.
    // CORRECTION (2026-07-30): this stored the raw argument, so the ambient landed 16x too dark
    // (0x80 -> 0.031 instead of 0.5 in the 1.3.12 scale the color stage adds it at) and every
    // surface not facing one of the two lights collapsed to near-black.
    // CORRECTION (2026-07-03): retired the previous no-op — ComputeVertexLighting's lighting
    // math (gte_ncct) reads BK as the color-stage additive term, and its output (vertex
    // gouraud colors) is consumed by Render_UpdateClutTable (in-scope), so this can no longer be a
    // pure-rasterization-only no-op per the "only stub when confirmed unread in scope" rule.
    public static void SetBackColor(long rbk, long gbk, long bbk)
    {
        gteBK[0] = (int)rbk << 4;
        gteBK[1] = (int)gbk << 4;
        gteBK[2] = (int)bbk << 4;
    }

    // GHIDRA: gte_SetColorMatrix @ 0x200000a8
    // CORRECTION (2026-07-03): retired the previous no-op for the same reason as SetBackColor above
    // — this loads the GTE "color matrix" (LCM) register set, read by gte_ncct's color stage.
    public static void SetColorMatrix(MATRIX m)
    {
        Array.Copy(m.m, gteLCM, 9);
    }

    // GHIDRA: gte_SetLightMatrix — loads the GTE "light matrix" (LLM) register set, read by
    // gte_ncct's normal-rotation stage.
    public static void SetLightMatrix(MATRIX m)
    {
        Array.Copy(m.m, gteLLM, 9);
    }

    // GHIDRA: gte_SetRotMatrix — loads the GTE "rotation matrix" (RT) register set from a MATRIX's
    // 9-short m[] (CERTAIN: closed via raw ctc2 $r11r12/$r13r21/$r22r23/$r31r32/$r33 decode, see
    // file header). Overloaded for both a real MATRIX object and a raw byte[]-backed column source
    // (JUSTIFICATION: C# language bridge — the original passes a plain MATRIX* in every case, but
    // this port sometimes represents that same memory as a byte[] scratch-buffer slice — see
    // RenderParameter.matrixArrayPtr_0x58 in TmdSystem.cs/SetupAnimationData for why).
    public static void SetRotMatrix(MATRIX m) => Array.Copy(m.m, gteR, 9);
    public static void SetRotMatrix(byte[] buf, int byteOffset)
    {
        for (int i = 0; i < 9; i++) gteR[i] = BitConverter.ToInt16(buf, byteOffset + i * 2);
    }

    // GHIDRA: gte_SetTransMatrix — loads TR from a MATRIX's t[] (offset 0x14 within the MATRIX,
    // CERTAIN: closed via raw ctc2 $trx/$try/$trz decode immediately after SetRotMatrix's ctc2
    // sequence at the same call site, see file header).
    public static void SetTransMatrix(MATRIX m) => Array.Copy(m.t, gteTR, 3);
    public static void SetTransMatrix(byte[] buf, int byteOffset)
    {
        for (int i = 0; i < 3; i++) gteTR[i] = BitConverter.ToInt32(buf, byteOffset + 0x14 + i * 4);
    }

    // GHIDRA: gte_ReadRotMatrix — inverse of SetRotMatrix: stores the CURRENT RT register set back
    // to memory as 9 shorts (CERTAIN, same evidence as SetRotMatrix).
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: PushMatrix @ 0x8006D274 and PopMatrix @ 0x8006D314 save and restore the current
    // rotation and translation registers. libgte keeps a small fixed stack for this; the depth
    // below is generous next to the single nesting level FUN_80037388 @ 0x80037388 uses, and an
    // overflow throws rather than silently corrupting the matrix like the console would.
    private static readonly short[][] s_matrixStackR = new short[20][];
    private static readonly int[][] s_matrixStackT = new int[20][];
    private static int s_matrixStackDepth;

    public static void PushMatrix()
    {
        if (s_matrixStackDepth >= s_matrixStackR.Length)
        {
            throw new InvalidOperationException("PushMatrix: GTE matrix stack overflow");
        }

        s_matrixStackR[s_matrixStackDepth] = (short[])gteR.Clone();
        s_matrixStackT[s_matrixStackDepth] = (int[])gteTR.Clone();
        s_matrixStackDepth++;
    }

    public static void PopMatrix()
    {
        if (s_matrixStackDepth == 0)
        {
            throw new InvalidOperationException("PopMatrix: GTE matrix stack underflow");
        }

        s_matrixStackDepth--;
        Array.Copy(s_matrixStackR[s_matrixStackDepth], gteR, 9);
        Array.Copy(s_matrixStackT[s_matrixStackDepth], gteTR, 3);
    }

    public static void ReadRotMatrix(MATRIX m) => Array.Copy(gteR, m.m, 9);

    // GHIDRA: gte_ReadTransMatrix — CERTAIN, closed by raw COP2 decode of the push branch at
    // ProcessAnimationMatrices 0x8003a3ec-0x8003a404: `cfc2 t4,$5` / `cfc2 t5,$6` / `cfc2 t6,$7`
    // followed by `sw` to +0x14/+0x18/+0x1C. Control registers 5/6/7 are TRX/TRY/TRZ, i.e. the
    // TRANSLATION, and +0x14 is exactly where MATRIX.t begins.
    // This matters because Ghidra's decompiler prints that whole 16-instruction block as a single
    // `gte_ReadRotMatrix(r0_02)` call — the rotation macro's name — which reads as "the push saves
    // rotation only". The bytes say otherwise: the push saves rotation AND translation. A pop that
    // restores a zero translation reparents every bone after it to the origin.
    public static void ReadTransMatrix(MATRIX m)
    {
        m.t[0] = gteTR[0];
        m.t[1] = gteTR[1];
        m.t[2] = gteTR[2];
    }
    public static void ReadRotMatrix(byte[] buf, int byteOffset)
    {
        for (int i = 0; i < 9; i++) BitConverter.GetBytes(gteR[i]).CopyTo(buf, byteOffset + i * 2);
    }

    // GHIDRA: gte_ldclmv — loads IR1-3 from ONE COLUMN of a 3x3 matrix (3 shorts at byte offsets
    // 0, +6, +12 from the given base — CERTAIN, closed via raw lhu+mtc2 decode at
    // ProcessAnimationMatrices 0x8003a228-0x8003a23c). The original always passes a MATRIX* that's
    // already been column-shifted by pointer arithmetic (e.g. `pMVar2->m[0]+1` for column 1); this
    // port instead takes the base MATRIX/byte[] plus an explicit `column` (0-2) — JUSTIFICATION: C#
    // language bridge (no raw pointer arithmetic on a struct field).
    public static void LdClmv(MATRIX m, int column)
    {
        gteIR1 = m.m[column];
        gteIR2 = m.m[3 + column];
        gteIR3 = m.m[6 + column];
    }
    public static void LdClmv(byte[] buf, int byteOffset, int column)
    {
        int c = column * 2;
        gteIR1 = BitConverter.ToInt16(buf, byteOffset + c);
        gteIR2 = BitConverter.ToInt16(buf, byteOffset + 6 + c);
        gteIR3 = BitConverter.ToInt16(buf, byteOffset + 12 + c);
    }

    // GHIDRA: gte_stclmv — inverse of ldclmv: stores IR1-3 (saturated) to one column of a 3x3
    // matrix (CERTAIN, closed via raw mfc2+sh decode at 0x8003a24c-0x8003a260).
    public static void StClmv(MATRIX m, int column)
    {
        m.m[column] = gteIR1;
        m.m[3 + column] = gteIR2;
        m.m[6 + column] = gteIR3;
    }
    public static void StClmv(byte[] buf, int byteOffset, int column)
    {
        int c = column * 2;
        BitConverter.GetBytes(gteIR1).CopyTo(buf, byteOffset + c);
        BitConverter.GetBytes(gteIR2).CopyTo(buf, byteOffset + 6 + c);
        BitConverter.GetBytes(gteIR3).CopyTo(buf, byteOffset + 12 + c);
    }

    // GHIDRA: gte_ldlv0 — loads V0 from a VECTOR-shaped (int[3], 4-byte-spaced) source, reading
    // only the LOW 16 bits of each int (CERTAIN: closed via raw lhu-pair-pack-then-mtc2/lwc2 decode
    // at 0x8003a314-0x8003a328 — the source there is MATRIX.t, an int[3]).
    public static void LdLv0(int[] vecT)
    {
        gteV0[0] = (short)vecT[0];
        gteV0[1] = (short)vecT[1];
        gteV0[2] = (short)vecT[2];
    }
    public static void LdLv0(byte[] buf, int byteOffset)
    {
        gteV0[0] = (short)BitConverter.ToInt32(buf, byteOffset);
        gteV0[1] = (short)BitConverter.ToInt32(buf, byteOffset + 4);
        gteV0[2] = (short)BitConverter.ToInt32(buf, byteOffset + 8);
    }

    // GHIDRA: gte_ldv0 — loads V0 from an SVECTOR-shaped (4 packed shorts) source directly
    // (CERTAIN: closed via raw lwc2 $vxy0/$vz0 decode at 0x8003a638-0x8003a63c — reads 2 packed
    // words straight from a densely-packed short source, unlike ldlv0's manual gather).
    // JUSTIFICATION: C# language bridge — original source is a raw SVECTOR* view into a byte[]-
    // backed TMD/scratch buffer in this port's callers; overloaded for both an SVECTOR object and a
    // raw byte[]+offset.
    public static void LdV0(SVECTOR v)
    {
        gteV0[0] = v.vx;
        gteV0[1] = v.vy;
        gteV0[2] = v.vz;
    }
    public static void LdV0(byte[] buf, int byteOffset)
    {
        gteV0[0] = BitConverter.ToInt16(buf, byteOffset);
        gteV0[1] = BitConverter.ToInt16(buf, byteOffset + 2);
        gteV0[2] = BitConverter.ToInt16(buf, byteOffset + 4);
    }

    // GHIDRA: gte_stlvl — stores the CURRENT IR1-3 (saturated) as 3 consecutive 32-bit words
    // (CERTAIN: closed via raw mfc2+swc2-word decode — distinct from stclmv's 16-bit-short stores).
    public static void StLvl(int[] outVecT)
    {
        outVecT[0] = gteIR1;
        outVecT[1] = gteIR2;
        outVecT[2] = gteIR3;
    }

    public static void StLvl(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes((int)gteIR1).CopyTo(buf, byteOffset);
        BitConverter.GetBytes((int)gteIR2).CopyTo(buf, byteOffset + 4);
        BitConverter.GetBytes((int)gteIR3).CopyTo(buf, byteOffset + 8);
    }

    // GHIDRA: gte_ldopv1 — CERTAIN: closed via raw COP2 decode at Render_DrawRoom 0x800681f4-0x80068208
    // (`ctc2 $r11r12, w0`, `ctc2 $r22r23, w1`, `ctc2 $r33, w2` over three consecutive 32-bit words).
    // Loads the OP opcode's D1/D2/D3 operands, which the GTE aliases onto the rotation-matrix
    // diagonal RT11/RT22/RT33 — so this DESTROYS the currently loaded rotation matrix, and callers
    // must re-issue SetRotMatrix afterwards (Render_DrawRoom does).
    public static void LdOpv1(byte[] buf, int byteOffset)
    {
        uint w0 = BitConverter.ToUInt32(buf, byteOffset);
        uint w1 = BitConverter.ToUInt32(buf, byteOffset + 4);
        uint w2 = BitConverter.ToUInt32(buf, byteOffset + 8);
        gteR[0] = (short)w0; gteR[1] = (short)(w0 >> 16);   // RT11, RT12
        gteR[4] = (short)w1; gteR[5] = (short)(w1 >> 16);   // RT22, RT23
        gteR[8] = (short)w2;                                // RT33
    }

    // GHIDRA: gte_ldopv2 — CERTAIN: same decode site, `lwc2 $ir3/$ir1/$ir2` over three consecutive
    // 32-bit words (only the low 16 bits of each reach the 16-bit IR registers).
    public static void LdOpv2(byte[] buf, int byteOffset)
    {
        gteIR1 = (short)BitConverter.ToInt32(buf, byteOffset);
        gteIR2 = (short)BitConverter.ToInt32(buf, byteOffset + 4);
        gteIR3 = (short)BitConverter.ToInt32(buf, byteOffset + 8);
    }

    // GHIDRA: gte_op12 (nOP12) — CERTAIN: raw COP2 word 0x4b78000c at Render_DrawRoom 0x80068224
    // decodes to `gte::op<sf:1,...>`, funct=0x0c. The public GTE OP (outer/cross product) formula:
    //   MAC1 = (IR3*D2 - IR2*D3) >> 12, MAC2 = (IR1*D3 - IR3*D1) >> 12, MAC3 = (IR2*D1 - IR1*D2) >> 12
    // with D1/D2/D3 = RT11/RT22/RT33, then IR1-3 = Sat16(MAC1-3).
    public static void Op12()
    {
        int d1 = gteR[0], d2 = gteR[4], d3 = gteR[8];
        int ir1 = gteIR1, ir2 = gteIR2, ir3 = gteIR3;
        gteMAC1 = (int)(((long)ir3 * d2 - (long)ir2 * d3) >> 12);
        gteMAC2 = (int)(((long)ir1 * d3 - (long)ir3 * d1) >> 12);
        gteMAC3 = (int)(((long)ir2 * d1 - (long)ir1 * d2) >> 12);
        gteIR1 = Sat16(gteMAC1);
        gteIR2 = Sat16(gteMAC2);
        gteIR3 = Sat16(gteMAC3);
    }

    // GHIDRA: RotTransPers @ 0x80079244
    // CERTAIN (raw disassembly of the whole 10-instruction body): loads the SVECTOR into V0, runs
    // `gte::rtps<sf:1,lm:0,mx:rt,v:v0,cv:tr>`, writes the packed SXY2 word to *sxy, IR0 to *p and the
    // GTE FLAG register to *flag, and returns `SZ3 >> 2` (arithmetic) — the ordering-table Z used by
    // its callers against the 0x1000-bucket table.
    // PARTIAL: this port exposes no IR0 register (DQA/DQB and MAC0's depth-cue term ARE modelled
    // since 2026-08-03, but nothing reads IR0) and no FLAG register, so *p and *flag are written as
    // 0 rather than as the saturated MAC0 >> 12. Both are provably dead at Render_DrawRoom's four
    // call sites (written into scratch slots that are never read back); any future caller that does
    // read them needs those two registers modelled first.
    public static int RotTransPers(byte[] v0Buf, int v0Offset, byte[] outBuf, int sxyOffset, int pOffset, int flagOffset)
    {
        PerspectiveTransformPoint(
            BitConverter.ToInt16(v0Buf, v0Offset),
            BitConverter.ToInt16(v0Buf, v0Offset + 2),
            BitConverter.ToInt16(v0Buf, v0Offset + 4),
            out _, out _, out ushort sz, out short sx, out short sy);
        gteSZ[3] = sz;
        gteSXY[2][0] = sx; gteSXY[2][1] = sy;
        BitConverter.GetBytes((ushort)sx | ((uint)(ushort)sy << 16)).CopyTo(outBuf, sxyOffset);
        BitConverter.GetBytes(0).CopyTo(outBuf, pOffset);
        BitConverter.GetBytes(0).CopyTo(outBuf, flagOffset);
        return sz >> 2;
    }

    // GHIDRA: RotTransPers4 @ 0x80079304 — PSX SDK (psyq MTX_*.OBJ), batch T04g. 30 instructions,
    // read as raw MIPS: nothing in the executable calls it, only the PE.IMG scene overlays do.
    // TEN ARGUMENTS — four in registers and six off the caller's frame at sp+0x10..0x24. The four
    // SVECTORs project into four packed SXY words; `p` takes IR0 and `flag` the OR of the GTE FLAG
    // register read after EACH of the two GTE ops. The return is `SZ3 >> 2`, the same
    // ordering-table Z RotTransPers above returns.
    // IT IS TWO GTE OPS, NOT FOUR. `cop2 0x4A280030` is RTPT (fn 0x30), which projects V0, V1 and
    // V2 in one go and leaves their results in SXY0/SXY1/SXY2; the fourth vector then goes through
    // an ordinary RTPS, whose result lands in SXY2 after the FIFO shifts. That is why the third and
    // fourth stores both read $c14. Every COP2 word decoded by script.
    // PARTIAL, inherited from RotTransPers: this port exposes no IR0 register and no FLAG register,
    // so `p` and `flag` are written as 0. FUN_800c5a40, the only caller reached so
    // far, writes both into stack slots it never reads back.
    public static int RotTransPers4(byte[] vBuf, int v0Off, int v1Off, int v2Off, int v3Off,
        byte[] sxyBuf, int sxy0Off, int sxy1Off, int sxy2Off, int sxy3Off,
        byte[] pBuf, int pOff, byte[] flagBuf, int flagOff)
    {
        int[] offs = { v0Off, v1Off, v2Off, v3Off };
        int[] outs = { sxy0Off, sxy1Off, sxy2Off, sxy3Off };
        ushort sz = 0;
        for (int i = 0; i < 4; i++)
        {
            PerspectiveTransformPoint(
                BitConverter.ToInt16(vBuf, offs[i]),
                BitConverter.ToInt16(vBuf, offs[i] + 2),
                BitConverter.ToInt16(vBuf, offs[i] + 4),
                out _, out _, out sz, out short sx, out short sy);
            gteSZ[3] = sz;
            gteSXY[2][0] = sx; gteSXY[2][1] = sy;
            BitConverter.GetBytes((ushort)sx | ((uint)(ushort)sy << 16)).CopyTo(sxyBuf, outs[i]);
        }
        BitConverter.GetBytes(0).CopyTo(pBuf, pOff);
        BitConverter.GetBytes(0).CopyTo(flagBuf, flagOff);
        return sz >> 2;
    }

    // GHIDRA: gte_stlvnl — stores the CURRENT MAC1-3 (UNsaturated 32-bit accumulator, not IR) as 3
    // consecutive 32-bit words (CERTAIN: closed via raw `swc2 $mac1/$mac2/$mac3` decode at
    // 0x8003a5a8-0x8003a5ac — distinct from stlvl, which reads $ir1-3 instead).
    public static void StLvnl(int[] outVecT)
    {
        outVecT[0] = gteMAC1;
        outVecT[1] = gteMAC2;
        outVecT[2] = gteMAC3;
    }
    public static void StLvnl(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes(gteMAC1).CopyTo(buf, byteOffset);
        BitConverter.GetBytes(gteMAC2).CopyTo(buf, byteOffset + 4);
        BitConverter.GetBytes(gteMAC3).CopyTo(buf, byteOffset + 8);
    }

    // GHIDRA: gte_stsv — stores the CURRENT IR1-3 (saturated) as 3 consecutive shorts into an
    // SVECTOR-shaped destination (CERTAIN: closed via raw mfc2+sh decode at 0x8003a64c-0x8003a658).
    public static void StSv(SVECTOR outv)
    {
        outv.vx = gteIR1;
        outv.vy = gteIR2;
        outv.vz = gteIR3;
    }
    public static void StSv(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes(gteIR1).CopyTo(buf, byteOffset);
        BitConverter.GetBytes(gteIR2).CopyTo(buf, byteOffset + 2);
        BitConverter.GetBytes(gteIR3).CopyTo(buf, byteOffset + 4);
    }

    // GHIDRA: gte_rtir (nRTIR) — real MVMVA math, mx=RT, v=IR(current), cv=none, sf=1, lm=0
    // (CERTAIN: closed via raw COP2 instruction-word decode, see file header). Rotates the vector
    // currently loaded in IR1-3 by the current rotation matrix RT, with NO translation added:
    //   MAC[1..3] = (RT * IR) >> 12;  IR[1..3] = Saturate16(MAC[1..3])   (lm=0: signed range)
    // Used 3x in a row (once per matrix column, via ldclmv/stclmv) to compose RT * (some 3x3
    // matrix) one column at a time — i.e. hierarchical world = parent_rotation * local_rotation.
    public static void Rtir()
    {
        long mac1 = (long)gteR[0] * gteIR1 + (long)gteR[1] * gteIR2 + (long)gteR[2] * gteIR3;
        long mac2 = (long)gteR[3] * gteIR1 + (long)gteR[4] * gteIR2 + (long)gteR[5] * gteIR3;
        long mac3 = (long)gteR[6] * gteIR1 + (long)gteR[7] * gteIR2 + (long)gteR[8] * gteIR3;
        gteMAC1 = (int)(mac1 >> 12);
        gteMAC2 = (int)(mac2 >> 12);
        gteMAC3 = (int)(mac3 >> 12);
        gteIR1 = Sat16(gteMAC1);
        gteIR2 = Sat16(gteMAC2);
        gteIR3 = Sat16(gteMAC3);
    }

    // GHIDRA: gte_rt (nRT) — real MVMVA math, mx=RT, v=V0, cv=TR, sf=1, lm=0 (CERTAIN: closed via
    // raw COP2 instruction-word decode, see file header). Rotates V0 by RT and adds the current
    // translation TR:
    //   MAC[1..3] = ((RT * V0) >> 12) + TR;  IR[1..3] = Saturate16(MAC[1..3])
    public static void Rt()
    {
        long mac1 = (long)gteR[0] * gteV0[0] + (long)gteR[1] * gteV0[1] + (long)gteR[2] * gteV0[2];
        long mac2 = (long)gteR[3] * gteV0[0] + (long)gteR[4] * gteV0[1] + (long)gteR[5] * gteV0[2];
        long mac3 = (long)gteR[6] * gteV0[0] + (long)gteR[7] * gteV0[1] + (long)gteR[8] * gteV0[2];
        gteMAC1 = (int)(mac1 >> 12) + gteTR[0];
        gteMAC2 = (int)(mac2 >> 12) + gteTR[1];
        gteMAC3 = (int)(mac3 >> 12) + gteTR[2];
        gteIR1 = Sat16(gteMAC1);
        gteIR2 = Sat16(gteMAC2);
        gteIR3 = Sat16(gteMAC3);
    }

    // GHIDRA: the MVMVA variant `cop2 0x0486012` — psyq's `gte_rtv0`. SAME multiply as Rt() above
    // and a DIFFERENT accumulator: cv = 3 means NO TRANSLATION VECTOR IS ADDED.
    //
    // ADDED 2026-08-10 because Rt() was being used for both, which is wrong for one of them. The
    // command word decodes cmd=0x12 (MVMVA), mx=0 (rotation matrix), v=0 (V0), sf=1, lm=0 and
    // **cv=3**; cv=0 would be TR, 1 BK, 2 FC. Rt() adds gteTR unconditionally, so it is `gte_rt`.
    //
    // BOTH VARIANTS ARE REAL AND BOTH ARE USED, which is why this is a second function rather than a
    // correction to the first. Counted over SLUS_006.62: 53 MVMVA sites are mx=0 v=0 **cv=0** (TR
    // added — Rt() is right for those, and they are the executable's own game code) against 12 that
    // are mx=0 v=0 **cv=3**, all inside the SDK segment. In the PE.IMG descriptor overlays the ratio
    // inverts: C01, C02, C05 and C19 carry TEN cv=3 sites each, and C02 and C05 carry two cv=0 sites
    // as well — so a single family uses both, and picking one for the whole port cannot be right.
    //
    // The difference is unobservable exactly when TR happens to be zero on the path, which is why
    // C01 has been drawing the m0022i parrot correctly with the wrong one. Measured by the C02 pass:
    // with TR = 0, 0 of 60 cases diverge; with TR = (0x1234, -0x777, 0x40), 60 of 60 do.
    public static void RtV0()
    {
        long mac1 = (long)gteR[0] * gteV0[0] + (long)gteR[1] * gteV0[1] + (long)gteR[2] * gteV0[2];
        long mac2 = (long)gteR[3] * gteV0[0] + (long)gteR[4] * gteV0[1] + (long)gteR[5] * gteV0[2];
        long mac3 = (long)gteR[6] * gteV0[0] + (long)gteR[7] * gteV0[1] + (long)gteR[8] * gteV0[2];
        gteMAC1 = (int)(mac1 >> 12);
        gteMAC2 = (int)(mac2 >> 12);
        gteMAC3 = (int)(mac3 >> 12);
        gteIR1 = Sat16(gteMAC1);
        gteIR2 = Sat16(gteMAC2);
        gteIR3 = Sat16(gteMAC3);
    }

    // GHIDRA: gte_ldv3 — loads V0,V1,V2 in one call from 3 separate SVECTOR sources (CERTAIN
    // control-flow/argument-order: matches ComputeVertexLighting's 3-normal-lookup call pattern;
    // PARTIAL on whether real hardware truly loads all 3 in one cycle vs the pseudo-op modeling 3
    // real ldv-equivalents — functionally identical either way for this port).
    public static void LdV3(SVECTOR v0, SVECTOR v1, SVECTOR v2)
    {
        gteV3[0][0] = v0.vx; gteV3[0][1] = v0.vy; gteV3[0][2] = v0.vz;
        gteV3[1][0] = v1.vx; gteV3[1][1] = v1.vy; gteV3[1][2] = v1.vz;
        gteV3[2][0] = v2.vx; gteV3[2][1] = v2.vy; gteV3[2][2] = v2.vz;
    }
    public static void LdV3(byte[] buf0, int off0, byte[] buf1, int off1, byte[] buf2, int off2)
    {
        gteV3[0][0] = BitConverter.ToInt16(buf0, off0); gteV3[0][1] = BitConverter.ToInt16(buf0, off0 + 2); gteV3[0][2] = BitConverter.ToInt16(buf0, off0 + 4);
        gteV3[1][0] = BitConverter.ToInt16(buf1, off1); gteV3[1][1] = BitConverter.ToInt16(buf1, off1 + 2); gteV3[1][2] = BitConverter.ToInt16(buf1, off1 + 4);
        gteV3[2][0] = BitConverter.ToInt16(buf2, off2); gteV3[2][1] = BitConverter.ToInt16(buf2, off2 + 2); gteV3[2][2] = BitConverter.ToInt16(buf2, off2 + 4);
    }

    // GHIDRA: gte_ldrgb — loads the RGBC "material color code" register from a CVECTOR-shaped
    // (r,g,b,cd bytes) source (CERTAIN structure; standard GTE RGBC register).
    public static void LdRgb(byte r, byte g, byte b, byte cd)
    {
        gteRgbcR = r; gteRgbcG = g; gteRgbcB = b; gteRgbcCd = cd;
    }

    // GHIDRA: gte_ncct (nNCCT) — "Normal Color Color, triple": lights 3 normals (V0,V1,V2, as
    // loaded by ldv3) against the light matrix + color matrix + background color + material RGBC,
    // producing 3 output colors in the RGB FIFO (read out by strgb3).
    // CERTAIN on the opcode (closed 2026-07-30, raw COP2 instruction-word decode at BOTH of the
    // game's only two ncct sites, ComputeVertexLighting 0x8003bbdc and 0x8003bc64: word 0x4B18043F
    // = funct 0x3F (NCCT) with sf=1 (bit19) and lm=1 (bit10) — so the shift is 12 and both IR
    // saturations clamp to [0, 0x7FFF], which is what Sat16Positive does).
    //   for each of the 3 loaded normals Vn:
    //     IR = Saturate16Positive((LLM * Vn) >> 12)                  -- light-matrix rotate
    //     IR = Saturate16Positive(((LCM * IR) >> 12) + BK)           -- color-matrix + background add
    //     result[ch] = Saturate8((IR[ch] * RGBC[ch]) >> 12)          -- modulate by material color
    // The BK term: hardware computes ((BK * 0x1000) + LCM*IR) >> 12, which is algebraically the
    // ((LCM*IR) >> 12) + BK written here (BK is already loaded pre-shifted, see SetBackColor).
    // The modulation shift: hardware is MAC = (RGBC * IR) << 4, then MAC >>= sf*12, then the RGB
    // FIFO takes MAC/16 — i.e. (RGBC * IR) >> 12 net.
    // CORRECTION (2026-07-30): the modulation shift was >> 7, i.e. 32x too bright. It was masked
    // because the normal codebook was empty (see StaticVariables.g_vertexNormalsTable): with IR
    // pinned at the ambient 0x80 the wrong shift returned exactly the neutral 0x80, so models
    // rendered unlit-but-plausible. The invariant that settles it is the PSX texture-blend
    // convention: RGBC 0x80 against a fully-lit IR of 0x1000 must return 0x80 (no change), which
    // only >> 12 satisfies; >> 7 returns 4096 and saturates every lit pixel to white.
    public static void Ncct()
    {
        for (int n = 0; n < 3; n++)
        {
            short[] v = gteV3[n];
            long l1 = (long)gteLLM[0] * v[0] + (long)gteLLM[1] * v[1] + (long)gteLLM[2] * v[2];
            long l2 = (long)gteLLM[3] * v[0] + (long)gteLLM[4] * v[1] + (long)gteLLM[5] * v[2];
            long l3 = (long)gteLLM[6] * v[0] + (long)gteLLM[7] * v[1] + (long)gteLLM[8] * v[2];
            short lir1 = Sat16Positive((int)(l1 >> 12));
            short lir2 = Sat16Positive((int)(l2 >> 12));
            short lir3 = Sat16Positive((int)(l3 >> 12));

            long c1 = (long)gteLCM[0] * lir1 + (long)gteLCM[1] * lir2 + (long)gteLCM[2] * lir3;
            long c2 = (long)gteLCM[3] * lir1 + (long)gteLCM[4] * lir2 + (long)gteLCM[5] * lir3;
            long c3 = (long)gteLCM[6] * lir1 + (long)gteLCM[7] * lir2 + (long)gteLCM[8] * lir3;
            int cir1 = Sat16Positive((int)(c1 >> 12) + gteBK[0]);
            int cir2 = Sat16Positive((int)(c2 >> 12) + gteBK[1]);
            int cir3 = Sat16Positive((int)(c3 >> 12) + gteBK[2]);

            gteRgbFifo[n][0] = Sat8((cir1 * gteRgbcR) >> 12);
            gteRgbFifo[n][1] = Sat8((cir2 * gteRgbcG) >> 12);
            gteRgbFifo[n][2] = Sat8((cir3 * gteRgbcB) >> 12);
            gteRgbFifo[n][3] = gteRgbcCd;
        }
    }

    private static short Sat16Positive(int v) => (short)(v > short.MaxValue ? short.MaxValue : v < 0 ? 0 : v);

    // GHIDRA: gte_strgb3 — reads the 3-entry RGB output FIFO (filled by the last ncct call) out to
    // 3 CVECTOR-shaped (r,g,b,cd bytes) destinations (CERTAIN structure; standard GTE RGB FIFO
    // readout, matches gte_strgb3's 3-destination call signature in ComputeVertexLighting).
    // JUSTIFICATION: C# language bridge — destinations in this port are always byte[]-backed
    // scratch/cache buffers (UINT_ARRAY_800b1638 / g_flatPrimGouraudColorTable), not CVECTOR
    // objects, so this takes 3 explicit (buffer, offset) pairs.
    public static void StRgb3(byte[] buf0, int off0, byte[] buf1, int off1, byte[] buf2, int off2)
    {
        gteRgbFifo[0].CopyTo(buf0, off0);
        gteRgbFifo[1].CopyTo(buf1, off1);
        gteRgbFifo[2].CopyTo(buf2, off2);
    }

    // GHIDRA: gte_ldH @ used by SetGeomScreen (0x80079024)
    // CORRECTION (2026-07-04): retired the previous no-op — RenderEquippedWeaponModel's tree
    // (TransformAndProjectVertices -> gte_rtpt, ComputeYPlaneScreenCoords -> gte_rtps) performs the
    // real GTE perspective divide (SZ/H -> screen XY), which needs a live H register. CERTAIN: raw
    // ctc2 decode at 0x80079024 (`ctc2 $h, $a0`, no shift) proves H is stored verbatim, no scaling.
    private static int gteH;
    public static void LdH(int screenH)
    {
        gteH = screenH;
    }

    // GHIDRA: SetGeomScreen @ 0x80079024
    // CERTAIN: thin GTE wrapper; decompilation is gte_ldH(screenH). Loads the GTE projection
    // screen distance from frontend page descriptors.
    public static void SetGeomScreen(int screenH)
    {
        LdH(screenH);
    }

    // === Perspective-projection register file additions (2026-07-04) ===
    // JUSTIFICATION: PSX hardware adaptation — OFX/OFY (screen-center offset, ctc2 $24/$25), the
    // Z-FIFO (SZ0-3, unsigned 16-bit) and XY-FIFO (SXY0-2, packed signed 16-bit pairs) plus MAC0
    // (used by both the perspective-divide numerator and, unrelatedly, by NCLIP's cross product).
    private static int gteOFX, gteOFY;                 // ctc2 $ofx/$ofy — CERTAIN: pre-shifted <<16, see SetGeomOffset
    private static readonly ushort[] gteSZ = new ushort[4];      // SZ0..SZ3 (SZ0 unused by this port's call graph)
    private static readonly short[][] gteSXY = { new short[2], new short[2], new short[2] }; // SXY0..SXY2, each [sx,sy]
    private static int gteMAC0;                         // MAC0 — written by NCLIP (cross product) and, as the LAST
                                                          // thing RTPS/RTPT do, by the depth-cue term DQB + DQA*q

    // GHIDRA: the DQA/DQB control-register pair (ctc2 $27/$28), loaded once by InitGeom @0x80077F68 —
    // `addiu $t0,$zero,-4194` / `ctc2 $t0,$27` at 0x80077FD0-0x80077FD4 and `lui $t0,0x0140` /
    // `ctc2 $t0,$28` at 0x80077FDC-0x80077FE0.
    // CERTAIN that they never change afterwards: an exact ctc2 scan of the whole 0x80010000 text
    // segment finds only two other writers, the three-word setters at 0x80078FAC (DQA) and
    // 0x80078FB8 (DQB), and their single caller 0x80077E64 has no `jal` xref anywhere in
    // SLUS_006.62. So every RTPS this game runs uses exactly these two values.
    // WHY THEY ARE HERE NOW. RTPS/RTPT end by writing `MAC0 = DQB + DQA*q` (the depth-cue numerator,
    // psx-spx's third and last MAC0 store of the op), and until 2026-08-03 this port left MAC0
    // holding the SECOND store instead — the SY numerator `q*IR2 + OFY`. Nothing read it that way
    // until FUN_800D1384 @0x800D1384, which gates its entire loop on `gte_stopz != 0` straight after
    // an RTPT. With the real values that expression is 0x1400000 - 4194*q, which is never exactly
    // zero for an integer q, so the guard cannot fire; with the old model it fired whenever the
    // projected SY numerator happened to land on zero. Render3DParticleFlare @0x800D004C,
    // Render3DGouraudTriangle @0x800D0E88 and Render3DTexturedQuad @0x800D2370 read MAC0 the same
    // way and were dropping primitives for the same wrong reason.
    // MEASURED 2026-08-10 by EXHIBITING THE ALGEBRA rather than by looking at a screen, which is the
    // stronger proof and the one available:
    //   FIXED model  MAC0 = DQB + DQA*q = 20971520 - 4194*q. Swept exhaustively over q in
    //     [0, 65535]: ZERO values make it zero. The only real root is 20971520/4194 = 5000.362424,
    //     which is not an integer -- the expression straddles it, 1520 at q=5000 and -2674 at
    //     q=5001, and never lands on 0. So `gte_stopz != 0` CANNOT fire after an RTPS/RTPT, and the
    //     guard is dead code, which is exactly what the renderers need it to be.
    //   OLD model    MAC0 = q*IR2 + OFY, the SY numerator. It vanishes for ordinary GTE values --
    //     e.g. q=1792 with IR2=-4096 against OFY = 112<<16 -- and in general for any q*IR2 == -OFY.
    //     Every such point was a primitive the renderer DROPPED.
    private static int gteDQA = -4194;
    private static int gteDQB = 0x1400000;

    // GHIDRA: gte_SetGeomOffset @ 0x800661a4/0x800661cc call sites (SetGeomOffsetFromTextSlot3,
    // SetDefaultGeomOffset) — CERTAIN: raw disassembly at both real call sites shows
    // `sll $t4,ofx,0x10` / `sll $t5,ofy,0x10` immediately before `ctc2 $ofx,$t4` / `ctc2 $ofy,$t5` —
    // the SDK macro left-shifts its plain-pixel-unit arguments by 16 before loading the GTE control
    // registers (matches the observed call values 0xA0/0x70 = 160/112 = half of a 320x224 screen).
    public static void SetGeomOffset(int ofx, int ofy)
    {
        gteOFX = ofx << 16;
        gteOFY = ofy << 16;
    }

    // GHIDRA: InitGeom @ 0x8006E1F0 (TITLE.EXE)
    // The original enables COP2 in the Status register through _patch_gte/setCopReg, then loads
    // seven control registers:
    //   ZSF3 = 0x155, ZSF4 = 0x100, H = 1000, DQA = 0xFFFFEF9E, DQB = 0x1400000, OFX = 0, OFY = 0
    // Enabling the coprocessor has no desktop equivalent: this GTE is a software model that is
    // always available. The register loads do carry over, and they are what this reproduces.
    // ZSF3 is not a field here — LibGte already pins it to InitGeom's 0x155, see the depth-cue
    // note further down — and ZSF4 is not modelled at all.
    public static void InitGeom()
    {
        LdH(1000);
        gteDQA = -4194;
        gteDQB = 0x1400000;
        gteOFX = 0;
        gteOFY = 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the FC (far colour) control register triplet, COP2 $21-$23.
    // PARTIAL: stored so SetFarColor keeps its observable contract, but no GTE operation modelled
    // here consumes FC yet — it feeds the depth-cue path (gte_dpcs/gte_dpct), which is not
    // implemented. Reading these back is the only way to tell the value was received.
    private static int gteRFC, gteGFC, gteBFC;

    // GHIDRA: SetFarColor @ 0x8006D884 (TITLE.EXE)
    // Raw disassembly is three `sll ,0x4` then `ldfcdir`, so the SDK macro shifts its plain
    // arguments left by 4 before loading the control registers.
    public static void SetFarColor(long rfc, long gfc, long bfc)
    {
        gteRFC = (int)rfc << 4;
        gteGFC = (int)gfc << 4;
        gteBFC = (int)bfc << 4;
    }

    private static ushort SatZ(int v) => (ushort)(v > 0xFFFF ? 0xFFFF : v < 0 ? 0 : v);
    private static short SatSxy(int v) => (short)(v > 0x3FF ? 0x3FF : v < -0x400 ? -0x400 : v);

    // JUSTIFICATION: C# language bridge — RTPS and RTPT (below) both perform this exact per-point
    // formula (RTPT is documented as "RTPS x3" against the loaded V0/V1/V2 triple); factoring out the
    // shared math is a mechanical extraction, not a merge of two distinct original functions (each
    // keeps its own GHIDRA-addressed entry point and FIFO push-count).
    // PARTIAL: overall structure (rotate+translate -> IR, SZ=Sat0..FFFF(MAC3), quotient=H/SZ,
    // SX/SY=Sat11((quotient*IR + OFX/OFY) >> 16)) is the well-documented public PSX GTE RTPS/RTPT
    // formula; the exact hardware UNR reciprocal-table division algorithm is NOT bit-for-bit
    // reproduced here (plain integer division is used instead) — not independently disassembly-
    // verified past the MAC/IR/SZ stage (which IS CERTAIN, see gte_rtir/gte_rt evidence in the file
    // header, same MVMVA-family math with sf:1,lm:0,mx:rt,v:v0,cv:tr).
    private static void PerspectiveTransformPoint(short vx, short vy, short vz, out short outIR1, out short outIR2, out ushort outSZ, out short outSX, out short outSY)
    {
        long mac1 = (long)gteR[0] * vx + (long)gteR[1] * vy + (long)gteR[2] * vz;
        long mac2 = (long)gteR[3] * vx + (long)gteR[4] * vy + (long)gteR[5] * vz;
        long mac3 = (long)gteR[6] * vx + (long)gteR[7] * vy + (long)gteR[8] * vz;
        gteMAC1 = (int)(mac1 >> 12) + gteTR[0];
        gteMAC2 = (int)(mac2 >> 12) + gteTR[1];
        gteMAC3 = (int)(mac3 >> 12) + gteTR[2];
        gteIR1 = Sat16(gteMAC1);
        gteIR2 = Sat16(gteMAC2);
        gteIR3 = Sat16(gteMAC3);

        ushort sz = SatZ(gteMAC3); // sf=1 => SZ3 = Sat0..FFFF(MAC3 >> 0)
        int quotient = sz == 0 ? 0x1FFFF : (int)Math.Min(0x1FFFFL, ((long)gteH << 16) / sz);
        gteMAC0 = quotient * gteIR1 + gteOFX;
        short sx = SatSxy(gteMAC0 >> 16);
        gteMAC0 = quotient * gteIR2 + gteOFY;
        short sy = SatSxy(gteMAC0 >> 16);
        // The op's THIRD and last MAC0 store: the depth-cue numerator. IR0, its saturated >> 12, is
        // not exposed by this port's helpers and so is not kept; MAC0 is, because gte_stopz reads it.
        gteMAC0 = quotient * gteDQA + gteDQB;

        outIR1 = gteIR1; outIR2 = gteIR2; outSZ = sz; outSX = sx; outSY = sy;
    }

    // GHIDRA: gte_rtps (nRTPS) — real MVMVA math, mx=RT, v=V0, cv=TR, sf=1, lm=0 (CERTAIN: closed via
    // raw COP2 instruction-word decode at ComputeYPlaneScreenCoords 0x8003c47c ->
    // `gte::rtps<sf:1,lm:0,mx:rt,v:v0,cv:tr>`, funct=0x01). Transforms V0 (rotate+translate, same as
    // gte_rt) then perspective-projects it, pushing the result into the Z-FIFO (as SZ3) and the
    // XY-FIFO (as SXY2 — CERTAIN: `swc2 $sxy2` observed immediately after at 0x8003c480).
    public static void Rtps()
    {
        PerspectiveTransformPoint(gteV0[0], gteV0[1], gteV0[2], out _, out _, out ushort sz, out short sx, out short sy);
        gteSZ[3] = sz;
        gteSXY[2][0] = sx; gteSXY[2][1] = sy;
    }

    // GHIDRA: gte_stsxy — reads back the newest XY-FIFO slot (SXY2, CERTAIN: `swc2 $sxy2` at
    // 0x8003c480) as one packed 32-bit word (low 16 = SX, high 16 = SY — standard GTE SXY register
    // packing).
    // JUSTIFICATION: C# language bridge — destination in this port is always a byte[] scratch buffer.
    public static void StSxy(byte[] buf, int byteOffset)
    {
        uint packed = (ushort)gteSXY[2][0] | ((uint)(ushort)gteSXY[2][1] << 16);
        BitConverter.GetBytes(packed).CopyTo(buf, byteOffset);
    }

    // GHIDRA: gte_stsxy — same register read as the byte[] overload above, returned directly.
    // JUSTIFICATION: C# language bridge — TransformRenderVertices / TransformSingleObjectRender store
    // into RenderParameter.screenXYWordA_0x5C / screenXYWordB_0x64, which are plain uint fields in
    // this port rather than byte-buffer offsets. Same "no scratchpad round-trip" convention as StOpz.
    public static uint StSxy() => (ushort)gteSXY[2][0] | ((uint)(ushort)gteSXY[2][1] << 16);

    // GHIDRA: gte_stsz — CERTAIN: raw `stsz` mnemonic observed at RenderEffectQueueSprite 0x800e06b8
    // (single-value counterpart of gte_stsz3c above). Stores back the newest Z-FIFO slot (SZ3, the
    // same slot rtps just pushed to) as one zero-extended 32-bit word.
    // JUSTIFICATION: C# language bridge — destination in this port is always a byte[] scratch buffer.
    public static void StSz(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes((uint)gteSZ[3]).CopyTo(buf, byteOffset);
    }

    // GHIDRA: gte_stSZ3 — same register read as the byte[] overload above, returned directly.
    // JUSTIFICATION: C# language bridge — RenderMeshWireframe @0x8007041c stores only the LOW SHORT
    // of SZ3 into its scratchpad SZ array (`*(short *)(base + i*2) = (short)uVar19`), a 2-byte stride,
    // so the 4-byte overload above cannot be used. Same "no scratchpad round-trip" convention as
    // StSxy() and StOpz().
    public static int StSz() => gteSZ[3];

    // GHIDRA: gte_ldv3c — loads V0,V1,V2 (data regs 0-5) from 3 consecutive 8-byte TMDVertex-shaped
    // entries (packed vx,vy then vz,pad — CERTAIN: raw lwc2 $vxy0/$vz0/$vxy1/$vz1/$vxy2/$vz2 decode
    // at TransformAndProjectVertices 0x8003ae8c-0x8003aea0, 6 words / 24 bytes = 3x TMDVertex).
    // JUSTIFICATION: C# language bridge — reuses the existing gteV3 register slots (same as ldv3).
    public static void LdV3c(byte[] buf, int byteOffset)
    {
        for (int n = 0; n < 3; n++)
        {
            int o = byteOffset + n * 8;
            gteV3[n][0] = BitConverter.ToInt16(buf, o);
            gteV3[n][1] = BitConverter.ToInt16(buf, o + 2);
            gteV3[n][2] = BitConverter.ToInt16(buf, o + 4);
        }
    }

    // GHIDRA: gte_rtpt (nRTPT) — real MVMVA-family math, mx=RT, v=V0(triple), cv=TR, sf=1, lm=0
    // (CERTAIN: closed via raw COP2 instruction-word decode/bit-layout at TransformAndProjectVertices
    // 0x8003aeac -> word 0x4A280030 -> sf=1,lm=0,funct=0x30=RTPT, cross-validated bit-for-bit against
    // the already-closed gte_rtir/gte_rt MVMVA encodings at the same call site). Applies
    // PerspectiveTransformPoint to each of V0,V1,V2 (as loaded by ldv3c) in order, pushing 3 results
    // through the Z-FIFO and XY-FIFO — CERTAIN the final readable state after exactly 3 pushes is
    // SZ1=point0,SZ2=point1,SZ3=point2 and SXY0=point0,SXY1=point1,SXY2=point2 (raw `swc2
    // $sxy0/$sxy1/$sxy2` / `$sz1/$sz2/$sz3` decode at 0x8003aec0-0x8003aed4), so this port writes
    // those slots directly rather than modeling a 4-deep shift register.
    public static void Rtpt()
    {
        for (int n = 0; n < 3; n++)
        {
            PerspectiveTransformPoint(gteV3[n][0], gteV3[n][1], gteV3[n][2], out _, out _, out ushort sz, out short sx, out short sy);
            gteSZ[n + 1] = sz;
            gteSXY[n][0] = sx; gteSXY[n][1] = sy;
        }
    }

    // GHIDRA: gte_stsxy3c — stores SXY0,SXY1,SXY2 as 3 packed 32-bit words (CERTAIN: raw `swc2
    // $sxy0/$sxy1/$sxy2` decode at 0x8003aec0-0x8003aec8).
    public static void StSxy3c(byte[] buf, int byteOffset)
    {
        for (int n = 0; n < 3; n++)
        {
            uint packed = (ushort)gteSXY[n][0] | ((uint)(ushort)gteSXY[n][1] << 16);
            BitConverter.GetBytes(packed).CopyTo(buf, byteOffset + n * 4);
        }
    }

    // GHIDRA: gte_stsz3c — stores SZ1,SZ2,SZ3 as 3 separate 32-bit words, zero-extended (CERTAIN: raw
    // `swc2 $sz1/$sz2/$sz3` decode at 0x8003aecc-0x8003aed4).
    public static void StSz3c(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes((uint)gteSZ[1]).CopyTo(buf, byteOffset);
        BitConverter.GetBytes((uint)gteSZ[2]).CopyTo(buf, byteOffset + 4);
        BitConverter.GetBytes((uint)gteSZ[3]).CopyTo(buf, byteOffset + 8);
    }

    // GHIDRA: gte_ldsxy3 — direct (non-FIFO-push) load of SXY0,SXY1,SXY2 from 3 already-packed 32-bit
    // words (CERTAIN: raw `mtc2 $sxy0/$sxy2/$sxy1` decode at InsertPrimitivesIntoOrderingTable
    // 0x8003b204-0x8003b20c — a plain register write, not a FIFO push, unlike stsxy3c's producer
    // side).
    public static void LdSxy3(uint sxy0, uint sxy1, uint sxy2)
    {
        gteSXY[0][0] = (short)(ushort)sxy0; gteSXY[0][1] = (short)(ushort)(sxy0 >> 16);
        gteSXY[1][0] = (short)(ushort)sxy1; gteSXY[1][1] = (short)(ushort)(sxy1 >> 16);
        gteSXY[2][0] = (short)(ushort)sxy2; gteSXY[2][1] = (short)(ushort)(sxy2 >> 16);
    }

    // GHIDRA: gte_ldSXY2 — loads only the newest XY-FIFO slot (SXY2) from a packed (X | Y<<16) word,
    // leaving SXY0/SXY1 as previously loaded. Used by ValidateEntityWalkmeshMovement to re-run NCLIP
    // against a second test point while keeping the same boundary line in SXY0/SXY1.
    public static void LdSxy2(uint sxy2)
    {
        gteSXY[2][0] = (short)(ushort)sxy2; gteSXY[2][1] = (short)(ushort)(sxy2 >> 16);
    }

    // GHIDRA: gte_nclip (nNCLIP) — CERTAIN: closed via raw COP2 decode at InsertPrimitivesIntoOrderingTable
    // 0x8003b218 -> word 0x4b400006, funct=0x06=NCLIP. Computes the signed double-area (2D cross
    // product) of the screen-space triangle SXY0,SXY1,SXY2 into MAC0, no shift/saturation — the
    // well-documented public NCLIP formula (backface/degenerate test: <=0 means facing away or
    // degenerate).
    public static void NClip()
    {
        int sx0 = gteSXY[0][0], sy0 = gteSXY[0][1];
        int sx1 = gteSXY[1][0], sy1 = gteSXY[1][1];
        int sx2 = gteSXY[2][0], sy2 = gteSXY[2][1];
        gteMAC0 = sx0 * sy1 - sx1 * sy0 + sx1 * sy2 - sx2 * sy1 + sx2 * sy0 - sx0 * sy2;
    }

    // GHIDRA: gte_stopz — CERTAIN: despite the OTZ-suggesting pseudo-name (as printed by the
    // decompiler for this call), raw disassembly at InsertPrimitivesIntoOrderingTable 0x8003b224
    // decodes to `swc2 $mac0, 0x0(s3)` — i.e. this reads back MAC0 (the NCLIP cross product just
    // computed above), NOT the OTZ ordering-table-Z register. The original then loads that scratchpad
    // word straight back (`lw v0,0(s3)`) to test `< 1`. This port skips the scratchpad round-trip
    // (JUSTIFICATION: C# language bridge — gteMAC0 is already a directly readable C# field, matching
    // this file's existing "no literal 0x1F800000 scratchpad" convention, see file header) and
    // returns the value directly.
    public static int StOpz() => gteMAC0;

    // GHIDRA: gte_ldLZCS / gte_stlzc — CERTAIN: raw COP2 decode in ScriptCmd_CheckInput @0x800130b4.
    // 0x80013188 is `4882f000` = MTC2 v0,$30 (LZCS) and 0x800131a0 is `e8df0000` = SWC2 $31,0x0(a2)
    // (LZCR). Writing LZCS immediately computes LZCR: the number of leading bits equal to bit 31 —
    // leading ZEROS for a non-negative value, leading ONES for a negative one — always in 1..32.
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the GTE LZCS/LZCR register pair's observable contract. This is a bit
    // count with an exactly specified result, so it is reproduced exactly rather than approximated;
    // unlike SquareRoot0 above there is no ROM table and no rounding involved.
    private static int gteLZCR;

    public static void LdLZCS(uint value)
    {
        // Complementing a negative value turns "leading ones" into "leading zeros", so one loop
        // covers both signs. v == 0 is the all-same-bits case (0x00000000 or 0xffffffff) -> 32.
        uint v = (value & 0x80000000) != 0 ? ~value : value;
        int count = 0;
        if (v == 0)
        {
            count = 32;
        }
        else
        {
            while ((v & 0x80000000) == 0)
            {
                v <<= 1;
                count++;
            }
        }
        gteLZCR = count;
    }

    public static int StLZCR() => gteLZCR;

    // GHIDRA: SquareRoot0 @ 0x80078004
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the psyq SquareRoot0 (MSC02.OBJ) observable contract.
    // CERTAIN (raw disassembly 0x80078004-0x80078084): the original normalises `a` into [0x40,0xFF]
    // using the GTE's leading-zero-count register (mtc2 lzcs / mfc2 lzcr), indexes the 192-entry
    // ROM sqrt table at 0x800960bc, and rescales by (0x1f - (lzc & ~1)) >> 1 then >> 12. Verified
    // numerically (a=0x10000 -> 256, a=100 -> 10, a=0 -> 0): the contract is the truncated integer
    // square root of `a`. Reproduced here with ordinary C# integer arithmetic rather than by
    // emulating the GTE LZCR op and extracting the ROM table (rules 13/14). The table's own rounding
    // can differ from an exact integer sqrt by at most one unit on some inputs; that residual is a
    // documented approximation, not a modelled behaviour.
    public static int SquareRoot0(int a)
    {
        if (a <= 0)
        {
            return 0;
        }
        uint value = (uint)a;
        uint root = 0;
        uint bit = 1u << 30;
        while (bit > value)
        {
            bit >>= 2;
        }
        while (bit != 0)
        {
            if (value >= root + bit)
            {
                value -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }
            bit >>= 2;
        }
        return (int)root;
    }

    // GHIDRA: rsin @ 0x80077cf4
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the psyq rsin (MSC02.OBJ) observable contract — sine of a 12-bit angle
    // GHIDRA: ApplyMatrixLV @ 0x80078934 — PSX SDK (Ghidra's own comment: "Possible
    // MTX_004.OBJ/ApplyMatrixLV").
    // JUSTIFICATION: PSX hardware adaptation only — rule 13/14, the same treatment SquareRoot0,
    // VectorNormal, rsin and rcos already get: the observable contract is reproduced, the GTE
    // microcode is not.
    // CONTRACT, derived rather than assumed. The routine splits each 32-bit input component into
    // `hi = v >> 15` and `lo = v & 0x7fff` (both magnitude-preserving, so the sign survives), runs
    // hi through MVMVA with **sf = 0** (gte_rtir_sf0_b, no shift), lo through MVMVA with **sf = 1**
    // (gte_rtir_b, >> 12), and returns `lo_result + (hi_result << 3)`. Since
    //     m*v = m*(hi << 15) + m*lo,  and  (m*v) >> 12 = ((m*hi) << 3) + ((m*lo) >> 12),
    // that is exactly `out = (m x in) >> 12` — the split exists only to keep each half inside the
    // GTE's 16-bit input range. Computed here in 64-bit so the intermediate cannot overflow.
    // (The original's `if (x < 0) x = x * 8; else x = x << 3;` is one operation written twice, a
    // signed-shift codegen artefact, not two cases.)
    public static VECTOR ApplyMatrixLV(MATRIX m, VECTOR v2, VECTOR v3)
    {
        long vx = v2.vx;
        long vy = v2.vy;
        long vz = v2.vz;
        v3.vx = (int)((m.m[0] * vx + m.m[1] * vy + m.m[2] * vz) >> 12);
        v3.vy = (int)((m.m[3] * vx + m.m[4] * vy + m.m[5] * vz) >> 12);
        v3.vz = (int)((m.m[6] * vx + m.m[7] * vy + m.m[8] * vz) >> 12);
        return v3;
    }

    // MOVED 2026-07-31 from Remaster/TmdSystem.cs, together with RotMatrixYXZ. These are ROM
    // trig-table accessors, not game logic, and leaving them in Remaster forced a PsxSdk ->
    // Remaster `using` — a layering inversion worse than the rule-13 placement it was fixing.
    // NOT merged into rsin/rcos above despite computing the same function: those round with
    // MidpointRounding.AwayFromZero and these with the default ToEven, which can differ by one
    // unit at exact midpoints. Merging them would be a silent behaviour change, so both stay.
    // JUSTIFICATION: PSX hardware/ROM-data adaptation — DAT_800966ec is a 4096-entry (16 KB) fixed
    // ROM sin/cos table (angle units = 1/4096 of a circle; stride 4 bytes; low 16 bits = sin*4096,
    // high 16 bits = cos*4096). CERTAIN (closed 2026-07-03 via raw memory read,
    // mcp__ReVa__read-memory, 0x800966ec, cross-checked against Math.Sin/Cos): entry0 = 0x10000000
    // -> sin(0)=0, cos(0)=0x1000 (1.0 Q12); entry1 = low16=6 -> matches
    // Round(Math.Sin(1*2*PI/4096)*4096)=6 exactly. The table is the standard PSX quarter-wave
    // sin/cos convention, so it is computed here via real trig rather than embedding all 16 KB of
    // ROM bytes verbatim — mathematically identical to the ROM table for every angle (verified at
    // 2 independent points; a full byte-for-byte diff of the ROM table was not performed, hence
    // PARTIAL rather than CERTAIN on every single one of the 4096 entries individually).
    // Used by both RotMatrix (this file) and RotMatrixYXZ (AnimationSystem.cs) — the same ROM table.
    public static int GetTrigSinQ12(int angleUnits)
    {
        double radians = (angleUnits & 0xfff) * (2.0 * Math.PI / 4096.0);
        return (int)Math.Round(Math.Sin(radians) * 4096.0);
    }

    public static int GetTrigCosQ12(int angleUnits)
    {
        double radians = (angleUnits & 0xfff) * (2.0 * Math.PI / 4096.0);
        return (int)Math.Round(Math.Cos(radians) * 4096.0);
    }

    // (0x1000 = 360 degrees), returned in Q12 so the range is [-4096, 4096]. Same angle unit ratan2
    // produces, which is what lets ApplyKnockbackReaction feed one straight into the other.
    // Reproduced with ordinary C# floating point rather than by extracting the ROM sine table; the
    // table's own quantisation can differ by at most one unit on some inputs.
    public static int rsin(int angle)
    {
        return (int)Math.Round(Math.Sin(angle * 2.0 * Math.PI / 4096.0) * 4096.0,
                               MidpointRounding.AwayFromZero);
    }

    // GHIDRA: rcos @ 0x80077dc4
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the cosine counterpart of rsin above, same units and same range.
    public static int rcos(int angle)
    {
        return (int)Math.Round(Math.Cos(angle * 2.0 * Math.PI / 4096.0) * 4096.0,
                               MidpointRounding.AwayFromZero);
    }

    // GHIDRA: gte_stlvnl — stores the CURRENT MAC1-3 as 3 consecutive 32-bit words. The "nl" is
    // no-limit: it is the UNSATURATED counterpart of stlvl above, which stores IR1-3. Used by
    // RotTrans @0x800792d4.
    public static void StLvnl(VECTOR outVec)
    {
        outVec.vx = gteMAC1;
        outVec.vy = gteMAC2;
        outVec.vz = gteMAC3;
    }

    // GHIDRA: RotTrans @ 0x800792d4
    // CERTAIN (40 bytes, full decompilation): `gte_ldv0(param_1)`, then `copFunction(2, 0x480012)`,
    // then `gte_stlvnl(param_2)` and the FLAG word into `*param_3`.
    // The raw command word 0x480012 decodes to MVMVA with funct 0x12, sf=1 (bit 19), mx=0 (rotation
    // matrix), v=0 (V0), cv=0 (TR), lm=0 — which is exactly the `gte_rt` this file already models,
    // so it is reused rather than re-derived.
    // BLOCKED: the GTE FLAG register is not modelled in this port, so the overflow/saturation word
    // is written as 0. DrawTargetCursor, its only caller in this tree, passes a stack slot it never
    // reads back.
    public static void RotTrans(SVECTOR param_1, VECTOR param_2, int[] param_3)
    {
        LdV0(param_1);
        Rt();
        StLvnl(param_2);
        param_3[0] = 0;
    }

    // GHIDRA: TransMatrix @ 0x80078c94
    // CERTAIN (36 bytes, full decompilation): copies the three words of a VECTOR into the matrix's
    // translation column and returns the matrix.
    // Name left raw: Ghidra's own note calls it a "Possible MTX_07.OBJ/TransMatrix" — probable, not
    // proven, so the raw name stands per the naming rules.
    // JUSTIFICATION: C# language bridge only
    // RELATION: libgte declares TransMatrix(MATRIX *, VECTOR *). The int[] form below predates
    // this one; call sites that hold a real VECTOR, such as FUN_80037388 @ 0x80037388, reach this
    // overload instead of unpacking the vector at every call.
    public static MATRIX TransMatrix(MATRIX matrix, VECTOR v)
    {
        matrix.t[0] = v.vx;
        matrix.t[1] = v.vy;
        matrix.t[2] = v.vz;
        return matrix;
    }

    public static MATRIX TransMatrix(MATRIX matrix, int[] param_2)
    {
        int lVar1;
        int lVar2;

        lVar1 = param_2[1];
        lVar2 = param_2[2];
        matrix.t[0] = param_2[0];
        matrix.t[1] = lVar1;
        matrix.t[2] = lVar2;
        return matrix;
    }

    // GHIDRA: OuterProduct0 @ 0x800791d0 — PSX SDK (psyq SMP_00.OBJ). `v2 = v0 x v1`, the GTE
    // outer product, i.e. the cross product. Ghidra had already named it; its parameters were
    // renamed to v0/v1/v2 and the decode recorded as a plate comment there (2026-08-03).
    // CERTAIN, every COP2 word decoded by script rather than read off the listing:
    //   `cfc2 $c0/$c2/$c4` saves R11R12, R22R23 and R33; `ctc2` then loads them from v0's three
    //   components, so the matrix DIAGONAL becomes (v0.vx, v0.vy, v0.vz) — each truncated to a
    //   short, because R11/R22/R33 are 16-bit halves of those control registers.
    //   `lwc2 $c11/$c9/$c10` puts v1 into IR3, IR1, IR2 — vz FIRST, which reads like a slip and is
    //   not one; only the register numbers matter.
    //   `0x4B70000C` is OP with sf=0, lm=0:
    //     MAC1 = R22*IR3 - R33*IR2   MAC2 = R33*IR1 - R11*IR3   MAC3 = R11*IR2 - R22*IR1
    //   which is exactly v0 x v1. `swc2 $c25/$c26/$c27` then reads MAC1-3 back.
    // TWO CONSEQUENCES OF sf=0 AND THE MAC READBACK: the products are NOT shifted right 12, and the
    // result is NOT clamped to 16 bits — it leaves as three full 32-bit accumulators. Using IR here
    // instead, as several other routines in this file do, would silently saturate at +-0x7FFF.
    // The three control registers are saved and restored around the op, so this does NOT clobber
    // the GTE rotation matrix — unlike ApplyMatrix, CompMatrix and MulMatrix0 above.
    // ZERO static callers in SLUS_006.62; reached only from the PE.IMG scene overlays.
    public static VECTOR OuterProduct0(VECTOR v0, VECTOR v1, VECTOR v2)
    {
        // PSX: the diagonal is loaded through R11/R22/R33, which are 16-bit.
        int r11 = (short)v0.vx;
        int r22 = (short)v0.vy;
        int r33 = (short)v0.vz;
        int ir1 = (short)v1.vx;
        int ir2 = (short)v1.vy;
        int ir3 = (short)v1.vz;
        v2.vx = r22 * ir3 - r33 * ir2;
        v2.vy = r33 * ir1 - r11 * ir3;
        v2.vz = r11 * ir2 - r22 * ir1;
        return v2;
    }

    // GHIDRA: RotMatrix @ 0x800794c4
    // CERTAIN (100 bytes, full decompilation reviewed): builds a full 3-axis rotation matrix into
    // `param_2` from the three 12-bit angles in `param_1`, and returns it. Each of sin/cos comes out
    // of one packed table word — sin in the low half (`(short)`), cos in the high half (`>> 0x10`).
    // JUSTIFICATION: PSX hardware adaptation — this port supplies that table's contents through the
    // rsin/rcos adapters, in the same 12-bit angle / Q12 units, exactly as RotMatrixY below already
    // does. Quantisation can differ by at most one unit on some inputs, as recorded on rsin.
    // LITERAL DETAIL WORTH KEEPING: the two-term rows truncate EACH product to short BEFORE the
    // add/subtract (`(short)(a >> 0xc) - (short)(b >> 0xc)`), not after. That is observable and is
    // reproduced as written.
    // PARTIAL: a NEGATIVE vx, vy or vz each take a different path in the original (FGO_01_OBJ_64,
    // FGO_01_OBJ_CC, FGO_01_OBJ_160 — SDK internals that are not decompiled). Masking with 0xFFF
    // makes negative angles behave as their positive equivalent here, which is the natural reading
    // of a 12-bit angle but is NOT proven to be what those branches do. Same situation, and the same
    // wording, as RotMatrixY below. The only caller in this port's tree, DrawTargetCursor, passes
    // `(0, 0, (g_globalFrameCounter & 0x3f) << 6)` — all three components non-negative.
    public static MATRIX RotMatrix(SVECTOR param_1, MATRIX param_2)
    {
        short sVar1;
        int iVar3;
        int iVar4;
        int iVar5;
        int iVar6;
        int iVar7;
        short sVar8;
        int iVar9;

        iVar6 = rsin(param_1.vx & 0xfff);
        iVar3 = rcos(param_1.vx & 0xfff);
        sVar8 = (short)rsin(param_1.vy & 0xfff);
        iVar9 = -(int)sVar8;
        iVar4 = rcos(param_1.vy & 0xfff);
        sVar1 = param_1.vz;
        param_2.m[2] = sVar8;                                   // m[0][2]
        param_2.m[5] = (short)(-(iVar4 * iVar6) >> 0xc);        // m[1][2]
        sVar8 = (short)(iVar4 * iVar3 >> 0xc);
        param_2.m[8] = sVar8;                                   // m[2][2]
        iVar7 = rsin(sVar1 & 0xfff);
        iVar5 = rcos(sVar1 & 0xfff);
        param_2.m[0] = (short)(iVar5 * iVar4 >> 0xc);           // m[0][0]
        param_2.m[1] = (short)(-(iVar7 * iVar4) >> 0xc);        // m[0][1]
        iVar4 = iVar5 * iVar9 >> 0xc;
        param_2.m[3] = (short)((short)(iVar7 * iVar3 >> 0xc) - (short)(iVar4 * iVar6 >> 0xc)); // m[1][0]
        param_2.m[6] = (short)((short)(iVar7 * iVar6 >> 0xc) + (short)(iVar4 * iVar3 >> 0xc)); // m[2][0]
        iVar9 = iVar7 * iVar9 >> 0xc;
        param_2.m[4] = (short)((short)(iVar5 * iVar3 >> 0xc) + (short)(iVar9 * iVar6 >> 0xc)); // m[1][1]
        param_2.m[7] = (short)((short)(iVar5 * iVar6 >> 0xc) - (short)(iVar9 * iVar3 >> 0xc)); // m[2][1]
        return param_2;
    }

    // GHIDRA: RotMatrixY @ 0x80079c74
    // CERTAIN (full decompilation reviewed, 100 bytes): right-multiplies `m` by a rotation of
    // `angle` about Y, in place, and returns it. Rows 0 and 2 are recombined against cos/-sin and
    // sin/cos with a Q12 shift; row 1 (the Y axis itself) is untouched, which is what makes this a
    // Y rotation rather than the general RotMatrix.
    // The original reads both terms out of one packed table word at DAT_800966ec + (angle & 0xfff)*4
    // — cos in the high half (`>> 0x10`), -sin from the negated low half (`-(short)`).
    // JUSTIFICATION: PSX hardware adaptation — this port already supplies that table's contents
    // through the rsin/rcos adapters above, in the same 12-bit angle / Q12 result units, so the two
    // terms are taken from those instead of extracting the ROM table. Quantisation can differ by at
    // most one unit on some inputs, exactly as recorded on rsin.
    // CLOSED 2026-07-31 — the negative-angle branch is EXACTLY equivalent to the `& 0xfff` used here.
    // Same finding, and same method, as RotMatrixZ below; see that function's note for the full
    // decode and for the ROM-table symmetry measurement, which is what both proofs rest on.
    // `FGO_05_OBJ_64` is likewise a fake `*_OBJ_<hex>` label rather than an SDK internal — the common
    // tail at 0x80079cd8, inside RotMatrixY's own 100 bytes.
    // The sign convention here is the MIRROR of RotMatrixZ's, and it cancels the same way. Raw MIPS:
    //   positive arm (0x80079cbc-0x80079cd4): sra t7,...,0x10 then `subu t1,zero,t7` -> t1 = -sin[m]
    //   negative arm (0x80079ca4-0x80079cb0): sra t1,...,0x10 with NO negation -> t1 = +sin[4096-m]
    // and sin[4096-m] == -sin[m], so both arms yield -sin[m]; cos is taken as `word >> 16` on both.
    // Decoded independently of RotMatrixZ rather than assumed by symmetry, because the two functions
    // negate on OPPOSITE arms and a symmetry argument would have hidden that.
    // Note this was never load-bearing for RotMatrixY: RenderAttackRangeDome, its only caller, hands
    // over `uVar2 & 0xfffe` — always non-negative. It is closed here only so the two do not drift.
    public static MATRIX RotMatrixY(uint angle, MATRIX m)
    {
        int iVar1;
        int iVar3;
        int iVar4;
        int iVar5;
        int iVar6;
        int iVar7;
        int iVar8;
        int iVar9;

        iVar1 = -rsin((int)(angle & 0xfff));
        iVar3 = rcos((int)(angle & 0xfff));
        iVar4 = m.m[0];
        iVar7 = m.m[6];
        iVar5 = m.m[1];
        iVar8 = m.m[7];
        iVar6 = m.m[2];
        iVar9 = m.m[8];
        m.m[0] = (short)(iVar3 * iVar4 - iVar1 * iVar7 >> 0xc);
        m.m[1] = (short)(iVar3 * iVar5 - iVar1 * iVar8 >> 0xc);
        m.m[2] = (short)(iVar3 * iVar6 - iVar1 * iVar9 >> 0xc);
        m.m[6] = (short)(iVar1 * iVar4 + iVar3 * iVar7 >> 0xc);
        m.m[7] = (short)(iVar1 * iVar5 + iVar3 * iVar8 >> 0xc);
        m.m[8] = (short)(iVar1 * iVar6 + iVar3 * iVar9 >> 0xc);
        return m;
    }

    // GHIDRA: gte_stopz — stores the CURRENT OTZ register (the averaged/ordering Z the RTP family
    // leaves behind). CERTAIN by symmetry with StSz/StSxy above: the pseudo-op is a single
    // `swc2 $otz` and this port keeps OTZ as SZ3 >> 2, which is exactly what RotTransPers returns.
    public static void StOpz(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes(gteSZ[3] >> 2).CopyTo(buf, byteOffset);
    }

    // GHIDRA: ratan2 @ 0x80079fb4
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the psyq ratan2 (RATAN.OBJ) observable contract.
    // CERTAIN (full decompilation reviewed): the original takes (y, x), works on absolute values,
    // divides the smaller by the larger, indexes the 12-bit ROM arctangent table at 0x8009a6ec, and
    // reassembles the quadrant: `0x400 - table[...]` for the |y| >= |x| branch, then `0x800 - a` when
    // x was negative and `-a` when y was negative. So the unit is the psyq 12-bit angle (0x1000 =
    // 360 degrees, 0x400 = 90 degrees) and the result range is [-0x800, 0x800]. That is corroborated
    // by two independent call sites: ScriptCmd_FaceEntity @0x80014054 does `0x1400 - ratan2(...) &
    // 0xfff` (0x1400 = 0x1000 + 0x400, i.e. a quarter-turn rotation kept inside one 12-bit turn), and
    // the combat aim checks compare the result against -0xab.
    // Reproduced with ordinary C# floating point rather than by extracting the ROM table (rules
    // 13/14); the table's own quantisation can differ by at most one unit on some inputs.
    public static int ratan2(int y, int x)
    {
        if (x == 0 && y == 0)
        {
            return 0;
        }
        double angle = Math.Atan2(y, x) * 4096.0 / (2.0 * Math.PI);
        return (int)Math.Round(angle, MidpointRounding.AwayFromZero);
    }

    // GHIDRA: VectorNormal @ 0x80078134 (body: MSC02_OBJ_100 @ 0x80078194)
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the psyq VectorNormal (MSC02.OBJ) observable contract.
    // CERTAIN (raw disassembly 0x80078194-0x80078250): GTE SQR of (vx,vy,vz) summed into one word,
    // leading-zero-count normalisation, a lookup in the 192-entry ROM reciprocal-sqrt table at
    // 0x80096250, then GPF(0) (ir0 * ir1..ir3) shifted right by (0x1f - (lzc & ~1)) >> 1. Verified
    // numerically with v = (4096,0,0) -> (4096,0,0): the contract is "scale v to length 4096", i.e.
    // the standard Q12 unit vector. `in` and `out` may alias (FindBoundaryPolygonEdgeForPoint calls
    // it with the same VECTOR twice), so the components are read before any write.
    public static void VectorNormal(VECTOR vin, VECTOR vout)
    {
        int vx = vin.vx;
        int vy = vin.vy;
        int vz = vin.vz;
        long lengthSquared = (long)vx * vx + (long)vy * vy + (long)vz * vz;
        if (lengthSquared == 0)
        {
            vout.vx = 0;
            vout.vy = 0;
            vout.vz = 0;
            return;
        }
        long length = (long)Math.Sqrt((double)lengthSquared);
        if (length == 0)
        {
            length = 1;
        }
        vout.vx = (int)((long)vx * 4096 / length);
        vout.vy = (int)((long)vy * 4096 / length);
        vout.vz = (int)((long)vz * 4096 / length);
    }

    // GHIDRA: ScaleMatrix @ 0x80078cc4 — PSX SDK (psyq MTX_*.OBJ), 39 call sites in SLUS_006.62.
    // CERTAIN (full decompilation with synchronized disassembly reviewed): scales the rotation part
    // of `m` in place, column-wise, in Q12: for each flat element k of m.m[0..8],
    //     m.m[k] = (short)((short)m.m[k] * v[k % 3] >> 12)
    // The original works on the five 32-bit words that pack the nine shorts, which is what makes the
    // k%3 pairing visible: word 0 takes (vx,vy), word 1 (vz,vx), word 2 (vy,vz), word 3 (vx,vy) and
    // word 4's low half vz. m.t is untouched. Returns `m` (the caller here ignores it).
    // PARTIAL: the final store is a full `sw` at m+0x10, so on PSX it also overwrites the 2 pad
    // bytes at m+0x12 with the high half of the m[8] result. MATRIX above has no pad field, so that
    // side effect has no representation here; no reader of those two bytes is known.
    public static MATRIX ScaleMatrix(MATRIX param_1, VECTOR param_2)
    {
        int iVar2;
        int iVar3;
        int iVar4;

        iVar2 = param_2.vx;
        iVar3 = param_2.vy;
        iVar4 = param_2.vz;
        // word 0 at m+0x00
        param_1.m[0] = (short)(param_1.m[0] * iVar2 >> 0xc);
        param_1.m[1] = (short)(param_1.m[1] * iVar3 >> 0xc);
        // word 1 at m+0x04
        param_1.m[2] = (short)(param_1.m[2] * iVar4 >> 0xc);
        param_1.m[3] = (short)(param_1.m[3] * iVar2 >> 0xc);
        // word 2 at m+0x08
        param_1.m[4] = (short)(param_1.m[4] * iVar3 >> 0xc);
        param_1.m[5] = (short)(param_1.m[5] * iVar4 >> 0xc);
        // word 3 at m+0x0c
        param_1.m[6] = (short)(param_1.m[6] * iVar2 >> 0xc);
        param_1.m[7] = (short)(param_1.m[7] * iVar3 >> 0xc);
        // word 4 at m+0x10 (low half only; see the PARTIAL note above)
        param_1.m[8] = (short)(param_1.m[8] * iVar4 >> 0xc);
        return param_1;
    }

    // === Additions for the static high-effect particle render layer (0x800c2eac-0x800c608c) ===

    // GHIDRA: gte_avsz3 @ 0x200001f8 (gte_AverageZ3) — the OTZ register: OTZ = Sat0..FFFF(
    // (ZSF3 * (SZ1 + SZ2 + SZ3)) >> 12). Written by FUN_800c3324 / FUN_800c42a4 immediately after a
    // gte_rtpt, and read straight back by gte_stotz as the ordering-table bucket index.
    // JUSTIFICATION: PSX hardware adaptation only — the ZSF3 control register (COP2 $29) is not
    // modelled by this port and the game never loads it: there is no gte_SetZScaleFactor3 pseudo-op
    // in SLUS_006.62's 0x20000000-0x20000260 macro table, so ZSF3 keeps InitGeom's default 0x155.
    // 0x155 * 3 / 0x1000 = 0.2498, i.e. OTZ is the average SZ divided by four — which is EXACTLY the
    // "OTZ as SZ >> 2" convention this file already uses for RotTransPers/StOpz, so the two agree.
    private static int gteOTZ;

    public static void Avsz3()
    {
        gteMAC0 = 0x155 * (gteSZ[1] + gteSZ[2] + gteSZ[3]);
        int otz = gteMAC0 >> 12;
        gteOTZ = otz > 0xFFFF ? 0xFFFF : otz < 0 ? 0 : otz;
    }

    // GHIDRA: gte_stotz @ 0x20000134 — reads the OTZ register Avsz3 just produced.
    // JUSTIFICATION: C# language bridge — the destination in this port's callers is either a byte[]
    // scratch word or an ordinary local, so both a byte[] overload and a direct return are provided,
    // matching the StSxy/StSz pair above.
    public static void StOtz(byte[] buf, int byteOffset)
    {
        BitConverter.GetBytes(gteOTZ).CopyTo(buf, byteOffset);
    }

    public static int StOtz() => gteOTZ;

    // GHIDRA: gte_stsxy3 @ 0x200000d4 — stores SXY0, SXY1, SXY2 as 3 packed 32-bit words into THREE
    // SEPARATE destinations (unlike stsxy3c above, which writes them contiguously). FUN_800c3324 and
    // FUN_800c42a4 use it to fill the 1st, 2nd and 3rd vertex of a POLY_FT4, whose XY slots are 8
    // bytes apart, not 4.
    // JUSTIFICATION: C# language bridge — destination in this port is always a byte[] draw buffer.
    public static void StSxy3(byte[] buf, int off0, int off1, int off2)
    {
        BitConverter.GetBytes((ushort)gteSXY[0][0] | ((uint)(ushort)gteSXY[0][1] << 16)).CopyTo(buf, off0);
        BitConverter.GetBytes((ushort)gteSXY[1][0] | ((uint)(ushort)gteSXY[1][1] << 16)).CopyTo(buf, off1);
        BitConverter.GetBytes((ushort)gteSXY[2][0] | ((uint)(ushort)gteSXY[2][1] << 16)).CopyTo(buf, off2);
    }

    // GHIDRA: gte_rtv0_b — the MVMVA issued by ApplyMatrixSV @0x80078c34.
    // CERTAIN: closed via raw COP2 instruction-word decode of the real bytes at 0x80078c64 — the word
    // is 0x4A486012: funct 0x12 (MVMVA), sf=1 (bit19, shift 12), mx=0 (RT), v=0 (V0), cv=3 (NO
    // translation vector), lm=0 (signed IR saturation). So it is gte_rt above WITHOUT the TR add,
    // i.e. the same math as Rtir but sourcing V0 instead of the current IR1-3:
    //   MAC[1..3] = (RT * V0) >> 12;  IR[1..3] = Saturate16(MAC[1..3])
    public static void RtV0B()
    {
        long mac1 = (long)gteR[0] * gteV0[0] + (long)gteR[1] * gteV0[1] + (long)gteR[2] * gteV0[2];
        long mac2 = (long)gteR[3] * gteV0[0] + (long)gteR[4] * gteV0[1] + (long)gteR[5] * gteV0[2];
        long mac3 = (long)gteR[6] * gteV0[0] + (long)gteR[7] * gteV0[1] + (long)gteR[8] * gteV0[2];
        gteMAC1 = (int)(mac1 >> 12);
        gteMAC2 = (int)(mac2 >> 12);
        gteMAC3 = (int)(mac3 >> 12);
        gteIR1 = Sat16(gteMAC1);
        gteIR2 = Sat16(gteMAC2);
        gteIR3 = Sat16(gteMAC3);
    }

    // GHIDRA: ApplyMatrix @ 0x80078be4 — PSX SDK (psyq MTX_*.OBJ). The third member of the family,
    // sitting between ApplyMatrixLV and ApplyMatrixSV in the executable: SVECTOR in, VECTOR out.
    // CERTAIN (full 20-instruction body decoded from raw MIPS): the same five `lw` + `ctc2` rotation
    // load as ApplyMatrixSV below — the TRANSLATION is not loaded — then `gte_ldv0(param_2)`, the
    // 0x4A486012 MVMVA, then `swc2 $mac1/$mac2/$mac3` into param_3. Returns param_3.
    // IT STORES MAC, NOT IR, which is the one difference from ApplyMatrixSV: that one narrows
    // through the saturating IR registers into an SVECTOR, this one keeps the full 32-bit
    // accumulators. Using IR here would silently clamp every component to +-0x7FFF.
    // Decoded with a script rather than by eye — cv reads NONE and getting that field wrong by hand
    // is how an earlier pass nearly added a bogus helper (see FUN_800cf844 in EffectMeshRender.cs).
    // ZERO static callers in SLUS_006.62, which is why Ghidra never named it; it is reached only
    // from the PE.IMG scene overlays, where 2 families over 13 scenes call it.
    // SIDE EFFECT, same as its sibling: this CLOBBERS the GTE rotation-matrix registers.
    public static VECTOR ApplyMatrix(MATRIX param_1, SVECTOR param_2, VECTOR param_3)
    {
        SetRotMatrix(param_1);
        LdV0(param_2);
        RtV0B();
        param_3.vx = gteMAC1;
        param_3.vy = gteMAC2;
        param_3.vz = gteMAC3;
        return param_3;
    }

    // GHIDRA: ApplyMatrixSV @ 0x80078c34 — PSX SDK (psyq MTX_*.OBJ), 18 call sites in SLUS_006.62.
    // CERTAIN (full 92-byte body decoded from raw MIPS): five `lw` + `ctc2 $r11r12/$r13r21/$r22r23/
    // $r31r32/$r33` (i.e. gte_SetRotMatrix on param_1 — the TRANSLATION is NOT loaded), then
    // `gte_ldv0(param_2)`, the 0x4A486012 MVMVA decoded on RtV0B above, then `mfc2`+`sh` of IR1/IR2/
    // IR3 into param_3->vx/vy/vz. Returns param_3.
    // SIDE EFFECT WORTH KEEPING: this CLOBBERS the GTE's rotation-matrix register set. RenderParticleQuad
    // calls it four times in a row after it has finished with the projection matrix, which is why that
    // is harmless there; any future caller must re-issue SetRotMatrix afterwards.
    public static SVECTOR ApplyMatrixSV(MATRIX param_1, SVECTOR param_2, SVECTOR param_3)
    {
        SetRotMatrix(param_1);
        LdV0(param_2);
        RtV0B();
        param_3.vx = gteIR1;
        param_3.vy = gteIR2;
        param_3.vz = gteIR3;
        return param_3;
    }

    // GHIDRA: RotMatrixZ @ 0x80079e14 — PSX SDK (psyq MTX_*.OBJ).
    // CERTAIN (full 100-byte decompilation reviewed): right-multiplies `matrix` by a rotation of
    // `param_1` about Z, IN PLACE, and returns it. Rows 0 and 1 are recombined against (cos, -sin) and
    // (sin, cos) with a Q12 shift; row 2 (the Z axis itself) is untouched — the exact structural
    // counterpart of RotMatrixY above, which leaves row 1 alone.
    // The original reads both terms out of one packed word of the same ROM table RotMatrixY uses,
    // DAT_800966ec + (angle & 0xfff) * 4 — SIN in the low half (`(short)`), COS in the high half
    // (`>> 0x10`); note that is the opposite assignment to RotMatrixY, which negates the low half.
    // JUSTIFICATION: PSX hardware adaptation — this port supplies that table through the rsin/rcos
    // adapters above, same 12-bit angle / Q12 result units. Quantisation can differ by at most one
    // unit on some inputs, exactly as recorded on rsin.
    // CLOSED 2026-07-31 — the negative-angle branch is EXACTLY equivalent to the `& 0xfff` used here.
    // This note used to be a PARTIAL reading "a NEGATIVE angle takes a different path in the original
    // (FGO_06_OBJ_64, an SDK internal that is not decompiled) ... NOT proven to be what that branch
    // does". Both halves of that were wrong.
    // FGO_06_OBJ_64 @0x80079e78 is not an SDK internal and is not a function: it is one of Ghidra's
    // fake `*_OBJ_<hex>` symbols (offset 0x64 into the object), i.e. a LABEL inside RotMatrixZ that
    // Ghidra promoted, which is why RotMatrixZ's own endAddress is 0x80079e77. It is the COMMON TAIL
    // that BOTH branches converge on — the matrix recombination — and the sign branch is the 13
    // instructions at 0x80079e20-0x80079e54, inside these same 100 bytes.
    // Raw MIPS, decoded from the bytes at 0x80079e14 (not from the decompiler):
    //   0x80079e1c  bgez t7,0x80079e58        ; angle >= 0 -> positive arm
    //   0x80079e20  andi t9,t7,0xfff          ; delay slot, runs on BOTH arms
    //   negative arm: subu t7,zero,t7 / andi t7,t7,0xfff / sll t8,t7,2 / lw t9,0x66ec(...)
    //                 sll t6,t9,0x10 / sra t6,t6,0x10    ; t6 = (short)word          = sin
    //                 subu t1,zero,t6                    ; t1 = -sin   <- NEGATED
    //                 j 0x80079e78 / sra t0,t9,0x10      ; t0 = word >> 16           = cos
    //   positive arm: sll t8,t9,2 / lw t9,0x66ec(...) / sra t1,...,0x10 / sra t0,t9,0x10
    //                 t1 = +(short)word = sin, t0 = cos   (NOT negated)
    // So with m = angle & 0xfff, the negative arm indexes the table at (-angle) & 0xfff, which is
    // 4096 - m (and 0 when m is 0), and negates the sine. The ROM table at DAT_800966ec was then read
    // directly and is exactly odd/even symmetric — sin[4096-m] == -sin[m] and cos[4096-m] == cos[m],
    // measured on the seven pairs straddling the wrap boundary where a quantisation asymmetry would
    // have to appear (m=1..7 give sin 6,13,19,25,31,38,44 against -6,-13,-19,-25,-31,-38,-44 at
    // 4095..4089; cos is 0x1000 in both windows).
    // Therefore -sin[4096-m] == +sin[m] and cos[4096-m] == cos[m]: the negative arm computes exactly
    // what the positive arm computes for m. Masking to 12 bits is not an approximation of that
    // branch, it IS that branch. The same holds for RotMatrixY above, decoded the same way (its two
    // arms carry the opposite negation, and cancel identically).
    // This matters because the PARTIAL was load-bearing: Render3DRotatedPointWithOutline @0x800d27fc
    // (call at 0x800d28c0) takes `angle` from 0x800d59d8 and 0x800dac80, both `lh` SIGNED loads of a
    // truncated rand(), so negative roughly half the time — and one site accumulates +0x10 per tick
    // and overflows 0x7FFF on its own. All of those are now covered.
    // RotMatrixZ's other caller, RenderParticleQuad @0x800c3b04 (call at 0x800c3ee0), reaches its
    // rotated branch only when the descriptor's +0x0C rotationAngle is non-zero, and that field has
    // exactly one writer per variant — an `sh $zero` in the matching HighEffectN_Load (0x800cd870,
    // 0x800cd858, 0x800cd828, 0x800ce0ec) — so that branch stays dead in this build either way.
    public static MATRIX RotMatrixZ(uint param_1, MATRIX matrix)
    {
        int iVar2;
        int iVar3;
        int iVar4;
        int iVar5;
        int iVar6;
        int iVar7;
        int iVar8;
        int iVar9;

        iVar3 = rsin((int)(param_1 & 0xfff));
        iVar2 = rcos((int)(param_1 & 0xfff));
        iVar4 = matrix.m[0];
        iVar7 = matrix.m[3];
        iVar5 = matrix.m[1];
        iVar8 = matrix.m[4];
        iVar6 = matrix.m[2];
        iVar9 = matrix.m[5];
        matrix.m[0] = (short)(iVar2 * iVar4 - iVar3 * iVar7 >> 0xc);
        matrix.m[1] = (short)(iVar2 * iVar5 - iVar3 * iVar8 >> 0xc);
        matrix.m[2] = (short)(iVar2 * iVar6 - iVar3 * iVar9 >> 0xc);
        matrix.m[3] = (short)(iVar3 * iVar4 + iVar2 * iVar7 >> 0xc);
        matrix.m[4] = (short)(iVar3 * iVar5 + iVar2 * iVar8 >> 0xc);
        matrix.m[5] = (short)(iVar3 * iVar6 + iVar2 * iVar9 >> 0xc);
        return matrix;
    }

    // GHIDRA: RotMatrixYXZ @ 0x80079754
    // CERTAIN (structure/branches + trig table, see TmdSystem.GetTrigSinQ12/GetTrigCosQ12 for the
    // shared ROM-table evidence): custom fixed-point YXZ-order Euler rotation matrix builder — same
    // DAT_800966ec sin/cos table as RotMatrix (TmdSystem.cs), different matrix-cell wiring/order.
    // Unlike RotMatrix (only ever called with angle (0,0,0) in its ported call sites), this function
    // IS reached with real, varying bone rotation angles from animation data (LoadAnimStandardFormat/
    // LoadAnimCompressedFormat below) — hence the trig table was made fully general (real Math.Sin/
    // Cos) rather than left as an angle-0-only stub.
    // THE THREE "NEGATIVE COMPONENT" EARLY RETURNS WERE A DEFECT OF THIS PORT, AND THEY ARE GONE
    // (fixed 2026-08-07). The old note called them BLOCKED — "ported as literal early-returns of the
    // unmodified matrix, not independently closed via disassembly, expected to be unreachable for
    // well-formed animation data". Both halves of that were wrong, and reading the real MIPS at
    // 0x80079754 settles it in six instructions. For each of vx, vy and vz the original does:
    //     lh   $t7, N($a0)
    //     bgez $t7, <positive path>      ; angle >= 0
    //     andi $t9, $t7, 0x0fff          ; delay slot: mask, for the positive path
    //     subu $t7, $zero, $t7           ; NEGATE the angle
    //     bgez $t7, <join>
    //     andi $t7, $t7, 0x0fff          ; mask the negated angle
    //     ... table lookup ...
    //     subu $t3, $zero, $t6           ; NEGATE THE SINE (the cosine is kept as-is)
    // That is `sin(-x) = -sin(x)`, `cos(-x) = cos(x)` — it BUILDS THE MATRIX, it does not abandon it.
    // The three branch sites are 0x8007975C (vx), 0x800797C4 (vy) and 0x80079840 (vz), and the vz one
    // stores m[8] on both paths before continuing, which is why that store survives below.
    // WHY THE FIX IS ONLY A DELETION. GetTrigSinQ12/GetTrigCosQ12 already mask with `& 0xfff`, and
    // for a negative angle C#'s two's-complement mask lands on the same point of the circle that the
    // original reaches by negate-then-mask-then-negate-the-sine: negating before or after the mask
    // differs by a multiple of 4096 units, and sine is odd while cosine is even. So removing the
    // three returns is the whole correction; no sign handling has to be added.
    // IT WAS NOT UNREACHABLE. Family A40 (`37125a94`, scene 89) reaches this function with a NEGATIVE
    // vy on every phase-2 render, in two separate handlers, and its differential campaign is what
    // exposed the gap: the real MIPS returns m = [-4096,0,0, 0,4096,0, 0,0,-4096] for vy = -2048
    // where this port returned the caller's matrix untouched.
    // MOVED 2026-07-31 from Remaster/AnimationSystem.cs — rule 13: this is a psyq LIBGTE routine,
    // not game runtime.
    public static MATRIX RotMatrixYXZ(SVECTOR vector, MATRIX matrix)
    {
        short sVar9 = (short)GetTrigSinQ12(vector.vx);
        int iVar3 = GetTrigCosQ12(vector.vx);
        int iVar6 = (short)GetTrigSinQ12(vector.vy);
        int iVar4 = GetTrigCosQ12(vector.vy);
        short sVar1 = vector.vz;
        matrix.m[1 * 3 + 2] = (short)(-sVar9);
        matrix.m[0 * 3 + 2] = (short)((iVar6 * iVar3) >> 0xc);
        short sVar8 = (short)((iVar4 * iVar3) >> 0xc);
        matrix.m[2 * 3 + 2] = sVar8;
        int iVar7 = (short)GetTrigSinQ12(sVar1);
        int iVar5 = GetTrigCosQ12(sVar1);
        matrix.m[1 * 3 + 0] = (short)((iVar7 * iVar3) >> 0xc);
        matrix.m[1 * 3 + 1] = (short)((iVar5 * iVar3) >> 0xc);
        int iVar3b = (iVar6 * sVar9) >> 0xc;
        matrix.m[0 * 3 + 0] = (short)((short)((iVar4 * iVar5) >> 0xc) + (short)((iVar3b * iVar7) >> 0xc));
        matrix.m[0 * 3 + 1] = (short)((short)((iVar3b * iVar5) >> 0xc) - (short)((iVar4 * iVar7) >> 0xc));
        int iVar3c = (iVar4 * sVar9) >> 0xc;
        matrix.m[2 * 3 + 1] = (short)((short)((iVar6 * iVar7) >> 0xc) + (short)((iVar3c * iVar5) >> 0xc));
        matrix.m[2 * 3 + 0] = (short)((short)((iVar3c * iVar7) >> 0xc) - (short)((iVar6 * iVar5) >> 0xc));
        return matrix;
    }

    // GHIDRA: RotTrans @ 0x800792d4 — same routine as the SVECTOR/VECTOR overload above.
    // JUSTIFICATION: C# language bridge only — FUN_800c42a4 passes `matrix->t` (a MATRIX's own int[3]
    // translation column) as the destination rather than a standalone VECTOR object, which this
    // port's MATRIX cannot expose as a VECTOR.
    public static void RotTrans(SVECTOR param_1, int[] param_2, int[] param_3)
    {
        LdV0(param_1);
        Rt();
        StLvnl(param_2);
        param_3[0] = 0;
    }

    // ===== Additions for the shared EffectHandler_* render layer (Remaster/EffectMeshRender.cs) =====

    // GHIDRA: gte_ldv0 — SVECTOR-shaped load expressed as three loose shorts.
    // JUSTIFICATION: C# language bridge only — CompMatrix/MulRotMatrix below build V0 from three
    // NON-CONTIGUOUS shorts of a MATRIX (one matrix COLUMN, i.e. m[c], m[3+c], m[6+c]); the original
    // does it with two packed `mtc2 $vxy0/$vz0` words assembled by hand, which C# cannot express as a
    // byte[] slice without inventing a buffer.
    public static void LdV0(short vx, short vy, short vz)
    {
        gteV0[0] = vx;
        gteV0[1] = vy;
        gteV0[2] = vz;
    }

    // GHIDRA: gte_stIR1 / gte_stIR2 / gte_stIR3 — `mfc2 rN,$ir1|$ir2|$ir3`, observed at
    // CompMatrix 0x80078838-0x80078840 and MulRotMatrix 0x80078794-0x80078798.
    // JUSTIFICATION: C# language bridge only — the originals keep these in registers and pack them
    // into 32-bit words by hand; there is no memory destination to model.
    public static short StIr1() => gteIR1;
    public static short StIr2() => gteIR2;
    public static short StIr3() => gteIR3;

    // GHIDRA: gte_stmac1 / gte_stmac2 / gte_stmac3 — `mfc2 rN,$mac1|$mac2|$mac3` at CompMatrix
    // 0x800788f8-0x80078900. Same JUSTIFICATION as StIr1-3 above.
    public static int StMac1() => gteMAC1;
    public static int StMac2() => gteMAC2;
    public static int StMac3() => gteMAC3;

    // GHIDRA: gte_stszotz @ 0x20000130
    // CERTAIN (raw MIPS at Render3DBillboardSprite 0x800cf2b0-0x800cf2bc): `mfc2 $t4,$sz3` /
    // `sra $t4,0x02` / `sw` — the pseudo-op is SZ3 >> 2, NOT the OTZ register, and it matches this
    // file's existing "OTZ as SZ >> 2" convention exactly (see StOpz(byte[],int) and Avsz3).
    // JUSTIFICATION: C# language bridge only — the callers immediately reload the stored word and
    // subtract a bias from it, so the value is returned directly instead of round-tripping through a
    // scratchpad word, the same convention as StSxy()/StSz()/StOpz().
    public static int StSzOtz() => gteSZ[3] >> 2;

    // GHIDRA: CompMatrix @ 0x800787d4
    // CERTAIN (full raw MIPS 0x800787d4-0x80078930 decoded instruction by instruction; the decompiler
    // output is NOT usable here — it drops the `swc2 $ir3,0x10` store of m[8] entirely and renders the
    // final MAC1-3 readback as a bogus `read_mt(...)` call).
    // Composes m0 * m1 into `outM`: loads m0's rotation into RT, pushes each COLUMN of m1 through
    // gte_rtv0_b (MVMVA mx=RT v=V0 cv=NONE sf=1), and reassembles the three saturated IR triples into
    // outM's rows. Then it rotates m1's TRANSLATION (truncated to 16 bits by the VXY0/VZ0 registers)
    // through the same RT and adds m0's translation, reading the UNSATURATED MAC1-3 for that step,
    // not IR1-3.
    // The packing is transposed relative to the loop: column c's IR1 lands in outM.m[c], its IR2 in
    // outM.m[3+c], its IR3 in outM.m[6+c] — which is what the three `sw` word-pair assemblies at
    // 0x80078894/0x800788a4/0x800788e4/0x800788f4 plus the `swc2 $ir3,0x10` spell out.
    public static MATRIX CompMatrix(MATRIX m0, MATRIX m1, MATRIX outM)
    {
        SetRotMatrix(m0);

        LdV0(m1.m[0], m1.m[3], m1.m[6]);
        RtV0B();
        short c0Ir1 = StIr1(), c0Ir2 = StIr2(), c0Ir3 = StIr3();

        LdV0(m1.m[1], m1.m[4], m1.m[7]);
        RtV0B();
        short c1Ir1 = StIr1(), c1Ir2 = StIr2(), c1Ir3 = StIr3();

        LdV0(m1.m[2], m1.m[5], m1.m[8]);
        RtV0B();
        short c2Ir1 = StIr1(), c2Ir2 = StIr2(), c2Ir3 = StIr3();

        // PSX: the translation is loaded through VXY0/VZ0, which are 16-bit registers — the `lhu`/
        // `lw`+`sll 16` at 0x800788b4-0x800788bc keep only the low half of each 32-bit t[] word.
        LdV0((short)m1.t[0], (short)m1.t[1], (short)m1.t[2]);
        RtV0B();

        outM.m[0] = c0Ir1; outM.m[1] = c1Ir1; outM.m[2] = c2Ir1;
        outM.m[3] = c0Ir2; outM.m[4] = c1Ir2; outM.m[5] = c2Ir2;
        outM.m[6] = c0Ir3; outM.m[7] = c1Ir3; outM.m[8] = c2Ir3;
        outM.t[0] = StMac1() + m0.t[0];
        outM.t[1] = StMac2() + m0.t[1];
        outM.t[2] = StMac3() + m0.t[2];
        return outM;
    }

    // GHIDRA: MulRotMatrix @ 0x800786e4
    // CERTAIN (same treatment as CompMatrix: raw MIPS, tail re-read at 0x8007878c-0x800787c8 because
    // the decompiler again drops the `swc2 $ir3,0x10(a0)` that writes m[8]).
    // The rotation-only half of CompMatrix, IN PLACE: multiplies the CURRENTLY loaded RT by `m`'s own
    // 3x3 and writes the product back over `m`'s 3x3. It does NOT load RT itself — the caller must
    // have done so — and it leaves `m.t` untouched.
    public static MATRIX MulRotMatrix(MATRIX m)
    {
        LdV0(m.m[0], m.m[3], m.m[6]);
        RtV0B();
        short c0Ir1 = StIr1(), c0Ir2 = StIr2(), c0Ir3 = StIr3();

        LdV0(m.m[1], m.m[4], m.m[7]);
        RtV0B();
        short c1Ir1 = StIr1(), c1Ir2 = StIr2(), c1Ir3 = StIr3();

        LdV0(m.m[2], m.m[5], m.m[8]);
        RtV0B();
        short c2Ir1 = StIr1(), c2Ir2 = StIr2(), c2Ir3 = StIr3();

        m.m[0] = c0Ir1; m.m[1] = c1Ir1; m.m[2] = c2Ir1;
        m.m[3] = c0Ir2; m.m[4] = c1Ir2; m.m[5] = c2Ir2;
        m.m[6] = c0Ir3; m.m[7] = c1Ir3; m.m[8] = c2Ir3;
        return m;
    }

    // GHIDRA: MulMatrix0 @ 0x800785d4 — PSX SDK (psyq MTX_*.OBJ), batch T04h. 67 instructions,
    // read as raw MIPS: nothing in the executable calls it, only the PE.IMG scene overlays do, so
    // Ghidra never named it.
    // The third member of the CompMatrix family, and exactly the intersection of its two siblings
    // above: it loads RT from m0 the way CompMatrix does (MulRotMatrix does not load RT at all),
    // pushes each COLUMN of m1 through the same MVMVA, and writes the saturated IR triples into a
    // THIRD matrix (MulRotMatrix writes back over its input). It does NOT do CompMatrix's fourth
    // pass over the translation, so `m2.t` is left exactly as the caller had it — the five stores
    // at 0x80078694/0x800786A4/0x800786BC/0x800786CC and the `swc2 $ir3,0x10` cover m[0..8] and
    // nothing else.
    // The packing is the same transposed assembly as CompMatrix: column c's IR1 lands in m[c], its
    // IR2 in m[3+c], its IR3 in m[6+c].
    // `swc2 $ir3,0x10($a2)` IS A 32-BIT STORE, so on hardware it also overwrites the two pad bytes
    // at +0x12 with the sign extension of m[8]. This port's MATRIX has no field there and nothing
    // reads it — the same situation CompMatrix and MulRotMatrix are already in. Measured, not
    // assumed: the differential test asserts that byte pair against the original's actual output.
    // Every COP2 word was decoded by script, not by eye: the five `ctc2 $c0..$c4`, the `mtc2`
    // pairs feeding VXY0/VZ0, and 0x4A486012 as MVMVA mx=RT v=V0 cv=NONE lm=0 sf=1.
    // SIDE EFFECT, like its siblings: this CLOBBERS the GTE rotation-matrix registers.
    public static MATRIX MulMatrix0(MATRIX m0, MATRIX m1, MATRIX m2)
    {
        SetRotMatrix(m0);

        LdV0(m1.m[0], m1.m[3], m1.m[6]);
        RtV0B();
        short c0Ir1 = StIr1(), c0Ir2 = StIr2(), c0Ir3 = StIr3();

        LdV0(m1.m[1], m1.m[4], m1.m[7]);
        RtV0B();
        short c1Ir1 = StIr1(), c1Ir2 = StIr2(), c1Ir3 = StIr3();

        LdV0(m1.m[2], m1.m[5], m1.m[8]);
        RtV0B();
        short c2Ir1 = StIr1(), c2Ir2 = StIr2(), c2Ir3 = StIr3();

        m2.m[0] = c0Ir1; m2.m[1] = c1Ir1; m2.m[2] = c2Ir1;
        m2.m[3] = c0Ir2; m2.m[4] = c1Ir2; m2.m[5] = c2Ir2;
        m2.m[6] = c0Ir3; m2.m[7] = c1Ir3; m2.m[8] = c2Ir3;
        return m2;
    }

    // GHIDRA: LoadAverageCol @ 0x80078554
    // CERTAIN on the arithmetic, PARTIAL on the register plumbing: the original issues
    // gte_ldIR0(p0) + gte_ldsv_(c0) + gte_gpf0_b(0), reads the result, then gte_ldIR0(p1) +
    // gte_ldsv_(c1) + gte_gpl0_b(0) and reads it again — the standard GTE GPF/GPL pair, i.e.
    //   out = ((c0 * p0) >> 12) + ((c1 * p1) >> 12)
    // with the usual 0..255 colour saturation. Ghidra renders the two readbacks as a bogus
    // `read_mt(...)` plus three literal zero stores, which is why the formula is taken from the
    // documented GPF0/GPL0 semantics rather than from the decompiled body.
    // Its only caller in this layer is InterpolateColorGradient @0x800cf3ac, which always passes
    // p0 + p1 == 0x1000, so this is a straight linear blend between two keyframe colours.
    // JUSTIFICATION: C# language bridge only for the (byte[],offset) parameter shape — the original
    // takes three raw byte* (two 3-byte colour sources and a 3-byte destination).
    public static void LoadAverageCol(byte[] c0, int c0Off, byte[] c1, int c1Off,
        int p0, int p1, byte[] outCol, int outOff)
    {
        outCol[outOff] = Sat8((c0[c0Off] * p0 >> 12) + (c1[c1Off] * p1 >> 12));
        outCol[outOff + 1] = Sat8((c0[c0Off + 1] * p0 >> 12) + (c1[c1Off + 1] * p1 >> 12));
        outCol[outOff + 2] = Sat8((c0[c0Off + 2] * p0 >> 12) + (c1[c1Off + 2] * p1 >> 12));
    }

    // GHIDRA: LoadAverageShort12 @ 0x800783E4
    // MOVED 2026-07-31 from Remaster/EffectHandlers/EffectHandlersD.cs — rule 13: psyq routine, not
    // game runtime. It was written there only because PsxSdkMonogame/ was off-limits to that batch.
    // CERTAIN on the arithmetic: the raw MIPS is gte_ldIR0(p0) + gte_ldsv_(v0) + GPF12, then
    // gte_ldIR0(p1) + gte_ldsv_(v1) + GPL12, then gte_stIR1/2/3 packed back as two words —
    //   out_i = sat16( ((v0_i * p0) >> 12) + ((v1_i * p1) >> 12) )
    // the SVECTOR analogue of LoadAverageCol, which this port already carries with the same reading.
    // The MAC accumulation between GPF and GPL is NOT saturated; only the final IR readback is,
    // which is why the two terms are summed before Sat16 rather than after each shift.
    // The original stores IR3 as a FULL 32-BIT WORD (`mfc2 $t2,$ir3` at 0x80078458 then
    // `sw $t2,0x4($t5)`), so the destination's fourth short is that word's HIGH half — and MFC2
    // SIGN-EXTENDS IR1..IR3, which are signed 16-bit registers. The fourth short is therefore the
    // sign extension of the third, NOT a constant zero.
    // CORRECTED 2026-08-03, measured rather than reasoned: running the real 0x800783E4 through
    // ai-agent-data/mips-differential-test.py with v0 = (100, 200, -300), v1 = (10, 20, -30) and
    // p0 = p1 = 0x800 writes {55, 110, -165, -1}; the same inputs with a positive z write
    // {55, 110, 165, 0}. This file previously hard-coded 0 for both, which is right only for a
    // non-negative z. It is load-bearing wherever the destination is a particle record —
    // EffectPoolCallback_BoneTrackedFlare, and both call sites of the 0x88 overlay source.
    public static void LoadAverageShort12(short v0x, short v0y, short v0z,
        short v1x, short v1y, short v1z, int p0, int p1, short[] outv)
    {
        outv[0] = Sat16(((v0x * p0) >> 12) + ((v1x * p1) >> 12));
        outv[1] = Sat16(((v0y * p0) >> 12) + ((v1y * p1) >> 12));
        outv[2] = Sat16(((v0z * p0) >> 12) + ((v1z * p1) >> 12));
        outv[3] = (short)(outv[2] >> 15);
    }
}
