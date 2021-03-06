using System;
using System.Linq;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Components.Movement;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timers;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Movement
{

    [RegisterComponent]
    public class ServerTeleporterComponent : Component, IAfterAttack
    {
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IServerEntityManager _serverEntityManager;
        [Dependency] private readonly IRobustRandom _spreadRandom;
#pragma warning restore 649
        // TODO: Look at MapManager.Map for Beacons to get all entities on grid
        public ItemTeleporterState State => _state;

        public override string Name => "ItemTeleporter";

        [ViewVariables] private float _chargeTime;
        [ViewVariables] private float _cooldown;
        [ViewVariables] private int _range;
        [ViewVariables] private ItemTeleporterState _state;
        [ViewVariables] private TeleporterType _teleporterType;
        [ViewVariables] private string _departureSound;
        [ViewVariables] private string _arrivalSound;
        [ViewVariables] private string _cooldownSound;
        // If the direct OR random teleport will try to avoid hitting collidables
        [ViewVariables] private bool _avoidCollidable;
        [ViewVariables] private float _portalAliveTime;

        private AppearanceComponent _appearanceComponent;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _teleporterType, "teleporter_type", TeleporterType.Random);
            serializer.DataField(ref _range, "range", 15);
            serializer.DataField(ref _chargeTime, "charge_time", 0.2f);
            serializer.DataField(ref _cooldown, "cooldown", 2.0f);
            serializer.DataField(ref _avoidCollidable, "avoid_walls", true);
            serializer.DataField(ref _departureSound, "departure_sound", "/Audio/effects/teleport_departure.ogg");
            serializer.DataField(ref _arrivalSound, "arrival_sound", "/Audio/effects/teleport_arrival.ogg");
            serializer.DataField(ref _cooldownSound, "cooldown_sound", null);
            serializer.DataField(ref _portalAliveTime, "portal_alive_time", 5.0f);  // TODO: Change this to 0 before PR?
        }

        public override void OnRemove()
        {
            _appearanceComponent = null;

            base.OnRemove();
        }

        private void SetState(ItemTeleporterState newState)
        {
            if (newState == ItemTeleporterState.Cooldown)
            {
                _appearanceComponent.SetData(TeleporterVisuals.VisualState, TeleporterVisualState.Charging);
            }
            else
            {
                _appearanceComponent.SetData(TeleporterVisuals.VisualState, TeleporterVisualState.Ready);
            }
            _state = newState;
        }

        void IAfterAttack.AfterAttack(AfterAttackEventArgs eventArgs)
        {
            if (_teleporterType == TeleporterType.Directed)
            {
                var userTarget = eventArgs.ClickLocation.ToWorld(_mapManager);
                TryDirectedTeleport(eventArgs.User, userTarget);
            }

            if (_teleporterType == TeleporterType.Random)
            {
                TryRandomTeleport(eventArgs.User);
            }
        }

        public void TryDirectedTeleport(IEntity user, GridCoordinates grid)
        {
            // Checks
            if (user.Transform.GridPosition.Distance(_mapManager, grid) > _range)
            {
                return;
            }

            if (_state == ItemTeleporterState.On)
            {
                return;
            }
            if (_avoidCollidable)
            {
                foreach (var entity in _serverEntityManager.GetEntitiesIntersecting(grid))
                {
                    // Added this component to avoid stacking portals and causing shenanigans
                    // TODO: Doesn't do a great job of stopping stacking portals for directed
                    if (entity.HasComponent<CollidableComponent>() || entity.HasComponent<ServerTeleporterComponent>())
                    {
                        return;
                    }
                }
            }
            // Start / Continue
            if (_state == ItemTeleporterState.Off)
            {
                SetState(ItemTeleporterState.Charging);
                // Play charging sound here if you want
            }

            if (_state != ItemTeleporterState.Charging)
            {
                return;
            }

            Timer.Spawn(TimeSpan.FromSeconds(_chargeTime), () => Teleport(user, new Vector2(grid.X, grid.Y)));
            StartCooldown();
        }

        public void StartCooldown()
        {
            SetState(ItemTeleporterState.Cooldown);
            Timer.Spawn(TimeSpan.FromSeconds(_chargeTime + _cooldown), () => SetState(ItemTeleporterState.Off));
            if (_cooldownSound != null)
            {
                var soundPlayer = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AudioSystem>();
                soundPlayer.Play(_cooldownSound, Owner);
            }
        }

        public override void Initialize()
        {
            _appearanceComponent = Owner.GetComponent<AppearanceComponent>();
            _state = ItemTeleporterState.Off;
            base.Initialize();
        }

        private bool emptySpace(IEntity user, Vector2 target)
        {
            // TODO: Check the user's spot? Upside is no stacking TPs but downside is they can't unstuck themselves from walls.
            foreach (var entity in _serverEntityManager.GetEntitiesIntersecting(user.Transform.MapID, target))
            {
                if (entity.HasComponent<CollidableComponent>() || entity.HasComponent<ServerPortalComponent>())
                {
                    return false;
                }
            }
            return true;
        }

        private Vector2 randomEmptySpot(IEntity user, int range)
        {
            Vector2 targetVector = user.Transform.GridPosition.Position;
            // Definitely a better way to do this
            foreach (var i in Enumerable.Range(0, 5))
            {
                var randomRange = _spreadRandom.Next(0, range);
                var angle = Angle.FromDegrees(_spreadRandom.Next(0, 359));
                targetVector = user.Transform.GridPosition.Position + angle.ToVec() * randomRange;
                if (emptySpace(user, targetVector))
                {
                    return targetVector;
                }
                if (i == 19)
                {
                    return targetVector;
                }
            }

            return targetVector;
        }

        public void TryRandomTeleport(IEntity user)
        {
            // Checks
            if (_state == ItemTeleporterState.On)
            {
                return;
            }

            Vector2 targetVector;
            if (_avoidCollidable)
            {
                targetVector = randomEmptySpot(user, _range);
            }
            else
            {
               var randomRange = _spreadRandom.Next(0, _range);
               var angle = Angle.FromDegrees(_spreadRandom.Next(0, 359));
               targetVector = user.Transform.GridPosition.Position + angle.ToVec() * randomRange;
            }
            // Start / Continue
            if (_state == ItemTeleporterState.Off)
            {
                SetState(ItemTeleporterState.Charging);
            }

            if (_state != ItemTeleporterState.Charging)
            {
                return;
            }

            // Seemed easier to just start the cd timer at the same time
            Timer.Spawn(TimeSpan.FromSeconds(_chargeTime), () => Teleport(user, targetVector));
            StartCooldown();
        }

        public void Teleport(IEntity user, Vector2 vector)
        {
            // Messy maybe?
            GridCoordinates targetGrid = new GridCoordinates(vector, user.Transform.GridID);
            var soundPlayer = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AudioSystem>();

            // If portals use those, otherwise just move em over
            if (_portalAliveTime > 0.0f)
            {
                // Call Delete here as the teleporter should have control over portal longevity
                // Departure portal
                var departurePortal = _serverEntityManager.SpawnEntityAt("Portal", user.Transform.GridPosition);
                departurePortal.TryGetComponent<ServerPortalComponent>(out var departureComponent);

                // Arrival portal
                var arrivalPortal = _serverEntityManager.SpawnEntityAt("Portal", targetGrid);
                arrivalPortal.TryGetComponent<ServerPortalComponent>(out var arrivalComponent);

                // Connect. TODO: If the OnUpdate in ServerPortalComponent is changed this may need to change as well.
                arrivalComponent.TryConnectPortal(departurePortal);
            }
            else
            {
                // Departure
                soundPlayer.Play(_departureSound, user.Transform.GridPosition);

                // Arrival
                user.Transform.DetachParent();
                user.Transform.WorldPosition = vector;
                soundPlayer.Play(_arrivalSound, user.Transform.GridPosition);
            }

        }
    }
}
