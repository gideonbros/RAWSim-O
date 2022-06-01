﻿using RAWSimO.Toolbox;
using RAWSimO.Core.Configurations;
using RAWSimO.Core.Generator;
using RAWSimO.Core.Items;
using RAWSimO.Core.Randomization;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace RAWSimO.Core.IO
{
    /// <summary>
    /// Exposes methods to serialize and deserialize instances and more.
    /// </summary>
    public class InstanceIO
    {
        private static readonly XmlSerializer _instanceSerializer = new XmlSerializer(typeof(DTOInstance));
        private static readonly XmlSerializer _layoutConfigSerializer = new XmlSerializer(typeof(LayoutConfiguration));
        private static readonly XmlSerializer _settingConfigSerializer = new XmlSerializer(typeof(SettingConfiguration));
        private static readonly XmlSerializer _controlConfigSerializer = new XmlSerializer(typeof(ControlConfiguration));
        private static readonly XmlSerializer _listSerializer = new XmlSerializer(typeof(DTOOrderList));
        private static readonly XmlSerializer _simpleItemGeneratorConfigSerializer = new XmlSerializer(typeof(SimpleItemGeneratorConfiguration));

        #region Read

        /// <summary>
        /// Reads an instance from a file.
        /// </summary>
        /// <param name="instancePath">The path to either the instance or a layout configuration.</param>
        /// <param name="settingConfigPath">The path to the file specifying the setting.</param>
        /// <param name="controlConfigPath">The path to the file supplying the configuration for all controlling mechanisms.</param>
        /// <param name="overrideVisualizationAttached">Indicates whether a visualization shall be attached.</param>
        /// <param name="visualizationOnly">If this is enabled most of the initialization will be skipped.</param>
        /// <param name="logAction">A action that will be used for logging some lines.</param>
        /// <returns></returns>
        public static Instance ReadInstance(
            string instancePath,
            string settingConfigPath,
            string controlConfigPath,
            bool overrideVisualizationAttached = false,
            bool visualizationOnly = false,
            Action<string> logAction = null)
        {
            // Test for layout / instance file
            XmlDocument doc = new XmlDocument();
            doc.Load(instancePath);
            string rootName = doc.SelectSingleNode("/*").Name;
            bool layoutConfigurationGiven = false;
            if (rootName == nameof(Instance)) layoutConfigurationGiven = false;
            else if (rootName == nameof(LayoutConfiguration)) layoutConfigurationGiven = true;
            else throw new ArgumentException("No valid instance or layout file given!");
            logAction?.Invoke(rootName + " recognized!");

            // --> Read configurations
            SettingConfiguration settingConfig = null;
            ControlConfiguration controlConfig = null;
            LayoutConfiguration layoutConfig = null;
            if (!visualizationOnly)
            {
                // Read the setting configuration
                logAction?.Invoke("Parsing setting config ...");
                using (StreamReader sr = new StreamReader(settingConfigPath))
                {
                    // Deserialize the xml-file
                    settingConfig = (SettingConfiguration)_settingConfigSerializer.Deserialize(sr);
                    if (settingConfig.Seed == -1)
                    {
                        settingConfig.Seed = RandomizerSimple.GetRandomSeed();
                    }
                    // If it contains a path to a word-file that is not leading to a wordlist file try the default wordlist locations
                    if (settingConfig.InventoryConfiguration.ColoredWordConfiguration != null &&
                        !File.Exists(settingConfig.InventoryConfiguration.ColoredWordConfiguration.WordFile))
                        settingConfig.InventoryConfiguration.ColoredWordConfiguration.WordFile =
                            IOHelper.FindResourceFile(settingConfig.InventoryConfiguration.ColoredWordConfiguration.WordFile, instancePath);
                    // If it contains a path to an order-file that is not leading to a orderlist file try the default orderlist locations
                    if (settingConfig.InventoryConfiguration.FixedInventoryConfiguration != null &&
                        !string.IsNullOrWhiteSpace(settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderFile) &&
                        !File.Exists(settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderFile))
                        settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderFile =
                            IOHelper.FindResourceFile(settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderFile, instancePath);
                    // If it contains a path to an orderLocation-file that is not leading to a  orderLocation-file, try the default orderLocation-file
                    if (settingConfig.InventoryConfiguration.FixedInventoryConfiguration != null &&
                        !string.IsNullOrWhiteSpace(settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile) &&
                        !File.Exists(settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile))
                        settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile =
                            IOHelper.FindResourceFile(settingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderLocationFile, instancePath);
                    // If it contains a path to an simple-item-file that is not leading to a generator config file try the default locations
                    if (settingConfig.InventoryConfiguration.SimpleItemConfiguration != null &&
                        !string.IsNullOrWhiteSpace(settingConfig.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile) &&
                        !File.Exists(settingConfig.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile))
                        settingConfig.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile =
                            IOHelper.FindResourceFile(settingConfig.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile, instancePath);
                }
                var valid = settingConfig.CheckValidityOfPaths(out var errorMessage);
                if (!valid)
                {
                    logAction?.Invoke(errorMessage);
                    return null;
                }
                // Read the control configuration
                logAction?.Invoke("Parsing control config ...");
                using (StreamReader sr = new StreamReader(controlConfigPath))
                    // Deserialize the xml-file
                    controlConfig = (ControlConfiguration)_controlConfigSerializer.Deserialize(sr);
            }
            // --> Init or generate instance
            Instance instance = null;
            if (layoutConfigurationGiven)
            {
                // Read the layout configuration
                logAction?.Invoke("Parsing layout config ...");
                using (StreamReader sr = new StreamReader(instancePath))
                    // Deserialize the xml-file
                    layoutConfig = (LayoutConfiguration)_layoutConfigSerializer.Deserialize(sr);
                // Apply override config, if available
                if (settingConfig != null && settingConfig.OverrideConfig != null)
                    layoutConfig.ApplyOverrideConfig(settingConfig.OverrideConfig);
                // If it contains a path to an map-file that is not leading to a map file try the default orderlist locations
                if (layoutConfig.MapFile != null &&
                    !string.IsNullOrWhiteSpace(layoutConfig.MapFile) &&
                    !File.Exists(layoutConfig.MapFile))
                    layoutConfig.MapFile = IOHelper.FindResourceFile(layoutConfig.MapFile, instancePath);
                // Generate instance
                logAction?.Invoke("Generating instance...");
                instance = InstanceGenerator.GenerateLayout(layoutConfig, new RandomizerSimple(settingConfig.Seed), settingConfig, controlConfig, logAction);
            }
            else
            {
                // Init the instance object
                instance = new Instance();
            }

            // Check whether the config is required
            if (!visualizationOnly)
            {
                // Submit config first to the instance object
                instance.SettingConfig = settingConfig;
                instance.ControllerConfig = controlConfig;
            }
            else
            {
                // Add default config (none required though)
                instance.SettingConfig = new SettingConfiguration();
                instance.SettingConfig.VisualizationOnly = true;
                instance.ControllerConfig = new ControlConfiguration();
            }
            // If a visualization is already present set it to true
            instance.SettingConfig.VisualizationAttached = overrideVisualizationAttached;

            // --> Parse the instance from a file, if no layout was given but a specific instance
            if (!layoutConfigurationGiven)
            {
                // Read the instance
                logAction?.Invoke("Parsing instance ...");
                using (StreamReader sr = new StreamReader(instancePath))
                {
                    // Deserialize the xml-file
                    DTOInstance dtoInstance = (DTOInstance)_instanceSerializer.Deserialize(sr);
                    // Submit the data to an instance object
                    dtoInstance.Submit(instance);
                }
            }

            // Return it
            return instance;
        }

        /// <summary>
        /// Reads a DTO representation of the instance given by the file.
        /// </summary>
        /// <param name="instancePath">The file to read.</param>
        /// <returns>The instance.</returns>
        public static DTOInstance ReadDTOInstance(string instancePath)
        {
            // Init reference
            DTOInstance dtoInstance;
            // Read the instance
            using (StreamReader sr = new StreamReader(instancePath))
            {
                // Deserialize the xml-file
                dtoInstance = (DTOInstance)_instanceSerializer.Deserialize(sr);
            }
            // Return it
            return dtoInstance;
        }
        /// <summary>
        /// Reads an order list from a file.
        /// </summary>
        /// <param name="orderFile">The file.</param>
        /// <param name="instance">The instance to submit to.</param>
        /// <returns>The order list.</returns>
        public static OrderList ReadOrders(string orderFile, Instance instance)
        {
            // Read the list
            OrderList list = null;
            using (StreamReader sr = new StreamReader(orderFile))
            {
                // Deserialize the xml-file
                DTOOrderList dtoConfig = (DTOOrderList)_listSerializer.Deserialize(sr);
                // Submit list to the instance object
                list = dtoConfig.Submit(instance);
            }
            return list;
        }
        /// <summary>
        /// Reads an order list from a csv file.
        /// </summary>
        /// <param name="orderFile">Path to CSV file</param>
        /// <param name="instance">The instance to submit to</param>
        /// <returns>The order list.</returns>
        public static void ReadOrdersFromFile(string orderFile, Instance instance)
        {
            using StreamReader stream = new StreamReader(orderFile);

            if (stream.EndOfStream)
            {
                return;
            }

            var waypointsWithPods = instance.WaypointGraph.GetPodPositions();

            while (!stream.EndOfStream)
            {
                var currentLine = stream.ReadLine().Trim();
                if (currentLine.Length == 0)
                {
                    continue;
                }

                var data = currentLine.Split('|');
                var line = data[0].Split(',');

                Order order = null;

                if (double.TryParse(line[0], out double dummy))
                {
                    if (instance.OrderList == null)
                    {
                        instance.CreateOrderList(ItemType.LocationsList);
                    }

                    order = new Order(instance.GetDropWaypointFromAddress(""));
                    for (var i = 0; i < line.Length; i += 3)
                    {
                        var x = double.Parse(line[i]);
                        var y = double.Parse(line[i + 1]);
                        var pickupDuration = double.Parse(line[i + 2]);
                        var waypoint = waypointsWithPods.Find(waypoint => waypoint.X == x && waypoint.Y == y);
                        if (waypoint == null)
                        {
                            throw new ArgumentException($"location ({x}, {y}) is not valid");
                        }
                        order.AddPosition(new SimpleItemDescription(instance, waypoint), 1, pickupDuration);
                    }
                    instance.OrderList.Orders.Add(order);
                }
                else
                {
                    if (instance.OrderList == null)
                    {
                        instance.CreateOrderList(ItemType.AddressList);
                    }

                    List<string> cleanValues = line.Select(s => s.TrimStart('(', '\'', ' ').TrimEnd(')', '\'', ' ')).ToList();

                    if (instance.SettingConfig.BotsSelfAssist)
                    {
                        CreateOrder(cleanValues, instance, data[1]);
                    }
                    else
                    {
                        List<List<string>> splitOrder = new List<List<string>>();
                        int lastItemInPreviousOrder = -1;
                        for (int i = 0; i < line.Length; i += 2)
                        {
                            if (cleanValues[i + 1].EndsWith(")}"))
                            {
                                splitOrder.Add(cleanValues.GetRange(lastItemInPreviousOrder + 1, i + 1 - lastItemInPreviousOrder));
                                lastItemInPreviousOrder = i + 1;
                            }
                        }

                        if (splitOrder.Count == 0)
                        {
                            CreateOrder(cleanValues, instance, data[1]);
                        }
                        else
                        {
                            foreach (var newOrder in splitOrder)
                            {
                                CreateOrder(newOrder, instance, data[1]);
                            }
                        }
                    }
                }
            }

            // sort the order order (not locations inside orders)
            if (instance.SettingConfig.SortOrders)
            {
                instance.OrderList.Orders.Sort((a, b) =>
                {
                    var aY = a.Positions.Max(p => instance.GetWaypointByID(p.Key.ID).Y);
                    var bY = b.Positions.Max(p => instance.GetWaypointByID(p.Key.ID).Y);
                    var lenA = a.Positions.Count;
                    var lenB = b.Positions.Count;

                    // sort by location count, and then by highest location
                    if (instance.SettingConfig.SortOrdersByLenghtFirst)
                    {
                        if (lenA == lenB)
                        {
                            return (int)(bY - aY);
                        }
                        return lenB - lenA;
                    }
                    // sort by highest location
                    else
                    {
                        if (aY == bY)
                        {
                            return lenB - lenA;
                        }
                        return (int)(bY - aY);
                    }
                });
            }
        }

        private static void CreateOrder(List<string> cleanValues, Instance instance, string outputStandAddress)
        {
            List<Tuple<string, string, int>> valuePairs = new List<Tuple<string, string, int>>();
            int palletID = -1;
            if (!cleanValues[0].StartsWith("{('"))
            {
                ++palletID;
            }
            for (int i = 0; i < cleanValues.Count; i += 2)
            {
                if (cleanValues[i].StartsWith("{('"))
                {
                    ++palletID;
                    cleanValues[i] = cleanValues[i].Remove(0, 3);
                }
                if (cleanValues[i + 1].EndsWith(")}"))
                {
                    cleanValues[i + 1] = cleanValues[i + 1].Remove(cleanValues[i + 1].Length - 2);
                }
                else valuePairs.Add(new Tuple<string, string, int>(cleanValues[i], cleanValues[i + 1], palletID));
            }
            if (valuePairs.Count == 0) return;

            if (instance.SettingConfig.usingMapSortItems)
            {
                valuePairs.Sort(
                    (Tuple<string, string, int> at, Tuple<string, string, int> bt) =>
                {
                    int va = instance.addressToSortOrder[at.Item1], vb = instance.addressToSortOrder[bt.Item1];
                    if (va == vb) return 0;
                    else if (va < vb) return -1;
                    else return 1;
                }
                );
            }

            List<string> allAddresses = valuePairs.Select(pair => pair.Item1).ToList();
            List<double> allTimes = valuePairs.Select(pair => double.Parse(pair.Item2, CultureInfo.InvariantCulture)).ToList();
            List<int> allPalletIDs = valuePairs.Select(pair => pair.Item3).ToList();

            List<string> addresses = new List<string>();
            List<double> times = new List<double>();
            List<int> palletIDs = new List<int>();
            instance.MergeTheSameItems(allAddresses, allTimes, addresses, times, allPalletIDs, palletIDs);

            var dict = addresses.Zip(palletIDs, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);

            Order order = new Order(instance.GetDropWaypointFromAddress(outputStandAddress));
            for (int i = 0; i < addresses.Count; i++)
            {
                var waypoint = instance.GetWaypointFromAddress(addresses[i]);

                if (waypoint == null)
                {
                    throw new ArgumentException($"waypoint with address {addresses[i]} does not exist");
                }

                instance.AddInfoLocationAddress(waypoint.ID, addresses[i]);

                SimpleItemDescription description = new SimpleItemDescription(instance, waypoint);
                // color the order depending on the sector

                description.Hue = instance.GetHueForSector('B');
                description.location = addresses[i];
                description.row = instance.GetRowFromY(waypoint.GetInfoCenterY());
                description.col = instance.GetColFromX(waypoint.GetInfoCenterX());
                // add order
                order.AddPosition(description, 1, times[i], dict[addresses[i]]);
            }
            instance.OrderList.Orders.Add(order);
        }

        /// <summary>
        /// Loads the sector row order from a file
        /// </summary>
        /// <param name="locationSortFile"></param>
        /// <param name="instance"></param>
        public static void ReadOrderLocationInfo(string locationSortFile, Instance instance)
        {
            instance.CreateOrderLocationInfo(locationSortFile);
        }
        /// <summary>
        /// Reads the configuration for a simple item generator instance.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>The configuration.</returns>
        public static SimpleItemGeneratorConfiguration ReadSimpleItemGeneratorConfig(string file, Instance instance)
        {
            // Read the config
            SimpleItemGeneratorConfiguration config = null;
            string searchedPath = IOHelper.FindResourceFile(file, Directory.GetCurrentDirectory());
            using (StreamReader sr = new StreamReader(searchedPath))
                // Deserialize the xml-file
                config = (SimpleItemGeneratorConfiguration)_simpleItemGeneratorConfigSerializer.Deserialize(sr);

            var maxID = instance.PodCount;
            config.ItemDescriptions.RemoveAll(item => item.Key >= maxID);
            config.ItemDescriptionWeights.RemoveAll(item => item.Key >= maxID);
            config.ItemDescriptionBundleSizes.RemoveAll(item => item.Key >= maxID);
            config.ItemWeights.RemoveAll(item => item.Key >= maxID);
            config.ItemCoWeights.RemoveAll(item => item.Key1 >= maxID);

            return config;
        }

        #endregion

        #region Write

        /// <summary>
        /// Writes the instance to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="instance">The instance.</param>
        public static void WriteInstance(string path, Instance instance)
        {
            // Implicitly convert the instance to a DTO and serialize it
            DTOInstance dtoInstance = instance;
            using (TextWriter writer = new StreamWriter(path))
                _instanceSerializer.Serialize(writer, dtoInstance);
        }
        /// <summary>
        /// Writes a DTO instance representation to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="instance">The instance.</param>
        public static void WriteDTOInstance(string path, DTOInstance instance)
        {
            // Serialize it
            using (TextWriter writer = new StreamWriter(path))
                _instanceSerializer.Serialize(writer, instance);
        }
        /// <summary>
        /// Writes the layout configuration to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="config">The layout configuration.</param>
        public static void WriteLayout(string path, LayoutConfiguration config)
        {
            // Serialize it
            using (TextWriter writer = new StreamWriter(path))
                _layoutConfigSerializer.Serialize(writer, config);
        }
        /// <summary>
        /// Writes the setting specification to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="config">The setting specification.</param>
        public static void WriteSetting(string path, SettingConfiguration config)
        {
            // Serialize it
            using (TextWriter writer = new StreamWriter(path))
                _settingConfigSerializer.Serialize(writer, config);
        }
        /// <summary>
        /// Writes the configuration to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="config">The configuration.</param>
        public static void WriteConfiguration(string path, ControlConfiguration config)
        {
            // Serialize it
            using (TextWriter writer = new StreamWriter(path))
                _controlConfigSerializer.Serialize(writer, config);
        }
        /// <summary>
        /// Writes the order list to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="list">The order list.</param>
        public static void WriteOrders(string path, OrderList list)
        {
            // Implicitly convert the instance to a DTO and serialize it
            DTOOrderList dtoList = list;
            using (TextWriter writer = new StreamWriter(path))
                _listSerializer.Serialize(writer, dtoList);
        }
        /// <summary>
        /// Writes a simple item generator configuration to a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="config">The configuration.</param>
        public static void WriteSimpleItemGeneratorConfigFile(string path, SimpleItemGeneratorConfiguration config)
        {
            // Serialize the object to xml and write it to the given path
            using (StreamWriter sw = new StreamWriter(path))
                _simpleItemGeneratorConfigSerializer.Serialize(sw, config);
        }

        #endregion
    }
}
