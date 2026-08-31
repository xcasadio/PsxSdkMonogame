using System;
using static PsxSdkMonogame.MipsMemory;

namespace PsxSdkMonogame;

// Sony's HIGH-LEVEL graphics library, libgs (GS / GsOT / GsSPRITE).
//
// WHICH BINARY THIS IS FROM: /SELECT.EXE. Every address in this file is a SELECT.EXE address and is
// tagged as such, because this SDK is shared across the owner's PSX ports and already cites more
// than one binary. TITLE.EXE does NOT link libgs at all — it drives libgpu directly (DrawOTag,
// PutDrawEnv, a raw 0x800-entry ordering table) — so nothing here transfers from that overlay, and
// nothing here may be read as describing it.
//
// WHERE libgs SITS: it is a layer OVER libgpu. It owns the one DRAWENV (0x8005975C) and the one
// DISPENV (0x800597B8) that SELECT.EXE's game code never names, it programs them through
// LibGpu.PutDrawEnv / LibGpu.PutDispEnv, and its sort routines write libgpu packets into a work area
// that GsSetWorkBase points at. The ordering table it fills is an ordinary libgpu ordering table:
// GsClearOt hands GsOT.org to ClearOTagR and GsDrawOt hands GsOT.tag to DrawOTag.
//
// SCOPE OF THIS FILE: the TYPES, the INITIALISATION and BUFFER-SWAP routines (GsInitGraph,
// GsInit3D, GsDefDispBuff, GsGetActiveBuff, GsSetWorkBase, GsSwapDispBuff), the libgs-internal
// routines they call (gpu_init, gte_init, valiable_init, GsSetDrawBuffClip,
// GsSetDrawBuffOffset, GS_010_OBJ_88), and the SORT routines (GsClearOt, GsSortSprite,
// GsSortLine, GsSortBoxFill, GsSortClear, GsDrawOt, _make_packet). Between them they cover
// every libgs call FUN_800344A4 @ 0x800344A4 issues, which is the only place SELECT.EXE draws.
//
// HOW THE DOUBLE BUFFER ACTUALLY WORKS HERE, because the indexing looks wrong until it is traced.
// SELECT.EXE's frame step FUN_800344a4 @ 0x800344A4 runs, in this order:
//     A = GsGetActiveBuff(); GsSetWorkBase(A * 24000 + 0x800597CC); build OT[A];
//     DrawSync; VSync; ResetGraph(1); GsSwapDispBuff(); GsSortClear; GsDrawOt(OT[A]);
// GsSwapDispBuff DISPLAYS drawbuf[A] and flips the active index to 1 - A, and GsSetDrawBuffClip then
// clips to drawbuf[1 - A] — which is the buffer GsDrawOt, two lines later, draws into. Meanwhile
// GsSetDrawBuffOffset deliberately reads the OPPOSITE index (verified instruction by instruction at
// 0x80048AAC and 0x80048AD0: `bne a1,zero` skips an `addiu v0,v0,2`, so index 0 selects entry 1),
// because the offset it publishes into DAT_80065394 / DAT_80065398 is consumed by the NEXT frame's
// sort, not by this one. Writing the frame index k: the offset in use while frame k sorts was set at
// the end of frame k-1 and equals drawX[A(k-1)] = drawX[A(k+1)], which is exactly the clip origin of
// the buffer frame k's packets get drawn into. The two agree.
public static class LibGs
{
    // ================================================================================
    // TYPES
    //
    // Each layout below is closed from the code in /SELECT.EXE that walks it, not from a header.
    // Ghidra's psyq340 archive carries the same field lists; where it does, the archive and the
    // image agree, and the image is what is cited.
    // ================================================================================

    // Ordering-table tag. FOUR BYTES.
    // STRIDE CLOSED FROM THE IMAGE: main @ 0x8003045C arms two GsOT by hand with org = 0x80065350
    // and org = 0x80065370 — 0x20 apart — and length = 3 on both, so each table holds 1 << 3 = 8
    // entries and the stride is 0x20 / 8 = 4.
    // WHAT THE FOUR BYTES ARE: GsClearOt @ 0x80048994 passes GsOT.org straight to libgpu's
    // ClearOTagR and GsDrawOt @ 0x8004884C passes GsOT.tag straight to libgpu's DrawOTag, so a tag
    // IS one libgpu ordering-table word — a 24-bit link plus an 8-bit length byte.
    // NOTE ON THE PORT'S MEMORY MODEL: a live tag array is therefore a RAM byte[] registered with
    // LibGpu.RamRegion and walked by LibGpu.ClearOTagR / LibGpu.RasterizeOrderingTable, NOT an array
    // of these objects. This declaration exists because the original's type does and because GsOT
    // points at it.
    public class GsOT_TAG
    {
        public uint p;    // +0x00, 24 bits — the link
        public byte num;  // +0x03,  8 bits — the length byte

        public const int SizeOf = 4;
    }

    // Ordering-table handle. TWENTY BYTES.
    // SIZE CLOSED TWICE FROM THE IMAGE: main @ 0x8003045C arms one at 0x800654C4 and one at
    // 0x800654D8 (0x14 = 20 apart), and the frame step FUN_800344a4 @ 0x800344A4 indexes them as
    // `&DAT_800654c4 + activeBuf * 5` on an int * — five 4-byte words.
    // FIELD LIST CLOSED FROM GsClearOt @ 0x80048994 AND GsDrawOt @ 0x8004884C:
    //   GsClearOt reads length (+0x00) and org (+0x04), writes offset (+0x08), point (+0x0C) and
    //   tag (+0x10) as tag = (char *)org - 4 + (4 << (length & 0x1f)), i.e. &org[(1 << length) - 1],
    //   then calls ClearOTagR(org, 1 << length);
    //   GsDrawOt passes tag to DrawOTag. ClearOTagR builds the chain in REVERSE, so the LAST entry
    //   is the head of the list — which is why tag points at it and not at org.
    // org / tag are GsOT_TAG * in the original. This port keeps them as the RAW PSX ADDRESSES they
    // already are, because that is what LibGpu.ClearOTagR(int, int) and LibGpu.DrawOTag(int) take,
    // and because main itself assigns them as link-time constants (DAT_800654c8 = &DAT_80065350).
    public class GsOT
    {
        public uint length;   // +0x00
        public int org;       // +0x04 — GsOT_TAG *, held as a PSX address
        public uint offset;   // +0x08
        public uint point;    // +0x0C
        public int tag;       // +0x10 — GsOT_TAG *, held as a PSX address

        public const int SizeOf = 20;
    }

    // 2D sprite. THIRTY-SIX BYTES.
    // STRIDE CLOSED TWICE FROM THE IMAGE: FUN_80030848 @ 0x80030848 (the array initialiser) advances
    // with `param_1 = param_1 + 9` on an undefined4 * — nine words — and main passes 0x80065AB0 into
    // the same initialiser for the array based at 0x800654EC, where 0x80065AB0 - 0x800654EC = 0x5C4
    // = 36 * 41, i.e. element 41 of a 36-byte stride.
    // EVERY FIELD AND OFFSET below is closed from the two routines that touch them:
    //   FUN_80030848 @ 0x80030848 WRITES +0x04, +0x06, +0x08, +0x0A, +0x0E, +0x0F, +0x14, +0x15,
    //     +0x16, +0x18, +0x1A, +0x1C, +0x1E, +0x20 and +0x00 (w = h = 0x10, r = g = b = 0x80,
    //     scalex = scaley = 0x1000, rotate = 0, attribute = 0x80000000, the rest 0). It never writes
    //     +0x0C — tpage is left alone by the initialiser.
    //   GsSortSprite @ 0x8004820C READS attribute, w, h, scalex, scaley, rotate, x, y, tpage, mx,
    //     my, u, v, cx, cy, r, g, b at exactly these offsets.
    public class GsSPRITE
    {
        public uint attribute;   // +0x00 — bit 31 disables the sprite (GsSortSprite: `if ((int)attribute < 0) return;`)
        public short x;          // +0x04
        public short y;          // +0x06
        public ushort w;         // +0x08 — 0 also disables (GsSortSprite returns early)
        public ushort h;         // +0x0A
        public ushort tpage;     // +0x0C
        public byte u;           // +0x0E
        public byte v;           // +0x0F
        public short cx;         // +0x10
        public short cy;         // +0x12
        public byte r;           // +0x14
        public byte g;           // +0x15
        public byte b;           // +0x16
        // +0x17 is padding: FUN_80030848 writes +0x14/+0x15/+0x16 and then jumps to +0x18.
        public short mx;         // +0x18
        public short my;         // +0x1A
        public short scalex;     // +0x1C
        public short scaley;     // +0x1E
        public int rotate;       // +0x20

