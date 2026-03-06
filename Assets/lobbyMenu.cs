using UnityEngine;

public class lobbyMenu : MonoBehaviour
{
    [Header("Options")]
    public string[] rooms = { "Lab", "Warehouse", "Office" };
    public int[] playerCounts = { 2, 3, 4, 5, 6 };
    public string[] occupations = { "Engineer", "Medic", "Security" };

    [Header("Layout")]
    public float x = 10f;
    public float y = 10f;
    public float rowHeight = 45f;
    public float labelWidth = 160f;
    public float arrowWidth = 40f;
    public float valueWidth = 220f;

    public int RoomIndex { get; private set; }
    public int PlayerCountIndex { get; private set; }
    public int OccupationIndex { get; private set; }

    public string SelectedRoom => (rooms != null && rooms.Length > 0) ? rooms[RoomIndex] : "";
    public int SelectedPlayers => (playerCounts != null && playerCounts.Length > 0) ? playerCounts[PlayerCountIndex] : 0;
    public string SelectedOccupation => (occupations != null && occupations.Length > 0) ? occupations[OccupationIndex] : "";

    void OnGUI()
    {
        // Guard against empty arrays
        if (rooms == null || rooms.Length == 0) rooms = new[] { "Room 1" };
        if (playerCounts == null || playerCounts.Length == 0) playerCounts = new[] { 2 };
        if (occupations == null || occupations.Length == 0) occupations = new[] { "Occupation" };

        RoomIndex = Mathf.Clamp(RoomIndex, 0, rooms.Length - 1);
        PlayerCountIndex = Mathf.Clamp(PlayerCountIndex, 0, playerCounts.Length - 1);
        OccupationIndex = Mathf.Clamp(OccupationIndex, 0, occupations.Length - 1);

        float yy = y;

        RoomIndex = DrawArrowSelector(
            new Rect(x, yy, labelWidth + arrowWidth + valueWidth + arrowWidth, rowHeight),
            "Select Room",
            rooms,
            RoomIndex
        );
        yy += rowHeight + 10f;

        // Convert int[] to string labels for display
        string[] playerLabels = new string[playerCounts.Length];
        for (int i = 0; i < playerCounts.Length; i++) playerLabels[i] = playerCounts[i].ToString();

        PlayerCountIndex = DrawArrowSelector(
            new Rect(x, yy, labelWidth + arrowWidth + valueWidth + arrowWidth, rowHeight),
            "Number of Players",
            playerLabels,
            PlayerCountIndex
        );
        yy += rowHeight + 10f;

        OccupationIndex = DrawArrowSelector(
            new Rect(x, yy, labelWidth + arrowWidth + valueWidth + arrowWidth, rowHeight),
            "Occupation",
            occupations,
            OccupationIndex
        );
    }

    int DrawArrowSelector(Rect rowRect, string label, string[] options, int index)
    {
        // Label
        GUI.Label(new Rect(rowRect.x, rowRect.y + 12f, labelWidth, rowRect.height), label);

        // Left arrow
        if (GUI.Button(new Rect(rowRect.x + labelWidth, rowRect.y, arrowWidth, rowRect.height), "<"))
            index = Wrap(index - 1, options.Length);

        // Value (clicking it also advances, optional)
        string value = options[index];
        if (GUI.Button(new Rect(rowRect.x + labelWidth + arrowWidth, rowRect.y, valueWidth, rowRect.height), value))
            index = Wrap(index + 1, options.Length);

        // Right arrow
        if (GUI.Button(new Rect(rowRect.x + labelWidth + arrowWidth + valueWidth, rowRect.y, arrowWidth, rowRect.height), ">"))
            index = Wrap(index + 1, options.Length);

        return index;
    }

    int Wrap(int i, int len)
    {
        if (len <= 0) return 0;
        i %= len;
        if (i < 0) i += len;
        return i;
    }
}