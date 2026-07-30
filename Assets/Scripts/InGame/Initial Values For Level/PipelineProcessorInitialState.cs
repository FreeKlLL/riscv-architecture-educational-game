using UnityEngine;

[CreateAssetMenu(fileName = "PipelineInitialState", menuName = "Scriptable Objects/ProcessorInitialState")]
public class PipelineProcessorInitialState : BaseProcessorInitialState
{
    // Instruction Memory
    [Header("Instruction Memory  [addresses 0 / 4 / 8 / 12]")]
    public int firstInstructionWord;
    public int secondInstructionWord;
    public int thirdInstructionWord;
    public int fourthInstructionWord;
    
    // Data Memory
    [Header("Data Memory  [addresses 0 / 4 / 8 / 12]")]
    public int firstDataWord;
    public int secondDataWord;
    public int thirdDataWord;
    public int fourthDataWord;
}
