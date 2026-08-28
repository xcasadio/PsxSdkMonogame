namespace PsxSdkMonogame;

// JUSTIFICATION: C# language bridge only
//
// The compiler's magic-multiply signed division, transliterated as the (magic, shift) pair the raw
// MIPS spells rather than as a C# `/`. Both forms the original emits appear here: the plain one
// (mfhi ; sra s ; subu q,(n sra 31)) and the ADD-BACK one (mfhi ; addu hi,n ; sra s ; subu).
//
// Keeping the constants visible is the point — a `/ 6` in the port hides which divisor the ASM
// actually encoded, and the divisor is only recoverable from the shift (see the magic-divisor note
// in the project's Ghidra lessons).
//
// These lived as 15 private copies across EffectHandlers/, all byte-identical.
public static class MipsMath
{
    public static int MagicDivPlain(int n, uint magic, int shift)
    {
        long p = (long)n * (int)magic;
        int hi = (int)(uint)(p >> 32);
        return (hi >> shift) - (n >> 31);
    }

    public static int MagicDivAddBack(int n, uint magic, int shift)
    {
        long p = (long)n * (int)magic;
        int hi = (int)(uint)(p >> 32);
        return ((int)(hi + n) >> shift) - (n >> 31);
    }
}
