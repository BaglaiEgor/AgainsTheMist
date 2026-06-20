using UnityEngine;

[DisallowMultipleComponent]
public class CreepySpriteAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [Min(0.02f)] [SerializeField] private float frameTime = 0.12f;
    [SerializeField] private bool playOnEnable = true;

    private int frameIndex;
    private float timer;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        SetFrame(0);
    }

    private void OnEnable()
    {
        if (!playOnEnable)
            return;

        frameIndex = 0;
        timer = 0f;
        SetFrame(frameIndex);
    }

    private void Update()
    {
        if (!playOnEnable || frames == null || frames.Length <= 1)
            return;

        timer += Time.deltaTime;
        if (timer < frameTime)
            return;

        timer -= frameTime;
        frameIndex = (frameIndex + 1) % frames.Length;
        SetFrame(frameIndex);
    }

    private void SetFrame(int index)
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0)
            return;

        Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        if (frame != null)
            spriteRenderer.sprite = frame;
    }
}
