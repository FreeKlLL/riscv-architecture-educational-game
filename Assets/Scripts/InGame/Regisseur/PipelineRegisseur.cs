using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Full snapshot of the 5-stage pipeline's state - every pipeline register's latched
/// fields (including the extra bookkeeping fields needed for hazard detection: Rs1E/Rs2E
/// and the auto-decoded RegWrite/MemToReg control bits), the PC, register file, both
/// memories, and every control-signal selection. Used for save/restore (the "previous
/// tick" rewind button) exactly like the other processor levels.
/// </summary>
public struct PipelineState
{
    public int PcValue;

    // F/D pipeline register (Fetch -> Decode)
    public int FdInstrValue;
    public int FdPcPlus4Value;
    public int FdPcValue;

    // D/E pipeline register (Decode -> Execute)
    public int DeRd1Value;
    public int DeRd2Value;
    public int DePcValue;
    public int DeRdValue;
    public int DeImmExtValue;
    public int DePcPlus4Value;
    public int DeRs1Value;         // source register *number* (rs1), needed for forwarding
    public int DeRs2Value;         // source register *number* (rs2), needed for forwarding
    public bool DeRegWriteValue;   // auto-decoded from the instruction's opcode
    public bool DeMemToRegValue;   // auto-decoded: true if this instruction is a load

    // E/M pipeline register (Execute -> Memory)
    public int EmAluResultValue;
    public int EmWriteDataValue;
    public int EmRdValue;
    public int EmPcPlus4Value;
    public bool EmRegWriteValue;

    // M/W pipeline register (Memory -> Writeback)
    public int MwAluResultValue;
    public int MwReadDataValue;
    public int MwRdValue;
    public int MwPcPlus4Value;
    public bool MwRegWriteValue;

    public int[] RegisterFieldValue;

    public int FirstInstructionMemoryValue;
    public int SecondInstructionMemoryValue;
    public int ThirdInstructionMemoryValue;
    public int FourthInstructionMemoryValue;

    public int FirstDataMemoryValue;
    public int SecondDataMemoryValue;
    public int ThirdDataMemoryValue;
    public int FourthDataMemoryValue;

    public bool PcWe;

    public int AluOperation;
    public int ExtenderOperation;
    public int PcPlus4AluOperation;
    public int BtaAluOperation;

    public int MuxPcPath;     // 0 = PCPlus4F, 1 = PCTargetE (taking this path is what triggers a flush)
    public int MuxSrcBPath;   // 0 = RD2E (post-forwarding), 1 = ImmExtE
    public int MuxResultPath; // 0 = ALUResultW, 1 = ReadDataW, 2 = PCPlus4W
}

/// <summary>
/// Every wire from the pipelined datapath diagram, grouped by stage, plus the four
/// forwarding paths added for hazard handling (not on the original diagram, but this is
/// exactly where a real forwarding unit taps in - from the E/M and M/W registers back to
/// the EX-stage ALU inputs).
/// </summary>
public class PipelineBusSegments : IBusSegmentProvider
{
    [Header("IF - Instruction Fetch")]
    [Tooltip("PC MUX output (PCF') -> PC register")]
    public LineRenderer pcMuxToPcReg;
    [Tooltip("PC -> Instruction Memory address")]
    public LineRenderer pcToInstrMem;
    [Tooltip("PC -> PC+4 adder input A")]
    public LineRenderer pcToPcPlus4Adder;
    [Tooltip("Constant 4 -> PC+4 adder input B")]
    public LineRenderer constFourToPcPlus4Adder;
    [Tooltip("Instruction Memory read data -> F/D register (becomes InstrD)")]
    public LineRenderer instrMemToFdReg;
    [Tooltip("PC+4 (PCPlus4F) -> F/D register (becomes PCPlus4D)")]
    public LineRenderer pcPlus4ToFdReg;
    [Tooltip("PC (PCF) -> F/D register (becomes PCD, needed later for the branch target)")]
    public LineRenderer pcToFdReg;

    [Header("ID - Instruction Decode")]
    [Tooltip("F/D register InstrD[19:15] -> Register File A1 (rs1)")]
    public LineRenderer fdRegToRegFileA1;
    [Tooltip("F/D register InstrD[24:20] -> Register File A2 (rs2)")]
    public LineRenderer fdRegToRegFileA2;
    [Tooltip("F/D register InstrD[31:7] -> Extend unit")]
    public LineRenderer fdRegToExtend;
    [Tooltip("F/D register InstrD[11:7] -> RdD (destination register tag, threaded through the pipeline)")]
    public LineRenderer fdRegToRdD;
    [Tooltip("Register File RD1 -> D/E register (becomes RD1E)")]
    public LineRenderer regFileRd1ToDeReg;
    [Tooltip("Register File RD2 -> D/E register (becomes RD2E)")]
    public LineRenderer regFileRd2ToDeReg;
    [Tooltip("Extend unit output (ImmExtD) -> D/E register (becomes ImmExtE)")]
    public LineRenderer extendToDeReg;
    [Tooltip("RdD -> D/E register (becomes RdE)")]
    public LineRenderer rdDToDeReg;
    [Tooltip("F/D register PCD -> D/E register (becomes PCE)")]
    public LineRenderer fdRegPcToDeReg;
    [Tooltip("F/D register PCPlus4D -> D/E register (becomes PCPlus4E)")]
    public LineRenderer fdRegPcPlus4ToDeReg;