        public const int SizeOf = 36;
    }

    // 2D line. SIXTEEN BYTES.
    // STRIDE CLOSED FROM THE IMAGE: the frame step FUN_800344a4 @ 0x800344A4 sorts four of them at
    // 0x80065484, 0x80065494, 0x800654A4 and 0x800654B4 — 0x10 apart.
    // FIELDS CLOSED FROM GsSortLine @ 0x80048C0C, which reads attribute (+0x00, `if (-1 < (int)
    // attribute)` gates the whole body), r (+0x0C), g (+0x0D), b (+0x0E), x0 (+0x04), y0 (+0x06),
    // x1 (+0x08), y1 (+0x0A). +0x0F is unread padding.
    public class GsLINE
    {
        public uint attribute;   // +0x00
        public short x0;         // +0x04
        public short y0;         // +0x06
        public short x1;         // +0x08
        public short y1;         // +0x0A
        public byte r;           // +0x0C
        public byte g;           // +0x0D
        public byte b;           // +0x0E

        public const int SizeOf = 16;
    }

    // 2D filled box. SIXTEEN BYTES.
    // FIELDS CLOSED FROM GsSortBoxFill @ 0x80048870, which reads attribute (+0x00, same sign gate as
    // GsLINE), r (+0x0C), g (+0x0D), b (+0x0E), x (+0x04), y (+0x06), w (+0x08), h (+0x0A).
    // The frame step's boxfill path walks five of them from 0x80067B68.
    public class GsBOXF
    {
        public uint attribute;   // +0x00
        public short x;          // +0x04
        public short y;          // +0x06
        public ushort w;         // +0x08
        public ushort h;         // +0x0A
        public byte r;           // +0x0C
        public byte g;           // +0x0D
        public byte b;           // +0x0E

        public const int SizeOf = 16;
    }

    // ================================================================================
    // GLOBALS
    //
    // These are libgs's own .bss/.data, not the game's. SELECT.EXE's game code names none of them.
    // ================================================================================

    // GHIDRA: DAT_8005975c @ 0x8005975C (SELECT.EXE)
    // libgs's ONE DRAWENV, 92 bytes. Field offsets verified against this port's LibGpu.DRAWENV by
    // the individual stores in gpu_init @ 0x80047AC8 and GsSetDrawBuffClip @ 0x8004807C:
    //   clip.x/.y/.w/.h at 0x8005975C/5E/60/62, ofs[0]/ofs[1] at 0x80059764/66, tw at
    //   0x80059768/6A/6C/6E, tpage at 0x80059770, dtd at 0x80059772, dfe at 0x80059773, isbg at
    //   0x80059774. 0x8005975C + 92 = 0x800597B8, which is exactly where the DISPENV starts.
    public static readonly LibGpu.DRAWENV DAT_8005975c = new LibGpu.DRAWENV();

    // GHIDRA: DAT_800597b8 @ 0x800597B8 (SELECT.EXE)
    // libgs's ONE DISPENV, 20 bytes. Offsets verified from gpu_init's stores: disp.x/.y/.w/.h at
    // 0x800597B8/BA/BC/BE, screen.x/.y/.w/.h at 0x800597C0/C2/C4/C6, isinter at 0x800597C8,
    // isrgb24 at 0x800597C9, pad0 at 0x800597CA. 0x800597B8 + 20 = 0x800597CC, which is the GPU
    // packet workspace SELECT.EXE's frame step hands to GsSetWorkBase.
    public static readonly LibGpu.DISPENV DAT_800597b8 = new LibGpu.DISPENV();

    // GHIDRA: DAT_800691c0 @ 0x800691C0 (SELECT.EXE)
    // The active buffer index, 0 or 1. A SIGNED HALFWORD: GsGetActiveBuff @ 0x800489EC is
    // `lui v0,0x8007; lh v0,-0x6E40(v0); jr ra`, and GsSwapDispBuff stores it back with `sh`.
    public static short DAT_800691c0;

    // GHIDRA: DAT_800691c8 @ 0x800691C8 (SELECT.EXE)
    // Set once, by gpu_init, to `intmode & 4` (halfword store at 0x80047C10). SELECT.EXE calls
    // GsInitGraph(0x140, 0xF0, 0, 0, 0) from FUN_80030698 @ 0x80030698, so in this overlay it is
    // ALWAYS 0 and every branch guarded by it below is dead. The branches are transliterated anyway.
    public static short DAT_800691c8;

    // GHIDRA: DAT_800691bc @ 0x800691BC (SELECT.EXE)
    // A 32-bit counter bumped once per buffer swap by GS_010_OBJ_88 and set to 1 by valiable_init.
    // PARTIAL: nothing in SELECT.EXE reads it, so what it counts for is not closed.
    public static int DAT_800691bc;

    // GHIDRA: DAT_80059430 @ 0x80059430 (SELECT.EXE)
    // The GPU packet cursor, held as a RAW PSX ADDRESS. GsSetWorkBase stores into it; GsSortLine
    // @ 0x80048C0C, GsSortSprite @ 0x8004820C and GsSortBoxFill @ 0x80048870 all open with
    // `iVar = DAT_80059430` and write their packet words through it, then store back what
    // _make_packet returns.
    public static int DAT_80059430;

    // GHIDRA: DAT_80058e78 @ 0x80058E78 (SELECT.EXE)
    // Draw-buffer origin X, one entry per buffer. GsDefDispBuff writes both entries; GsSetDrawBuffClip
    // and GsSwapDispBuff index it with the active buffer, GsSetDrawBuffOffset with the other one.
    public static readonly short[] DAT_80058e78 = new short[2];

    // GHIDRA: DAT_80058e7c @ 0x80058E7C (SELECT.EXE)
    // Draw-buffer origin Y, one entry per buffer. Same three readers as DAT_80058e78.
    public static readonly short[] DAT_80058e7c = new short[2];

    // GHIDRA: DAT_80058ea8 @ 0x80058EA8 (SELECT.EXE)
    // Display-buffer origin X pair, written by valiable_init and GsDefDispBuff.
    // PARTIAL: nothing on SELECT.EXE's observed path READS this pair — GsSwapDispBuff takes the
    // display origin from DAT_80058e78 / DAT_80058e7c instead (verified at 0x80048B30/0x80048B4C).
    public static readonly short[] DAT_80058ea8 = new short[2];

    // GHIDRA: DAT_80058eac @ 0x80058EAC (SELECT.EXE)
    // Display-buffer origin Y pair. Same PARTIAL note as DAT_80058ea8.
    public static readonly short[] DAT_80058eac = new short[2];

    // GHIDRA: DAT_80065394 @ 0x80065394 (SELECT.EXE)
    // The X offset every 2D sort adds to its coordinates: GsSortLine does `lp->x0 + DAT_80065394`,
    // GsSortSprite does `(x + DAT_80065394) - mx`, GsSortBoxFill does `bp->x + DAT_80065394`.
    // Published by GsSetDrawBuffOffset and zeroed by gte_init.
    public static short DAT_80065394;

    // GHIDRA: DAT_80065398 @ 0x80065398 (SELECT.EXE)
    // The Y half of DAT_80065394.
    public static short DAT_80065398;

    // GHIDRA: DAT_800593b0 @ 0x800593B0 (SELECT.EXE)
    // Screen-centre X. valiable_init zeroes it; GsInit3D sets it to width / 2.
    public static short DAT_800593b0;

    // GHIDRA: DAT_800593b2 @ 0x800593B2 (SELECT.EXE)
    // Screen-centre Y. valiable_init zeroes it; GsInit3D sets it to height / 2.
    public static short DAT_800593b2;

    // GHIDRA: _DAT_800653cc @ 0x800653CC (SELECT.EXE)
    // Screen width, stored as a full 32-bit word by valiable_init (`sw` at 0x80047CB4) from
    // `param_1 & 0xFFFF`. SELECT.EXE's value is 0x140.
    public static int _DAT_800653cc;

    // GHIDRA: _DAT_800653d4 @ 0x800653D4 (SELECT.EXE)
    // Screen height, same treatment. SELECT.EXE's value is 0xF0.
    public static int _DAT_800653d4;

