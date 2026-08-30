using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: the sixteen button masks of a PlayStation digital controller, in the layout the BIOS
// PAD driver writes into the buffer installed by PAD_init: (first status byte << 8) | second
// status byte. Closed from the game itself rather than assumed: FUN_8002165c @ 0x8002165C fills a
// table of fourteen masks per pad with exactly these values. L3 (0x0200) and R3 (0x0400) are the
// two absent from that table, which is correct for a digital pad. This is also what makes the
// FMV players' `PadRead(1) & 0x800` test read as Start.
public static class PadButton
{
    public const ushort L2 = 0x0001;
    public const ushort R2 = 0x0002;
    public const ushort L1 = 0x0004;
    public const ushort R1 = 0x0008;
    public const ushort Triangle = 0x0010;
    public const ushort Circle = 0x0020;
    public const ushort Cross = 0x0040;
    public const ushort Square = 0x0080;
    public const ushort Select = 0x0100;
    public const ushort L3 = 0x0200;
    public const ushort R3 = 0x0400;
    public const ushort Start = 0x0800;
    public const ushort Up = 0x1000;
    public const ushort Right = 0x2000;
    public const ushort Down = 0x4000;
    public const ushort Left = 0x8000;
}

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: desktop replacement for the controller hardware that PAD_dr samples. It publishes the
// same 16-bit active-low halfword per port that the BIOS driver writes, so LibEtc.PadRead keeps
// returning its one's complement unchanged and no original control flow moves.
//
// Threading: the runtime runs on its own thread and is parked at the frame baton while the host
// draws, so the host samples the devices once per frame through Poll() and the runtime only ever
// reads the published snapshot. Nothing here calls into MonoGame from the runtime thread.
public static class PadInputBackend
{
    private static volatile uint s_published = 0xFFFFFFFF;

    // JUSTIFICATION: backend MonoGame only
    // RELATION: opt-in trace of the sampled pad word, on the same DBZ_*_DIAG pattern as the rest of
    // the port. Prints only on change so a held button does not flood the console.
    private static readonly bool s_diag =
        System.Environment.GetEnvironmentVariable("DBZ_PAD_DIAG") == "1";

    private static uint s_lastTraced = 0xFFFFFFFF;

    // JUSTIFICATION: backend MonoGame only
    // RELATION: opt-in button mask OR-ed into port 1, so the pad path can be exercised without the
    // window holding keyboard focus. Windows refuses focus transfer to a background process, which
    // makes a synthetic-keystroke test unreliable; this injects at the same point the devices are
    // sampled, leaving PadRead and every original call site untouched. Value is the active-high
    // PSX mask, e.g. DBZ_PAD_FORCE=0x0800 holds Start.
    private static readonly ushort s_forced = ParseForcedMask();

