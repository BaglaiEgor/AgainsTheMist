using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepyFinalAltarController : MonoBehaviour
{
    private enum FinalState
    {
        WaitingFirstActivation,
        TrialActive,
        WaitingSecondActivation,
        Finished
    }

    [Header("Interaction")]
    [SerializeField] private string playerTag = "Player";
    [Min(0.1f)] [SerializeField] private float interactDistance = 1.8f;

    [Header("Doors")]
    [SerializeField] private List<CreepyDoorBlocker> doorsToCloseOnStart = new();
    [SerializeField] private List<CreepyDoorBlocker> doorsToOpenOnFinish = new();

    [Header("Trial")]
    [SerializeField] private List<CreepyDestructibleTarget> protectiveTargets = new();
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] enemySpawnPoints;
    [Min(0.1f)] [SerializeField] private float completionCheckInterval = 0.25f;

    [Header("Heart")]
    [SerializeField] private Transform heart;
    [SerializeField] private GameObject heartGate;
    [SerializeField] private GameObject altarReadyVisual;
    [Min(0f)] [SerializeField] private float heartShakeTime = 1.2f;
    [Min(0f)] [SerializeField] private float heartShakeDistance = 0.08f;
    [SerializeField] private Vector3 heartFallOffset = new Vector3(0f, -4f, 0f);
    [Min(0.05f)] [SerializeField] private float heartFallTime = 1.5f;
    [SerializeField] private bool hideHeartAfterFall = true;

    [Header("Fog To Disable")]
    [SerializeField] private List<GameObject> objectsToDisableOnFinish = new();
    [SerializeField] private List<Behaviour> behavioursToDisableOnFinish = new();

    private readonly List<GameObject> spawnedEnemies = new();
    private FinalState state;

    public bool IsFinished => state == FinalState.Finished;

    private void Awake()
    {
        Collider2D altarCollider = GetComponent<Collider2D>();
        altarCollider.isTrigger = true;
        state = FinalState.WaitingFirstActivation;

        if (altarReadyVisual != null)
            altarReadyVisual.SetActive(true);
    }

    private void OnMouseDown()
    {
        TryUseAltar();
    }

    public bool TryUseAltar()
    {
        if (!IsPlayerClose())
            return false;

        if (state == FinalState.WaitingFirstActivation)
        {
            StartTrial();
            return true;
        }

        if (state == FinalState.WaitingSecondActivation)
        {
            StartCoroutine(FinishSequence());
            return true;
        }

        return false;
    }

    public void StartTrial()
    {
        if (state != FinalState.WaitingFirstActivation)
            return;

        state = FinalState.TrialActive;

        if (altarReadyVisual != null)
            altarReadyVisual.SetActive(false);

        for (int i = 0; i < doorsToCloseOnStart.Count; i++)
        {
            if (doorsToCloseOnStart[i] != null)
                doorsToCloseOnStart[i].Close();
        }

        SpawnEnemies();

        for (int i = 0; i < protectiveTargets.Count; i++)
        {
            if (protectiveTargets[i] != null)
                protectiveTargets[i].ActivateTarget();
        }

        StartCoroutine(WaitForTrialComplete());
    }

    private void SpawnEnemies()
    {
        int count = Mathf.Min(enemyPrefabs != null ? enemyPrefabs.Length : 0, enemySpawnPoints != null ? enemySpawnPoints.Length : 0);
        for (int i = 0; i < count; i++)
        {
            if (enemyPrefabs[i] == null || enemySpawnPoints[i] == null)
                continue;

            GameObject enemy = Instantiate(enemyPrefabs[i], enemySpawnPoints[i].position, enemySpawnPoints[i].rotation);
            spawnedEnemies.Add(enemy);
        }
    }

    private IEnumerator WaitForTrialComplete()
    {
        WaitForSeconds wait = new WaitForSeconds(completionCheckInterval);
        while (!AreTargetsDestroyed() || !AreEnemiesDefeated())
            yield return wait;

        state = FinalState.WaitingSecondActivation;

        if (altarReadyVisual != null)
            altarReadyVisual.SetActive(true);
    }

    private bool AreTargetsDestroyed()
    {
        for (int i = 0; i < protectiveTargets.Count; i++)
        {
            CreepyDestructibleTarget target = protectiveTargets[i];
            if (target != null && !target.IsDestroyed)
                return false;
        }

        return true;
    }

    private bool AreEnemiesDefeated()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemyObject = spawnedEnemies[i];
            if (enemyObject == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            FogEnemy fogEnemy = enemyObject.GetComponent<FogEnemy>();
            if (fogEnemy != null && fogEnemy.IsDead)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            return false;
        }

        return true;
    }

    private IEnumerator FinishSequence()
    {
        if (state == FinalState.Finished)
            yield break;

        state = FinalState.Finished;

        if (altarReadyVisual != null)
            altarReadyVisual.SetActive(false);

        if (heartGate != null)
            heartGate.SetActive(false);

        if (heart != null)
        {
            Vector3 startPosition = heart.position;
            float timer = 0f;
            while (timer < heartShakeTime)
            {
                timer += Time.deltaTime;
                Vector2 shake = Random.insideUnitCircle * heartShakeDistance;
                heart.position = startPosition + new Vector3(shake.x, shake.y, 0f);
                yield return null;
            }

            Vector3 fallStart = startPosition;
            Vector3 fallEnd = startPosition + heartFallOffset;
            timer = 0f;
            while (timer < heartFallTime)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / heartFallTime);
                heart.position = Vector3.Lerp(fallStart, fallEnd, t);
                yield return null;
            }

            if (hideHeartAfterFall)
                heart.gameObject.SetActive(false);
        }

        DisableFogObjects();

        for (int i = 0; i < doorsToOpenOnFinish.Count; i++)
        {
            if (doorsToOpenOnFinish[i] != null)
                doorsToOpenOnFinish[i].Open();
        }
    }

    private void DisableFogObjects()
    {
        for (int i = 0; i < objectsToDisableOnFinish.Count; i++)
        {
            if (objectsToDisableOnFinish[i] != null)
                objectsToDisableOnFinish[i].SetActive(false);
        }

        for (int i = 0; i < behavioursToDisableOnFinish.Count; i++)
        {
            if (behavioursToDisableOnFinish[i] != null)
                behavioursToDisableOnFinish[i].enabled = false;
        }
    }

    private bool IsPlayerClose()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return false;

        return Vector2.Distance(player.transform.position, transform.position) <= interactDistance;
    }
}