    [Header("EX - Execute")]
    [Tooltip("D/E register RD1E (post-forwarding) -> ALU input A (SrcAE)")]
    public LineRenderer deRegRd1ToAlu;
    [Tooltip("D/E register RD2E (post-forwarding) -> SrcB MUX input [0]")]
    public LineRenderer deRegRd2ToSrcBMux;
    [Tooltip("D/E register ImmExtE -> SrcB MUX input [1]")]
    public LineRenderer deRegImmExtToSrcBMux;
    [Tooltip("SrcB MUX output (SrcBE) -> ALU input B")]
    public LineRenderer srcBMuxToAlu;
    [Tooltip("Forwarded RD2 (post-forwarding) -> WriteDataE (store data)")]
    public LineRenderer deRegRd2ToWriteDataE;
    [Tooltip("D/E register PCE -> branch-target adder input A")]
    public LineRenderer deRegPcToBtaAdder;
    [Tooltip("D/E register ImmExtE -> branch-target adder input B")]
    public LineRenderer deRegImmExtToBtaAdder;
    [Tooltip("Branch-target adder output (PCTargetE) -> back to the PC MUX (long feedback wire)")]
    public LineRenderer btaAdderToPcMux;
    [Tooltip("ALU result -> E/M register (becomes ALUResultM)")]
    public LineRenderer aluToEmReg;
    [Tooltip("WriteDataE -> E/M register (becomes WriteDataM)")]
    public LineRenderer writeDataEToEmReg;
    [Tooltip("D/E register RdE -> E/M register (becomes RdM)")]
    public LineRenderer deRegRdToEmReg;
    [Tooltip("D/E register PCPlus4E -> E/M register (becomes PCPlus4M)")]
    public LineRenderer deRegPcPlus4ToEmReg;

    [Header("EX - Forwarding paths (hazard handling)")]
    [Tooltip("E/M register ALUResultM forwarded back into the ALU-A input path")]
    public LineRenderer emForwardToAluA;
    [Tooltip("M/W register ResultW forwarded back into the ALU-A input path")]
    public LineRenderer mwForwardToAluA;
    [Tooltip("E/M register ALUResultM forwarded back into the SrcB/WriteDataE path")]
    public LineRenderer emForwardToAluB;
    [Tooltip("M/W register ResultW forwarded back into the SrcB/WriteDataE path")]
    public LineRenderer mwForwardToAluB;

    [Header("MEM - Memory")]
    [Tooltip("E/M register ALUResultM -> Data Memory address")]
    public LineRenderer emRegAluResultToDataMem;
    [Tooltip("E/M register WriteDataM -> Data Memory write data")]
    public LineRenderer emRegWriteDataToDataMem;
    [Tooltip("E/M register ALUResultM -> M/W register (becomes ALUResultW)")]
    public LineRenderer emRegAluResultToMwReg;
    [Tooltip("Data Memory read data -> M/W register (becomes ReadDataW)")]
    public LineRenderer dataMemToMwReg;
    [Tooltip("E/M register RdM -> M/W register (becomes RdW)")]
    public LineRenderer emRegRdToMwReg;
    [Tooltip("E/M register PCPlus4M -> M/W register (becomes PCPlus4W)")]
    public LineRenderer emRegPcPlus4ToMwReg;

    [Header("WB - Writeback")]
    [Tooltip("M/W register ALUResultW -> Result MUX input [00]")]
    public LineRenderer mwRegAluResultToResultMux;
    [Tooltip("M/W register ReadDataW -> Result MUX input [01]")]
    public LineRenderer mwRegReadDataToResultMux;
    [Tooltip("M/W register PCPlus4W -> Result MUX input [10]")]
    public LineRenderer mwRegPcPlus4ToResultMux;
    [Tooltip("Result MUX output (ResultW) -> Register File WD3 (long feedback wire)")]
    public LineRenderer resultMuxToRegFileWd3;
    [Tooltip("M/W register RdW -> Register File A3 (write address feedback)")]
    public LineRenderer mwRegRdToRegFileA3;

    public void RegisterAll(BusController c)
    {
        c.RegisterSegment(pcMuxToPcReg);
        c.RegisterSegment(pcToInstrMem);
        c.RegisterSegment(pcToPcPlus4Adder);
        c.RegisterSegment(constFourToPcPlus4Adder);
        c.RegisterSegment(instrMemToFdReg);
        c.RegisterSegment(pcPlus4ToFdReg);
        c.RegisterSegment(pcToFdReg);

        c.RegisterSegment(fdRegToRegFileA1);
        c.RegisterSegment(fdRegToRegFileA2);
        c.RegisterSegment(fdRegToExtend);
        c.RegisterSegment(fdRegToRdD);
        c.RegisterSegment(regFileRd1ToDeReg);
        c.RegisterSegment(regFileRd2ToDeReg);
        c.RegisterSegment(extendToDeReg);
        c.RegisterSegment(rdDToDeReg);
        c.RegisterSegment(fdRegPcToDeReg);
        c.RegisterSegment(fdRegPcPlus4ToDeReg);

        c.RegisterSegment(deRegRd1ToAlu);
        c.RegisterSegment(deRegRd2ToSrcBMux);
        c.RegisterSegment(deRegImmExtToSrcBMux);
        c.RegisterSegment(srcBMuxToAlu);
        c.RegisterSegment(deRegRd2ToWriteDataE);
        c.RegisterSegment(deRegPcToBtaAdder);
        c.RegisterSegment(deRegImmExtToBtaAdder);
        c.RegisterSegment(btaAdderToPcMux);
        c.RegisterSegment(aluToEmReg);
        c.RegisterSegment(writeDataEToEmReg);
        c.RegisterSegment(deRegRdToEmReg);
        c.RegisterSegment(deRegPcPlus4ToEmReg);

        c.RegisterSegment(emForwardToAluA);
        c.RegisterSegment(mwForwardToAluA);
        c.RegisterSegment(emForwardToAluB);
        c.RegisterSegment(mwForwardToAluB);

        c.RegisterSegment(emRegAluResultToDataMem);
        c.RegisterSegment(emRegWriteDataToDataMem);
        c.RegisterSegment(emRegAluResultToMwReg);
        c.RegisterSegment(dataMemToMwReg);
        c.RegisterSegment(emRegRdToMwReg);
        c.RegisterSegment(emRegPcPlus4ToMwReg);

        c.RegisterSegment(mwRegAluResultToResultMux);
        c.RegisterSegment(mwRegReadDataToResultMux);
        c.RegisterSegment(mwRegPcPlus4ToResultMux);
        c.RegisterSegment(resultMuxToRegFileWd3);
        c.RegisterSegment(mwRegRdToRegFileA3);
    }
}

