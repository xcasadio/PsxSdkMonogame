namespace PsxSdkMonogame;

public static class LibSnd
{
    public delegate void SndTickCallback();

    public class SndVolume
    {
        public ushort left;
        public ushort right;
    }

    public class SndVolume2
    {
        public short left;
        public short right;
    }

    public class SndRegisterAttr
    {
        public SndVolume2 volume;
        public short pitch;
        public short mask;
        public short addr;
        public short adsr1;
        public short adsr2;
    }

    public class SndVoiceStats
    {
        public short vagId;
        public short vabId;
        public ushort pitch;
        public short vol;
        public byte pan;
        public short note;
        public short tone;
        public short prog_num;
        public short prog_actual;
    }

    public class ProgAtr
    {
        public byte tones;
        public byte mvol;
        public byte prior;
        public byte mode;
        public byte mpan;
        public byte reserved0;
        public short attr;
        public uint reserved1;
        public uint reserved2;
    }

    public class VabHdr
    {
        public int form;
        public int ver;
        public int id;
        public uint fsize;
        public ushort reserved0;
        public ushort ps;
        public ushort ts;
        public ushort vs;
        public byte mvol;
        public byte pan;
        public byte attr1;
        public byte attr2;
        public uint reserved1;
    }

    public class VagAtr
    {
        public byte prior;
        public byte mode;
        public byte vol;
        public byte pan;
        public byte center;
        public byte shift;
        public byte min;
        public byte max;
        public byte vibW;
        public byte vibT;
        public byte porW;
        public byte porT;
        public byte pbmin;
        public byte pbmax;
        public byte reserved1;
        public byte reserved2;
        public ushort adsr1;
        public ushort adsr2;
        public short prog;
        public short vag;
        public short[] reserved = new short[4];
    }

    public delegate void sCb();

    public class SsFCALL
    {
        public sCb noteon;
        public sCb programchange;
        public sCb pitchbend;
        public sCb metaevent;
        public sCb[] control = new sCb[13];
        public sCb[] ccentry = new sCb[20];
    }

    //extern _SsFCALL SsFCALL;

    public delegate void SsMarkCallbackProc(short arg0, short arg1, short arg2);

