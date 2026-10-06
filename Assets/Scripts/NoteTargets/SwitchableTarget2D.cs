using System;
using UnityEngine;

/// <summary>
/// A mechanism that cycles through configured visual and collider states when
/// reached by the second note's wave.
/// </summary>
[DisallowMultipleComponent]
public sealed class SwitchableTarget2D : MonoBehaviour, INoteWaveReceiver2D
{
    [Serializable]
    private struct PresetState
    {
        public string label;
        public Color tint;
        public bool visible;
        public bool blocksMovement;
    }

    [SerializeField] private SpriteRenderer[] visuals;
    [SerializeField] private Collider2D[] solidColliders;
    [SerializeField] private PresetState[] states =
    {
        new PresetState { label = "Closed", tint = Color.white, visible = true, blocksMovement = true },
        new PresetState { label = "Open", tint = Color.white, visible = false, blocksMovement = false }
    };
    [SerializeField, Min(0)] private int initialState;

    private int currentState;

    public bool IsWaveAvailable => states != null && states.Length > 1;
    public string CurrentStateLabel => states != null && states.Length > 0 ? states[currentState].label : "No states";

    private void Awake()
    {
        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<SpriteRenderer>(true);

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
        if (solidColliders == null || solidColliders.Length == 0)
        {
            solidColliders = Array.FindAll(
                allColliders,
                targetCollider => targetCollider != null && !targetCollider.isTrigger);
        }

        if (states == null || states.Length == 0)
        {
            Debug.LogError($"{nameof(SwitchableTarget2D)} on '{name}' needs at least one preset state.", this);
            return;
        }

        currentState = Mathf.Clamp(initialState, 0, states.Length - 1);
        ApplyCurrentState();
    }

    public float GetWaveContactDistance(Vector2 origin)
    {
        float targetRadius = 0f;
        if (visuals != null)
        {
            foreach (SpriteRenderer visual in visuals)
            {
                if (visual == null || visual.sprite == null)
                    continue;

                Bounds spriteBounds = visual.sprite.bounds;
                Vector3 scale = visual.transform.lossyScale;
                float extentX = spriteBounds.extents.x * Mathf.Abs(scale.x);
                float extentY = spriteBounds.extents.y * Mathf.Abs(scale.y);
                targetRadius = Mathf.Max(targetRadius, new Vector2(extentX, extentY).magnitude);
            }
        }

        return Mathf.Max(0f, Vector2.Distance(origin, transform.position) - targetRadius);
    }

    public bool ReceiveWave(NoteWaveKind kind)
    {
        if (kind != NoteWaveKind.Switch || !IsWaveAvailable)
            return false;

        AdvanceState();
        return true;
    }

    public void AdvanceState()
    {
        if (states == null || states.Length < 2)
            return;

        currentState = (currentState + 1) % states.Length;
        ApplyCurrentState();
        Debug.Log($"[{name}] switched to preset '{CurrentStateLabel}'.", this);
    }

    private void ApplyCurrentState()
    {
        PresetState state = states[currentState];

        if (visuals != null)
        {
            foreach (SpriteRenderer visual in visuals)
            {
                if (visual == null)
                    continue;

                visual.enabled = state.visible;
                visual.color = state.tint;
            }
        }

        if (solidColliders != null)
        {
            foreach (Collider2D targetCollider in solidColliders)
            {
                if (targetCollider != null)
                    targetCollider.enabled = state.blocksMovement;
            }
        }
    }
}