    // GHIDRA: DAT_800653d8 @ 0x800653D8 (SELECT.EXE)
    // A 32-byte MATRIX image that valiable_init fills with the identity: shorts 0x1000 at +0x00,
    // +0x08 and +0x10, zero at +0x02/+0x04/+0x06/+0x0A/+0x0C/+0x0E, and three zero words at +0x14,
    // +0x18, +0x1C. +0x12 — the MATRIX pad — is never written by any instruction in the function.
    // GsSortSprite reads it as the rotation matrix of its unrotated matrix path, which is what
    // the identity is for: at unit scale the projection then reproduces, exactly, the rectangle
    // the fast path emits. SortSpriteValidation drives both routes and compares them.
    public static readonly byte[] DAT_800653d8 = new byte[32];

    // GHIDRA: DAT_80066300 @ 0x80066300 (SELECT.EXE)
    // valiable_init copies DAT_800653d8 here, 32 bytes, as eight lw/sw pairs. PARTIAL: no reader.
    public static readonly byte[] DAT_80066300 = new byte[32];

    // GHIDRA: DAT_80059540 @ 0x80059540 (SELECT.EXE)
    // valiable_init copies DAT_800653d8 here and then zeroes the three diagonal shorts (+0x10, +0x08,
    // +0x00), leaving all 32 bytes zero. PARTIAL: no reader.
    public static readonly byte[] DAT_80059540 = new byte[32];

    // GHIDRA: DAT_8006539c @ 0x8006539C (SELECT.EXE)
    // valiable_init copies DAT_80059540 here, 32 bytes. PARTIAL: no reader.
    public static readonly byte[] DAT_8006539c = new byte[32];

    // GHIDRA: DAT_80066308 @ 0x80066308 (SELECT.EXE)
    // valiable_init stores ((height << 14) / width) / 3 as a halfword. For 320x240 that is 0x1000.
    // PARTIAL: nothing in SELECT.EXE reads it.
    public static short DAT_80066308;

    // GHIDRA: DAT_80066328 @ 0x80066328 (SELECT.EXE)
    // Clip-rect X bias. GsSetDrawBuffClip reads it SIGNED (`lh` at 0x80048090) and adds it to the
    // buffer origin. valiable_init zeroes it and nothing else writes it.
    public static short DAT_80066328;

    // GHIDRA: DAT_8006632a @ 0x8006632A (SELECT.EXE)
    // Clip-rect Y bias, the pair of DAT_80066328.
    public static short DAT_8006632a;

    // GHIDRA: DAT_8006632c @ 0x8006632C (SELECT.EXE)
    // Clip-rect width. valiable_init sets it from the screen width; GsSetDrawBuffClip reads it
    // UNSIGNED (`lhu` at 0x80048088) into DRAWENV.clip.w.
    public static ushort DAT_8006632c;

    // GHIDRA: DAT_8006632e @ 0x8006632E (SELECT.EXE)
    // Clip-rect height, the pair of DAT_8006632c.
    public static ushort DAT_8006632e;

    // GHIDRA: DAT_80068930 @ 0x80068930 (SELECT.EXE)
    // GsInit3D sets it to 0. PARTIAL: no reader in SELECT.EXE.
    public static int DAT_80068930;

    // GHIDRA: DAT_80068934 @ 0x80068934 (SELECT.EXE)
    // GsInit3D sets it to 10. PARTIAL: no reader in SELECT.EXE.
    public static int DAT_80068934;

    // GHIDRA: DAT_800662fc @ 0x800662FC (SELECT.EXE)
    // GsInit3D sets it to 0x3FFF. PARTIAL: no reader in SELECT.EXE.
    public static int DAT_800662fc;

    // GHIDRA: DAT_80058eb0 @ 0x80058EB0 (SELECT.EXE)
    // Thirty-two records of 0x28 bytes, ending at 0x800593B0 — which is exactly where DAT_800593b0
    // begins, closing the extent at 32 * 0x28 = 0x500 bytes. GS_010_OBJ_88 zeroes the HALFWORD at
    // +0x00 of each record, walking down from byte offset 0x4D8 in steps of 0x28
    // (`ori v0,zero,0x4D8` / `sh zero,0(at)` / `addiu v0,v0,-0x28` / `bgez` at 0x80048BDC-0x80048BF4).
    // PARTIAL: only that one halfword per record is touched anywhere in scope, so what the records
    // are is not closed. Modelled as the raw byte span so the stores stay literal.
    public static readonly byte[] DAT_80058eb0 = new byte[0x28 * 32];

    // GHIDRA: DAT_80058c90 @ 0x80058C90 (SELECT.EXE)
    // The first of two 16-byte block-fill packets valiable_init arms through SetBlockFill.
    // Registered with LibGpu.RamRegion because a packet with no PSX address cannot be linked into an
    // ordering table: the table stores 24-bit addresses and LibGpu.RasterizeOrderingTable walks them.
    public static readonly byte[] DAT_80058c90 = LibGpu.RamRegion(unchecked((int)0x80058C90), 16);

    // GHIDRA: DAT_80058ca0 @ 0x80058CA0 (SELECT.EXE)
    // The second block-fill packet. Adjacent to DAT_80058c90 in the original (0x10 apart), kept as a
    // separate region here because the original passes each one's address separately.
    public static readonly byte[] DAT_80058ca0 = LibGpu.RamRegion(unchecked((int)0x80058CA0), 16);

    // GHIDRA: DAT_800597cc @ 0x800597CC (SELECT.EXE)
    // THE GPU PACKET WORKSPACE. Every packet the four sort routines below build lives here, and
    // nowhere else: each of them opens with `iVar = DAT_80059430`, writes its words through that
    // cursor, and hands the cursor to _make_packet, which links the packet into an ordering-table
    // bucket and returns the cursor advanced past it. GsSetWorkBase is what arms the cursor.
    //
    // ADDRESS AND SIZE, both closed from the image, neither assumed:
    //   * the address is a link-time constant embedded in SELECT.EXE's frame step FUN_800344a4
    //     @ 0x800344A4, which calls GsSetWorkBase(GsGetActiveBuff() * 24000 + 0x800597CC) — Ghidra
    //     renders the constant as the negative displacement -0x7ffa6834;
    //   * so there are TWO 24000-byte areas, one per display buffer: 0x800597CC..0x8005F58B and
    //     0x8005F58C..0x8006534B, 48000 bytes end to end;
    //   * both ends are closed by neighbours. 0x800597B8 + 20 = 0x800597CC is the byte after the
    //     DISPENV above, and 0x800597CC + 48000 = 0x8006534C is four bytes below 0x80065350, the
    //     first of the two ordering-table tag arrays main @ 0x8003045C arms by hand. No named
    //     global in SELECT.EXE falls inside the span.
    //
    // WHY IT IS A LibGpu.RamRegion AND NOT A BARE byte[]: this declaration is the ONLY place the
    // work area is registered, and the registration is what makes the ordering table work at all.
    // _make_packet stores the packet's PSX address into the bucket's low 24 bits and
    // LibGpu.RasterizeOrderingTable turns those 24 bits back into (array, offset) through
    // LibGpu.RamResolveLink; a packet living in an unregistered array resolves to nothing and the
    // walk silently drops it and everything chained behind it. The same registry is what lets the
    // sort routines below turn the raw cursor DAT_80059430 into a writable buffer at all.
    //
    // PARTIAL: whether the symbol belongs to SELECT.EXE's own .bss or to libgs is NOT ESTABLISHED —
    // Ghidra has no symbol here and only the frame step names the address. It is filed under libgs
    // because libgs is what reads it. The address itself is fixed either way.
    public static readonly byte[] DAT_800597cc = LibGpu.RamRegion(unchecked((int)0x800597CC), 48000);

    // ================================================================================
    // ROUTINES
    // ================================================================================

    // GHIDRA: GetVideoMode @ 0x8004D2A0 (SELECT.EXE)
    // The whole body is two instructions, `lui v0,0x8005; lw v0,0x5964(v0)` — it returns the 32-bit
    // word DAT_80055964 @ 0x80055964. Ghidra's plate reads "Possible VMODE.OBJ/GetVideoMode".
    // DAT_80055964 has exactly ONE cross-reference in the whole of SELECT.EXE (this read), and its
    // image value in .data is 0x00000000, so in this overlay the routine is a constant 0 = NTSC and
    // gpu_init's PAL branch below is dead code. LibEtc.GetVideoMode() returns the same 0.
    // It sits at 0x8004D2A0, inside libetc, not libgs; it lives here only because LibEtc.cs is owned
    // by a later phase and gpu_init needs it now.
    private static int GetVideoMode()
    {
        return (int)LibEtc.GetVideoMode();
    }

