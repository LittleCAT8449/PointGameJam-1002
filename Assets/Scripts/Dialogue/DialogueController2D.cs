using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// Displays a DialogueData asset in preconfigured UI. Assign the panel and TMP
/// text fields in the Inspector, then call StartDialogue from another script.
/// </summary>
public sealed class DialogueController2D : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private DialogueData dialogueData;

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text advanceHintText;

    [Header("Input and Display")]
    [SerializeField] private Key advanceKey = Key.Space;
    [SerializeField] private Key alternateAdvanceKey = Key.Enter;
    [SerializeField, Min(0f)] private float charactersPerSecond = 35f;
    [SerializeField] private string advanceHint = "空格 / 回车 继续";

    [Header("Events")]
    [SerializeField] private UnityEvent onDialogueEnded;

    private DialogueData activeDialogue;
    private Coroutine typingCoroutine;
    private int lineIndex;
    private int visibleCharacterCount;
    private bool isTyping;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        if (advanceHintText != null)
            advanceHintText.text = advanceHint;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsPlaying || !WasAdvancePressed())
            return;

        if (isTyping)
        {
            FinishCurrentLine();
            return;
        }

        ShowLine(lineIndex + 1);
    }

    private void OnDisable()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = null;
        isTyping = false;
        IsPlaying = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    /// <summary>Starts the DialogueData assigned in the Inspector.</summary>
    public void StartDialogue()
    {
        StartDialogue(dialogueData);
    }

    /// <summary>Starts a specific dialogue asset, allowing triggers to choose content.</summary>
    public void StartDialogue(DialogueData data)
    {
        if (!HasValidUiReferences())
            return;

        if (data == null || data.LineCount == 0)
        {
            Debug.LogWarning("Cannot start dialogue: the DialogueData asset has no lines.", this);
            return;
        }

        StopTypingCoroutine();
        activeDialogue = data;
        lineIndex = -1;
        IsPlaying = true;
        dialoguePanel.SetActive(true);
        ShowLine(0);
    }

    /// <summary>Closes the dialogue and invokes the completion event.</summary>
    public void EndDialogue()
    {
        if (!IsPlaying)
            return;

        StopTypingCoroutine();
        IsPlaying = false;
        activeDialogue = null;
        dialoguePanel.SetActive(false);
        onDialogueEnded?.Invoke();
    }

    private bool WasAdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return (advanceKey != Key.None && keyboard[advanceKey].wasPressedThisFrame)
            || (alternateAdvanceKey != Key.None && keyboard[alternateAdvanceKey].wasPressedThisFrame);
    }

    private void ShowLine(int index)
    {
        if (activeDialogue == null || index >= activeDialogue.LineCount)
        {
            EndDialogue();
            return;
        }

        lineIndex = index;
        DialogueLine line = activeDialogue.GetLine(index);
        string speaker = line != null ? line.Speaker : string.Empty;
        string content = line != null ? line.Content : string.Empty;
        dialogueText.text = string.IsNullOrWhiteSpace(speaker) ? content : $"{speaker}: {content}";
        dialogueText.ForceMeshUpdate();

        visibleCharacterCount = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;
        typingCoroutine = StartCoroutine(TypeCurrentLine());
    }

    private IEnumerator TypeCurrentLine()
    {
        isTyping = true;
        if (charactersPerSecond <= 0f || visibleCharacterCount == 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
            typingCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (dialogueText.maxVisibleCharacters < visibleCharacterCount)
        {
            elapsed += Time.unscaledDeltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Min(
                visibleCharacterCount,
                Mathf.FloorToInt(elapsed * charactersPerSecond) + 1);
            yield return null;
        }

        isTyping = false;
        typingCoroutine = null;
    }

    private void FinishCurrentLine()
    {
        StopTypingCoroutine();
        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    private void StopTypingCoroutine()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = null;
        isTyping = false;
    }

    private bool HasValidUiReferences()
    {
        if (dialoguePanel != null && dialogueText != null)
            return true;

        Debug.LogError("Assign Dialogue Panel and Dialogue Text on DialogueController2D.", this);
        return false;
    }
}
