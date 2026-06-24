using UnityEngine;

[DisallowMultipleComponent]
public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource trapSfxSource;

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
    [SerializeField] private AudioClip normalFootstepClip;
    [SerializeField] private AudioClip snowFootstepClip;
    [SerializeField] private AudioClip entryExitClip;
    [SerializeField] private AudioClip dungeonPlateClip;
    [SerializeField] private AudioClip dungeonDoorClip;
    [SerializeField] private AudioClip dungeonTrapClip;
    [SerializeField] private AudioClip dungeonFireClip;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("Trap SFX")]
    [Range(0f, 1f)] [SerializeField] private float trapSfxVolume = 0.3f;

    [Header("Footsteps")]
    [Min(0f)] [SerializeField] private float footstepCooldown = 0.25f;
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.55f;
    [Min(0.1f)] [SerializeField] private float footstepPitchMin = 0.96f;
    [Min(0.1f)] [SerializeField] private float footstepPitchMax = 1.04f;
    [Tooltip("К текущему normalFootstepClip можно добавить ещё 4 варианта.")]
    [SerializeField] private AudioClip[] normalFootstepVariations = new AudioClip[4];
    [Tooltip("К текущему snowFootstepClip можно добавить ещё 4 варианта.")]
    [SerializeField] private AudioClip[] snowFootstepVariations = new AudioClip[4];

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
    private float lastFootstepTime = float.NegativeInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        trapSfxVolume = Mathf.Clamp01(trapSfxVolume);
        pitchMin = Mathf.Max(0.1f, pitchMin);
        pitchMax = Mathf.Max(pitchMin, pitchMax);
        volumeMin = Mathf.Clamp01(volumeMin);
        volumeMax = Mathf.Max(volumeMin, Mathf.Clamp01(volumeMax));
        maxObjectSfxDistance = Mathf.Max(0f, maxObjectSfxDistance);
        footstepCooldown = Mathf.Max(0f, footstepCooldown);
        footstepVolume = Mathf.Clamp01(footstepVolume);
        footstepPitchMin = Mathf.Max(0.1f, footstepPitchMin);
        footstepPitchMax = Mathf.Max(footstepPitchMin, footstepPitchMax);

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
    public void PlayFootstep(bool onSnow)
    {
        if (Time.time < lastFootstepTime + footstepCooldown)
            return;

        lastFootstepTime = Time.time;
        PlayFootstepSfx(onSnow
            ? GetRandomFootstepClip(snowFootstepClip, snowFootstepVariations)
            : GetRandomFootstepClip(normalFootstepClip, normalFootstepVariations));
    }
    public void PlayEntryExit() => PlaySfx(entryExitClip);
    public void PlayDungeonPlate() => PlaySfx(dungeonPlateClip);
    public void PlayDungeonDoor() => PlaySfx(dungeonDoorClip);
    public void PlayDungeonTrap() => PlayTrapSfx(dungeonTrapClip);
    public void PlayDungeonFire() => PlayTrapSfx(dungeonFireClip);

    public void PlayInteract(Vector3 sourcePosition) => PlaySfxNearPlayer(interactClip, sourcePosition);
    public void PlayDungeonPlate(Vector3 sourcePosition) => PlaySfxNearPlayer(dungeonPlateClip, sourcePosition);
    public void PlayDungeonDoor(Vector3 sourcePosition) => PlaySfxNearPlayer(dungeonDoorClip, sourcePosition);
    public void PlayDungeonTrap(Vector3 sourcePosition) => PlayTrapSfxNearPlayer(dungeonTrapClip, sourcePosition);
    public void PlayDungeonFire(Vector3 sourcePosition) => PlayTrapSfxNearPlayer(dungeonFireClip, sourcePosition);

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

    private AudioClip GetRandomFootstepClip(AudioClip mainClip, AudioClip[] variations)
    {
        if (variations == null || variations.Length == 0)
            return mainClip;

        int availableCount = mainClip != null ? 1 : 0;

        foreach (AudioClip variation in variations)
        {
            if (variation != null)
                availableCount++;
        }

        if (availableCount == 0)
            return null;

        int selectedIndex = Random.Range(0, availableCount);
        if (mainClip != null && selectedIndex-- == 0)
            return mainClip;

        foreach (AudioClip variation in variations)
        {
            if (variation != null && selectedIndex-- == 0)
                return variation;
        }

        return mainClip;
    }

    private void PlayFootstepSfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.pitch = Random.Range(footstepPitchMin, footstepPitchMax);
        sfxSource.PlayOneShot(clip, sfxVolume * footstepVolume);
    }

    private void PlayTrapSfx(AudioClip clip)
    {
        if (trapSfxSource == null || clip == null || trapSfxSource.isPlaying)
            return;

        float pitch = randomizeSfx ? Random.Range(pitchMin, pitchMax) : 1f;
        float volume = randomizeSfx ? Random.Range(volumeMin, volumeMax) : 1f;

        trapSfxSource.clip = clip;
        trapSfxSource.pitch = pitch;
        trapSfxSource.volume = sfxVolume * trapSfxVolume * volume;
        trapSfxSource.Play();
    }

    private void PlaySfxNearPlayer(AudioClip clip, Vector3 sourcePosition)
    {
        if (!IsSourceNearPlayer(sourcePosition))
            return;

        PlaySfx(clip);
    }

    private void PlayTrapSfxNearPlayer(AudioClip clip, Vector3 sourcePosition)
    {
        if (!IsSourceNearPlayer(sourcePosition))
            return;

        PlayTrapSfx(clip);
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

        if (trapSfxSource == null)
            trapSfxSource = sources.Length > 2 ? sources[2] : gameObject.AddComponent<AudioSource>();
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

        if (trapSfxSource != null)
        {
            trapSfxSource.playOnAwake = false;
            trapSfxSource.loop = false;
        }
    }
}