    // GHIDRA: SetBlockFill @ 0x8004BDF0 (SELECT.EXE)
    // Twenty bytes: `*(u8 *)(p + 3) = 3; *(u8 *)(p + 7) = 2;`. In libgpu packet terms that is the
    // tag's length byte set to 3 words and the primitive code byte set to 0x02 — the GPU's
    // "fill rectangle in VRAM" command.
    // It sits at 0x8004BDF0, inside libgpu, not libgs; it lives here only because LibGpu.cs is owned
    // by a later phase and valiable_init needs it now. It belongs in LibGpu.cs.
    private static void SetBlockFill(byte[] param_1)
    {
        param_1[3] = 3;
        param_1[7] = 2;
    }

    // GHIDRA: gpu_init @ 0x80047AC8 (SELECT.EXE)
    // Builds libgs's DRAWENV and DISPENV from scratch and pushes both to the GPU. Every store below
    // is one `sh`/`sb` in the original, in this order.
    private static void gpu_init(ushort param_1, ushort param_2, ushort param_3, ushort param_4, ushort param_5)
    {
        LibGpu.ResetGraph(0);
        DAT_8005975c.ofs[1] = 0;
        DAT_8005975c.ofs[0] = 0;
        DAT_8005975c.tw.h = 0;
        DAT_8005975c.tw.w = 0;
        DAT_8005975c.tw.y = 0;
        DAT_8005975c.tw.x = 0;
        DAT_8005975c.tpage = 0;
        DAT_8005975c.dtd = (byte)param_4;
        DAT_8005975c.dfe = 0;
        DAT_8005975c.isbg = 0;
        LibGpu.PutDrawEnv(DAT_8005975c);
        DAT_800597b8.disp.x = 0;
        DAT_800597b8.disp.y = 0;
        DAT_800597b8.screen.x = 0;
        DAT_800597b8.screen.y = 0;
        DAT_800597b8.screen.w = 0;
        DAT_800597b8.screen.h = 0;
        DAT_800597b8.disp.w = (short)param_1;
        DAT_800597b8.disp.h = (short)param_2;
        int iVar1 = GetVideoMode();
        if (iVar1 == 1)
        {
            DAT_800597b8.screen.y = 0x18;
            DAT_800597b8.pad0 = 1;
        }

        DAT_800597b8.isinter = (byte)(param_3 & 1);
        DAT_800691c8 = (short)(param_3 & 4);
        DAT_800597b8.isrgb24 = (byte)param_5;
        LibGpu.PutDispEnv(DAT_800597b8);
    }

    // GHIDRA: gte_init @ 0x80048948 (SELECT.EXE)
    private static void gte_init()
    {
        LibGte.InitGeom();
        LibGte.SetFarColor(0, 0, 0);
        LibGte.SetGeomOffset(0, 0);
        DAT_80065398 = 0;
        DAT_80065394 = 0;
    }

    // GHIDRA: valiable_init @ 0x80047C98 (SELECT.EXE)
    // (The name is the library's own misspelling of "variable_init", carried through from Ghidra.)
    // Latches the screen size, builds an identity MATRIX at 0x800653D8 and propagates it to two more
    // 32-byte slots, arms the two block-fill packets, and sets the clip rectangle to the full screen.
    private static void valiable_init(ushort param_1, ushort param_2)
    {
        _DAT_800653d4 = param_2;
        _DAT_800653cc = param_1;

        // The original's `div v1,v0` at 0x80047CC4 is followed by the compiler's own two guards:
        // `break 0x1C00` when the divisor is 0 and `break 0x1800` on the 0x80000000 / -1 overflow.
        // C# raises DivideByZeroException and OverflowException on exactly those two operands, so
        // the guards are not re-spelled here.
        int iVar1 = (_DAT_800653d4 << 0xe) / _DAT_800653cc;

        WriteI16(DAT_800653d8, 4, 0);
        WriteI16(DAT_800653d8, 2, 0);
        WriteI16(DAT_800653d8, 0xa, 0);
        WriteI16(DAT_800653d8, 6, 0);
        WriteI16(DAT_800653d8, 0xe, 0);
        WriteI16(DAT_800653d8, 0xc, 0);
        WriteI32(DAT_800653d8, 0x1c, 0);
        WriteI32(DAT_800653d8, 0x18, 0);
        WriteI32(DAT_800653d8, 0x14, 0);
        WriteI16(DAT_800653d8, 0, 0x1000);
        WriteI16(DAT_800653d8, 8, 0x1000);
        WriteI16(DAT_800653d8, 0x10, 0x1000);

        // Eight lw/sw pairs at 0x80047D78-0x80047DB4, i.e. a 32-byte copy.
        Array.Copy(DAT_800653d8, DAT_80066300, 32);

        // Eight more at 0x80047DC8-0x80047E04, then the three diagonal shorts are cleared, which
        // leaves this slot entirely zero.
        Array.Copy(DAT_800653d8, DAT_80059540, 32);
        WriteI16(DAT_80059540, 0x10, 0);
        WriteI16(DAT_80059540, 8, 0);
        WriteI16(DAT_80059540, 0, 0);

        Array.Copy(DAT_80059540, DAT_8006539c, 32);

        DAT_80058ea8[0] = 0;
        DAT_80058ea8[1] = 0;
        DAT_80058eac[0] = 0;
        DAT_80058eac[1] = 0;
        DAT_800593b2 = 0;
        DAT_800593b0 = 0;
        DAT_8006632a = 0;

        // `mult v1,0x55555556` / `mfhi` / `subu v0,v0,v1>>31` at 0x80047E18-0x80047EB0: the compiler's
        // division by 3, stored as a halfword.
        DAT_80066308 = (short)((int)(((long)iVar1 * 0x55555556L) >> 32) - (iVar1 >> 31));

        DAT_80066328 = 0;
        DAT_8006632c = (ushort)_DAT_800653cc;
        DAT_8006632e = (ushort)_DAT_800653d4;
        SetBlockFill(DAT_80058c90);
        SetBlockFill(DAT_80058ca0);
        DAT_800691bc = 1;
    }

    // GHIDRA: GsSetDrawBuffClip @ 0x8004807C (SELECT.EXE)
    // Points DRAWENV.clip at the buffer the ACTIVE index selects and pushes it to the GPU.
    private static void GsSetDrawBuffClip()
    {
        DAT_8005975c.clip.w = (short)DAT_8006632c;
        DAT_8005975c.clip.h = (short)DAT_8006632e;
        DAT_8005975c.clip.x = (short)(DAT_80066328 + DAT_80058e78[DAT_800691c0]);
        DAT_8005975c.clip.y = (short)(DAT_8006632a + DAT_80058e7c[DAT_800691c0]);
        LibGpu.PutDrawEnv(DAT_8005975c);
    }

    // GHIDRA: GsSetDrawBuffOffset @ 0x800489FC (SELECT.EXE)
    // Publishes the offset that every 2D sort adds to its coordinates (DAT_80065394 / DAT_80065398).
    // THE OPPOSITE BUFFER INDEX IS DELIBERATE — see the double-buffer note at the top of this file.
    // It is not a decompiler artefact: at 0x80048AAC and 0x80048AD0 the branch is `bne a1,zero,+2`
    // over an `addiu v0,v0,2`, so an active index of 0 selects entry 1 and vice versa.
    private static void GsSetDrawBuffOffset()
    {
        if (DAT_800691c8 != 0)
        {
            DAT_80065398 = 0;
            DAT_80065394 = 0;
            DAT_8005975c.ofs[0] = (short)(DAT_800593b0 + DAT_80058e78[DAT_800691c0]);
            DAT_8005975c.ofs[1] = (short)(DAT_800593b2 + DAT_80058e7c[DAT_800691c0]);
            LibGpu.PutDrawEnv(DAT_8005975c);

            // The original's tail here is `j 0x80048B00`, a jump into this function's own epilogue.
            // Ghidra models that epilogue as a separate zero-body function, GS_002_OBJ_114
            // @ 0x80048B00; there is nothing to call.
            return;
        }

        int iVar5 = DAT_800593b0;
        short sVar1 = DAT_80058e78[DAT_800691c0 == 0 ? 1 : 0];
        int iVar4 = DAT_800593b2;
        short sVar2 = DAT_80058e7c[DAT_800691c0 == 0 ? 1 : 0];
        LibGte.SetGeomOffset(iVar5 + sVar1, iVar4 + sVar2);
        DAT_80065394 = (short)(iVar5 + sVar1);
        DAT_80065398 = (short)(iVar4 + sVar2);
    }

