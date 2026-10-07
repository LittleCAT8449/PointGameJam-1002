using UnityEngine;

public enum NoteWaveKind
{
    Reveal,
    Switch,
    Gather,
    Scatter,
    Connect
}

/// <summary>
/// Implemented by a scene object that decides how it responds to a note wave.
/// The expanding wave only reports the note kind and contact event.
/// </summary>
public interface INoteWaveReceiver2D
{
    bool IsWaveAvailable { get; }
    float GetWaveContactDistance(Vector2 origin);
    bool ReceiveWave(NoteWaveKind kind);
}

/// <summary>
/// Optional exact contact-point provider for targets whose interaction shape
/// is not represented by an enabled Collider2D.
/// </summary>
public interface INoteWaveContactPoint2D
{
    Vector2 GetWaveContactPoint(Vector2 origin);
}
