using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
///     Orchestrates the dialogue flow by connecting data (DialogueGraph) with the interface (DialogueUI).
///     Manages node transitions, branching logic, and dialogue events.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [FormerlySerializedAs("_ui")] [Header("References")] [SerializeField]
    private DialogueUI ui;

    [FormerlySerializedAs("_activeGraph")] [SerializeField]
    private DialogueGraph activeGraph;

    [FormerlySerializedAs("_hintGraph")] [SerializeField]
    private DialogueGraph hintGraph;

    private DialogueNode _currentNode;
    private int _currentNodeIndex;
    
    [Header("Back button")]
    [Tooltip("When stepping back, show the text at once instead of typing it out again.")]
    [SerializeField] private bool instantTextOnBack = true;
    
    /// <summary>
    ///     A node the player has already passed.
    ///     Index is needed only while the flow is position-based (next = index + 1)
    /// </summary>
    private struct HistoryEntry
    {
        public readonly DialogueNode Node;
        public readonly int Index;

        public HistoryEntry(DialogueNode node, int index)
        {
            Node = node;
            Index = index;
        }
    }
    private readonly Stack<HistoryEntry> _history = new Stack<HistoryEntry>();

    public void Start()
    {
        ui.OnBackRequested += HandleBack;
        OnBackAvailabilityChanged += ui.SetBackAvailable;
        RaiseBackAvailability();
        
        // Automatically start the dialogue if a graph is assigned
        if (activeGraph != null) StartDialogue(activeGraph);


        // Subscribe to UI events
        ui.OnNextRequested += HandleNextQuote;
        ui.OnSpecificPathRequested += HandleBranching;
    }


    private void OnDestroy()
    {
        if (ui == null) return;
        ui.OnNextRequested -= HandleNextQuote;
        ui.OnSpecificPathRequested -= HandleBranching;
        ui.OnBackRequested -= HandleBack;
        OnBackAvailabilityChanged -= ui.SetBackAvailable;
    }

    public event Action OnDialogueEnd;
    public event Action OnDialogueBegin;
    public event Action OnHintEnabled;
    public event Action<bool> OnBackAvailabilityChanged;

    /// <summary>
    ///     Switches the active conversation to the hint graph.
    /// </summary>
    public void SetupHintDialogue()
    {
        if (hintGraph == null) return;

        OnHintEnabled?.Invoke();
        StartDialogue(hintGraph);
    }

    /// <summary>
    ///     Advances the dialogue to the next sequential node.
    /// </summary>
    private void HandleNextQuote()
    {
        var nextIndex = _currentNodeIndex + 1;

        if (nextIndex < activeGraph.nodes.Count)
            SetNode(nextIndex);
        else
            EndDialogue();
    }

    /// <summary>
    ///     Jumps to a specific node index based on the player's choice.
    /// </summary>
    /// <param name="branchIndex">The ID of the chosen answer (1, 2, or 3).</param>
    private void HandleBranching(int branchIndex)
    {
        int targetIndex;

        switch (branchIndex)
        {
            case 1:
                targetIndex = _currentNode.firstOption;
                break;
            case 2:
                targetIndex = _currentNode.secondOption;
                break;
            case 3:
                targetIndex = _currentNode.thirdOption;
                break;
            default:
                CustomLog.LogEditorWarning($"[DialogueManager] Uncommon path selected! selectedPath: {branchIndex}");
                return;
        }

        // Check if the target node exists in the graph
        if (targetIndex >= 0 && targetIndex < activeGraph.nodes.Count)
        {
            SetNode(targetIndex);
        }
        else
        {
            CustomLog.LogEditorWarning(
                $"[DialogueManager] Target index {targetIndex} is out of bounds. Ending dialogue.");
            EndDialogue();
        }
    }
    
    private void HandleBack()
    {
        if (_history.Count == 0) return;

        var entry = _history.Pop();
        ShowNode(entry.Node, entry.Index);
        RaiseBackAvailability();

        // The text was already read once - no need to type it out again.
        if (instantTextOnBack) ui.SkipAnimationOfTyping();
    }

    #region helpers

    /// <summary>
    ///     Starts a conversation using the provided dialogue graph.
    /// </summary>
    private void StartDialogue(DialogueGraph graph)
    {
        if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
        {
            CustomLog.LogEditorError("[DialogueManager] Cannot start dialogue: Graph is null or empty!");
            return;
        }

        activeGraph = graph;
        _history.Clear();
        _currentNode = null;
        OnDialogueBegin?.Invoke();
        SetNode(0);
    }

    /// <summary>
    ///     Updates the current state and refreshes the UI.
    /// </summary>
    private void SetNode(int index)
    {
        if (_currentNode != null) _history.Push(new HistoryEntry(_currentNode, _currentNodeIndex));

        ShowNode(activeGraph.nodes[index], index);
        RaiseBackAvailability();
    }
    
    /// <summary>
    ///     Updates the current state and refreshes the UI.
    ///     Does not touch the history (shared by forward and back navigation).
    /// </summary>
    private void ShowNode(DialogueNode node, int index)
    {
        _currentNodeIndex = index;
        _currentNode = node;
        ui.UpdateVisuals(node);
    }
    
    private void RaiseBackAvailability()
    {
        OnBackAvailabilityChanged?.Invoke(_history.Count > 0);
    }

    private void EndDialogue()
    {
        CustomLog.LogEditor("[DialogueManager] Dialogue sequence finished.");
        _history.Clear();
        RaiseBackAvailability();
        OnDialogueEnd?.Invoke();
        ui.StopAnimatingText();
    }

    public void SetActiveGraph(DialogueGraph g)
    {
        activeGraph = g;
    }

    #endregion
}