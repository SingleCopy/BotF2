// FleetOrders.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Supremacy.AI;
using Supremacy.Annotations;
using Supremacy.Collections;
using Supremacy.Diplomacy;
using Supremacy.Economy;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.IO;
using Supremacy.Resources;
using Supremacy.Tech;
using Supremacy.Text;
using Supremacy.Types;
using Supremacy.Universe;
using Supremacy.Utility;

namespace Supremacy.Orbitals
{
    [Serializable]
    public static class FleetOrders
    {
        public static readonly EngageOrder EngageOrder;
        public static readonly AvoidOrder AvoidOrder;
        public static readonly ColonizeOrder ColonizeOrder;
        public static readonly InfiltrateOrder InfiltrateOrder;
        public static readonly RaidOrder RaidOrder;
        public static readonly SabotageOrder SabotageOrder;
		public static readonly InfluenceOrder InfluenceOrder;
        public static readonly MedicalOrder MedicalOrder;
        public static readonly TowOrder TowOrder;
        public static readonly WormholeOrder WormholeOrder;
        public static readonly CollectDeuteriumOrder CollectDeuteriumOrder;
        //public static readonly EscortOrder EscortOrder;
        public static readonly BuildStationOrder BuildStationOrder;
        public static readonly ExploreOrder ExploreOrder;
        public static readonly AssaultSystemOrder AssaultSystemOrder;

        private static readonly List<FleetOrder> _orders;

        static FleetOrders()
        {
            EngageOrder = new EngageOrder();
            AvoidOrder = new AvoidOrder();
            ColonizeOrder = new ColonizeOrder();
            InfiltrateOrder = new InfiltrateOrder();
            RaidOrder = new RaidOrder();
            SabotageOrder = new SabotageOrder();
            InfluenceOrder = new InfluenceOrder();
            MedicalOrder = new MedicalOrder();
            TowOrder = new TowOrder();
            WormholeOrder = new WormholeOrder();
            CollectDeuteriumOrder = new CollectDeuteriumOrder();
            //EscortOrder = new EscortOrder();
            BuildStationOrder = new BuildStationOrder();
            ExploreOrder = new ExploreOrder();
            AssaultSystemOrder = new AssaultSystemOrder();

            _orders = new List<FleetOrder>
                      {
                          EngageOrder,
                          AvoidOrder,
                          ColonizeOrder,
                          InfiltrateOrder,
                          RaidOrder,
                          SabotageOrder,
                          InfluenceOrder,
                          MedicalOrder,
                          //TowOrder,
                          WormholeOrder,
                          CollectDeuteriumOrder,
                          //EscortOrder,
                          BuildStationOrder,
                          //ExploreOrder,
                          AssaultSystemOrder,
                      };
        }

        public static ICollection<FleetOrder> GetAvailableOrders(Fleet fleet)
        {
            return _orders.Where(o => o.CanAssignOrder(fleet)).Select(o => o.Create()).ToList();
        }
    }

