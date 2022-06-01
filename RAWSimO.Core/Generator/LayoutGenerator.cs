using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAWSimO.Core.Configurations;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Randomization;
using RAWSimO.Core.Control;
using RAWSimO.Core.Waypoints;
using System.IO;
using RAWSimO.Core.IO;
using Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RAWSimO.Core.Generator
{
    class LayoutGenerator
    {

        #region member variables

        private SettingConfiguration baseConfiguration;
        private IRandomizer rand;
        private Dictionary<Tuple<double, double>, Elevator> elevatorPositions;
        private Dictionary<Elevator, List<Waypoint>> elevatorWaypoints;
        private Dictionary<Waypoint, QueueSemaphore> elevatorSemaphores;
        private double orientationPodDefault = 0;
        private Instance instance;
        private LayoutConfiguration layoutConfiguration;
        private bool _logInfo = true;
        private Action<string> _logAction;
        private Tile[,] tiles;
        private Tier tier;
        private ZoneConfiguration zoneConfiguration;
        // List of waypoints with orientation used for fixed spawning of robots and pickers
        // The last parameter is the index indicating whether this is a robot or picker spawning point
        private List<Tuple<Waypoint, double, int>> spawnWaypoints;
        /// <summary>
        /// Contains zone configurations depending on number of pickers.
        /// </summary>
        public ZoneInfo zoneInfo { get; internal set; }
        /// <summary>
        /// List of mates and their zones
        /// </summary>
        public List<List<string>> mateBotZones = new List<List<string>>();

        #endregion

        public LayoutGenerator(
            LayoutConfiguration layoutConfiguration,
            IRandomizer rand,
            SettingConfiguration baseConfiguration,
            ControlConfiguration controlConfiguration,
            Action<string> logAction = null)
        {
            _logAction = logAction;
            if (!layoutConfiguration.isValid(out string errorMessage))
            {
                throw new ArgumentException("LayoutConfiguration is not valid. " + errorMessage);
            }

            baseConfiguration.InventoryConfiguration.autogenerate();
            if (!baseConfiguration.InventoryConfiguration.isValid(layoutConfiguration.PodCapacity, out errorMessage))
            {
                throw new ArgumentException("InventoryConfiguration is not valid. " + errorMessage);
            }

            if (!controlConfiguration.IsValid(out errorMessage))
            {
                throw new ArgumentException("ControlConfiguration is not valid. " + errorMessage);
            }

            this.rand = rand;
            this.baseConfiguration = baseConfiguration;
            elevatorPositions = new Dictionary<Tuple<double, double>, Elevator>();
            elevatorWaypoints = new Dictionary<Elevator, List<Waypoint>>();
            elevatorSemaphores = new Dictionary<Waypoint, QueueSemaphore>();
            spawnWaypoints = new List<Tuple<Waypoint, double, int>>();
            instance = Instance.CreateInstance(this.baseConfiguration, controlConfiguration);
            instance.layoutConfiguration = layoutConfiguration;
            instance.Name = layoutConfiguration.NameLayout;
            this.layoutConfiguration = layoutConfiguration;
        }

        public Instance GenerateLayout()
        {
            // Read the csv files with map layout
            instance.MapArray = LoadCsvFile(layoutConfiguration.MapFile).ToList();
            instance.ItemAddressArray = LoadCsvFile(layoutConfiguration.ItemAddressesFile).ToList();
            if (instance.SettingConfig.usingMapSortItems)
                instance.ItemAddressSortOrder = LoadCsvFile(layoutConfiguration.ItemAddressSortOrderFile).ToList();
            if (instance.layoutConfiguration.warehouse.UseZones() && instance.SettingConfig.ZonesEnabled)
                instance.ZoneLocationsOnMap = LoadCsvFile(layoutConfiguration.ZoneFile).ToList();
            else
                LoadZoneLocationsWithZeros();

            // Assign map parameters to instance
            AssignMapParameters();
            // Set zone configuration
            if (instance.SettingConfig.ZonesEnabled)
                SetZoneConfiguration();
            // Init the tier
            AddTiersToCompound();
            // Fill the tiers
            FillTiers();
            // Return the instance
            return instance;
        }
        public void FillTiers()
        {
            tier = instance.Compound.Tiers[0]; //ground level
            tiles = new Tile[instance.MapRowCount, instance.MapColumnCount]; //this keeps track of all the waypoints, their directions and type. This is handy for construction of the layout but also to for example display information in the console during debugging

            if (_logInfo)
            {
                //WriteAllDirectionsInfo(tiles);
                //WriteAllTypesInfo(tiles);
            }

            CreateMapFromFile();
            GeneratePods(tier);
            ConnectAllWayPoints(tiles);
            if (baseConfiguration.BotLocations == BotLocations.Fixed) 
                AddSpawnWaypointsFromFile();
            GenerateRobots(tier, tiles);
            instance.Flush();
            LocateResourceFiles();
        }
        /// <summary>
        /// Setting zone configurations by: 
        /// - loading zone config file
        /// - check if zone config, MateBotZone from csv file and MateBotCount matches 
        /// - create list of mates and their zones from zone_config file
        /// </summary>
        private void SetZoneConfiguration()
        {
            zoneInfo = LoadJsonFile(layoutConfiguration.ZoneConfiguration);
            // Current zone configuration
            zoneConfiguration = zoneInfo.zoneConfigurations.FirstOrDefault(z => z.numberOfMates == layoutConfiguration.MateBotCount);
            // Check if zone info matches zone file
            CheckIfZoneInfoMatchesZoneFile();

            CreateListOfMatesAndTheirZones();
        }
        /// <summary>
        /// Create list of mates and their zones from map_zone_config file
        /// </summary>
        private void CreateListOfMatesAndTheirZones()
        {
            for (int i = 0; i < zoneConfiguration.numberOfMates; i++)
            {
                List<string> line = new List<string>();
                for (int j = 0; j < zoneConfiguration.zones.Count; j++)
                {
                    if (zoneConfiguration.zones[j].mates.Contains(i + 1) && !line.Contains((j + 1).ToString()))
                        line.Add(zoneConfiguration.zones[j].zone.ToString());
                }
                mateBotZones.Add(line);
            }
        }
        /// <summary>
        /// Check if zone config, MateBotZone from csv file and MateBotCount matches 
        /// </summary>
        private void CheckIfZoneInfoMatchesZoneFile()
        {
            List<string> zones = new List<string>();
            //get unique/distinct zones from zone file
            for (int row = 0; row < instance.MapRowCount; row++)
            {
                for (int col = 0; col < instance.MapColumnCount; col++)
                {
                    if (!zones.Contains(instance.ZoneLocationsOnMap[row][col]))
                        if (instance.ZoneLocationsOnMap[row][col] != "0")
                            zones.Add(instance.ZoneLocationsOnMap[row][col]);
                }
            }

            if (zoneConfiguration == null)
                throw new Exception("Zone configuration for " + layoutConfiguration.MateBotCount.ToString() + " mateBots doesn't exists!");
            if (zoneConfiguration.zones.Count != zones.Count)
                throw new Exception("Zone file doesn't match zone configurations. Number of zones in config file doesn't match with csv file!");
        }

        private ZoneInfo LoadJsonFile(string path)
        {
            //null check
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Invalid map file path: " + path, path);
            //extension check
            if (Path.GetExtension(path) != ".json")
                throw new ArgumentException("file " + Path.GetFileName(path) + " is not of right type", path);
            path = IOHelper.FindResourceFile(path, Directory.GetCurrentDirectory());

            var zoneInfo = JsonConvert.DeserializeObject<ZoneInfo>(File.ReadAllText(@path));
            
            return zoneInfo;
        }

       private void Write(string msg)
        {
            _logAction?.Invoke(msg);
            Console.Write(msg);
        }
        public void AddTiersToCompound()
        {
            for (int whichTier = 0; whichTier < layoutConfiguration.TierCount; whichTier++)
            {
                double relativePositionX = 0;
                double relativePositionY = 0;
                double relativePositionZ = whichTier * layoutConfiguration.TierHeight;
                instance.CreateTier(instance.RegisterTierID(), instance.MapHorizontalLength, instance.MapVerticalLength, relativePositionX, relativePositionY, relativePositionZ);
            }
        }

        public void ConnectAllWayPoints(Tile[,] tiles)
        {
            for (int row = 0; row < tiles.GetLength(0); row++)
            {
                for (int column = 0; column < tiles.GetLength(1); column++)
                {
                    if (tiles[row, column] != null)
                    {
                        bool addWest = false;
                        bool addEast = false;
                        bool addNorth = false;
                        bool addSouth = false;
                        switch (tiles[row, column].direction)
                        {
                            case directions.EastNorthSouthWest: addEast = true; addNorth = true; addSouth = true; addWest = true; break;
                            case directions.NorthSouthWest: addNorth = true; addSouth = true; addWest = true; break;
                            case directions.EastNorthSouth: addEast = true; addNorth = true; addSouth = true; break;
                            case directions.EastNorthWest: addEast = true; addNorth = true; addWest = true; break;
                            case directions.EastSouthWest: addEast = true; addSouth = true; addWest = true; break;
                            case directions.NorthSouth: addNorth = true; addSouth = true; break;
                            case directions.NorthWest: addNorth = true; addWest = true; break;
                            case directions.EastNorth: addEast = true; addNorth = true; break;
                            case directions.SouthWest: addSouth = true; addWest = true; break;
                            case directions.EastSouth: addEast = true; addSouth = true; break;
                            case directions.EastWest: addEast = true; addWest = true; break;
                            case directions.East: addEast = true; break;
                            case directions.West: addWest = true; break;
                            case directions.South: addSouth = true; break;
                            case directions.North: addNorth = true; break;
                            case directions.Invalid: throw new ArgumentException("invalid direction encountered");
                            default: break;
                        }
                        Waypoint current = tiles[row, column].wp;
                        if (addWest)
                        {
                            Waypoint west = tiles[row, column - 1].wp;
                            current.AddPath(west);
                        }
                        if (addEast)
                        {
                            Waypoint east = tiles[row, column + 1].wp;
                            current.AddPath(east);
                        }
                        if (addNorth)
                        {
                            Waypoint north = tiles[row + 1, column].wp;
                            current.AddPath(north);
                        }
                        if (addSouth)
                        {
                            Waypoint south = tiles[row - 1, column].wp;
                            current.AddPath(south);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Load CSV file 
        /// </summary>
        public LinkedList<List<string>> LoadCsvFile(string path)
        {
            //null check
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Invalid map file path: " + path, path);
            //extension check
            if (Path.GetExtension(path) != ".csv")
                throw new ArgumentException("file " + Path.GetFileName(path) + " is not of right type", path);
            path = IOHelper.FindResourceFile(path, Directory.GetCurrentDirectory());

            //read the csv file backwards for simpler indexing
            using (var reader = new StreamReader(path))
            {
                LinkedList<List<string>> tempList = new LinkedList<List<string>>();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine().Split(',').ToList();
                    tempList.AddFirst(line);
                }
                return tempList;
            }
        }
        /// <summary>
        /// Load zone locations on map if zones are disabled 
        /// </summary>
        public void LoadZoneLocationsWithZeros()
        {
            instance.ZoneLocationsOnMap = new List<List<string>>();
            for(int i = 0; i < instance.MapArray.Count; ++i)
            {
                List<string> l = new List<string>();
                for(int j = 0; j < instance.MapArray[i].Count; ++j)
                {
                    l.Add("0");
                }
            instance.ZoneLocationsOnMap.Add(l);
            }
        }

        /// <summary>
        /// Assigns the map layout parameters to Instance
        /// </summary>
        public void AssignMapParameters()
        {
            instance.MapRowCount = instance.MapArray.Count();
            instance.MapColumnCount = instance.MapArray[0].Count();
            instance.MapVerticalLength = layoutConfiguration.VerticalWaypointDistance * instance.MapRowCount;
            instance.MapHorizontalLength = layoutConfiguration.HorizontalWaypointDistance * instance.MapColumnCount;
            instance.PodCount = (from sublist in instance.MapArray
                                 from str in sublist
                                 where str.Equals("0")
                                 select str).Count();
            instance.NInputPalletStands = (from sublist in instance.MapArray
                                           from str in sublist
                                           where str.Contains("I")
                                           select str).Count();
            instance.NOutputPalletStands = (from sublist in instance.MapArray
                                            from str in sublist
                                            where str.Contains("O")
                                            select str).Count();
            if (instance.NInputPalletStands > 0)
                instance.InputQueueSize = (from sublist in instance.MapArray
                                           from str in sublist
                                           where str.Contains("i")
                                           select int.Parse(str.Split('-').Last())).Max();
            if (instance.NOutputPalletStands > 0)
                instance.OutputQueueSize = (from sublist in instance.MapArray
                                        from str in sublist
                                        where str.Contains("o")
                                        select int.Parse(str.Split('-').Last())).Max();
        }

        /// <summary>
        /// For each row,col pair create an integer value representing priority.
        /// This priority is then used to sort addresses within orders.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        public void UpdateOrderDictionary(int row, int col)
        {
            string adr = instance.ItemAddressArray[row][col];
            string sortStr = instance.ItemAddressSortOrder[row][col];
            string[] sortParts = sortStr.Split('-');
            int sortOrder = int.Parse(sortParts[0]) * 10000 + int.Parse(sortParts[1]) * 100 + int.Parse(sortParts[2]);
            instance.addressToSortOrder.Add(adr, sortOrder);
        }

        public void AddSpawnWaypointsFromFile()
        {
            // row, col, orientation, index (robot or picker)
            if (string.IsNullOrWhiteSpace(baseConfiguration.LocationsFile)) throw new ArgumentException("The LocationsFile for BotLocations.Fixed was not provided");
            baseConfiguration.LocationsFile = IOHelper.FindResourceFile(baseConfiguration.LocationsFile, Directory.GetCurrentDirectory());
            StreamReader sr = new StreamReader(baseConfiguration.LocationsFile);

            int index = 0;

            var orientations = new Dictionary<char, double>()
            {
                {'N',1.57},{'S',4.71},{'E',0.0},{'W',3.14}
            };

            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                if (line[0] == '-')
                {
                    // next will be the picker spawn locations
                    index = 1;
                    continue;
                }
                List<string> items = line.Split(',').ToList();
                int row = int.Parse(items[0]);
                int col = int.Parse(items[1]);
                double direction = orientations[items[2][0]];
                Tuple<Waypoint, double, int> wpDirInd = new Tuple<Waypoint, double, int>(tiles[row,col].wp, direction, index);
                spawnWaypoints.Add(wpDirInd);
            }
        }

        /// <summary>
        /// Create map layout from the previously parsed CSV file
        /// </summary>
        public void CreateMapFromFile()
        {
            var mapArray = instance.MapArray;

            // initialize I/O buffers
            List<Waypoint>[] inputBufferPath = new List<Waypoint>[instance.NInputPalletStands];
            List<Waypoint>[] outputBufferPath = new List<Waypoint>[instance.NOutputPalletStands];
            for (int i = 0; i < inputBufferPath.Length; i++) inputBufferPath[i] = Enumerable.Repeat(default(Waypoint), instance.InputQueueSize + 1).ToList();
            for (int i = 0; i < outputBufferPath.Length; i++) outputBufferPath[i] = Enumerable.Repeat(default(Waypoint), instance.OutputQueueSize + 1).ToList();

            for (int row = 0; row < instance.MapRowCount; row++)
            {
                for (int col = 0; col < instance.MapColumnCount; col++)
                {
                    if (mapArray[row][col].Equals("0"))
                    {
                        // create storage pods
                        mapArray[row][col] = "nnnn"; //change '0' to 'nnnn' for direction costs
                       
                        if (instance.SettingConfig.usingMapSortItems)
                            UpdateOrderDictionary(row, col);
                        
                        createTile_StorageLocation(row, col, directions.EastNorthSouthWest, instance.ItemAddressArray[row][col], instance.ZoneLocationsOnMap[row][col]);
                    }
                    else if (mapArray[row][col].Equals("X"))
                    {
                        mapArray[row][col] = "ffff"; //change '0' to 'nnnn' for direction costs
                        createTile_UnavailableStorage(row, col, instance.ZoneLocationsOnMap[row][col], directions.EastNorthSouthWest);
                    }
                    else
                    {
                        // parse directions
                        char[] chars = mapArray[row][col].ToCharArray();
                        bool east = true;
                        bool north = true;
                        bool south = true;
                        bool west = true;
                        if (chars[0].Equals('f'))
                            east = false;
                        if (chars[1].Equals('f'))
                            north = false;
                        if (chars[2].Equals('f'))
                            south = false;
                        if (chars[3].Equals('f'))
                            west = false;
                        directions d = LayoutGenerator.GetDirectionType(east, west, south, north);

                        // create roads
                        // it is either of length 4 ('npfu') or the fourth character is 'Q' ('nfupQ')
                        if (chars.Length == 4 || chars[4].Equals('Q'))
                            createTile_Road(row, col, instance.ZoneLocationsOnMap[row][col], d);
                        else
                        {
                            // add parking locations and I/O pallet stands
                            if (chars[4].Equals('P'))
                            {
                                createTile_Road(row, col, instance.ZoneLocationsOnMap[row][col], d);
                                instance.ParkingLot.Add(tiles[row, col].wp);
                            }
                            if (chars[4].Equals('I'))
                            {
                                int standID = int.Parse((new string(chars)).Substring(5).ToString());
                                buildInputPalletStand(row, col, d, inputBufferPath[standID], standID);
                                inputBufferPath[standID][0] = tiles[row, col].wp; //add the pallet stand itself to buffer
                            }
                            if (chars[4].Equals('O'))
                            {
                                int standID = int.Parse((new string(chars)).Substring(5).ToString());
                                buildOutputPalletStand(row, col, d, outputBufferPath[standID], standID);
                                outputBufferPath[standID][0] = tiles[row, col].wp; //add the pallet stand itself to buffer                                
                            }
                            if (chars[4].Equals('X'))
                            {
                                createTile_Road(row, col, instance.ZoneLocationsOnMap[row][col], d, waypointTypes.Unavailable);
                            }
                            // queue zones for pallet stands
                            if (chars[4].Equals('i'))
                            {
                                createTile_Buffer(row, col, d);
                                string str = new string(chars);
                                var qList = str.Split('-');
                                int inputStationId = int.Parse(new string(qList.First().Where(Char.IsDigit).ToArray()));
                                int queueId = int.Parse(qList.Last());
                                inputBufferPath[inputStationId][queueId] = tiles[row, col].wp;
                            }
                            if (chars[4].Equals('o'))
                            {
                                createTile_Buffer(row, col, d);
                                string str = new string(chars);
                                var qList = str.Split('-');
                                int outputStationId = int.Parse(new string(qList.First().Where(Char.IsDigit).ToArray()));
                                int queueId = int.Parse(qList.Last());
                                outputBufferPath[outputStationId][queueId] = tiles[row, col].wp;
                            }

                        }
                    }
                }
            }


            // Location manager queue setup
            for (int row = 0; row < instance.MapRowCount; row++)
            {
                for (int col = 0; col < instance.MapColumnCount; col++)
                {
                    if (mapArray[row][col].Length > 4 && mapArray[row][col].Substring(4, 1).Equals("Q"))
                    {
                        Waypoint wp = tiles[row, col].wp;

                        // this waypoint is an access point for picking
                        wp.isAccessPoint = true;

                        string direction = mapArray[row][col].Substring(5, 1);
                        // if there is no next queue waypoint for this waypoint continue
                        if (direction == "0") continue;

                        // otherwise determine the next queue waypoint...
                        int r = 0, c = 0;
                        switch (direction)
                        {
                            case "n": r = 1; break;
                            case "s": r = -1; break;
                            case "w": c = -1; break;
                            case "e": c = 1; break;
                            default: throw new InvalidDataException("The directions in the map file after 'Q' need to be: 'n', 's', 'w' or 'e'");
                        }
                        wp.nextQueueWaypoint = tiles[row + r,col + c].wp;
                    }
                }
            }

            //extract additional waypoint info
            instance.WaypointXs = instance.Waypoints.Select(wp => wp.X).Distinct().ToList();
            instance.WaypointXs.Sort();
            instance.WaypointYs = instance.Waypoints.Select(wp => wp.Y).Distinct().ToList();
            instance.WaypointYs.Sort();

            foreach(var x in instance.WaypointXs)
            {
                var row = instance.Waypoints.Where(wp => wp.X == x);
                foreach(var wp in row)
                {
                    if(instance.Waypoints_dict.ContainsKey(x))
                    {
                        instance.Waypoints_dict[x].Add(wp.Y, wp);
                    }
                    else
                    {
                        instance.Waypoints_dict.Add(x, new Dictionary<double, Waypoint>());
                        instance.Waypoints_dict[x].Add(wp.Y, wp);
                    }
                }
            }
        }

        public void createTile_StorageLocation(int row, int column, directions d, string address, string zone)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, X, Y, true, false, address, zone);
            wp.Row = row;
            wp.Column = column;
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_StorageLocation: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, waypointTypes.StorageLocation);
        }
        public void createTile_UnavailableStorage(int row, int column, string zone, directions d)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, X, Y, false, false, zone);
            wp.Row = row;
            wp.Column = column;
            wp.Forbidden = true;
            wp.UnavailableStorage = true;
            wp.VerticalLength = layoutConfiguration.PodVerticalLength;
            wp.HorizontalLength = layoutConfiguration.PodHorizontalLength;
           
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_Road: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, waypointTypes.UnavailableStorage);
        }

        public void createTile_Road(int row, int column, string zone, directions d, waypointTypes type = waypointTypes.Road)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, X, Y, false, false, zone);
            wp.Row = row;
            wp.Column = column;
            if (type == waypointTypes.Unavailable)
            {
                wp.Forbidden = true;
            }
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_Road: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, type);
        }
        public void createTile_Road(int row, int column, directions d, waypointTypes type = waypointTypes.Road)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, X, Y, false, false);
            if (type == waypointTypes.Unavailable)
            {
                wp.Forbidden = true;
            }
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_Road: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, type);
        }

        public void GeneratePods(Tier tier)
        {
            List<Waypoint> potentialWaypoints = tier.Waypoints.Where(wp => wp.PodStorageLocation).ToList();
            for (int i = 0; i < instance.PodCount; i++)
            {
                int waypointIndex = rand.NextInt(potentialWaypoints.Count);
                Waypoint chosenWaypoint = potentialWaypoints[waypointIndex];
                Pod pod = instance.CreatePod(instance.RegisterPodID(), tier, chosenWaypoint, layoutConfiguration.PodRadius, layoutConfiguration.PodHorizontalLength, layoutConfiguration.PodVerticalLength, orientationPodDefault, layoutConfiguration.PodCapacity);
                potentialWaypoints.RemoveAt(waypointIndex);
            }
        }

        public void buildOutputPalletStand(int row, int column, directions d, List<Waypoint> bufferPaths, int activationOrderID)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            OutputPalletStand palletStand = instance.CreateOutputPalletStand(
                 instance.RegisterOutputPalletStandID(), tier, X, Y, layoutConfiguration.StationRadius, activationOrderID);
            createTile_OutputPalletStand(row, column, d, palletStand);
            Waypoint wp = tiles[row, column].wp;
            wp.Row = row;
            wp.Column = column;
            palletStand.Queues[wp] = bufferPaths;
        }

        public void buildInputPalletStand(int row, int column, directions d, List<Waypoint> bufferPaths, int activationOrderID)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            InputPalletStand palletStand = instance.CreateInputPalletStand(
                instance.RegisterInputPalletStandID(), tier, X, Y, layoutConfiguration.StationRadius, activationOrderID);
            createTile_InputPalletStand(row, column, d, palletStand);
            Waypoint wp = tiles[row, column].wp;
            wp.Row = row;
            wp.Column = column;
            palletStand.Queues[wp] = bufferPaths;
        }

        public void createTile_Buffer(int row, int column, directions d)
        {
            instance.RowColToXY(row, column, out var X, out var Y);
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, X, Y, false, true);
            wp.Row = row;
            wp.Column = column;
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_Buffer: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, waypointTypes.Buffer);
        }

        public void createTile_OutputPalletStand(int row, int column, directions d, OutputPalletStand oStand)
        {
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, oStand, true);
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_PickStation: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, waypointTypes.PickStation);
        }

        public void createTile_InputPalletStand(int row, int column, directions d, InputPalletStand iStand)
        {
            Waypoint wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, iStand, true);
            if (tiles[row, column] != null)
            {
                throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_ReplenishmentStation: tiles[" + row + ", " + column + "] != null");
            }
            tiles[row, column] = new Tile(d, wp, waypointTypes.ReplenishmentStation);
        }

        public void GenerateRobots(Tier tier, Tile[,] tiles)
        {
            List<List<Waypoint>> waypoints = getWaypointsForInitialRobotPositions(tiles);
            List<Waypoint> potentialLocations = waypoints.SelectMany(w => w).Where(w => w.Pod == null && w.InputStation == null && w.OutputStation == null).ToList();

            Waypoint Waypoint = null;
            double orientation;

            //don't generate MateBots if BotsSelfAssist is used
            if (baseConfiguration.BotsSelfAssist)
                layoutConfiguration.MateBotCount = 0;
            for (int i = 0; i < layoutConfiguration.MovableStationCount + layoutConfiguration.MateBotCount; i++)
            {
                var botType = i < layoutConfiguration.MovableStationCount ? BotType.MovableStation : BotType.MateBot; //first create MovableStations, then MateBots
                // index to determine the set of appropriate spawn waypoints
                switch (baseConfiguration.BotLocations)
                {
                    case BotLocations.Fixed:
                        int index = botType == BotType.MovableStation ? 0 : 1;
                        // the waypoints have already been loaded
                        var wpDirInd = spawnWaypoints.Where(x => x.Item3 == index).FirstOrDefault();
                        if (wpDirInd == null)
                        {
                            if (botType == BotType.MateBot) { goto case BotLocations.Random; }
                            else
                            {
                                Waypoint = instance.ParkingLot.ElementAt(i);
                                orientation = 0.0;
                            }
                        }
                        else
                        {
                            Waypoint = wpDirInd.Item1;
                            orientation = wpDirInd.Item2;
                            spawnWaypoints.Remove(wpDirInd);
                        }
                        break;
                    case BotLocations.Random:
                        int randomWaypointIndex = rand.NextInt(potentialLocations.Count);
                        Waypoint = potentialLocations[randomWaypointIndex];
                        orientation = 0;
                        break;
                    default:
                        Waypoint = null;
                        orientation = 0;
                        break;
                }
                List<string> zones = new List<string>();
                if (instance.SettingConfig.ZonesEnabled)
                {
                    if (i < layoutConfiguration.MovableStationCount)
                        zones.Add("none");
                    else
                        zones = mateBotZones[i - layoutConfiguration.MovableStationCount];
                }    
                else zones.Add("none");
                double hue = 0;
                // if this is movable station
                if (i < layoutConfiguration.MovableStationCount)
                {
                    if (layoutConfiguration.MovableStationCount > 1)
                    {
                        hue = (200 / (layoutConfiguration.MovableStationCount - 1) * i);
                    }
                }
                // else this is mate bot
                else
                {
                    if (layoutConfiguration.MateBotCount > 1)
                    {
                        hue = (200 / (layoutConfiguration.MateBotCount - 1) * (i - layoutConfiguration.MovableStationCount));
                    }
                    else hue = 100;
                }
                double radius = layoutConfiguration.BotRadius;
                radius /= i < layoutConfiguration.MovableStationCount ? 1 : 1.3; // decrease the picker raidus a little -> FIX
                double maxAcceleration = botType == BotType.MovableStation ? layoutConfiguration.MaxAcceleration : botType == BotType.MateBot ? layoutConfiguration.MaxMateAcceleration : double.NaN;
                double maxDeceleration = botType == BotType.MovableStation ? layoutConfiguration.MaxDeceleration : botType == BotType.MateBot ? layoutConfiguration.MaxMateDeceleration : double.NaN;
                double maxVelocity = botType == BotType.MovableStation ? layoutConfiguration.MaxVelocity : botType == BotType.MateBot ? layoutConfiguration.MaxMateVelocity : double.NaN;
                double turnSpeed = botType == BotType.MovableStation ? layoutConfiguration.TurnSpeed : botType == BotType.MateBot ? layoutConfiguration.MateTurnSpeed : double.NaN;
                Bot bot = instance.CreateBot(instance.RegisterBotID(), tier, Waypoint.X, Waypoint.Y, radius, orientation,
                                                layoutConfiguration.PodTransferTime, maxAcceleration, maxDeceleration, maxVelocity, turnSpeed,
                                                layoutConfiguration.CollisionPenaltyTime, botType, hue, zones);
                Waypoint.AddBotApproaching(bot);
                bot.CurrentWaypoint = Waypoint;
                potentialLocations.Remove(Waypoint);
            }
        }

        public void LocateResourceFiles()
        {
            if (layoutConfiguration.MapFile != null && !string.IsNullOrWhiteSpace(layoutConfiguration.MapFile))
                layoutConfiguration.MapFile = IOHelper.FindResourceFile(layoutConfiguration.MapFile, Directory.GetCurrentDirectory());
            if (baseConfiguration.InventoryConfiguration.ColoredWordConfiguration != null && !string.IsNullOrWhiteSpace(baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile))
                baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile = IOHelper.FindResourceFile(baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile, Directory.GetCurrentDirectory());
            if (baseConfiguration.InventoryConfiguration.OrderMode == OrderMode.Fixed && !string.IsNullOrWhiteSpace(baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile))
                baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile = IOHelper.FindResourceFile(baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile, Directory.GetCurrentDirectory());
            if (baseConfiguration.InventoryConfiguration.OrderMode == OrderMode.Fixed && !string.IsNullOrWhiteSpace(baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile))
                baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile = IOHelper.FindResourceFile(baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile, Directory.GetCurrentDirectory());
        }

        public List<List<Waypoint>> getWaypointsForInitialRobotPositions(Tile[,] tiles)
        {
            List<List<Waypoint>> waypoints = new List<List<Waypoint>>();
            for (int row = 0; row < tiles.GetLength(0); row++)
            {
                waypoints.Add(new List<Waypoint>());
                for (int column = 0; column < tiles.GetLength(1); column++)
                {
                    Tile tile = tiles[row, column];
                    if (tile != null && (tile.type.Equals(waypointTypes.Road) || tile.type.Equals(waypointTypes.Buffer)))
                    {
                        Waypoint waypoint = tiles[row, column].wp;
                        waypoints.Last().Add(waypoint);
                    }
                }
            }
            return waypoints;
        }

        public void WriteAllDirectionsInfo(Tile[,] tiles)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Info about all directions within the generated instance:");
            for (int i = 0; i < tiles.GetLength(0); i++)
            {
                for (int j = 0; j < tiles.GetLength(1); j++)
                {
                    if (tiles[i, j] == null)
                    {
                        sb.Append("# ");
                    }
                    else
                    {
                        sb.Append(tiles[i, j].directionAsString() + " ");
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            Write(sb.ToString());
        }

        public void WriteAllTypesInfo(Tile[,] tiles)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Info about all types within the generated instance:");
            for (int i = 0; i < tiles.GetLength(0); i++)
            {
                for (int j = 0; j < tiles.GetLength(1); j++)
                {
                    if (tiles[i, j] == null)
                    {
                        sb.Append("# ");
                    }
                    else
                    {
                        sb.Append(tiles[i, j].typeAsString());
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            Write(sb.ToString());
        }


        #region helper methods

        /// <summary>
        /// Helper method projecting boolean direction markers into a direction type.
        /// </summary>
        /// <param name="east">Indicates whether a east direction is desired.</param>
        /// <param name="west">Indicates whether a west direction is desired.</param>
        /// <param name="north">Indicates whether a north direction is desired.</param>
        /// <param name="south">Indicates whether a south direction is desired.</param>
        /// <returns>The direction.</returns>
        public static directions GetDirectionType(bool east, bool west, bool south, bool north)
        {
            if (east)
            {
                // EAST
                if (west)
                {
                    // WEST
                    if (north)
                    {
                        // NORTH
                        if (south)
                            // SOUTH
                            return directions.EastNorthSouthWest;
                        else
                            // NO SOUTH
                            return directions.EastNorthWest;
                    }
                    else
                    {
                        // NO NORTH
                        if (south)
                            // SOUTH
                            return directions.EastSouthWest;
                        else
                            // NO SOUTH
                            return directions.EastWest;
                    }
                }
                else
                {
                    // NO WEST
                    if (north)
                    {
                        // NORTH
                        if (south)
                            // SOUTH
                            return directions.EastNorthSouth;
                        else
                            // NO SOUTH
                            return directions.EastNorth;
                    }
                    else
                    {
                        // NO NORTH
                        if (south)
                            // SOUTH
                            return directions.EastSouth;
                        else
                            // NO SOUTH
                            return directions.East;
                    }
                }
            }
            else
            {
                // NO EAST
                if (west)
                {
                    // WEST
                    if (north)
                    {
                        // NORTH
                        if (south)
                            // SOUTH
                            return directions.NorthSouthWest;
                        else
                            // NO SOUTH
                            return directions.NorthWest;
                    }
                    else
                    {
                        // NO NORTH
                        if (south)
                            // SOUTH
                            return directions.SouthWest;
                        else
                            // NO SOUTH
                            return directions.West;
                    }
                }
                else
                {
                    // NO WEST
                    if (north)
                    {
                        // NORTH
                        if (south)
                            // SOUTH
                            return directions.NorthSouth;
                        else
                            // NO SOUTH
                            return directions.North;
                    }
                    else
                    {
                        // NO NORTH
                        if (south)
                            // SOUTH
                            return directions.South;
                        else
                            // NO SOUTH
                            return directions.Invalid;
                    }
                }
            }
        }

        /// <summary>
        /// Prints the layout given by the 2D array to the console.
        /// </summary>
        /// <param name="tiles">The layout to print.</param>
        /// <param name="showCols">Shows the column indices instead of the type.</param>
        /// <param name="showRows">Shows the row indices instead of the type.</param>
        internal static void DebugPrintLayout(Tile[,] tiles, bool showRows = false, bool showCols = false)
        {
            int maxRowIndexLength = (tiles.GetLength(0) - 1).ToString().Length;
            int maxColIndexLength = (tiles.GetLength(1) - 1).ToString().Length;
            for (int i = 0; i < tiles.GetLength(0); i++)
            {
                for (int j = 0; j < tiles.GetLength(1); j++)
                    Console.Write((
                        showRows ? i.ToString().PadLeft(maxRowIndexLength) :
                        showCols ? j.ToString().PadLeft(maxColIndexLength) :
                        (tiles[i, j] != null ? tiles[i, j].directionAsString() : " ")) +
                        (j == tiles.GetLength(1) - 1 ? "" : " "));
                Console.WriteLine();
            }
        }

        #endregion

    }

    internal enum directions
    {
        //this enum indicates to which other waypoints a waypoint is connected. 
        //So for example, East means that a waypoint is only connected to the waypoint directly east of it, 
        //whereas EastNorth would indicate that the waypoint is connected to both the one directly to the east and directly to the north

        //used alphabetical order for ordering directions when there are multiple (i.e. EastNorth instead of NorthEast)

        //single directional:
        East, North, South, West,

        //two directions:
        EastNorth, EastSouth, EastWest,
        NorthSouth, NorthWest,
        SouthWest,

        //three directions:
        EastNorthSouth, EastNorthWest, EastSouthWest, NorthSouthWest,

        //four directions:
        EastNorthSouthWest,

        //invalid value
        Invalid,
    }
    /// <summary>
    /// A subset of the possible directions that is limited to single directions.
    /// </summary>
    internal enum UniDirections
    {
        /// <summary>
        /// An invalid direction.
        /// </summary>
        Invalid,
        /// <summary>
        /// A connection to the east.
        /// </summary>
        East,
        /// <summary>
        /// A connection to the north.
        /// </summary>
        North,
        /// <summary>
        /// A connection to the south.
        /// </summary>
        South,
        /// <summary>
        /// A connection to the west.
        /// </summary>
        West,
    }
    /// <summary>
    /// Distinguishes the different hallways that can be generated.
    /// </summary>
    public enum HallwayField
    {
        /// <summary>
        /// Indicates the eastern hallway field.
        /// </summary>
        East,
        /// <summary>
        /// Indicates the western hallway field.
        /// </summary>
        West,
        /// <summary>
        /// Indicates the southern hallway field.
        /// </summary>
        South,
        /// <summary>
        /// Indicates the northern hallway field.
        /// </summary>
        North,
    }

    public enum waypointTypes
    {
        Elevator, Road, StorageLocation, PickStation, ReplenishmentStation, Buffer, Invalid, Unavailable, UnavailableStorage
    }

    /// <summary>
    /// Comprises a coordinate.
    /// </summary>
    internal struct Coordinate
    {
        /// <summary>
        /// Creates a new coordinate.
        /// </summary>
        /// <param name="row">The row of the coordinate.</param>
        /// <param name="column">The column of the coordinate.</param>
        public Coordinate(int row, int column) { Row = row; Column = column; }
        /// <summary>
        /// The row.
        /// </summary>
        int Row;
        /// <summary>
        /// The column.
        /// </summary>
        int Column;
        /// <summary>
        /// Returns a string representation of the coordinate.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString() { return $"{Row},{Column}"; }
    }

    class Tile
    {
        public Waypoint wp { get; }
        public directions direction { get; }
        public waypointTypes type { get; }

        public Tile(directions d, Waypoint wp, waypointTypes type)
        {
            this.direction = d;
            this.wp = wp;
            this.type = type;

            if (type.Equals(waypointTypes.StorageLocation) && !d.Equals(directions.EastNorthSouthWest))
            {
                throw new ArgumentException("something went wrong with storage locations");
            }
            if (d.Equals(directions.Invalid))
            {
                throw new ArgumentException("direction invalid");
            }
            if (wp == null)
            {
                throw new ArgumentException("wp is null");
            }
        }

        public String directionAsString()
        {
            switch (direction)
            {
                case directions.EastNorthSouthWest: return "+";
                case directions.NorthSouthWest: return "<";
                case directions.EastNorthSouth: return ">";
                case directions.EastNorthWest: return "^";
                case directions.EastSouthWest: return "v";
                case directions.NorthSouth: return "|";
                case directions.NorthWest: return "d";
                case directions.EastNorth: return "b";
                case directions.SouthWest: return "q";
                case directions.EastSouth: return "p";
                case directions.EastWest: return "-";
                case directions.East: return "e";
                case directions.West: return "w";
                case directions.South: return "s";
                case directions.North: return "n";
                case directions.Invalid: return "INVALID DIRECTION!";
                default: return "SOMETHING WENT WRONG";
            }
        }

        public bool isStation()
        {
            return type.Equals(waypointTypes.PickStation) || type.Equals(waypointTypes.ReplenishmentStation) || type.Equals(waypointTypes.Elevator);
        }

        public String typeAsString()
        {
            switch (type)
            {
                case waypointTypes.Elevator: return "e ";
                case waypointTypes.Road: return "r ";
                case waypointTypes.StorageLocation: return "s ";
                case waypointTypes.Buffer: return "b ";
                case waypointTypes.PickStation: return "o ";
                case waypointTypes.ReplenishmentStation: return "i ";
                case waypointTypes.Invalid: return "INVALID DIRECTION!";
                default: return "SOMETHING WENT WRONG";
            }
        }
    }
}
