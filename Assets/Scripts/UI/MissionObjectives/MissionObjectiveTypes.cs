using System;

public enum MissionObjectiveStatus
{
    Active,
    Completed,
    Failed
}

[Serializable]
public struct MissionObjectiveViewData
{
    public string Id;
    public string Title;
    public string Detail;
    public MissionObjectiveStatus Status;
    public int SortPriority;

    public MissionObjectiveViewData(string id, string title, string detail, MissionObjectiveStatus status, int sortPriority)
    {
        Id = id;
        Title = title;
        Detail = detail;
        Status = status;
        SortPriority = sortPriority;
    }
}