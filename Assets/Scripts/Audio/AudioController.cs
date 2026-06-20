using UnityEngine;

[DisallowMultipleComponent]
public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    [SerializeField] private AudioClip musicClip;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.45f;

    [Header("SFX")]
    [SerializeField] private AudioClip weaponSwingClip;
    [SerializeField] private AudioClip toolSwingClip;
    [SerializeField] private AudioClip mineClip;
    [SerializeField] private AudioClip placeClip;
    [SerializeField] private AudioClip eatClip;
    [SerializeField] private AudioClip potionClip;
    [SerializeField] private AudioClip interactClip;
    [SerializeField] private AudioClip snowClimbClip;
    [SerializeField] private AudioClip entryExitClip;
    [SerializeField] private AudioClip dungeonPlateClip;
    [SerializeField] private AudioClip dungeonDoorClip;
    [SerializeField] private AudioClip dungeonTrapClip;
    [SerializeField] private AudioClip dungeonFireClip;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("Object SFX Distance")]
    [SerializeField] private string playerTag = "Player";
    [Min(0f)] [SerializeField] private float maxObjectSfxDistance = 12f;

    [Header("SFX Randomization")]
    [SerializeField] private bool randomizeSfx = true;
    [Min(0.1f)] [SerializeField] private float pitchMin = 0.92f;
    [Min(0.1f)] [SerializeField] private float pitchMax = 1.08f;
    [Range(0f, 1f)] [SerializeField] private float volumeMin = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float volumeMax = 1f;

    private Transform playerTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSources();
        musicVolume = SettingsStorage.MusicVolume;
        sfxVolume = SettingsStorage.SfxVolume;
        ApplySourceSettings();
    }

    private void Start()
    {
        PlayMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        pitchMin = Mathf.Max(0.1f, pitchMin);
        pitchMax = Mathf.Max(pitchMin, pitchMax);
        volumeMin = Mathf.Clamp01(volumeMin);
        volumeMax = Mathf.Max(volumeMin, Mathf.Clamp01(volumeMax));
        maxObjectSfxDistance = Mathf.Max(0f, maxObjectSfxDistance);

        if (!Application.isPlaying)
            return;

        ApplySourceSettings();
    }

    public void PlayWeaponSwing() => PlaySfx(weaponSwingClip);
    public void PlayToolSwing() => PlaySfx(toolSwingClip);
    public void PlayMine() => PlaySfx(mineClip);
    public void PlayPlace() => PlaySfx(placeClip);
    public void PlayEat() => PlaySfx(eatClip);
    public void PlayPotion() => PlaySfx(potionClip);
    public void PlayInteract() => PlaySfx(interactClip);
    public void PlaySnowClimb() => PlaySfx(snowClimbClip);
    public void PlayEntryExit() => PlaySfx(entryExitClip);
    public void PlayDungeonPlate() => PlaySfx(dungeonPlateClip);
    public void PlayDungeonDoor() => PlaySfx(dungeonDoorClip);
    public void PlayDungeonTrap() => PlaySfx(dungeonTrapClip);
    public void PlayDungeonFire() => PlaySfx(dungeonFireClip);

    public void PlayInteract(Vector3 sourcePosition) => PlaySfxNearPlayer(interactClip, sourcePosition);
    public void PlayDungeonPlate(Vector3 sourcePosition) => PlaySfxNearPlayer(dungeonPlateClip, sourcePosition);
    public void PlayDungeonDoor(Vector3 sourcePosition) => PlaySfxNearPlayer(dungeonDoorClip, sourcePosition);
    public void PlayDungeonTrap(Vector3 sourcePosition) => PlaySfxNearPlayer(dungeonTrapClip, sourcePosition);
    public void PlayDungeonFire(Vector3 sourcePosition) => PlaySfxNearPlayer(dungeonFireClip, sourcePosition);

    public float GetMusicVolume() => musicVolume;
    public float GetSfxVolume() => sfxVolume;

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        SettingsStorage.SaveMusicVolume(musicVolume);
        ApplySourceSettings();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        SettingsStorage.SaveSfxVolume(sfxVolume);
        ApplySourceSettings();
    }

    private void PlayMusic()
    {
        if (musicSource == null || musicClip == null)
            return;

        musicSource.clip = musicClip;
        musicSource.volume = musicVolume;
        musicSource.loop = true;

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        float pitch = randomizeSfx ? Random.Range(pitchMin, pitchMax) : 1f;
        float volume = randomizeSfx ? Random.Range(volumeMin, volumeMax) : 1f;

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, sfxVolume * volume);
    }

    private void PlaySfxNearPlayer(AudioClip clip, Vector3 sourcePosition)
    {
        if (!IsSourceNearPlayer(sourcePosition))
            return;

        PlaySfx(clip);
    }

    private bool IsSourceNearPlayer(Vector3 sourcePosition)
    {
        if (maxObjectSfxDistance <= 0f)
            return true;

        if (!TryResolvePlayerTransform(out Transform player))
            return true;

        Vector2 source2D = sourcePosition;
        Vector2 player2D = player.position;
        float maxDistanceSqr = maxObjectSfxDistance * maxObjectSfxDistance;
        return (source2D - player2D).sqrMagnitude <= maxDistanceSqr;
    }

    private bool TryResolvePlayerTransform(out Transform player)
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
        {
            player = playerTransform;
            return true;
        }

        playerTransform = null;

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            player = null;
            return false;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        playerTransform = playerObject != null ? playerObject.transform : null;
        player = playerTransform;
        return player != null;
    }

    private void EnsureSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();

        if (musicSource == null)
            musicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
    }

    private void ApplySourceSettings()
    {
        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.volume = sfxVolume;
        }
    }
}
