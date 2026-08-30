using System;
using static PsxSdkMonogame.MipsMemory;

namespace PsxSdkMonogame;

public static class LibGpu
{
    public class RECT
    {
        public short x;
        public short y;
        public short w;
        public short h;
    }

    public class RECT32
    {
        public int x, y;
        public int w, h;
    }

    public class OT_TYPE
    {
        public ulong tag;
        public ulong len;
    }

    public class P_TAG
    {
        public ulong addr;
        public ulong len;
        byte r0, g0, b0, code;
    }

    public class P_CODE
    {
        byte r0, g0, b0, code;
    }

    public class DR_ENV
    {
        public ulong tag;
        public ulong[] code = new ulong[15];
    }

    public class POLY_F3
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        short x1, y1;
        short x2, y2;
    } // 0x20

    public class POLY_FT3
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        byte u0, v0;
        public ushort clut;
        short x1, y1;
        byte u1, v1;
        public ushort tpage;
        short x2, y2;
        byte u2, v2;
        public ushort pad1;
    } // 0x24

    public class POLY_F4
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        short x1, y1;
        short x2, y2;
        short x3, y3;
    } // 0x28

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the vertex, colour and UV fields were left private, so no transliterated call site
    // could fill one. FUN_80058d64 @ 0x80058D64 writes every one of them by hand.
    public class POLY_FT4
    {
        public ulong tag;
        public byte r0, g0, b0, code;
        public short x0, y0;
        public byte u0, v0;
        public ushort clut;
        public short x1, y1;
        public byte u1, v1;
        public ushort tpage;
        public short x2, y2;
        public byte u2, v2;
        public ushort pad1;
        public short x3, y3;
        public byte u3, v3;
        public ushort pad2;
    } // 0x2C

    public class POLY_G3
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        byte r1, g1, b1, pad1;
        short x1, y1;
        byte r2, g2, b2, pad2;
        short x2, y2;
    } // 0x30

    public class POLY_GT3
    {
        public ulong tag;
        public byte r0;
        public byte g0;
        public byte b0;
        public byte code;
        public short x0;
        public short y0;
        public byte u0;
        public byte v0;
        public ushort clut;
        public byte r1;
        public byte g1;
        public byte b1;
        public byte p1;
        public short x1;
        public short y1;
        public byte u1;
        public byte v1;
        public ushort tpage;
        public byte r2;
        public byte g2;
        public byte b2;
        public byte p2;
        public short x2;
        public short y2;
        public byte u2;
        public byte v2;
        public ushort pad2;
    } // 0x34

    public class POLY_G4
    {
        public ulong tag;
        public byte r0;
        public byte g0;
        public byte b0;
        public byte code;
        public short x0;
        public short y0;
        public byte r1;
        public byte g1;
        public byte b1;
        public byte pad1;
        public short x1;
        public short y1;
        public byte r2;
        public byte g2;
        public byte b2;
        public byte pad2;
        public short x2;
        public short y2;
        public byte r3;
        public byte g3;
        public byte b3;
        public byte pad3;
        public short x3;
        public short y3;
    } // 0x38

    public class POLY_GT4
    {
        public ulong tag;
        public byte r0;
        public byte g0;
        public byte b0;
        public byte code;
        public short x0;
        public short y0;
        public byte u0;
        public byte v0;
        public ushort clut;
        public byte r1;
        public byte g1;
        public byte b1;
        public byte p1;
        public short x1;
        public short y1;
        public byte u1;
        public byte v1;
        public ushort tpage;
        public byte r2;
        public byte g2;
        public byte b2;
        public byte p2;
        public short x2;
        public short y2;
        public byte u2;
        public byte v2;
        public ushort pad2;
        public byte r3;
        public byte g3;
        public byte b3;
        public byte p3;
        public short x3;
        public short y3;
        public byte u3;
        public byte v3;
        public ushort pad3;
    } // 0x3C

    public class LINE_F2
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        short x1, y1;
    } // 0x40

    public class LINE_F3
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        short x1, y1;
        short x2, y2;
        public ulong pad;
    } // 0x48

    public class LINE_F4
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        short x1, y1;
        short x2, y2;
        short x3, y3;
        public ulong pad;
    } // 0x4C

    public class LINE_G2
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        byte r1, g1, b1, p1;
        short x1, y1;
    } // 0x50

    public class LINE_G3
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        byte r1, g1, b1, p1;
        short x1, y1;
        byte r2, g2, b2, p2;
        short x2, y2;
        public ulong pad;
    } // 0x58

    public class LINE_G4
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        byte r1, g1, b1, p1;
        short x1, y1;
        byte r2, g2, b2, p2;
        short x2, y2;
        byte r3, g3, b3, p3;
        short x3, y3;
        public ulong pad;
    } // 0x5C

    public class TILE
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        short w, h;
    } // 0x60

    public class SPRT
    {
        public ulong tag;
        public byte r0;
        public byte g0;
        public byte b0;
        public byte code;
        public short x0;
        public short y0;
        public byte u0;
        public byte v0;
        public ushort clut;
        public short w;
        public short h;
    } // 0x64

    public class TILE_1
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
    } // 0x68

    public class TILE_8
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
    } // 0x70

    public class SPRT_8
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
        byte u0, v0;
        public ushort clut;
    } // 0x74

    public class TILE_16
    {
        public ulong tag;
        byte r0, g0, b0, code;
        short x0, y0;
    } // 0x78

    public class SPRT_16
    {
        public ulong tag;
        public byte r0;
        public byte g0;
        public byte b0;
        public byte code;
        public short x0;
        public short y0;
        public byte u0;
        public byte v0;
        public ushort clut;
    } // 0x7C

    /*
     *  Special Primitive Definitions
     */
    public class DR_MODE
    {
        public ulong tag;
        public ulong[] code = new ulong[2];
    }

    public class DR_TWIN
    {
        public ulong tag;
        public ulong[] code = new ulong[2];
    }

    public class DR_AREA
    {
        public ulong tag;
        public ulong[] code = new ulong[2];
    }

    public class DR_OFFSET
    {
        public ulong tag;
        public ulong[] code = new ulong[2];
    }

    public class DR_STP
    {
        public ulong tag;
        public ulong[] code = new ulong[2];
    }

    public class DR_MOVE
    {
        public ulong tag;
        public ulong[] code = new ulong[5];
    }

    public class DR_LOAD
    {
        public ulong tag;
        public ulong[] code = new ulong[3];
        public ulong[] p = new ulong[13];
    }

    public class DR_TPAGE
    {
        public ulong tag;
        public ulong[] code = new ulong[1];
    }

    public class DRAWENV
    {
        public RECT clip = new();
        public short[] ofs = new short[2];
        public RECT tw = new();
        public ushort tpage;
        public byte dtd;
        public byte dfe;
        public byte isbg;
        public byte r0, g0, b0;
        public DR_ENV dr_env;
    }

    public class DISPENV
    {
        public RECT disp = new();
        public RECT screen = new();
        public byte isinter;
        public byte isrgb24;
        public byte pad0;
        public byte pad1;
    }

    public class PixPattern
    {
        public byte w;
        public byte h;
        public byte x;
        public byte y;
    }

    public class TIM_IMAGE
    {
        public ulong mode;
        RECT crect;
        ulong[] caddr;
        RECT prect;
        ulong[] paddr;
    }


    /**
     * @brief Load texture pattern to frame buffer
     *
     * Loads a texture pattern from the memory area starting at the address pix into
     * the frame buffer area starting at the address (x, y), and calculates the
     * texture page ID for the loaded texture pattern. The texture pattern size w
     * represents the number of pixels, not the actual size of the transfer area in
     * the frame buffer.
     *
     * @param pix Pointer to texture pattern start address
     * @param tp Bit depth (0 = 4-bit; 1 = 8-bit; 2 = 16-bit)
     * @param abr Semitransparency rate
     * @param x Destination frame buffer X address
     * @param y Destination frame buffer Y address
     * @param w Texture pattern width
     * @param h Texture pattern height
     * @return Texture page ID
     */
    public static ushort LoadTPage(ulong[] pix, int tp, int abr, int x, int y, int w, int h)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Load CLUT to frame buffer
     *
     * @param clut Pointer to CLUT data
     * @param x Horizontal frame buffer address
     * @param y Vertical frame buffer address
     * @return CLUT ID
     */
    public static ushort LoadClut(ulong[] clut, int x, int y)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Load CLUT to frame buffer (alternative)
     *
     * @param clut Pointer to CLUT data
     * @param x Horizontal frame buffer address
     * @param y Vertical frame buffer address
     * @return CLUT ID
     */
    public static ushort LoadClut2(ulong[] clut, int x, int y)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Calculate and return texture CLUT ID
     *
     * The CLUT address is limited to multiples of 16 in the x direction.
     *
     * @param x Horizontal frame buffer address of CLUT
     * @param y Vertical frame buffer address of CLUT
     * @return CLUT ID
     */
    // GHIDRA: GetClut @ 0x80077aa4
    // CERTAIN: pure bit-packing, no globals/side effects — return (y & 0x3ff) << 6 | (x >> 4) & 0x3f.
    // Fits in ushort (max 0xffff), matching this method's existing return type.
    public static ushort GetClut(int x, int y)
    {
        return (ushort)(((y & 0x3ff) << 6) | ((x >> 4) & 0x3f));
    }

    /**
     * @brief Calculate and return texture page ID
     *
     * @param tp Texture mode (0=4bit, 1=8bit, 2=16bit)
     * @param abr Semi-transparency rate (0=0.5, 1=1.0, 2=1.0, 3=0.25)
     * @param x Texture page X position in frame buffer
     * @param y Texture page Y position in frame buffer
     * @return Texture page ID
     */
    // GHIDRA: GetTPage @ 0x80077a64
    // CERTAIN: pure bit-packing, no globals/side effects —
    // (tp & 3) << 7 | (abr & 3) << 5 | (y & 0x100) >> 4 | (x & 0x3ff) >> 6 | (y & 0x200) << 2.
    // Fits in ushort (max 0xffff), matching this method's existing return type.
    public static ushort GetTPage(int tp, int abr, int x, int y)
    {
        return (ushort)(((tp & 3) << 7) | ((abr & 3) << 5) | ((y & 0x100) >> 4) | ((x & 0x3ff) >> 6) | ((y & 0x200) << 2));
    }

    /**
     * @brief Get next primitive in list
     *
     * @param p Pointer to current primitive
     * @return Pointer to next primitive
     */
    public static object NextPrim(object p)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Register a primitive to the OT
     *
     * Registers a primitive beginning with the address *p to the OT entry *ot in
     * OT table. A primitive may be added to a primitive list only once in the same
     * frame.
     *
     * @param ot OT entry
     * @param p Start address of primitive to be registered
     */
    public static void AddPrim(object ot, object p)
    {
        // Do nothing PSX SDK
    }

    // GHIDRA: AddPrim @ 0x80077ac4 — PSX SDK, twelve instructions, read as raw MIPS:
    //     p->tag = (p->tag & 0xFF000000) | (*ot & 0x00FFFFFF)
    //     *ot    = (*ot & 0xFF000000) | ((u_long)p & 0x00FFFFFF)
    // i.e. the packet takes the bucket's current head as its `next` and becomes the new head, with
    // each word's top byte (the primitive length / the OT's own length) preserved on both sides.
    // The link word carries the packet's REAL PSX address in its low 24 bits, which is why this
    // goes through RamAddressOf.
    // JUSTIFICATION: C# language bridge — the object overload above cannot express this, because
    // both operands here are cursors inside byte[] draw buffers. Several already-ported renderers
    // open-code the identical splice at their emit sites (EffectMeshRender.AddPrimToBucket is that
    // open-coded form); this overload is for the callers that really `jal` the SDK routine, of
    // which FUN_800d3114 @0x800D3114 is the first.
    public static void AddPrim(byte[] otBuf, int otOffset, byte[] buf, int packetOffset)
    {
        uint pktWord = ReadU32(buf, packetOffset);
        uint otWord = ReadU32(otBuf, otOffset);
        WriteU32(buf, packetOffset, (pktWord & 0xff000000u) | (otWord & 0x00ffffffu));
        // PSX: the head word is re-read at 0x80077AE8 rather than reused.
        otWord = ReadU32(otBuf, otOffset);
        WriteU32(otBuf, otOffset,
            (otWord & 0xff000000u) | ((uint)RamAddressOf(buf, packetOffset) & 0x00ffffffu));
    }

    /**
     * @brief Collectively register primitives to the OT
     *
     * Registers primitives beginning with p0 and ending with p1 to the *ot entry
     * in the OT.
     *
     * @param ot OT entry
     * @param p0 Start address of primitive list
     * @param p1 End address of primitive list
     */
    public static void AddPrims(object ot, object p0, object p1)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Concatenate primitives
     *
     * @param p0 First primitive
     * @param p1 Second primitive
     */
    public static void CatPrim(object p0, object p1)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Draw ordering table
     *
     * @param p Pointer to ordering table
     */
    public static void DrawOTag(OT_TYPE p)
    {
        // Do nothing PSX SDK
    }

    // JUSTIFICATION: PSX hardware adaptation only — int-sentinel overload used when ported code
    // stores a raw PSX ordering-table address as int. What that sentinel selects, and how the
    // resulting frame is rasterized, is game-specific, so the game installs this bridge at
    // startup (see the game's PsxSdkBridges) and the overload below stays game-agnostic.
    // Not installed -> no-op, matching DrawOTag(OT_TYPE) above.
    public static Action<int> DrawOTagIntHandler;

    public static void DrawOTag(int otagBase)
    {
        DrawOTagIntHandler?.Invoke(otagBase);
    }

    /**
     * @brief Draw ordering table with I/O
     *
     * @param p Pointer to ordering table
     */
    public static void DrawOTagIO(OT_TYPE p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Draw ordering table with environment
     *
     * @param p Pointer to ordering table
     * @param env Drawing environment
     */
    public static void DrawOTagEnv(OT_TYPE p, DRAWENV env)
    {
        // Do nothing PSX SDK
    }

    // JUSTIFICATION: PSX hardware adaptation only — DrawOTagEnv is "PutDrawEnv(env) then draw the
    // ordering table starting at p": the SDK's two-in-one present call, used by
    // MainLoop.PresentFrameAndSwapBuffers. The byte[]+offset shape is a C# language bridge, since
    // the original computes the OT head as a raw pointer offset ((char *)otHead + offset).
    // Was a no-op in both halves.
    //
    // The PutDrawEnv half is exact: it programs the same GPU registers the int/DRAWENV overloads
    // do, including the isbg clear (DRAWENV_ARRAY_800bcdc8 both carry isbg=1, r0=g0=b0=0).
    //
    // CORRECTION 2026-07-28: the draw half was BLOCKED and walked without rasterizing, because a
    // 24-bit link could not name a buffer in this port's array-relative representation — so the
    // walk had to stay inside the ordering table and would have silently dropped every entity
    // primitive the moment UpdateAndRenderEntities was ported. Links are PSX addresses now (see the
    // RamRegion block), so this is the ordinary rasterize call it always should have been.
    public static void DrawOTagEnv(byte[] p, int offset, DRAWENV env)
    {
        ApplyDrawEnv(env);
        RasterizeOrderingTable(p, offset);
    }

    /**
     * @brief Draw primitive
     *
     * @param p Pointer to primitive
     */
    // JUSTIFICATION: PSX hardware adaptation only — real hardware submits ANY primitive object
    // (including DR_TPAGE "set current texture page" pseudo-primitives) to the GPU command FIFO in
    // call order; a DR_TPAGE changes GPU state for subsequently-processed primitives until the
    // next one. Dispatches into the same rasterizer and the same GPU state the ordering-table path
    // uses (RasterizeOrderingTable), so an immediate-mode primitive and an OT-linked one behave
    // identically — which is what the hardware does.
    //
    // CORRECTION 2026-07-27 (step 3 of docs/plan-unify-framebuffer-on-vram-2026-07-27.md): the
    // SPRT case used to be a hand-written special case matched on `LastTPage == 0x18 &&
    // clut == 0x7800`, sampling a separately-recorded copy of RunPublisherSplashSequence's texture
    // rather than VRAM, because this port had no VRAM to sample. It has now, and that sequence's
    // own two LoadImage calls put the texture where tpage 0x18 / clut 0x7800 resolve to — VRAM
    // (512,256) and (0,480) — so an ordinary textured rectangle reproduces it exactly.
    //
    // CORRECTION (2026-07-24, twice): the splash was first mis-decoded as the persistent
    // "[parasite eve]" swirl-logo background, and dispatch was removed on that theory. Both wrong:
    // the real background bug was RenderTargetUsage.DiscardContents wiping the host canvas.
    // Re-verified by actually rendering the decoded texture rather than assuming from raw memory
    // bytes: it is the "Published by Square Electronic Arts L.L.C." publisher splash, whose
    // 8-second fade-in/plateau/fade-out plays BEFORE the title loop's kind objects ever spawn.
    public static void DrawPrim(object p)
    {
        if (p is DR_TPAGE tpage)
        {
            s_gpuCurrentTPage = (ushort)((int)tpage.code[0] & 0x1ff);
            LastTPage = (int)tpage.code[0];
            return;
        }

        if (p is SPRT sprt)
        {
            uint bgr = (uint)(sprt.r0 | (sprt.g0 << 8) | (sprt.b0 << 16));
            FillTexturedRect(sprt.x0 + s_gpuDrawOffsetX, sprt.y0 + s_gpuDrawOffsetY, sprt.w, sprt.h,
                sprt.u0, sprt.v0, sprt.clut, s_gpuCurrentTPage, bgr, (sprt.code & 0x02) != 0);
        }

        // Do nothing PSX SDK — every other primitive type is an extension point for the future
        // full version, exactly as in RasterizePrimitivePacket.
    }

    /**
     * @brief Dump CLUT information
     *
     * @param clut CLUT ID
     */
    public static void DumpClut(ushort clut)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Dump display environment
     *
     * @param env Display environment
     */
    public static void DumpDispEnv(DISPENV env)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Dump drawing environment
     *
     * @param env Drawing environment
     */
    public static void DumpDrawEnv(DRAWENV env)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Dump ordering table
     *
     * @param p Pointer to ordering table
     */
    public static void DumpOTag(OT_TYPE p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Dump texture page information
     *
     * @param tpage Texture page ID
     */
    public static void DumpTPage(ushort tpage)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Load font pattern
     *
     * @param tx X position in frame buffer
     * @param ty Y position in frame buffer
     */
    public static void FntLoad(int tx, int ty)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set display mask
     *
     * @param mask 0: display off, 1: display on
     */
    // GHIDRA: SetDispMask @ 0x80074d28 — its trace string at 0x80011870 is "SetDispMask(%d)...".
    // ADDRESS ESTABLISHED 2026-08-02 from the function's OWN TRACE STRING, not from where it sits
    // between its neighbours: the psyq libgpu entry points open with a debug-level test
    // (`lbu 0x8009574E`, `sltiu $v0,$v0,2`) and, above level 1, call the trace hook at 0x80095748
    // with a format string naming themselves. Reading that string out of SLUS_006.62 identifies
    // the address outright.
    // The real body also dispatches through the GPU vector table at 0x80095744 (+0x10); the mask
    // has no desktop equivalent, so the no-op stands.
    public static void SetDispMask(int mask)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set drawing area primitive
     *
     * @param p Drawing area primitive
     * @param r Rectangle area
     */
    public static void SetDrawArea(DR_AREA p, RECT r)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set drawing environment primitive
     *
     * @param dr_env Drawing environment primitive
     * @param env Drawing environment
     */
    public static void SetDrawEnv(DR_ENV dr_env, DRAWENV env)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set load image primitive
     *
     * @param p Load image primitive
     * @param rect Rectangle area
     */
    public static void SetDrawLoad(DR_LOAD p, RECT rect)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set drawing mode primitive
     *
     * @param p Drawing mode primitive
     * @param dfe Drawing to display area flag
     * @param dtd Dithering flag
     * @param tpage Texture page
     * @param tw Texture window
     */
    public static void SetDrawMode(DR_MODE p, int dfe, int dtd, int tpage, RECT tw)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set texture page primitive
     *
     * @param p Texture page primitive
     * @param dfe Drawing to display area flag
     * @param dtd Dithering flag
     * @param tpage Texture page ID
     */
    // GHIDRA: SetDrawTPage @ 0x80077c84 — object form. Same eleven-instruction body as the
    // byte[] overload below; only the tag byte has nowhere to go in this port's DR_TPAGE class,
    // whose `tag` is a whole ulong rather than four bytes.
    public static void SetDrawTPage(DR_TPAGE p, int dfe, int dtd, int tpage)
    {
        p.tag = (p.tag & 0x00ffffffUL) | (1UL << 24);
        p.code[0] = ComposeDrawModeWord(dfe, dtd, tpage);
    }

    // GHIDRA: SetDrawTPage @ 0x80077c84 — PSX SDK, 27 static call sites, Ghidra's own name and
    // signature `void SetDrawTPage(DR_TPAGE *p, int dfe, int dtd, uint tpage)`.
    // The original is eleven instructions, read as raw MIPS at 0x80077C84-0x80077CAC, and does
    // THREE things:
    //     p->tag byte 3 = 1                       (one code word follows the tag)
    //     code = (dtd ? 0xE1000200 : 0xE1000000) | (tpage & 0x9FF) | (dfe ? 0x400 : 0)
    //     p->code[0] = code
    // Both `beq` are "skip the ori when the flag is ZERO", so dtd selects 0x200 and dfe 0x400.
    // FIXED 2026-08-03, and it was a real defect MEASURED rather than reasoned about: until now both
    // overloads wrote only the RAW tpage value — no GP0(0xE1) command byte, no 0x9FF mask, no tag
    // length, `dfe`/`dtd` ignored. RasterizePrimitivePacket dispatches draw-mode packets on
    // `cmd == 0xE1`, i.e. on byte +7, which for a bare tpage is 0 — so every packet these two built
    // was silently skipped, while TextSystem.WriteDrawTPage (which composes the word by hand) worked.
    // The FUN_800d1384 @0x800D1384 differential test had the original write 0xE1000020 where the
    // port wrote 0x20, on every trial that took the blended path.
    // BEHAVIOUR CHANGE, intended: fourteen call sites now emit draw-mode packets the rasterizer
    // honours. What they select is the tpage each caller already computed, so any visible difference
    // is that tpage finally taking effect.
    // MEASURED 2026-08-10, and it no longer rests on reading alone. A probe that calls THESE
    // overloads (not a transcription of them) over tpages 0x000/0x00A/0x088/0x1FF/0x9FF/0x1234 x
    // dfe x dtd -- 24 cases on the byte[] form and 6 on the DR_TPAGE form -- confirms every packet
    // now carries cmd 0xE1 at byte +7 and tag length 1, which is what RasterizePrimitivePacket
    // dispatches on, and that the 0x9FF mask keeps bits 0..8 and 11 while dtd and dfe remain the
    // ONLY sources of 0x200 and 0x400. The contrast is the point: a bare tpage word has byte +7 == 0
    // and is skipped outright. (The first version of that probe asserted a tpage's own 0x200 should
    // survive the mask; it should not, and the code was right -- the probe was fixed, not this.)
    // (The commit that first reported this fix, c391939, landed only this rationale and not the code;
    // the code lands here.)
    // JUSTIFICATION: C# language bridge — byte[]+offset overload of the DR_TPAGE form above, for
    // callers that build primitives directly at a cursor inside a draw buffer rather than into a
    // standalone object (RenderMeshWireframe @0x8007041c).
    public static void SetDrawTPage(byte[] buf, int byteOffset, int dfe, int dtd, int tpage)
    {
        buf[byteOffset + 3] = 1;
        uint value = (uint)ComposeDrawModeWord(dfe, dtd, tpage);
        buf[byteOffset + 4] = (byte)value;
        buf[byteOffset + 5] = (byte)(value >> 8);
        buf[byteOffset + 6] = (byte)(value >> 16);
        buf[byteOffset + 7] = (byte)(value >> 24);
    }

    // JUSTIFICATION: C# language bridge only — the single command word both SetDrawTPage overloads
    // build, factored out so the two cannot drift apart. No control flow and no side effects.
    private static ulong ComposeDrawModeWord(int dfe, int dtd, int tpage)
    {
        uint code = dtd != 0 ? 0xE1000200u : 0xE1000000u;
        uint mode = (uint)tpage & 0x9ff;
        if (dfe != 0)
        {
            mode = mode | 0x400;
        }
        return code | mode;
    }

    /**
     * @brief Set move image primitive
     *
     * @param p Move image primitive
     * @param rect Source rectangle
     * @param x Destination X coordinate
     * @param y Destination Y coordinate
     */
    public static void SetDrawMove(DR_MOVE p, RECT rect, int x, int y)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set drawing offset primitive
     *
     * @param p Drawing offset primitive
     * @param ofs Offset values [X, Y]
     */
    public static void SetDrawOffset(DR_OFFSET p, ushort ofs)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set debug font stream ID
     *
     * @param id Stream ID
     */
    public static void SetDumpFnt(int id)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded line primitive (2 vertices)
     *
     * @param p Line primitive
     */
    public static void SetLineF2(LINE_F2 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded line primitive (3 vertices)
     *
     * @param p Line primitive
     */
    public static void SetLineF3(LINE_F3 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded line primitive (4 vertices)
     *
     * @param p Line primitive
     */
    public static void SetLineF4(LINE_F4 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded line primitive (2 vertices)
     *
     * @param p Line primitive
     */
    public static void SetLineG2(LINE_G2 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded line primitive (3 vertices)
     *
     * @param p Line primitive
     */
    public static void SetLineG3(LINE_G3 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded line primitive (4 vertices)
     *
     * @param p Line primitive
     */
    public static void SetLineG4(LINE_G4 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded triangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyF3(POLY_F3 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded quadrangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyF4(POLY_F4 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded, texture-mapped triangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyFT3(POLY_FT3 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize flat-shaded, texture-mapped quadrangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyFT4(POLY_FT4 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded triangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyG3(POLY_G3 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded quadrangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyG4(POLY_G4 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded, texture-mapped triangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyGT3(POLY_GT3 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize Gouraud-shaded, texture-mapped quadrangle primitive
     *
     * @param p Polygon primitive
     */
    public static void SetPolyGT4(POLY_GT4 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set semi-transparency attribute
     *
     * @param p Primitive
     * @param abe Semi-transparency flag (0: off, 1: on)
     */
    public static void SetSemiTrans(object p, int abe)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set texture shading attribute
     *
     * @param p Primitive
     * @param tge Texture shading flag (0: texture off, 1: texture and shade on)
     */
    public static void SetShadeTex(object p, int tge)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize sprite primitive
     *
     * @param p Sprite primitive
     */
    public static void SetSprt(SPRT p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize 16x16 sprite primitive
     *
     * @param p Sprite primitive
     */
    public static void SetSprt16(SPRT_16 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize 8x8 sprite primitive
     *
     * @param p Sprite primitive
     */
    public static void SetSprt8(SPRT_8 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Set texture window primitive
     *
     * @param p Texture window primitive
     * @param tw Texture window rectangle
     */
    public static void SetTexWindow(DR_TWIN p, RECT tw)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize tile primitive
     *
     * @param p Tile primitive
     */
    public static void SetTile(TILE p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize 1x1 tile primitive
     *
     * @param p Tile primitive
     */
    public static void SetTile1(TILE_1 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize 16x16 tile primitive
     *
     * @param p Tile primitive
     */
    public static void SetTile16(TILE_16 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Initialize 8x8 tile primitive
     *
     * @param p Tile primitive
     */
    public static void SetTile8(TILE_8 p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Terminate primitive list
     *
     * @param p Primitive
     */
    public static void TermPrim(object p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Reset graphics system
     *
     * @param mode Reset mode (0: complete reset, 1: cancels only reset of drawing
     * engine, 3: reset without video mode change)
     * @return Previous video mode
     */
    public static int ResetGraph(int mode)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Set graphics debug level
     *
     * @param level Debug level
     * @return Previous debug level
     */
    public static int SetGraphDebug(int level)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Set graphics reverse mode
     *
     * @param mode Reverse mode
     * @return Previous mode
     */
    public static int SetGraphReverse(int mode)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Set graphics queue mode
     *
     * @param mode Queue mode
     * @return Previous mode
     */
    public static int SetGraphQueue(int mode)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Set drawing completion callback
     *
     * @param func Callback function
     * @return Previous callback function
     */
    //public static ulong DrawSyncCallback(void (* func)())
    //{
    //    // Do nothing PSX SDK
    //}

    /**
     * @brief Print formatted text to debug font stream
     *
     * @param fmt Format string
     * @return Number of characters printed
     */
    //public static int FntPrint(const char* fmt, ...)
    //{
    //    // Do nothing PSX SDK
    //}

    /**
     * @brief Check primitive validity
     *
     * @param s Debug message string
     * @param p Primitive
     * @return 1 if valid, 0 if invalid
     */
    public static int CheckPrim(char[] s, OT_TYPE p)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Clear frame buffer rectangle
     *
     * @param rect Rectangle area
     * @param r Red component
     * @param g Green component
     * @param b Blue component
     * @return 1 on success
     */
    // JUSTIFICATION: PSX hardware adaptation only — real VRAM fill. Was a no-op stub, which was
    // invisible while the port had no VRAM to fill; with the rasterizer and the presenter both on
    // `Vram` it is a real frame-buffer operation. Unlike PutDrawEnv's isbg clear this ignores the
    // drawing area and the drawing offset — ClearImage addresses VRAM directly (it is a GPU
    // fill-rectangle command, not a draw), which is why the menu's own
    // `ClearImage({0,0,0x140,0x1e0})` can blank both 15-bit draw buffers in one call.
    public static int ClearImage(RECT rect, byte r, byte g, byte b)
    {
        if (rect == null || rect.w <= 0 || rect.h <= 0)
        {
            return 0;
        }

        ushort fill = (ushort)((r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10));
        for (int row = 0; row < rect.h; row++)
        {
            int vy = (rect.y + row) & 0x1ff;
            for (int col = 0; col < rect.w; col++)
            {
                Vram[vy * 1024 + ((rect.x + col) & 0x3ff)] = fill;
            }
        }

        return 1;
    }

    /**
     * @brief Wait for drawing to finish
     *
     * @param mode 0: wait for completion, 1: return immediately
     * @return 0 if drawing complete, positive if drawing in progress
     */
    // GHIDRA: DrawSync @ 0x80074dc0 — its trace string at 0x80011884 is "DrawSync(%d)...".
    // ADDRESS ESTABLISHED 2026-08-02 from the function's OWN TRACE STRING, not from where it sits
    // between its neighbours: the psyq libgpu entry points open with a debug-level test
    // (`lbu 0x8009574E`, `sltiu $v0,$v0,2`) and, above level 1, call the trace hook at 0x80095748
    // with a format string naming themselves. Reading that string out of SLUS_006.62 identifies
    // the address outright.
    // The original waits on the GPU vector table's +0x3C entry; this port rasterizes synchronously,
    // so there is never anything outstanding to wait for.
    public static int DrawSync(int mode)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Open debug font stream
     *
     * @param x X position
     * @param y Y position
     * @param w Width
     * @param h Height
     * @param isbg Background clear flag
     * @param n Maximum characters
     * @return Stream ID, or -1 on error
     */
    public static int FntOpen(int x, int y, int w, int h, int isbg, int n)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Get graphics debug level
     *
     * @return Current debug level
     */
    public static int GetGraphDebug()
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Flush debug font stream
     *
     * @param id Stream ID
     * @return Pointer to primitive, or NULL if buffer empty
     */
    public static ulong[] FntFlush(int id)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Open kanji font stream
     *
     * @param x X position
     * @param y Y position
     * @param w Width
     * @param h Height
     * @param dx Character width
     * @param dy Character height
     * @param cx Columns
     * @param cy Rows
     * @param isbg Background clear flag
     * @param n Maximum characters
     * @return Stream ID, or -1 on error
     */
    public static int KanjiFntOpen(int x, int y, int w, int h, int dx, int dy, int cx, int cy, int isbg, int n)
    {
        return 0;
    }

    /**
     * @brief Load image from memory to frame buffer
     *
     * @param rect Destination rectangle in frame buffer
     * @param p Pointer to image data
     * @return 1 on success
     */
    // GHIDRA: LoadImage @ 0x8007506c — it passes the literal string "LoadImage" (0x800118D4) to
    // the RECT validator at 0x80074E28, which is the "%s:bad RECT" reporter at 0x80011898.
    // ADDRESS ESTABLISHED 2026-08-02 from the function's OWN TRACE STRING, not from where it sits
    // between its neighbours: the psyq libgpu entry points open with a debug-level test
    // (`lbu 0x8009574E`, `sltiu $v0,$v0,2`) and, above level 1, call the trace hook at 0x80095748
    // with a format string naming themselves. Reading that string out of SLUS_006.62 identifies
    // the address outright.
    // The real body then dispatches through the GPU vector table's +0x20 entry with size 8.
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the u_long* form of LoadImage, used when the source is a local buffer rather than a
    // PSX address — FUN_80038228 @ 0x80038228 builds its 2x1 fade texture on the stack that way.
    // It was a stub, so such a call configured its primitive and then uploaded nothing.
    //
    // A PSX u_long is 32 bits while a C# ulong is 64, so this cannot be a byte-for-byte mirror.
    // The convention is that each element carries one PSX word in its low 32 bits, which is what a
    // transliterated call site writing a 0x1111FFFF word produces. Everything past that point
    // goes through the ordinary byte[] path, so the VRAM write is identical.
    public static int LoadImage(RECT rect, ulong[] p)
    {
        if (rect == null || rect.w <= 0 || rect.h <= 0 || p == null)
        {
            return 0;
        }

        byte[] source = new byte[p.Length * 4];
        for (int i = 0; i < p.Length; i++)
        {
            uint word = (uint)p[i];
            source[(i * 4) + 0] = (byte)word;
            source[(i * 4) + 1] = (byte)(word >> 8);
            source[(i * 4) + 2] = (byte)(word >> 16);
            source[(i * 4) + 3] = (byte)(word >> 24);
        }

        LoadImage(rect, source, 0);
        return 1;
    }

    // JUSTIFICATION: PSX hardware adaptation only — resolves a raw PSX RAM address (stored as int)
    // to the byte[] the port uses to model that span of PSX RAM, as (buffer, offset), or null when
    // the address maps to nothing. Which byte[] models which span is game-specific, so the game
    // installs this at startup (see the game's PsxSdkBridges).
    // This is now a thin delegating property over the shared PsxRam.AddressResolver hook (see
    // PsxRam.cs) so LibGpu and other SDK modules (LibCd's St* ring API) resolve PSX addresses
    // through one installed mapping. Existing game wiring (`LibGpu.RamAddressResolver = ...`)
    // keeps working unchanged — it now just writes through to PsxRam.AddressResolver.
    public static Func<int, (byte[] buffer, int offset)?> RamAddressResolver
    {
        get => PsxRam.AddressResolver;
        set => PsxRam.AddressResolver = value;
    }

    // JUSTIFICATION: PSX hardware adaptation only — int overload used when the source pointer is
    // stored as a raw PSX address in an int global. Resolves that address through
    // RamAddressResolver and then takes the ordinary byte[] path.
    // Call sites whose real arguments were never reconstructed still pass an empty RECT and 0;
    // both are rejected here, so they stay the no-ops they were.
    public static int LoadImage(RECT rect, int p)
    {
        if (rect == null || rect.w <= 0 || rect.h <= 0 || p == 0)
        {
            return 0;
        }

        var resolved = RamAddressResolver?.Invoke(p);
        if (resolved == null)
        {
            return 0;
        }

        LoadImage(rect, resolved.Value.buffer, resolved.Value.offset);
        return 1;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the PSX GPU's 1024x512 16-bit-per-cell frame
    // buffer. This is the port's ONE representation of it: LoadImage/MoveImage/ClearImage DMA into
    // it, the software rasterizer draws into it (RasterizeOrderingTable), the texture sampler reads
    // it (SampleTexel), and the host presents a window of it (ReadDisplayRgb24).
    // Until step 3 of docs/plan-unify-framebuffer-on-vram-2026-07-27.md there were three further
    // RGB24 stand-ins for regions of this same memory — LastImageRect/Pixels/Valid (LoadImage's
    // damage rect), PendingSprite* (the publisher splash) and MenuFramebuffer* (the rasterizer's
    // target) — each composited by the host in an order that had nothing to do with the order the
    // writes actually happened in. They are gone; nothing stands in for VRAM any more.
    public static ushort[] Vram = new ushort[1024 * 512];

    private static void WriteVramRect(RECT rect, byte[] source, int sourceOffset)
    {
        int neededBytes = rect.w * rect.h * 2;
        if (sourceOffset < 0 || neededBytes <= 0 || sourceOffset + neededBytes > source.Length)
        {
            return;
        }

        for (int row = 0; row < rect.h; row++)
        {
            int vy = (rect.y + row) & 0x1ff;
            int srcRowOffset = sourceOffset + row * rect.w * 2;
            for (int col = 0; col < rect.w; col++)
            {
                int vx = (rect.x + col) & 0x3ff;
                ushort word = (ushort)(source[srcRowOffset + col * 2] | (source[srcRowOffset + col * 2 + 1] << 8));
                Vram[vy * 1024 + vx] = word;
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — the real LoadImage (0x8007506c) DMAs raw
    // 16-bit-per-cell data into VRAM at the given rect, whatever bit depth the destination texpage
    // implies. `rect.w` is therefore always a count of VRAM HALFWORDS, not of pixels — 24-bit
    // sources pass 3 halfwords per 2 pixels (see FUN_8018f2f4_PrimeDoubleBufferedDisplay's
    // background rect and Func_801909b4's damage rect, both scaled *3/2 for exactly that reason).
    public static void LoadImage(RECT rect, byte[] source, int sourceOffset)
    {
        if (rect == null || rect.w <= 0 || rect.h <= 0 || source == null)
        {
            return;
        }

        WriteVramRect(rect, source, sourceOffset);
    }

    // JUSTIFICATION: PSX hardware adaptation only — minimal render-backend bridge (Blocage 4
    // JUSTIFICATION: PSX hardware adaptation only — minimal render-backend bridge. Real hardware
    // tracks "current texture page" as GPU state, updated whenever a DR_TPAGE primitive is
    // submitted via DrawPrim (SetDrawTPage itself only prepares the primitive object in memory —
    // matches PSX SDK semantics: the GPU state doesn't change until DrawPrim actually submits it).
    // Kept alongside s_gpuCurrentTPage, which DrawPrim now also updates, because callers outside
    // this file read it.
    public static int LastTPage;

    // =========================================================================
    // PSX main-RAM address space (ordering-table link resolution)
    // =========================================================================
    // JUSTIFICATION: PSX hardware adaptation only — on the console the ordering table and every
    // primitive packet live in ONE address space, so the 24-bit value addPrim() stores
    // (GHIDRA: AddPrim @ 0x80077ac4 — `*p2 = *p2 & 0xff000000 | *p1 & 0xffffff;
    // *p1 = *p1 & 0xff000000 | (uint)p2 & 0xffffff;`) names its target unambiguously: it is the low
    // 24 bits of a main-RAM address, and every buffer that can carry a primitive lives inside
    // 0x80000000..0x80ffffff, so the top byte is redundant and 24 bits are enough.
    // This port keeps that RAM as a set of independent managed byte[] objects and used to store an
    // ARRAY-RELATIVE offset in the link instead, which cannot say WHICH array the offset belongs
    // to. Three things were being paid for because of it:
    //   * RasterizeOrderingTable needed an `atOtHead` flag — read the first hop out of the ordering
    //     table, every hop after it out of one hard-coded primitive buffer. Correct only while a
    //     single emitter links into a single slot;
    //   * ClearOTagR(int, int) had to stay a no-op, because a pre-linked table feeds that walker
    //     ordering-table offsets it then reads as primitive-buffer offsets (measured: 2.15% of the
    //     Select Slot screen turned to garbage);
    //   * DrawOTagEnv could not follow a link out of the ordering table at all.
    // The two routines below restore the missing half: every buffer that can hold an ordering table
    // or a primitive packet declares the PSX address it stands for at its own declaration (see the
    // LibGpu.RamRegion calls in Remaster/StaticVariables.cs), so a link can be turned back into
    // (array, offset) exactly as the GPU turns it back into a RAM pointer. Link VALUES are now the
    // hardware's own. That also retires the "offset 0 collides with the end-of-chain terminator"
    // hazard this port had to work around in two places (TextSystem.UiPrimitivePoolBase and
    // ClearOTagR's entry 1): no PSX RAM address is 0.
    private static byte[][] s_ramRegionBuffer = new byte[64][];
    private static int[] s_ramRegionBase = new int[64];
    private static int s_ramRegionCount;

    // JUSTIFICATION: PSX hardware adaptation only — allocates a buffer AND records the PSX address
    // it stands for, so `new byte[n]` at a declaration becomes `RamRegion(addr, n)`.
    public static byte[] RamRegion(int psxAddress, int lengthBytes)
    {
        return RamRegion(psxAddress, new byte[lengthBytes]);
    }

    // JUSTIFICATION: PSX hardware adaptation only — records the PSX address an already-allocated
    // buffer stands for. Re-registering the same buffer object updates its address rather than
    // adding a second row, so an alias assigned later (InitPeImgBufferPointers) cannot duplicate it.
    public static byte[] RamRegion(int psxAddress, byte[] buffer)
    {
        if (buffer == null)
        {
            return null;
        }

        for (int i = 0; i < s_ramRegionCount; i++)
        {
            if (ReferenceEquals(s_ramRegionBuffer[i], buffer))
            {
                s_ramRegionBase[i] = psxAddress;
                return buffer;
            }
        }

        if (s_ramRegionCount < s_ramRegionBuffer.Length)
        {
            s_ramRegionBuffer[s_ramRegionCount] = buffer;
            s_ramRegionBase[s_ramRegionCount] = psxAddress;
            s_ramRegionCount++;
        }

        return buffer;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the PSX address of (buffer, offset), i.e. what
    // the original's `(uint)p2` already is. Returns 0 for a buffer that has not declared an
    // address; 0 is not a legal PSX RAM address, so callers use it as "not addressable".
    public static int RamAddressOf(byte[] buffer, int offset)
    {
        for (int i = 0; i < s_ramRegionCount; i++)
        {
            if (ReferenceEquals(s_ramRegionBuffer[i], buffer))
            {
                return s_ramRegionBase[i] + offset;
            }
        }

        return 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the inverse: turns a PSX address back into the
    // managed buffer holding it. Several of this port's buffers are declared LARGER than the gap to
    // the next symbol (they are CD staging areas sized for their real payload rather than for the
    // gap — see BYTE_ARRAY_801229a0), so an address can fall inside more than one declared extent.
    // The region with the HIGHEST base wins, which is the one the original's own symbol table would
    // name; the low-based neighbour is then just the overspill that the same physical RAM allows.
    public static bool RamResolve(int psxAddress, out byte[] buffer, out int offset)
    {
        int best = -1;
        for (int i = 0; i < s_ramRegionCount; i++)
        {
            int candidate = psxAddress - s_ramRegionBase[i];
            if (candidate < 0 || candidate >= s_ramRegionBuffer[i].Length)
            {
                continue;
            }

            if (best < 0 || s_ramRegionBase[i] > s_ramRegionBase[best])
            {
                best = i;
            }
        }

        if (best < 0)
        {
            buffer = null;
            offset = 0;
            return false;
        }

        buffer = s_ramRegionBuffer[best];
        offset = psxAddress - s_ramRegionBase[best];
        return true;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the link field of an ordering-table entry or a
    // primitive tag holds an address with its top byte dropped; main RAM is mirrored at
    // 0x80000000, so restoring that byte is the whole of the conversion.
    public static bool RamResolveLink(uint link, out byte[] buffer, out int offset)
    {
        return RamResolve(unchecked((int)(0x80000000u | (link & 0x00ffffff))), out buffer, out offset);
    }

    // =========================================================================
    // Minimal software GPU rasterizer (menu ordering-table pipeline)
    // =========================================================================
    // JUSTIFICATION: PSX hardware adaptation only — real DrawOTag/DrawOTagEnv walk the GPU's
    // ordering table and rasterize each linked primitive into VRAM; every overload above was a
    // no-op stub, which is why RenderUiBoxChrome's output (a real, correctly-built primitive+OT
    // chain, see TextSystem.TryAllocateUiPrimitive/AddUiPrimitive) never reached the screen. It
    // implements only the primitive shapes this port's writers actually emit today — DR_TPAGE
    // (draw-mode state), DR_TWIN (texture window, state only), DR_AREA (drawing area), TILE and
    // SPRT (flat / textured rectangles), and POLY_F3/POLY_FT4. Extension point for the future full
    // version: add cases to RasterizePrimitivePacket below (LINE_F2, POLY_G4, Gouraud shading) —
    // the OT-walking/dispatch shell does not need to change.
    //
    // CORRECTION 2026-07-27 (step 2 of docs/plan-unify-framebuffer-on-vram-2026-07-27.md): the
    // rasterizer used to target one small persistent RGB24 surface (MenuFramebufferPixels, 320x240)
    // standing in for "the active draw buffer", which forced a whole coordinate-space conversion
    // (MenuDrawOriginX/Y) and made the composite ORDER between a LoadImage and a rasterized
    // primitive — rather than the order the writes actually happened in — decide what covered what.
    // It now writes real `Vram` cells, in absolute VRAM coordinates, exactly like the hardware.
    //
    // JUSTIFICATION: PSX hardware adaptation only — walks a PSX-style ordering table: a singly
    // linked chain of nodes, each node's first 4-byte word storing a tag LENGTH byte (top byte: how
    // many 32-bit words of primitive data follow the tag) and a 24-bit link to the next node (see
    // TextSystem.AddPrimitiveToOrderingTable, which builds this exact chain by inserting each new
    // primitive at the head, so walking from `tailOffset` visits primitives newest-first — and that
    // walk order IS the real GPU's draw order; see the CORRECTION on the rasterize loop below).
    //
    // CORRECTION 2026-07-28: this used to carry a `bool atOtHead` — read the first hop out of
    // `orderingTable`, every hop after it out of a single `primitiveBuffer` parameter. Both are
    // gone: a link is now a PSX RAM address (see the RamRegion block above), so the walk runs
    // through ONE memory and the emitter's identity no longer has to be guessed. That is what makes
    // a pre-linked ClearOTagR table and DrawOTagEnv's escape into the packet stream possible.
    //
    // The tag length byte is now honoured too, and it is the second half of the same fix: an
    // ordering-table ENTRY carries length 0 (it is a pure link node, nothing to draw) while every
    // primitive carries its own word count (1 for DR_TPAGE, 3 for TILE, 4 for SPRT/POLY_F3,
    // 9 for POLY_FT4 — see TextSystem's Write* helpers and CombatHud.SetPrimTag). Ignoring it is
    // exactly why enabling ClearOTagR used to rasterize link words as primitives.
    public static void RasterizeOrderingTable(byte[] orderingTable, int tailOffset)
    {
        if (orderingTable == null || tailOffset < 0 || tailOffset + 4 > orderingTable.Length)
        {
            return;
        }

        const uint Low24Mask = 0x00ffffff;
        var visitOrder = new System.Collections.Generic.List<byte[]>();
        var visitOffset = new System.Collections.Generic.List<int>();

        uint link = ReadU32(orderingTable, tailOffset) & Low24Mask;
        int safety = 0;

        // A cleared frontend table is 0x1000 entries long and the walk visits every one of them
        // before reaching the terminator, so the cap has to clear that plus the frame's primitives.
        while (link != 0 && link != Low24Mask && safety++ < 0x4000)
        {
            // A link that does not resolve ends the walk, and it does so SILENTLY — everything
            // chained behind it is dropped. That is how this port went without a screen fade for
            // months: g_screenFadeDrawModePrimitives was a plain C# object with no declared address,
            // so the walk broke on 0x800bcfd8 before it ever reached the fade tile behind it.
            // When a primitive that should be on screen is missing, log `link` here first.
            if (!RamResolveLink(link, out byte[] node, out int nodeOffset) ||
                nodeOffset + 4 > node.Length)
            {
                break;
            }

            uint word = ReadU32(node, nodeOffset);
            if ((word >> 24) != 0)
            {
                visitOrder.Add(node);
                visitOffset.Add(nodeOffset);
            }

            link = word & Low24Mask;
        }

        // JUSTIFICATION: PSX hardware adaptation — real GPU state (current texture page) persists
        // across the whole primitive stream and is only changed by a DR_TPAGE command; SPRT/TILE
        // packets themselves carry no tpage field (see TextSystem.WriteSprite), they rely on
        // whatever DR_TPAGE most recently preceded them in draw order.
        // CORRECTION 2026-07-27: this was a per-call local reset to 0. On real hardware GP0(0xE1) is
        // persistent GPU state with no frame boundary at all, so it is hoisted to a static here.
        // (With the draw order fixed below it no longer decides anything visible, since every SPRT
        // is now immediately preceded by its own DR_TPAGE, but the state is still modelled as the
        // hardware models it.)
        //
        // CORRECTION 2026-07-27: the loop used to run `for (i = Count-1; i >= 0; i--)`, i.e.
        // oldest-added first, on the stated assumption that "later UI draws correctly layer on top
        // of earlier ones". That is backwards: the real GPU follows the chain from the entry given
        // to DrawOTag, and AddPrimitiveToOrderingTable inserts at the HEAD, so the hardware draws
        // the most recently added primitive FIRST. Two independent pieces of the original prove it:
        //   * DrawUiSprite (Ghidra 0x8005eb64) adds its SPRT and then its DR_TPAGE; only newest-
        //     first execution puts the DR_TPAGE before the sprite it belongs to. With the reversed
        //     order every SPRT sampled the tpage of the PREVIOUS sprite instead of its own, which
        //     is why the memory-card slot rows rendered as noise or vanished depending on what had
        //     been drawn before them.
        //   * RenderUiTypeOneFrames (0x80062fec) calls DispatchUiTreeCallbacks (a window's CONTENTS)
        //     before RenderUiBoxChrome (its FRAME) — which only composites correctly if the
        //     later-added chrome is drawn underneath, i.e. newest-first.
        // `visitOrder` is already collected head-to-tail (newest to oldest), so the real order is
        // simply the walk order.
        for (int i = 0; i < visitOrder.Count; i++)
        {
            RasterizePrimitivePacket(visitOrder[i], visitOffset[i]);
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — the GP0(0xE1) draw-mode register's texture-page
    // field, which is persistent GPU state (see RasterizeOrderingTable's remarks above).
    private static ushort s_gpuCurrentTPage;
    // GP0(0xE1) bits 5-6: the semi-transparency RATE (ABR). Persistent GPU state like the tpage
    // above and latched from the same word. See PlotPixel for the four rates and the evidence.
    private static int s_gpuSemiTransRate;
    // GP0(0xE1) bit 9: dither 24bit-to-15bit. Persistent GPU state, NOT part of the 9-bit tpage
    // field above — which is why every latch of s_gpuCurrentTPage masks with 0x1ff and this is a
    // separate flag. The game really does drive it both ways: SLUS_006_62 (0x800152dc region) sets
    // DRAWENV.dtd = 1 on BOTH draw buffers at boot, and individual DR_TPAGE packets then toggle it
    // per primitive group — TextSystem's dialogue/cursor/overlay pages and CombatHud pass dtd = 1,
    // while the UI border/fill paths (TextSystem RenderUiBorderPath, MenuSystem RenderUiFillTile)
    // and the Section 70 splash pass dtd = 0.
    // Initial value is the hardware's reset state (off), not a guess: the game turns it on itself
    // through PutDrawEnv, which MainLoop calls once per frame with one of those two DRAWENVs.
    private static bool s_gpuDither;

    // JUSTIFICATION: PSX hardware adaptation only — GP0(0xE2), the TEXTURE WINDOW, persistent state
    // in the same family as the tpage/dither registers above.
    // Four 5-bit fields, all in units of 8 texels: mask X (bits 0-4), mask Y (5-9), offset X
    // (10-14), offset Y (15-19). Sampling rewrites each coordinate as
    //   u = (u & ~(maskX * 8)) | ((offsetX & maskX) * 8)
    // which is what makes a sprite REPEAT a small patch instead of walking across the page.
    // Modelled 2026-08-01 because PE's window fills need it: RenderUiBoxChrome fills a window with
    // ONE sprite the size of the box wrapped in a 32x32 window, i.e. a patch meant to tile. The
    // encoding was taken from the CONSOLE, not assumed — the original emits 0xE200039C for the
    // "No Save Data" dialogue's fill (see WriteTexWindow's remarks).
    // Mask 0 is the register's reset state and makes the formula the identity, so any primitive that
    // does not arm a window samples exactly as it did before this existed.
    private static int s_gpuTexWindowMaskX;
    private static int s_gpuTexWindowMaskY;
    private static int s_gpuTexWindowOffsetX;
    private static int s_gpuTexWindowOffsetY;

    // JUSTIFICATION: PSX hardware adaptation only — the rest of the GPU's persistent drawing
    // registers, all of them programmed by PutDrawEnv from a DRAWENV and overridable mid-stream by
    // the matching GP0 packets:
    //   * GP0(0xE3)/GP0(0xE4) drawing area — inclusive top-left / bottom-right, ABSOLUTE VRAM;
    //   * GP0(0xE5) drawing offset — added by the GPU to every primitive vertex, i.e. the VRAM
    //     origin of the draw buffer the DRAWENV selects ((0,0) and (0,224) for the menu's two).
    // The game's own primitive coordinates are buffer-LOCAL (g_uiDrawX/g_uiDrawY are 0..320 /
    // 0..224 regardless of which buffer is active) while the rects it builds by hand are not:
    // SetupUiWindowDisplay and RenderUiWindowDrawArea (MenuSystem.cs) both add 0xe0 to their DR_AREA
    // rect's y when g_menuDrawBufIdx != 0. Adding the offset to vertices — as the hardware does —
    // brings both into the one absolute VRAM space, which is what removed this port's
    // MenuDrawOriginX/Y back-translation of the DR_AREA rects (and with it the 30 Hz flicker that
    // came from clipping every second frame away entirely).
    private static short s_gpuClipX0;
    private static short s_gpuClipY0;
    private static short s_gpuClipX1 = 1023;
    private static short s_gpuClipY1 = 511;
    private static short s_gpuDrawOffsetX;
    private static short s_gpuDrawOffsetY;

    // JUSTIFICATION: PSX hardware adaptation only — interprets one GPU primitive packet using the
    // same [color(3)][cmd(1)][...] layout TextSystem's WriteTile/WriteSprite/WritePolyF3/
    // WriteDrawTPage/WriteTexWindow/WriteDrawArea helpers already write (cmd byte's top 3 bits
    // select the real PSX GPU command category: 0x20=polygon, 0x60=rectangle/sprite; 0xE1/0xE2/0xE3
    // are the GP0 draw-mode/tex-window/drawing-area setup commands, distinguishable at the same
    // byte offset since every packet here is [tag(4)][cmd-word(4)...]).
    // JUSTIFICATION: PSX hardware adaptation only — the three colour bytes of a packet's
    // `r,g,b,code` word. The fourth byte is `code`/`pad` and is never part of the colour.
    private static uint ReadBgr(byte[] buf, int at)
    {
        return (uint)(buf[at] | (buf[at + 1] << 8) | (buf[at + 2] << 16));
    }

    private static void RasterizePrimitivePacket(byte[] buf, int offset)
    {
        if (offset + 8 > buf.Length)
        {
            return;
        }

        byte cmd = buf[offset + 7];
        if (cmd == 0xE1)
        {
            // DR_TPAGE: GP0(0xE1) draw-mode setting. TextSystem.WriteDrawTPage packs the tpage
            // value (same bit layout as GetTPage) into the low 9 bits of the code word at +4.
            uint drawMode = ReadU32(buf, offset + 4);
            s_gpuCurrentTPage = (ushort)(drawMode & 0x1ff);
            s_gpuSemiTransRate = (s_gpuCurrentTPage >> 5) & 3;
            // Bit 9 = dither, bit 10 = draw-to-display-area. Both live OUTSIDE the 9-bit tpage
            // field, and both were being masked away here. TextSystem.WriteDrawTPage packs them at
            // exactly 0x200/0x400, so the dtd argument its callers already pass had no effect at all.
            s_gpuDither = (drawMode & 0x200) != 0;

            // JUSTIFICATION: PSX hardware adaptation only — a MERGED packet (PsyQ MargePrim,
            // 0x80077cb4) is ONE ordering-table node holding several GP0 commands back to back, and
            // real DMA submits every word the tag's LENGTH byte covers. This dispatcher handles one
            // command per call, so a merge used to execute the DR_TPAGE and silently drop whatever
            // followed it — which is why no dialogue glyph and no dialogue backdrop bar has ever
            // been drawn (TextSystem builds every glyph as DR_TPAGE + SPRT merged into one packet).
            // MargePrim zeroes the absorbed primitive's tag word, so the sibling still begins at a
            // tag exactly one DR_TPAGE (2 words) later and can be dispatched as an ordinary packet.
            // PARTIAL, stated rather than hidden: only this DR_TPAGE + one-primitive shape is
            // modelled, because it is the only one the game builds — all five merge sites
            // (TextSystem's glyph/cursor/overlay pairs, CombatHud's InitTPageSprtPrim /
            // InitTPageTilePrim) pair a DR_TPAGE with exactly one following primitive.
            if (buf[offset + 3] > 1)
            {
                RasterizePrimitivePacket(buf, offset + 8);
            }

            return;
        }

        if (cmd == 0xE2)
        {
            // The COMMAND word is at offset + 4, not at offset: these are 8-byte packets whose first
            // word is the tag (the 0xE3 case below reads offset + 4 for the same reason). Reading
            // the tag instead decodes 0x01000000, i.e. an all-zero window that silently does
            // nothing — which is exactly how a first attempt at this looked correct and was not.
            if (offset + 8 > buf.Length)
            {
                return;
            }

            uint tw = ReadU32(buf, offset + 4) & 0xfffff;
            s_gpuTexWindowMaskX = (int)(tw & 0x1f);
            s_gpuTexWindowMaskY = (int)((tw >> 5) & 0x1f);
            s_gpuTexWindowOffsetX = (int)((tw >> 10) & 0x1f);
            s_gpuTexWindowOffsetY = (int)((tw >> 15) & 0x1f);
            return;
        }

        if (cmd == 0xE3)
        {
            if (offset + 12 > buf.Length)
            {
                return;
            }

            uint topLeft = ReadU32(buf, offset + 4);
            uint bottomRight = ReadU32(buf, offset + 8);
            // GP0(0xE3/0xE4) carry ABSOLUTE VRAM coordinates, and so does `Vram` — the two spaces
            // coincide now, so the packet is taken as-is. (It used to be translated back into a
            // draw-buffer-local surface; see the CORRECTION on RasterizeOrderingTable.)
            s_gpuClipX0 = (short)(topLeft & 0x3ff);
            s_gpuClipY0 = (short)((topLeft >> 10) & 0x1ff);
            s_gpuClipX1 = (short)(bottomRight & 0x3ff);
            s_gpuClipY1 = (short)((bottomRight >> 10) & 0x1ff);
            return;
        }

        uint bgr = (uint)(buf[offset + 4] | (buf[offset + 5] << 8) | (buf[offset + 6] << 16));
        bool semiTransparent = (cmd & 0x02) != 0;
        byte category = (byte)(cmd & 0xE0);
        if (category == 0x60)
        {
            // Real PSX GPU command encoding for this category: bit2 (0x04) = textured (SPRT vs
            // TILE), bit1 (0x02) = semi-transparency (already read above as `semiTransparent`), and
            // bits 4-3 = the rectangle SIZE selector: 00 = variable (a w/h word follows in the
            // packet), 01 = 1x1, 10 = 8x8, 11 = 16x16, in which case NO w/h word is present and the
            // packet is 4 bytes shorter.
            // CORRECTION 2026-07-28: the size bits were ignored and w/h were always read from the
            // packet. That is right for SPRT/TILE (codes 0x64/0x60) and wrong for every fixed-size
            // rectangle — notably SPRT_16, code 0x7c, which is what the whole frontend page is built
            // from (InitFrontendSpriteEntryPrims writes tag length 3 = 16 bytes total). Those reads
            // landed on the NEXT packet's bytes, so every page tile got a garbage width and height.
            bool textured = (cmd & 0x04) != 0;
            int sizeSelector = (cmd >> 3) & 3;
            int minLen = sizeSelector == 0 ? (textured ? 20 : 16) : (textured ? 16 : 12);
            if (offset + minLen > buf.Length)
            {
                return;
            }

            // GP0(0xE5): the GPU adds the drawing offset to every primitive vertex.
            int x = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
            int y = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
            short fixedSize = sizeSelector == 1 ? (short)1 : (sizeSelector == 2 ? (short)8 : (short)16);
            if (textured)
            {
                byte u0 = buf[offset + 12];
                byte v0 = buf[offset + 13];
                ushort clut = (ushort)(buf[offset + 14] | (buf[offset + 15] << 8));
                short w = sizeSelector == 0 ? ReadI16(buf, offset + 16) : fixedSize;
                short h = sizeSelector == 0 ? ReadI16(buf, offset + 18) : fixedSize;
                FillTexturedRect(x, y, w, h, u0, v0, clut, s_gpuCurrentTPage, bgr, semiTransparent);
            }
            else
            {
                short w = sizeSelector == 0 ? ReadI16(buf, offset + 12) : fixedSize;
                short h = sizeSelector == 0 ? ReadI16(buf, offset + 14) : fixedSize;
                FillRect(x, y, w, h, bgr, semiTransparent);
            }
        }
        else if (category == 0x40)
        {
            // JUSTIFICATION: PSX hardware adaptation only — the GP0 LINE category, the last of the
            // three shapes this rasterizer's own remarks list as a missing extension point. It was
            // never exercised until RunCombatStateMachine landed: MeshWireframe is the port's only
            // LINE writer and it is reached solely through the attack-range dome, which is why the
            // dome emitted ~2000 bytes of correctly-linked primitives every frame and drew nothing.
            //
            // Real PSX encoding for this category: bit4 (0x10) = GOURAUD (a per-vertex colour word
            // precedes each vertex after the first), bit3 (0x08) = POLY-LINE (a variable-length
            // vertex run terminated by 0x55555555), bit1 (0x02) = semi-transparency, already decoded
            // above. That gives 0x40 LINE_F2, 0x48 LINE_F3+, 0x50 LINE_G2, 0x58 LINE_G3+.
            //
            // PARTIAL, stated rather than hidden: only the TWO-POINT forms are modelled, because they
            // are the only ones this port emits. MeshWireframe writes 0x50/0x52 — LINE_G2, 20 bytes,
            // tag length 4 — with r=0 and g=b equal, which is the dome's cyan. The poly-line bit is
            // handled by refusing to draw rather than by walking an unterminated run, so a future
            // writer that sets it fails visibly instead of corrupting the walk.
            //
            // Packet layout, taken from MeshWireframe's own stores:
            //   +0x04 r0,g0,b0   +0x07 code   +0x08 xy0
            //   LINE_G2: +0x0C r1,g1,b1   +0x0F pad   +0x10 xy1
            //   LINE_F2: +0x0C xy1
            bool gouraud = (cmd & 0x10) != 0;
            bool polyLine = (cmd & 0x08) != 0;
            if (!polyLine)
            {
                int lx0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
                int ly0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
                int secondVertex = gouraud ? offset + 0x10 : offset + 0x0c;
                if (secondVertex + 4 <= buf.Length)
                {
                    int lx1 = ReadI16(buf, secondVertex) + s_gpuDrawOffsetX;
                    int ly1 = ReadI16(buf, secondVertex + 2) + s_gpuDrawOffsetY;
                    byte r0 = buf[offset + 4], g0 = buf[offset + 5], b0 = buf[offset + 6];
                    byte r1 = r0, g1 = g0, b1 = b0;
                    if (gouraud && offset + 0x0f <= buf.Length)
                    {
                        r1 = buf[offset + 0x0c];
                        g1 = buf[offset + 0x0d];
                        b1 = buf[offset + 0x0e];
                    }
                    // `gouraud` doubles as the dither predicate — see DrawGouraudLine.
                    DrawGouraudLine(lx0, ly0, lx1, ly1, r0, g0, b0, r1, g1, b1, semiTransparent, gouraud);
                }
            }

            return;
        }
        else if (category == 0x20)
        {
            // Real PSX GPU command encoding for polygons: bit2 (0x04) = textured, bit3 (0x08) =
            // quad (4 vs 3 vertices), bit4 (0x10) = Gouraud shading. CORRECTION: this dispatch
            // previously tested bit0 (0x01) for "textured", which does not exist in the real PSX
            // opcode table (POLY_F3=0x20, POLY_FT3=0x24, POLY_F4=0x28, POLY_FT4=0x2C — the
            // textured/quad bits are 0x04/0x08). Harmless until now because the quad check alone
            // already forced every quad (textured or not) to skip, and no textured triangle
            // (POLY_FT3) is emitted anywhere in this codebase yet.
            bool textured = (cmd & 0x04) != 0;
            bool quad = (cmd & 0x08) != 0;
            bool gouraud = (cmd & 0x10) != 0;

            // GOURAUD FORMS ADDED 2026-07-28 — this is what the entity models are made of, and
            // until now none of them drew correctly. `ParseTMDModel` stamps exactly four codes into
            // the packets it builds: GT4 0x3C/0x3E, GT3 0x34/0x36, G4 0x38/0x3A, G3 0x30/0x32.
            // Before this, GT4 fell into the FT4 arm below and was decoded with FT4's 40-byte
            // layout (it is 52), so every vertex and UV after the first was read from the wrong
            // word; GT3, G4 and the rest hit the `return` and drew nothing at all.
            //
            // The four packet layouts are CERTAIN, confirmed twice over from this port's own
            // writers rather than from an SDK header:
            //   * AnimationSystem.InsertPrimitivesIntoOrderingTable stores the vertex XY words at
            //     +8/+20/+32/+44 (GT4), +8/+20/+32 (GT3), +8/+16/+24/+32 (G4), +8/+16/+24 (G3);
            //   * AnimationSystem.Render_UpdateClutTable stores the per-vertex colour words at
            //     +4/+16/+28/+40 (GT4) — and its own comment notes the byte at +7 is written from
            //     itself, which is the `code` byte sharing that first colour word;
            //   * the per-primitive strides those two loops advance by are 0x34 / 0x28 / 0x24 /
            //     0x1c, i.e. 52 / 40 / 36 / 28 bytes, matching the layouts exactly.
            //
            // Dithering of the Gouraud output IS modelled as of 2026-07-30 — see s_gpuDither and
            // PlotPixel. It was a real gap rather than a cosmetic one: these two fillers carry the
            // GTE's per-vertex lighting gradient, and writing it straight into a 5-bit-per-channel
            // buffer bands it. It only stopped mattering while the lighting itself was flat.
            if (gouraud)
            {
                if (textured)
                {
                    int need = quad ? 52 : 40;
                    if (offset + need > buf.Length)
                    {
                        return;
                    }

                    int gx0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
                    int gy0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
                    byte gu0 = buf[offset + 12];
                    byte gv0 = buf[offset + 13];
                    ushort gclut = (ushort)(buf[offset + 14] | (buf[offset + 15] << 8));

                    uint gbgr1 = ReadBgr(buf, offset + 16);
                    int gx1 = ReadI16(buf, offset + 20) + s_gpuDrawOffsetX;
                    int gy1 = ReadI16(buf, offset + 22) + s_gpuDrawOffsetY;
                    byte gu1 = buf[offset + 24];
                    byte gv1 = buf[offset + 25];
                    ushort gtpage = (ushort)(buf[offset + 26] | (buf[offset + 27] << 8));
                    LatchPolygonTPage(gtpage);

                    uint gbgr2 = ReadBgr(buf, offset + 28);
                    int gx2 = ReadI16(buf, offset + 32) + s_gpuDrawOffsetX;
                    int gy2 = ReadI16(buf, offset + 34) + s_gpuDrawOffsetY;
                    byte gu2 = buf[offset + 36];
                    byte gv2 = buf[offset + 37];

                    FillTexturedGouraudTriangle(gx0, gy0, gu0, gv0, bgr,
                        gx1, gy1, gu1, gv1, gbgr1, gx2, gy2, gu2, gv2, gbgr2,
                        gclut, gtpage, semiTransparent);

                    if (quad)
                    {
                        uint gbgr3 = ReadBgr(buf, offset + 40);
                        int gx3 = ReadI16(buf, offset + 44) + s_gpuDrawOffsetX;
                        int gy3 = ReadI16(buf, offset + 46) + s_gpuDrawOffsetY;
                        byte gu3 = buf[offset + 48];
                        byte gv3 = buf[offset + 49];
                        // Second triangle of the quad, in the PSX strip order (v1, v3, v2).
                        FillTexturedGouraudTriangle(gx1, gy1, gu1, gv1, gbgr1,
                            gx3, gy3, gu3, gv3, gbgr3, gx2, gy2, gu2, gv2, gbgr2,
                            gclut, gtpage, semiTransparent);
                    }

                    return;
                }
                else
                {
                    int need = quad ? 36 : 28;
                    if (offset + need > buf.Length)
                    {
                        return;
                    }

                    int gx0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
                    int gy0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
                    uint gbgr1 = ReadBgr(buf, offset + 12);
                    int gx1 = ReadI16(buf, offset + 16) + s_gpuDrawOffsetX;
                    int gy1 = ReadI16(buf, offset + 18) + s_gpuDrawOffsetY;
                    uint gbgr2 = ReadBgr(buf, offset + 20);
                    int gx2 = ReadI16(buf, offset + 24) + s_gpuDrawOffsetX;
                    int gy2 = ReadI16(buf, offset + 26) + s_gpuDrawOffsetY;

                    FillGouraudTriangle(gx0, gy0, bgr, gx1, gy1, gbgr1, gx2, gy2, gbgr2, semiTransparent);

                    if (quad)
                    {
                        uint gbgr3 = ReadBgr(buf, offset + 28);
                        int gx3 = ReadI16(buf, offset + 32) + s_gpuDrawOffsetX;
                        int gy3 = ReadI16(buf, offset + 34) + s_gpuDrawOffsetY;
                        FillGouraudTriangle(gx1, gy1, gbgr1, gx3, gy3, gbgr3, gx2, gy2, gbgr2, semiTransparent);
                    }

                    return;
                }
            }

            if (textured && quad)
            {
                // POLY_FT4: tag(4) + color/cmd(4) + 4x[xy(4) + uv/clut-or-tpage(4)] = 40 bytes.
                // Layout/vertex order per TextSystem.WritePolyFT4Rect: v0=(x0,y0,u0,v0,clut),
                // v1=(x1,y1,u1,v1,tpage), v2=(x2,y2,u2,v2), v3=(x3,y3,u3,v3). Split into two
                // triangles (v0,v1,v2) and (v1,v3,v2) — matches the axis-aligned rectangle the UI
                // glyph producers (RenderCharGlyph/DrawDigitGlyph) build, and generalizes correctly
                // to any quad via affine (non-perspective-correct) UV interpolation, matching real
                // PSX GPU behavior.
                // CORRECTION 2026-07-30: those two are NOT the only FT4 producers, as this note used
                // to say. AnimationSystem stamps code 0x2c and EffectQueueSystem 0x2e (the entity
                // ground shadow), so this arm also carries scene geometry — which is why it is one
                // of the arms that dithers (see PlotPixel).
                if (offset + 40 > buf.Length)
                {
                    return;
                }

                int x0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
                int y0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
                byte u0 = buf[offset + 12];
                byte v0 = buf[offset + 13];
                ushort clut = (ushort)(buf[offset + 14] | (buf[offset + 15] << 8));

                int x1 = ReadI16(buf, offset + 16) + s_gpuDrawOffsetX;
                int y1 = ReadI16(buf, offset + 18) + s_gpuDrawOffsetY;
                byte u1 = buf[offset + 20];
                byte v1 = buf[offset + 21];
                ushort tpage = (ushort)(buf[offset + 22] | (buf[offset + 23] << 8));
                LatchPolygonTPage(tpage);

                int x2 = ReadI16(buf, offset + 24) + s_gpuDrawOffsetX;
                int y2 = ReadI16(buf, offset + 26) + s_gpuDrawOffsetY;
                byte u2 = buf[offset + 28];
                byte v2 = buf[offset + 29];

                int x3 = ReadI16(buf, offset + 32) + s_gpuDrawOffsetX;
                int y3 = ReadI16(buf, offset + 34) + s_gpuDrawOffsetY;
                byte u3 = buf[offset + 36];
                byte v3 = buf[offset + 37];

                FillTexturedTriangle(x0, y0, u0, v0, x1, y1, u1, v1, x2, y2, u2, v2,
                    clut, tpage, bgr, semiTransparent);
                FillTexturedTriangle(x1, y1, u1, v1, x3, y3, u3, v3, x2, y2, u2, v2,
                    clut, tpage, bgr, semiTransparent);
                return;
            }

            if (textured)
            {
                // POLY_FT3: tag(4) + colour/cmd(4) + 3x[xy(4) + uv/clut-or-tpage(4)] = 32 bytes.
                if (offset + 32 > buf.Length)
                {
                    return;
                }

                int tx0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
                int ty0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
                byte tu0 = buf[offset + 12];
                byte tv0 = buf[offset + 13];
                ushort tclut = (ushort)(buf[offset + 14] | (buf[offset + 15] << 8));
                int tx1 = ReadI16(buf, offset + 16) + s_gpuDrawOffsetX;
                int ty1 = ReadI16(buf, offset + 18) + s_gpuDrawOffsetY;
                byte tu1 = buf[offset + 20];
                byte tv1 = buf[offset + 21];
                ushort ttpage = (ushort)(buf[offset + 22] | (buf[offset + 23] << 8));
                LatchPolygonTPage(ttpage);
                int tx2 = ReadI16(buf, offset + 24) + s_gpuDrawOffsetX;
                int ty2 = ReadI16(buf, offset + 26) + s_gpuDrawOffsetY;
                byte tu2 = buf[offset + 28];
                byte tv2 = buf[offset + 29];
                FillTexturedTriangle(tx0, ty0, tu0, tv0, tx1, ty1, tu1, tv1, tx2, ty2, tu2, tv2,
                    tclut, ttpage, bgr, semiTransparent);
                return;
            }

            if (quad)
            {
                // POLY_F4: tag(4) + colour/cmd(4) + 4x xy(4) = 24 bytes.
                if (offset + 24 > buf.Length)
                {
                    return;
                }

                int qx0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
                int qy0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
                int qx1 = ReadI16(buf, offset + 12) + s_gpuDrawOffsetX;
                int qy1 = ReadI16(buf, offset + 14) + s_gpuDrawOffsetY;
                int qx2 = ReadI16(buf, offset + 16) + s_gpuDrawOffsetX;
                int qy2 = ReadI16(buf, offset + 18) + s_gpuDrawOffsetY;
                int qx3 = ReadI16(buf, offset + 20) + s_gpuDrawOffsetX;
                int qy3 = ReadI16(buf, offset + 22) + s_gpuDrawOffsetY;
                FillTriangle(qx0, qy0, qx1, qy1, qx2, qy2, bgr, semiTransparent);
                FillTriangle(qx1, qy1, qx3, qy3, qx2, qy2, bgr, semiTransparent);
                return;
            }

            if (offset + 20 > buf.Length)
            {
                return;
            }

            int px0 = ReadI16(buf, offset + 8) + s_gpuDrawOffsetX;
            int py0 = ReadI16(buf, offset + 10) + s_gpuDrawOffsetY;
            int px1 = ReadI16(buf, offset + 12) + s_gpuDrawOffsetX;
            int py1 = ReadI16(buf, offset + 14) + s_gpuDrawOffsetY;
            int px2 = ReadI16(buf, offset + 16) + s_gpuDrawOffsetX;
            int py2 = ReadI16(buf, offset + 18) + s_gpuDrawOffsetY;
            FillTriangle(px0, py0, px1, py1, px2, py2, bgr, semiTransparent);
        }

        // else: LINE_F2/F4 (0x40) and anything else — extension point for the future full version.
    }

    // JUSTIFICATION: PSX hardware adaptation only — a TEXTURED POLYGON carries its own texpage
    // attribute word, and on real hardware that word is written into the GPU's draw-mode register
    // (the same bits 0-8 GP0(0xE1) sets) BEFORE the polygon is rasterized, so it governs that
    // polygon and every later primitive until the next texpage change. Only the texture page was
    // being honoured here — it was passed straight to the sampler — while the SEMI-TRANSPARENCY
    // RATE kept coming from whatever DR_TPAGE happened to run last.
    //
    // That is what made the entity ground shadow (Render_DrawRoom, POLY_FT4 code 0x2e, tpage 0xcb)
    // render as a hard grey quad. Measured on the running scene: the shadow's own tpage encodes
    // ABR 2 = back MINUS front, while the latched state was ABR 0 = (back + front) / 2. Its texture
    // is a 64x64 radial ramp (CLUT index 0x01 at the centre through 0x0f at the rim, all 15 entries
    // with STP set, none of them index 0), so under subtraction the rim removes ~1/31 of the
    // background and the centre ~14/31 — a soft round shadow. Under the 50 % rate every texel
    // instead pulls the background halfway to its own value, which flattens the whole quad into
    // uniform grey and makes the texture's square footprint the visible shape.
    // The 0x1ff mask is not a shortcut: a polygon's texpage attribute carries ONLY bits 0-8 of the
    // draw mode. Bits 9 (dither) and 10 (draw to display area) are not part of it and are left
    // alone here — they change only through GP0(0xE1) or PutDrawEnv. See s_gpuDither.
    private static void LatchPolygonTPage(int tpage)
    {
        s_gpuCurrentTPage = (ushort)(tpage & 0x1ff);
        s_gpuSemiTransRate = (s_gpuCurrentTPage >> 5) & 3;
    }

    // JUSTIFICATION: PSX hardware adaptation only — decodes the tpage bit layout GetTPage/
    // WriteDrawTPage/WritePolyFT4Rect already produce: bits0-3 = texture page X in 64px units,
    // bit4 = texture page Y in 256px units (0 or 256), bits5-6 = semi-transparency rate (unused by
    // this sampler), bits7-8 = color depth (0=4bpp CLUT, 1=8bpp CLUT, 2=16bpp direct).
    private static void DecodeTPage(int tpage, out int pageX, out int pageY, out int colorDepth)
    {
        pageX = (tpage & 0xF) * 64;
        pageY = ((tpage >> 4) & 1) * 256;
        colorDepth = (tpage >> 7) & 3;
    }

    // JUSTIFICATION: PSX hardware adaptation only — inverse of GetClut: clutX = (clut&0x3f)*16,
    // clutY = (clut>>6)&0x1ff. Verified live: clut 0x89c (RenderCharGlyph's font CLUT) decodes to
    // VRAM (448,34), which live VRAM capture showed holds plausible palette-like BGR555 entries.
    private static void DecodeClut(int clut, out int clutX, out int clutY)
    {
        clutX = (clut & 0x3f) * 16;
        clutY = (clut >> 6) & 0x1ff;
    }

    // JUSTIFICATION: PSX hardware adaptation only — real VRAM texel sample + CLUT lookup.
    // CORRECTION 2026-08-01: the two indexed arms used to short-circuit on `index == 0` and return
    // "transparent" WITHOUT consulting the CLUT, on the stated assumption that palette slot 0 is
    // transparent by TIM convention. That is not the hardware rule. The GPU always resolves the
    // index through the CLUT and then tests the RESULTING 16-bit halfword: only the value 0x0000
    // (black with the STP bit clear) is fully transparent — a non-zero colour parked in slot 0 is
    // drawn, and conversely a non-zero index whose CLUT entry is 0x0000 is NOT drawn. The index
    // test agrees with the hardware only for the (common) palettes whose slot 0 happens to hold
    // 0x0000, which is why UI text was unaffected: the dialogue font's CLUT 0x89c has entry 0 =
    // 0x0000, so its glyph backgrounds stay transparent under either rule.
    // Found on the FT16 clown: 62 of its textured primitives address a 4x4 patch of index-0 texels
    // and take their entire colour from CLUT 0x7084, whose entry 0 is 0x801f = opaque red — the
    // hat and the shoes. Under the index test all 62 were discarded, so the head's grey face
    // primitives showed through as a grey blob where the red hat belongs. Using a solid-index
    // patch plus a one-colour CLUT is a standard PSX idiom for a flat-coloured textured primitive,
    // so this affected every model built that way, not just this one; a VRAM survey of the scene
    // found most loaded palettes hold a non-zero colour in slot 0.
    private static bool SampleTexel(int tpage, int clut, int u, int v, out int r, out int g, out int b)
    {
        // GP0(0xE2) texture window, applied here because the hardware applies it per sampled texel,
        // not at packet-build time. Masks are in units of 8 texels; mask 0 (the reset state, and
        // what every primitive outside a windowed fill has) makes both lines the identity.
        u = (u & ~(s_gpuTexWindowMaskX * 8)) | ((s_gpuTexWindowOffsetX & s_gpuTexWindowMaskX) * 8);
        v = (v & ~(s_gpuTexWindowMaskY * 8)) | ((s_gpuTexWindowOffsetY & s_gpuTexWindowMaskY) * 8);

        DecodeTPage(tpage, out int pageX, out int pageY, out int colorDepth);
        int vramY = (pageY + v) & 0x1ff;
        ushort color16;

        if (colorDepth == 0)
        {
            int vramX = (pageX + (u >> 2)) & 0x3ff;
            ushort cell = Vram[vramY * 1024 + vramX];
            int index = (cell >> ((u & 3) * 4)) & 0xF;
            DecodeClut(clut, out int clutX, out int clutY);
            color16 = Vram[(clutY & 0x1ff) * 1024 + ((clutX + index) & 0x3ff)];
        }
        else if (colorDepth == 1)
        {
            int vramX = (pageX + (u >> 1)) & 0x3ff;
            ushort cell = Vram[vramY * 1024 + vramX];
            int index = (u & 1) == 0 ? (cell & 0xFF) : (cell >> 8);
            DecodeClut(clut, out int clutX, out int clutY);
            color16 = Vram[(clutY & 0x1ff) * 1024 + ((clutX + index) & 0x3ff)];
        }
        else
        {
            int vramX = (pageX + u) & 0x3ff;
            color16 = Vram[vramY * 1024 + vramX];
        }

        if (color16 == 0)
        {
            r = g = b = 0;
            return false;
        }

        r = (color16 & 0x1F) << 3;
        g = ((color16 >> 5) & 0x1F) << 3;
        b = ((color16 >> 10) & 0x1F) << 3;
        return true;
    }

    // JUSTIFICATION: PSX hardware adaptation only — standard PSX texture modulation: output = min
    // (255, texel * vertexColor / 128), 128 = neutral (matches RasterizePublisherSplashSprite's
    // established convention above, reused here for consistency).
    private static void FillTexturedRect(int x, int y, int w, int h,
        byte u0, byte v0, int clut, int tpage, uint bgr, bool semiTransparent)
    {
        byte modR = (byte)bgr, modG = (byte)(bgr >> 8), modB = (byte)(bgr >> 16);
        int x0 = System.Math.Max(x, System.Math.Max(0, (int)s_gpuClipX0));
        int y0 = System.Math.Max(y, System.Math.Max(0, (int)s_gpuClipY0));
        int x1 = System.Math.Min(x + w, System.Math.Min(1024, s_gpuClipX1 + 1));
        int y1 = System.Math.Min(y + h, System.Math.Min(512, s_gpuClipY1 + 1));

        for (int py = y0; py < y1; py++)
        {
            int v = v0 + (py - y);
            for (int px = x0; px < x1; px++)
            {
                int u = u0 + (px - x);
                if (!SampleTexel(tpage, clut, u, v, out int r, out int g, out int b))
                {
                    continue;
                }

                int outR = System.Math.Min(255, r * modR / 128);
                int outG = System.Math.Min(255, g * modG / 128);
                int outB = System.Math.Min(255, b * modB / 128);
                PlotPixel(px, py, (byte)outR, (byte)outG, (byte)outB, semiTransparent, false);
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — same barycentric coverage test as FillTriangle,
    // extended with affine (non-perspective-correct) UV interpolation, matching real PSX GPU
    // texture mapping (the hardware has no perspective correction).
    private static void FillTexturedTriangle(
        int x0, int y0, int u0, int v0,
        int x1, int y1, int u1, int v1,
        int x2, int y2, int u2, int v2,
        int clut, int tpage, uint bgr, bool semiTransparent)
    {
        int area = Edge(x0, y0, x1, y1, x2, y2);
        if (area == 0)
        {
            return;
        }

        byte modR = (byte)bgr, modG = (byte)(bgr >> 8), modB = (byte)(bgr >> 16);
        int denom = 4 * (area < 0 ? -area : area);
        int minX = System.Math.Max(System.Math.Min(x0, System.Math.Min(x1, x2)), System.Math.Max(0, (int)s_gpuClipX0));
        int maxX = System.Math.Min(System.Math.Max(x0, System.Math.Max(x1, x2)), System.Math.Min(1023, (int)s_gpuClipX1));
        int minY = System.Math.Max(System.Math.Min(y0, System.Math.Min(y1, y2)), System.Math.Max(0, (int)s_gpuClipY0));
        int maxY = System.Math.Min(System.Math.Max(y0, System.Math.Max(y1, y2)), System.Math.Min(511, (int)s_gpuClipY1));
        bool flip = area < 0;
        int absArea = flip ? -area : area;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                if (CoversPixelCentre(x0, y0, x1, y1, x2, y2, px, py, flip,
                        out int w0, out int w1, out int w2))
                {
                    int u = (int)(((long)u0 * w0 + (long)u1 * w1 + (long)u2 * w2) / denom);
                    int v = (int)(((long)v0 * w0 + (long)v1 * w1 + (long)v2 * w2) / denom);
                    if (!SampleTexel(tpage, clut, u, v, out int r, out int g, out int b))
                    {
                        continue;
                    }

                    int outR = System.Math.Min(255, r * modR / 128);
                    int outG = System.Math.Min(255, g * modG / 128);
                    int outB = System.Math.Min(255, b * modB / 128);
                    PlotPixel(px, py, (byte)outR, (byte)outG, (byte)outB, semiTransparent, true);
                }
            }
        }
    }

    private static void FillRect(int x, int y, int w, int h, uint bgr, bool semiTransparent)
    {
        byte r = (byte)bgr, g = (byte)(bgr >> 8), b = (byte)(bgr >> 16);
        int x0 = System.Math.Max(x, System.Math.Max(0, (int)s_gpuClipX0));
        int y0 = System.Math.Max(y, System.Math.Max(0, (int)s_gpuClipY0));
        int x1 = System.Math.Min(x + w, System.Math.Min(1024, s_gpuClipX1 + 1));
        int y1 = System.Math.Min(y + h, System.Math.Min(512, s_gpuClipY1 + 1));
        for (int py = y0; py < y1; py++)
        {
            for (int px = x0; px < x1; px++)
            {
                PlotPixel(px, py, r, g, b, semiTransparent, false);
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — POLY_G3/G4's per-vertex colour, interpolated
    // across the triangle with the SAME barycentric weights FillTriangle already computes for
    // coverage. Real Gouraud shading on the GPU is exactly this linear interpolation in screen
    // space (there is no perspective correction), so no extra machinery is needed.
    private static void FillGouraudTriangle(int x0, int y0, uint bgr0,
        int x1, int y1, uint bgr1, int x2, int y2, uint bgr2, bool semiTransparent)
    {
        int area = Edge(x0, y0, x1, y1, x2, y2);
        if (area == 0)
        {
            return;
        }

        int r0 = (byte)bgr0, g0 = (byte)(bgr0 >> 8), b0 = (byte)(bgr0 >> 16);
        int r1 = (byte)bgr1, g1 = (byte)(bgr1 >> 8), b1 = (byte)(bgr1 >> 16);
        int r2 = (byte)bgr2, g2 = (byte)(bgr2 >> 8), b2 = (byte)(bgr2 >> 16);
        int minX = System.Math.Max(System.Math.Min(x0, System.Math.Min(x1, x2)), System.Math.Max(0, (int)s_gpuClipX0));
        int maxX = System.Math.Min(System.Math.Max(x0, System.Math.Max(x1, x2)), System.Math.Min(1023, (int)s_gpuClipX1));
        int minY = System.Math.Max(System.Math.Min(y0, System.Math.Min(y1, y2)), System.Math.Max(0, (int)s_gpuClipY0));
        int maxY = System.Math.Min(System.Math.Max(y0, System.Math.Max(y1, y2)), System.Math.Min(511, (int)s_gpuClipY1));
        bool flip = area < 0;
        int absArea = flip ? -area : area;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                if (CoversPixelCentre(x0, y0, x1, y1, x2, y2, px, py, flip,
                        out int w0, out int w1, out int w2))
                {
                    int denom = 4 * absArea;
                    int r = (int)(((long)r0 * w0 + (long)r1 * w1 + (long)r2 * w2) / denom);
                    int g = (int)(((long)g0 * w0 + (long)g1 * w1 + (long)g2 * w2) / denom);
                    int b = (int)(((long)b0 * w0 + (long)b1 * w1 + (long)b2 * w2) / denom);
                    PlotPixel(px, py, (byte)r, (byte)g, (byte)b, semiTransparent, true);
                }
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — POLY_GT3/GT4: the affine UV interpolation of
    // FillTexturedTriangle with the flat modulation colour replaced by the interpolated per-vertex
    // one. The `texel * colour / 128` blend is the same one FillTexturedTriangle uses; only where
    // the colour comes from changes.
    private static void FillTexturedGouraudTriangle(
        int x0, int y0, int u0, int v0, uint bgr0,
        int x1, int y1, int u1, int v1, uint bgr1,
        int x2, int y2, int u2, int v2, uint bgr2,
        int clut, int tpage, bool semiTransparent)
    {
        int area = Edge(x0, y0, x1, y1, x2, y2);
        if (area == 0)
        {
            return;
        }

        int r0 = (byte)bgr0, g0 = (byte)(bgr0 >> 8), b0 = (byte)(bgr0 >> 16);
        int r1 = (byte)bgr1, g1 = (byte)(bgr1 >> 8), b1 = (byte)(bgr1 >> 16);
        int r2 = (byte)bgr2, g2 = (byte)(bgr2 >> 8), b2 = (byte)(bgr2 >> 16);
        int minX = System.Math.Max(System.Math.Min(x0, System.Math.Min(x1, x2)), System.Math.Max(0, (int)s_gpuClipX0));
        int maxX = System.Math.Min(System.Math.Max(x0, System.Math.Max(x1, x2)), System.Math.Min(1023, (int)s_gpuClipX1));
        int minY = System.Math.Max(System.Math.Min(y0, System.Math.Min(y1, y2)), System.Math.Max(0, (int)s_gpuClipY0));
        int maxY = System.Math.Min(System.Math.Max(y0, System.Math.Max(y1, y2)), System.Math.Min(511, (int)s_gpuClipY1));
        bool flip = area < 0;
        int absArea = flip ? -area : area;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                if (CoversPixelCentre(x0, y0, x1, y1, x2, y2, px, py, flip,
                        out int w0, out int w1, out int w2))
                {
                    int denom = 4 * absArea;
                    int u = (int)(((long)u0 * w0 + (long)u1 * w1 + (long)u2 * w2) / denom);
                    int v = (int)(((long)v0 * w0 + (long)v1 * w1 + (long)v2 * w2) / denom);
                    if (!SampleTexel(tpage, clut, u, v, out int r, out int g, out int b))
                    {
                        continue;
                    }

                    int modR = (int)(((long)r0 * w0 + (long)r1 * w1 + (long)r2 * w2) / denom);
                    int modG = (int)(((long)g0 * w0 + (long)g1 * w1 + (long)g2 * w2) / denom);
                    int modB = (int)(((long)b0 * w0 + (long)b1 * w1 + (long)b2 * w2) / denom);
                    int outR = System.Math.Min(255, r * modR / 128);
                    int outG = System.Math.Min(255, g * modG / 128);
                    int outB = System.Math.Min(255, b * modB / 128);
                    PlotPixel(px, py, (byte)outR, (byte)outG, (byte)outB, semiTransparent, true);
                }
            }
        }
    }

    private static void FillTriangle(int x0, int y0, int x1, int y1, int x2, int y2,
        uint bgr, bool semiTransparent)
    {
        int area = Edge(x0, y0, x1, y1, x2, y2);
        if (area == 0)
        {
            return;
        }

        byte r = (byte)bgr, g = (byte)(bgr >> 8), b = (byte)(bgr >> 16);
        int minX = System.Math.Max(System.Math.Min(x0, System.Math.Min(x1, x2)), System.Math.Max(0, (int)s_gpuClipX0));
        int maxX = System.Math.Min(System.Math.Max(x0, System.Math.Max(x1, x2)), System.Math.Min(1023, (int)s_gpuClipX1));
        int minY = System.Math.Max(System.Math.Min(y0, System.Math.Min(y1, y2)), System.Math.Max(0, (int)s_gpuClipY0));
        int maxY = System.Math.Min(System.Math.Max(y0, System.Math.Max(y1, y2)), System.Math.Min(511, (int)s_gpuClipY1));
        bool flip = area < 0;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                if (CoversPixelCentre(x0, y0, x1, y1, x2, y2, px, py, flip,
                        out _, out _, out _))
                {
                    PlotPixel(px, py, r, g, b, semiTransparent, false);
                }
            }
        }
    }

    private static int Edge(int ax, int ay, int bx, int by, int px, int py)
    {
        return (bx - ax) * (py - ay) - (by - ay) * (px - ax);
    }

    // JUSTIFICATION: PSX hardware adaptation only — the coverage test the four triangle fillers
    // share. Coverage is decided at the pixel CENTRE, not at its integer top-left corner.
    // WHY, measured 2026-08-02: with a corner test and `>= 0`, a quad spanning x..x+w and y..y+h
    // rasterized w+1 columns and h+1 rows — the bounding-box loops are inclusive and the pixels
    // sitting exactly on the right and bottom edges passed. The real GPU does not draw those edges.
    // On a 5x7 digit glyph the extra column sampled u+5, which is the FIRST COLUMN OF THE NEXT
    // GLYPH: the digit font packs its glyphs 5 texels apart with no gutter. Inside a number that
    // bleed is overwritten by the next digit's own quad, so only the LAST digit kept a visible
    // sliver — the stray mark after every stat-gauge number, and the "0" of "10" reading as a broken
    // glyph because it had inherited the first stroke of the "1".
    // Doubling the vertices and testing (2*px+1, 2*py+1) is the centre test in integer arithmetic.
    // The three edge values then sum to 4*area, which is why the interpolating callers divide by
    // 4*absArea and widen their numerators to long.
    private static bool CoversPixelCentre(int x0, int y0, int x1, int y1, int x2, int y2,
        int px, int py, bool flip, out int w0, out int w1, out int w2)
    {
        int cx = 2 * px + 1;
        int cy = 2 * py + 1;
        w0 = Edge(2 * x1, 2 * y1, 2 * x2, 2 * y2, cx, cy);
        w1 = Edge(2 * x2, 2 * y2, 2 * x0, 2 * y0, cx, cy);
        w2 = Edge(2 * x0, 2 * y0, 2 * x1, 2 * y1, cx, cy);
        if (flip)
        {
            w0 = -w0;
            w1 = -w1;
            w2 = -w2;
        }

        return w0 >= 0 && w1 >= 0 && w2 >= 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only — writes one VRAM cell in the GPU's native
    // BGR555 packing (bits 0-4 R, 5-9 G, 10-14 B — the same layout SampleTexel/ReadDisplayRgb24
    // decode), truncating each 8-bit channel to 5 bits exactly as the hardware does.
    //
    // CORRECTION 2026-07-28: semi-transparency was hardcoded to ABR rate 0 (0.5 x back + 0.5 x
    // front) under a note saying "no writer in this codebase selects the other rates yet". Three
    // writers do, and all three are load-bearing:
    //   * the screen fade's DR_TPAGE packs `(drawModeIndex & 3) << 5` with drawModeIndex = 2, i.e.
    //     rate 2 = back MINUS front. A full-white tile subtracted is BLACK — which is why the fade
    //     rendered as a wash to white under rate 0, and why the Ghidra names read backwards: they
    //     name the TILE's colour, not the result;
    //   * MenuSystem.cs:970 writes tpage 0x20 = rate 1 (additive), for a bright UI fill;
    //   * TextSystem.cs:175/190 (RenderUiBorderPath) draws a border in TWO passes,
    //     `((borderStyle + 1) & 3) << 5` then `((2 - borderStyle) & 3) << 5` — for borderStyle 0
    //     that is rate 1 then rate 2, a textbook additive-highlight + subtractive-shadow bevel.
    // A subtractive edge over a BLACK background necessarily produces 0 — so a UI frame on a black
    // screen legitimately shows only its highlight edge. That is arithmetic, not an approximation;
    // rate 0 was making both edges visible as 50% grey, which is the artefact.
    //
    // All four rates, saturating to 0..31 as the hardware does:
    //   0: B/2 + F/2   1: B + F   2: B - F   3: B + F/4
    // JUSTIFICATION: PSX hardware adaptation only — the GP0 LINE category's span filler, the line
    // counterpart of the polygon and rectangle fillers above. Bresenham on the major axis with the
    // two endpoint colours interpolated along it, which is what the hardware's Gouraud line does; a
    // flat line reaches this with both colours equal, so one routine covers LINE_F2 and LINE_G2.
    // Clipping is left to PlotPixel, exactly as the polygon path does.
    private static void DrawGouraudLine(int x0, int y0, int x1, int y1,
                                        byte r0, byte g0, byte b0,
                                        byte r1, byte g1, byte b1,
                                        bool semiTransparent, bool dither)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;
        int adx = dx < 0 ? -dx : dx;
        int ady = dy < 0 ? -dy : dy;
        int steps = adx > ady ? adx : ady;
        if (steps == 0)
        {
            PlotPixel(x0, y0, r0, g0, b0, semiTransparent, dither);
            return;
        }

        // A PSX line spanning more than the drawing area is a malformed packet, not a long line; the
        // hardware itself refuses spans wider than 1023/511. Bounding the loop keeps a corrupt packet
        // from stalling the frame instead of just drawing wrong.
        if (steps > 1024)
        {
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            int px = x0 + (dx * i) / steps;
            int py = y0 + (dy * i) / steps;
            byte pr = (byte)(r0 + ((r1 - r0) * i) / steps);
            byte pg = (byte)(g0 + ((g1 - g0) * i) / steps);
            byte pb = (byte)(b0 + ((b1 - b0) * i) / steps);
            PlotPixel(px, py, pr, pg, pb, semiTransparent, dither);
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — the GPU's 4x4 ordered dither matrix, indexed by
    // (x AND 3, y AND 3) in ABSOLUTE VRAM coordinates (which is what PlotPixel receives, the draw
    // offset having already been added by the dispatcher). The offset is added to the 8-bit channel,
    // the sum is clipped to 0..0xFF, and only then are the upper 5 bits kept — the same order the
    // hardware uses, which is why a +1 can carry a channel up into the next 5-bit step and a -4
    // can pull it down.
    private static readonly int[] s_ditherOffsets =
    {
        -4,  0, -3,  1,
         2, -2,  3, -1,
        -3,  1, -4,  0,
         3, -1,  2, -2,
    };

    private static byte DitherChannel(int value, int offset)
    {
        int v = value + offset;
        if (v < 0) { return 0; }
        if (v > 255) { return 255; }
        return (byte)v;
    }

    // `dither` says whether THIS primitive is one the GPU dithers, independently of whether dithering
    // is currently enabled (s_gpuDither) — both must hold. The rule the GPU applies is that SHADED
    // and TEXTURE-BLENDED pixels are dithered and flat monochrome ones are not, so the predicate is
    // a property of the primitive form, decided at each call site rather than inferred from the
    // colour values (two equal endpoint colours can still be a Gouraud primitive).
    // The three POLYGON fillers pass true:
    //   * the two Gouraud fillers — shaded pixels are the documented case, and they are the entire
    //     entity-model path (ParseTMDModel emits only GT4/GT3/G4/G3), i.e. exactly where the GTE's
    //     per-vertex lighting gradient lands and where 5-bit truncation shows as banding;
    //   * FillTexturedTriangle (POLY_FT3/FT4) — texture-BLENDED pixels are the other documented
    //     case. `texel * colour / 128` is a blend even when the colour is flat, and the result is
    //     an 8-bit-per-channel value going into a 5-bit buffer exactly like the Gouraud one.
    // Rectangles and sprites keep passing false: the hardware does not dither rectangle commands at
    // all, which is also why every TILE/SPRT stays bit-identical — including the dialogue text,
    // whose glyphs TextSystem.TryEmitDialogueGlyph emits as SPRT, not as polygons.
    //
    // On the FT4 producers, correcting the older note at the POLY dispatch above: they are NOT only
    // RenderCharGlyph/DrawDigitGlyph. AnimationSystem stamps code 0x2c and EffectQueueSystem 0x2e
    // (the entity ground shadow), so this arm carries scene geometry too, not just UI glyphs. The
    // UI glyph quads are affected only where the game itself left dithering enabled — the same gate
    // the hardware applies, since a polygon's texpage attribute cannot change bit 9 (see
    // LatchPolygonTPage) and the state therefore comes from whichever GP0(0xE1)/PutDrawEnv ran last
    // in ordering-table order. Whether that lands on a given menu glyph is a runtime-order question,
    // deliberately left to the gate rather than to a hardcoded exception here.
    //
    // MEASURED 2026-07-31, and the gate answers "no" in the UI. Live A/B on the running game (the
    // dither offset table zeroed in place between two captures of one static screen, blinking pixels
    // excluded by a same-setting control): the in-game menu's FT4 text changed by ZERO pixels — 0 of
    // 1088 over the "Use Item" header and 0 of 780 over the DrawDigitGlyph counters — while the
    // entity model on the same frame changed by 202. That is not a saturation artefact: 67% and 81%
    // of those glyph pixels sit at non-saturated 5-bit levels, so a dithered primitive there would
    // have moved them. The game turns dithering OFF for its UI itself (every DR_TPAGE on the
    // TextSystem/MenuSystem UI paths passes dtd = 0), which is exactly what the console does.
    // The same run also confirms the arithmetic end to end: every changed channel moved by exactly
    // one 5-bit level (-1/0/+1), the signature dithering can produce and nothing else can.
    //
    // LINES follow the same split: DrawGouraudLine takes the dispatcher's LINE_G-vs-LINE_F answer
    // and passes it straight through, so a Gouraud line dithers and a monochrome one does not. That
    // covers both of this port's line emitters — MeshWireframe's dome (LINE_G2 0x50/0x52, which is
    // shaded and therefore dithers) and CombatHud.SetLineF2Tag's target cursor leader (LINE_F2 0x40,
    // which does not).
    private static void PlotPixel(int x, int y, byte r, byte g, byte b, bool semiTransparent, bool dither)
    {
        if (x < 0 || y < 0 || x >= 1024 || y >= 512)
        {
            return;
        }

        if (dither && s_gpuDither)
        {
            int offset = s_ditherOffsets[((y & 3) << 2) | (x & 3)];
            r = DitherChannel(r, offset);
            g = DitherChannel(g, offset);
            b = DitherChannel(b, offset);
        }

        // CORRECTION 2026-07-30: the drawing area (GP0 0xE3/0xE4, held in s_gpuClip*) was honoured by
        // every filler in this rasterizer EXCEPT this one, which bounded only to VRAM. On real
        // hardware the drawing area clips EVERY primitive, lines included — it is the GPU's own
        // scissor, not a per-shape convenience.
        //
        // What that cost, measured over 38233 rasterized segments of the attack-range dome: 2872 of
        // them reach above the top of their draw buffer (by up to 17 px — the GTE projects the dome's
        // apex off-screen and the hardware would simply scissor it). With the draw buffers at VRAM
        // (0,0) and (0,224), the buffer-0 copies land at y<0 and were rejected here by luck, while the
        // buffer-1 copies land at y=207..223 — inside BUFFER 0 — and were drawn. That is the reported
        // "the dome runs off the screen and reappears on the other side": the dome's top, painted into
        // the bottom of the other frame.
        //
        // This is exactly the clamp lines 2212/2283/2334/2386 (triangles) and 2172/2254 (rects)
        // already apply before entering their loops, so the six filler call sites are unaffected —
        // their loops cannot produce a pixel this test would reject. Only DrawGouraudLine, which
        // deliberately leaves clipping to this function, changes behaviour.
        if (x < s_gpuClipX0 || x > s_gpuClipX1 || y < s_gpuClipY0 || y > s_gpuClipY1)
        {
            return;
        }

        int cell = y * 1024 + x;
        int r5 = r >> 3, g5 = g >> 3, b5 = b >> 3;
        if (semiTransparent)
        {
            ushort back = Vram[cell];
            int backR = back & 0x1f, backG = (back >> 5) & 0x1f, backB = (back >> 10) & 0x1f;
            switch (s_gpuSemiTransRate)
            {
                case 1:
                    r5 = backR + r5; g5 = backG + g5; b5 = backB + b5;
                    break;
                case 2:
                    r5 = backR - r5; g5 = backG - g5; b5 = backB - b5;
                    break;
                case 3:
                    r5 = backR + r5 / 4; g5 = backG + g5 / 4; b5 = backB + b5 / 4;
                    break;
                default:
                    r5 = (backR + r5) / 2; g5 = (backG + g5) / 2; b5 = (backB + b5) / 2;
                    break;
            }

            if (r5 < 0) { r5 = 0; } else if (r5 > 31) { r5 = 31; }
            if (g5 < 0) { g5 = 0; } else if (g5 > 31) { g5 = 31; }
            if (b5 < 0) { b5 = 0; } else if (b5 > 31) { b5 = 31; }
        }

        Vram[cell] = (ushort)(r5 | (g5 << 5) | (b5 << 10));
    }

    /**
     * @brief Merge primitive lists
     *
     * @param p0 First primitive list
     * @param p1 Second primitive list
     * @return 1 on success
     */
    public static int MargePrim(object p0, object p1)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Store image from frame buffer to memory
     *
     * @param rect Source rectangle in frame buffer
     * @param p Pointer to destination buffer
     * @return 1 on success
     */
    // GHIDRA: StoreImage @ 0x800750cc — same shape as LoadImage above: it passes "StoreImage"
    // (0x800118E0) to the same RECT validator and dispatches through +0x1C instead of +0x20.
    // ADDRESS ESTABLISHED 2026-08-02 from the function's OWN TRACE STRING, not from where it sits
    // between its neighbours: the psyq libgpu entry points open with a debug-level test
    // (`lbu 0x8009574E`, `sltiu $v0,$v0,2`) and, above level 1, call the trace hook at 0x80095748
    // with a format string naming themselves. Reading that string out of SLUS_006.62 identifies
    // the address outright.
    public static int StoreImage(RECT rect, ulong[] p)
    {
        // Do nothing PSX SDK
        return 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only — byte[] overload for buffers declared as
    // byte[] in C# (e.g. g_primitiveDrawBuf0) instead of ulong[].
    //
    // THIS NO-OP IS A LIVE DEFECT, identified 2026-08-08 as the cause of the missing full-screen
    // flash on the m0022i parrot transformation. It is the exact shape LoadImage had: harmless while
    // its consumer was also a stub, and wrong the moment the consumer became real.
    // The chain, measured on console: g_menuTransitionState 3 stores the VRAM strip here into
    // g_primitiveDrawBuf0; state 6 has ProcessFrameABufferFade @0x80042d40 fade buf0 -> buf1
    // (verified live, $v0 = 6, $ra = 0x80042FC8); UploadMenuTransitionStrip @0x80042fe8 then uploads
    // buf1. With this reading nothing, buf0 stays zero, and the fade's `uVar3 == 0` arm writes zero
    // into buf1 — so a strip of zeros is uploaded and nothing shows.
    // THAT PREDICTION MATCHES THE FRAME-ACCURATE A/B EXACTLY: the port reproduces the darkening ramp,
    // which comes from the DRAWENV background colour the fade sets at its tail and does NOT depend on
    // the buffer, and misses only the bright desaturated strip, which does. See the note on
    // Gpu.FUN_80042fe8 for the numbers.
    // IMPLEMENTED 2026-08-08 as the exact inverse of LoadImage's real write, through ReadVramRect
    // below — same halfword-count `rect.w`, same row-major order, same little-endian byte pair, same
    // 0x3ff / 0x1ff wrap. Anything else would make a store/load round trip lossy.
    // The one call site that needs it fits its buffer exactly: state 3 stores
    // {x=0, y=0x1E0, w=0x100, h=g_menuTransitionStripHeight} into g_primitiveDrawBuf0, which is
    // BYTE_ARRAY_801771a0 = byte[0x4000] = 256 halfwords * 32 rows * 2 bytes. The strip starts at
    // VRAM y=480 and VRAM is 512 tall, so h cannot exceed 32 — the buffer is sized for the maximum
    // and the bounds check below cannot reject a legitimate call.
    public static int StoreImage(RECT rect, byte[] p)
    {
        if (rect == null || rect.w <= 0 || rect.h <= 0 || p == null)
        {
            return 0;
        }

        ReadVramRect(rect, p, 0);
        return 1;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the read half of WriteVramRect above, kept
    // beside it deliberately so the two conventions cannot drift apart.
    private static void ReadVramRect(RECT rect, byte[] dest, int destOffset)
    {
        int neededBytes = rect.w * rect.h * 2;
        if (destOffset < 0 || neededBytes <= 0 || destOffset + neededBytes > dest.Length)
        {
            return;
        }

        for (int row = 0; row < rect.h; row++)
        {
            int vy = (rect.y + row) & 0x1ff;
            int dstRowOffset = destOffset + row * rect.w * 2;
            for (int col = 0; col < rect.w; col++)
            {
                int vx = (rect.x + col) & 0x3ff;
                ushort word = Vram[vy * 1024 + vx];
                dest[dstRowOffset + col * 2] = (byte)(word & 0xff);
                dest[dstRowOffset + col * 2 + 1] = (byte)(word >> 8);
            }
        }
    }

    /**
     * @brief Move image within frame buffer
     *
     * @param rect Source rectangle
     * @param x Destination X coordinate
     * @param y Destination Y coordinate
     * @return 1 on success
     */
    // JUSTIFICATION: PSX hardware adaptation only — real VRAM-to-VRAM block copy. Was a no-op
    // stub. This one is NOT cosmetic: the title screen's 24-bit background is 320 pixels x 3 bytes
    // = 480 VRAM halfwords wide, so uploading it (FUN_8018f2f4_PrimeDoubleBufferedDisplay, rect
    // {0, 0x14|0x104, 0x1e0, 0xcc}) overwrites VRAM x=320..479 — which is where boot put the UI
    // font atlas (320,0,64,256) and part of the section-9 image block (448,0,64,254). The original
    // knows this and keeps a spare copy at x=704: Func_801909b4 restores it with
    // `MoveImage({0x2c0,0,0xa0,0x100}, 0x140, 0)` immediately after the title loop, before setting
    // the menu frontend up. Without that copy the memory-card menu samples title-background bytes
    // as 4bpp glyph indices and every character comes out as coloured noise.
    // Source is snapshotted first so overlapping rects behave like the hardware's read-then-write.
    // GHIDRA: MoveImage @ 0x8007512c — it passes "MoveImage" (0x800118EC) to the RECT validator.
    // ADDRESS ESTABLISHED 2026-08-02 from the function's OWN TRACE STRING, not from where it sits
    // between its neighbours: the psyq libgpu entry points open with a debug-level test
    // (`lbu 0x8009574E`, `sltiu $v0,$v0,2`) and, above level 1, call the trace hook at 0x80095748
    // with a format string naming themselves. Reading that string out of SLUS_006.62 identifies
    // the address outright.
    // THE ORIGINAL REJECTS A DEGENERATE RECT: `lh 0x4($s0)` and `lh 0x6($s0)` are tested against
    // zero at 0x8007515C and 0x8007516C, and either one returns -1 without touching VRAM. This port
    // returns 0 on `w <= 0 || h <= 0` — a wider guard with a different sentinel. No caller in this
    // port reads the result, so nothing observes the difference today.
    public static int MoveImage(RECT rect, int x, int y)
    {
        if (rect == null || rect.w <= 0 || rect.h <= 0)
        {
            return 0;
        }

        ushort[] copy = new ushort[rect.w * rect.h];
        for (int row = 0; row < rect.h; row++)
        {
            int vy = (rect.y + row) & 0x1ff;
            for (int col = 0; col < rect.w; col++)
            {
                copy[row * rect.w + col] = Vram[vy * 1024 + ((rect.x + col) & 0x3ff)];
            }
        }

        for (int row = 0; row < rect.h; row++)
        {
            int vy = (y + row) & 0x1ff;
            for (int col = 0; col < rect.w; col++)
            {
                Vram[vy * 1024 + ((x + col) & 0x3ff)] = copy[row * rect.w + col];
            }
        }

        return 1;
    }

    /**
     * @brief Open TIM image file
     *
     * @param addr Pointer to TIM data
     * @return 0 on success, -1 on error
     */
    public static int OpenTIM(ulong[] addr)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Clear ordering table
     *
     * @param ot Pointer to ordering table
     * @param n Number of entries
     * @return Pointer to ordering table
     */
    public static OT_TYPE ClearOTag(OT_TYPE ot, int n)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Clear ordering table in reverse
     *
     * @param ot Pointer to ordering table
     * @param n Number of entries
     * @return Pointer to ordering table
     */
    public static OT_TYPE ClearOTagR(OT_TYPE ot, int n)
    {
        // Do nothing PSX SDK
        return null;
    }

    // GHIDRA: ClearOTagR @ 0x800752ac
    // JUSTIFICATION: PSX hardware adaptation only — the ROM routine is a thin wrapper that
    // dispatches through the libgpu command table (`(**(code **)(PTR_PTR_80095744 + 0x2c))`), so
    // what is transliterated here is its observable contract, not the dispatch. ClearOTagR
    // initialises an ordering table for REVERSE (back-to-front) traversal: entry i links to entry
    // i-1, and entry 0 holds the 0x00ffffff end-of-chain terminator. Callers then hand the LAST
    // entry to DrawOTag/DrawOTagEnv — RunMainMenuFrontendLoop clears 0x1000 entries and
    // PresentFrameAndSwapBuffers draws from byte offset 0x3ffc = entry 0xfff — so the walk runs
    // 0xfff -> 0xffe -> ... -> 0.
    // Was a no-op, which left the frontend's ordering table uninitialised.
    // CORRECTION 2026-07-28: the link written into entry i used to be the ARRAY OFFSET of entry
    // i-1. It is now that entry's PSX ADDRESS (see the RamRegion block above), which is what the
    // original stores and what makes the chain readable from outside this one array. It also
    // removes the collision this comment used to document: entry 1's link was 0, the walker's
    // end-of-chain value, so the walk stopped one entry early.
    public static byte[] ClearOTagR(byte[] ot, int n)
    {
        return ClearOTagR(ot, 0, n);
    }

    // JUSTIFICATION: C# language bridge only — the body shared by the two overloads, which differ
    // solely in how the table's first entry is named (an array, or a PSX address that resolves to a
    // byte offset inside one).
    private static byte[] ClearOTagR(byte[] ot, int baseOffset, int n)
    {
        if (ot == null || n <= 0 || baseOffset < 0)
        {
            return ot;
        }

        int last = System.Math.Min(n, (ot.Length - baseOffset) / 4);
        for (int i = last - 1; i > 0; i--)
        {
            WriteU32(ot, baseOffset + i * 4, (uint)RamAddressOf(ot, baseOffset + (i - 1) * 4) & 0x00ffffff);
        }

        WriteU32(ot, baseOffset, 0x00ffffff);


        return ot;
    }

    // JUSTIFICATION: PSX hardware adaptation only — int overload used where g_menuOTagBase holds the
    // ordering table's PSX address (InitMenuFrameGpuState @0x8005e6f0), which C# cannot dereference.
    // Was a deliberate no-op, because a pre-linked table fed the old array-relative walker
    // ordering-table offsets that it read as primitive-buffer offsets and rasterized as primitives —
    // measured at the time as 2.15% of the Select Slot screen turning to garbage. Both causes are
    // gone: links are PSX addresses, and the walker skips any node whose tag length is 0, which
    // every ordering-table entry is.
    public static void ClearOTagR(int otagBase, int n)
    {
        if (RamResolve(otagBase, out byte[] ot, out int baseOffset))
        {
            ClearOTagR(ot, baseOffset, n);
        }
    }

    /**
     * @brief Set drawing environment
     *
     * @param env Drawing environment
     * @return Pointer to drawing environment
     */
    // JUSTIFICATION: PSX hardware adaptation only — programs the GPU's persistent drawing
    // registers from a DRAWENV: the drawing area (GP0(0xE3)/(0xE4), from `clip`) and the drawing
    // offset (GP0(0xE5), from `ofs`), both in absolute VRAM coordinates. When `isbg` is set it
    // ALSO fills the drawing area with (r0,g0,b0) before anything else is drawn into it — the
    // PSX's per-frame frame-buffer clear.
    // Was a no-op stub; the title path (PeSection70Overlay's OverlayFrameState.Draw, isbg
    // explicitly cleared by FUN_80190db4_DispEnvSetup) reaches this overload, and its drawing
    // offset is what places DrawPrim's primitives in the right draw buffer.
    public static DRAWENV PutDrawEnv(DRAWENV env)
    {
        ApplyDrawEnv(env);
        return env;
    }

    // JUSTIFICATION: PSX hardware adaptation only — int-index overload used when g_menuActiveDrawEnvPtr
    // stores a raw PSX address as int (C# cannot select DRAWENV via raw pointer arithmetic).
    // `envPtr` is the drawBufIdx sentinel InitMenuFrameGpuState stores (0 -> DRAWENV_800a2180,
    // 1 -> DRAWENV_800a21f8).
    // The isbg clear this performs (see ApplyDrawEnv) is the menu frontend's only frame-buffer
    // clear: InitPsxDoubleBuffering (0x8005e588) sets isbg=1 with r0=g0=b0=0 on both menu DRAWENVs,
    // and no UI object requests a box fill (RenderUiTypeOneFrames passes cursorX_0x44, which is 0
    // for the memory-card tree). Without it the memory-card screen accumulated every frame's
    // primitives forever — "Checking Memory Card" and "Select Slot" superimposed.
    // CORRECTION 2026-07-27: an earlier revision of this comment also claimed the clear is the ONLY
    // thing that repaints the draw area, because "PresentMenuFrame's `if (g_menuVramLoadPending)`
    // LoadImage is dead code in the retail build (Ghidra: the only write to 0x8009d134 is
    // InitPsxDoubleBuffering setting it to 0)". That is WRONG, and wrong in a way worth remembering:
    // find-cross-references only covers the program it is asked about, and the write that matters
    // lives in the SECTION 70 OVERLAY, not in SLUS_006.62 — Func_801909b4 sets
    // g_menuVramLoadPending = OVL801D_DISPENV_ARRAY_BaseAddr[1] + 0x1C080 immediately before the
    // memory-card polling loop and back to 0 after it (PeSection70Overlay.cs). So the real per-frame
    // sequence is: clear the draw area to black, RE-UPLOAD the dimmed title background over it
    // (320x204 at local y=11, i.e. absolute y=0xb or 0xeb — the same buffer-dependent absolute-VRAM
    // convention as the DR_AREA rects), then DrawOTag the menu on top.
    // Which DRAWENV the int sentinel selects is game-specific (see the game's PsxSdkBridges);
    // not installed -> no-op.
    public static Func<int, DRAWENV> DrawEnvIntResolver;

    public static void PutDrawEnv(int envPtr)
    {
        DRAWENV env = DrawEnvIntResolver?.Invoke(envPtr);
        if (env != null)
        {
            ApplyDrawEnv(env);
        }
    }

    // JUSTIFICATION: C# language bridge only — the body shared by the two PutDrawEnv overloads
    // above, which differ solely in how they name the DRAWENV.
    private static void ApplyDrawEnv(DRAWENV env)
    {
        if (env == null)
        {
            return;
        }

        // PutDrawEnv issues a GP0(0xE1) built from the DRAWENV's tpage/dtd/dfe, so the whole draw
        // mode travels with the environment, not only with standalone DR_TPAGE packets. This is the
        // path that actually turns dithering on in-game: SLUS_006_62 sets dtd = 1 on both draw
        // buffers at boot and MainLoop calls PutDrawEnv with one of them every frame.
        //
        // The tpage half landed one commit later than the dither bit, deliberately: it also moves the
        // SEMI-TRANSPARENCY RATE, which PlotPixel's rate behaviour was calibrated against. Re-checked
        // against the two rate-sensitive UI writers before enabling, and both are safe because they
        // carry their own DR_TPAGE: MenuSystem.RenderUiFillTile (tpage 0x20, rate 1) and
        // TextSystem.RenderUiBorderPath (two passes, rates from borderStyle). Note the ordering that
        // makes them work — each ADDS its DR_TPAGE to the ordering table AFTER the primitives it
        // governs, which puts it BEFORE them in this rasterizer's newest-added-first walk.
        // What DOES change is a primitive submitted with no DR_TPAGE of its own: it used to inherit
        // whatever rate the PREVIOUS FRAME happened to leave behind, and now gets the environment's
        // — which is what the hardware does, and PE sets tpage = 0 on both draw buffers (texture
        // page 0, rate 0, the 50% blend).
        // PARTIAL, stated rather than hidden: PutDrawEnv also issues GP0(0xE2) from `tw` and bit 10
        // from `dfe`. Neither is modelled — this rasterizer has no texture window (the 0xE2 packet
        // case is a documented no-op) and no draw-to-display-area restriction — so those two stay
        // unwired rather than being half-applied here.
        s_gpuDither = env.dtd != 0;
        s_gpuCurrentTPage = (ushort)(env.tpage & 0x1ff);
        s_gpuSemiTransRate = (s_gpuCurrentTPage >> 5) & 3;
        s_gpuDrawOffsetX = env.ofs[0];
        s_gpuDrawOffsetY = env.ofs[1];
        s_gpuClipX0 = env.clip.x;
        s_gpuClipY0 = env.clip.y;
        s_gpuClipX1 = (short)(env.clip.x + env.clip.w - 1);
        s_gpuClipY1 = (short)(env.clip.y + env.clip.h - 1);

        if (env.isbg == 0)
        {
            return;
        }

        ushort fill = (ushort)((env.r0 >> 3) | ((env.g0 >> 3) << 5) | ((env.b0 >> 3) << 10));
        int x0 = System.Math.Max(0, (int)env.clip.x);
        int y0 = System.Math.Max(0, (int)env.clip.y);
        int x1 = System.Math.Min(1024, env.clip.x + env.clip.w);
        int y1 = System.Math.Min(512, env.clip.y + env.clip.h);
        for (int py = y0; py < y1; py++)
        {
            int row = py * 1024;
            for (int px = x0; px < x1; px++)
            {
                Vram[row + px] = fill;
            }
        }
    }

    /**
     * @brief Set display environment
     *
     * @param env Display environment
     * @return Pointer to display environment
     */
    // JUSTIFICATION: PSX hardware adaptation only — the real PutDispEnv programs the GPU's display
    // window: which VRAM rect is scanned out, and at which colour depth. The host presenter
    // (ReadDisplayRgb24) needs exactly that, because the two paths differ in BOTH: the title screen
    // displays 24-bit (isrgb24 = 1, reaching the overlay's frames via FUN_80190db4_DispEnvSetup)
    // while the menu frontend displays 15-bit. Latching the env is all that is modelled; display
    // timing is not.
    // GHIDRA: PutDispEnv @ 0x800755f0 — its trace string at 0x80011970 is "PutDispEnv(%08x)...".
    // ADDRESS ESTABLISHED 2026-08-02 from the function's OWN TRACE STRING, not from where it sits
    // between its neighbours: the psyq libgpu entry points open with a debug-level test
    // (`lbu 0x8009574E`, `sltiu $v0,$v0,2`) and, above level 1, call the trace hook at 0x80095748
    // with a format string naming themselves. Reading that string out of SLUS_006.62 identifies
    // the address outright.
    public static DISPENV PutDispEnv(DISPENV env)
    {
        if (env != null)
        {
            ActiveDispEnv = env;
        }
        return env;
    }

    // JUSTIFICATION: PSX hardware adaptation only — int-index overload used when g_menuActiveDrawEnvPtr
    // stores a raw PSX address as int; callers add 0x5c to reach the embedded DISPENV. In the original
    // the menu's DISPENV sits immediately after its DRAWENV (0x800a2180 + 0x5c = DISPENV_800a21dc,
    // 0x800a21f8 + 0x5c = DISPENV_800a2254), so PresentMenuFrame's drawBufIdx sentinel resolves the
    // same way PutDrawEnv(int) resolves its own.
    // Which DISPENV the int sentinel selects is game-specific (see the game's PsxSdkBridges);
    // not installed -> no-op.
    public static Func<int, DISPENV> DispEnvIntResolver;

    public static void PutDispEnv(int envPtr)
    {
        DISPENV env = DispEnvIntResolver?.Invoke(envPtr);
        if (env != null)
        {
            ActiveDispEnv = env;
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — the GPU's display-window register: which VRAM
    // rect the host presents, and at which colour depth. See PutDispEnv above.
    public static DISPENV ActiveDispEnv;

    // JUSTIFICATION: backend MonoGame only — the host present path. Scans the active display window
    // out of `Vram` into tightly-packed 24-bit RGB, the format GameRemaster uploads to a Texture2D.
    // Replaces the three RGB24 side channels the backend used to composite (LastImage*,
    // PendingSprite*, MenuFramebuffer*), each of which stood in for a region of this same VRAM and
    // whose composite ORDER — rather than the order the writes actually happened in — used to decide
    // what covered what. See docs/plan-unify-framebuffer-on-vram-2026-07-27.md.
    // `disp.x` is a halfword offset while `disp.w` is a PIXEL count; that asymmetry is the PSX SDK's
    // own (24-bit callers pass w = 0x1e0 halfwords then rewrite disp.w = disp.w * 2 / 3 to get 320
    // pixels; 15-bit callers pass 0x140, where halfwords and pixels coincide).
    // 24-bit reads 3 consecutive VRAM bytes per pixel (row stride 2048 bytes); 15-bit reads one
    // halfword per pixel with the same BGR555 decode SampleTexel uses (bits 0-4 R, 5-9 G, 10-14 B).
    public static void ReadDisplayRgb24(byte[] dest, int width, int height)
    {
        if (dest == null || dest.Length < width * height * 3)
        {
            return;
        }

        DISPENV env = ActiveDispEnv;
        int originHalfwordX = env?.disp.x ?? 0;
        int originY = env?.disp.y ?? 0;
        bool rgb24 = env != null && env.isrgb24 != 0;
        int visibleW = env == null || env.disp.w <= 0 ? width : env.disp.w;
        int visibleH = env == null || env.disp.h <= 0 ? height : env.disp.h;
        // `screen` is the display WINDOW on the TV, and its height is the number of VRAM rows the
        // GPU actually scans out of `disp` — 224 for both paths here (InitializeGameSystems and
        // InitPsxDoubleBuffering both set screen.h, alongside screen.y = 8). Without honouring it the
        // presenter shows all 240 rows of the buffer, including the 16 below the scan-out window that
        // the game never paints.
        if (env != null && env.screen.h > 0 && env.screen.h < visibleH)
        {
            visibleH = env.screen.h;
        }

        for (int y = 0; y < height; y++)
        {
            int destRow = y * width * 3;
            int vramRow = ((originY + y) & 0x1ff) * 1024;
            for (int x = 0; x < width; x++)
            {
                byte r = 0, g = 0, b = 0;
                if (x < visibleW && y < visibleH)
                {
                    if (rgb24)
                    {
                        int byteInRow = originHalfwordX * 2 + x * 3;
                        r = ReadVramByte(vramRow, byteInRow);
                        g = ReadVramByte(vramRow, byteInRow + 1);
                        b = ReadVramByte(vramRow, byteInRow + 2);
                    }
                    else
                    {
                        ushort cell = Vram[vramRow + ((originHalfwordX + x) & 0x3ff)];
                        r = (byte)((cell & 0x1f) << 3);
                        g = (byte)(((cell >> 5) & 0x1f) << 3);
                        b = (byte)(((cell >> 10) & 0x1f) << 3);
                    }
                }

                int o = destRow + x * 3;
                dest[o] = r;
                dest[o + 1] = g;
                dest[o + 2] = b;
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only — byte-addresses VRAM, which the 24-bit display
    // mode needs (a 24-bit pixel straddles halfword boundaries). Little-endian within a halfword,
    // matching WriteVramRect's own packing.
    private static byte ReadVramByte(int vramRowBase, int byteInRow)
    {
        int halfword = vramRowBase + ((byteInRow >> 1) & 0x3ff);
        ushort cell = Vram[halfword];
        return (byteInRow & 1) == 0 ? (byte)(cell & 0xff) : (byte)(cell >> 8);
    }

    /**
     * @brief Set default display environment
     *
     * @param env Display environment
     * @param x X position
     * @param y Y position
     * @param w Width
     * @param h Height
     * @return Pointer to display environment
     */
    // GHIDRA: SetDefDispEnv @ 0x800749d8
    // CERTAIN — BLOCKED question CLOSED 2026-07-28 by decompiling the routine itself. It is not a
    // partial fill: it writes disp.x/y/w, zeroes screen.x/y/w/h, zeroes isrgb24, isinter, pad1 and
    // pad0, and writes disp.h last (in the `jr ra` delay slot). Transliterated in that order.
    // The port used to fill only `disp`, with the note "the resets are deliberately NOT performed:
    // InitializeGameSystems sets DISPENV_ARRAY_800bce80[i].isrgb24 = 1 on the two lines immediately
    // BEFORE its SetDefDispEnv calls, so clearing the flag here knocks the title out of 24-bit".
    // That observation was right but the conclusion was backwards: those two ROM assignments really
    // ARE dead — the call that follows overwrites them — and every caller that genuinely wants
    // 24-bit sets the flag AFTER the call instead (LoadPeImgSection70_Boot @0x8006e96c does exactly
    // `SetDefDispEnv(&dispEnv,0,0,0x140,0xf0); dispEnv.isrgb24 = 1;`, and Func_801909b4's own setup
    // block writes byte +0x6d on both overlay frames). Keeping the flag sticky here left
    // DISPENV_ARRAY_800bce80 in 24-bit for the rest of the run, so everything after the title —
    // the main-menu frontend, the Tutorial screen — was scanned out as packed 24-bit: dark,
    // horizontally mis-scaled, with VRAM past x=320 showing as a column of noise.
    public static DISPENV SetDefDispEnv(DISPENV env, int x, int y, int w, int h)
    {
        if (env == null)
        {
            return null;
        }

        env.disp.x = (short)x;
        env.disp.y = (short)y;
        env.disp.w = (short)w;
        env.screen.x = 0;
        env.screen.y = 0;
        env.screen.w = 0;
        env.screen.h = 0;
        env.isrgb24 = 0;
        env.isinter = 0;
        env.pad1 = 0;
        env.pad0 = 0;
        env.disp.h = (short)h;
        return env;
    }

    /**
     * @brief Set default drawing environment
     *
     * @param env Drawing environment
     * @param x X position
     * @param y Y position
     * @param w Width
     * @param h Height
     * @return Pointer to drawing environment
     */
    // JUSTIFICATION: PSX hardware adaptation only — was a no-op stub, which left DRAWENV.clip and
    // DRAWENV.ofs at all-zero for every caller. PutDrawEnv(int) below needs the real values: `ofs`
    // is the drawing offset the GPU adds to every primitive vertex, i.e. the VRAM origin of the
    // draw buffer this DRAWENV selects, and it is what lets the rasterizer convert the absolute
    // VRAM rects the game builds (DR_AREA packets, see RasterizePrimitivePacket's 0xE3 case) into
    // this port's draw-buffer-local menu surface. Only the fields whose contract is exercised are
    // filled; the rest of the real SetDefDrawEnv's defaults (dr_env packet, tpage) are not modelled.
    public static DRAWENV SetDefDrawEnv(DRAWENV env, int x, int y, int w, int h)
    {
        if (env == null)
        {
            return null;
        }

        env.clip.x = (short)x;
        env.clip.y = (short)y;
        env.clip.w = (short)w;
        env.clip.h = (short)h;
        env.ofs[0] = (short)x;
        env.ofs[1] = (short)y;
        env.tw.x = 0;
        env.tw.y = 0;
        env.tw.w = 0;
        env.tw.h = 0;
        env.dtd = 1;
        env.dfe = 0;
        env.isbg = 0;
        env.r0 = 0;
        env.g0 = 0;
        env.b0 = 0;
        return env;
    }

    /**
     * @brief Read TIM image
     *
     * @param timimg TIM image structure
     * @return Pointer to TIM image structure
     */
    public static TIM_IMAGE ReadTIM(TIM_IMAGE timimg)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Interrupt drawing
     *
     * Interrupts drawing after the current polygon is drawn. The return value is
     * the next drawing entry; to resume drawing, pass this value to DrawOTag().
     *
     * @return Next polygon drawing entry (0xffffffff during DMA transfer)
     */
    public static ulong[] BreakDraw()
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Continue drawing interrupted OT
     *
     * Continue to draw the OT interrupted by BreakDraw(). Immediately executes the
     * OT supplied by inst_ot without entering it in the libgpu queue.
     *
     * @param inst_ot OT to execute immediately
     * @param cont_ot OT to draw after inst_ot completes
     */
    public static void ContinueDraw(ulong[] inst_ot, ulong[] cont_ot)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Draw ordering table without queueing
     *
     * Immediately executes an OT without queueing. When drawing is suspended with
     * BreakDraw() after DrawOTag2() is called, confirm completion of data transfer
     * using IsIdleGPU() before restarting with ContinueDraw().
     *
     * @param p Pointer to ordering table
     */
    public static void DrawOTag2(ulong[] p)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Check if drawing suspended by BreakDraw() was completed
     *
     * When drawing is suspended by BreakDraw(), the GPU doesn't stop until drawing
     * of the current primitive is completed. This function checks whether the
     * drawing suspended by BreakDraw() has completed.
     *
     * @param maxcount Number of times to check for idle before returning
     * @return 0 if GPU is idle, 1 if still busy
     */
    public static int IsIdleGPU(int maxcount)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Get display environment
     *
     * @param env Display environment to receive current settings
     * @return Pointer to display environment
     */
    public static DISPENV GetDispEnv(DISPENV env)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Get drawing environment
     *
     * @param env Drawing environment to receive current settings
     * @return Pointer to drawing environment
     */
    public static DRAWENV GetDrawEnv(DRAWENV env)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Get drawing environment (alternative)
     *
     * @param env Drawing environment to receive current settings
     * @return Pointer to drawing environment
     */
    public static DRAWENV GetDrawEnv2(DRAWENV env)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Get current drawing area
     *
     * @param area Rectangle to receive current drawing area
     * @return Pointer to rectangle
     */
    public static RECT GetDrawArea(RECT area)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Get current drawing mode
     *
     * @param dfe Pointer to receive drawing to display area flag
     * @param dtd Pointer to receive dithering flag
     * @param tpage Pointer to receive texture page
     * @param tw Pointer to receive texture window
     */
    public static void GetDrawMode(int[] dfe, int[] dtd, int[] tpage, RECT tw)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Get current drawing offset
     *
     * @param ofs Array to receive offset values [X, Y]
     */
    public static void GetDrawOffset(ushort[] ofs)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Get texture window settings
     *
     * @param tw Rectangle to receive texture window settings
     * @return Pointer to rectangle
     */
    public static RECT GetTexWindow(RECT tw)
    {
        // Do nothing PSX SDK
        return null;
    }

    /**
     * @brief Get ordering table draw enable flag
     *
     * @return ODE flag value
     */
    public static int GetODE()
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Get TIM image size
     *
     * @param addr Pointer to TIM data
     * @return Size of TIM image in bytes
     */
    public static int GetTimSize(ulong[] addr)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Clear frame buffer rectangle (alternative)
     *
     * @param rect Rectangle area
     * @param r Red component
     * @param g Green component
     * @param b Blue component
     * @return 1 on success
     */
    public static int ClearImage2(RECT rect, byte r, byte g, byte b)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Load image from memory to frame buffer (non-blocking)
     *
     * Non-blocking version of LoadImage(). Use IsIdleGPU() to check completion.
     *
     * @param rect Destination rectangle in frame buffer
     * @param p Pointer to image data
     * @return 1 on success
     */
    public static int LoadImage2(RECT rect, ulong[] p)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Store image from frame buffer to memory (non-blocking)
     *
     * Non-blocking version of StoreImage(). Use IsIdleGPU() to check completion.
     *
     * @param rect Source rectangle in frame buffer
     * @param p Pointer to destination buffer
     * @return 1 on success
     */
    public static int StoreImage2(RECT rect, ulong[] p)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Move image within frame buffer (non-blocking)
     *
     * Non-blocking version of MoveImage(). Use IsIdleGPU() to check completion.
     *
     * @param rect Source rectangle
     * @param x Destination X coordinate
     * @param y Destination Y coordinate
     * @return 1 on success
     */
    public static int MoveImage2(RECT rect, int x, int y)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Set STP bit primitive
     *
     * @param p STP primitive
     * @param stp STP bit value
     */
    public static void SetDrawStp(DR_STP p, int stp)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Close kanji font stream
     *
     * @param id Stream ID
     */
    public static void KanjiFntClose(int id)
    {
        // Do nothing PSX SDK
    }

    /**
     * @brief Flush kanji font stream
     *
     * @param id Stream ID
     * @return Pointer to primitive, or NULL if buffer empty
     */
    //extern ulong[] KanjiFntFlush(int id);

    /**
     * @brief Print formatted kanji text to font stream
     *
     * @param id Stream ID
     * @param fmt Format string
     * @return Number of characters printed
     */
    //public static int KanjiFntPrint(int id,  const char* fmt,  ...)
    //{
    //    // Do nothing PSX SDK
    //}

    /**
     * @brief Convert KROM font to TIM format
     *
     * @param sjis Shift-JIS character code
     * @param tim Pointer to TIM image structure
     * @return Pointer to TIM image structure
     */
    //public static TIM_IMAGE* Krom2Tim(ushort sjis, TIM_IMAGE* tim)
    //{
    //    // Do nothing PSX SDK
    //}

    /**
     * @brief Open TMD file
     *
     * @param addr Pointer to TMD data
     * @param obj_no Object number
     * @return The number of polygons comprising the object as a positive integer;
     * on failure, returns 0.
     */
    public static int OpenTMD(ulong[] addr, int obj_no)
    {
        // Do nothing PSX SDK
        return 0;
    }

    /**
     * @brief Read TMD file
     *
     * @param addr Pointer to TMD data
     * @return tmdprim if successful; 0 on failure.
     */
    public static int ReadTMD(ulong[] addr)
    {
        // Do nothing PSX SDK
        return 0;
    }

    // ===== Additions for the shared EffectHandler_* render layer (Remaster/EffectMeshRender.cs) =====
    //
    // WHY THESE ARE NOT NO-OPS, unlike their object-taking siblings above: the object forms operate
    // on typed POLY_*/TILE instances whose `code`/`len` this port's rasterizer never reads back, so
    // stubbing them is harmless. The five overloads below build the packet DIRECTLY inside
    // g_peImgSection11Buffer_raw at a byte cursor, and RasterizeOrderingTable dispatches on exactly
    // the two bytes they write (packet+3 = word length, packet+7 = GPU command code). A packet whose
    // code byte stays 0x00 matches no branch and is silently discarded — the defect already recorded
    // in memory as "a decompiled `*(p+N) = *(p+N)` is a SAVE/RESTORE".
    // JUSTIFICATION: C# language bridge only (byte[]+offset parameter shape); the stored VALUES are
    // straight transliterations of the SDK bodies at the addresses named on each one.

    // GHIDRA: SetTile @ 0x80077C44
    // CERTAIN (full decompilation): tag byte 3 = 3 (TILE is 3 words), code = 0x60.
    public static void SetTile(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 3;
        buf[byteOffset + 7] = 0x60;
    }

    // GHIDRA: SetTile8 @ 0x80077C24
    // CERTAIN (full decompilation): tag byte 3 = 2 (TILE_8 is 2 words), code = 0x68 ('h').
    public static void SetTile8(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 2;
        buf[byteOffset + 7] = 0x68;
    }

    // GHIDRA: SetPolyG3 @ 0x80077B84
    // CERTAIN (full decompilation): tag byte 3 = 6 (POLY_G3 is 28 bytes = tag + 6 words), code = 0x30.
    // RENAMED IN GHIDRA 2026-08-01 (was FUN_80077b84, plate "Possible P14.OBJ/SetPolyG3"): the two
    // constants are exactly the psyq setPolyG3 macro, and the function is byte-for-byte symmetric
    // with its already-named neighbours SetPolyG4 @0x80077BC4, SetTile @0x80077C44 and SetTile8
    // @0x80077C24 — same two-store setter idiom, same 20-byte body, same block. This annotation now
    // matches the Ghidra symbol instead of carrying the stale default name.
    public static void SetPolyG3(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 6;
        buf[byteOffset + 7] = 0x30;
    }

    // GHIDRA: SetPolyG4 @ 0x80077BC4
    // CERTAIN (full decompilation): tag byte 3 = 8 (POLY_G4 is 8 words), code = 0x38 ('8').
    public static void SetPolyG4(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 8;
        buf[byteOffset + 7] = 0x38;
    }

    // GHIDRA: SetPolyF3 @ 0x800711E0 (TITLE.EXE)
    // CERTAIN: both constants read straight off the two-store body,
    // `ori $v0,0x04 / sb $v0,3($a0)` then `ori $v0,0x20 / sb $v0,7($a0)`.
    // Buffer form, for primitives living in a malloc'd pool rather than as objects.
    public static void SetPolyF3(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 4;
        buf[byteOffset + 7] = 0x20;
    }

    // GHIDRA: SetPolyFT3 @ 0x800711F4 (TITLE.EXE)
    // CERTAIN: both constants read straight off the two-store body,
    // `ori $v0,0x07 / sb $v0,3($a0)` then `ori $v0,0x24 / sb $v0,7($a0)`.
    // Buffer form, for primitives living in a malloc'd pool rather than as objects.
    public static void SetPolyFT3(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 7;
        buf[byteOffset + 7] = 0x24;
    }

    // GHIDRA: SetPolyGT3 @ 0x8007121C (TITLE.EXE)
    // CERTAIN: both constants read straight off the two-store body,
    // `ori $v0,0x09 / sb $v0,3($a0)` then `ori $v0,0x34 / sb $v0,7($a0)`.
    // Buffer form, for primitives living in a malloc'd pool rather than as objects.
    public static void SetPolyGT3(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 9;
        buf[byteOffset + 7] = 0x34;
    }

    // GHIDRA: SetPolyF4 @ 0x80071230 (TITLE.EXE)
    // CERTAIN: both constants read straight off the two-store body,
    // `ori $v0,0x05 / sb $v0,3($a0)` then `ori $v0,0x28 / sb $v0,7($a0)`.
    // Buffer form, for primitives living in a malloc'd pool rather than as objects.
    public static void SetPolyF4(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 5;
        buf[byteOffset + 7] = 0x28;
    }

    // GHIDRA: SetPolyFT4 @ 0x80071244 (TITLE.EXE)
    // CERTAIN: both constants read straight off the two-store body,
    // `ori $v0,0x09 / sb $v0,3($a0)` then `ori $v0,0x2C / sb $v0,7($a0)`.
    // Buffer form, for primitives living in a malloc'd pool rather than as objects.
    public static void SetPolyFT4(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 9;
        buf[byteOffset + 7] = 0x2C;
    }

    // GHIDRA: SetPolyGT4 @ 0x8007126C (TITLE.EXE)
    // CERTAIN: both constants read straight off the two-store body,
    // `ori $v0,0x0C / sb $v0,3($a0)` then `ori $v0,0x3C / sb $v0,7($a0)`.
    // Buffer form, for primitives living in a malloc'd pool rather than as objects.
    public static void SetPolyGT4(byte[] buf, int byteOffset)
    {
        buf[byteOffset + 3] = 12;
        buf[byteOffset + 7] = 0x3C;
    }

    // GHIDRA: SetSemiTrans — the psyq macro `p->code |= 2` (no standalone function; it is inlined at
    // every call site, e.g. Render3DRotatedPointWithOutline 0x800d27fc).
    // The `abe == 0` case clears the bit, matching the macro's documented contract; every call site
    // reached from this layer passes 1.
    public static void SetSemiTrans(byte[] buf, int byteOffset, int abe)
    {
        if (abe != 0)
        {
            buf[byteOffset + 7] |= 2;
        }
        else
        {
            buf[byteOffset + 7] &= 0xfd;
        }
    }
}
