using RAWSimO.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RAWSimO.Core.Configurations
{
    /// <summary>
    /// Declares some basic aisle layout types.
    /// </summary>
    public enum AisleLayoutTypes
    {
        /// <summary>
        /// Layout of Tim Lamballais.
        /// </summary>
        Tim,
        /// <summary>
        /// Modified layout using a highway as a hallway enabling more possibilities for the bots to switch aisles.
        /// </summary>
        HighwayHallway,
    }

    [XmlInclude(typeof(WarehouseConfiguration))]
    /// <summary>
    /// Supplies all attributes of a layout for generating it.
    /// </summary>
    public class LayoutConfiguration
    {
        /// <summary>
        /// Function that overrides the default variable values in layout configuration using the warehouse configuration.
        /// </summary>
        public void OverrideData()
        {
            MapFile = warehouse.GetMapFilePath();
            ItemAddressesFile = warehouse.GetAddressesFilePath();
            ItemAddressSortOrderFile = warehouse.GetAddressesSortFilePath();
            if (warehouse.UseZones())
            {
                ZoneFile = warehouse.GetZoneFilePath();
                ZoneConfiguration = warehouse.GetPickerToZoneFilePath();
            }
            // set the layout configuration variables to the warehouse values
            PodRadius = warehouse.pod_radius;
            PodHorizontalLength = warehouse.pod_horizontal_len;
            PodVerticalLength = warehouse.pod_vertical_len;
            HorizontalWaypointDistance = warehouse.wp_horizontal_dist;
            VerticalWaypointDistance = warehouse.wp_vertical_dist;
            MaxVelocity = warehouse.vehicle_max_velocity;
            MaxMateVelocity = warehouse.human_max_velocity;
            MaxAcceleration = warehouse.vehicle_max_acceleration;
            MaxMateAcceleration = warehouse.human_max_acceleration;
            MaxDeceleration = warehouse.vehicle_max_deceleration;
            MaxMateDeceleration = warehouse.human_max_deceleration;
        }


        #region member variables 

        /// <summary>
        /// If true the data for warehouse, orders and robots is read from file
        /// </summary>
        public bool OverrideDataWithFile = true;

        protected class MapData
        {
            public double waypoint_horizontal_distance;
            public double waypoint_vertical_distance;
            public double pod_radius;
            public double pod_horizontal_length;
            public double pod_vertical_length;
        }

        protected class OrdersData
        {
            public double simulation_duration;
            public double const_assist_time;
            public double switch_pallet_time;
            public double average_batch_size;
            public double batch_time_interval;
            public bool poisson;
        }

        protected class AgentData
        {
            public bool bots_as_pickers;
            public double vehicle_max_velocity;
            public double human_max_velocity;
            public double vehicle_max_acceleration;
            public double vehicle_max_deceleration;
            public double human_max_acceleration;
            public double human_max_deceleration;
            //public bool reserve_same_loc;
            //public bool reserve_next_loc;
        }

        protected class AgentDataSelector
        {
            public int agent_data_version_index { get; set; }
            public string[] agent_data_versions { get; set; }
        }

        protected class WarehouseSelector
        {
            public int warehouse_index { get; set; }
            public string[] warehouses { get; set; }
        }

        protected class VersionSelector
        {
            public int warehouse_version_index { get; set; }
            public int orders_version_index { get; set; }
            public int zones_index { get; set; }
            public int spawn_locations { get; set; }
            public string[] warehouse_versions { get; set; }
            public string[] orders_versions { get; set; }
            public string[] zones_versions { get; set; }
        }
        public class WarehouseConfiguration
        {
            private AgentDataSelector _agentDataSelector;
            private WarehouseSelector _warehouseSelector;
            private VersionSelector _versionSelector;
            private MapData _mapData;
            private OrdersData _ordersData;
            private AgentData _agentData;
            private string _mainFolder = "warehouse_examples";
            private string _mainWarehouseDataFolder = "Warehouse_data";
            private string _name;
            private string _mapsFolder = "maps";
            private string _ordersFolder = "orders";
            private string _zonesFolder = "zones";
            private string _mainAgentDataFolder = "Agent_data";
            private char sep = System.IO.Path.DirectorySeparatorChar;

            public string name;
            // public variables are displayed on the instance view
            // warehouse version
            public string map;
            // sort the addresses in orders
            public string orders;
            public bool commission_order;
            public double time_limit;
            public double const_assist_time;
            public double switch_pallet_time;
            public double avg_batch_size;
            public double batch_time_interval;
            public bool poisson;
            // if empty don't use the zones
            public string zones;
            // warehouse parametrs
            // traveling distance between waypoints
            public double wp_horizontal_dist;
            public double wp_vertical_dist;
            // how much space is taken by pod
            // TODO: chech this info
            public double pod_radius;
            // visual occupation
            public double pod_horizontal_len;
            public double pod_vertical_len;

            public string spawn_locations;
            private BotLocations _spawn_locations_type;
            public string agent_config;
            private string _agent_config;
            public bool bots_as_pickers;
            public double vehicle_max_velocity;
            public double human_max_velocity;
            public double vehicle_max_acceleration;
            public double human_max_acceleration;
            public double vehicle_max_deceleration;
            public double human_max_deceleration;
            //public bool reserve_same_loc;
            //public bool reserve_next_loc;

            public WarehouseConfiguration()
            {
                string rootFolder = System.IO.Directory.GetCurrentDirectory();

                string warehouseSelectorFile = _mainFolder + sep + _mainWarehouseDataFolder + sep + "warehouse_selector.json";
                string warehouseSelectorPath = IOHelper.FindResourceFile(warehouseSelectorFile, rootFolder); 
                _warehouseSelector = Newtonsoft.Json.JsonConvert.DeserializeObject<WarehouseSelector>(System.IO.File.ReadAllText(@warehouseSelectorPath));

                _name = _warehouseSelector.warehouses[_warehouseSelector.warehouse_index];
                name = _name.ToUpper();

                string agentDataSelectorFile = _mainFolder + sep + _mainAgentDataFolder + sep + "agent_data_selector.json";
                string agentDataSelectorPath = IOHelper.FindResourceFile(agentDataSelectorFile, rootFolder); 
                _agentDataSelector = Newtonsoft.Json.JsonConvert.DeserializeObject<AgentDataSelector>(System.IO.File.ReadAllText(@agentDataSelectorPath));
                _agent_config = _agentDataSelector.agent_data_versions[_agentDataSelector.agent_data_version_index];
                agent_config = _agent_config.ToUpper();

                string agentDataFile = _mainFolder + sep + _mainAgentDataFolder + sep + _agent_config + sep + "agent_data.json";
                string agentDataPath = IOHelper.FindResourceFile(agentDataFile, rootFolder);
                _agentData = Newtonsoft.Json.JsonConvert.DeserializeObject<AgentData>(System.IO.File.ReadAllText(@agentDataPath));

                bots_as_pickers = _agentData.bots_as_pickers;
                vehicle_max_velocity = _agentData.vehicle_max_velocity;
                human_max_velocity = _agentData.human_max_velocity;
                vehicle_max_acceleration = _agentData.vehicle_max_acceleration;
                human_max_acceleration = _agentData.human_max_acceleration;
                vehicle_max_deceleration = _agentData.vehicle_max_deceleration;
                human_max_deceleration = _agentData.human_max_deceleration;
                //reserve_same_loc = _agentData.reserve_same_loc;
                //reserve_next_loc = _agentData.reserve_next_loc;

                string versionSelectorFile = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + "version_selector.json"; 
                string versionSelectorPath = IOHelper.FindResourceFile(versionSelectorFile, rootFolder);
                _versionSelector = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionSelector>(System.IO.File.ReadAllText(@versionSelectorPath));
                map = _versionSelector.warehouse_versions[_versionSelector.warehouse_version_index];
                commission_order = UsingPreferredCommissionOrder();
                orders = _versionSelector.orders_versions[_versionSelector.orders_version_index];

                spawn_locations = _versionSelector.spawn_locations == 0 ? "random" : "fixed";
                _spawn_locations_type = _versionSelector.spawn_locations == 0 ? BotLocations.Random : BotLocations.Fixed;

                string ordersDataFile= _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _ordersFolder + sep + orders + sep + "orders_data.json";
                string orderDataPath = IOHelper.FindResourceFile(ordersDataFile, rootFolder);
                _ordersData = Newtonsoft.Json.JsonConvert.DeserializeObject<OrdersData>(System.IO.File.ReadAllText(orderDataPath));

                int zones_index = _versionSelector.zones_index;
                if (zones_index == -1)
                    zones = "";
                else
                    zones = _versionSelector.zones_versions[zones_index];

                string mapDataFile = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _mapsFolder + sep + map + sep + "map_data.json"; 
                string mapDataPath = IOHelper.FindResourceFile(mapDataFile, rootFolder);
                _mapData = Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(System.IO.File.ReadAllText(@mapDataPath));

                // set the variables
                wp_horizontal_dist = _mapData.waypoint_horizontal_distance;
                wp_vertical_dist = _mapData.waypoint_vertical_distance;
                pod_radius = _mapData.pod_radius;
                pod_horizontal_len = _mapData.pod_horizontal_length;
                pod_vertical_len = _mapData.pod_vertical_length;

                time_limit = _ordersData.simulation_duration;
                const_assist_time = _ordersData.const_assist_time;
                switch_pallet_time = _ordersData.switch_pallet_time;
                avg_batch_size = _ordersData.average_batch_size;
                batch_time_interval = _ordersData.batch_time_interval;
                poisson = _ordersData.poisson;

            }
            public string GetMapFilePath()
            {
                string mapFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _mapsFolder + sep + map + sep + "map.csv";
                return mapFilePath;
            }
            public string GetAddressesFilePath()
            {
                string addressesFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _mapsFolder + sep + map + sep + "addresses.csv";
                return addressesFilePath;
            }
            public string GetAddressesSortFilePath()
            {
                string addressesSortFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _mapsFolder + sep + map + sep + "addresses_sort.csv";
                return addressesSortFilePath;
            }
            public bool UsingPreferredCommissionOrder()
            {
                bool commission_order = false;
                try
                {
                    string path = IOHelper.FindResourceFile(GetAddressesSortFilePath(), System.IO.Directory.GetCurrentDirectory());
                    Console.WriteLine(path);
                    commission_order = true;
                }
                catch (ArgumentException ex)
                {
                    commission_order = false;
                }
                return commission_order;
            }
            public string GetOrderFilePath()
            {
                string orderFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _ordersFolder + sep + orders + sep + "orders.txt";
                return orderFilePath;
            }
            public bool UseConstAssistTime()
            {
                return const_assist_time > 0;
            }

            public bool UseOrderBatching()
            {
                return avg_batch_size > 0;
            }
            public bool UseZones()
            {
                return !zones.Equals("");
            }
            public string GetZoneFilePath()
            {
                string zoneFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _zonesFolder + sep + zones + sep + "zones.csv";
                return zoneFilePath;
            }
            public string GetPickerToZoneFilePath()
            {
                string pickerToZoneFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _zonesFolder + sep + zones + sep + "pickers_to_zones.json";
                return pickerToZoneFilePath;
            }
            public BotLocations GetSpawnLocationsType()
            {
                return _spawn_locations_type;
            }
            public string GetSpawnLocationsFilePath()
            {
                string spawnLocationsFilePath = _mainFolder + sep + _mainWarehouseDataFolder + sep + _name + sep + _mapsFolder + sep + map + sep + "spawn_locations.txt";
                return spawnLocationsFilePath;
            }
        }


        public WarehouseConfiguration warehouse = new WarehouseConfiguration();

        /// <summary>
        /// The number of MovableStations to generate.
        /// </summary>
        public int MovableStationCount = 6; 
        /// <summary>
        /// The number of Mates to generate
        /// </summary>
        public int MateBotCount = 4;
        /// <summary>
        /// The csv file containing map layout and direction costs 
        /// </summary>
        public string MapFile = "";
        /// <summary>
        /// The csv file containing Item addresses 
        /// </summary>
        public string ItemAddressesFile = "";
        public string ItemAddressSortOrderFile = "";
        /// <summary>
        /// The csv file containing Matebot zones
        /// </summary>
        public string ZoneFile = "";
        /// <summary>
        /// The json file with all zone configurations
        /// </summary>
        public string ZoneConfiguration = "";
        /// <summary>
        /// Horizontal distance between waypoints
        /// </summary>
        public double HorizontalWaypointDistance = 0.9;
        /// <summary>
        /// Horizontal distance between waypoints
        /// </summary>
        public double VerticalWaypointDistance = 1.3;
        /// <summary>
        /// The radius of a bot in m.
        /// </summary>
        public double BotRadius = 0.35;
        /// <summary>
        /// The acceleration of a bot in m/s^2.
        /// </summary>
        public double MaxAcceleration = 0.25;
        /// <summary>
        /// The acceleration of a MateBot in m/s^2.
        /// </summary>
        public double MaxMateAcceleration = 10;
        /// <summary>
        /// The deceleration of a bot in m/s^2.
        /// </summary>
        public double MaxDeceleration = 0.25;
        /// <summary>
        /// The deceleration of a MateBot in m/s^2.
        /// </summary>
        public double MaxMateDeceleration = 10;
        /// <summary>
        /// The maximal velocity of a bot in m/s.
        /// </summary>
        public double MaxVelocity = 1.15;
        /// <summary>
        /// The maximal velocity of a MateBot in m/s.
        /// </summary>
        public double MaxMateVelocity = 1;
        /// <summary>
        /// The time used for turning in while driving
        /// </summary>
        public double TurnSpeed = 2;
        /// <summary>
        /// The time it takes for the bot to do a complete (360°) turn in s.
        /// </summary>
        public double StartStopTurnSpeed = 12.5;
        /// <summary>
        /// The time it takes for the MateBot to do a complete (360°) turn in s.
        /// </summary>
        public double MateTurnSpeed = 2;
        /// <summary>
        /// The penalty time for bots that collide.
        /// </summary>
        public double CollisionPenaltyTime = 0.5;
        /// <summary>
        /// The time it takes to pickup / setdown a pod.
        /// </summary>
        public double PodTransferTime = 2.2;
        /// <summary>
        /// The amount of pods generated relative to the number of available storage locations.
        /// </summary>
        public double PodAmount = 1;
        /// <summary>
        /// The radius of a pod in m.
        /// </summary>
        public double PodRadius = 0.4;
        /// <summary>
        /// The horizontal length of a pod in m.
        /// </summary>
        public double PodHorizontalLength = 0.4;
        /// <summary>
        /// The vertical length of a pod in m.
        /// </summary>
        public double PodVerticalLength = 0.6;
        /// <summary>
        /// The capacity of a pod.
        /// </summary>
        public double PodCapacity = 500;
        /// <summary>
        /// The radius of the I/O-stations in m.
        /// </summary>
        public double StationRadius = 0.45;
        /// <summary>
        /// The name of the layout.
        /// </summary>
        public string NameLayout = "tiny";
        /// <summary>
        /// The number of tiers to generate.
        /// </summary>
        public int TierCount = 1;
        /// <summary>
        /// The height of a tier. (Only relevant for visual feedback)
        /// </summary>
        public double TierHeight = 4;
            

        #endregion

        /// <summary>
        /// Applies values set by an override configuration.
        /// </summary>
        /// <param name="overrideConfig">The override config to apply.</param>
        public void ApplyOverrideConfig(OverrideConfiguration overrideConfig)
        {
            // Return on null config
            if (overrideConfig == null)
                return;
            // Apply values
            if (overrideConfig.OverrideBotPodTransferTime)
                PodTransferTime = overrideConfig.OverrideBotPodTransferTimeValue;
            if (overrideConfig.OverrideBotMaxAcceleration)
                MaxAcceleration = overrideConfig.OverrideBotMaxAccelerationValue;
            if (overrideConfig.OverrideBotMaxDeceleration)
                MaxDeceleration = overrideConfig.OverrideBotMaxDecelerationValue;
            if (overrideConfig.OverrideBotMaxVelocity)
                MaxVelocity = overrideConfig.OverrideBotMaxVelocityValue;
            if (overrideConfig.OverrideBotTurnSpeed)
                TurnSpeed = overrideConfig.OverrideBotTurnSpeedValue;
            if (overrideConfig.OverridePodCapacity)
                PodCapacity = overrideConfig.OverridePodCapacityValue;
        }

        /// <summary>
        /// Adjusts values to an override target some by equally increasing or decreasing them.
        /// </summary>
        /// <param name="targetValue">The target value.</param>
        /// <param name="firstValue">The first value (this will be modified first).</param>
        /// <param name="secondValue">The first value (this will be modified second).</param>
        /// <param name="thirdValue">The first value (this will be modified third).</param>
        /// <param name="fourthValue">The first value (this will be modified fourth).</param>
        private void AdjustToOverrideValue(int targetValue, ref int firstValue, ref int secondValue, ref int thirdValue, ref int fourthValue)
        {
            if (targetValue < 0)
                throw new ArgumentException("Cannot target a negative value!");
            int currentValue = firstValue + secondValue + thirdValue + fourthValue;
            int currentValueToModify = 0;
            while (currentValue != targetValue)
            {
                if (currentValue < targetValue)
                {
                    switch (currentValueToModify)
                    {
                        case 0: firstValue++; break;
                        case 1: secondValue++; break;
                        case 2: thirdValue++; break;
                        case 3: fourthValue++; break;
                        default: throw new InvalidOperationException("Unknown index!");
                    }
                    currentValue++;
                }
                else if (currentValue > targetValue)
                {
                    switch (currentValueToModify)
                    {
                        case 0: if (firstValue > 0) firstValue--; break;
                        case 1: if (secondValue > 0) secondValue--; break;
                        case 2: if (thirdValue > 0) thirdValue--; break;
                        case 3: if (fourthValue > 0) fourthValue--; break;
                        default: throw new InvalidOperationException("Unknown index!");
                    }
                    currentValue--;
                }
                else
                {
                    throw new InvalidOperationException("Something went wrong while adjusting to target value!");
                }
                // Move on to next value
                currentValueToModify = (currentValueToModify + 1) % 4;
            }
        }

        /// <summary>
        /// Returns a simple layout describing name.
        /// </summary>
        /// <returns>A string that can be used as an instance / layout name.</returns>
        public string GetMetaInfoBasedLayoutName()
        {
            string delimiter = "-";
            return MovableStationCount + delimiter + MateBotCount + delimiter + PodAmount.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        }

        /// <summary>
        /// Checks whether the layout can be generated.
        /// </summary>
        /// <param name="errorMessage">A message describing the error if the layout is not valid.</param>
        /// <returns>Indicates whether the layout is valid.</returns>
        public bool isValid(out String errorMessage)
        {
            if (TierCount <= 0)
            {
                errorMessage = "TierCount <= 0, TierCount: " + TierCount;
                return false;
            }
            if (TierHeight <= 0)
            {
                errorMessage = "TierHeight <= 0, TierHeight: " + TierHeight;
                return false;
            }
            if (BotRadius <= 0 || BotRadius >= 0.5)
            {
                errorMessage = "BotRadius <= 0 || BotRadius >= 0.5, BotRadius: " + BotRadius;
                return false;
            }
            if (MaxAcceleration <= 0)
            {
                errorMessage = "MaxAcceleration <= 0, MaxAcceleration: " + MaxAcceleration;
                return false;
            }
            if (MaxDeceleration <= 0)
            {
                errorMessage = "MaxDeceleration <= 0, MaxDeceleration: " + MaxDeceleration;
                return false;
            }
            if (MaxVelocity <= 0)
            {
                errorMessage = "MaxVelocity <= 0, MaxVelocity: " + MaxVelocity;
                return false;
            }
            if (TurnSpeed <= 0)
            {
                errorMessage = "TurnSpeed <= 0, TurnSpeed: " + TurnSpeed;
                return false;
            }
            if (CollisionPenaltyTime <= 0)
            {
                errorMessage = "CollisionPenaltyTime <= 0, CollisionPenaltyTime: " + CollisionPenaltyTime;
                return false;
            }
            if (PodTransferTime <= 0)
            {
                errorMessage = "PodTransferTime <= 0, PodTransferTime: " + PodTransferTime;
                return false;
            }
            if (PodAmount <= 0 || PodAmount > 1)
            {
                errorMessage = "PodAmount <= 0 || PodAmount > 1, PodAmount: " + PodAmount;
                return false;
            }
            if (PodRadius <= 0 || PodRadius >= 0.5)
            {
                errorMessage = "PodRadius <= 0 || PodRadius >= 0.5, PodRadius: " + PodRadius;
                return false;
            }
            if (PodCapacity <= 0)
            {
                errorMessage = "PodCapacity <= 0, PodCapacity: " + PodCapacity;
                return false;
            }
            if (StationRadius <= 0 || StationRadius >= 0.5)
            {
                errorMessage = "StationRadius <= 0 || StationRadius >= 0.5, StationRadius: " + StationRadius;
                return false;
            }

            errorMessage = "";
            return true;
        }

    }
}
