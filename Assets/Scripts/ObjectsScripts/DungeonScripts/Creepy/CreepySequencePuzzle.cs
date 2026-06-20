using System.Collections.Generic;
using UnityEngine;

public enum CreepySequenceSymbol
{
    Eye,
    Mouth,
    Tentacle
}

[DisallowMultipleComponent]
public class CreepySequencePuzzle : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField] private List<CreepySequenceSymbol> correctSequence = new()
    {
        CreepySequenceSymbol.Eye,
        CreepySequenceSymbol.Mouth,
        CreepySequenceSymbol.Tentacle
    };
    [SerializeField] private List<CreepySequencePuzzleButton> buttons = new();
    [SerializeField] private CreepyDoorBlocker doorToOpenOnComplete;

    [Header("Failure")]
    [SerializeField] private bool resetButtonsOnFail = true;

    private int currentIndex;
    private bool completed;

    public bool Completed => completed;

    public void PressSymbol(CreepySequenceSymbol symbol)
    {
        PressButton(null, symbol);
    }

    public void PressButton(CreepySequencePuzzleButton button, CreepySequenceSymbol symbol)
    {
        if (completed || correctSequence.Count == 0)
            return;

        if (symbol == correctSequence[currentIndex])
        {
            if (button != null)
                button.SetLocked(true);

            currentIndex++;

            if (currentIndex >= correctSequence.Count)
                Complete();

            return;
        }

        Fail();
    }

    public void ResetPuzzle()
    {
        if (completed)
            return;

        currentIndex = 0;

        if (!resetButtonsOnFail)
            return;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
                buttons[i].SetLocked(false);
        }
    }

    private void Complete()
    {
        completed = true;
        currentIndex = correctSequence.Count;

        if (doorToOpenOnComplete != null)
            doorToOpenOnComplete.Open();
    }

    private void Fail()
    {
        ResetPuzzle();
    }
}