    // GHIDRA: GsInitGraph @ 0x80047A50 (SELECT.EXE)
    // SELECT.EXE calls it once, GsInitGraph(0x140, 0xF0, 0, 0, 0) from FUN_80030698 @ 0x80030698:
    // 320x240, intmode 0, no dither, 15-bit.
    public static void GsInitGraph(ushort x, ushort y, ushort intmode, ushort dith, ushort varmmode)
    {
        gpu_init(x, y, intmode, dith, varmmode);
        gte_init();
        DAT_800691c0 = 0;
        valiable_init(x, y);
        GsSetDrawBuffClip();
        GsSetDrawBuffOffset();
    }

    // GHIDRA: GsInit3D @ 0x80048194 (SELECT.EXE)
    // Moves the sort origin to the centre of the screen. The two divisions are the compiler's signed
    // `(v + (v >>> 31)) >> 1`, which is C#'s int division by 2.
    public static void GsInit3D()
    {
        DAT_800593b0 = (short)(_DAT_800653cc / 2);
        DAT_800593b2 = (short)(_DAT_800653d4 / 2);
        GsSetDrawBuffOffset();
        DAT_80068934 = 10;
        DAT_80068930 = 0;
        DAT_800662fc = 0x3fff;
    }

    // GHIDRA: GsDefDispBuff @ 0x8004879C (SELECT.EXE)
    // Ghidra leaves it unnamed but plates it "Possible GS_103.OBJ/GsDefDispBuff", and the body is
    // GsDefDispBuff's: it takes the two buffer origins (x0, y0, x1, y1) and writes both the draw
    // pair and the display pair, then re-arms the clip and the offset.
    // SELECT.EXE calls GsDefDispBuff(0, 0, 0x140, 0) — buffer 0 at VRAM (0,0), buffer 1 at (320,0).
    public static void GsDefDispBuff(short param_1, short param_2, short param_3, short param_4)
    {
        DAT_80058e78[0] = param_1;
        DAT_80058e78[1] = param_3;
        DAT_80058e7c[0] = param_2;
        DAT_80058e7c[1] = param_4;
        if (DAT_800691c8 == 0)
        {
            DAT_80058ea8[0] = param_1;
            DAT_80058ea8[1] = param_3;
            DAT_80058eac[0] = param_2;
            DAT_80058eac[1] = param_4;
        }
        else
        {
            DAT_80058ea8[0] = 0;
            DAT_80058ea8[1] = 0;
            DAT_80058eac[0] = 0;
            DAT_80058eac[1] = 0;
        }

        GsSetDrawBuffClip();
        GsSetDrawBuffOffset();
    }

    // GHIDRA: GsGetActiveBuff @ 0x800489EC (SELECT.EXE)
    public static int GsGetActiveBuff()
    {
        return DAT_800691c0;
    }

    // GHIDRA: GsSetWorkBase @ 0x8004883C (SELECT.EXE)
    // Ghidra leaves it unnamed and plates three candidates (GsSetNearClip / GsSetFarClip /
    // GsSetWorkBase), all of which are one-store functions. GsSetWorkBase is the one that fits the
    // USE: its single argument lands in DAT_80059430 @ 0x80059430, and GsSortLine, GsSortSprite and
    // GsSortBoxFill all read DAT_80059430 as the address they write their packet words through. A
    // near- or far-clip scalar could not be dereferenced that way. SELECT.EXE's frame step calls it
    // as GsSetWorkBase(activeBuf * 24000 + 0x800597CC), i.e. two 24000-byte packet areas.
    public static void GsSetWorkBase(int param_1)
    {
        DAT_80059430 = param_1;
    }

    // GHIDRA: GS_010_OBJ_88 @ 0x80048BA0 (SELECT.EXE)
    // Not a real function: it is the shared tail of GsSwapDispBuff, which reaches it by `j` from one
    // branch and by fall-through from the other. Ghidra made it a function, and it is kept as one
    // here so both branches read as they do in the original.
    private static void GS_010_OBJ_88()
    {
        GsSetDrawBuffClip();
        GsSetDrawBuffOffset();
        int iVar2 = DAT_800691bc + 1;
        bool bVar1 = DAT_800691bc != 0;
        DAT_800691bc = 1;
        if (bVar1)
        {
            DAT_800691bc = iVar2;
        }

        // The original stores the incremented value first (`sw v1` at 0x80048BC4) and then stores
        // the selected one over it (`sw a0` at 0x80048BD8). Both stores are kept above.
        iVar2 = 0x4d8;
        do
        {
            WriteI16(DAT_80058eb0, iVar2, 0);
            iVar2 = iVar2 + -0x28;
        } while (-1 < iVar2);
    }

    // GHIDRA: GsSwapDispBuff @ 0x80048B18 (SELECT.EXE)
    // Displays the buffer the active index currently selects, flips the index, and re-arms the clip
    // and the offset for the buffer that is about to be drawn into. Note that the DISPLAY origin is
    // taken from the DRAW pair (DAT_80058e78 / DAT_80058e7c), not from DAT_80058ea8 / DAT_80058eac.
    public static void GsSwapDispBuff()
    {
        DAT_800597b8.disp.x = DAT_80058e78[DAT_800691c0];
        DAT_800597b8.disp.y = DAT_80058e7c[DAT_800691c0];
        LibGpu.PutDispEnv(DAT_800597b8);
        LibGpu.SetDispMask(1);
        if (DAT_800691c0 == 0)
        {
            DAT_800691c0 = 1;
            GS_010_OBJ_88();
            return;
        }

        DAT_800691c0 = 0;
        GS_010_OBJ_88();
    }

    // GHIDRA: ReadGeomScreen @ 0x8004C8D8 (SELECT.EXE)
    // Three instructions — `gte_stH v0; jr ra; nop` — i.e. it reads GTE control register H, the
    // perspective projection distance, and returns it.
    // It sits at 0x8004C8D8, inside libgte, not libgs; it lives here only because LibGte.cs is owned
    // by a later phase and GsSortSprite's rotate/scale path needs it now. It belongs in LibGte.cs,
    // where H is the private field `gteH` — that file has a writer for it (LdH / SetGeomScreen) and
    // no reader at all.
    // THE VALUE IS A LINK-TIME CONSTANT IN THIS OVERLAY, which is why a constant can stand in for
    // the register: InitGeom @ 0x8004C744 executes `gte_ldH(1000)` (its decompilation, line 14, and
    // the `gte_ldH t0` at 0x8004C790), main @ 0x8003045C calls InitGeom before anything reaches a
    // sort, and SetGeomScreen @ 0x8004C1D4 has ZERO cross-references in SELECT.EXE (measured), so
    // nothing writes H again.
    // PARTIAL: it returns that constant rather than the register itself.
    private static int ReadGeomScreen()
    {
        return 1000;
    }

