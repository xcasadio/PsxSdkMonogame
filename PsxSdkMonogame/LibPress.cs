using System;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation — the libpress DecDCT* API surface (PSY-Q's software
// wrapper around the MDEC chip). All PSX-address/PsxRam plumbing lives here; the actual chip
// behaviour (entropy decode, dequant, IDCT, colour conversion, strip assembly) is MdecCore, which
// knows nothing about PSX memory or this game. Game-agnostic — nothing here reads or writes any
// Parasite Eve-specific state.
public static class LibPress
{
    private static readonly MdecCore s_chip = new();

    // The RLE command list buffer format this port's DecDCTvlc2/DecDCTin agree on (an internal
    // convention — only these two functions ever read or write this buffer's bytes, so nothing
    // outside LibPress needs to know it): u16 at +0 = word count N that follows, then N u16 MdecCode
    // words starting at +2.

    public sealed class DecDctEnv
    {
        public byte[] IqY = new byte[64];
        public byte[] IqC = new byte[64];
        public short[] Scale = new short[64];
    }

    // GHIDRA: DecDCTReset @ 0x8010BE3C (wrapper) / core @ 0x8010C0FC
    // PROOF: CERTAIN interface — mode==0 additionally clears the DMA-in/out callbacks (matches the
    // plan's stated behaviour); any other mode leaves callbacks alone. Table re-arm is MdecCore's
    // job (see MdecCore.Reset).
    public static int DecDCTReset(int mode)
    {
        s_chip.Reset();
        if (mode == 0)
        {
            LibEtc.DMACallback(0, null);
            LibEtc.DMACallback(1, null);
        }

        return 0;
    }

    // GHIDRA: DecDCTGetEnv @ 0x8010BE70
    // PROOF: CERTAIN interface — copies the chip's current IQ-Y/IQ-C/scale tables into an
    // env object at PSX address `envAddr` (64 bytes IQ-Y, 64 bytes IQ-C, 64 u16 scale = 192 bytes).
    public static int DecDCTGetEnv(int envAddr)
    {
        s_chip.GetEnv(out byte[] iqY, out byte[] iqC, out short[] scale);
        if (!PsxRam.WriteBytes(envAddr, iqY)) return -1;
        if (!PsxRam.WriteBytes(envAddr + 64, iqC)) return -1;
        byte[] scaleBytes = new byte[128];
        for (int i = 0; i < 64; i++)
        {
            scaleBytes[i * 2] = (byte)scale[i];
            scaleBytes[i * 2 + 1] = (byte)(scale[i] >> 8);
        }

        return PsxRam.WriteBytes(envAddr + 128, scaleBytes) ? 0 : -1;
    }

    // GHIDRA: DecDCTPutEnv @ 0x8010BEFC
    // PROOF: CERTAIN interface — inverse of DecDCTGetEnv; replaces the chip's tables from `envAddr`.
    public static int DecDCTPutEnv(int envAddr)
    {
        byte[] iqY = PsxRam.ReadBytes(envAddr, 64);
        byte[] iqC = PsxRam.ReadBytes(envAddr + 64, 64);
        byte[] scaleBytes = PsxRam.ReadBytes(envAddr + 128, 128);
        if (iqY == null || iqC == null || scaleBytes == null) return -1;

        short[] scale = new short[64];
        for (int i = 0; i < 64; i++)
        {
            scale[i] = (short)(scaleBytes[i * 2] | (scaleBytes[i * 2 + 1] << 8));
        }

        s_chip.PutEnv(iqY, iqC, scale);
        return 0;
    }

    // GHIDRA: DecDCTvlcSize @ 0x8010C4CC
    // PROOF: interface CERTAIN ("sets the max output length state for the VLC stage") — ADAPTED
    // internals: this port enforces it as a hard cap on DecDCTvlc2's RLE output word count (see
    // MdecCore.VlcDecode), giving it real teeth as an overflow guard rather than leaving it
    // store-only, since this slice's own harness needs exactly that guard to catch a runaway decode
    // safely instead of writing past its scratch buffer.
    public static int DecDCTvlcSize(int n)
    {
        s_chip.SetVlcMaxWords(n);
        return 0;
    }

    // GHIDRA: DecDCTvlc @ 0x8010C4FC
    // PROOF: interface CERTAIN — the shipped-internal-table variant. This port has only one VLC
    // decode path (MdecCore.VlcDecode, spec-based — see DecDCTvlc2's note below), so this is a thin
    // alias with tableAddr=0.
    public static int DecDCTvlc(int bsAddr, int outAddr) => DecDCTvlc2(bsAddr, outAddr, 0);

    // GHIDRA: DecDCTvlc2 @ 0x8010C89C
    // PROOF: interface CERTAIN (caller-supplied VLC table address) — IMPLEMENTATION DECISION
    // (approved): this decoder is spec-based (BS v2 entropy coding is public/standard — see
    // MdecCore's class header). `tableAddr` is accepted and recorded (below) but never read: the
    // shipped table is an implementation detail of the console's table-driven decoder, and the
    // output contract (the MDEC RLE command list) is what matters and is fully preserved by this
    // port's spec-based decode.
    //
    // `bsAddr` points at the FULL demuxed BS frame, 8-byte header included (exactly what the St ring
    // hands the game and what this slice's FmvDecode harness passes straight through) — MdecCore
    // parses that header itself (see MdecCore.VlcDecode's note on the 8-vs-16-byte header
    // divergence found empirically this slice).
    public static int LastVlc2TableAddr;

    public static int DecDCTvlc2(int bsAddr, int outAddr, int tableAddr)
    {
        LastVlc2TableAddr = tableAddr; // recorded only — see the JUSTIFICATION above.

        var resolved = PsxRam.AddressResolver?.Invoke(bsAddr);
        if (resolved == null)
        {
            return -1;
        }

        var (buf, offset) = resolved.Value;
        int available = buf.Length - offset;
        if (available <= 0)
        {
            return -1;
        }

        // Read the whole rest of the backing scratch region rather than a fixed cap: a real BS
        // frame is self-terminating (macroblock-count- and EOB-driven, not byte-length-driven — see
        // MdecCore.VlcDecode), so there is no length this call needs up front; the only requirement
        // is that "enough" bytes are actually present in the resolved buffer.
        byte[] frameData = new byte[available];
        Array.Copy(buf, offset, frameData, 0, available);

        ushort[] rle = s_chip.VlcDecode(frameData, out int mbCount, out string error);
        if (rle == null)
        {
            Console.WriteLine($"[LibPress] DecDCTvlc2 FAIL: {error} (macroblocks decoded: {mbCount})");
            return -1;
        }

        byte[] outBytes = new byte[2 + rle.Length * 2];
        outBytes[0] = (byte)rle.Length;
        outBytes[1] = (byte)(rle.Length >> 8);
        for (int i = 0; i < rle.Length; i++)
        {
            outBytes[2 + i * 2] = (byte)rle[i];
            outBytes[2 + i * 2 + 1] = (byte)(rle[i] >> 8);
        }

        return PsxRam.WriteBytes(outAddr, outBytes) ? 0 : -1;
    }

    // GHIDRA: DecDCTin @ 0x8010BFA0
    // PROOF: interface CERTAIN (feeds an RLE command list into the chip; mode bit0 = 15/24-bit
    // output depth selector, bit1 = STP/bit15 handling) — ADAPTED internals: see MdecCore.FeedRleList
    // for why this port decodes the whole picture eagerly here instead of streaming macroblocks
    // through a FIFO as DecDCTout is pumped.
    //
    // `bufAddr` is this port's own RLE-list buffer convention (u16 length prefix, see the class
    // header note) — the same buffer DecDCTvlc2 just wrote.
    public static int DecDCTin(int bufAddr, int mode)
    {
        byte[] header = PsxRam.ReadBytes(bufAddr, 2);
        if (header == null) return -1;
        int count = header[0] | (header[1] << 8);

        byte[] wordBytes = PsxRam.ReadBytes(bufAddr + 2, count * 2);
        if (wordBytes == null) return -1;

        ushort[] words = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            words[i] = (ushort)(wordBytes[i * 2] | (wordBytes[i * 2 + 1] << 8));
        }

        if (!s_chip.FeedRleList(words, mode, out int mbCount, out string error))
        {
            Console.WriteLine($"[LibPress] DecDCTin FAIL: {error} (macroblocks parsed: {mbCount})");
            return -1;
        }

        return 0;
    }

    // GHIDRA: DecDCTout wrapper @ 0x8010C01C / raw @ 0x8010C27C
    // PROOF: interface CERTAIN (drains the next decoded strip into bufAddr, sizeWords 32-bit words)
    // — ADAPTED internals: on console this is driven by the DMA1-complete IRQ; on desktop the copy
    // happens synchronously right here, then the channel-1 callback (registered via
    // DecDCToutCallback/LibEtc.DMACallback) is invoked.
    //
    // S3 (2026-08-29) DELIVERY-ORDER FIX: the callback must NOT be invoked inline when DecDCTout
    // is itself called from inside a channel-1 callback. The game's FMV callback (a transliterated
    // original) does, in this order: (a) issue DecDCTout for the NEXT strip into the OTHER of two
    // strip buffers, then (b) LoadImage the strip that JUST finished from the current buffer. On
    // console (a) is asynchronous, so (b) reads the buffer before the chip ever reuses it. With a
    // recursive inline invoke, (a) would run the ENTIRE remaining strip chain — overwriting each
    // buffer several times — before (b) ever executed, uploading stale pixels for all but the last
    // two strips (verified by tracing the transliterated callback: with N strips, strip k's upload
    // would see strip k+2's bytes for every k <= N-2). Deferring nested completions until the
    // current callback returns reproduces the console's serialized IRQ delivery: write strip,
    // return, THEN fire its completion — the double-buffer pipeline works exactly as on hardware.
    private static bool s_outCallbackActive;
    private static int s_outCallbackPending;

    public static int DecDCTout(int bufAddr, int sizeWords)
    {
        if (!s_chip.TryDequeueStrip(out byte[] strip))
        {
            return -1; // no more strips queued for this picture
        }

        int byteCount = Math.Min(sizeWords * 4, strip.Length);
        byte[] toWrite = byteCount == strip.Length ? strip : SubArray(strip, byteCount);
        if (!PsxRam.WriteBytes(bufAddr, toWrite))
        {
            return -1;
        }

        if (s_outCallbackActive)
        {
            // Called from inside a channel-1 callback: the data is written, but the completion is
            // queued for after the current callback returns (see the delivery-order note above).
            s_outCallbackPending++;
            return 0;
        }

        s_outCallbackActive = true;
        try
        {
            LibEtc.GetDmaCallback(1)?.Invoke();
            while (s_outCallbackPending > 0)
            {
                s_outCallbackPending--;
                Action cb = LibEtc.GetDmaCallback(1);
                if (cb == null)
                {
                    s_outCallbackPending = 0;
                    break;
                }

                cb.Invoke();
            }
        }
        finally
        {
            s_outCallbackActive = false;
            s_outCallbackPending = 0;
        }

        return 0;
    }

    private static byte[] SubArray(byte[] source, int count)
    {
        byte[] result = new byte[count];
        Array.Copy(source, result, count);
        return result;
    }

    // GHIDRA: DecDCTinSync @ 0x8010C03C
    // PROOF: interface CERTAIN — desktop decode is fully synchronous (DecDCTin already ran to
    // completion by the time it returns), so this always reports idle (0), matching the "no pending
    // in-transfer" state a real sync call would see once the transfer it's waiting on has finished.
    public static int DecDCTinSync(int mode) => 0;

    // GHIDRA: DecDCToutSync @ 0x8010C078
    // PROOF: interface CERTAIN — same reasoning as DecDCTinSync: DecDCTout's copy + callback are
    // already complete by the time it returns, so this always reports idle (0).
    public static int DecDCToutSync(int mode) => 0;

    // GHIDRA: DecDCTinCallback @ 0x8010C0B4
    // PROOF: CERTAIN — thin wrapper over DMACallback(0, fn), matching the original.
    public static Action DecDCTinCallback(Action fn) => (Action)LibEtc.DMACallback(0, fn);

    // GHIDRA: DecDCToutCallback @ 0x8010C0D8
    // PROOF: CERTAIN — thin wrapper over DMACallback(1, fn), matching the original.
    public static Action DecDCToutCallback(Action fn) => (Action)LibEtc.DMACallback(1, fn);
}
