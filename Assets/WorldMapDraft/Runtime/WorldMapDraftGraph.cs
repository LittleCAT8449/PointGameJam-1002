using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMapDraftGraph : MonoBehaviour
{
    public WorldMapMeta meta = new WorldMapMeta();
    public List<WorldMapRegionData> regions = new List<WorldMapRegionData>();
    public List<WorldMapKnowledgeData> knowledge = new List<WorldMapKnowledgeData>();
    public List<WorldMapGateData> gates = new List<WorldMapGateData>();
    public List<WorldMapLoopData> explorationLoops = new List<WorldMapLoopData>();
    public List<WorldMapEarlySightlineData> earlySightlines = new List<WorldMapEarlySightlineData>();
    public WorldMapLayoutData layout = new WorldMapLayoutData();
    public List<string> criticalPath = new List<string>();
    public List<string> progressionAssumptions = new List<string>();
    public List<WorldMapRoom> rooms = new List<WorldMapRoom>();
    public float unityUnitsPer100Pixels = 1f;
    public float sourcePixelCenterX;
    public float sourcePixelCenterY;
}