    // GHIDRA: _make_packet @ 0x80048708 (SELECT.EXE)
    // The tail every sort routine below ends with: it links a finished packet into one bucket of an
    // ordering table and returns the work-area cursor advanced past that packet.
    //   bucket index = (pri & 0xffff) - ot->offset, and a NEGATIVE index is only diagnosed, not
    //     corrected — the printf runs and the out-of-range bucket is then written anyway
    //     (`bgez s1` at 0x80048734 skips the printf, nothing else);
    //   bucket address = ot->org + index * 4;
    //   packet->tag = *bucket, then packet's length byte := param_4;
    //   *bucket = packet, then the bucket's length byte := 0.
    // THAT IS AddPrim'S SPLICE PLUS TWO STORES AddPrim DOES NOT DO. LibGpu.AddPrim preserves the top
    // byte of both words; this writes both of them. The four memory operations are therefore spelled
    // out here rather than delegated, which also keeps their ORDER — copy, set length, store back,
    // clear length — the order the image has them in.
    // The return is `param_1 + param_4 + 1` on an `int *`, i.e. (len + 1) words: the raw MIPS is
    // `andi v0,s3,0xff; sll v0,v0,2; addiu v0,v0,4; addu v0,s0,v0` at 0x80048758-0x80048768.
    private static int _make_packet(int param_1, GsOT param_2, uint param_3, byte param_4)
    {
        int iVar2 = (int)(param_3 & 0xffff) - (int)param_2.offset;
        if (iVar2 < 0)
        {
            Kernel.printf("ps_sort_sprite,bg: z resolution overflow\n");
        }

        int piVar1 = iVar2 * 4 + param_2.org;

        // The original dereferences two raw addresses here. C# cannot, so both are resolved through
        // the registry that DAT_800597cc and the game's tag arrays declare themselves to.
        if (LibGpu.RamResolve(param_1, out byte[] pkt, out int pktOffset)
            && LibGpu.RamResolve(piVar1, out byte[] bucket, out int bucketOffset))
        {
            WriteU32(pkt, pktOffset, ReadU32(bucket, bucketOffset));
            pkt[pktOffset + 3] = param_4;
            WriteU32(bucket, bucketOffset, (uint)param_1);
            bucket[bucketOffset + 3] = 0;
        }

        return param_1 + ((param_4 & 0xff) << 2) + 4;
    }

    // GHIDRA: GsClearOt @ 0x80048994 (SELECT.EXE)
    // Initialises one GsOT's tag array as a linked chain. Eighty-eight bytes, five statements.
    //
    // WHICH DIRECTION, MEASURED RATHER THAN ASSUMED: this libgs builds the table in REVERSE. The
    // call at 0x800489D4 is `jal 0x800495d8`, and 0x800495D8 is ClearOTagR — not ClearOTag. That is
    // why `tag` is set to the LAST entry and not to org: ClearOTagR links entry i back to entry
    // i - 1 and terminates entry 0, so the head of the chain is the highest entry, which is the one
    // GsDrawOt hands to DrawOTag.
    // TITLE.EXE clears FORWARD (ClearOTag(&DAT_800a6830, 0x800) in RunFrameLoop @ 0x800587A8) and
    // that has no bearing here; the two overlays do not share a renderer.
    //
    // HOW BIG THE TAG ARRAY IS, also measured: `1 << (length & 0x1f)` entries and no more. This
    // libgs does NO sub-division — the whole body is offset, point, tag, ClearOTagR, and there is no
    // second table and no doubling anywhere in the 88 bytes. SELECT.EXE's two GsOT carry length = 3,
    // so eight tags each, and main @ 0x8003045C arms their org fields 0x20 = 8 * 4 bytes apart,
    // which is exactly 2^3 entries and leaves no room for more.
    //
    // tag = (char *)org - 4 + (4 << length), i.e. &org[(1 << length) - 1]: `sllv v0,v0,v1` /
    // `addiu v0,v0,-0x4` / `sw v0,0x10(a2)` at 0x800489BC-0x800489C8, with v0 = 4 and v1 = length.
    public static void GsClearOt(ushort offset, ushort point, GsOT otp)
    {
        otp.offset = offset;
        otp.point = point;
        otp.tag = (otp.org - 4) + (4 << (int)(otp.length & 0x1f));
        LibGpu.ClearOTagR(otp.org, 1 << (int)(otp.length & 0x1f));
    }

    // GHIDRA: GsSortLine @ 0x80048C0C (SELECT.EXE)
    // Builds a LINE_F2 in the work area and splices it in. Four words: the tag, one colour/code word
    // and the two endpoints, hence the length 3 handed to _make_packet.
    // The code byte is `0x40 | ((attribute >> 0x1d) & 2)` — the GPU's flat two-point line command
    // with its semi-transparency bit taken from attribute bit 30.
    // SELECT.EXE's frame step sorts four of these every frame, from 0x80065484, 0x80065494,
    // 0x800654A4 and 0x800654B4, all at priority 1.
    public static void GsSortLine(GsLINE lp, GsOT ot, ushort pri)
    {
        int iVar2 = DAT_80059430;
        uint uVar5 = lp.attribute;
        if (-1 < (int)uVar5)
        {
            // The original writes straight through the cursor. C# resolves it first; a cursor that
            // does not resolve means the work area was never registered, and nothing can be written.
            if (!LibGpu.RamResolve(iVar2, out byte[] pBuf, out int pOff))
            {
                return;
            }

            pBuf[pOff + 4] = lp.r;
            pBuf[pOff + 5] = lp.g;
            byte uVar1 = lp.b;
            pBuf[pOff + 7] = (byte)(((byte)((int)uVar5 >> 0x1d) & 2) | 0x40);
            pBuf[pOff + 6] = uVar1;
            short sVar4 = DAT_80065398;
            short sVar3 = DAT_80065394;
            WriteI16(pBuf, pOff + 8, lp.x0 + DAT_80065394);
            WriteI16(pBuf, pOff + 10, lp.y0 + sVar4);
            WriteI16(pBuf, pOff + 0xc, lp.x1 + sVar3);
            WriteI16(pBuf, pOff + 0xe, lp.y1 + sVar4);
            DAT_80059430 = _make_packet(iVar2, ot, pri, 3);
        }
    }

    // GHIDRA: GsSortBoxFill @ 0x80048870 (SELECT.EXE)
    // Builds a MERGED packet — one GP0(0xE1) draw-mode word followed by one TILE — in the work area
    // and splices it in. Five words: the tag, the draw-mode word, and the TILE's three, hence the
    // length 4.
    // The draw-mode word is `0xE1000200 | (attribute >> 0x11 & 0x180) | (attribute >> 0x17 & 0x60)`:
    // the constant carries the dither bit (0x200), attribute bits 24-25 become the colour-depth
    // field and bits 28-29 the semi-transparency rate. The TILE's code byte is
    // `0x60 | ((attribute >> 0x1d) & 2)`.
    // SELECT.EXE's frame step sorts five of these from 0x80067B68, but only on the boxfill path
    // (DAT_80055B80 bit 3).
    public static void GsSortBoxFill(GsBOXF bp, GsOT ot, ushort pri)
    {
        int iVar2 = DAT_80059430;
        uint uVar3 = bp.attribute;
        if (-1 < (int)uVar3)
        {
            if (!LibGpu.RamResolve(iVar2, out byte[] pBuf, out int pOff))
            {
                return;
            }

            WriteI32(pBuf, pOff + 4,
                ((int)uVar3 >> 0x11 & 0x180) | ((int)uVar3 >> 0x17 & 0x60) | unchecked((int)0xe1000200));
            pBuf[pOff + 8] = bp.r;
            pBuf[pOff + 9] = bp.g;
            byte uVar1 = bp.b;
            pBuf[pOff + 0xb] = (byte)(((byte)((int)uVar3 >> 0x1d) & 2) | 0x60);
            pBuf[pOff + 10] = uVar1;
            WriteI16(pBuf, pOff + 0xc, bp.x + DAT_80065394);
            WriteI16(pBuf, pOff + 0xe, bp.y + DAT_80065398);
            WriteU16(pBuf, pOff + 0x10, bp.w);
            WriteU16(pBuf, pOff + 0x12, bp.h);
            DAT_80059430 = _make_packet(iVar2, ot, pri, 4);
        }
    }

    // GHIDRA: 2D_SP0_OBJ_4C0 @ 0x800486CC (SELECT.EXE)
    // Not a real function: it is the shared tail of GsSortSprite, and Ghidra made it one because two
    // paths reach it. The fast path arrives by `j 0x800486cc` at 0x800483A8; the rotate/scale path
    // falls into it from 0x800486C8. Both arrive with the packet's LAST word split across two
    // registers, which the tail ORs together before making the packet:
    //     0x800486CC  or v1,v1,v0
    //     0x800486D0  jal _make_packet
    //     0x800486D4  sw v1,0x14(a0)      (delay slot)
    //     0x800486D8  addu s2,v0,zero
    //     0x800486E0  sw s2,-0x6BD0(at)   -> DAT_80059430
    // The two halves are named in_v1 / in_v0 here because that is all the image says they are; what
    // they mean differs per caller and is documented at each call site.
    // JUSTIFICATION: C# language bridge only
    // RELATION: the register-passed operands of a jump target, made explicit as parameters. a0/a1/a2
    // and a3 (the packet cursor, the ordering table, the priority and the length) are passed
    // straight through to _make_packet by both callers and are spelled the same way.
    private static void _D_SP0_OBJ_4C0(int param_1, int in_v1, int in_v0, GsOT ot, ushort pri, byte len)
    {
        if (LibGpu.RamResolve(param_1, out byte[] pBuf, out int pOff))
        {
            WriteI32(pBuf, pOff + 0x14, in_v1 | in_v0);
        }

        DAT_80059430 = _make_packet(param_1, ot, pri, len);
    }