public class PipelineRegisseur : BaseLevelRegisseur<PipelineState, PipelineBusSegments>
{
    [Header("Fetch")]
    [SerializeField] private RegisterVisualizer registerPCVisualizer;
    [SerializeField] private MultiplexerVisualizer pcMuxVisualizer;
    [SerializeField] private AluVisualiser pcPlus4AluVisualizer;
    [SerializeField] private InstructionDataMemoryVisualizer instructionMemoryVisualizer;

    [Header("F/D Register")]
    [SerializeField] private RegisterVisualizer fdInstrVisualizer;
    [SerializeField] private RegisterVisualizer fdPcPlus4Visualizer;
    [SerializeField] private RegisterVisualizer fdPcVisualizer;

    [Header("Decode")]
    [SerializeField] private RegisterFileVisualizer registerFileVisualizer;
    [SerializeField] private ExtenderVisualizer extenderVisualizer;

    [Header("D/E Register")]
    [SerializeField] private RegisterVisualizer deRd1Visualizer;
    [SerializeField] private RegisterVisualizer deRd2Visualizer;
    [SerializeField] private RegisterVisualizer dePcVisualizer;
    [SerializeField] private RegisterVisualizer deRdVisualizer;
    [SerializeField] private RegisterVisualizer deImmExtVisualizer;
    [SerializeField] private RegisterVisualizer dePcPlus4Visualizer;

    [Header("Execute")]
    [SerializeField] private MultiplexerVisualizer srcBMuxVisualizer;
    [SerializeField] private AluVisualiser aluVisualizer;
    [SerializeField] private AluVisualiser btaAluVisualizer;

    [Header("E/M Register")]
    [SerializeField] private RegisterVisualizer emAluResultVisualizer;
    [SerializeField] private RegisterVisualizer emWriteDataVisualizer;
    [SerializeField] private RegisterVisualizer emRdVisualizer;
    [SerializeField] private RegisterVisualizer emPcPlus4Visualizer;

    [Header("Memory")]
    [SerializeField] private InstructionDataMemoryVisualizer dataMemoryVisualizer;

    [Header("M/W Register")]
    [SerializeField] private RegisterVisualizer mwAluResultVisualizer;
    [SerializeField] private RegisterVisualizer mwReadDataVisualizer;
    [SerializeField] private RegisterVisualizer mwRdVisualizer;
    [SerializeField] private RegisterVisualizer mwPcPlus4Visualizer;

    [Header("Writeback")]
    [SerializeField] private MultiplexerVisualizer resultMuxVisualizer;

    [Header("Initial values for level")]
    public static PipelineProcessorInitialState Initial;

    // RISC-V opcodes this simulator understands (matches RiscVDecoder).
    private const int OP_R_TYPE = 0x33;
    private const int OP_I_ALU = 0x13;
    private const int OP_LOAD = 0x03;
    private const int OP_STORE = 0x23;
    private const int OP_BRANCH = 0x63;
    private const int OP_JAL = 0x6F;

    // --- Internal simulation state -----------------------------------------------------

    private Register _pc;

    private Register _fdInstr;
    private Register _fdPcPlus4;
    private Register _fdPc;

    private Register _deRd1;
    private Register _deRd2;
    private Register _dePc;
    private Register _deRd;
    private Register _deImmExt;
    private Register _dePcPlus4;
    private Register _deRs1;        // 0/1-style int Registers reused for small "tag" values
    private Register _deRs2;
    private Register _deRegWrite;   // 0 or 1, standing in for a bool through the two-phase clock
    private Register _deMemToReg;   // 0 or 1

    private Register _emAluResult;
    private Register _emWriteData;
    private Register _emRd;
    private Register _emPcPlus4;
    private Register _emRegWrite;

    private Register _mwAluResult;
    private Register _mwReadData;
    private Register _mwRd;
    private Register _mwPcPlus4;
    private Register _mwRegWrite;

    private RegisterFile _registerFile;
    private DataInstMemory _instructionMemory;
    private DataInstMemory _dataMemory;

    protected void Awake()
    {
        levelManager.SetLevelDialogue(Initial.customDialogueGraph);
    }

    protected override IEnumerator ReverseBusVisualizations()
    {
        throw new System.NotImplementedException();
    }

    protected override void Start()
    {
        base.Start();
        buses.RegisterAll(busController);
    }

