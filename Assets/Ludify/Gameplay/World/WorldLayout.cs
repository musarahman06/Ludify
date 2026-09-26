using Ludify.Gameplay.Core;
using UnityEngine;

namespace Ludify.Gameplay.World
{
    /// <summary>
    /// Single source of truth for the CityMap geometry that was generated procedurally
    /// (terrain, river curve, bridge crossings, district bounds). Keep in sync with the
    /// terrain generator if the map is ever regenerated.
    /// </summary>
    public static class WorldLayout
    {
        public const float MapSize = 500f;

        /// <summary>Half-width of the full-depth river channel.</summary>
        public const float RiverChannelHalfWidth = 16f;

        /// <summary>Half-width of the channel including the sloped banks.</summary>
        public const float RiverBankHalfWidth = 26f;

        /// <summary>Bridge decks are 90 m long road-bridge tiles centered on the river.</summary>
        public const float BridgeHalfLength = 45f;
        public const float BridgeDeckWidth = 8f;

        /// <summary>World X of the river's center line at world Z (same curve that carved the terrain).</summary>
        public static float RiverCenterX(float z)
        {
            float t = (MapSize - z) / MapSize;
            return 250f + 40f * Mathf.Sin(t * 2.6f + 0.3f);
        }

        public readonly struct BridgeInfo
        {
            public readonly BridgeId Id;
            public readonly string DeckObjectName;
            public readonly float Z;

            public BridgeInfo(BridgeId id, string deckObjectName, float z)
            {
                Id = id;
                DeckObjectName = deckObjectName;
                Z = z;
            }

            public float CenterX => RiverCenterX(Z);
            public float WestEndX => CenterX - BridgeHalfLength;
            public float EastEndX => CenterX + BridgeHalfLength;
        }

        public static readonly BridgeInfo[] Bridges =
        {
            new BridgeInfo(BridgeId.Suspension, "SuspensionBridge_RoadDeck", 410f),
            new BridgeInfo(BridgeId.S1, "S1_Bridge_RoadDeck", 350f),
            new BridgeInfo(BridgeId.B2Truss, "B2_TrussBridge_RoadDeck", 240f),
            new BridgeInfo(BridgeId.B3Arch, "B3_ArchBridge_RoadDeck", 140f),
        };

        /// <summary>Ground-plane (XZ) footprint of each zone. West-bank zones stop short of the river; Downtown is the whole east bank.</summary>
        public static Rect ZoneRect(ZoneId zone)
        {
            switch (zone)
            {
                case ZoneId.Racetrack: return Rect.MinMaxRect(0f, 0f, 245f, 200f);
                case ZoneId.Farm: return Rect.MinMaxRect(0f, 200f, 245f, 300f);
                case ZoneId.Suburbs: return Rect.MinMaxRect(0f, 300f, 245f, MapSize);
                case ZoneId.Downtown: return Rect.MinMaxRect(275f, 0f, MapSize, MapSize);
                default: return Rect.zero;
            }
        }
    }
}