    // JUSTIFICATION: backend MonoGame only
    private static ushort ParseForcedMask()
    {
        string raw = System.Environment.GetEnvironmentVariable("DBZ_PAD_FORCE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        raw = raw.Trim();
        bool hex = raw.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase);
        return ushort.TryParse(
            hex ? raw.Substring(2) : raw,
            hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out ushort mask)
            ? mask
            : (ushort)0;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: sampled state of both ports, active-low exactly like the BIOS pad buffer. Port 1
    // occupies the low halfword: the runtime tests Start as `& 0x800` on the value PadRead returns
    // (PlayBandaiMovie @ 0x80020DE8 and RunFrameLoop @ 0x800587A8 both do), which places port 1 in
    // the low half.
    public static uint PublishedActiveLow => s_published;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the BIOS pad driver's status byte at buffer offset +0, which the InitPAD path
    // reads as presence (0 = pad present). LibEtc's PadRead path has no equivalent — its single
    // 32-bit word carries buttons only — so this is published separately rather than folded into
    // PublishedActiveLow. Port 1 is always present because the keyboard is its device; port 2 is
    // present only when a second gamepad really is connected. Consumer: LibApi.WriteBiosPadBuffer,
    // and through it SELECT.EXE's FUN_800261E4 @ 0x800261E4.
    private static volatile bool s_port1Connected = true;
    private static volatile bool s_port2Connected;

    public static bool PublishedPort1Connected => s_port1Connected;

    public static bool PublishedPort2Connected => s_port2Connected;

    // JUSTIFICATION: backend MonoGame only
    // RELATION: called once per host frame, from the host thread, to sample keyboard and gamepad.
    public static void Poll()
    {
        ushort port1 = (ushort)(SamplePort(PlayerIndex.One, keyboardIsPort1: true) & ~s_forced);
        ushort port2 = SamplePort(PlayerIndex.Two, keyboardIsPort1: false);
        uint sampled = (uint)(port2 << 16) | port1;
        s_published = sampled;
        s_port1Connected = true;
        s_port2Connected = GamePad.GetState(PlayerIndex.Two).IsConnected;

        if (s_diag && sampled != s_lastTraced)
        {
            s_lastTraced = sampled;
            System.Console.WriteLine($"[pad] active-low={sampled:X8} PadRead={~sampled:X8}");
        }
    }

    // JUSTIFICATION: backend MonoGame only
    // RELATION: maps one desktop device pair to the PSX digital-pad halfword. The returned value is
    // active-low, matching the hardware: a set bit means the button is released.
    private static ushort SamplePort(PlayerIndex player, bool keyboardIsPort1)
    {
        ushort pressed = 0;

        GamePadState gamepad = GamePad.GetState(player);
        if (gamepad.IsConnected)
        {
            if (gamepad.DPad.Up == ButtonState.Pressed) pressed |= PadButton.Up;
            if (gamepad.DPad.Down == ButtonState.Pressed) pressed |= PadButton.Down;
            if (gamepad.DPad.Left == ButtonState.Pressed) pressed |= PadButton.Left;
            if (gamepad.DPad.Right == ButtonState.Pressed) pressed |= PadButton.Right;
            if (gamepad.Buttons.A == ButtonState.Pressed) pressed |= PadButton.Cross;
            if (gamepad.Buttons.B == ButtonState.Pressed) pressed |= PadButton.Circle;
            if (gamepad.Buttons.X == ButtonState.Pressed) pressed |= PadButton.Square;
            if (gamepad.Buttons.Y == ButtonState.Pressed) pressed |= PadButton.Triangle;
            if (gamepad.Buttons.LeftShoulder == ButtonState.Pressed) pressed |= PadButton.L1;
            if (gamepad.Buttons.RightShoulder == ButtonState.Pressed) pressed |= PadButton.R1;
            if (gamepad.Triggers.Left > 0.5f) pressed |= PadButton.L2;
            if (gamepad.Triggers.Right > 0.5f) pressed |= PadButton.R2;
            if (gamepad.Buttons.Start == ButtonState.Pressed) pressed |= PadButton.Start;
            if (gamepad.Buttons.Back == ButtonState.Pressed) pressed |= PadButton.Select;
            if (gamepad.Buttons.LeftStick == ButtonState.Pressed) pressed |= PadButton.L3;
            if (gamepad.Buttons.RightStick == ButtonState.Pressed) pressed |= PadButton.R3;
        }

        if (keyboardIsPort1)
        {
            KeyboardState keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.Up)) pressed |= PadButton.Up;
            if (keys.IsKeyDown(Keys.Down)) pressed |= PadButton.Down;
            if (keys.IsKeyDown(Keys.Left)) pressed |= PadButton.Left;
            if (keys.IsKeyDown(Keys.Right)) pressed |= PadButton.Right;
            if (keys.IsKeyDown(Keys.X)) pressed |= PadButton.Cross;
            if (keys.IsKeyDown(Keys.D)) pressed |= PadButton.Circle;
            if (keys.IsKeyDown(Keys.Z)) pressed |= PadButton.Square;
            if (keys.IsKeyDown(Keys.S)) pressed |= PadButton.Triangle;
            if (keys.IsKeyDown(Keys.A)) pressed |= PadButton.L1;
            if (keys.IsKeyDown(Keys.F)) pressed |= PadButton.R1;
            if (keys.IsKeyDown(Keys.Q)) pressed |= PadButton.L2;
            if (keys.IsKeyDown(Keys.R)) pressed |= PadButton.R2;
            if (keys.IsKeyDown(Keys.Enter)) pressed |= PadButton.Start;
            if (keys.IsKeyDown(Keys.Space)) pressed |= PadButton.Select;
        }

        return (ushort)~pressed;
    }
}
