// File:GalaxyGenerator.cs
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Collections;
using Supremacy.Data;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Resources;
using Supremacy.Types;
using Supremacy.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Supremacy.Universe
{
    public static class GalaxyGenerator
    {
        public const double MinDistanceBetweenStars = 1.25;
        public const int MinHomeworldDistanceFromInterference = 2;

        private static TableMap UniverseTables;
        private static string _text;
        private static bool bool_output_done;
        private static readonly Dictionary<StarType, int> StarTypeDist;
        private static readonly Dictionary<Tuple<StarType, PlanetSize>, int> StarTypeModToPlanetSizeDist;
        private static readonly Dictionary<Tuple<int, PlanetSize>, int> SlotModToPlanetSizeDist;
        private static readonly Dictionary<Tuple<StarType, PlanetType>, int> StarTypeModToPlanetTypeDist;
        private static readonly Dictionary<Tuple<PlanetSize, PlanetType>, int> PlanetSizeModToPlanetTypeDist;
        private static readonly Dictionary<Tuple<int, PlanetType>, int> SlotModToPlanetTypeDist;
        private static readonly Dictionary<Tuple<PlanetSize, MoonSize>, int> PlanetSizeModToMoonSizeDist;
        private static readonly Dictionary<Tuple<PlanetType, MoonSize>, int> PlanetTypeModToMoonSizeDist;

        static GalaxyGenerator()
        {
            UniverseTables = UniverseManager.Tables;

            StarTypeDist = new Dictionary<StarType, int>();
            StarTypeModToPlanetSizeDist = new Dictionary<Tuple<StarType, PlanetSize>, int>();
            StarTypeModToPlanetTypeDist = new Dictionary<Tuple<StarType, PlanetType>, int>();
            SlotModToPlanetSizeDist = new Dictionary<Tuple<int, PlanetSize>, int>();
            SlotModToPlanetTypeDist = new Dictionary<Tuple<int, PlanetType>, int>();
            PlanetSizeModToPlanetTypeDist = new Dictionary<Tuple<PlanetSize, PlanetType>, int>();
            PlanetSizeModToMoonSizeDist = new Dictionary<Tuple<PlanetSize, MoonSize>, int>();
            PlanetTypeModToMoonSizeDist = new Dictionary<Tuple<PlanetType, MoonSize>, int>();

            _text = "GalaxyGenerator starts...";
            Console.WriteLine(_text);
            GameLog.Core.GalaxyGenerator.DebugFormat(_text);


            foreach (StarType starType in EnumHelper.GetValues<StarType>())
            {
                StarTypeDist[starType] = Number.ParseInt32(UniverseTables["StarTypeDist"][starType.ToString()][0]);
                foreach (PlanetSize planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    StarTypeModToPlanetSizeDist[new Tuple<StarType, PlanetSize>(starType, planetSize)] =
                        Number.ParseInt32(
                            UniverseTables["StarTypeModToPlanetSizeDist"][starType.ToString()][planetSize.ToString()]);
                }
                foreach (PlanetType planetType in EnumHelper.GetValues<PlanetType>())
                {
                    StarTypeModToPlanetTypeDist[new Tuple<StarType, PlanetType>(starType, planetType)] =
                        Number.ParseInt32(
                            UniverseTables["StarTypeModToPlanetTypeDist"][starType.ToString()][planetType.ToString()]);
                }
            }

            for (int i = 0; i < StarSystem.MaxPlanetsPerSystem; i++)
            {
                foreach (PlanetSize planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    SlotModToPlanetSizeDist[new Tuple<int, PlanetSize>(i, planetSize)] =
                        Number.ParseInt32(UniverseTables["SlotModToPlanetSizeDist"][i][planetSize.ToString()]);
                }
                foreach (PlanetType planetType in EnumHelper.GetValues<PlanetType>())
                {
                    SlotModToPlanetTypeDist[new Tuple<int, PlanetType>(i, planetType)] =
                        Number.ParseInt32(UniverseTables["SlotModToPlanetTypeDist"][i][planetType.ToString()]);
                }
            }

            foreach (PlanetSize planetSize in EnumHelper.GetValues<PlanetSize>())
            {
                foreach (PlanetType planetType in EnumHelper.GetValues<PlanetType>())
                {
                    PlanetSizeModToPlanetTypeDist[new Tuple<PlanetSize, PlanetType>(planetSize, planetType)] =
                        Number.ParseInt32(
                            UniverseTables["PlanetSizeModToPlanetTypeDist"][planetSize.ToString()][planetType.ToString()
                                ]);
                }
            }

            foreach (MoonSize moonSize in EnumHelper.GetValues<MoonSize>())
            {
                foreach (PlanetSize planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    PlanetSizeModToMoonSizeDist[new Tuple<PlanetSize, MoonSize>(planetSize, moonSize)] =
                        Number.ParseInt32(
                            UniverseTables["PlanetSizeModToMoonSizeDist"][planetSize.ToString()][moonSize.ToString()]);
                }
                foreach (PlanetType planetType in EnumHelper.GetValues<PlanetType>())
                {
                    PlanetTypeModToMoonSizeDist[new Tuple<PlanetType, MoonSize>(planetType, moonSize)] =
                        Number.ParseInt32(
                            UniverseTables["PlanetTypeModToMoonSizeDist"][planetType.ToString()][moonSize.ToString()]);
                }
            }
        }

        private static CollectionBase<string> GetStarNames()
        {
            FileStream file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Data/StarNames.txt"),
                FileMode.Open,
                FileAccess.Read);

            CollectionBase<string> names = new CollectionBase<string>();

            using (StreamReader reader = new StreamReader(file))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    names.Add(line.Trim());
                }
            }

            var qry = from s in names
                      group s by s into grp
                      select new
                      {
                          num = grp.Key,
                          count = grp.Count()
                      };
            //then...
            foreach (var o in qry)
            {
                if (o.count > 1)
                {
                    Console.WriteLine("###### Star Name {0} is used in StarNames.txt *{1}* times", o.num, o.count);
                    GameLog.Core.GalaxyGenerator.ErrorFormat("###### Star Name {0} is used in StarNames.txt *{1}* times", o.num, o.count);
                }
            }


            return names;
        }

        private static IList<string> GetNebulaNames()
        {
            FileStream file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Data/NebulaNames.txt"),
                FileMode.Open,
                FileAccess.Read);

            List<string> names = new List<string>();

            using (StreamReader reader = new StreamReader(file))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    //GameLog.Core.GalaxyGenerator.DebugFormat("Nebula Name = {0}", line);
                    names.Add(line.Trim());
                }
            }

            var qry = from s in names
                      group s by s into grp
                      select new
                      {
                          num = grp.Key,
                          count = grp.Count()
                      };
            //then...
            foreach (var o in qry)
            {
                if (o.count > 1)
                {
                    Console.WriteLine("###### Nebula Name {0} is used in NebulaNames.txt *{1}* times", o.num, o.count);
                    GameLog.Core.GalaxyGenerator.ErrorFormat("###### Nebula Name {0} is used in NebulaNames.txt *{1}* times", o.num, o.count);
                }
            }

            return names;
        }

        private static int GetMinDistanceBetweenHomeworlds()
        {
            int size = Math.Min(GameContext.Current.Universe.Map.Width, GameContext.Current.Universe.Map.Height);

            // If its an MP game, we want the different Empires to be sufficiently far away from each others
            // TODO Disabled this for now as it turns out that it is still able to fail to place homeworlds.
            // Tried to rework the loop so that either it restarted the placement process or tried to re-place
            // this failing homeworld with a smaller distance requirement. But it keeps failing or crashing.
            /*if (GameContext.Current.IsMultiplayerGame)
            {
                return (int)(0.3f * (float)size);  // min distance would be of 30% of the full map distance, higher than that it starts to be difficult to place all 5 empires
            }*/

            // Ensure empireCount has a positive value to avoid a divide-by-zero error.
            int empireCount = Math.Max(1, GameContext.Current.Civilizations.Count(o => o.IsEmpire));

            // new 2019-09-28: try to avoid crashes at TINY galaxies
            int minDistance = size / empireCount;

            if (GameContext.Current.Options.GalaxyShape == GalaxyShape.Elliptical || GameContext.Current.Options.GalaxyShape == GalaxyShape.Cluster)
            {
                minDistance--;
                if (minDistance < 1)
                {
                    minDistance = 1;
                }
            }
            GameLog.Core.GalaxyGenerator.DebugFormat("GalaxySize = {0}, EmpireCount = {1}, MinDistanceBetweenHomeworlds = {2}", size, empireCount, minDistance);
            return minDistance;
        }

        public static void GenerateGalaxy(GameContext game)
        {
            GameContext.PushThreadContext(game);
            try
            {
                while (true)
                {
                    /* We reload the Universe Tables so that any changes made to the tables
                     * during runtime will be applied without restarting the game.  This
                     * will be useful for tweaking the tables during development.  We can
                     * fall back to using UniverseManager.Tables later on.
                     */
                    UniverseTables = TableMap.ReadFromFile(
                        ResourceManager.GetResourcePath("Resources/Data/UniverseTables.txt"));

                    Table galaxySizes = UniverseTables["GalaxySizes"];

                    Dimension mapSize = new Dimension(
                        Number.ParseInt32(galaxySizes[game.Options.GalaxySize.ToString()]["Width"]),
                        Number.ParseInt32(galaxySizes[game.Options.GalaxySize.ToString()]["Height"]));

                    GameContext.Current.Universe = new UniverseManager(mapSize);

                    List<MapLocation> starPositions = GetStarPositions().ToList();
                    CollectionBase<string> starNames = GetStarNames();

                    starNames.RandomizeInPlace();


                    if (!PlaceHomeworlds(starPositions, starNames, out CollectionBase<MapLocation> homeLocations))
                    {
                        continue;
                    }

                    GenerateSystems(starPositions, starNames, homeLocations);

                    PlaceMoons();

                    LinkWormholes();

                    //Find somewhere to place the Bajoran end of the wormhole
                    MapLocation? bajoranWormholeLocation = null;
                    if (GameContext.Current.Universe.Find<StarSystem>().TryFindFirstItem(s => s.Name == "Bajor", out StarSystem bajoranSystem))
                    {
                        foreach (Sector sector in bajoranSystem.Sector.GetNeighbors())
                        {
                            if ((sector.System == null) && (GameContext.Current.Universe.Map.GetQuadrant(sector) == Quadrant.Alpha))
                            {
                                bajoranWormholeLocation = sector.Location;
                                GameLog.Core.GalaxyGenerator.DebugFormat("Place for Bajoran wormhole found at {0}", sector.Location);
                                break;
                            }
                        }
                    }

                    //Find somewhere to place the Gamma end of the wormhole
                    MapLocation? gammaWormholeLocation = null;
                    MapLocation desiredLocation = new MapLocation(GameContext.Current.Universe.Map.Width / 4, GameContext.Current.Universe.Map.Height / 4);
                    if (GameContext.Current.Universe.Map[desiredLocation].System != null)
                    {
                        foreach (Sector sector in GameContext.Current.Universe.Map[desiredLocation].GetNeighbors())
                        {
                            if (sector.System == null)
                            {
                                gammaWormholeLocation = sector.Location;
                                GameLog.Core.GalaxyGenerator.DebugFormat("Place for Gamma wormhole found at {0}", sector.Location);
                                break;
                            }
                        }
                    }
                    else
                    {
                        gammaWormholeLocation = desiredLocation;
                        GameLog.Core.GalaxyGenerator.DebugFormat("Place for Gamma wormhole found at {0}", desiredLocation);
                    }

                    //We've found somewhere to place the wormholes. Now to actually do it
                    if ((gammaWormholeLocation != null) && (bajoranWormholeLocation != null))
                    {
                        StarSystem bajorWormhole = new StarSystem
                        {
                            StarType = StarType.Wormhole,
                            Name = "Bajoran",
                            WormholeDestination = gammaWormholeLocation,
                            Location = (MapLocation)bajoranWormholeLocation
                        };
                        GameContext.Current.Universe.Objects.Add(bajorWormhole);
                        GameContext.Current.Universe.Map[(MapLocation)bajoranWormholeLocation].System = bajorWormhole;
                        GameLog.Core.GalaxyGenerator.DebugFormat("Bajoran wormhole placed");

                        StarSystem gammaWormhole = new StarSystem
                        {
                            StarType = StarType.Wormhole,
                            Name = "Gamma",
                            WormholeDestination = bajoranWormholeLocation,
                            Location = (MapLocation)gammaWormholeLocation
                        };
                        GameContext.Current.Universe.Objects.Add(gammaWormhole);
                        GameContext.Current.Universe.Map[(MapLocation)gammaWormholeLocation].System = gammaWormhole;
                        GameLog.Core.GalaxyGenerator.DebugFormat("Gamma wormhole placed");
                    }
                    else
                    {
                        GameLog.Core.GalaxyGenerator.DebugFormat("Unable to place Bajoran and/or Gamma wormholes");
                    }

                    int count = 0;
                    bool_output_done = false;
                    for (int x = 0; x < mapSize.Height; x++)
                    {
                        for (int y = 0; y < mapSize.Width; y++)
                        {
                            Sector loc = GameContext.Current.Universe.Map[y, x];
                            if (!loc.Name.Contains("(") && !bool_output_done == true)  // emtpy sector are named e.g. (0,0)
                            {
                                _text = ";MapContent for;" + y + ";" + x + ";" + loc.Name + " - " + loc.System.StarType
                                    + " - no more output or deactivate this line and the boolean"
                                    ;
                                bool_output_done = true;
                                Console.WriteLine(_text);
                                //GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text); // hiding info in Log.txt
                                count += 1;
                            }
                        }
                    }
                    _text = "### MapContent-Count:;" + count;
                    Console.WriteLine(_text);
                    GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text);// hiding info in Log.txt

                    _text = "Searching for Crash: next: systemNamesList";
                    Console.WriteLine(_text);
                    IEnumerable<UniverseObject> systemNamesList = GameContext.Current.Universe.Objects.Where(o => o.ObjectType == UniverseObjectType.StarSystem);

                    var qry = from s in systemNamesList
                              group s by s into grp
                              select new
                              {
                                  num = grp.Key,
                                  count = grp.Count()
                              };
                    //then...
                    //if (qry.curr != null)
                    foreach (var o in qry)
                    {
                        if (o.count > 1)
                        {
                            _text = "######  Star Name " + o.num + " is used in systemNamesList " + o.count + " times - ";
                            Console.WriteLine(_text);
                            GameLog.Core.GalaxyGenerator.ErrorFormat(_text);
                        }
                    }

                    count = 0;
                    bool_output_done = false;
                    foreach (UniverseObject item in systemNamesList)
                    {
                        if (!bool_output_done)
                        {
                        _text = "Systems:;inhabited=" + item.Sector.System.IsInhabited + ";" + item.Location + ";" + item.Name + ";"
                                + " - no more output or deactivate the boolean"
                                ;
                            bool_output_done = true;
                        if (item.Sector.System.Colony != null)
                            _text += ";" + item.Sector.System.Colony.MaxPopulation;
                        Console.WriteLine(_text);
                        //GameLog.Core.GalaxyGenerator.DebugFormat(_text);  // hiding info in Log.txt
                        count += 1;
                        }

                    }
                    _text = "### Systems-Count:;" + count;
                    Console.WriteLine(_text);
                    GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text);

                    break;
                }
            }
            finally
            {

                _ = GameContext.PopThreadContext();
            }
        }

        private static CollectionBase<MapLocation> GetStarPositions()
        {
            IGalaxyLayout layout;

            int width = GameContext.Current.Universe.Map.Width;
            int height = GameContext.Current.Universe.Map.Height;
            int number = width * height;

            switch (GameContext.Current.Options.StarDensity)
            {
                case StarDensity.Sparse:
                    number /= 12;
                    break;
                case StarDensity.Medium:
                    number /= 10;
                    break;
                case StarDensity.Dense:
                default:
                    number /= 8;
                    break;
            }

            switch (GameContext.Current.Options.GalaxyShape)
            {
                case GalaxyShape.Ring:
                    layout = new RingGalaxyLayout();
                    break;
                case GalaxyShape.Cluster:
                    layout = new ClusterGalaxyLayout();
                    break;
                case GalaxyShape.Spiral:
                    layout = new SpiralGalaxyLayout();
                    break;
                case GalaxyShape.Elliptical:
                    layout = new EllipticalGalaxyLayout();
                    break;
                default:
                case GalaxyShape.Irregular:
                    layout = new IrregularGalaxyLayout();
                    break;
            }


            _ = layout.GetStarPositions(out ICollection<MapLocation> positions, number, width, height);

            CollectionBase<MapLocation> result = new CollectionBase<MapLocation>(positions.Count);

            positions.CopyTo(result);

            return result;
        }

        public static StarSystemDescriptor GenerateHomeSystem(Civilization civ)
        {
            StarSystemDescriptor system = new StarSystemDescriptor
            {
                StarType = GetStarType(true),
                Name = civ.HomeSystemName,
                Inhabitants = civ.Race.Key,
                Bonuses = (civ.CivilizationType == CivilizationType.MinorPower)
                                 ? SystemBonus.Duranium
                                 : SystemBonus.Dilithium | SystemBonus.Duranium
            };

            GeneratePlanetsWithHomeworld(system, civ);
            GameLog.Client.GameData.DebugFormat("No HomeSystem defined - HomeSystemsGeneration will be done for {0}", civ.Name);
            return system;
        }

        private static void SetPlanetNames(StarSystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException("system");
            }

            for (int i = 0; i < system.Planets.Count; i++)
            {
                if (string.IsNullOrEmpty(system.Planets[i].Name))
                {
                    system.Planets[i].Name = (system.Planets[i].PlanetType == PlanetType.Asteroids)
                                                 ? "Asteroids"
                                                 : system.Name + " " + RomanNumber.Get(i + 1);
                }
            }
        }

        private static int GetIdealSlot(StarSystemDescriptor system, PlanetDescriptor planet)
        {
            int bestScore = 0;
            int bestSlot = 0;

            for (int iSlot = 0; iSlot <= system.Planets.Count; iSlot++)
            {
                int score = GetPlanetSizeScore(system.StarType.Value, planet.Size.Value, iSlot) +
                            GetPlanetTypeScore(system.StarType.Value, planet.Size.Value, planet.Type.Value, iSlot);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = iSlot;
                }
            }

            return bestSlot;
        }

        private static void FinalizaHomeworldPlacement(IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            Civilization civ,
            MapLocation location)
        {
            CivilizationManager civManager = new CivilizationManager(GameContext.Current, civ);

            GameContext.Current.CivilizationManagers.Add(civManager);

            StarSystemDescriptor homeSystemDescriptor = homeSystemDatabase.ContainsKey(civ.Key)
                                        ? homeSystemDatabase[civ.Key]
                                        : GenerateHomeSystem(civ);

            List<Planet> planets = new List<Planet>();
            Race race = civ.Race;
            StarSystem homeSystem = new StarSystem();

            if (race.Key == "BORG")
            {
                // Breakpoint
            }


            if (!homeSystemDescriptor.IsNameDefined)
            {
                if (starNames.Count > 0)
                {
                    homeSystemDescriptor.Name = starNames[0];
                    starNames.RemoveAt(0);
                }
                else
                {
                    homeSystemDescriptor.Name = civ.ShortName + " Home System";
                }
            }

            homeSystem.Name = homeSystemDescriptor.Name;
            homeSystem.Location = location;

            if (homeSystemDescriptor.IsInhabitantsDefined)
            {
                race = GameContext.Current.Races[homeSystemDescriptor.Inhabitants];
            }

            homeSystem.StarType = homeSystemDescriptor.StarType ?? GetStarType(true);

            if (homeSystemDescriptor.HasBonuses)
            {
                homeSystem.AddBonus(homeSystemDescriptor.Bonuses);
            }

            if (homeSystemDescriptor.Planets.Count == 0)
            {
                GeneratePlanetsWithHomeworld(homeSystemDescriptor, civ);
            }
            else
            {
                GenerateUnspecifiedPlanets(homeSystemDescriptor);
            }

            foreach (PlanetDescriptor planetDescriptor in homeSystemDescriptor.Planets)
            {
                if (planets.Count >= StarHelper.MaxNumberOfPlanets(homeSystem.StarType))
                {
                    break;
                }

                if (!planetDescriptor.IsSinglePlanet)
                {
                    continue;
                }

                Planet planet = new Planet();

                if (planetDescriptor.IsNameDefined)
                {
                    planet.Name = planetDescriptor.Name;
                }

                planet.PlanetSize = PlanetSize.Small;
                planet.PlanetSize = planetDescriptor.Size ?? PlanetSize.Small;

                if (planetDescriptor.Type.HasValue)
                {
                    if (!planetDescriptor.Size.HasValue)
                    {
                        switch (planetDescriptor.Type)
                        {
                            case PlanetType.Asteroids:
                                planet.PlanetSize = PlanetSize.Asteroids;
                                break;
                            case PlanetType.GasGiant:
                                planet.PlanetSize = PlanetSize.GasGiant;
                                break;
                        }
                    }
                    planet.PlanetType = planetDescriptor.Type.Value;
                }

                if (planetDescriptor.HasBonuses)
                {
                    planet.AddBonus(planetDescriptor.Bonuses);
                }

                planet.Variation = RandomHelper.Random(Planet.MaxVariations);
                planets.Add(planet);
            }

            homeSystem.AddPlanets(planets);

            SetPlanetNames(homeSystem);

            homeSystem.Owner = civ;
            GameContext.Current.Universe.Map[homeSystem.Location].System = homeSystem;

            PlaceBonuses(homeSystem);
            CreateHomeColony(civ, homeSystem, race);

            if (civManager.HomeColony == null)
            {
                civManager.HomeColony = homeSystem.Colony;
            }

            civManager.Colonies.Add(homeSystem.Colony);

            GameContext.Current.Universe.Objects.Add(homeSystem);
            GameContext.Current.Universe.Objects.Add(homeSystem.Colony);
        }

        private static bool PlaceEmpireHomeworlds(List<MapLocation> positions,
            IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            List<Civilization> empireCivs,
            CollectionBase<MapLocation> empireHomeLocations,
            List<Civilization> chosenCivs,
            bool mustRespectQuadrants)

        {
            int minHomeDistance = GetMinDistanceBetweenHomeworlds();

            //Go through all of the empires
            for (int index = 0; index < empireCivs.Count; index++)
            {
                int iPosition;
                //If we are respecting quadrants
                if (mustRespectQuadrants)
                {
                    //Ensure that The Dominion is in the top left of the Gamma quadrant
                    if (empireCivs[index].Key == "DOMINION")
                    {
                        //GameLog.Core.GalaxyGenerator.DebugFormat("dom_Location-LIMITS are up to {0} and to {1}",
                        iPosition = GameContext.Current.Options.GalaxyShape == GalaxyShape.Elliptical || GameContext.Current.Options.GalaxyShape == GalaxyShape.Cluster
                            ? positions.FirstIndexWhere((d) => { return d.X <= 3 && d.Y <= 3; })
                            : positions.FirstIndexWhere((l) =>
                            {
                                return (l.X < (GameContext.Current.Universe.Map.Width / 4)) &&
                                (l.Y <= ((GameContext.Current.Universe.Map.Height / 2) - 3));
                            }
                            );
                    }
                    //Ensure that The Borg is in the top right of the Delta quadrant
                    else if (empireCivs[index].Key == "BORG")
                    {
                        if (GameContext.Current.Options.GalaxyShape == GalaxyShape.Elliptical || GameContext.Current.Options.GalaxyShape == GalaxyShape.Cluster)

                        {
                            int borgX = GameContext.Current.Universe.Map.Width - (GameContext.Current.Universe.Map.Width / 8);
                            int borgY = GameContext.Current.Universe.Map.Height / 8;
                            iPosition = positions.FirstIndexWhere((d) => { return d.X >= borgX && d.Y <= borgY; });
                        }
                        else
                        {
                            iPosition = positions.FirstIndexWhere((l) =>
                            {
                                return (l.X > (GameContext.Current.Universe.Map.Width / 4 * 3)) &&
                                (l.Y <= (GameContext.Current.Universe.Map.Height / 2 - 3));
                            });
                        }
                    }
                    //For everybody else just ensure they are in the right quadrant
                    else
                    {
                        iPosition = positions.FirstIndexWhere((l) =>
                        {
                            return GameContext.Current.Universe.Map.GetQuadrant(l) == empireCivs[index].HomeQuadrant;
                        });
                    }
                }
                //If we're not respecting quadrants, shove them anywhere as long as they aren't too close together
                else
                {
                    iPosition = positions.FirstIndexWhere((l) =>
                    {
                        return empireHomeLocations.All(t => MapLocation.GetDistance(l, t) >= minHomeDistance);
                    });
                }

                //If we don't have a valid position
                if (iPosition == -1)
                {
                    GameLog.Core.GalaxyGenerator.WarnFormat("Failed to find a suitable home sector for civilization {0}.  Galaxy generation will start over.",
                        empireCivs[index].Name);
                    empireCivs.RemoveAt(index--);
                    return false;
                }

                //We have a valid position



                empireHomeLocations.Add(positions[iPosition]);
                chosenCivs.Add(empireCivs[index]);
                FinalizaHomeworldPlacement(starNames, homeSystemDatabase, empireCivs[index], positions[iPosition]);
                GameLog.Core.GalaxyGeneratorDetails.DebugFormat("Civilization {0} placed at {1} as {2}", empireCivs[index].Name, positions[iPosition], empireCivs[index].CivilizationType);
                positions.RemoveAt(iPosition);
            }

            return true;
        }

        private static bool PlaceMinorRaceHomeworlds(List<MapLocation> positions,
            IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            List<Civilization> minorRaceCivs,
            CollectionBase<MapLocation> minorHomeLocations,
            List<Civilization> chosenCivs,
            bool mustRespectQuadrants)
        {
            //Firstly, we need to find out how many minor races that we need
            string minorRaceFrequency = GameContext.Current.Options.MinorRaceFrequency.ToString();
            float minorRacePercentage = 0.25f;
            int minorRaceLimit = 9999;

            Table minorRaceTable = GameContext.Current.Tables.UniverseTables["MinorRaceFrequency"];
            if (minorRaceTable != null)
            {
                try
                {
                    double? divisor = (double?)minorRaceTable.GetValue(minorRaceFrequency, "AvailableSystemsDivisor");
                    if (divisor.HasValue)
                    {
                        minorRacePercentage = (float)(1d / divisor.Value);
                    }
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.Core.GalaxyGenerator.Error(e);
                }

                try
                {
                    int? limit = (int?)minorRaceTable.GetValue(minorRaceFrequency, "MaxCount");
                    if (limit.HasValue)
                    {
                        minorRaceLimit = limit.Value;
                    }
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.Core.GalaxyGenerator.Error(e);
                }
            }

            minorRacePercentage = minorRacePercentage <= 0.0f ? 0.0f : Math.Min(1.0f, minorRacePercentage);

            float wantedMinorRaceCount = positions.Count * minorRacePercentage;
            wantedMinorRaceCount = Math.Min(wantedMinorRaceCount, minorRaceLimit);

            //We now know how many minor races we need. Check whether there are enough
            if (wantedMinorRaceCount > minorRaceCivs.Count)
            {
                GameLog.Core.GalaxyGenerator.WarnFormat("No more minor race definitions available.  Galaxy generation will stop.");
                return false;
            }

            //There are enough. Find their homes
            for (int index = 0; index < wantedMinorRaceCount; index++)
            {
                int iPosition;
                //If we are respecting the quadrants
                if (mustRespectQuadrants)
                {
                    //if (minorRaceCivs[index].CivID < 7)
                    //    continue;
                    //Ensure that the Bajorans are in the bottom left of the Alpha quadrant
                    iPosition = minorRaceCivs[index].Key == "BAJORANS"
                        ? positions.FirstIndexWhere((l) =>
                        {
                            return (l.X < (GameContext.Current.Universe.Map.Width / 4)) &&
                                (l.Y > GameContext.Current.Universe.Map.Height / 4 * 3);
                        })
                        : positions.FirstIndexWhere((l) =>
                        {
                            return GameContext.Current.Universe.Map.GetQuadrant(l) == minorRaceCivs[index].HomeQuadrant;
                        });
                }
                //If we're not respecting quadrants, it really doesn't matter
                else
                {
                    iPosition = 0;
                }

                //If we have failed to find a position, error out
                if (iPosition == -1)
                {
                    GameLog.Core.GalaxyGenerator.WarnFormat(
                        "Failed to find a suitable home sector for civilization {0}.  Galaxy generation will stop.",
                        minorRaceCivs[index].Name);
                    return false;
                }

                //We have a valid position
                minorHomeLocations.Add(positions[iPosition]);
                chosenCivs.Add(minorRaceCivs[index]);
                FinalizaHomeworldPlacement(starNames, homeSystemDatabase, minorRaceCivs[index], positions[iPosition]);

                GameLog.Core.GalaxyGeneratorDetails.DebugFormat("Civilization {0} placed at {1} as {2}"
                    , minorRaceCivs[index].Name, positions[iPosition], minorRaceCivs[index].CivilizationType);

                minorRaceCivs.RemoveAt(index);
                positions.RemoveAt(iPosition);
            }

            return true;
        }

        private static bool PlaceHomeworlds(List<MapLocation> positions,
            IList<string> starNames,
            out CollectionBase<MapLocation> homeLocations)
        {
            HomeSystemsDatabase homeSystemDatabase = HomeSystemsDatabase.Load();
            MinorRaceFrequency minorRaceFrequency = GameContext.Current.Options.MinorRaceFrequency;
            List<Civilization> empires = new List<Civilization>();
            List<Civilization> minorRaces = new List<Civilization>();

            foreach (Civilization civ in GameContext.Current.Civilizations)
            {
                if (civ.IsEmpire)
                {
                    empires.Add(civ);
                }
                else if (minorRaceFrequency != MinorRaceFrequency.None)
                {
                    minorRaces.Add(civ);
                }
            }

            //Randomize the places and minor races
            positions.RandomizeInPlace();
            minorRaces.RandomizeInPlace();
            //INFO: If you want to ensure that a race is in the game,
            //move it forward in the randomized minorRaces list

            homeLocations = new CollectionBase<MapLocation>();
            List<Civilization> chosenCivs = new List<Civilization>();

            bool result = PlaceEmpireHomeworlds(positions, starNames, homeSystemDatabase, empires, homeLocations, chosenCivs, GameContext.Current.Options.GalaxyCanon == GalaxyCanon.Canon);
            if (minorRaceFrequency != MinorRaceFrequency.None)
            {
                _ = PlaceMinorRaceHomeworlds(positions, starNames, homeSystemDatabase, minorRaces, homeLocations, chosenCivs, GameContext.Current.Options.GalaxyCanon == GalaxyCanon.Canon);
            }

            HashSet<int> unusedCivs = GameContext.Current.Civilizations.Except(chosenCivs).Select(o => o.CivID).ToHashSet();

            _ = GameContext.Current.Civilizations.RemoveRange(unusedCivs);
            _ = GameContext.Current.CivilizationManagers.RemoveRange(unusedCivs);

            return result;
        }

        private static void PlaceBonuses(StarSystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException("system");
            }

            /*
             * Dilithium and DURANIUM System Bonuses
             */
            if (system.IsInhabited && system.Colony.Owner.CanExpand)
            {
                system.AddBonus(SystemBonus.Dilithium | SystemBonus.Duranium);
            }
            else if (system.HasBonus(SystemBonus.Random))
            {
                if (system.Planets.Any(p => p.PlanetType.IsHabitable()))
                {
                    if (!system.HasDilithiumBonus && RandomHelper.Chance(4))
                    {
                        system.AddBonus(SystemBonus.Dilithium);
                    }

                    if (!system.HasDuraniumBonus && RandomHelper.Chance(3))
                    {
                        system.AddBonus(SystemBonus.Duranium);
                    }
                }
            }

            system.RemoveBonus(SystemBonus.Random);

            int foodPlacementCount = 0;
            int energyPlacementCount = 0;

            foreach (Planet planet in system.Planets)
            {
                if (planet.HasFoodBonus)
                {
                    ++foodPlacementCount;
                }

                if (planet.HasEnergyBonus)
                {
                    ++energyPlacementCount;
                }
            }

            /*
             * Energy and Food Planet Bonus
             */
            foreach (Planet planet in system.Planets)
            {
                if (planet.HasBonus(PlanetBonus.Random))
                {
                    if (!planet.HasEnergyBonus && energyPlacementCount < 2)
                    {
                        if (planet.PlanetType == PlanetType.Volcanic && RandomHelper.Chance(2) ||
                            planet.PlanetType == PlanetType.Desert && RandomHelper.Chance(3))
                        {
                            planet.AddBonus(PlanetBonus.Energy);
                            ++energyPlacementCount;
                        }
                    }

                    if (!planet.HasFoodBonus && foodPlacementCount < 2)
                    {
                        if ((planet.PlanetType == PlanetType.Terran ||
                             planet.PlanetType == PlanetType.Oceanic ||
                             planet.PlanetType == PlanetType.Jungle) && RandomHelper.Chance(3))
                        {
                            planet.AddBonus(PlanetBonus.Food);
                            ++foodPlacementCount;
                        }
                    }

                    planet.RemoveBonus(PlanetBonus.Random);
                }
            }
        }

        private static void GeneratePlanetsWithHomeworld(StarSystemDescriptor system, Civilization civ)
        {
            PlanetDescriptor homePlanet = new PlanetDescriptor();
            PlanetSize planetSize;
            homePlanet.Type = civ.Race.HomePlanetType;
            while (!(planetSize = EnumUtilities.NextEnum<PlanetSize>()).IsHabitable())
            {
                continue;
            }

            if (!system.IsStarTypeDefined) // null star type
            {
                system.StarType = GetStarType(true);
            }

            homePlanet.Size = planetSize;
            homePlanet.Name = system.Name + " Prime";

            GeneratePlanets(system, StarHelper.MaxNumberOfPlanets(system.StarType.Value) - 1);

            system.Planets.Insert(
                GetIdealSlot(system, homePlanet),
                homePlanet);
        }

        private static void GenerateUnspecifiedPlanets(StarSystemDescriptor system)
        {
            GeneratePlanets(system, 0);
        }

        private static int GetDefinedPlanetCount(StarSystemDescriptor system)
        {
            int result = 0;
            foreach (PlanetDescriptor planetDescriptor in system.Planets)
            {
                if (planetDescriptor.IsSinglePlanet)
                {
                    result++;
                }
            }
            return result;
        }

        private static void GeneratePlanets(StarSystemDescriptor system, int maxNewPlanets)
        {
            int initialCount;
            if (!system.IsStarTypeDefined)
            {
                system.StarType = GetStarType(true);
            }
            for (int i = 0; i < system.Planets.Count; i++)
            {
                if (!system.Planets[i].IsSinglePlanet)
                {
                    int attemptNumber = 0;
                    int newPlanets = 0;
                    PlanetDescriptor planetDescriptor = system.Planets[i];

                    initialCount = GetDefinedPlanetCount(system);
                    system.Planets.RemoveAt(i--);

                    while ((newPlanets < planetDescriptor.MinNumberOfPlanets || attemptNumber < planetDescriptor.MaxNumberOfPlanets) &&
                           initialCount + attemptNumber < StarHelper.MaxNumberOfPlanets(system.StarType.Value))
                    {
                        PlanetSize planetSize = GetPlanetSize(system.StarType.Value, initialCount);
                        if (planetSize != PlanetSize.NoWorld)
                        {
                            PlanetDescriptor planet = new PlanetDescriptor
                            {
                                Size = planetSize,
                                Type = GetPlanetType(
                                    system.StarType.Value,
                                    planetSize,
                                    initialCount + attemptNumber)
                            };
                            system.Planets.Insert(++i, planet);
                            newPlanets++;
                        }
                        else
                        {
                            // Asteroids

                            //_text = "PlanetSize = " + planetSize + " at " + system.Name + " Number " + newPlanets;
                            //Console.WriteLine(_text);
                            //GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text);
                        }

                        attemptNumber++;
                    }
                }
            }

            for (int i = 0; i < system.Planets.Count; i++)
            {
                PlanetDescriptor planetDescriptor = system.Planets[i];
                if (planetDescriptor.IsSinglePlanet)
                {
                    if (!planetDescriptor.IsSizeDefined)
                    {
                        while ((planetDescriptor.Size = GetPlanetSize(system.StarType.Value, i)) == PlanetSize.NoWorld)
                        {
                            continue;
                        }
                    }

                    if (!planetDescriptor.IsTypeDefined)
                    {
                        planetDescriptor.Type = GetPlanetType(system.StarType.Value, planetDescriptor.Size.Value, i);
                    }
                }
            }

            initialCount = GetDefinedPlanetCount(system);

            for (int i = 0;
                 (i < maxNewPlanets) &&
                 ((initialCount + i) < StarHelper.MaxNumberOfPlanets(system.StarType.Value));
                 i++)
            {
                PlanetSize planetSize = GetPlanetSize(system.StarType.Value, initialCount + i);
                if (planetSize != PlanetSize.NoWorld)
                {
                    PlanetDescriptor planet = new PlanetDescriptor
                    {
                        Size = planetSize,
                        Type = GetPlanetType(system.StarType.Value, planetSize, initialCount + i)
                    };
                    system.Planets.Add(planet);
                }
            }
        }

        private static void CreateHomeColony(Civilization civ, StarSystem system, Race inhabitants)
        {
            CivilizationManager civManager = GameContext.Current.CivilizationManagers[civ];
            Colony colony = new Colony(system, inhabitants);

            colony.Population.BaseValue = (int)(0.5f * system.GetMaxPopulation(inhabitants));
            colony.Population.Reset();
            colony.Name = system.Name;

            system.Colony = colony;
            colony.Morale.BaseValue = civManager.Civilization.BaseMoraleLevel;

            colony.Morale.Reset();

            civManager.MapData.SetExplored(colony.Location, true);
            civManager.MapData.SetScanned(colony.Location, true, 1);

            GameContext.Current.Universe.HomeColonyLookup[civ] = colony;
        }

        private static void GenerateSystems(
            IEnumerable<MapLocation> positions,
            IList<string> starNames,
            IIndexedCollection<MapLocation> homeLocations)
        {
            int maxPlanets;
            IList<string> nebulaNames = GetNebulaNames();

            switch (GameContext.Current.Options.PlanetDensity)
            {
                case PlanetDensity.Sparse:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem - 4;
                    break;
                case PlanetDensity.Medium:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem - 2;
                    break;
                default:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem;
                    break;
            }

            nebulaNames.RandomizeInPlace();

            GameContext gameContext = GameContext.Current;

            //_ = Parallel.ForEach(
            //    positions,
            //    position =>
            //    {
            //        GameContext.PushThreadContext(gameContext);

            //        try
            //        {

            foreach (MapLocation position in positions)
            {
                GameContext.PushThreadContext(gameContext);

                StarSystem system = new StarSystem();
                List<Planet> planets = new List<Planet>();

                StarType starType;

                do { starType = GetStarType(false); }
                while (!StarHelper.CanPlaceStar(starType, position, homeLocations));

                system.StarType = starType;
                system.Location = position;

                //Set the name
                switch (system.StarType)
                {
                    case StarType.BlackHole:
                        system.Name = "Black Hole";
                        break;
                    case StarType.NeutronStar:
                        system.Name = "Neutron Star";
                        break;
                    case StarType.Quasar:
                        system.Name = "Quasar";
                        break;
                    case StarType.RadioPulsar:
                        system.Name = "Radio Pulsar";
                        break;
                    case StarType.XRayPulsar:
                        system.Name = "X-Ray Pulsar";
                        break;
                    case StarType.Nebula:
                        if (nebulaNames.Count == 0)
                        {
                            break;
                        }

                        system.Name = nebulaNames[0];
                        nebulaNames.RemoveAt(0);
                        break;
                    case StarType.Wormhole:
                        if (system.Quadrant == Quadrant.Delta) // No wormholes near Borg in Delta Quadrant
                        {
                            system.StarType = StarType.BlackHole;
                            system.Name = "Black Hole";
                            GameLog.Core.GalaxyGeneratorDetails.DebugFormat("BlackHole in place of a Wormhole in Delta quadrant at {0}", system.Location);
                            break;
                        }
                        GameLog.Core.GalaxyGeneratorDetails.DebugFormat("Wormhole placed at {0}", system.Location);
                        break;
                    case StarType.White:
                    //break;
                    case StarType.Blue:
                    //break;
                    case StarType.Yellow:
                    //break;
                    case StarType.Orange:
                    //break;
                    case StarType.Red:
                    //break;
                    default:
                        if (starNames.Count == 0)
                        {
                            system.Name = "System " + system.ObjectID;
                            break;
                        }

                        //_text = system.Location + " has type > " + system.StarType;
                        //Console.WriteLine(_text);
                        //GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text);

                        //system.Name = "Dummy";  // not inside Parallel foreach
                        system.Name = starNames[0];
                        starNames.RemoveAt(0);
                        break;
                }

                _text = "Searching for Crash: systemNamesList";
                //Console.WriteLine(_text);
                IEnumerable<UniverseObject> systemNamesList = GameContext.Current.Universe.Objects.Where(o => o.ObjectType == UniverseObjectType.StarSystem);

                //foreach (var pos in positions)
                //{
                if (system.Name == "Dummy")
                {
                    system.Name = starNames.FirstOrDefault();
                    _ = starNames.Remove(system.Name);
                    _text = system.Name + " got used and wiped out from list of Star names";
                    Console.WriteLine(_text);
                    GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text);

                }

                var qry = from s in systemNamesList
                          group s by s into grp
                          select new
                          {
                              num = grp.Key,
                              count = grp.Count()
                          };
                //then...
                foreach (var o in qry)
                {
                    if (o.count > 1)
                    {
                        Console.WriteLine("###### Star Name {0} is used in systemNamesList *{1}* times", o.num, o.count);
                        GameLog.Core.GalaxyGenerator.ErrorFormat("###### Star Name {0} is used in systemNamesList *{1}* times", o.num, o.count);
                    }
                }



                // below "SupportsPlanets" doesn't work atm ... so here manually (to get some sector available to COLONIZE)
                bool _supportPlanets;
                switch (system.StarType)
                {
                    case StarType.White:
                    case StarType.Blue:
                    case StarType.Yellow:
                    case StarType.Orange:
                    case StarType.Red:
                    case StarType.Nebula:
                        _supportPlanets = true;
                        break;
                    default:
                        _supportPlanets = false;
                        break;
                }

                //If the system supports planets, generate them
                //doesn't work atm:   if (starType.SupportsPlanets())
                if (_supportPlanets == true)
                {
                    for (int i = 0; i < maxPlanets - 1; i++)
                    {
                        PlanetSize planetSize = GetPlanetSize(system.StarType, i);
                        if (planetSize != PlanetSize.NoWorld)
                        {
                            Planet planet = new Planet
                            {
                                PlanetSize = planetSize,
                                PlanetType = GetPlanetType(system.StarType, planetSize, i),
                                Variation = RandomHelper.Random(Planet.MaxVariations),
                                Bonuses = PlanetBonus.Random
                            };

                            planets.Add(planet);
                        }
                        if (system.StarType == StarType.Nebula)
                        {
                            break;
                        }
                    }

                    if (planets.Count > 0)
                    {
                        int rndSystemBonusType = RandomHelper.Random(8);
                        switch (rndSystemBonusType)
                        {
                            case 1:
                                system.AddBonus(SystemBonus.Dilithium);
                                break;
                            case 2:
                            case 3:
                            case 4:
                                system.AddBonus(SystemBonus.Duranium);
                                break;
                            case 5:
                                system.AddBonus(SystemBonus.Dilithium);
                                system.AddBonus(SystemBonus.Duranium);
                                break;
                            default:
                                break;
                        }
                    }

                    system.AddPlanets(planets);
                    SetPlanetNames(system);
                    PlaceBonuses(system);
                }

                GameContext.Current.Universe.Objects.Add(system);
                GameContext.Current.Universe.Map[position].System = system;

                //_text = "Searching for Crash: systemNamesList";
                //Console.WriteLine(_text);
                //IEnumerable<UniverseObject> systemNamesList = GameContext.Current.Universe.Objects.Where(o => o.ObjectType == UniverseObjectType.StarSystem);

                //foreach (var pos in positions)
                //{
                if (system.Name == "Dummy")
                {
                    system.Name = starNames.FirstOrDefault();
                    _ = starNames.Remove(system.Name);
                    _text = system.Name + " got used and wiped out from list of Star names";
                    Console.WriteLine(_text);
                    GameLog.Core.GalaxyGeneratorDetails.DebugFormat(_text);

                }

                //var qry = from s in systemNamesList
                //          group s by s into grp
                //          select new
                //          {
                //              num = grp.Key,
                //              count = grp.Count()
                //          };
                ////then...
                //foreach (var o in qry)
                //{
                //    if (o.count > 1)
                //    {
                //        Console.WriteLine("###### Star Name {0} is used in systemNamesList *{1}* times", o.num, o.count);
                //        GameLog.Core.GalaxyGenerator.ErrorFormat("###### Star Name {0} is used in systemNamesList *{1}* times", o.num, o.count);
                //    }
                //}

            }
            //}
            //            finally
            //            {
            _ = GameContext.PopThreadContext();
            //}
            //}
            //);

        }

        private static void LinkWormholes()
        {
            List<StarSystem> wormholes = GameContext.Current.Universe.Find<StarSystem>(s => s.StarType == StarType.Wormhole).ToList();

            foreach (StarSystem wormhole in wormholes)
            {
                //Everything less than Nebula is a proper star

                string notFinalName = GameContext.Current.Universe.FindNearest<StarSystem>(wormhole.Location,
                    s => s.StarType < StarType.Nebula, false).Name;

                IEnumerable<CivilizationManager> emp = GameContext.Current.CivilizationManagers.Where(c => c.CivilizationID < 7);

                foreach (CivilizationManager cID in emp)
                {
                    if (notFinalName == cID.HomeColony.Name)
                    {
                        notFinalName = "Wormhole";
                    }
                }
                wormhole.Name = notFinalName;

                GameLog.Core.GalaxyGeneratorDetails.DebugFormat("Wormhole at {0} named {1}", wormhole.Location, wormhole.Name);
            }

            while (wormholes.Count > 1)
            {
                GameContext.Current.Universe.Map[wormholes[0].Sector.Location].System.WormholeDestination = wormholes[1].Sector.Location;
                GameContext.Current.Universe.Map[wormholes[1].Sector.Location].System.WormholeDestination = wormholes[0].Sector.Location;
                GameLog.Core.GalaxyGeneratorDetails.DebugFormat("Wormholes at {0} and {1} linked", wormholes[0].Sector.Location, wormholes[1].Sector.Location);
                //Call this twice to remove the first 2 wormholes which are now linked
                wormholes.RemoveAt(0);
                wormholes.RemoveAt(0);
            }
        }

        /// <summary>
        /// Returns a random star type
        /// </summary>
        /// <param name="supportsPlanets"></param>
        /// <returns></returns>
        private static StarType GetStarType(bool supportsPlanets)
        {
            StarType result = StarType.White;
            int maxRoll = 0;

            foreach (StarType type in EnumUtilities.GetValues<StarType>().Where(s => s.SupportsPlanets() == supportsPlanets))
            {
                int currentRoll = RandomHelper.Roll(100 + StarTypeDist[type]);
                if (currentRoll > maxRoll)
                {
                    result = type;
                    maxRoll = currentRoll;
                }
            }

            return result;
        }

        private static int GetPlanetSizeScore(StarType starType, PlanetSize planetSize, int slot)
        {
            return RandomHelper.Roll(100)
                   + StarTypeModToPlanetSizeDist[new Tuple<StarType, PlanetSize>(starType, planetSize)]
                   + SlotModToPlanetSizeDist[new Tuple<int, PlanetSize>(slot, planetSize)];
        }

        private static PlanetSize GetPlanetSize(StarType starType, int slot)
        {
            PlanetSize result = PlanetSize.NoWorld;
            int maxRoll = 0;
            foreach (PlanetSize size in EnumUtilities.GetValues<PlanetSize>())
            {
                int currentRoll = GetPlanetSizeScore(starType, size, slot);
                if (currentRoll > maxRoll)
                {
                    result = size;
                    maxRoll = currentRoll;
                }
            }
            return result;
        }

        private static PlanetType GetPlanetType(StarType starType, PlanetSize size, int slot)
        {
            if (size == PlanetSize.Asteroids)
            {
                return PlanetType.Asteroids;
            }

            PlanetType result = PlanetType.Barren;
            int maxRoll = 0;

            foreach (PlanetType type in EnumUtilities.GetValues<PlanetType>())
            {
                int currentRoll = GetPlanetTypeScore(starType, size, type, slot);
                if (currentRoll > maxRoll)
                {
                    result = type;
                    maxRoll = currentRoll;
                }
            }

            return result;
        }

        private static int GetPlanetTypeScore(StarType starType, PlanetSize planetSize, PlanetType planetType, int slot)
        {
            return RandomHelper.Roll(100)
                   + StarTypeModToPlanetTypeDist[new Tuple<StarType, PlanetType>(starType, planetType)]
                   + PlanetSizeModToPlanetTypeDist[new Tuple<PlanetSize, PlanetType>(planetSize, planetType)]
                   + SlotModToPlanetTypeDist[new Tuple<int, PlanetType>(slot, planetType)];
        }

        private static void PlaceMoons()
        {
            List<MoonType> moons = new List<MoonType>(Planet.MaxMoonsPerPlanet);
            foreach (StarSystem system in GameContext.Current.Universe.Find<StarSystem>())
            {
                foreach (Planet planet in system.Planets)
                {
                    int handicap = 0;

                    moons.Clear();

                    if (planet.PlanetType == PlanetType.Asteroids)
                    {
                        continue;
                    }

                    for (int i = 0; i < Planet.MaxMoonsPerPlanet; i++)
                    {
                        int maxRoll = handicap;
                        MoonSize moonSize = MoonSize.NoMoon;

                        foreach (MoonSize moon in EnumUtilities.GetValues<MoonSize>())
                        {
                            int currentRoll = RandomHelper.Roll(100)
                                              + PlanetSizeModToMoonSizeDist[new Tuple<PlanetSize, MoonSize>(planet.PlanetSize, moon)]
                                              + PlanetTypeModToMoonSizeDist[new Tuple<PlanetType, MoonSize>(planet.PlanetType, moon)]
                                              - handicap;

                            if (currentRoll > maxRoll)
                            {
                                moonSize = moon;
                                maxRoll = currentRoll;
                            }
                        }

                        if (moonSize != MoonSize.NoMoon)
                        {
                            moons.Add(moonSize.GetType(EnumUtilities.NextEnum<MoonShape>()));
                        }

                        handicap += maxRoll / Planet.MaxMoonsPerPlanet;
                    }
                    planet.Moons = moons.ToArray();
                }
            }
        }
    }
}