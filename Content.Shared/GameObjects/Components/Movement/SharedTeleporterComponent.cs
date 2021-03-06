using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Movement
{
    public enum ItemTeleporterState
    {
        Off,
        Charging,
        On,
        Cooldown,
    }

    public enum TeleporterType
    {
        Directed,
        Random,
        Beacon,
    }

    [NetSerializable]
    [Serializable]
    public enum TeleporterVisuals
    {
        VisualState,
    }

    [NetSerializable]
    [Serializable]
    public enum TeleporterVisualState
    {
        Ready,
        Charging,
    }

    [Serializable, NetSerializable]
    public class TeleportMessage : ComponentMessage
    {
        public readonly GridCoordinates Target;

        public TeleportMessage(GridCoordinates target)
        {
            Target = target;
        }
    }
}
