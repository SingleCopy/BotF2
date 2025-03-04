// ColonyAI.cs
//
// Copyright (c) 2008 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Annotations;
using Supremacy.Diplomacy;
using Supremacy.Economy;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Orbitals;
using Supremacy.Tech;
using Supremacy.Universe;
using Supremacy.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Supremacy.AI
{
    public static class ColonyAI
    {
        private const int NumScouts = 1;
        //private const int ColonyShipEveryTurns = 2;
        private const int ColonyShipEveryTurnsMinor = 5;
        private const int MaxMinorColonyCount = 3;
        private const int MaxEmpireColonyCount = 999;
        private static string _text;
        private static int neededColonizer;
        private static bool need1Colonizer;

        public static void DoTurn([NotNull] Civilization civ)
        {
            if (civ == null)
            {
                throw new ArgumentNullException(nameof(civ));
            }

            foreach (Colony colony in GameContext.Current.Universe.FindOwned<Colony>(civ.CivID))
            {
                HandleEnergyProduction(colony);
                HandleFoodProduction(colony);
                HandleBuildings(colony, civ);
                colony.ProcessQueue();
                HandleBuyBuild(colony, civ);
                HandleIndustryProduction(colony);
                HandleLabors(colony);
                if (!PlayerAI.IsInFinancialTrouble(civ))
                {
                    if (civ.IsEmpire)
                    {
                        HandleShipProductionEmpire(colony, civ);
                    }
                    else
                    {
                        HandleShipProductionMinor(colony, civ);
                    }
                }
            }
        }

        private static void SetFacility(Colony colony, ProductionCategory category, int netProd, double output, IEnumerable<ProductionCategory> otherCategories)
        {
            double reserveFacility = Math.Floor(netProd / output);
            reserveFacility = Math.Max(reserveFacility, -(colony.TotalFacilities[category].Value - colony.GetActiveFacilities(category)));
            reserveFacility = Math.Min(reserveFacility, colony.GetActiveFacilities(category));
            int labors = colony.GetAvailableLabor() / colony.GetFacilityType(category).LaborCost;
            while (reserveFacility < 0 && labors > 0)
            {
                _ = colony.ActivateFacility(category);
                reserveFacility++;
                labors--;
            }
            foreach (ProductionCategory c in otherCategories)
            {
                while (reserveFacility < 0 && colony.GetActiveFacilities(c) > 0)
                {
                    _ = colony.DeactivateFacility(c);
                    _ = colony.ActivateFacility(category);
                    reserveFacility++;
                }
            }

            // deactivate not needed
            for (int i = 0; i < reserveFacility; i++)
            {
                _ = colony.DeactivateFacility(category);
            }
        }

        private static void HandleEnergyProduction(Colony colony)
        {
            double energyOutput = colony.GetFacilityType(ProductionCategory.Energy).UnitOutput * (1.0 + colony.GetProductionModifier(ProductionCategory.Energy).Efficiency);
            List<Buildings.Building> offlineBuilding = colony.Buildings.Where(b => !b.IsActive && b.BuildingDesign.EnergyCost > 0).ToList();
            List<ShipyardBuildSlot> offlineShipyardSlots = colony.Shipyard == null ? new List<ShipyardBuildSlot>() : colony.Shipyard.BuildSlots.Where(s => !s.IsActive).ToList();
            int netEnergy = colony.NetEnergy - offlineBuilding.Sum(b => b.BuildingDesign.EnergyCost) - offlineShipyardSlots.Sum(s => s.Shipyard.ShipyardDesign.BuildSlotEnergyCost);
            SetFacility(colony, ProductionCategory.Energy, netEnergy, energyOutput, new[] { ProductionCategory.Intelligence, ProductionCategory.Research, ProductionCategory.Industry, ProductionCategory.Food });

            // turn things on
            foreach (Buildings.Building building in offlineBuilding)
            {
                _ = colony.ActivateBuilding(building);
            }
            foreach (ShipyardBuildSlot slot in offlineShipyardSlots)
            {
                _ = colony.ActivateShipyardBuildSlot(slot);
            }

            ProductionFacilityDesign facilityType = colony.GetFacilityType(ProductionCategory.Energy);
            if ((colony.Buildings.Any(b => !b.IsActive && b.BuildingDesign.EnergyCost > 0) || (colony.Shipyard?.BuildSlots.Any(s => !s.IsActive) == true)) && !colony.IsBuilding(facilityType))
            {
                colony.BuildQueue.Add(new BuildQueueItem(new ProductionFacilityBuildProject(colony, facilityType)));
            }
        }

        private static void HandleFoodProduction(Colony colony)
        {
            double foodOutput = colony.GetFacilityType(ProductionCategory.Food).UnitOutput * (1.0 + colony.GetProductionModifier(ProductionCategory.Food).Efficiency);
            double neededFood = colony.NetFood + colony.FoodReserves.CurrentValue - (10 * foodOutput);
            SetFacility(colony, ProductionCategory.Food, (int)neededFood, foodOutput, new[] { ProductionCategory.Intelligence, ProductionCategory.Research, ProductionCategory.Industry });
            neededFood = colony.Population.CurrentValue + foodOutput;
            double maxFoodProduction = colony.GetProductionModifier(ProductionCategory.Food).Bonus + (colony.GetTotalFacilities(ProductionCategory.Food) * foodOutput);
            ProductionFacilityDesign facilityType = colony.GetFacilityType(ProductionCategory.Food);
            if (maxFoodProduction < neededFood && !colony.IsBuilding(facilityType))
            {
                colony.BuildQueue.Add(new BuildQueueItem(new ProductionFacilityBuildProject(colony, facilityType)));
            }
        }

        private static void HandleIndustryProduction(Colony colony)
        {
            double prodOutput = colony.GetFacilityType(ProductionCategory.Industry).UnitOutput * colony.Morale.CurrentValue / (0.5f * MoraleHelper.MaxValue) * (1.0 + colony.GetProductionModifier(ProductionCategory.Industry).Efficiency);
            int maxProdFacility = Math.Min(colony.TotalFacilities[ProductionCategory.Industry].Value, (colony.GetAvailableLabor() / colony.GetFacilityType(ProductionCategory.Industry).LaborCost) + colony.ActiveFacilities[ProductionCategory.Intelligence].Value + colony.ActiveFacilities[ProductionCategory.Research].Value + colony.ActiveFacilities[ProductionCategory.Industry].Value);
            int industryNeeded = colony.BuildSlots.Where(s => s.Project != null).Select(s => s.Project.IsRushed ? 0 : s.Project.GetCurrentIndustryCost()).Sum();
            int turnsNeeded = industryNeeded == 0 ? 0 : (int)Math.Ceiling(industryNeeded / (colony.GetProductionModifier(ProductionCategory.Industry).Bonus + (maxProdFacility * prodOutput)));
            double facilityNeeded = turnsNeeded == 0 ? 0 : Math.Truncate(((industryNeeded / turnsNeeded) - colony.GetProductionModifier(ProductionCategory.Industry).Bonus) / prodOutput);
            double netIndustry = -(facilityNeeded - colony.ActiveFacilities[ProductionCategory.Industry].Value) * prodOutput;
            SetFacility(colony, ProductionCategory.Industry, (int)netIndustry, prodOutput, new[] { ProductionCategory.Intelligence, ProductionCategory.Research });
        }

        private static void HandleLabors(Colony colony)
        {
            while (colony.ActivateFacility(ProductionCategory.Research)) { }
            while (colony.ActivateFacility(ProductionCategory.Intelligence)) { }
            while (colony.ActivateFacility(ProductionCategory.Food)) { }
            while (colony.ActivateFacility(ProductionCategory.Industry)) { }
        }

        private static void HandleBuildings(Colony colony, Civilization civ)
        {
            if (colony.Shipyard == null)
            {
                BuildProject project = TechTreeHelper.GetBuildProjects(colony).FirstOrDefault(bp => bp.BuildDesign is ShipyardDesign);
                if (colony == GameContext.Current.Universe.HomeColonyLookup[civ] && project != null && !colony.IsBuilding(project.BuildDesign))
                {
                    colony.BuildQueue.Add(new BuildQueueItem(project));
                }
            }

            colony.ProcessQueue();

            if (colony.BuildSlots.All(t => t.Project == null) && colony.BuildQueue.Count == 0)
            {
                double prodOutput = colony.GetFacilityType(ProductionCategory.Industry).UnitOutput
                    * colony.Morale.CurrentValue / (0.5f * MoraleHelper.MaxValue)
                    * (1.0 + colony.GetProductionModifier(ProductionCategory.Industry).Efficiency);
                CivilizationManager manager = GameContext.Current.CivilizationManagers[civ];
                Dictionary<ResourceType, int> availableResources = manager.Colonies
                    .SelectMany(c => c.BuildSlots)
                    .Where(os => os.Project != null)
                    .Select(os => os.Project)
                    .SelectMany(p => EnumHelper.GetValues<ResourceType>().Select(r => new { Resource = r, Cost = p.GetCurrentResourceCost(r) }))
                    .GroupBy(r => r.Resource)
                    .Select(g => new { Resource = g.Key, Used = g.Sum(r => r.Cost) })
                    .ToDictionary(r => r.Resource, r => manager.Resources[r.Resource].CurrentValue - r.Used);

                StructureBuildProject structureProject = TechTreeHelper
                    .GetBuildProjects(colony)
                    .OfType<StructureBuildProject>()
                    .Where(p =>
                            p.GetCurrentIndustryCost() > 0
                            && EnumHelper
                                .GetValues<ResourceType>()
                                .Where(availableResources.ContainsKey)
                                .All(r => availableResources[r] >= p.GetCurrentResourceCost(r)))
                    .OrderBy(p => p.BuildDesign.BuildCost).FirstOrDefault();
                if (structureProject != null && Math.Ceiling(structureProject.GetCurrentIndustryCost() / (colony.GetProductionModifier(ProductionCategory.Industry).Bonus + (colony.TotalFacilities[ProductionCategory.Industry].Value * prodOutput))) <= 5.0)
                {
                    colony.BuildQueue.Add(new BuildQueueItem(structureProject));
                }
            }

            if (colony.BuildSlots.All(t => t.Project == null) && colony.BuildQueue.Count == 0)
            {
                ProductionFacilityUpgradeProject upgradeIndustryProject = TechTreeHelper
                    .GetBuildProjects(colony)
                    .OfType<ProductionFacilityUpgradeProject>()
                    .FirstOrDefault(bp => bp.FacilityDesign == colony.GetFacilityType(ProductionCategory.Industry));
                if (upgradeIndustryProject != null)
                {
                    colony.BuildQueue.Add(new BuildQueueItem(upgradeIndustryProject));
                }
            }

            if (colony.BuildSlots.All(t => t.Project == null) && colony.BuildQueue.Count == 0)
            {
                List<ProductionCategory> flexProduction = new List<ProductionCategory> { ProductionCategory.Industry, ProductionCategory.Research, ProductionCategory.Intelligence };
                int flexLabors = colony.GetAvailableLabor() + flexProduction.Sum(c => colony.GetFacilityType(c).LaborCost * colony.GetActiveFacilities(c));
                if (flexLabors > 0)
                {
                    if (colony.GetTotalFacilities(ProductionCategory.Industry) <= colony.GetTotalFacilities(ProductionCategory.Research) + colony.GetTotalFacilities(ProductionCategory.Intelligence))
                    {
                        colony.BuildQueue.Add(new BuildQueueItem(new ProductionFacilityBuildProject(colony, colony.GetFacilityType(ProductionCategory.Industry))));
                    }
                    else
                    {
                        colony.BuildQueue.Add(new BuildQueueItem(new ProductionFacilityBuildProject(colony, colony.GetFacilityType(ProductionCategory.Research))));
                    }
                }
            }

            if (colony.BuildSlots.All(t => t.Project == null) && colony.BuildQueue.Count == 0)
            {
                IList<BuildProject> projects = TechTreeHelper.GetBuildProjects(colony);
            }
        }

        private static void HandleBuyBuild(Colony colony, Civilization civ)
        {
            CivilizationManager manager = GameContext.Current.CivilizationManagers[civ];
            colony.BuildSlots.Where(s => s.Project?.IsRushed == false).ToList().ForEach(s =>
            {
                List<BuildProject> otherProjects = manager.Colonies
                    .SelectMany(c => c.BuildSlots)
                    .Where(os => os != s && os.Project != null)
                    .Select(os => os.Project)
                    .Where(p => p.GetTimeEstimate() <= 1 || p.IsRushed)
                    .ToList();

                // what does this help? ...costs for the next projects??
                //int cost = otherProjects
                //    .Where(p => p.IsRushed)
                //    .Select(p => p.GetTotalCreditsCost())
                //    .DefaultIfEmpty()
                //    .Sum();

                int cost = s.Project.GetTotalCreditsCost() * 2;  // we take max half of the credits

                //if ((manager.Credits.CurrentValue - (cost * 0.2)) > s.Project.GetTotalCreditsCost())
                if ((manager.Credits.CurrentValue > cost))
                {
                    double prodOutput = colony.GetFacilityType(ProductionCategory.Industry).UnitOutput * colony.Morale.CurrentValue / (0.5f * MoraleHelper.MaxValue) * (1.0 + colony.GetProductionModifier(ProductionCategory.Industry).Efficiency);
                    int maxProdFacility = Math.Min(colony.TotalFacilities[ProductionCategory.Industry].Value, (colony.GetAvailableLabor() / colony.GetFacilityType(ProductionCategory.Industry).LaborCost) + colony.ActiveFacilities[ProductionCategory.Intelligence].Value + colony.ActiveFacilities[ProductionCategory.Research].Value + colony.ActiveFacilities[ProductionCategory.Industry].Value);
                    int industryNeeded = colony.BuildSlots.Where(bs => bs.Project != null).Select(bs => bs.Project.IsRushed ? 0 : bs.Project.GetCurrentIndustryCost()).Sum();
                    int turnsNeeded = industryNeeded == 0 ? 0 : (int)Math.Ceiling(industryNeeded / (colony.GetProductionModifier(ProductionCategory.Industry).Bonus + (maxProdFacility * prodOutput)));
                    
                    
                    if (turnsNeeded > 1 && turnsNeeded < 3)  // we buy when turnsNeede = 2
                    {
                        _text = "HandleBuyBuild: "
                            + "Credits.Current= " + manager.Credits.CurrentValue
                            + ", Costs= " + cost
                            + ", industryNeeded= " + industryNeeded
                            + ", prodOutput= " + prodOutput.ToString()
                            + ", turnsNeeded= " + turnsNeeded
                            + " > IsRushed for " + s.Project 
                            + " on " + colony.Name + " " + s.Project.Location
                        ;
                        Console.WriteLine(_text);

                        s.Project.IsRushed = true;
                        while (colony.DeactivateFacility(ProductionCategory.Industry)) { }
                    }
                }
            });
        }

        //TODO: Move ship production out of colony AI. It requires a greater oversight than just a single colony
        //TODO: Is there any need for separate functions for empires and minor races?
        //TODO: Break these functions up into smaller chunks
        private static void HandleShipProductionEmpire(Colony colony, Civilization civ)
        {
            if (colony.Shipyard == null)
            {
                return;
            }

            //>Check ShipProduction

            IList<BuildProject> potentialProjects = TechTreeHelper.GetShipyardBuildProjects(colony.Shipyard);
            List<ShipDesign> shipDesigns = GameContext.Current.TechTrees[colony.OwnerID].ShipDesigns.ToList();
            List<Fleet> fleets = GameContext.Current.Universe.FindOwned<Fleet>(civ).ToList();
            Sector homeSector = GameContext.Current.CivilizationManagers[civ].SeatOfGovernment.Sector;
            List<Fleet> homeFleets = homeSector.GetOwnedFleets(civ).ToList();


            IList<BuildProject> projects = TechTreeHelper.GetShipyardBuildProjects(colony.Shipyard);
            //foreach (BuildProject project in projects)
            //{
            //    _text = "ShipProduction_2"
            //        + " at " + colony.Location
            //        + " - " + colony.Owner
            //        + ": available= " + project.BuildDesign

            //        ;
            //    Console.WriteLine(_text);
            //}

            if (colony.Sector == homeSector)
            {
                _text = "ShipProduction at " + colony.Location + " " + colony.Name
                    //+ " - Not Habited: Habitation= "
                    //+ item.HasColony
                    //+ " at " + item.Location
                    //+ " - " + item.Owner
                    ;
                Console.WriteLine(_text);

                neededColonizer = 0;

                CheckForColonizerBuildProject(colony);

                if (neededColonizer > 1)
                {
                    neededColonizer -= 1;
                    need1Colonizer = true;
                }


                // Colonization
                //if (GameContext.Current.Universe.FindOwned<Colony>(civ).Count < MaxEmpireColonyCount &&
                //    //GameContext.Current.TurnNumber % ColonyShipEveryTurns == 0 &&
                //    //need1Colonizer &&
                //    !shipDesigns.Where(o => o.ShipType == ShipType.Colony).Any(colony.Shipyard.IsBuilding))
                if (colony.Sector.GetOwnedFleets(civ).All(o => !o.IsColonizer) &&
                    !shipDesigns.Where(o => o.ShipType == ShipType.Colony).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Colony && p.BuildDesign == d));
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                        _text = "ShipProduction "
                            + " at " + colony.Location 
                            + " " + colony.Name 
                            + " - " + colony.Owner
                            + ": Added Colonizer project..." + project.BuildDesign

                            ;
                        Console.WriteLine(_text);
                    }
                }

                // Construction
                if (colony.Sector.Station == null &&
                    colony.Sector.GetOwnedFleets(civ).All(o => !o.IsConstructor) &&
                    !shipDesigns.Where(o => o.ShipType == ShipType.Construction).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Construction && p.BuildDesign == d));
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                        _text = "ShipProduction "
                            + " at " + colony.Location
                            + " - " + colony.Owner
                            + ": Added Construction ship project..." + project.BuildDesign

                            ;
                        Console.WriteLine(_text);
                    }
                }

                // Military
                Fleet defenseFleet = homeSector.GetOwnedFleets(civ).FirstOrDefault(o => o.UnitAIType == UnitAIType.SystemDefense);
                if ((defenseFleet?.HasCommandShip != true) &&
                    homeFleets.All(o => !o.HasCommandShip) &&
                    !shipDesigns.Where(o => o.ShipType == ShipType.Command).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Command && p.BuildDesign == d));
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                    }
                }
                if ((defenseFleet == null || defenseFleet.Ships.Count < 5) &&
                    homeFleets.Where(o => o.IsBattleFleet).Sum(o => o.Ships.Count) < 5 &&
                    !shipDesigns.Where(o => o.ShipType == ShipType.FastAttack || o.ShipType == ShipType.Cruiser).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Cruiser && p.BuildDesign == d));
                    if (project != null)
                    {
                        project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.FastAttack && p.BuildDesign == d));
                    }
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                    }
                }

                // Exploration - HomeSector has Starting Scouts
                if (!shipDesigns.Where(o => o.ShipType == ShipType.Scout).Any(colony.Shipyard.IsBuilding))
                {
                    for (int i = fleets.Count(o => o.IsScout); i < NumScouts; i++)
                    {
                        BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Scout && p.BuildDesign == d));
                        if (project != null)
                        {
                            colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                        }
                    }
                }
            } // end of HomeSector

            // all Colonies - build colony ships
    //        if (GameContext.Current.Universe.FindOwned<Colony>(civ).Count < MaxEmpireColonyCount &&
    //GameContext.Current.TurnNumber % ColonyShipEveryTurns == 0 &&
    //!shipDesigns.Where(o => o.ShipType == ShipType.Colony).Any(colony.Shipyard.IsBuilding))
    //        {
    //            BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Colony && p.BuildDesign == d));
    //            if (project != null)
    //            {
    //                colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
    //            }
    //        }

            if (colony.Sector != homeSector && colony.Shipyard != null)
            {
                _text = "next: check for ShipProduction - not at HomeSector: "
                    + colony.Shipyard.Design
                    + " at " + colony.Location
                    + " - " + colony.Owner
                    ;
                Console.WriteLine(_text);
                CheckForColonizerBuildProject(colony);
            }

            if (colony.Shipyard.BuildSlots.All(t => t.Project == null) && colony.Shipyard.BuildQueue.Count == 0)
            {
                IList<BuildProject> projects2 = TechTreeHelper.GetShipyardBuildProjects(colony.Shipyard);
                //foreach (BuildProject project in projects2)
                //{
                //    _text = "ShipProduction at HomeSector: "
                //        + " at " + colony.Location
                //        + " - " + colony.Owner
                //        + ": available= " + project.BuildDesign

                //        ;
                //    Console.WriteLine(_text);
                //}

                BuildProject newProject = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Colony && p.BuildDesign == d));
                if (newProject != null)
                {
                    colony.Shipyard.BuildQueue.Add(new BuildQueueItem(newProject));
                    _text = "ShipProduction "
                        + " at " + colony.Location
                        + " - " + colony.Owner
                        + ": Added Colonizer project..." + newProject.BuildDesign

                        ;
                    Console.WriteLine(_text);
                }
            }

            foreach (var item in colony.Shipyard.BuildQueue)
            {
                _text = colony.Location
                    + " > " + item.Project.BuildDesign
                    + ", TurnsRemaining= " + item.Project.TurnsRemaining


                    ;
                Console.WriteLine(_text);
            }

            colony.Shipyard.ProcessQueue();
        }

        private static void CheckForColonizerBuildProject(Colony colony)
        {
            // need a fleet for getting a range for IsSectorWithinFuelRange
            Fleet fleet = GameContext.Current.Universe.FindOwned<Fleet>(colony.Owner).Where(f => f.IsColonizer).FirstOrDefault();
            if (fleet == null)
                return;

            _text = "CheckForColonizerBuildProject - using " + fleet.Location + " " + fleet.Ships[0].Design
                    //+ " - Not Habited: Habitation Aim= "
                    //+ item.HasColony
                    //+ " at " + item.Location
                    //+ " - " + item.Owner
                    ;
            Console.WriteLine(_text);

            var possibleSystems = GameContext.Current.Universe.Find<StarSystem>()
                .Where(c => c.Sector != null && c.IsInhabited == false && c.IsHabitable(colony.Owner.Race) == true 
                && FleetHelper.IsSectorWithinFuelRange(c.Sector, fleet) && DiplomacyHelper.IsTravelAllowed(colony.Owner, c.Sector)) /*&& mapData.IsScanned(c.Location)*/
                //&& mapData.IsExplored(c.Location) && FleetHelper.IsSectorWithinFuelRange(c.Sector, fleet)
                //)//Where other science ship is not already going
                //.Where(d => !otherFleets.Any(f => f.Route.Waypoints.LastOrDefault() == d.Location || d.Location == f.Location))
                .ToList();

            neededColonizer = possibleSystems.Count;

            foreach (var item in possibleSystems)
            {
                _text = "ShipProduction at " + colony.Location + " " + colony.Name
                    + " - possible: " + possibleSystems.Count
                    + " - Not Habited: Habitation Aim= "
                    + item.HasColony
                    + " at " + item.Location
                    + " - " + item.Owner
                    ;
                Console.WriteLine(_text);
            }
        }

        private static void HandleShipProductionMinor(Colony colony, Civilization civ)
        {
            if (colony.Shipyard == null)
            {
                return;
            }

            IList<BuildProject> potentialProjects = TechTreeHelper.GetShipyardBuildProjects(colony.Shipyard);
            ShipDesign[] shipDesigns = GameContext.Current.TechTrees[colony.OwnerID].ShipDesigns.ToArray();
            //var fleets = GameContext.Current.Universe.FindOwned<Fleet>(civ);
            Sector homeSector = GameContext.Current.Universe.HomeColonyLookup[civ].Sector;

            if (colony.Sector == homeSector)
            {
                // Exploration
                //if (!shipDesigns.Where(o => o.ShipType == ShipType.Scout).Any(colony.Shipyard.IsBuilding))
                //{
                //    for (int i = fleets.Count(o => o.IsScout); i < NumScouts; i++)
                //    {
                //        var project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Scout && p.BuildDesign == d));
                //        if (project != null)
                //        {
                //            colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                //        }
                //    }
                //}
                // Colonization
                if (civ.CivilizationType == CivilizationType.ExpandingPower &&
                    GameContext.Current.Universe.FindOwned<Colony>(civ).Count < MaxMinorColonyCount &&
                    GameContext.Current.TurnNumber % ColonyShipEveryTurnsMinor == 0 &&
                    !shipDesigns.Where(o => o.ShipType == ShipType.Colony).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Colony && p.BuildDesign == d));
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                    }
                }
                // Construction
                if (civ.CivilizationType == CivilizationType.ExpandingPower &&
                    colony.Sector.Station == null &&
                    colony.Sector.GetOwnedFleets(civ).All(o => !o.IsConstructor) &&
                    !shipDesigns.Where(o => o.ShipType == ShipType.Construction).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Construction && p.BuildDesign == d));
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                    }
                }
                // Military
                Fleet defenseFleet = homeSector.GetOwnedFleets(civ).FirstOrDefault(o => o.UnitAIType == UnitAIType.SystemDefense);
                if (civ.CivilizationType != CivilizationType.MinorPower)
                {
                    if ((defenseFleet == null || defenseFleet.Ships.Count < 2) &&
                        !shipDesigns.Where(o => o.ShipType == ShipType.FastAttack || o.ShipType == ShipType.Cruiser).Any(colony.Shipyard.IsBuilding))
                    {
                        BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.Cruiser && p.BuildDesign == d));
                        if (project != null)
                        {
                            project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.FastAttack && p.BuildDesign == d));
                        }
                        if (project != null)
                        {
                            colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                        }
                    }
                }
                else if ((defenseFleet == null || defenseFleet.Ships.Count < 2) && !shipDesigns.Where(o => o.ShipType == ShipType.FastAttack).Any(colony.Shipyard.IsBuilding))
                {
                    BuildProject project = potentialProjects.LastOrDefault(p => shipDesigns.Any(d => d.ShipType == ShipType.FastAttack && p.BuildDesign == d));
                    if (project != null)
                    {
                        colony.Shipyard.BuildQueue.Add(new BuildQueueItem(project));
                    }
                }
            }

            if (colony.Shipyard.BuildSlots.All(t => t.Project == null) && colony.Shipyard.BuildQueue.Count == 0)
            {
                IList<BuildProject> projects = TechTreeHelper.GetShipyardBuildProjects(colony.Shipyard);
            }

            colony.Shipyard.ProcessQueue();
        }
    }
}