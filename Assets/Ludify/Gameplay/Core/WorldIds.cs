namespace Ludify.Gameplay.Core
{
    /// <summary>Map zones shown as nodes on the minimap. Downtown covers all three east-bank districts.</summary>
    public enum ZoneId
    {
        Racetrack,
        Farm,
        Suburbs,
        Downtown,
    }

    /// <summary>The four river crossings, north to south.</summary>
    public enum BridgeId
    {
        Suspension,
        S1,
        B2Truss,
        B3Arch,
    }

    public enum GameMode
    {
        Loading,
        Exploring,
        Driving,
        Racing,
        Inspecting,
        InMenu,
    }
}