    protected override void OnLevelStart()
    {
        if (Initial != null) levelTargetDescription = Initial.levelTarget;

        _pc = new Register(Initial.pcRegisterInitialValue) { WriteEnable = true };

        _fdInstr = new Register { WriteEnable = true };
        _fdPcPlus4 = new Register { WriteEnable = true };
        _fdPc = new Register { WriteEnable = true };

        _deRd1 = new Register { WriteEnable = true };
        _deRd2 = new Register { WriteEnable = true };
        _dePc = new Register { WriteEnable = true };
        _deRd = new Register { WriteEnable = true };
        _deImmExt = new Register { WriteEnable = true };
        _dePcPlus4 = new Register { WriteEnable = true };
        _deRs1 = new Register { WriteEnable = true };
        _deRs2 = new Register { WriteEnable = true };
        _deRegWrite = new Register { WriteEnable = true };
        _deMemToReg = new Register { WriteEnable = true };

        _emAluResult = new Register { WriteEnable = true };
        _emWriteData = new Register { WriteEnable = true };
        _emRd = new Register { WriteEnable = true };
        _emPcPlus4 = new Register { WriteEnable = true };
        _emRegWrite = new Register { WriteEnable = true };

        _mwAluResult = new Register { WriteEnable = true };
        _mwReadData = new Register { WriteEnable = true };
        _mwRd = new Register { WriteEnable = true };
        _mwPcPlus4 = new Register { WriteEnable = true };
        _mwRegWrite = new Register { WriteEnable = true };

        _registerFile = new RegisterFile { RegisterWriteEnable = false };
        _registerFile.InitializeRegisters(new[]
        {
            0, 1, 39, 43, 5, 6, 8, 40,
            3, 39, 13, 56, 63, 20, 50, 51,
            0, 12, 53, 65, 29, 60, 61, 0,
            25, 54, 0, 28, 70, 30, 31, 0
        });

        _instructionMemory = new DataInstMemory();
        _instructionMemory.LoadWord(0, Initial.firstInstructionWord);
        _instructionMemory.LoadWord(4, Initial.secondInstructionWord);
        _instructionMemory.LoadWord(8, Initial.thirdInstructionWord);
        _instructionMemory.LoadWord(12, Initial.fourthInstructionWord);

        _dataMemory = new DataInstMemory();
        _dataMemory.LoadWord(0, Initial.firstDataWord);
        _dataMemory.LoadWord(4, Initial.secondDataWord);
        _dataMemory.LoadWord(8, Initial.thirdDataWord);
        _dataMemory.LoadWord(12, Initial.fourthDataWord);

        UpdateVisualizers();
    }

    // Small bundle of everything ComputeSignals figures out combinationally in one tick,
    // including the hazard unit's forwarding/stall/flush decisions. Deliberately reads
    // only .Output / .Registers / .Memory "snapshots" - never .ReadData / anything that
    // itself depends on PreClockUpdate() having run - so this is a pure function of the
    // state as of the *start* of the tick, safe to call before touching any component.
    private readonly struct PipelineSignals
    {
        public readonly int FetchedInstr;
        public readonly int Rs1D, Rs2D, RdD, Rd1D, Rd2D, ImmExtD;
        public readonly bool RegWriteD, MemToRegD;

        public readonly int SrcAE, SrcBE, ForwardedRd2E, AluResultE, WriteDataE, BtaE;
        public readonly bool AluZeroE;

        // 0 = no forward (used the plain D/E-latched value), 1 = forwarded from E/M, 2 = forwarded from M/W.
        public readonly int ForwardASource, ForwardBSource;

        public readonly int MemReadDataW; // pure lookup of Data Memory at the E/M-latched address

        public readonly int PcPlus4F, PcNext;
        public readonly bool PcSrcE;   // branch is being taken this cycle (per the player's own MUX choice)
        public readonly bool StallF, StallD, FlushD, FlushE;

        public readonly int ResultW;

        public PipelineSignals(int fetchedInstr, int rs1D, int rs2D, int rdD, int rd1D, int rd2D, int immExtD,
            bool regWriteD, bool memToRegD,
            int srcAE, int srcBE, int forwardedRd2E, int aluResultE, bool aluZeroE, int writeDataE, int btaE,
            int forwardASource, int forwardBSource, int memReadDataW,
            int pcPlus4F, int pcNext, bool pcSrcE, bool stallF, bool stallD, bool flushD, bool flushE, int resultW)
        {
            FetchedInstr = fetchedInstr;
            Rs1D = rs1D; Rs2D = rs2D; RdD = rdD; Rd1D = rd1D; Rd2D = rd2D; ImmExtD = immExtD;
            RegWriteD = regWriteD; MemToRegD = memToRegD;
            SrcAE = srcAE; SrcBE = srcBE; ForwardedRd2E = forwardedRd2E;
            AluResultE = aluResultE; AluZeroE = aluZeroE; WriteDataE = writeDataE; BtaE = btaE;
            ForwardASource = forwardASource; ForwardBSource = forwardBSource; MemReadDataW = memReadDataW;
            PcPlus4F = pcPlus4F; PcNext = pcNext; PcSrcE = pcSrcE;
            StallF = stallF; StallD = stallD; FlushD = flushD; FlushE = flushE;
            ResultW = resultW;
        }
    }

