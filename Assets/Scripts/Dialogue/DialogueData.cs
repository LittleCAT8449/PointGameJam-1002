using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueData", menuName = "Game/Dialogue Data")]
public sealed class DialogueData : ScriptableObject
{
    [SerializeField] private DialogueLine[] lines = Array.Empty<DialogueLine>();

    public int LineCount => lines != null ? lines.Length : 0;

    public DialogueLine GetLine(int index)
    {
        if (lines == null || index < 0 || index >= lines.Length)
            return null;

        return lines[index];
    }
}

[Serializable]
public sealed class DialogueLine
{
    [SerializeField] private string speaker;
    [SerializeField, TextArea(2, 5)] private string content;

    public string Speaker => speaker;
    public string Content => content;
}
