// File:Fleet.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Threading;

using Supremacy.Collections;
using Supremacy.Diplomacy;
using Supremacy.Game;
using Supremacy.IO.Serialization;
using Supremacy.Resources;
using Supremacy.Universe;

using System.Linq;

namespace Supremacy.Orbitals
{
    /// <summary>
    /// Represents a fleet of ships in the game.
    /// </summary>
    [Serializable]
    public class Fleet : UniverseObject, IContactCenter, IGameUnit
    {
        #region Fields
        private CollectionBase<Ship> _ships;
        private bool _isInTow;
        private bool _areShipsLocked;
        private bool _isRouteLocked;
        private bool _isOrderLocked;
        private FleetOrder _order;
        private TravelRoute _route;
        [NonSerialized]
        private int _movementSempaphore;
        private UnitActivity _activity;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the type of the UniverseObject.
        /// </summary>
        /// <value>The type of the UniverseObject.</value>
        public sealed override UniverseObjectType ObjectType => UniverseObjectType.Fleet;

        /// <summary>
        /// Gets or sets the name of this <see cref="Fleet"/>.
        /// </summary>
        /// <value>The name.</value>
        public override string Name
        {
            get
            {
                string _NameString = "";

                if (_ships.Count == 0)
                {
                    _NameString = base.Name;
                }

                if (_ships.Count == 1)
                {
                    _NameString = _ships[0].IsCloaked == true
                        ? _ships[0].Name + " " + ResourceManager.GetString("CLOAKED")
                        : _ships[0].IsCamouflaged == true ? _ships[0].Name + " " + ResourceManager.GetString("CAMOUFLAGED") : _ships[0].Name;
                }

                if (_ships.Count > 1)
                {
                    ShipDesign design = null;

                    foreach (Ship ship in _ships)
                    {
                        if (design == null)
                        {
                            design = ship.ShipDesign;

                            if (design == null)
                            {
                                _NameString = string.Format(
                                    "{0}x {1}",
                                    _ships.Count, " Unknown ShipDesign");
                            }
                            else
                            {
                                _NameString = string.Format(
                                    "{0}x {1}",
                                    _ships.Count,
                                    ResourceManager.GetString(design.Name));
                            }
                        }

                        if (design != ship.ShipDesign)
                        {
                            _NameString = string.Format(ResourceManager.GetString("MULTI_SHIP_FLEET_FORMAT"), _ships.Count);
                        }

                        if (ship.IsCloaked)
                        {
                            _NameString += string.Format(ResourceManager.GetString("CLOAKED"));
                        }

                        if (ship.IsCamouflaged)
                        {
                            _NameString += string.Format(ResourceManager.GetString("CAMOUFLAGED"));
                        }
                    }

                    if (design == null || design.Name == null)
                    {
                        _NameString = string.Format(ResourceManager.GetString("MULTI_SHIP_FLEET_FORMAT"), _ships.Count);
                    }
                    //ToDo: After a changed (Cloaked/Camouflaged) a 'RefreshTaskListView' has to be done, but didn't found a way to do it directly
                }

                return _NameString;
            }
        }

