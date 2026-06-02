using UnityEngine;

[CreateAssetMenu(fileName = "GuidanceEntry", menuName = "Farm/Guidance Entry")]
public class GuidanceEntry : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string title;
    [TextArea(2, 5)] [SerializeField] private string text;
    [SerializeField] private Sprite icon;
    [TextArea(2, 4)] [SerializeField] private string playerText;
    [TextArea(2, 4)] [SerializeField] private string npcText;

    public string Id => id;
    public string Title => string.IsNullOrWhiteSpace(title) ? PlayerText : title;
    public string Text => string.IsNullOrWhiteSpace(text) ? NpcText : text;
    public Sprite Icon => icon;
    public string PlayerText => playerText;
    public string NpcText => string.IsNullOrWhiteSpace(npcText) ? playerText : npcText;
}