    protected override void HandleClockUpdate()
    {
        // Sync write-enables from whatever the player has set on each visualizer.
        _pc.WriteEnable = registerPCVisualizer.isWriteEnabled;
        _registerFile.RegisterWriteEnable = registerFileVisualizer.isWriteEnabled;
        _dataMemory.MemoryWrite = dataMemoryVisualizer.isWriteEnabled;
        _instructionMemory.MemoryWrite = false; // instruction memory is read-only in this simulator

        // ---- Phase 1: pure combinational computation, no component touched yet ----
        var sig = ComputeSignals();

        // ---- Phase 2: set every stateful component's input for this cycle ----
        _registerFile.ReadAdress1 = sig.Rs1D;
        _registerFile.ReadAdress2 = sig.Rs2D;
        _registerFile.WriteAdress = _mwRd.Output;
        _registerFile.WriteData = sig.ResultW;

        _instructionMemory.Address = _pc.Output;

        _dataMemory.Address = _emAluResult.Output;
        _dataMemory.WriteData = _emWriteData.Output;

        _pc.Input = sig.PcNext;
        if (sig.StallF) _pc.WriteEnable = false;

        if (sig.StallD)
        {
            _fdInstr.WriteEnable = false;
            _fdPcPlus4.WriteEnable = false;
            _fdPc.WriteEnable = false;
        }
        else
        {
            _fdInstr.Input = sig.FlushD ? 0 : sig.FetchedInstr;
            _fdPcPlus4.Input = sig.PcPlus4F;
            _fdPc.Input = _pc.Output;
            _fdInstr.WriteEnable = true;
            _fdPcPlus4.WriteEnable = true;
            _fdPc.WriteEnable = true;
        }

        // D/E latches the RAW register-file read (Rd1D/Rd2D), not the forwarded Execute-stage
        // value - forwarding (sig.SrcAE / sig.ForwardedRd2E) is a per-cycle bypass used only
        // for *this* tick's ALU inputs, not something that should overwrite what the next
        // instruction reads.
        _deRd1.Input = sig.FlushE ? 0 : sig.Rd1D;
        _deRd2.Input = sig.FlushE ? 0 : sig.Rd2D;
        _dePc.Input = _fdPc.Output;
        _deRd.Input = sig.FlushE ? 0 : sig.RdD;
        _deImmExt.Input = sig.ImmExtD;
        _dePcPlus4.Input = _fdPcPlus4.Output;
        _deRs1.Input = sig.FlushE ? 0 : sig.Rs1D;
        _deRs2.Input = sig.FlushE ? 0 : sig.Rs2D;
        _deRegWrite.Input = sig.FlushE ? 0 : (sig.RegWriteD ? 1 : 0);
        _deMemToReg.Input = sig.FlushE ? 0 : (sig.MemToRegD ? 1 : 0);

        _emAluResult.Input = sig.AluResultE;
        _emWriteData.Input = sig.WriteDataE;
        _emRd.Input = _deRd.Output;
        _emPcPlus4.Input = _dePcPlus4.Output;
        _emRegWrite.Input = _deRegWrite.Output;

        _mwAluResult.Input = _emAluResult.Output;
        _mwReadData.Input = sig.MemReadDataW;
        _mwRd.Input = _emRd.Output;
        _mwPcPlus4.Input = _emPcPlus4.Output;
        _mwRegWrite.Input = _emRegWrite.Output;

        // ---- Phase 3: PreClockUpdate() on every stateful component ----
        _pc.PreClockUpdate();
        _fdInstr.PreClockUpdate(); _fdPcPlus4.PreClockUpdate(); _fdPc.PreClockUpdate();
        _deRd1.PreClockUpdate(); _deRd2.PreClockUpdate(); _dePc.PreClockUpdate(); _deRd.PreClockUpdate();
        _deImmExt.PreClockUpdate(); _dePcPlus4.PreClockUpdate();
        _deRs1.PreClockUpdate(); _deRs2.PreClockUpdate(); _deRegWrite.PreClockUpdate(); _deMemToReg.PreClockUpdate();
        _emAluResult.PreClockUpdate(); _emWriteData.PreClockUpdate(); _emRd.PreClockUpdate();
        _emPcPlus4.PreClockUpdate(); _emRegWrite.PreClockUpdate();
        _mwAluResult.PreClockUpdate(); _mwReadData.PreClockUpdate(); _mwRd.PreClockUpdate();
        _mwPcPlus4.PreClockUpdate(); _mwRegWrite.PreClockUpdate();
        _registerFile.PreClockUpdate();
        _instructionMemory.PreClockUpdate();
        _dataMemory.PreClockUpdate();

        // ---- Phase 4: Clock() on every stateful component ----
        _pc.Clock();
        _fdInstr.Clock(); _fdPcPlus4.Clock(); _fdPc.Clock();
        _deRd1.Clock(); _deRd2.Clock(); _dePc.Clock(); _deRd.Clock(); _deImmExt.Clock(); _dePcPlus4.Clock();
        _deRs1.Clock(); _deRs2.Clock(); _deRegWrite.Clock(); _deMemToReg.Clock();
        _emAluResult.Clock(); _emWriteData.Clock(); _emRd.Clock(); _emPcPlus4.Clock(); _emRegWrite.Clock();
        _mwAluResult.Clock(); _mwReadData.Clock(); _mwRd.Clock(); _mwPcPlus4.Clock(); _mwRegWrite.Clock();
        _registerFile.Clock();
        _instructionMemory.Clock();
        _dataMemory.Clock();

        // ---- Bus animations ----
        busController.StartBusSignal(buses.pcToInstrMem, _pc.Output);
        busController.StartBusSignal(buses.pcToPcPlus4Adder, _pc.Output);
        busController.StartBusSignal(buses.instrMemToFdReg, sig.FetchedInstr);
        busController.StartBusSignal(buses.pcPlus4ToFdReg, sig.PcPlus4F);
        busController.StartBusSignal(buses.pcToFdReg, _pc.Output);
        busController.StartBusSignal(buses.pcMuxToPcReg, sig.PcNext);

        busController.StartBusSignal(buses.fdRegToRegFileA1, sig.Rs1D);
        busController.StartBusSignal(buses.fdRegToRegFileA2, sig.Rs2D);
        busController.StartBusSignal(buses.fdRegToExtend, sig.FetchedInstr);
        busController.StartBusSignal(buses.fdRegToRdD, sig.RdD);
        busController.StartBusSignal(buses.regFileRd1ToDeReg, sig.Rd1D);
        busController.StartBusSignal(buses.regFileRd2ToDeReg, sig.Rd2D);
        busController.StartBusSignal(buses.extendToDeReg, sig.ImmExtD);
        busController.StartBusSignal(buses.rdDToDeReg, sig.RdD);
        busController.StartBusSignal(buses.fdRegPcToDeReg, _fdPc.Output);
        busController.StartBusSignal(buses.fdRegPcPlus4ToDeReg, _fdPcPlus4.Output);

        busController.StartBusSignal(buses.deRegRd1ToAlu, sig.SrcAE);
        busController.StartBusSignal(buses.deRegRd2ToSrcBMux, sig.ForwardedRd2E);
        busController.StartBusSignal(buses.deRegImmExtToSrcBMux, _deImmExt.Output);
        busController.StartBusSignal(buses.srcBMuxToAlu, sig.SrcBE);
        busController.StartBusSignal(buses.deRegRd2ToWriteDataE, sig.ForwardedRd2E);

        // Only light up a forwarding wire on the cycles where a forward actually happens.
        if (sig.ForwardASource == 1) busController.StartBusSignal(buses.emForwardToAluA, sig.SrcAE);
        if (sig.ForwardASource == 2) busController.StartBusSignal(buses.mwForwardToAluA, sig.SrcAE);
        if (sig.ForwardBSource == 1) busController.StartBusSignal(buses.emForwardToAluB, sig.ForwardedRd2E);
        if (sig.ForwardBSource == 2) busController.StartBusSignal(buses.mwForwardToAluB, sig.ForwardedRd2E);

        busController.StartBusSignal(buses.deRegPcToBtaAdder, _dePc.Output);
        busController.StartBusSignal(buses.deRegImmExtToBtaAdder, _deImmExt.Output);
        busController.StartBusSignal(buses.btaAdderToPcMux, sig.BtaE);
        busController.StartBusSignal(buses.aluToEmReg, sig.AluResultE);
        busController.StartBusSignal(buses.writeDataEToEmReg, sig.WriteDataE);
        busController.StartBusSignal(buses.deRegRdToEmReg, _deRd.Output);
        busController.StartBusSignal(buses.deRegPcPlus4ToEmReg, _dePcPlus4.Output);

        busController.StartBusSignal(buses.emRegAluResultToDataMem, _emAluResult.Output);
        busController.StartBusSignal(buses.emRegWriteDataToDataMem, _emWriteData.Output);
        busController.StartBusSignal(buses.emRegAluResultToMwReg, _emAluResult.Output);
        busController.StartBusSignal(buses.dataMemToMwReg, sig.MemReadDataW);
        busController.StartBusSignal(buses.emRegRdToMwReg, _emRd.Output);
        busController.StartBusSignal(buses.emRegPcPlus4ToMwReg, _emPcPlus4.Output);

        busController.StartBusSignal(buses.mwRegAluResultToResultMux, _mwAluResult.Output);
        busController.StartBusSignal(buses.mwRegReadDataToResultMux, _mwReadData.Output);
        busController.StartBusSignal(buses.mwRegPcPlus4ToResultMux, _mwPcPlus4.Output);
        busController.StartBusSignal(buses.resultMuxToRegFileWd3, sig.ResultW);
        busController.StartBusSignal(buses.mwRegRdToRegFileA3, _mwRd.Output);

        // Note: BlinkClockedComponents() and UpdateVisualizers() are already called
        // automatically by BaseLevelRegisseur's tick sequence (before/after this method) -
        // do not call them again here.
    }