    // GHIDRA: GsSortSprite @ 0x8004820C (SELECT.EXE)
    // The one routine SELECT.EXE draws its whole screen with: the frame step FUN_800344a4
    // @ 0x800344A4 calls it up to a hundred times a frame over GsSPRITE_ARRAY_800654EC.
    //
    // IT HAS TWO PACKET SHAPES, and which one it builds is decided by the gate at 0x80048278-
    // 0x8004829C:
    //   uVar13 = (attribute >> 0x1b) & 1;                       attribute bit 27 forces the fast path
    //   if (scalex,scaley) == (0x1000,0x1000) and rotate == 0:
    //       uVar14 = ((attribute & 0xc00000) == 0);             no flip either -> fast path
    //   if (uVar13 | uVar14) -> unrotated, unscaled, unflipped: a MERGED GP0(0xE1) + SPRT, length 5
    //   else                 -> the GTE path: a POLY_FT4, length 9
    // The scale test is one 32-bit compare of the word at +0x1C, which is scalex in its low half and
    // scaley in its high half — `lw v0,0x1c(s0)` against `lui v1,0x1000; ori v1,v1,0x1000`.
    //
    // WHAT GHIDRA CALLS 2D_SP0_OBJ_388 AND 2D_SP0_OBJ_3C8 ARE NOT CALLS. They are labels inside this
    // function, reached by `j`, and its decompilation renders them as calls that never return. Read
    // as raw MIPS at 0x80048554-0x800485D0 the shape is two plain if/else pairs feeding one common
    // tail, which is how they are written below:
    //     0x80048554  lui v0,0x80 / and v0,s1,v0 / beqz v0,0x80048580
    //     0x80048564  (taken)     t1 = u + (byte)w - 1 ; t4 = u              [attribute bit 23 set]
    //     0x80048580  (not taken) t1 = u               ; t4 = u + (byte)w - 1
    //     0x80048594  lui v0,0x40 / and v0,s1,v0 / beqz v0,0x800485C0
    //     0x800485A4  (taken)     t2 = v + (byte)h - 1 ; t3 = v              [attribute bit 22 set]
    //     0x800485C0  (not taken) t2 = v               ; t3 = v + (byte)h - 1
    //     0x800485D4  the common tail: addu a0,s2,zero, then the nine packet words
    // So bit 23 swaps the two U coordinates and bit 22 swaps the two V coordinates — the horizontal
    // and vertical flips — and the packet is otherwise identical. The four vertices then read
    // (t1,t2) (t4,t2) (t1,t3) (t4,t3).
    //
    // 2D_SP0_OBJ_240 @ 0x8004844C is likewise a label, the point both GTE paths converge on.
    public static void GsSortSprite(GsSPRITE _29, GsOT ot, ushort pri)
    {
        int iVar11 = DAT_80059430;
        uint uVar16 = _29.attribute;
        if ((int)uVar16 < 0)
        {
            return;
        }

        if (_29.w == 0)
        {
            return;
        }

        uint uVar14 = 0;
        if (_29.h == 0)
        {
            return;
        }

        int iVar12 = (ushort)_29.scalex | (_29.scaley << 0x10);
        uint uVar13 = uVar16 >> 0x1b & 1;
        if (iVar12 == 0x10001000)
        {
            if (_29.rotate == 0)
            {
                uVar14 = (uint)((uVar16 & 0xc00000) == 0 ? 1 : 0);
            }
        }

        uVar13 = uVar13 | uVar14;

        // 2D_SP0_OBJ_94 @ 0x800482A0
        if (uVar13 != 0)
        {
            if (!LibGpu.RamResolve(iVar11, out byte[] fBuf, out int fOff))
            {
                return;
            }

            short sVar6 = _29.x;
            short sVar7 = _29.y;
            WriteI32(fBuf, fOff + 4,
                (_29.tpage & 0x1f) | (int)(uVar16 >> 0x11 & 0x180) | unchecked((int)0xe1000200)
                | (int)(uVar16 >> 0x17 & 0x60));
            int iVar15 = DAT_80065394;
            iVar12 = DAT_80065398;
            WriteI32(fBuf, fOff + 8,
                (int)(uVar16 >> 5 & 0x2000000) | (int)((uVar16 & 0x40) << 0x12)
                | unchecked((int)0x64000000) | (_29.b << 0x10) | (_29.g << 8) | _29.r);
            WriteI32(fBuf, fOff + 0xc,
                (((sVar6 + iVar15) - _29.mx) & 0xffff) | (((sVar7 + iVar12) - _29.my) * 0x10000));
            int uVar9 = _29.u | (_29.v << 8);
            WriteI32(fBuf, fOff + 0x10, uVar9 | (_29.cy << 0x16) | ((_29.cx & 0x3f0) << 0xc));

            // The two halves of the SPRT's width/height word: `lhu v1,0x8(s0)` at 0x800483A4 and
            // `lhu v0,0xa(s0)` / `sll v0,v0,0x10` at 0x800483A0 / 0x800483AC.
            _D_SP0_OBJ_4C0(iVar11, _29.w, _29.h << 0x10, ot, pri, 5);
            return;
        }

        LibGte.MATRIX local_88 = new LibGte.MATRIX();
        if (_29.rotate != 0)
        {
            // 2D_SP0_OBJ_204 @ 0x80048410
            LibGte.SVECTOR local_68 = new LibGte.SVECTOR();
            local_68.vx = 0;
            local_68.vy = 0;

            // The compiler's signed division of `rotate`: `mult v1,v0` with v0 = 0xB60B60B7, then
            // `mfhi v0; addu v0,v0,v1; sra v0,v0,8; sra v1,v1,31; subu v0,v0,v1`
            // (0x80048420-0x8004843C). 0xB60B60B7 is floor(2^40 / 360) + 1, so the divisor is 360.
            int iVar1 = (int)(((long)_29.rotate * (long)unchecked((int)0xb60b60b7)) >> 0x20);
            iVar1 = (iVar1 + _29.rotate) >> 8;
            local_68.vz = (short)(iVar1 - (_29.rotate >> 0x1f));

            LibGte.RotMatrix(local_68, local_88);
        }
        else
        {
            // 0x800483C4-0x80048404: the 32-byte MATRIX image at DAT_800653D8 copied as eight words
            // into the local matrix, then `j 2D_SP0_OBJ_240`.
            local_88.m[0] = ReadI16(DAT_800653d8, 0x00);
            local_88.m[1] = ReadI16(DAT_800653d8, 0x02);
            local_88.m[2] = ReadI16(DAT_800653d8, 0x04);
            local_88.m[3] = ReadI16(DAT_800653d8, 0x06);
            local_88.m[4] = ReadI16(DAT_800653d8, 0x08);
            local_88.m[5] = ReadI16(DAT_800653d8, 0x0a);
            local_88.m[6] = ReadI16(DAT_800653d8, 0x0c);
            local_88.m[7] = ReadI16(DAT_800653d8, 0x0e);
            local_88.m[8] = ReadI16(DAT_800653d8, 0x10);
            local_88.t[0] = ReadI32(DAT_800653d8, 0x14);
            local_88.t[1] = ReadI32(DAT_800653d8, 0x18);
            local_88.t[2] = ReadI32(DAT_800653d8, 0x1c);
        }

        // 2D_SP0_OBJ_240 @ 0x8004844C
        LibGte.VECTOR local_60 = new LibGte.VECTOR();
        int iVar5 = (ushort)_29.scalex | (_29.scaley << 0x10);
        if (iVar5 != 0x10001000)
        {
            local_60.vx = _29.scalex;
            local_60.vy = _29.scaley;
            local_60.vz = 0;
            LibGte.ScaleMatrix(local_88, local_60);
        }

        local_60.vx = _29.x;
        local_60.vy = _29.y;
        local_60.vz = ReadGeomScreen();
        LibGte.TransMatrix(local_88, local_60);
        LibGte.SetRotMatrix(local_88);
        LibGte.SetTransMatrix(local_88);

        // JUSTIFICATION: C# language bridge only — the original's four source SVECTORs and six
        // destination longs are contiguous stack slots (sp+0x60 .. sp+0x97) that it passes to
        // RotTransPers4 by address, and LibGte.RotTransPers4 takes (buffer, offset) pairs. This is
        // that stack window, at the same relative offsets: local_50 / local_48 / local_40 / local_38
        // at 0x00 / 0x08 / 0x10 / 0x18, local_30 / local_2c / local_28 / local_24 at 0x20 / 0x24 /
        // 0x28 / 0x2c, and lStack_20 / lStack_1c at 0x30 / 0x34.
        byte[] local_50 = new byte[0x38];
        WriteI16(local_50, 0x04, 0);
        WriteI16(local_50, 0x00, -_29.mx);
        WriteI16(local_50, 0x02, -_29.my);
        WriteI16(local_50, 0x0c, 0);
        WriteI16(local_50, 0x08, ReadI16(local_50, 0x00) + _29.w);
        WriteI16(local_50, 0x14, 0);
        WriteI16(local_50, 0x12, ReadI16(local_50, 0x02) + _29.h);
        WriteI16(local_50, 0x18, ReadI16(local_50, 0x00) + _29.w);
        WriteI16(local_50, 0x1c, 0);
        WriteI16(local_50, 0x1a, ReadI16(local_50, 0x02) + _29.h);
        WriteI16(local_50, 0x0a, ReadI16(local_50, 0x02));
        WriteI16(local_50, 0x10, ReadI16(local_50, 0x00));
        LibGte.RotTransPers4(local_50, 0x00, 0x08, 0x10, 0x18,
            local_50, 0x20, 0x24, 0x28, 0x2c,
            local_50, 0x30, local_50, 0x34);

        // 0x80048554-0x80048590: attribute bit 23 swaps the two U coordinates.
        int in_t1;
        int in_t4;
        if ((uVar16 & 0x800000) != 0)
        {
            in_t1 = (_29.u + (byte)_29.w) - 1;
            in_t4 = _29.u;
        }
        else
        {
            in_t1 = _29.u;
            in_t4 = (_29.u + (byte)_29.w) - 1;
        }

        // 0x80048594-0x800485D0: attribute bit 22 swaps the two V coordinates.
        int in_t2;
        int in_t3;
        if ((uVar16 & 0x400000) != 0)
        {
            in_t2 = (_29.v + (byte)_29.h) - 1;
            in_t3 = _29.v;
        }
        else
        {
            in_t2 = _29.v;
            in_t3 = (_29.v + (byte)_29.h) - 1;
        }

        // 2D_SP0_OBJ_3C8 @ 0x800485D4 — the common tail. Nine words after the tag: a POLY_FT4.
        if (!LibGpu.RamResolve(iVar11, out byte[] pBuf, out int pOff))
        {
            return;
        }

        int uVar8 = (in_t2 & 0xff) << 8;
        byte bVar1 = _29.b;
        byte bVar2 = _29.g;
        byte bVar3 = _29.r;
        WriteI32(pBuf, pOff + 8, ReadI32(local_50, 0x20));
        WriteI32(pBuf, pOff + 4,
            (int)(uVar16 >> 5 & 0x2000000) | (int)((uVar16 & 0x40) << 0x12)
            | unchecked((int)0x2c000000) | (bVar1 << 0x10) | (bVar2 << 8) | bVar3);
        short sVar4 = _29.cy;
        short sVar5 = _29.cx;
        WriteI32(pBuf, pOff + 0x10, ReadI32(local_50, 0x24));
        WriteI32(pBuf, pOff + 0xc,
            (in_t1 & 0xff) | uVar8 | (sVar4 << 0x16) | ((sVar5 & 0x3f0) << 0xc));
        ushort uVar6 = _29.tpage;
        WriteI32(pBuf, pOff + 0x18, ReadI32(local_50, 0x28));
        int uVar7 = (in_t3 & 0xff) << 8;
        WriteI32(pBuf, pOff + 0x1c, (in_t1 & 0xff) | uVar7);
        WriteI32(pBuf, pOff + 0x20, ReadI32(local_50, 0x2c));
        WriteI32(pBuf, pOff + 0x24, (in_t4 & 0xff) | uVar7);

        // The last word again falls to the shared tail: v1 carries everything but the two
        // colour-depth bits, v0 carries those (`srl v0,s1,7; lui t0,0x60; and v0,v0,t0` at
        // 0x800486C0-0x800486C8).
        _D_SP0_OBJ_4C0(iVar11,
            (in_t4 & 0xff) | uVar8 | ((uVar6 & 0x1f) << 0x10) | (int)(uVar16 >> 1 & 0x1800000),
            (int)(uVar16 >> 7 & 0x600000),
            ot, pri, 9);
    }

