using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RoomTradeStationAuditUtility
{
    [MenuItem("Tools/Regolith/Audit Room Trade Stations")]
    public static void AuditRoomTradeStations()
    {
        RoomTradeStation[] stations = Object.FindObjectsByType<RoomTradeStation>(FindObjectsInactive.Include);

        if (stations.Length == 0)
        {
            Debug.Log("RoomTradeStation audit: no RoomTradeStation components found in the active scene.");
            return;
        }

        Debug.Log("RoomTradeStation audit found " + stations.Length + " station component(s).");

        foreach (RoomTradeStation station in stations)
        {
            if (station == null)
            {
                continue;
            }

            string path = RoomTradeStation.GetHierarchyPath(station.transform);
            Collider collider = station.GetComponent<Collider>();
            Renderer[] childRenderers = station.GetComponentsInChildren<Renderer>(true);
            Renderer[] serializedRenderers = GetSerializedHighlightRenderers(station);
            int nullSerializedRenderers = serializedRenderers != null
                ? serializedRenderers.Count(renderer => renderer == null)
                : 0;

            string suspicious = string.Empty;
            if (collider == null)
            {
                suspicious += " Missing Collider.";
            }
            else if (!collider.isTrigger)
            {
                suspicious += " Collider is not a trigger.";
            }

            if (childRenderers.Length == 0 && (serializedRenderers == null || serializedRenderers.Length == 0))
            {
                suspicious += " No child or serialized highlight renderers.";
            }

            if (station.PromptPoint == null)
            {
                suspicious += " PromptPoint missing.";
            }

            Debug.Log(
                "RoomTradeStation audit:\n" +
                "- Path: " + path + "\n" +
                "- Station: " + station.StationName + " (" + station.Type + ")\n" +
                "- Collider: " + (collider != null ? collider.GetType().Name : "missing") + "\n" +
                "- Collider is trigger: " + (collider != null && collider.isTrigger) + "\n" +
                "- Child renderers: " + childRenderers.Length + "\n" +
                "- Serialized highlight renderers: " + (serializedRenderers != null ? serializedRenderers.Length : 0) + "\n" +
                "- Null serialized highlight renderers: " + nullSerializedRenderers + "\n" +
                "- Prompt point assigned: " + (station.PromptPoint != null) + "\n" +
                "- Notes:" + (string.IsNullOrWhiteSpace(suspicious) ? " OK" : suspicious),
                station);
        }
    }

    private static Renderer[] GetSerializedHighlightRenderers(RoomTradeStation station)
    {
        SerializedObject serializedStation = new SerializedObject(station);
        SerializedProperty renderersProperty = serializedStation.FindProperty("highlightRenderers");
        if (renderersProperty == null || !renderersProperty.isArray)
        {
            return null;
        }

        Renderer[] renderers = new Renderer[renderersProperty.arraySize];
        for (int i = 0; i < renderersProperty.arraySize; i++)
        {
            renderers[i] = renderersProperty.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
        }

        return renderers;
    }
}