    protected override void BlinkClockedComponents()
    {
        registerPCVisualizer.TriggerBlink();

        fdInstrVisualizer.TriggerBlink();
        fdPcPlus4Visualizer.TriggerBlink();
        fdPcVisualizer.TriggerBlink();

        deRd1Visualizer.TriggerBlink();
        deRd2Visualizer.TriggerBlink();
        dePcVisualizer.TriggerBlink();
        deRdVisualizer.TriggerBlink();
        deImmExtVisualizer.TriggerBlink();
        dePcPlus4Visualizer.TriggerBlink();

        emAluResultVisualizer.TriggerBlink();
        emWriteDataVisualizer.TriggerBlink();
        emRdVisualizer.TriggerBlink();
        emPcPlus4Visualizer.TriggerBlink();

        mwAluResultVisualizer.TriggerBlink();
        mwReadDataVisualizer.TriggerBlink();
        mwRdVisualizer.TriggerBlink();
        mwPcPlus4Visualizer.TriggerBlink();

        registerFileVisualizer.TriggerBlink();
        instructionMemoryVisualizer.TriggerBlink();
        dataMemoryVisualizer.TriggerBlink();
    }

    protected override IEnumerator RunBusVisualizations()
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Pure combinational logic for one tick: decode, register-file/memory reads (via direct
    /// array/dictionary lookup - never through .ReadData, which would depend on
    /// PreClockUpdate() having already run), forwarding unit, hazard detection,
    /// ALU/branch-target computation, and the writeback mux. Reads only every component's
    /// *current* (pre-clock, start-of-tick) Output/Registers/Memory - touches nothing.
    /// </summary>
    private PipelineSignals ComputeSignals()
    {
        int fetchedInstr = _instructionMemory.Memory.GetValueOrDefault(_pc.Output, 0);

        int instrD = _fdInstr.Output;
        int opcodeD = instrD & 0x7F;
        int rs1D = (instrD >> 15) & 0x1F;
        int rs2D = (instrD >> 20) & 0x1F;
        int rdD = (instrD >> 7) & 0x1F;
        bool regWriteD = opcodeD is OP_R_TYPE or OP_I_ALU or OP_LOAD or OP_JAL;
        bool memToRegD = opcodeD == OP_LOAD;
        int immExtD = Extender.Evaluate(extenderVisualizer.CurrentAluOperation, (uint)instrD);

        // Register File read, x0-hardwired-zero semantics (mirrors RegisterFile.ReadRegisters).
        int ReadReg(int addr) => addr <= 0 || addr >= _registerFile.Registers.Length ? 0 : _registerFile.Registers[addr];
        int rd1D = ReadReg(rs1D);
        int rd2D = ReadReg(rs2D);

        // ResultW is a function of *this tick's start-of-cycle* M/W register contents, so
        // it's safe to compute up front - both for the actual register-file write and for
        // M/W forwarding.
        int resultW = Multiplexer.SelectNto1(
            new[] { _mwAluResult.Output, _mwReadData.Output, _mwPcPlus4.Output },
            resultMuxVisualizer.CurrentChosenMuxPath);

        // --- Load-use hazard detection ---
        bool lwStall = _deMemToReg.Output == 1 && _deRd.Output != 0 &&
                       (_deRd.Output == rs1D || _deRd.Output == rs2D);

        // --- Forwarding unit (EX hazard) ---
        int rs1E = _deRs1.Output;
        int rs2E = _deRs2.Output;

        // returns (forwardedValue, source) where source is 0=none, 1=E/M, 2=M/W
        (int value, int source) ForwardFrom(int rsE, int fallback)
        {
            if (_emRegWrite.Output == 1 && _emRd.Output != 0 && _emRd.Output == rsE) return (_emAluResult.Output, 1);
            if (_mwRegWrite.Output == 1 && _mwRd.Output != 0 && _mwRd.Output == rsE) return (resultW, 2);
            return (fallback, 0);
        }

        var (srcAE, forwardASource) = ForwardFrom(rs1E, _deRd1.Output);
        var (forwardedRd2E, forwardBSource) = ForwardFrom(rs2E, _deRd2.Output);

        int srcBE = Multiplexer.SelectNto1(new[] { forwardedRd2E, _deImmExt.Output, 0 }, srcBMuxVisualizer.CurrentChosenMuxPath);
        int writeDataE = forwardedRd2E;

        var (aluResultE, aluZeroE) = Alu.Calculate(srcAE, srcBE, (AluOperation)aluVisualizer.CurrentAluOperation);
        var (btaE, _) = Alu.Calculate(_dePc.Output, _deImmExt.Output, (AluOperation)btaAluVisualizer.CurrentAluOperation);

        // --- Branch resolution: the player's own PC-MUX choice is what says "taken" ---
        bool pcSrcE = pcMuxVisualizer.CurrentChosenMuxPath == 1;

        var (pcPlus4F, _) = Alu.Calculate(_pc.Output, 4, (AluOperation)pcPlus4AluVisualizer.CurrentAluOperation);
        int pcNext = Multiplexer.SelectNto1(new[] { pcPlus4F, btaE, 0 }, pcMuxVisualizer.CurrentChosenMuxPath);

        bool stallF = lwStall;
        bool stallD = lwStall;
        bool flushD = pcSrcE;
        bool flushE = pcSrcE || lwStall;

        // Memory stage read - pure lookup at the E/M-latched address (this tick's Memory
        // stage instruction), independent of DataInstMemory.ReadData/PreClockUpdate.
        int memReadDataW = _dataMemory.Memory.GetValueOrDefault(_emAluResult.Output, 0);

        return new PipelineSignals(fetchedInstr, rs1D, rs2D, rdD, rd1D, rd2D, immExtD, regWriteD, memToRegD,
            srcAE, srcBE, forwardedRd2E, aluResultE, aluZeroE, writeDataE, btaE,
            forwardASource, forwardBSource, memReadDataW,
            pcPlus4F, pcNext, pcSrcE, stallF, stallD, flushD, flushE, resultW);
    }

    protected override bool CheckWinCondition()
    {
        // Matches the REGISTER_FIELD check used elsewhere: once the target instruction has
        // made it through writeback, its result is sitting in the register file - checking
        // the register file's final state is equivalent to "did that instruction complete
        // correctly", without needing to catch it at the exact tick it passes through WB.
        if (Initial.registerFieldAddressAnswer < 0 || Initial.registerFieldAddressAnswer >= _registerFile.Registers.Length)
            return false;

        return _registerFile.Registers[Initial.registerFieldAddressAnswer] == Initial.registerFieldValueAnswer;
    }

    protected override void UpdateVisualizers()
    {
        registerPCVisualizer.UIRegisterPanel.Display("PC", _pc.Output);

        fdInstrVisualizer.UIRegisterPanel.Display("InstrD", _fdInstr.Output);
        fdPcPlus4Visualizer.UIRegisterPanel.Display("PCPlus4D", _fdPcPlus4.Output);
        fdPcVisualizer.UIRegisterPanel.Display("PCD", _fdPc.Output);

        deRd1Visualizer.UIRegisterPanel.Display("RD1E", _deRd1.Output);
        deRd2Visualizer.UIRegisterPanel.Display("RD2E", _deRd2.Output);
        dePcVisualizer.UIRegisterPanel.Display("PCE", _dePc.Output);
        deRdVisualizer.UIRegisterPanel.Display("RdE", _deRd.Output);
        deImmExtVisualizer.UIRegisterPanel.Display("ImmExtE", _deImmExt.Output);
        dePcPlus4Visualizer.UIRegisterPanel.Display("PCPlus4E", _dePcPlus4.Output);

        emAluResultVisualizer.UIRegisterPanel.Display("ALUResultM", _emAluResult.Output);
        emWriteDataVisualizer.UIRegisterPanel.Display("WriteDataM", _emWriteData.Output);
        emRdVisualizer.UIRegisterPanel.Display("RdM", _emRd.Output);
        emPcPlus4Visualizer.UIRegisterPanel.Display("PCPlus4M", _emPcPlus4.Output);

        mwAluResultVisualizer.UIRegisterPanel.Display("ALUResultW", _mwAluResult.Output);
        mwReadDataVisualizer.UIRegisterPanel.Display("ReadDataW", _mwReadData.Output);
        mwRdVisualizer.UIRegisterPanel.Display("RdW", _mwRd.Output);
        mwPcPlus4Visualizer.UIRegisterPanel.Display("PCPlus4W", _mwPcPlus4.Output);
    }

    protected override PipelineState GetCurrentState()
    {
        return new PipelineState
        {
            PcValue = _pc.Output,

            FdInstrValue = _fdInstr.Output,
            FdPcPlus4Value = _fdPcPlus4.Output,
            FdPcValue = _fdPc.Output,

            DeRd1Value = _deRd1.Output,
            DeRd2Value = _deRd2.Output,
            DePcValue = _dePc.Output,
            DeRdValue = _deRd.Output,
            DeImmExtValue = _deImmExt.Output,
            DePcPlus4Value = _dePcPlus4.Output,
            DeRs1Value = _deRs1.Output,
            DeRs2Value = _deRs2.Output,
            DeRegWriteValue = _deRegWrite.Output == 1,
            DeMemToRegValue = _deMemToReg.Output == 1,

            EmAluResultValue = _emAluResult.Output,
            EmWriteDataValue = _emWriteData.Output,
            EmRdValue = _emRd.Output,
            EmPcPlus4Value = _emPcPlus4.Output,
            EmRegWriteValue = _emRegWrite.Output == 1,

            MwAluResultValue = _mwAluResult.Output,
            MwReadDataValue = _mwReadData.Output,
            MwRdValue = _mwRd.Output,
            MwPcPlus4Value = _mwPcPlus4.Output,
            MwRegWriteValue = _mwRegWrite.Output == 1,

            RegisterFieldValue = (int[])_registerFile.Registers.Clone(),

            FirstInstructionMemoryValue = _instructionMemory.Memory.GetValueOrDefault(0, 0),
            SecondInstructionMemoryValue = _instructionMemory.Memory.GetValueOrDefault(4, 0),
            ThirdInstructionMemoryValue = _instructionMemory.Memory.GetValueOrDefault(8, 0),
            FourthInstructionMemoryValue = _instructionMemory.Memory.GetValueOrDefault(12, 0),

            FirstDataMemoryValue = _dataMemory.Memory.GetValueOrDefault(0, 0),
            SecondDataMemoryValue = _dataMemory.Memory.GetValueOrDefault(4, 0),
            ThirdDataMemoryValue = _dataMemory.Memory.GetValueOrDefault(8, 0),
            FourthDataMemoryValue = _dataMemory.Memory.GetValueOrDefault(12, 0),

            PcWe = registerPCVisualizer.isWriteEnabled,

            AluOperation = aluVisualizer.CurrentAluOperation,
            ExtenderOperation = extenderVisualizer.CurrentAluOperation,
            PcPlus4AluOperation = pcPlus4AluVisualizer.CurrentAluOperation,
            BtaAluOperation = btaAluVisualizer.CurrentAluOperation,

            MuxPcPath = pcMuxVisualizer.CurrentChosenMuxPath,
            MuxSrcBPath = srcBMuxVisualizer.CurrentChosenMuxPath,
            MuxResultPath = resultMuxVisualizer.CurrentChosenMuxPath,
        };
    }

    protected override void ApplyState(PipelineState s)
    {
        _pc.Reset(s.PcValue);

        _fdInstr.Reset(s.FdInstrValue);
        _fdPcPlus4.Reset(s.FdPcPlus4Value);
        _fdPc.Reset(s.FdPcValue);

        _deRd1.Reset(s.DeRd1Value);
        _deRd2.Reset(s.DeRd2Value);
        _dePc.Reset(s.DePcValue);
        _deRd.Reset(s.DeRdValue);
        _deImmExt.Reset(s.DeImmExtValue);
        _dePcPlus4.Reset(s.DePcPlus4Value);
        _deRs1.Reset(s.DeRs1Value);
        _deRs2.Reset(s.DeRs2Value);
        _deRegWrite.Reset(s.DeRegWriteValue ? 1 : 0);
        _deMemToReg.Reset(s.DeMemToRegValue ? 1 : 0);

        _emAluResult.Reset(s.EmAluResultValue);
        _emWriteData.Reset(s.EmWriteDataValue);
        _emRd.Reset(s.EmRdValue);
        _emPcPlus4.Reset(s.EmPcPlus4Value);
        _emRegWrite.Reset(s.EmRegWriteValue ? 1 : 0);

        _mwAluResult.Reset(s.MwAluResultValue);
        _mwReadData.Reset(s.MwReadDataValue);
        _mwRd.Reset(s.MwRdValue);
        _mwPcPlus4.Reset(s.MwPcPlus4Value);
        _mwRegWrite.Reset(s.MwRegWriteValue ? 1 : 0);

        _registerFile.InitializeRegisters(s.RegisterFieldValue);

        _instructionMemory.LoadWord(0, s.FirstInstructionMemoryValue);
        _instructionMemory.LoadWord(4, s.SecondInstructionMemoryValue);
        _instructionMemory.LoadWord(8, s.ThirdInstructionMemoryValue);
        _instructionMemory.LoadWord(12, s.FourthInstructionMemoryValue);

        _dataMemory.LoadWord(0, s.FirstDataMemoryValue);
        _dataMemory.LoadWord(4, s.SecondDataMemoryValue);
        _dataMemory.LoadWord(8, s.ThirdDataMemoryValue);
        _dataMemory.LoadWord(12, s.FourthDataMemoryValue);

        registerPCVisualizer.ForceUpdateWriteEnableVisualization(s.PcWe);

        aluVisualizer.ChooseAluOperation(s.AluOperation);
        extenderVisualizer.ChooseAluOperation(s.ExtenderOperation);
        pcPlus4AluVisualizer.ChooseAluOperation(s.PcPlus4AluOperation);
        btaAluVisualizer.ChooseAluOperation(s.BtaAluOperation);

        pcMuxVisualizer.SelectPath(s.MuxPcPath);
        srcBMuxVisualizer.SelectPath(s.MuxSrcBPath);
        resultMuxVisualizer.SelectPath(s.MuxResultPath);

        UpdateVisualizers();
    }
}