        /// <summary>
        /// Gets or sets the ClassName of this <see cref="Fleet"/>.
        /// </summary>
        /// <value>The name.</value>
        public string ClassName
        {
            get
            {
                if (_ships.Count == 0)
                {
                    return string.Format(ResourceManager.GetString("UNKNOWN"));
                }

                if (_ships.Count == 1)
                {
                    return _ships[0].ClassName;
                }

                ShipDesign design = null;

                foreach (Ship ship in _ships)
                {
                    if (design == null)
                    {
                        design = ship.ShipDesign;
                    }

                    if (design != ship.ShipDesign)
                    {
                        return string.Format(ResourceManager.GetString("MULTI_SHIP_CLASS_MESSAGE"));
                    }
                }

                if (design == null || design.Name == null)
                {
                    return string.Format(ResourceManager.GetString("MULTI_SHIP_CLASS_MESSAGE"));
                }

                return string.Format(
                    "{0}x {1}",
                    _ships.Count,
                    ResourceManager.GetString(design.ClassName));
            }
        }












































        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Fleet"/> is in tow.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is in tow; otherwise, <c>false</c>.
        /// </value>
        public bool IsInTow
        {
            get => _isInTow;
            set
            {
                _isInTow = value;
                OnPropertyChanged("IsInTow");
                OnPropertyChanged("IsVisible");
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Fleet"/> is visible.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisible => !_isInTow;

        /// <summary>
        /// Gets or sets a value indicating whether the list of ships in this <see cref="Fleet"/> is locked.
        /// </summary>
        /// <value><c>true</c> if the list of ships is locked; otherwise, <c>false</c>.</value>
        public bool AreShipsLocked
        {
            get => _areShipsLocked;
            protected set => _areShipsLocked = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the route of this <see cref="Fleet"/> is locked.
        /// </summary>
        /// <value>
        /// <c>true</c> if the route is locked; otherwise, <c>false</c>.
        /// </value>
        public bool IsRouteLocked
        {
            get => _isRouteLocked;
            protected set => _isRouteLocked = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the order of this <see cref="Fleet"/> is locked.
        /// </summary>
        /// <value>
        /// <c>true</c> if the order is locked; otherwise, <c>false</c>.
        /// </value>
        public bool IsOrderLocked
        {
            get => _isOrderLocked;
            protected set => _isOrderLocked = value;
        }

        /// <summary>
        /// Gets or sets the route.
        /// </summary>
        /// <value>The route.</value>
        public TravelRoute Route
        {
            get => _route;
            set => SetRoute(value);
        }

        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        /// <value>The order.</value>
        public FleetOrder Order
        {
            get => _order;
            set => SetOrder(value);
        }

        /// <summary>
        /// Gets the maximum range.
        /// </summary>
        /// <value>The maximum range.</value>
        public int Range
        {
            get
            {
                int range = -1;
                foreach (Ship ship in Ships)
                {
                    if ((ship.Range < range) || (range == -1))
                    {
                        range = ship.Range;
                    }
                }
                range = Math.Max(0, range);
                return range;
            }
        }

        /// <summary>
        /// Gets the maximum speed.
        /// </summary>
        /// <value>The maximum speed.</value>
        public int Speed
        {
            get
            {
                int speed = -1;
                foreach (Ship ship in Ships)
                {
                    if ((ship.Speed < speed) || (speed == -1))
                    {
                        speed = ship.Speed;
                    }
                }
                speed = Math.Max(0, speed);
                return speed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> can move.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> can move; otherwise, <c>false</c>.
        /// </value>
        public override bool CanMove => Speed > 0;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> is stranded.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is stranded; otherwise, <c>false</c>.
        /// </value>
        public bool IsStranded
        {
            get
            {
                foreach (Ship ship in Ships)
                {
                    if (ship.IsStranded)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the lowest fuel level of any <see cref="Ship"/> in this <see cref="Fleet"/>.
        /// </summary>
        /// <value>The lowest fuel level.</value>
        public int LowestFuelLevel
        {
            get
            {
                int result = byte.MaxValue;
                foreach (Ship ship in Ships)
                {
                    result = Math.Min(ship.FuelReserve.CurrentValue, result);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets the maximum sensor range.
        /// </summary>
        /// <value>The maximum sensor range.</value>
        public int SensorRange
        {
            get
            {
                int sensorRange = 0;
                foreach (Ship ship in Ships)
                {
                    if (ship.ShipDesign.SensorRange > sensorRange)
                    {
                        sensorRange = ship.ShipDesign.SensorRange;
                    }
                }
                return sensorRange;
            }
        }

        /// <summary>
        /// Gets the maximum scan strength.
        /// </summary>
        /// <value>The maximum scan strength.</value>
        public int ScanStrength
        {
            get
            {
                int scanStrength = 0;
                if (Ships.Count > 0)
                {
                    foreach (Ship ship in Ships)
                    {
                        if (ship.ShipDesign.ScanStrength > scanStrength)
                        {
                            scanStrength = ship.ShipDesign.ScanStrength;
                        }
                    }
            }
                return scanStrength;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> is combatant.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is combatant; otherwise, <c>false</c>.
        /// </value>
        public bool IsCombatant => Ships.Any(o => o.IsCombatant);

        public bool IsBattleFleet => !IsScout && !IsFastAttack && Ships.All(
                    o => o.IsCombatant &&
                         o.ShipType != ShipType.Colony &&
                         o.ShipType != ShipType.Construction &&
                         o.ShipType != ShipType.Medical &&
                         o.ShipType != ShipType.Science &&
                         o.ShipType != ShipType.Transport &&
                         o.ShipType != ShipType.Diplomatic &&
                         o.ShipType != ShipType.Spy);

        public bool HasCommandShip => Ships.Any(o => o.ShipType == ShipType.Command);

        public bool IsScout => Ships.Count == 1 && Ships[0].ShipType == ShipType.Scout;

        public bool IsFastAttack => Ships.Count == 1 && Ships[0].ShipType == ShipType.FastAttack;

        public bool IsTransport => Ships.Count == 1 && Ships[0].ShipType == ShipType.Transport;
        public bool IsColonizer => Ships.Count == 1 && Ships[0].ShipType == ShipType.Colony;
        public bool MultiFleetHasAColonizer => Ships.Any(s => s.ShipType == ShipType.Colony) && Ships.Any(t => t.ShipType >= ShipType.FastAttack);
        public bool IsConstructor => Ships.Count == 1 && Ships[0].ShipType == ShipType.Construction;
        public bool MultiFleetHasAConstructor => Ships.Any(s => s.ShipType == ShipType.Construction) && Ships.Any(t => t.ShipType >= ShipType.FastAttack);

        public bool IsDiplomatic => Ships.Count == 1 && Ships[0].ShipType == ShipType.Diplomatic;

        public bool IsSpy => Ships.Count == 1 && Ships[0].ShipType == ShipType.Spy;

        public bool IsMedical => Ships.Count == 1 && Ships[0].ShipType == ShipType.Medical;

        public bool IsScience => Ships.Count == 1 && Ships[0].ShipType == ShipType.Science;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> contains any troop transport ships.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> contains any troop transport ships; otherwise, <c>false</c>.
        /// </value>
        public bool HasTroopTransports => Ships.Any(o => o.ShipDesign.ShipType == ShipType.Transport);

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> can cloak.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> can cloak; otherwise, <c>false</c>.
        /// </value>
        public bool CanCloak
        {
            get
            {
                foreach (Ship ship in Ships)
                {
                    if (ship.CanCloak)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Fleet"/> is cloaked.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is cloaked; otherwise, <c>false</c>.
        /// </value>
        public bool IsCloaked
        {
            get
            {
                foreach (Ship ship in Ships)
                {
                    if (ship.IsCloaked)
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                foreach (Ship ship in Ships)
                {
                    ship.IsCloaked = value;
                }

                OnPropertyChanged("IsCloaked");
            }
        }
        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> can camouflag.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> can camouflag; otherwise, <c>false</c>.
        /// </value>
        public bool CanCamouflage
        {
            get
            {
                foreach (Ship ship in Ships)
                {
                    if (ship.CanCamouflage)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Fleet"/> is camouflaged.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is camouflaged; otherwise, <c>false</c>.
        /// </value>
        public bool IsCamouflaged
        {
            get
            {
                foreach (Ship ship in Ships)
                {
                    if (ship.IsCamouflaged)
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                foreach (Ship ship in Ships)
                {
                    ship.IsCamouflaged = value;
                }

                OnPropertyChanged("IsCamouflaged");
            }
        }
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Fleet"/> is assimilated.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> is assimilated; otherwise, <c>false</c>.
        /// </value>
        public bool IsAssimilated
        {
            get
            {
                foreach (Ship ship in Ships)
                {
                    if (ship.IsAssimilated)
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                foreach (Ship ship in Ships)
                {
                    ship.IsAssimilated = value;
                }

                OnPropertyChanged("IsAssimilated");
            }
        }
        /// <summary>
        /// Gets a value indicating whether this <see cref="Fleet"/> can enter wormhole.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="Fleet"/> can enter wormhole; otherwise, <c>false</c>.
        /// </value>
        public bool CanEnterWormhole => Sector.System.StarType == StarType.Wormhole;

        /// <summary>
        /// Gets a read-only collection of the ships attached to this <see cref="Fleet"/>.
        /// </summary>
        /// <value>The ships.</value>
        public IIndexedCollection<Ship> Ships => _ships.AsReadOnly();
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="Fleet"/> class.
        /// </summary>
        internal Fleet()
        {
            Initialize();
        }

        private void Initialize()
        {
            _route = TravelRoute.Empty;
            _ships = new CollectionBase<Ship>();
            _activity = UnitActivity.NoActivity;
            UnitAIType = UnitAIType.NoUnitAI;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Fleet"/> class.
        /// </summary>
        /// <param name="objectId">The object ID of the <see cref="Fleet"/>.</param>
        public Fleet(int objectId)
            : base(objectId)
        {
            Initialize();
        }
        #endregion

        #region Methods

        /// <summary>
        /// Called when the location of this <see cref="Fleet"/> changes.
        /// </summary>
        protected override void OnLocationChanged()
        {
            if (GameContext.Current.TurnNumber < 1)
            {
                return;
            }

            foreach (Ship ship in Ships)
            {
                ship.Location = Location;
            }

            base.OnLocationChanged();

            CivilizationManager civManager = GameContext.Current.CivilizationManagers[OwnerID];
            if (civManager != null)
            {
                civManager.MapData.SetExplored(Location, true);
                civManager.MapData.SetScanned(Location, true, SensorRange);
            }

            if (_order != null)
            {
                _order.OnFleetMoved();
            }

            if (Interlocked.CompareExchange(ref _movementSempaphore, 0, 0) == 0)
            {
                return;
            }

            DiplomacyHelper.PerformFirstContacts(Owner, Location);
        }

        internal void AddShipInternal(Ship ship)
        {
            ship.Fleet = this;
            ship.Location = Location;
            if (!_ships.Contains(ship))
            {
                _ships.Add(ship);
            }
            OnPropertyChanged("Name");
            OnPropertyChanged("Range");
            OnPropertyChanged("Speed");
            OnPropertyChanged("CanCloak");
            OnPropertyChanged("CanCamouflage");
            OnPropertyChanged("CanEnterWormhole");
        }

        /// <summary>
        /// Adds a <see cref="Ship"/> to this <see cref="Fleet"/>.
        /// </summary>
        /// <param name="ship">The <see cref="Ship"/> to add.</param>
        internal void AddShip(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException("ship");
            }

            if (AreShipsLocked)
            {
                return;
            }

            Fleet oldFleet = ship.Fleet;
            if ((oldFleet != null) && (oldFleet != this))
            {
                oldFleet.RemoveShip(ship);
            }

            AddShipInternal(ship);
            EnsureValidOrder();
        }

        /// <summary>
        /// Removes a <see cref="Ship"/> from this <see cref="Fleet"/>.
        /// </summary>
        /// <param name="ship">The <see cref="Ship"/> to remove.</param>
        internal void RemoveShip(Ship ship)
        {
            if ((ship == null) || !_ships.Contains(ship))
            {
                return;
            }

            if (AreShipsLocked)
            {
                return;
            }

            _ = _ships.Remove(ship);
            if (ship.Fleet == this)
            {
                ship.Fleet = null;
            }
            if (_ships.Count == 0)
            {
                _ = GameContext.Current.Universe.Destroy(this);
            }
            else
            {
                OnPropertyChanged("Name");
                OnPropertyChanged("Range");
                OnPropertyChanged("Speed");
                OnPropertyChanged("CanCloak");
                OnPropertyChanged("CanCamouflage");
                OnPropertyChanged("CanEnterWormhole");
                EnsureValidOrder();
            }
        }

        /// <summary>
        /// Ensures the order assigned to this <see cref="Fleet"/> is valid.  If the order
        /// is not valid, then the default order is assigned.
        /// </summary>
        private void EnsureValidOrder()
        {
            if ((Order == null) || (!Order.IsValidOrder(this)))
            {
                Order = GetDefaultOrder();
            }
        }

        /// <summary>
        /// Locks the ships.
        /// </summary>
        public void LockShips()
        {
            AreShipsLocked = true;
        }

        /// <summary>
        /// Unlocks the ships.
        /// </summary>
        public void UnlockShips()
        {
            AreShipsLocked = false;
        }

        /// <summary>
        /// Locks the order.
        /// </summary>
        public void LockOrder()
        {
            IsOrderLocked = true;
        }

        /// <summary>
        /// Unlocks the order.
        /// </summary>
        public void UnlockOrder()
        {
            IsOrderLocked = false;
        }

        /// <summary>
        /// Locks the route.
        /// </summary>
        public void LockRoute()
        {
            IsRouteLocked = true;
        }

        /// <summary>
        /// Unlocks the route.
        /// </summary>
        public void UnlockRoute()
        {
            IsRouteLocked = false;
        }

        /// <summary>
        /// Sets the route internally.
        /// </summary>
        /// <param name="route">The route.</param>
        internal void SetRouteInternal(TravelRoute route)
        {
            _route = route;
        }

        /// <summary>
        /// Sets the route.
        /// </summary>
        /// <param name="route">The route.</param>
        public void SetRoute(TravelRoute route)
        {
            //GameLog.Core.Combat.DebugFormat("SetRoute begins...");
            if (IsRouteLocked)
            {
                return;
            }

            if (route == null)
            {
                route = TravelRoute.Empty;
            }

            TravelRoute lastRoute = _route;

            SetRouteInternal(route);
            OnPropertyChanged("Route");
            //GameLog.Core.Combat.DebugFormat("SetRoute changed...");

            if ((lastRoute == route) || (_order == null) || !_order.IsAssigned)
            {
                return;
            }

            //GameLog.Core.Combat.DebugFormat("NEXT: if IsCancelledOnRouteChange...");
            if (_order.IsCancelledOnRouteChange)
            {
                CancelOrder();
            }
            else
                if (_order is AssaultSystemOrder)
            {
                //GameLog.Core.Combat.DebugFormat("if (_order is AssaultSystemOrder)...");
                CancelOrder();
                OnPropertyChanged("Order");
                //GameLog.Core.Combat.DebugFormat("if (_order is AssaultSystemOrder)... order NEW = {0}", _order);
            }
            else
            {
                //GameLog.Core.Combat.DebugFormat("_order.OnFleetRouteChanged();...");
                _order.OnFleetRouteChanged();
                OnPropertyChanged("Order");
            }
        }

        /// <summary>
        /// Moves the <see cref="Fleet"/> along its travel route.
        /// </summary>
        /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
        internal bool MoveAlongRoute()
        {
            TravelRoute route = _route;

            if (IsStranded)
            {
                SetRoute(TravelRoute.Empty);
                return false;
            }

            if (route.IsEmpty)
            {
                return false;
            }

            Sector nextSector = GameContext.Current.Universe.Map[route.Pop()];
            if (nextSector == null)
            {
                return false;
            }

            _ = Interlocked.Increment(ref _movementSempaphore);
            try
            {
                Location = nextSector.Location;
            }
            finally
            {
                _ = Interlocked.Decrement(ref _movementSempaphore);
            }
            return true;
        }

        public void AdjustCrewExperience(int amount)
        {
            //if (amount < 0)
            //    throw new ArgumentOutOfRangeException("amount", "Value must be non-negative.");

            _ = _ships.ForEach(o => o.ExperienceLevel += amount);
        }

        /// <summary>
        /// Sets the order.
        /// </summary>
        /// <param name="order">The order.</param>
        public void SetOrder(FleetOrder order)
        {
            if (IsOrderLocked)
            {
                return;
            }

            if (order == null)
            {
                order = GetDefaultOrder();
                if (order == null)
                {
                    throw new Exception("Could not set default order for fleet");
                }
            }

            FleetOrder lastOrder = _order;
            if (lastOrder == order)
            {
                return;
            }

            if (lastOrder != null)
            {
                lastOrder.OnOrderCancelled();
            }

            _order = order;
            _order.Fleet = this;
            _order.OnOrderAssigned();

            OnPropertyChanged("Order");
        }

        /// <summary>
        /// Cancels the current order.
        /// </summary>
        public void CancelOrder()
        {
            FleetOrder lastOrder = Order;
            if (lastOrder != null)
            {
                lastOrder.OnOrderCancelled();
            }
            SetOrder(GetDefaultOrder());
        }

        /// <summary>
        /// Gets the default order for this <see cref="Fleet"/>.
        /// </summary>
        /// <returns>The default order.</returns>
        protected internal virtual FleetOrder GetDefaultOrder()
        {
            return IsCombatant
                ? FleetOrders.EngageOrder.Create()
                : FleetOrders.AvoidOrder.Create();
        }

        /// <summary>
        /// Resets this <see cref="Fleet"/> at the end of each game turn.
        /// If there are any fields or properties of this <see cref="Fleet"/>
        /// that should be reset or modified at the end of each turn, perform
        /// those operations here.
        /// </summary>
        protected internal override void Reset()
        {
            base.Reset();
            if (_order != null)
            {
                if (!_order.IsValidOrder(this))
                {
                    FleetOrder cancelledOrder = _order;
                    _order.OnOrderCancelled();
                    if (_order == cancelledOrder)
                    {
                        SetOrder(GetDefaultOrder());
                    }
                }

                if (_order.IsComplete)
                {
                    _order.OnOrderCompleted();
                    SetOrder(GetDefaultOrder());
                }
            }
        }

        /// <summary>
        /// Updates the references lost during reserialization.
        /// </summary>
        protected internal override void OnDeserialized()
        {
            base.OnDeserialized();
            if (_order != null)
            {
                _order.UpdateReferences();
            }
        }
        #endregion

        public override void SerializeOwnedData(SerializationWriter writer, object context)
        {
            base.SerializeOwnedData(writer, context);
            writer.Write(_areShipsLocked);
            writer.Write(_isInTow);
            writer.Write(_isOrderLocked);
            writer.Write(_isRouteLocked);
            writer.WriteObject(_order);
            writer.WriteObject(_route);
            writer.WriteOptimized((int)UnitAIType);
            writer.WriteOptimized((int)_activity);
            writer.WriteOptimized(ActivityStart);
        }

        public override void DeserializeOwnedData(SerializationReader reader, object context)
        {
            base.DeserializeOwnedData(reader, context);

            Initialize();

            _areShipsLocked = reader.ReadBoolean();
            _isInTow = reader.ReadBoolean();
            _isOrderLocked = reader.ReadBoolean();
            _isRouteLocked = reader.ReadBoolean();
            _order = reader.Read<FleetOrder>();
            _route = reader.Read<TravelRoute>();
            UnitAIType = (UnitAIType)reader.ReadOptimizedInt32();
            _activity = (UnitActivity)reader.ReadOptimizedInt32();
            ActivityStart = reader.ReadOptimizedInt32();
        }

        public UnitAIType UnitAIType { get; set; }

        public UnitActivity Activity
        {
            get => _activity;
            set
            {
                _activity = value;
                ActivityStart = GameContext.Current.TurnNumber;
            }
        }

        public int ActivityStart { get; private set; }

        public int ActivityDuration => Activity != UnitActivity.NoActivity ? GameContext.Current.TurnNumber - ActivityStart : 0;
    }
}
