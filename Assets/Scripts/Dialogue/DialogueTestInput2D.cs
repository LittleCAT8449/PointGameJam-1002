using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Temporary test input: press 0 to start the dialogue assigned to the controller.
/// </summary>
public sealed class DialogueTestInput2D : MonoBehaviour
{
    [SerializeField] private DialogueController2D dialogueController;
    [SerializeField] private Key testKey = Key.Digit0;
    [SerializeField] private bool acceptNumpadZero = true;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || dialogueController == null)
            return;

        bool pressed = testKey != Key.None && keyboard[testKey].wasPressedThisFrame;
        if (acceptNumpadZero)
            pressed |= keyboard[Key.Numpad0].wasPressedThisFrame;

        if (pressed)
            dialogueController.StartDialogue();
    }
}
