using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CreepySealRoomController : MonoBehaviour
{
    [Header("Seals")]
    [SerializeField] private List<CreepySealEncounter> seals = new();

    [Header("Final Door")]
    [SerializeField] private CreepyDoorBlocker finalDoor;

    private bool completed;

    public bool Completed => completed;

    private void Update()
    {
        if (!completed)
            RefreshCompletion();
    }

    public void NotifySealCompleted(CreepySealEncounter seal)
    {
        RefreshCompletion();
    }

    private void RefreshCompletion()
    {
        if (completed)
            return;

        for (int i = 0; i < seals.Count; i++)
        {
            if (seals[i] != null && !seals[i].Completed)
                return;
        }

        completed = true;

        if (finalDoor != null)
            finalDoor.Open();
    }
}
