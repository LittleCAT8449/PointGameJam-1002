using UnityEngine;

/// <summary>
/// A target that fades into view when reached by the first note's expanding wave.
/// Solid colliders stay disabled until the fade has completed.
/// </summary>
[DisallowMultipleComponent]
public sealed class RevealableTarget2D : MonoBehaviour, INoteWaveReceiver2D
{
    [SerializeField] private bool startsHidden = true;
    [SerializeField] private SpriteRenderer[] visuals;
    [SerializeField] private Collider2D[] solidColliders;
    [SerializeField, Min(0f)] private float revealDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;

    public bool IsRevealed { get; private set; }
    public bool IsRevealing { get; private set; }
    public bool IsWaveAvailable => !IsRevealed && !IsRevealing;

    private Color[] visibleColors;

    private void Awake()
    {
        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<SpriteRenderer>(true);

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);

        if (solidColliders == null || solidColliders.Length == 0)
        {
            solidColliders = System.Array.FindAll(
                allColliders,
                targetCollider => targetCollider != null && !targetCollider.isTrigger);
        }

        if (startsHidden)
            SetHidden();
        else
        {
            IsRevealed = true;
            SetSolidCollidersEnabled(true);
        }
    }

    public void Reveal()
    {
        if (IsRevealed || IsRevealing)
            return;

        if (revealDuration <= 0f)
        {
            SetAlpha(1f);
            FinishReveal();
            return;
        }

        StartCoroutine(FadeIn());
    }

    private void SetHidden()
    {
        visibleColors = new Color[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
        {
            SpriteRenderer visual = visuals[i];
            if (visual == null)
                continue;

            visibleColors[i] = visual.color;
            visibleColors[i].a = visibleAlpha;
            visual.enabled = true;
            Color hiddenColor = visual.color;
            hiddenColor.a = 0f;
            visual.color = hiddenColor;
        }

        SetSolidCollidersEnabled(false);
    }

    public float GetRevealDistanceFrom(Vector2 origin)
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

    public float GetWaveContactDistance(Vector2 origin)
    {
        return GetRevealDistanceFrom(origin);
    }

    public bool ReceiveWave(NoteWaveKind kind)
    {
        if (kind != NoteWaveKind.Reveal || !IsWaveAvailable)
            return false;

        Reveal();
        return true;
    }

    private System.Collections.IEnumerator FadeIn()
    {
        IsRevealing = true;
        float elapsed = 0f;

        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(elapsed / revealDuration));
            yield return null;
        }

        SetAlpha(1f);
        FinishReveal();
    }

    private void SetAlpha(float progress)
    {
        if (visuals == null || visibleColors == null)
            return;

        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] == null)
                continue;

            Color color = visibleColors[i];
            color.a = visibleAlpha * progress;
            visuals[i].color = color;
        }
    }

    private void FinishReveal()
    {
        IsRevealing = false;
        IsRevealed = true;
        SetSolidCollidersEnabled(true);
    }

    private void SetSolidCollidersEnabled(bool value)
    {
        if (solidColliders == null)
            return;

        foreach (Collider2D targetCollider in solidColliders)
        {
            if (targetCollider != null)
                targetCollider.enabled = value;
        }
    }
}