    #region Engage Order
    [Serializable]
    public sealed class EngageOrder : FleetOrder
    {
        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_ENGAGE"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_ENGAGE"); }
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            return (base.IsValidOrder(fleet) && fleet.IsCombatant);
        }

        public override FleetOrder Create()
        {
            return new EngageOrder();
        }
    }
    #endregion

    #region Assault System Order
    [Serializable]
    public sealed class AssaultSystemOrder : FleetOrder
    {
        public override string OrderName
        {
            get { return LocalizedTextDatabase.Instance.GetString(typeof(AssaultSystemOrder), "Description"); }
        }
        //             get { return ResourceManager.GetString("SYSTEM_ASSAULT_DESCRIPTION"); }
        //             get { return LocalizedTextDatabase.Instance.GetString(typeof(AssaultSystemOrder), "Description"); }

        public override string Status
        {
            get
            {
                var statusFormat = LocalizedTextDatabase.Instance.GetString(typeof(AssaultSystemOrder), "StatusFormat");
                //var statusFormat = ResourceManager.GetString("SYSTEM_ASSAULT_STATUS_FORMAT");
                if (statusFormat == null)
                    return OrderName;

                var fleet = Fleet;
                var sector = (fleet != null) ? fleet.Sector.Name : null;
                
                return string.Format(statusFormat, sector);
            }
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;

            if (!fleet.IsCombatant && !fleet.HasTroopTransports)
                return false;

            var system = GameContext.Current.Universe.Map[fleet.Location].System;
            if (system == null || !system.IsInhabited)
                return false;

            return DiplomacyHelper.AreAtWar(system.Colony.Owner, fleet.Owner);
        }

        public override FleetOrder Create()
        {
            return new AssaultSystemOrder();
        }
    }
    #endregion

    #region Avoid Order

    [Serializable]
    public sealed class AvoidOrder : FleetOrder
    {
        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_AVOID"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_AVOID"); }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public override FleetOrder Create()
        {
            return new AvoidOrder();
        }
    }

    #endregion

    #region ColonizeOrder

    [Serializable]
    public sealed class ColonizeOrder : FleetOrder
    {
        private readonly bool _isComplete;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_COLONIZE"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_COLONIZE"); }
        }

        public override FleetOrder Create()
        {
            return new ColonizeOrder();
        }

        public override bool IsComplete
        {
            get { return _isComplete; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public ColonizeOrder()
        {
            _isComplete = false;
        }

        private Ship FindBestColonyShip()
        {
            Ship bestShip = null;
            foreach (Ship ship in Fleet.Ships)
            {
                if (ship.ShipType == ShipType.Colony)
                {
                    if ((bestShip == null)
                        || (ship.ShipDesign.WorkCapacity > bestShip.ShipDesign.WorkCapacity))
                    {
                        bestShip = ship;
                    }
                }
            }
            return bestShip;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (fleet.Sector.System == null)
                return false;
            if (fleet.Sector.System.HasColony)
                return false;
            //if (fleet.Sector.IsOwned && (fleet.Sector.Owner != fleet.Owner))
            //    return false;
            if (!fleet.Sector.System.IsHabitable(fleet.Owner.Race))
                return false;
            foreach (var ship in fleet.Ships)
            {
                if (ship.ShipType == ShipType.Colony)
                    return true;
            }
            return false;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();
            if (_isComplete)
                return;
            var colonyShip = FindBestColonyShip();
            if (colonyShip == null)
                return;
            CreateColony(
                Fleet.Owner,
                Fleet.Sector.System,
                colonyShip.ShipDesign.WorkCapacity);
            GameContext.Current.Universe.Destroy(colonyShip);
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (!Fleet.Route.IsEmpty)
                Fleet.Route = TravelRoute.Empty;
        }

        private static void CreateColony(Civilization civ, StarSystem system, int population)
        {
            var colony = new Colony(system, civ.Race);
            var civManager = GameContext.Current.CivilizationManagers[civ.Key];

            colony.ObjectID = GameContext.Current.GenerateID();
            colony.Population.BaseValue = population;
            colony.Population.Reset();
            colony.Name = system.Name;
            colony.Owner = civ;

            system.Owner = civ;
            system.Colony = colony;

            GameContext.Current.Universe.Objects.Add(colony);
            civManager.Colonies.Add(colony);
            colony.Morale.BaseValue = civManager.Civilization.BaseMoraleLevel;

            colony.Morale.Reset();

            ColonyBuilder.Build(colony);

            civManager.MapData.SetScanned(colony.Location, true, 1);
            civManager.ApplyMoraleEvent(MoraleEvent.ColonizeSystem, system.Location);
            civManager.SitRepEntries.Add(new NewColonySitRepEntry(civ, colony));
        }
    }

    #endregion

    #region InfiltrateOrder

    [Serializable]
    public sealed class InfiltrateOrder : FleetOrder
    {
        private readonly bool _isComplete;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_INFILTRATE"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_INFILTRATE"); }
        }

        public override FleetOrder Create()
        {
            return new InfiltrateOrder();
        }

        public override bool IsComplete
        {
            get { return _isComplete; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public InfiltrateOrder()
        {
            _isComplete = false;
        }

        private Ship FindBestInfiltrateShip()
        {
            Ship bestShip = null;
            foreach (Ship ship in Fleet.Ships)
            {
                if (ship.ShipType == ShipType.Spy)
                {
                    if ((bestShip == null)
                        || (ship.ShipDesign.WorkCapacity > bestShip.ShipDesign.WorkCapacity))
                    {
                        bestShip = ship;
                    }
                }
            }
            return bestShip;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (fleet.Sector.System == null)
                return false;
            //if (fleet.Sector.System.IsInhabited)
            //    return false;
            if (fleet.Sector.IsOwned && (fleet.Sector.Owner == fleet.Owner))
                return false;
            //if (!fleet.Sector.System.IsHabitable(fleet.Owner.Race))
            //    return false;
            foreach (var ship in fleet.Ships)
            {
                if (ship.ShipType == ShipType.Spy)
                    return true;
            }
            return false;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();
            if (_isComplete)
                return;
            var spyShip = FindBestInfiltrateShip();
            if (spyShip == null)
                return;
            CreateInfiltrate(
                Fleet.Owner,
                Fleet.Sector.System);
            //GameContext.Current.Universe.Destroy(spyShip);
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (!Fleet.Route.IsEmpty)
                Fleet.Route = TravelRoute.Empty;
        }


        //public IAppContext AppContext { get; set; }
        private static void CreateInfiltrate(Civilization civ, StarSystem system)
        {


            var infiltratedCiv = GameContext.Current.CivilizationManagers[system.Owner].Colonies;
            var civManager = GameContext.Current.CivilizationManagers[civ.Key];


            int defenseIntelligence = GameContext.Current.CivilizationManagers[system.Owner].TotalIntelligence + 1;  // TotalIntelligence of attacked civ
            if (defenseIntelligence -1 < 0.1)
                defenseIntelligence = 2;
            //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: defenseIntelligence={0}", defenseIntelligence);

            int attackingIntelligence = GameContext.Current.CivilizationManagers[civ].TotalIntelligence + 1;  // TotalIntelligence of attacked civ
            if (attackingIntelligence -1 < 0.1)
                attackingIntelligence = 1;
            //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: attackingIntelligence={0}", attackingIntelligence);

            int ratio = attackingIntelligence / defenseIntelligence;
                //max ratio for no exceeding gaining points
                if (ratio > 10)
                    ratio = 10;

            GameLog.Print("FleetOrders.cs: owner= {0}, system= {1} is INFILTRATED by civ= {2} (Intelligence: defense={3}, attack={4}, ratio={5})", 
                                                    system.Owner, system.Name, civ.Name, defenseIntelligence, attackingIntelligence, ratio);


            int gainedResearchPointsSum = 0;
            int gainedOfTotalResearchPoints = 0;

            foreach (var infiltrated in infiltratedCiv)
            {
                int gainedResearchPoints = infiltrated.NetResearch;

                if (gainedResearchPoints > 10)
                    gainedResearchPoints = gainedResearchPoints * ratio / 10;

                gainedResearchPointsSum = gainedResearchPointsSum + gainedResearchPoints;
                gainedOfTotalResearchPoints = gainedOfTotalResearchPoints + infiltrated.NetResearch;
                var infiltratedColony = infiltrated;

                GameContext.Current.CivilizationManagers[civ].Research.UpdateResearch(gainedResearchPoints);

            }

            civManager.SitRepEntries.Add(new NewInfiltrateSitRepEntry(civ, system.Colony, gainedResearchPointsSum, gainedOfTotalResearchPoints));

            gainedResearchPointsSum = 0;
            gainedOfTotalResearchPoints = 0;

        }
    }

    #endregion

    #region MedicalOrder
    [Serializable]
    public sealed class MedicalOrder : FleetOrder
    {
        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_MEDICAL"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_MEDICAL"); }
        }

        public override FleetOrder Create()
        {
            return new MedicalOrder();
        }

        public override bool IsCancelledOnMove
        {
            get { return true; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
            {
                return false;
            }
            if (fleet.Sector.System == null)
            {
                return false;
            }
            if (fleet.Sector.System.Colony == null)
            {
                return false;
            }
            if (fleet.Sector.System.Colony.Health.CurrentValue == 100)
            {
                return false;
            }
            return fleet.Ships.Any(s => s.ShipType == ShipType.Medical);
        }

        protected internal override void OnTurnEnding()
        {
            //Medicate the colony
            Fleet.Sector.System.Colony.Health.AdjustCurrent(1 + (Fleet.Ships.Where(s => s.ShipType == ShipType.Medical).Sum(s => s.ShipDesign.PopulationHealth) / 10));

            //If the colony is not ours, increase regard etc
            if (Fleet.Sector.System.Colony.Owner != Fleet.Owner)
            {
                DiplomacyHelper.ApplyTrustChange(Fleet.Sector.System.Owner, Fleet.Owner, 20);
                Diplomat.Get(Fleet.Owner).GetForeignPower(Fleet.Sector.System.Owner).AddRegardEvent(new RegardEvent(10, RegardEventType.HealedPopulation, 200));
                Diplomat.Get(Fleet.Owner).GetForeignPower(Fleet.Sector.System.Owner).UpdateRegardAndTrustMeters();
            }
        }
    }
    #endregion

    #region RaidOrder

    [Serializable]
    public sealed class RaidOrder : FleetOrder
    {
        private readonly bool _isComplete;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_RAID"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_RAID"); }
        }

        public override FleetOrder Create()
        {
            return new RaidOrder();
        }

        public override bool IsComplete
        {
            get { return _isComplete; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public RaidOrder()
        {
            _isComplete = false;
        }

        private Ship FindBestRaidShip()
        {
            Ship bestShip = null;
            foreach (Ship ship in Fleet.Ships)
            {
                if (ship.ShipType == ShipType.Spy)
                {
                    if ((bestShip == null)
                        || (ship.ShipDesign.WorkCapacity > bestShip.ShipDesign.WorkCapacity))
                    {
                        bestShip = ship;
                    }
                }
            }
            return bestShip;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (fleet.Sector.System == null)
                return false;
            //if (fleet.Sector.System.IsInhabited)
            //    return false;
            if (fleet.Sector.IsOwned && (fleet.Sector.Owner == fleet.Owner))
                return false;
            //if (!fleet.Sector.System.IsHabitable(fleet.Owner.Race))
            //    return false;
            foreach (var ship in fleet.Ships)
            {
                if (ship.ShipType == ShipType.Spy)
                    return true;
            }
            return false;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();
            if (_isComplete)
                return;
            var raidShip = FindBestRaidShip();
            if (raidShip == null)
                return;
            CreateRaid(
                Fleet.Owner,
                Fleet.Sector.System);
            //GameContext.Current.Universe.Destroy(raidShip);
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (!Fleet.Route.IsEmpty)
                Fleet.Route = TravelRoute.Empty;
        }


        //public IAppContext AppContext { get; set; }
        private static void CreateRaid(Civilization civ, StarSystem system)
        {
            var raidedCiv = GameContext.Current.CivilizationManagers[system.Owner].Colonies;
            var civManager = GameContext.Current.CivilizationManagers[civ.Key];


        int defenseIntelligence = GameContext.Current.CivilizationManagers[system.Owner].TotalIntelligence + 1;  // TotalIntelligence of attacked civ
            if (defenseIntelligence - 1 < 0.1)
                defenseIntelligence = 2;
            //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: defenseIntelligence={0}", defenseIntelligence);

            int attackingIntelligence = GameContext.Current.CivilizationManagers[civ].TotalIntelligence + 1;  // TotalIntelligence of attacked civ
            if (attackingIntelligence - 1 < 0.1)
                attackingIntelligence = 1;
            //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: attackingIntelligence={0}", attackingIntelligence);

            int ratio = attackingIntelligence / defenseIntelligence;
            //max ratio for no exceeding gaining points
            if (ratio > 10)
                ratio = 10;

            GameLog.Print("FleetOrders.cs: owner= {0}, system= {1} is RAIDED by civ= {2} (Intelligence: defense={3}, attack={4}, ratio={5})",
                                                    system.Owner, system.Name, civ.Name, defenseIntelligence, attackingIntelligence, ratio);


            int gainedCreditsSum = 0;
            int gainedOfTotalCredits = 0;

            foreach (var raided in raidedCiv)
            {
                int gainedCredits = raided.TaxCredits;

                if (gainedCredits > 10)
                    gainedCredits = gainedCredits* ratio / 10;

                gainedCreditsSum = gainedCreditsSum + gainedCredits;
                gainedOfTotalCredits = gainedOfTotalCredits + raided.TaxCredits;
                var raidedColony = raided;


        //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: ------ Beginn of this system");
        //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: gainedCredits={0}, Sum={1}", gainedCredits, gainedCreditsSum);



        GameLog.Print("FleetOrders.cs: Owner= {0}, system= {1} at {2} (raided): TaxCredits={3}, Gained={4}, TotalSum={5} ",
                                                    //Energy ={ 3} out of facilities = { 4 },
                                         system.Owner, raided.Name, raided.Location,
                                                    //raided.GetEnergyUsage(),
                                                    //raided.GetActiveFacilities(ProductionCategory.Energy),
                                                    raided.TaxCredits, gainedCredits, gainedCreditsSum);
                GameLog.Print("FleetOrders.cs: our credits before={0}, their credits ={1}", GameContext.Current.CivilizationManagers[civ].Credits.CurrentValue, GameContext.Current.CivilizationManagers[system.Owner].Credits.CurrentValue);

                //GameContext.Current.CivilizationManagers[civ].Research.UpdateResearch(gainedCredits);
                GameContext.Current.CivilizationManagers[civ].Credits.AdjustCurrent(gainedCredits);
                GameContext.Current.CivilizationManagers[system.Owner].Credits.AdjustCurrent(gainedCredits* -1);

                GameLog.Print("FleetOrders.cs: our credits after={0}, their credits ={1}", GameContext.Current.CivilizationManagers[civ].Credits.CurrentValue, GameContext.Current.CivilizationManagers[system.Owner].Credits.CurrentValue);     

            }
            
            civManager.SitRepEntries.Add(new NewRaidSitRepEntry(civ, system.Colony, gainedCreditsSum, gainedOfTotalCredits));

            gainedCreditsSum = 0;
            gainedOfTotalCredits = 0;

        }
    }
    

    #endregion
    
    #region SabotageOrder

    [Serializable]
    public sealed class SabotageOrder : FleetOrder
    {
        private readonly bool _isComplete;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_SABOTAGE"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_SABOTAGE"); }
        }

        public override FleetOrder Create()
        {
            return new SabotageOrder();
        }

        public override bool IsComplete
        {
            get { return _isComplete; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public SabotageOrder()
        {
            _isComplete = false;
        }

        private Ship FindBestSabotageShip()
        {
            Ship bestShip = null;
            foreach (Ship ship in Fleet.Ships)
            {
                if (ship.ShipType == ShipType.Spy)
                {
                    if ((bestShip == null)
                        || (ship.ShipDesign.WorkCapacity > bestShip.ShipDesign.WorkCapacity))
                    {
                        bestShip = ship;
                    }
                }
            }
            return bestShip;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (fleet.Sector.System == null)
                return false;
            //if (fleet.Sector.System.IsInhabited)
            //    return false;
            if (fleet.Sector.IsOwned && (fleet.Sector.Owner == fleet.Owner))
                return false;
            //if (!fleet.Sector.System.IsHabitable(fleet.Owner.Race))
            //    return false;
            foreach (var ship in fleet.Ships)
            {
                if (ship.ShipType == ShipType.Spy)
                    return true;
            }
            return false;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();
            if (_isComplete)
                return;
            var sabotageShip = FindBestSabotageShip();
            if (sabotageShip == null)
                return;
            CreateSabotage(
                Fleet.Owner,
                Fleet.Sector.System);
            GameContext.Current.Universe.Destroy(sabotageShip);
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (!Fleet.Route.IsEmpty)
                Fleet.Route = TravelRoute.Empty;
        }


        //public IAppContext AppContext { get; set; }
        private static void CreateSabotage(Civilization civ, StarSystem system)
        {
            var sabotagedCiv = GameContext.Current.CivilizationManagers[system.Owner].Colonies;
            var civManager = GameContext.Current.CivilizationManagers[civ.Key];


            int defenseIntelligence = GameContext.Current.CivilizationManagers[system.Owner].TotalIntelligence + 1;  // TotalIntelligence of attacked civ
            if (defenseIntelligence - 1 < 0.1)
                defenseIntelligence = 2;
            //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: defenseIntelligence={0}", defenseIntelligence);

            int attackingIntelligence = GameContext.Current.CivilizationManagers[civ].TotalIntelligence + 1;  // TotalIntelligence of attacked civ
            if (attackingIntelligence - 1 < 0.1)
                attackingIntelligence = 1;
            //GameLog.Client.GameData.DebugFormat("FleetOrders.cs: attackingIntelligence={0}", attackingIntelligence);

            int ratio = attackingIntelligence / defenseIntelligence;
            //max ratio for no exceeding gaining points
            if (ratio > 10)
                ratio = 10;

            GameLog.Print("FleetOrders.cs: owner= {0}, system= {1} is SABOTAGED by civ= {2} (Intelligence: defense={3}, attack={4}, ratio={5})",
                                                    system.Owner, system.Name, civ.Name, defenseIntelligence, attackingIntelligence, ratio);


            GameLog.Print("FleetOrders.cs: Owner= {0}, system= {1} at {2} (sabotaged): Energy={3} out of facilities={4}, in total={5}", //, ResearchProd={5}, Gained={6}, Sum={7} ",
                                                system.Owner, system.Name, system.Location,
                                                system.Colony.GetEnergyUsage(),
                                                system.Colony.GetActiveFacilities(ProductionCategory.Energy),
                                                system.Colony.GetTotalFacilities(ProductionCategory.Energy)
                                                //sabotaged.NetResearch, gainedResearchPoints, gainedResearchPointsSum
                                                );
            GameLog.Print("FleetOrders.cs: {0}: TotalEnergyFacilities before={1}", system.Name, system.Colony.GetTotalFacilities(ProductionCategory.Energy));

            //Effect of sabatoge
            int removeEnergyFacilities = 0;
            if (system.Colony.GetTotalFacilities(ProductionCategory.Energy) > 1 && ratio > 1)// Energy: remaining everything down to 1, for ratio: first value > 1 is 2, so ratio must be 2 or more
            {
                removeEnergyFacilities = 1;
                system.Colony.RemoveFacilities(ProductionCategory.Energy, 1);
            }

            // if ratio > 2 than remove one more  EnergyFacility
            if (system.Colony.GetTotalFacilities(ProductionCategory.Energy) > 2 && ratio > 2)// Energy: remaining everything down to 1, for ratio: first value > 1 is 2, so ratio must be 2 or more
            {
                removeEnergyFacilities = 3;  //  2 and one from before
                system.Colony.RemoveFacilities(ProductionCategory.Energy, 2);
            }

            // if ratio > 3 than remove one more  EnergyFacility
            if (system.Colony.GetTotalFacilities(ProductionCategory.Energy) > 3 && ratio > 3)// Energy: remaining everything down to 1, for ratio: first value > 1 is 2, so ratio must be 2 or more
            {
                removeEnergyFacilities = 6;  //   3 and 3 from before = 6 in total , max 6 should be enough for one sabotage ship
                system.Colony.RemoveFacilities(ProductionCategory.Energy, 3);
            }

            //GameContext.Current.CivilizationManagers[civ].Research.UpdateResearch(gainedResearchPoints);

            GameLog.Client.GameData.DebugFormat("FleetOrders.cs: {0}: TotalEnergyFacilities after={1}", system.Name, system.Colony.GetTotalFacilities(ProductionCategory.Energy));
            civManager.SitRepEntries.Add(new NewSabotageSitRepEntry(civ, system.Colony, removeEnergyFacilities, system.Colony.GetTotalFacilities(ProductionCategory.Energy)));

        }
    }

    #endregion

    #region InfluenceOrder

    [Serializable]
    // Diplomatic mission ... by sending an envoy like Spock, treaties finally are made in DiplomaticScreen
    // positive: ...increasing Regard + Trust
    // negative: ...exit membership from foreign empire
    // positive to your systems, colonies: increasing morale earth first
    public sealed class InfluenceOrder : FleetOrder  
    {
        private readonly bool _isComplete;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_INFLUENCE"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_INFLUENCE"); }
        }

        public override FleetOrder Create()
        {
            return new InfluenceOrder();
        }

        public override bool IsComplete
        {
            get { return _isComplete; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public InfluenceOrder()
        {
            _isComplete = false;
        }

        private Ship FindBestInfluenceShip()
        {
            Ship bestShip = null;
            foreach (Ship ship in Fleet.Ships)
            {
                if (ship.ShipType == ShipType.Diplomatic)  
                {
                    if ((bestShip == null)
                        || (ship.ShipDesign.WorkCapacity > bestShip.ShipDesign.WorkCapacity))
                    {
                        bestShip = ship;
                    }
                }
            }
            return bestShip;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (fleet.Sector.System == null)
                return false;
            //if (fleet.Sector.System.IsInhabited)
            //    return false;
            if (fleet.Sector.IsOwned && (fleet.Sector.Owner == fleet.Owner))  // to improve moral in your systems, colonies 
                return true;
            if (!fleet.Sector.System.IsHabitable(fleet.Owner.Race))
                return false;
            foreach (var ship in fleet.Ships)
            {
                if (ship.ShipType == ShipType.Diplomatic)
                    return true;
            }
            return false;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();
            if (_isComplete)
                return;
            var _influenceShip = FindBestInfluenceShip();
            if (_influenceShip == null)
                return;
            CreateInfluence(
                Fleet.Owner,
                Fleet.Sector.System);
            //GameContext.Current.Universe.Destroy(_influenceShipShip);
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (!Fleet.Route.IsEmpty)
                Fleet.Route = TravelRoute.Empty; // 
        }


        //public IAppContext AppContext { get; set; }
        private static void CreateInfluence(Civilization civ, StarSystem system)
        {
            var InfluencedCiv = GameContext.Current.CivilizationManagers[system.Owner].Colonies;
            var civManager = GameContext.Current.CivilizationManagers[civ.Key];

            //GameLog.Print("FleetOrders.cs: owner= {0}, system= {1} is INFLUENCED by civ= {2}",
            //                                        system.Owner, system.Name, civ.Name, defenseIntelligence, attackingIntelligence, ratio);

            foreach (var Influenced in InfluencedCiv)
            {
                // plan is: 
                // - maxValue for Trust = 1000 .... increasing a little bit quicker than Regard
                // - maxValue for Regard= 1000 .... from Regard treaties are affected (see \Resources\Tables\DiplomacyTables.txt Line 1 RegardLevels

                // part 1: increase morale at own colony  // not above 95 so it's just for bad morale (population in bad mood)
                if (system.Owner == GameContext.Current.CivilizationManagers[civ].Civilization)
                {
                    GameLog.Print("Influence to own colony");
                    if (system.Colony.Morale.CurrentValue < 95)
                    {
                        system.Colony.Morale.AdjustCurrent(+3);
                        GameLog.Print("Our mission increased successfully the morale at our colony {0}", system.Name);
                    }
                    return;
                }

                // part 2: to *independed* minor race
                if (!system.Owner.IsEmpire)   // not an empire
                {
                    GameLog.Print("Trying influence a minor race {2} ({4}) = ({1} at {0} VS {3}", system.Location, system.Owner.Name, system.Name, civ.Name, system.OwnerID);

                    var _foreignPowerStatus = DiplomacyHelper.GetForeignPowerStatus(system.Owner, civ);
                    GameLog.Print("_foreignPowerStatus = {0}", _foreignPowerStatus);
                    DiplomacyHelper.ApplyTrustChange(system.Owner, civ, 288);
                    Diplomat.Get(civ).GetForeignPower(system.Owner).UpdateRegardAndTrustMeters();
                    //DiplomacyHelper.
                    //system.Colony.  // don't know how to access foreignPower.Add(regardEvent)
                }

                GameLog.Print("FleetOrders.cs: Owner= {0}, system= {1} at {2} (influenced): TaxCredits={3} ",
                                                 //Energy ={ 3} out of facilities = { 4 },
                                                 system.Owner, Influenced.Name, Influenced.Location,
                                                            //raided.GetEnergyUsage(),
                                                            //raided.GetActiveFacilities(ProductionCategory.Energy),
                                                            Influenced.TaxCredits);
                GameLog.Print("FleetOrders.cs: our credits before={0}, their credits ={1}", GameContext.Current.CivilizationManagers[civ].Credits.CurrentValue, GameContext.Current.CivilizationManagers[system.Owner].Credits.CurrentValue);

            }

        }
    }

    #endregion

    #region TowOrder

    [Serializable]
    public sealed class TowOrder : FleetOrder
    {
        private int _targetFleetId = GameObjectID.InvalidID;
        private bool _shipsLocked;
        private bool _orderLocked;
        private FleetOrder _lastOrder;

        public override object Target
        {
            get { return TargetFleet; }
            set
            {
                if (value == null)
                    TargetFleet = null;
                if (value is Fleet)
                    TargetFleet = value as Fleet;
                else
                    throw new ArgumentException("Target must be of type Supremacy.Orbitals.Fleet");
                OnPropertyChanged("Target");
            }
        }

        public Fleet TargetFleet
        {
            get { return GameContext.Current.Universe.Objects[_targetFleetId] as Fleet; }
            private set
            {
                var currentTarget = TargetFleet;
                if (currentTarget != null)
                    EndTow();
                if (value == null)
                    _targetFleetId = GameObjectID.InvalidID;
                else
                    _targetFleetId = value.ObjectID;
                OnPropertyChanged("TargetFleet");
            }
        }

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_TOW"); }
        }

        public override string Status
        {
            get
            {
                return String.Format(
                    ResourceManager.GetString("FLEET_ORDER_STATUS_TOW"),
                    TargetFleet);
            }
        }

        public override string DisplayText
        {
            get
            {
                if (!Fleet.Route.IsEmpty)
                {
                    int turns = Fleet.Route.Length / Fleet.Speed;
                    string formatString;
                    if ((Fleet.Route.Length % Fleet.Speed) != 0)
                        turns++;
                    if (turns == 1)
                        formatString = ResourceManager.GetString("ORDER_ETA_TURN_MULTILINE");
                    else
                        formatString = ResourceManager.GetString("ORDER_ETA_TURNS_MULTILINE");
                    return String.Format(formatString, Status, turns);
                }
                return Status;
            }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public override bool IsComplete
        {
            get
            {
                var targetFleet = TargetFleet;
                return (targetFleet != null) && targetFleet.IsInTow && !targetFleet.IsStranded && Fleet.Route.IsEmpty;
            }
        }

        public override FleetOrder Create()
        {
            return new TowOrder();
        }

        public override bool IsTargetRequired(Fleet fleet)
        {
            return true;
        }

        private void BeginTow()
        {
            if (TargetFleet.IsInTow)
                return;

            TargetFleet.IsInTow = true;

            if (!TargetFleet.Order.IsCancelledOnRouteChange)
            {
                _lastOrder = TargetFleet.Order;
                _orderLocked = TargetFleet.IsOrderLocked;
            }

            _shipsLocked = TargetFleet.AreShipsLocked;

            TargetFleet.LockShips();

            if (_orderLocked)
                TargetFleet.UnlockOrder();

            TargetFleet.SetOrder(FleetOrders.AvoidOrder.Create());
            TargetFleet.LockOrder();

            if (TargetFleet.IsRouteLocked)
                TargetFleet.UnlockRoute();

            TargetFleet.SetRoute(TravelRoute.Empty);
            TargetFleet.LockRoute();

            Fleet.LockShips();
        }

        private void EndTow()
        {
            if (!TargetFleet.IsInTow)
                return;

            TargetFleet.UnlockOrder();
            TargetFleet.UnlockRoute();

            if (_lastOrder != null)
                TargetFleet.SetOrder(_lastOrder);
            else
                TargetFleet.SetOrder(TargetFleet.GetDefaultOrder());

            if (_orderLocked)
                TargetFleet.LockOrder();
            if (!_shipsLocked)
                TargetFleet.UnlockShips();

            TargetFleet.IsInTow = false;

            Fleet.UnlockShips();
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (TargetFleet != null)
                BeginTow();
        }

        protected internal override void OnOrderCancelled()
        {
            if (TargetFleet != null)
                EndTow();
            base.OnOrderCancelled();
        }

        protected internal override void OnOrderCompleted()
        {
            if (TargetFleet != null)
                EndTow();
            base.OnOrderCompleted();
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();

            var targetFleet = TargetFleet;
            if ((targetFleet != null) && targetFleet.IsInTow)
                TargetFleet.SetRoute(TravelRoute.Empty);
        }

        protected internal override void OnTurnEnding()
        {
            base.OnTurnEnding();

            var targetFleet = TargetFleet;
            var civManager = GameContext.Current.CivilizationManagers[Fleet.OwnerID];

            if (targetFleet != null)
            {
                var ship = targetFleet.Ships.SingleOrDefault();
                if ((ship != null) && (!FleetHelper.IsFleetInFuelRange(targetFleet)))
                {
                    int fuelNeeded = ship.FuelReserve.Maximum - ship.FuelReserve.CurrentValue;
                    ship.FuelReserve.AdjustCurrent(civManager.Resources[ResourceType.Deuterium].AdjustCurrent(-fuelNeeded));
                }
            }

            if (IsComplete)
                Fleet.SetOrder(Fleet.GetDefaultOrder());
        }

        public override void OnFleetMoved()
        {
            base.OnFleetMoved();
            if (TargetFleet != null)
                TargetFleet.Location = Fleet.Location;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (fleet.Ships.Count != 1)
                return false;
            if (fleet == Fleet && fleet.IsStranded)
                return false;
            return true;
        }

        public override IEnumerable<object> FindTargets(Fleet source)
        {
            var targets = new List<Object>();
            foreach (var targetFleet in GameContext.Current.Universe.FindAt<Fleet>(source.Location))
            {
                if ((targetFleet != source)
                    && (targetFleet.Owner == source.Owner)
                    && (targetFleet.Ships.Count == 1)
                    && targetFleet.IsStranded)
                {
                    targets.Add(targetFleet);
                }
            }
            return targets;
        }
    }

    #endregion

    #region Wormhole Order


    [Serializable]
    public sealed class WormholeOrder : FleetOrder
    {
        private MapLocation _startingLocation;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_ENTER_WORMHOLE"); }
        }

        public override string Status
        {
            get
            {
                return String.Format(
                    ResourceManager.GetString("FLEET_ORDER_ENTER_WORMHOLE"),
                    Fleet);
            }
        }

        public override string DisplayText
        {
            get
            {
                return String.Format(
                    ResourceManager.GetString("ORDER_ENTER_WORMHOLE"),
                    Status);
            }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public override bool IsComplete
        {
            get { return Fleet.Location != _startingLocation; }
        }

        public override FleetOrder Create()
        {
            return new WormholeOrder();
        }

        public override bool IsTargetRequired(Fleet fleet)
        {
            return false;
        }

        protected internal override void OnOrderAssigned()
        {
            base.OnOrderAssigned();
            if (Fleet != null)
                _startingLocation = Fleet.Location;
        }


        protected internal override void OnTurnEnding()
        {

            if (Fleet != null)
            {
                //Wormhole leads nowhere so destroy the fleet
                if (Fleet.Sector.System.WormholeDestination == null)
                {
                    var civManager = GameContext.Current.CivilizationManagers[Fleet.OwnerID];
                    GameLog.Print("Fleet {0} destroyed by wormhole at {1}", Fleet.ObjectID, Fleet.Location);
                    // ToDo: Sitrep:   our connection was lost to ship entering wormhole . The fear is that it was destroyed.
                    Fleet.Destroy();
                }
                else
                {
                    Fleet.Location = (MapLocation)Fleet.Sector.System.WormholeDestination;
                    GameLog.Print("Fleet {0} entered wormhole at {1} and was moved to {2}", Fleet.ObjectID, _startingLocation, Fleet.Location);

                    if (IsComplete)
                        Fleet.SetOrder(Fleet.GetDefaultOrder());
                }
            }
        }

        public override bool IsValidOrder(Fleet fleet)
        {

            if (fleet.Sector.System != null && fleet.Sector.System.StarType == StarType.Wormhole)
                return true;

            return false;
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }
    }
    #endregion

    #region Collect Deuterium Order

    [Serializable]
    public sealed class CollectDeuteriumOrder : FleetOrder
    {
        private int _turnsCollecting;

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_COLLECT_DEUTERIUM"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_COLLECT_DEUTERIUM"); }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public override FleetOrder Create()
        {
            return new CollectDeuteriumOrder();
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (!base.IsValidOrder(fleet))
                return false;
            if (!FleetHelper.IsFleetInFuelRange(fleet))
            {
                bool needsFuel = false;
                foreach (var ship in fleet.Ships)
                {
                    if (ship.FuelReserve.IsMaximized)
                        continue;
                    needsFuel = true;
                    break;
                }
                if (needsFuel)
                {
                    var system = fleet.Sector.System;
                    if (system != null)
                        return ((system.StarType == StarType.Nebula) || system.ContainsPlanetType(PlanetType.GasGiant));
                }
            }
            return false;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();

            if ((++_turnsCollecting % 2) != 0)
                return;

            foreach (var ship in Fleet.Ships)
                ship.FuelReserve.AdjustCurrent(1);
        }
    }

    #endregion

    #region Build Station Order

    [Serializable]
    public sealed class BuildStationOrder : FleetOrder
    {
        private static readonly GameLog s_log = GameLog.GetLog(typeof(BuildStationOrder));

        private bool _finished;
        private StationBuildProject _buildProject;

        public StationDesign StationDesign
        {
            get { return BuildProject.BuildDesign as StationDesign; }
        }

        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_BUILD_STATION"); }
        }

        public override string Status
        {
            get
            {
                return String.Format(
                    ResourceManager.GetString("FLEET_ORDER_STATUS_BUILD_STATION"),
                    ResourceManager.GetString(_buildProject.StationDesign.Name));
            }
        }

        public override string TargetDisplayMember
        {
            get { return "BuildDesign.LocalizedName"; }
        }

        public override object Target
        {
            get { return BuildProject; }
            set { BuildProject = value as StationBuildProject; }
        }

        public StationBuildProject BuildProject
        {
            get
            {
                return _buildProject;
            }
            set
            {
                _buildProject = value;
            }
        }

        public override Percentage? PercentComplete
        {
            get
            {
                if (BuildProject != null)
                    return BuildProject.PercentComplete;
                return null;
            }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override bool IsRouteCancelledOnAssign
        {
            get { return true; }
        }

        public override bool IsCancelledOnMove {
            get { return true; }
        }

        public override bool IsComplete
        {
            get { return (BuildProject != null) && BuildProject.IsCompleted; }
        }

        public override FleetOrder Create()
        {
            return new BuildStationOrder();
        }

        public override IEnumerable<object> FindTargets([NotNull] Fleet source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            var designs = new List<StationDesign>();
            var targets = new List<object>();
            var civManager = GameContext.Current.CivilizationManagers[source.Owner];

            if (civManager == null)
            {
                s_log.General.WarnFormat(
                    "Failed to load CivilizationManager for fleet owner (fleet ID = {0}, owner ID = {1})",
                    source.ObjectID,
                    (source.Owner != null) ? source.Owner.ShortName : source.OwnerID.ToString());
                return targets;
            }

            foreach (var stationDesign in civManager.TechTree.StationDesigns)
            {
                if (TechTreeHelper.MeetsTechLevels(civManager, stationDesign))
                    designs.Add(stationDesign);
            }

            for (int i = 0; i < designs.Count; i++)
            {
                for (int j = 0; j < designs.Count; j++)
                {
                    if (i == j)
                        continue;
                    foreach (var obsoleteDesign in designs[i].ObsoletedDesigns)
                    {
                        if (obsoleteDesign != designs[j])
                            continue;
                        designs.RemoveAt(j);
                        if (i > j)
                            i--;
                        j--;
                    }
                }
            }

            foreach (var design in designs)
                targets.Add(new StationBuildProject(new FleetProductionCenter(source), design));

            return targets;
        }

        public override bool IsValidOrder(Fleet fleet)
        {
            if (fleet.Sector.Station != null)
                return false;
            if (fleet.Sector.IsOwned && (fleet.Sector.Owner != fleet.Owner))
                return false;
            return true;
        }

        public override bool CanAssignOrder(Fleet fleet)
        {
            if (!IsValidOrder(fleet))
                return false;

            // if build order already set, can't assign it again
            if (fleet.Order is BuildStationOrder)
                return false;

            // can't start building if any other ship is already building an outpost
            foreach (var otherFleet in GameContext.Current.Universe.FindAt<Fleet>(fleet.Location))
            {
                if ((otherFleet != fleet) && (otherFleet.Order is BuildStationOrder))
                    return false;
            }

            // needs to be a construction ship
            foreach (var ship in fleet.Ships)
            {
                if (ship.ShipType == ShipType.Construction)
                    return true;
            }

            return false;
        }

        public override bool IsTargetRequired(Fleet fleet)
        {
            return true;
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();

            if (!IsAssigned)
                return;

            var project = _buildProject;
            if ((project == null) || (project.ProductionCenter == null) || project.IsCompleted)
                return;

            var civManager = GameContext.Current.CivilizationManagers[project.Builder];
            if (civManager == null)
            {
                var owner = project.ProductionCenter.Owner;
                s_log.General.WarnFormat(
                    "Failed to load CivilizationManager for build project owner (build project ID = {0}, owner ID = {1})",
                    project.ProductionCenter.ObjectID,
                    (owner != null) ? owner.ShortName : "null");
                return;
            }

            var buildOutput = project.ProductionCenter.GetBuildOutput(0);
            var resources = new ResourceValueCollection();

            //resources[ResourceType.Personnel] = civManager.Personnel[PersonnelCategory.Officers].CurrentValue;
            resources[ResourceType.RawMaterials] = civManager.Resources[ResourceType.RawMaterials].CurrentValue;

            var usedResources = resources.Clone();

            project.Advance(ref buildOutput, usedResources);

            //civManager.Personnel[PersonnelCategory.Officers].AdjustCurrent(
            //    usedResources[ResourceType.Personnel] - resources[ResourceType.Personnel]);
            civManager.Resources[ResourceType.RawMaterials].AdjustCurrent(
                usedResources[ResourceType.RawMaterials] - resources[ResourceType.RawMaterials]);
        }

        protected internal override void OnOrderCompleted()
        {
            base.OnOrderCompleted();

            if (!_finished && (BuildProject != null))
            {
                BuildProject.Finish();
                _finished = true;
            }

            var destroyedShip = Fleet.Ships.FirstOrDefault(o => o.ShipType == ShipType.Construction);
            if (destroyedShip != null)
                GameContext.Current.Universe.Destroy(destroyedShip);
        }

        public override void OnFleetMoved()
        {
            base.OnFleetMoved();
            if (BuildProject != null)
                BuildProject.Cancel();
        }

        #region FleetProductionCenter Class

        internal class FleetProductionCenter : IProductionCenter
        {
            private readonly int _fleetId;
            private readonly BuildSlot _buildSlot;

            // ReSharper disable SuggestBaseTypeForParameter
            public FleetProductionCenter(Fleet fleet)
            {
                if (fleet == null)
                    throw new ArgumentNullException("fleet");
                _fleetId = fleet.ObjectID;
                _buildSlot = new BuildSlot();
            }

            // ReSharper restore SuggestBaseTypeForParameter

            public Fleet Fleet
            {
                get { return GameContext.Current.Universe.Objects[_fleetId] as Fleet; }
            }

            #region IProductionCenter Members

            public IIndexedEnumerable<BuildSlot> BuildSlots
            {
                get { return IndexedEnumerable.Single(_buildSlot); }
            }

            public int GetBuildOutput(int slot)
            {
                return Fleet.Ships.Where(o => o.ShipType == ShipType.Construction).Sum(o => o.ShipDesign.WorkCapacity);
            }

            public IList<BuildQueueItem> BuildQueue
            {
                get { return new ReadOnlyCollection<BuildQueueItem>(new List<BuildQueueItem>()); }
            }

            public void ProcessQueue() { }

            #endregion

            #region IUniverseObject Members

            public GameObjectID ObjectID
            {
                get { return Fleet.ObjectID; }
            }

            public MapLocation Location
            {
                get { return Fleet.Location; }
            }

            public GameObjectID OwnerID
            {
                get { return Fleet.OwnerID; }
            }

            public Civilization Owner
            {
                get { return Fleet.Owner; }
            }

            #endregion
        }

        #endregion
    }

    #endregion

    #region Explore Order

    [Serializable]
    public sealed class ExploreOrder : FleetOrder
    {
        public override string OrderName
        {
            get { return ResourceManager.GetString("FLEET_ORDER_EXPLORE"); }
        }

        public override string Status
        {
            get { return ResourceManager.GetString("FLEET_ORDER_STATUS_EXPLORE"); }
        }

        public override bool WillEngageHostiles
        {
            get { return false; }
        }

        public override bool IsCancelledOnRouteChange
        {
            get { return true; }
        }

        public override FleetOrder Create()
        {
            return new ExploreOrder();
        }

        protected internal override void OnTurnBeginning()
        {
            base.OnTurnBeginning();
            if (!IsAssigned)
                return;
            if (Fleet.Route.IsEmpty)
            {
                Fleet.SetRouteInternal(UnitAI.GetBestExploreRoute(Fleet));
            }
        }
    }

    #endregion

}
