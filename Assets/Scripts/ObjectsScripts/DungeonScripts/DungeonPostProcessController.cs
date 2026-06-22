using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class DungeonPostProcessController : MonoBehaviour
{
    [SerializeField] private Volume volume;
    [SerializeField] private VolumeProfile normalProfile;
    [SerializeField] private VolumeProfile dungeonProfile;

    void Awake()
    {
        EnsureVolume();

        if (normalProfile == null && volume != null)
            normalProfile = volume.sharedProfile;
    }

    void Reset()
    {
        EnsureVolume();

        if (normalProfile == null && volume != null)
            normalProfile = volume.sharedProfile;
    }

    public void SetDungeonMode(bool active)
    {
        EnsureVolume();
        if (volume == null)
            return;

        VolumeProfile targetProfile = active ? dungeonProfile : normalProfile;
        if (targetProfile != null)
            volume.sharedProfile = targetProfile;
    }

    private void EnsureVolume()
    {
        if (volume == null)
            volume = GetComponent<Volume>();
    }
}
