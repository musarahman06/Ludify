using System;

namespace Ludify.Gameplay.Core
{
    /// <summary>
    /// Static hub for cross-system notifications. Systems raise and subscribe here instead of
    /// referencing or polling each other. Events are added milestone by milestone as their payload
    /// types come into existence.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GameMode, GameMode> GameModeChanged;   // (previous, current)
        public static event Action<ZoneId> ZoneEntered;
        public static event Action<ZoneId> ZoneDiscovered;

        public static void RaiseGameModeChanged(GameMode previous, GameMode current) => GameModeChanged?.Invoke(previous, current);
        public static void RaiseZoneEntered(ZoneId zone) => ZoneEntered?.Invoke(zone);
        public static void RaiseZoneDiscovered(ZoneId zone) => ZoneDiscovered?.Invoke(zone);
    }
}
