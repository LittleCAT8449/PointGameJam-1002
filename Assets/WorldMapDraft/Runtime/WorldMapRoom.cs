using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldMapConnectionKind
{
    Walkway,
    KnowledgeGate,
    HiddenPassage,
    Shortcut,
    HubReturn,
    AutomaticTransition,
    Other
}

[Serializable]
public sealed class WorldMapSceneConnection
{
    public string id;
    public WorldMapRoom targetRoom;
    public string label;
    public WorldMapConnectionKind kind;
    public string sourceType;
    public string directionality;
    public bool oneWay;
    public List<string> requiresKnowledge = new List<string>();
    public bool shortcut;
    public bool hidden;
    public bool optional;
    public string openedFrom;
    public List<string> previewedFrom = new List<string>();
    [TextArea] public string notes;
}

public sealed class WorldMapRoom : MonoBehaviour
{
    public string roomId;
    public string roomName;
    public string regionId;
    public string regionName;
    [TextArea] public string function;
    public string pacingBeat;
    public List<string> grantsKnowledge = new List<string>();
    public bool optional;
    public bool hidden;
    public List<string> knowledgeVisibleBeforeMastery = new List<string>();
    [TextArea] public string recoveryAndReturn;
    public List<WorldMapSceneConnection> connections = new List<WorldMapSceneConnection>();

    public Vector2 RoomSize => new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));
}
