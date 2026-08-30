namespace PsxSdkMonogame;

// THE LIBGCC SOFT-FLOAT ENTRY POINTS SELECT.EXE'S SCREENS CALL.
//
// These are NOT game code. All five are named symbols in the library half of .text, above `start`
// @ 0x800347C4 — the boundary the shape dossier draws between SELECT.EXE's own code
// (0x800213A8..0x800347C3) and the PsyQ/libsn/libgcc tail:
//     __floatsidf @ 0x8004D908   220 bytes
//     __ltdf2     @ 0x8004DA0C   212 bytes
//     __divdf3    @ 0x8004DFE0   584 bytes
//     __muldf3    @ 0x8004E3C0   628 bytes
//     __fixdfsi   @ 0x8004E8FC   212 bytes
// The R3000 has no FPU, so GCC lowered every `double` operation in the C source to a call to one of
// these. They implement IEEE-754 binary64 in integer code.
//
// RULE 13 — REPORTED, NOT HIDDEN: PsxSdkMonogame carries no soft-float module, so there is no SDK
// home for them today. They live here for the same reason MemoryCard.cs holds its one-line `strcat`
// and with the same status: a missing SDK routine, named with its real address, standing in until
// PsxSdkMonogame gains a libgcc file. Nothing in this file is game logic; every body is one C#
// operator.
//
// WHY THE C# OPERATOR IS THE RIGHT STAND-IN, AND WHERE IT IS NOT:
//   __floatsidf / __muldf3 / __divdf3 are exact IEEE-754 binary64 round-to-nearest-even, and so is
//   C# `double` on every platform .NET targets. Same inputs, same bits.
//   __ltdf2's return convention is closed from its body at 0x8004DA0C: it returns 0 when the two
//   operands are bit-equal, and `-uVar2` — that is, -1 — on the "less than" arm; the only caller in
//   this overlay, FUN_800283a0 @ 0x800283A0, tests it as `< 0`.
//   __fixdfsi truncates toward zero, which is what a C `(int)` cast on a double means and what the
//   C# cast means. PARTIAL: the two differ for a value outside int's range — libgcc leaves that
//   unspecified, .NET saturates. No call site in this overlay can reach it: FUN_800283a0 feeds it
//   at most 4096 * 312 / 4096 * 1.2, and FUN_80030ef8 at most 9 * 409.6.
public static class LibGcc
{
    // GHIDRA: __floatsidf @ 0x8004D908
    // JUSTIFICATION: C# language bridge only
    // RELATION: libgcc int -> double. Ghidra renders the returned double as an `undefined8` that the
    // call sites immediately split into a (lo, hi) register pair; the C source it came from just has
    // a double, which is what this returns.
    public static double __floatsidf(int param_1)
    {
        return param_1;
    }

    // GHIDRA: __muldf3 @ 0x8004E3C0
    // JUSTIFICATION: C# language bridge only
    public static double __muldf3(double param_1, double param_2)
    {
        return param_1 * param_2;
    }

    // GHIDRA: __divdf3 @ 0x8004DFE0
    // JUSTIFICATION: C# language bridge only
    public static double __divdf3(double param_1, double param_2)
    {
        return param_1 / param_2;
    }

    // GHIDRA: __subdf3 @ 0x8004E304
    // JUSTIFICATION: C# language bridge only
    // RELATION: 116 bytes — it flips the second operand's sign bit and tail-calls __adddf3. Its one
    // caller in this slice is MenuIntro.FUN_8002ea8c, which uses it to decay the logo's scale.
    public static double __subdf3(double param_1, double param_2)
    {
        return param_1 - param_2;
    }

    // GHIDRA: __fixdfsi @ 0x8004E8FC
    // JUSTIFICATION: C# language bridge only
    public static int __fixdfsi(double param_1)
    {
        return (int)param_1;
    }

    // GHIDRA: __ltdf2 @ 0x8004DA0C
    // JUSTIFICATION: C# language bridge only
    // RELATION: the body at 0x8004DA0C returns 0 for bit-equal operands and negates a "left operand
    // is the smaller" flag otherwise, so "less than" is the negative answer. Unordered (NaN) cannot
    // arise here — both operands come from __floatsidf.
    public static int __ltdf2(double param_1, double param_2)
    {
        if (param_1 < param_2)
        {
            return -1;
        }

        return param_1 == param_2 ? 0 : 1;
    }
}
