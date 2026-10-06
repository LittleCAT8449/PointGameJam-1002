using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-side input and wave emission only. Targets receive the wave kind and
/// own the behavior that follows.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement2D))]
public sealed class NoteWaveEmitter2D : MonoBehaviour
{
    [Serializable]
    private sealed class NoteBinding
    {
        public NoteWaveKind kind;
        [Tooltip("Input Actions 里的动作名；继续支持鼠标、手柄等非键盘输入。")]
        public string actionName;
        [Tooltip("键盘按键；设为 None 时，这条绑定只能通过代码调用 Emit。")]
        public Key key = Key.None;
        [Min(0.1f)] public float maxRadius = 11f;
        [Min(0.1f)] public float expansionSpeed = 15f;
        [Min(0f)] public float cooldown = 0.35f;
        public Color waveColor = Color.white;
        public bool debugDisplay = true;
    }

    [Header("Non-keyboard Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";

    [Header("Notes")]
    [SerializeField] private NoteBinding[] noteBindings =
    {
        new NoteBinding
        {
            kind = NoteWaveKind.Reveal,
            actionName = "Attack",
            key = Key.Enter,
            waveColor = new Color(0.42f, 0.86f, 1f, 0.9f)
        },
        new NoteBinding
        {
            kind = NoteWaveKind.Switch,
            actionName = "Interact",
            key = Key.E,
            waveColor = new Color(1f, 0.45f, 0.2f, 0.9f)
        },
        new NoteBinding
        {
            kind = NoteWaveKind.Gather,
            key = Key.Digit3,
            waveColor = new Color(0.32f, 1f, 0.58f, 0.95f)
        },
        new NoteBinding
        {
            kind = NoteWaveKind.Scatter,
            key = Key.Digit4,
            waveColor = new Color(1f, 0.42f, 0.9f, 0.95f)
        },
        new NoteBinding
        {
            kind = NoteWaveKind.Connect,
            key = Key.Digit5,
            waveColor = new Color(0.55f, 0.9f, 1f, 0.95f)
        }
    };

    private readonly List<InputAction> subscribedActions = new List<InputAction>();
    private readonly Dictionary<NoteWaveKind, float> nextCastTimeByKind = new Dictionary<NoteWaveKind, float>();
    private InputActionMap playerActionMap;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogWarning($"No Input Actions asset assigned to {nameof(NoteWaveEmitter2D)} on '{name}'. Keyboard keys still work.", this);
            return;
        }

        playerActionMap = inputActions.FindActionMap(actionMapName, false);
        if (playerActionMap == null)
        {
            Debug.LogWarning($"Input action map '{actionMapName}' was not found. Keyboard keys still work.", this);
            return;
        }

        foreach (NoteBinding binding in noteBindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.actionName))
                continue;

            if (playerActionMap.FindAction(binding.actionName, false) == null)
                Debug.LogWarning($"Input action '{actionMapName}/{binding.actionName}' was not found.", this);
        }
    }

    private void OnEnable()
    {
        subscribedActions.Clear();
        if (playerActionMap == null || noteBindings == null)
            return;

        HashSet<InputAction> uniqueActions = new HashSet<InputAction>();
        foreach (NoteBinding binding in noteBindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.actionName))
                continue;

            InputAction action = playerActionMap.FindAction(binding.actionName, false);
            if (action != null && uniqueActions.Add(action))
            {
                action.performed += OnNoteActionPerformed;
                subscribedActions.Add(action);
            }
        }
    }

    private void OnDisable()
    {
        foreach (InputAction action in subscribedActions)
        {
            if (action != null)
                action.performed -= OnNoteActionPerformed;
        }

        subscribedActions.Clear();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || noteBindings == null)
            return;

        foreach (NoteBinding binding in noteBindings)
        {
            if (binding == null || binding.key == Key.None)
                continue;

            if (keyboard[binding.key].wasPressedThisFrame)
                Emit(binding.kind);
        }
    }

    private void OnNoteActionPerformed(InputAction.CallbackContext context)
    {
        // Keyboard bindings are controlled only by the per-note Key field below.
        // Keep Input Actions for other devices such as mouse and gamepad.
        if (context.control != null && context.control.device is Keyboard)
            return;

        if (noteBindings == null)
            return;

        foreach (NoteBinding binding in noteBindings)
        {
            if (binding != null && string.Equals(binding.actionName, context.action.name, StringComparison.Ordinal))
            {
                Emit(binding.kind);
                return;
            }
        }
    }

    /// <summary>
    /// Public entry point for a future note wheel or selection UI.
    /// </summary>
    public void Emit(NoteWaveKind kind)
    {
        NoteBinding binding = FindBinding(kind);
        if (binding == null)
        {
            Debug.LogWarning($"No input/wave configuration exists for note '{kind}'.", this);
            return;
        }

        if (nextCastTimeByKind.TryGetValue(kind, out float nextCastTime) && Time.time < nextCastTime)
            return;

        nextCastTimeByKind[kind] = Time.time + binding.cooldown;

        GameObject waveObject = new GameObject($"{GetNoteLabel(kind)} - Expanding Note Wave");
        waveObject.transform.position = transform.position;
        ExpandingNoteWave2D wave = waveObject.AddComponent<ExpandingNoteWave2D>();
        wave.Initialize(kind, binding.maxRadius, binding.expansionSpeed, binding.waveColor, binding.debugDisplay);
    }

    private NoteBinding FindBinding(NoteWaveKind kind)
    {
        if (noteBindings == null)
            return null;

        foreach (NoteBinding binding in noteBindings)
        {
            if (binding != null && binding.kind == kind)
                return binding;
        }

        return null;
    }

    private static string GetNoteLabel(NoteWaveKind kind)
    {
        return kind switch
        {
            NoteWaveKind.Reveal => "显",
            NoteWaveKind.Switch => "转",
            NoteWaveKind.Gather => "聚",
            NoteWaveKind.Scatter => "散",
            NoteWaveKind.Connect => "连",
            _ => kind.ToString()
        };
    }
}
