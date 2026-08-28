using static PsxSdkMonogame.LibGte;
using static PsxSdkMonogame.MipsMemory;

namespace PsxSdkMonogame;

// JUSTIFICATION: C# language bridge only
//
// Lifts a TYPED PSX struct out of a raw byte[] that stands in for the pointer the original hands
// over. Where MipsMemory.cs holds the scalar loads and stores, this holds the composite ones: the
// original passes `&buf[N]` where a callee's parameter is typed `SVECTOR *` / `MATRIX *` / `short *`
// and LibGte declares those as C# objects, so the fields have to be staged into one.
//
// EVERY MEMBER HERE IS A READ. That is what makes the copy observationally identical to the
// original's pointer pass: each callee named in the RELATION notes below only READS through the
// parameter. Where a callee writes back, the port uses an out-parameter instead — see
// WritePosition3, the one write-back member, which exists for exactly that case.
//
// SPLIT 2026-08-11 out of the class now named MipsMemory: these are not load/store instructions and
// they do not belong in the file that models them.
public static class RawStructs
{
    // RELATION: `&sStack_NN` where the original passes a run of three shorts straight into a renderer
    // that only READS its first three elements (verified in EffectMeshRender.cs). Lifting them out of
    // the byte buffer the original shares with FUN_800ce8f0 is a representation change with no
    // observable difference.
    public static short[] ReadPosition3(byte[] p, int o) =>
        [ReadI16(p, o), ReadI16(p, o + 2), ReadI16(p, o + 4)];

    // RELATION: the write-back half of ReadPosition3, for the callees whose port signature takes a
    // short[3] out parameter where the original writes straight through the caller's pointer. THREE
    // shorts, not four — the original's callee issues exactly three `sh`.
    //
    // EQUIVALENCE: the copies split between calling WriteI16 and WriteU16 per element, and one copy
    // carried the name StoreVector3. All three write the same six bytes.
    public static void WritePosition3(byte[] p, int o, short[] v)
    {
        WriteI16(p, o, v[0]);
        WriteI16(p, o + 2, v[1]);
        WriteI16(p, o + 4, v[2]);
    }

    // RELATION: the original hands a RAW POINTER where a callee's parameter is typed SVECTOR *; the
    // pool callback does exactly that at 0x8018F1D0 (the record itself as Render3DTexturedQuad's
    // rotation), and the HighEffect 0/1 spawn leaves do it again for the SVECTORs held inside
    // SHORT_ARRAY_800e0888 / DAT_800e09f0. LibGte.SVECTOR is a C# object, so the four shorts are
    // staged here. A copy is observationally identical because none of Render3DTexturedQuad,
    // Render3DBillboardSprite or ApplyMatrixSV writes through that parameter.
    //
    // EQUIVALENCE: three per-scene classes spelled this ReadRotation with a byte-identical body, and
    // HighEffect0Particles / HighEffect1Particles spelled the same four reads through BitConverter.
    public static SVECTOR ReadSVector(byte[] p, int o) => new()
    {
        vx = ReadI16(p, o),
        vy = ReadI16(p, o + 2),
        vz = ReadI16(p, o + 4),
        pad = ReadI16(p, o + 6),
    };

    // RELATION: the original passes `(MATRIX *)(buf + N)` straight to a GTE routine — SetRotMatrix,
    // ApplyMatrixSV — while the block it points into is a byte[] in this port. Reads the 32-byte
    // MATRIX image at that offset: m[0..8] as shorts at +0x00..+0x11, then the three 32-bit
    // translation words at +0x14/+0x18/+0x1C. The 2-byte pad at +0x12 is not modelled by
    // LibGte.MATRIX, which is why this cannot be a memcpy.
    //
    // EQUIVALENCE: six copies, in two spellings — ReadBoneMatrix (which additionally hard-coded
    // g_peImgSection10Buffer_raw as the buffer; see EffectMeshRender.ReadBoneMatrix, which is now the
    // one place that binding lives) and ReadMatrixAt (which already took the buffer). Their bodies
    // differed only in ReadI16/ReadI32 versus BitConverter, and in a `do/while` versus a `for`.
    public static MATRIX ReadMatrix(byte[] buf, int o)
    {
        MATRIX m = new MATRIX();
        for (int i = 0; i < 9; i++)
        {
            m.m[i] = ReadI16(buf, o + i * 2);
        }
        m.t[0] = ReadI32(buf, o + 0x14);
        m.t[1] = ReadI32(buf, o + 0x18);
        m.t[2] = ReadI32(buf, o + 0x1c);
        return m;
    }
}