    // GHIDRA: SsInit — no symbol in SLUS_006.62 (find-cross-references: invalid address/symbol), and
    // no other Ss* name resolves either. Checked for this tranche's task D (libspu/libsnd
    // independence): the only Ss* call in the whole binary is SsUtReverbOff @0x8007d054, which calls
    // _SpuInit directly (already covered under LibSpu.SpuStart's notes) rather than through this
    // file's API — so nothing in LibSnd.cs is reachable, and nothing here is transliterated this
    // tranche. Left as "Do nothing PSX SDK" stubs throughout the file.
    public static void SsInit()
    {
        // Do nothing PSX SDK — not linked into SLUS_006.62 (see note above).
    }
    public static void SsInitHot()
    {
        // Do nothing PSX SDK
    }
    public static void SsStart()
    {
        // Do nothing PSX SDK
    }
    public static void SsStart2()
    {
        // Do nothing PSX SDK
    }
    public static void SsEnd()
    {
        // Do nothing PSX SDK
    }
    public static void SsQuit()
    {
        // Do nothing PSX SDK
    }
    public static void SsSetTickMode(int tick_mode)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetTableSize(object table, short s_max, short t_max)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqCalledTbyT()
    {
        // Do nothing PSX SDK
    }
    public static SndTickCallback SsSetTickCallback(SndTickCallback cb)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabOpen(object addr, object vab_header)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabOpenHead(object addr, short vabid)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabOpenHeadSticky(object addr, short vabid, uint sbaddr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabTransBody(object addr, short vabid)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabTransBodyPartly(object addr, uint bufsize, short vabid)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabTransCompleted(short immediateFlag)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabTransfer(object vh_addr, object vb_addr, short vabid, short i_flag)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabFakeHead(object addr, short vabid, uint sbaddr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsVabFakeBody(short vabid)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsVabClose(short vab_id)
    {
        // Do nothing PSX SDK
    }
    public static short SsSeqOpen(object addr, short vab_id)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsSeqOpenJ(object addr, short vab_id)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSeqClose(short seq_access_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqPlay(short seq_access_num, byte play_mode, short l_count)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqPlayPtoP(short access_num, short seq_num, object start_point, object end_point, byte play_mode, short l_count)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqPause(short seq_access_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqReplay(short seq_access_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqStop(short seq_access_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqSetVol(short seq_access_num, short voll, short volr)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqGetVol(short access_num, short seq_num, object voll, object volr)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqSetCrescendo(short seq_access_num, short vol, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqSetDecrescendo(short seq_access_num, short vol, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqSetAccelerando(short seq_access_num, int tempo, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqSetRitardando(short seq_access_num, int tempo, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSeqSetNext(short seq_access_num1, short seq_access_num2)
    {
        // Do nothing PSX SDK
    }
    public static int SsSeqSkip(short access_num, short seq_num, byte unit, short count)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsSepOpen(object addr, short vab_id, short seq_num)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsSepOpenJ(object addr, short vab_id, short seq_num)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSepClose(short sep_access_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepPlay(short sep_access_num, short seq_num, byte play_mode, short l_count)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepPause(short sep_access_num, short seq_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepReplay(short sep_access_num, short seq_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepStop(short sep_access_num, short seq_num)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepSetVol(short sep_access_num, short seq_num, short voll, short volr)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepSetCrescendo(short sep_access_num, short seq_num, short vol, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepSetDecrescendo(short sep_access_num, short seq_num, short vol, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepSetAccelerando(short sep_access_num, short seq_num, int tempo, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsSepSetRitardando(short sep_access_num, short seq_num, int tempo, int v_time)
    {
        // Do nothing PSX SDK
    }
    public static void SsPlayBack(short access_num, short seq_num, short l_count)
    {
        // Do nothing PSX SDK
    }
    public static short SsIsEos(short access_num, short seq_num)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static byte[] SsGetCurrentPoint(short acn, short trn)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int SsSetCurrentPoint(short acn, short trn, object point)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsChannelMute(short acn, short trn, int channels)
    {
        // Do nothing PSX SDK
    }
    public static int SsGetChannelMute(short sep_num, short seq_num)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSetLoop(short access_num, short seq_num, short l_count)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetNext(short ac_no1, short tr_no1, short ac_no2, short tr_no2)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetTempo(short access_num, short seq_num, short tempo)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetMarkCallback(short access_num, short seq_num, SsMarkCallbackProc proc)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetMVol(short voll, short volr)
    {
        // Do nothing PSX SDK
    }
    public static void SsGetMVol(object m_vol)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetRVol(short voll, short volr)
    {
        // Do nothing PSX SDK
    }
    public static void SsGetRVol(object r_vol)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetMute(byte mode)
    {
        // Do nothing PSX SDK
    }
    public static byte SsGetMute()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSetMono()
    {
        // Do nothing PSX SDK
    }
    public static void SsSetStereo()
    {
        // Do nothing PSX SDK
    }
    public static void SsSetAutoKeyOffMode(short mode)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetSerialAttr(byte s_num, byte attr, byte mode)
    {
        // Do nothing PSX SDK
    }
    public static byte SsGetSerialAttr(byte s_num, byte attr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSetSerialVol(byte s_num, short voll, short volr)
    {
        // Do nothing PSX SDK
    }
    public static void SsGetSerialVol(byte s_num, object s_vol)
    {
        // Do nothing PSX SDK
    }
    public static byte SsSetReservedVoice(byte voices)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSetVoiceMask(uint s_voice)
    {
        // Do nothing PSX SDK
    }
    public static uint SsGetVoiceMask()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static byte SsBlockVoiceAllocation()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static byte SsUnBlockVoiceAllocation()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SsAllocateVoices(byte voices, byte priority)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSetVoiceSettings(int voice, object Snd_v_attr)
    {
        // Do nothing PSX SDK
    }
    public static short SsVoiceCheck(int voice, int vabId, short note)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsQueueRegisters(int voice, object SRA)
    {
        // Do nothing PSX SDK
    }
    public static void SsQueueKeyOn(int voices)
    {
        // Do nothing PSX SDK
    }
    public static void SsQueueReverb(int voices, int reverb)
    {
        // Do nothing PSX SDK
    }
    public static short SsUtKeyOn(short vabId, short prog, short tone, short note, short fine, short voll, short volr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtKeyOnV(short voice, short vabId, short prog, short tone, short note, short fine, short voll, short volr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtKeyOff(short voice, short vabId, short prog, short tone, short note)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtKeyOffV(short voice)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsUtAllKeyOff(short mode)
    {
        // Do nothing PSX SDK
    }
    public static int SsVoKeyOn(int vab_pro, int pitch, ushort volL, ushort volR)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SsVoKeyOff(int vab_pro, int pitch)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsUtFlush()
    {
        // Do nothing PSX SDK
    }
    public static short SsUtSetVVol(short vc, short voll, short volr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtGetVVol(short vc, object voll, object volr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtSetDetVVol(short vc, short detvoll, short detvolr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtGetDetVVol(short vc, object detvoll, object detvolr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtAutoVol(short vc, short start_vol, short end_vol, short delta_time)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtAutoPan(short vc, short start_pan, short end_pan, short delta_time)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtChangePitch(short voice, short vabId, short prog, short old_note, short old_fine, short new_note, short new_fine)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtPitchBend(short voice, short vabId, short prog, short note, short pbend)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtChangeADSR(short vc, short vabId, short prog, short old_note, ushort adsr1, ushort adsr2)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static ushort SsPitchFromNote(short note, short fine, byte center, byte shift)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsGetActualProgFromProg(short vabId, short ProgNum)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtSetReverbType(short type)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtGetReverbType()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsUtReverbOn()
    {
        // Do nothing PSX SDK
    }
    public static void SsUtReverbOff()
    {
        // Do nothing PSX SDK
    }
    public static void SsUtSetReverbDepth(short ldepth, short rdepth)
    {
        // Do nothing PSX SDK
    }
    public static void SsUtSetReverbDelay(short delay)
    {
        // Do nothing PSX SDK
    }
    public static void SsUtSetReverbFeedback(short feedback)
    {
        // Do nothing PSX SDK
    }
    public static short SsUtGetVabHdr(short vabId, object vabhdrptr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtSetVabHdr(short vabId, object vabhdrptr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtGetProgAtr(short vabId, short progNum, object progatrptr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtSetProgAtr(short vabId, short progNum, object progatrptr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtGetVagAtr(short vabId, short progNum, short toneNum, object vagatrptr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsUtSetVagAtr(short vabId, short progNum, short toneNum, object vagatrptr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SsUtGetVagAddr(short vabId, short vagId)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SsUtGetVagAddrFromTone(short vabid, short progid, short toneid)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SsUtGetVBaddrInSB(short vabid)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static short SsGetNck()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SsSetNck(short n_clock)
    {
        // Do nothing PSX SDK
    }
    public static void SsSetNoiseOff()
    {
        // Do nothing PSX SDK
    }
    public static void SsSetNoiseOn(short voll, short volr)
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsNoteOn()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetProgramChange()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetPitchBend()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsGetMetaEvent()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetControlChange()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContBankChange()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContDataEntry()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContMainVol()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContPanpot()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContExpression()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContDamper()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContNrpn1()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContNrpn2()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContRpn1()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContRpn2()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContExternal()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsContResetAll()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr0()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr1()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr2()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr3()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr4()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr5()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr6()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr7()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr8()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr9()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr10()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr11()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr12()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr13()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr14()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr15()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr16()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr17()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr18()
    {
        // Do nothing PSX SDK
    }
    public static void dmy_SsSetNrpnVabAttr19()
    {
        // Do nothing PSX SDK
    }
    public static int SpuClearReverbWorkArea(int rev_mode)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SpuGetAllKeysStatus(object status)
    {
        // Do nothing PSX SDK
    }
}