    // GHIDRA: GS_001_OBJ_5FC @ 0x8004804C (SELECT.EXE)
    // Not a real function: it is the shared tail of GsSortClear, which the 24-bit branch reaches by
    // `j` and the 15-bit branch by fall-through. Ghidra made it a function, and it is kept as one
    // here for the same reason GS_010_OBJ_88 above is — so both branches read as they do in the
    // original. Its argument is in a3, which is GsSortClear's own fourth parameter untouched.
    private static void GS_001_OBJ_5FC(GsOT in_a3)
    {
        LibGpu.AddPrim(in_a3.tag,
            LibGpu.RamAddressOf(DAT_800691c0 == 0 ? DAT_80058c90 : DAT_80058ca0, 0));
    }

    // GHIDRA: GsSortClear @ 0x80047F14 (SELECT.EXE)
    // Arms one of the two block-fill packets valiable_init prepared and links it into the ordering
    // table, so the frame's background is cleared to (r, g, b) before anything else is drawn.
    // The packet's r/g/b go at +4/+5/+6, its rect at +8/+10/+12/+14, and SetBlockFill already put
    // the tag length 3 at +3 and the GPU's fill command 0x02 at +7.
    // The rect is the WHOLE draw buffer: origin from DAT_80058e78 / DAT_80058e7c indexed by the
    // ACTIVE buffer, size from the screen size valiable_init latched.
    // SELECT.EXE's frame step calls it once per frame, GsSortClear(0,0,0,otp), unless bit 0 of
    // DAT_80055B80 is set.
    //
    // THE PACKET SELECTION: the original writes through `&DAT_80058c90 + DAT_800691c0 * 0x10`, one
    // 16-byte packet per display buffer. The two are adjacent in the image (0x80058C90 and
    // 0x80058CA0) and this port declares them as the two separate regions the SetBlockFill calls
    // above already pass separately, so the index becomes a selection between them.
    public static void GsSortClear(byte param_1, byte param_2, byte param_3, GsOT param_4)
    {
        byte[] pkt = DAT_800691c0 == 0 ? DAT_80058c90 : DAT_80058ca0;

        pkt[4] = param_1;
        pkt[5] = param_2;
        pkt[6] = param_3;
        short uVar2 = (short)_DAT_800653d4;
        int iVar3 = DAT_800691c0;
        WriteI16(pkt, 8, DAT_80058e78[iVar3]);
        short uVar1 = DAT_80058e7c[iVar3];
        WriteI16(pkt, 0xe, uVar2);
        WriteI16(pkt, 0xa, uVar1);
        if (DAT_800597b8.isrgb24 != 0)
        {
            WriteI16(pkt, 0xc, (short)((_DAT_800653cc * 3) / 2));
            GS_001_OBJ_5FC(param_4);
            return;
        }

        WriteI16(pkt, 0xc, (short)_DAT_800653cc);
        LibGpu.AddPrim(param_4.tag, LibGpu.RamAddressOf(pkt, 0));
    }

    // GHIDRA: GsDrawOt @ 0x8004884C (SELECT.EXE)
    // Thirty-six bytes, one call: `lw a0,0x10(a0)` then `jal 0x800496d0` — GsOT.tag straight to
    // libgpu's DrawOTag. tag is the LAST tag GsClearOt linked, which is the head of the reverse
    // chain. Ghidra plates the function "Possible GS_112.OBJ/GsDrawOtIO"; the body is DrawOTag's,
    // not DrawOTagIO's (0x800496D0 is DrawOTag).
    public static void GsDrawOt(GsOT ot)
    {
        LibGpu.DrawOTag(ot.tag);
    }
}
