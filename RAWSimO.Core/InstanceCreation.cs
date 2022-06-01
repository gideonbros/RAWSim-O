using RAWSimO.Core.Configurations;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Items;
using RAWSimO.Core.Waypoints;
using RAWSimO.MultiAgentPathFinding;
using RAWSimO.Toolbox;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using RAWSimO.Core.Management;

namespace RAWSimO.Core
{
    /// THIS PARTIAL CLASS CONTAINS ALL METHODS AND ADDITIONAL FIELDS FOR CREATION OF NEW ELEMENTS OF THE INSTANCE
    /// <summary>
    /// The core element of each simulation instance.
    /// </summary>
    public partial class Instance
    {
        #region Element creation

        #region Instance

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="settingConfig">The configuration for the setting to emulate.</param>
        /// <param name="controlConfig">The configuration for the controllers.</param>
        /// <returns>The newly created instance.</returns>
        public static Instance CreateInstance(SettingConfiguration settingConfig, ControlConfiguration controlConfig)
        {
            Instance instance = new Instance()
            {
                SettingConfig = (settingConfig != null) ? settingConfig : new SettingConfiguration(),
                ControllerConfig = (controlConfig != null) ? controlConfig : new ControlConfiguration(),
            };
            return instance;
        }

        #endregion

        #region ItemDescription

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _itemDescriptionID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterItemDescriptionID()
        {
            if (ItemDescriptions.Any() && _itemDescriptionID <= ItemDescriptions.Max(e => e.ID)) { _itemDescriptionID = ItemDescriptions.Max(e => e.ID) + 1; }
            return _itemDescriptionID++;
        }
        /// <summary>
        /// All volative IDs used for item descriptions so far.
        /// </summary>
        private HashSet<int> _volatileItemDescriptionIDs = new HashSet<int>();
        /// <summary>
        /// Creates an abstract item description for an item of the specified type.
        /// </summary>
        /// <param name="id">The ID of the item description.</param>
        /// <param name="itemType">The type of the item.</param>
        /// <returns>An abstract item description.</returns>
        public ItemDescription CreateItemDescription(int id, ItemType itemType)
        {
            ItemDescription item = null;
            switch (itemType)
            {
                case ItemType.Letter: { item = new ColoredLetterDescription(this); } break;
                case ItemType.SimpleItem: { item = new SimpleItemDescription(this); } break;
                default: throw new ArgumentException("Unknown item type: " + itemType.ToString());
            }
            item.ID = id;
            item.Instance = this;
            ItemDescriptions.Add(item);
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileItemDescriptionIDs.Contains(volatileID)) { volatileID++; }
            item.VolatileID = volatileID;
            _volatileItemDescriptionIDs.Add(item.VolatileID);
            // Maintain actual ID
            if (_idToItemDescription.ContainsKey(item.ID))
                throw new ArgumentException("Already have an item with this ID: " + id);
            _idToItemDescription[item.ID] = item;
            return item;
        }

        #endregion

        #region ItemBundle (and Item)

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _itemBundleID;

        /// <summary>
        /// Creates a bundle of items.
        /// </summary>
        /// <param name="itemDescription">An element describing the characteristics of the item.</param>
        /// <param name="count">The number of items in the bundle.</param>
        /// <returns>A bundle of items.</returns>
        public ItemBundle CreateItemBundle(ItemDescription itemDescription, int count)
        {
            // Create bundle
            ItemBundle bundle = null;
            switch (itemDescription.Type)
            {
                case ItemType.Letter: { bundle = new ColoredLetterBundle(this); } break;
                case ItemType.SimpleItem: { bundle = new SimpleItemBundle(this); } break;
                default: throw new ArgumentException("Unknown item type: " + itemDescription.Type);
            }
            bundle.ID = _itemBundleID++;
            bundle.Instance = this;
            bundle.ItemDescription = itemDescription;
            bundle.ItemCount = count;
            ItemBundles.Add(bundle);
            // Return the filled bundle
            return bundle;
        }

        #endregion

        #region OrderList and AreaInfo

        /// <summary>
        /// Creates a new order list.
        /// </summary>
        /// <param name="itemType">The type of the items in the list.</param>
        /// <returns></returns>
        public OrderList CreateOrderList(ItemType itemType)
        {
            OrderList = new OrderList(itemType);
            return OrderList;
        }
        
        /// <summary>
        /// Reads the file containing sorted sector:row positions and adds 
        /// results as a cost
        /// </summary>
        /// <param name="path"></param>
        public void CreateOrderLocationInfo(string path)
        {
            OrderLocationInfo = new Dictionary<string, Dictionary<int, int>>();
            using (StreamReader sr = new StreamReader(path))
            {
                // get all sectors as keys
                var firstLine = sr.ReadLine().Split(',');
                foreach (var letter in firstLine)
                {
                    OrderLocationInfo.Add(letter, new Dictionary<int, int>());
                }
                int count = 0;
                // go through sector:rows and add cost
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine().Split(' ');
                    for (int i = 0; i < line.Count(); ++i)
                    {
                        var data = line[i].Split(':');
                        OrderLocationInfo[data[0]].Add(int.Parse(data[1]), count++);
                    }
                }
            }
        }

        #endregion

        #region Compound

        /// <summary>
        /// Creates the compound that manages all the tiers.
        /// </summary>
        /// <returns>The newly created compound.</returns>
        public Compound CreateCompound()
        {
            if (Compound != null)
            {
                throw new InvalidOperationException("This instance already contains a compound element.");
            }
            Compound = new Compound(this) { ID = 0 };
            return Compound;
        }

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _tierID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterTierID()
        {
            if (Compound != null && Compound.Tiers.Any() && _tierID <= Compound.Tiers.Max(e => e.ID)) { _tierID = Compound.Tiers.Max(e => e.ID) + 1; }
            return _tierID++;
        }

        /// <summary>
        /// Adds a new tier to the compound.
        /// </summary>
        public Tier CreateTier(int id, double length, double width, double relativePositionX, double relativePositionY, double relativePositionZ)
        {
            if (Compound == null)
                Compound = CreateCompound();
            Tier tier = new Tier(this, length, width)
            {
                ID = id,
                VolatileID = _tierID,
                RelativePositionX = relativePositionX,
                RelativePositionY = relativePositionY,
                RelativePositionZ = relativePositionZ
            };
            Compound.Tiers.Add(tier);
            _idToTier[tier.ID] = tier;
            return tier;
        }

        #endregion

        #region Bot

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _botID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterBotID()
        {
            if (Bots.Any() && _botID <= Bots.Max(e => e.ID)) { _botID = Bots.Max(e => e.ID) + 1; }
            return _botID++;
        }
        /// <summary>
        /// All volative IDs used for bots so far.
        /// </summary>
        private HashSet<int> _volatileBotIDs = new HashSet<int>();
        /// <summary>
        /// Creates a bot with the given characteristics.
        /// </summary>
        /// <param name="id">The ID of the bot.</param>
        /// <param name="tier">The initial position (tier).</param>
        /// <param name="x">The initial position (x-coordinate).</param>
        /// <param name="y">The initial position (y-coordinate).</param>
        /// <param name="radius">The radius of the bot.</param>
        /// <param name="orientation">The initial orientation.</param>
        /// <param name="podTransferTime">The time for picking up and setting down a pod.</param>
        /// <param name="maxAcceleration">The maximal acceleration in m/s^2.</param>
        /// <param name="maxDeceleration">The maximal deceleration in m/s^2.</param>
        /// <param name="maxVelocity">The maximal velocity in m/s.</param>
        /// <param name="turnSpeed">The time it takes the bot to take a full turn in s.</param>
        /// <param name="collisionPenaltyTime">The penalty time for a collision in s.</param>
        /// <returns>The newly created bot.</returns>
        public Bot CreateBot(int id, Tier tier, double x, double y, double radius, double orientation, 
            double podTransferTime, double maxAcceleration, double maxDeceleration, double maxVelocity, double turnSpeed, 
            double collisionPenaltyTime, BotType type, double hue, List<string> zones)
        {
            // Init
            Bot bot = null;
            MovableStation ms = null;
            MateBot mb = null;
            if (type == BotType.MovableStation) {
                ms = new MovableStation(id, this, radius, maxAcceleration, maxDeceleration, maxVelocity, turnSpeed, collisionPenaltyTime, x, y);
                bot = ms;
            }
            else if (type == BotType.MateBot)
            {
                mb = new MateBot(id, this, radius, maxAcceleration, maxDeceleration, maxVelocity, turnSpeed, collisionPenaltyTime, x, y);
                bot = mb;
            }else{
                switch (ControllerConfig.PathPlanningConfig.GetMethodType())
                {
                    case PathPlanningMethodType.Simple:
                        bot = new BotHazard(this, ControllerConfig.PathPlanningConfig as SimplePathPlanningConfiguration);
                        break;
                    case PathPlanningMethodType.Dummy:
                    case PathPlanningMethodType.WHCAvStar:
                    case PathPlanningMethodType.WHCAnStar:
                    case PathPlanningMethodType.FAR:
                    case PathPlanningMethodType.BCP:
                    case PathPlanningMethodType.OD_ID:
                    case PathPlanningMethodType.CBS:
                    case PathPlanningMethodType.PAS:
                        bot = new BotNormal(id, this, radius, podTransferTime, maxAcceleration, maxDeceleration, maxVelocity, turnSpeed, collisionPenaltyTime, x, y);
                        break;
                    default: throw new ArgumentException("Unknown path planning engine: " + ControllerConfig.PathPlanningConfig.GetMethodType());
                }
            }
            
            // Set values
            bot.ID = id;
            bot.Tier = tier;
            bot.Instance = this;
            bot.Radius = radius;
            bot.X = x;
            bot.Y = y;
            bot.PodTransferTime = podTransferTime;
            bot.MaxAcceleration = maxAcceleration;
            bot.MaxDeceleration = maxDeceleration;
            bot.MaxVelocity = maxVelocity;
            bot.TurnSpeed = turnSpeed;
            bot.CollisionPenaltyTime = collisionPenaltyTime;
            bot.Orientation = orientation;
            bot.botHue = hue;
            bot.Zones = zones;

            if (bot is BotHazard)
            {
                ((BotHazard)bot).EvadeDistance = 2.3 * radius;
                ((BotHazard)bot).SetTargetOrientation(orientation);
            }
            // Add bot
            //if MovableStation was created
            if (ms != null)
            {
                ms.Capacity = 1000;
                Bots.Add(ms);
                //bot was referencing only bot-part of movable station and those values were updated
                MovableStations.Add(ms);
            }
            //if MateBot was created
            else if (mb != null)
            {
                bot.Radius *= 0.65;
                Bots.Add(mb);
                MateBots.Add(mb);
            }
            //if NormalBot or BotHazzard were created
            else
            {
                Bots.Add(bot);
            }

            tier.AddBot(bot);
            _idToBots[bot.ID] = bot;
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileBotIDs.Contains(volatileID)) { volatileID++; }
            bot.VolatileID = volatileID;
            _volatileBotIDs.Add(bot.VolatileID);
            // Return it
            return bot;
        }

        #endregion

        #region Pod

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _podID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterPodID()
        {
            if (Pods.Any() && _podID <= Pods.Max(e => e.ID)) { _podID = Pods.Max(e => e.ID) + 1; }
            return _podID++;
        }
        /// <summary>
        /// All volative IDs used for pods so far.
        /// </summary>
        private HashSet<int> _volatilePodIDs = new HashSet<int>();
        /// <summary>
        /// Determines and sets a volatile ID for the given pod. This must be called, if volatile IDs will be used.
        /// </summary>
        /// <param name="pod">The pod to determine the volatile ID for.</param>
        private void SetVolatileIDForPod(Pod pod)
        {
            // Determine volatile ID
            int volatileID = 0;
            while (_volatilePodIDs.Contains(volatileID)) { volatileID++; }
            pod.VolatileID = volatileID;
            _volatilePodIDs.Add(pod.VolatileID);
        }
        /// <summary>
        /// Creates a pod with the given characteristics.
        /// </summary>
        /// <param name="id">The ID of the pod.</param>
        /// <param name="tier">The initial position (tier).</param>
        /// <param name="x">The initial position (x-coordinate).</param>
        /// <param name="y">The initial position (y-coordinate).</param>
        /// <param name="radius">The radius of the pod.</param>
        /// <param name="orientation">The initial orientation of the pod.</param>
        /// <param name="capacity">The capacity of the pod.</param>
        /// <returns>The newly created pod.</returns>
        public Pod CreatePod(int id, Tier tier, double x, double y, double radius, double horizontal_length, double vertical_length, double orientation, double capacity)
        {
            // Consider override values
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverridePodCapacity)
                capacity = SettingConfig.OverrideConfig.OverridePodCapacityValue;
            // Create the pod
            Pod pod = new Pod(this) { ID = id, Tier = tier, Radius = radius, HorizontalLength = horizontal_length, VerticalLength = vertical_length, X = x, Y = y, Orientation = orientation, Capacity = capacity };
            Pods.Add(pod);
            tier.AddPod(pod);
            _idToPods[pod.ID] = pod;
            // Set volatile ID
            SetVolatileIDForPod(pod);
            // Notify listeners
            NewPod(pod);
            // Return it
            return pod;
        }
        /// <summary>
        /// Creates a pod with the given characteristics.
        /// </summary>
        /// <param name="id">The ID of the pod.</param>
        /// <param name="tier">The initial position (tier).</param>
        /// <param name="waypoint">The waypoint to place the pod at.</param>
        /// <param name="radius">The radius of the pod.</param>
        /// <param name="orientation">The initial orientation of the pod.</param>
        /// <param name="capacity">The capacity of the pod.</param>
        /// <returns>The newly created pod.</returns>
        public Pod CreatePod(int id, Tier tier, Waypoint waypoint, double radius, double horizontal_length, double vertical_length, double orientation, double capacity)
        {
            // Consider override values
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverridePodCapacity)
                capacity = SettingConfig.OverrideConfig.OverridePodCapacityValue;
            // Create the pod
            Pod pod = new Pod(this) { Zone = waypoint.Zone, Address = waypoint.Address, ID = id, Tier = tier, Radius = radius, HorizontalLength = horizontal_length, VerticalLength = vertical_length, X = waypoint.X, Y = waypoint.Y, Orientation = orientation, Capacity = capacity, Waypoint = waypoint };
            Pods.Add(pod);
            tier.AddPod(pod);
            _idToPods[pod.ID] = pod;
            // Set volatile ID
            SetVolatileIDForPod(pod);
            // Emulate setdown operation
            WaypointGraph.PodSetdown(pod, waypoint);
            // Notify listeners
            NewPod(pod);
            // Return it
            return pod;
        }

        #endregion

        #region Elevator

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _elevatorID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterElevatorID()
        {
            if (Elevators.Any() && _elevatorID <= Elevators.Max(e => e.ID)) { _elevatorID = Elevators.Max(e => e.ID) + 1; }
            return _elevatorID++;
        }
        /// <summary>
        /// Creates a new elevator.
        /// </summary>
        /// <param name="id">The ID of the elevator.</param>
        /// <returns>The newly created elevator.</returns>
        public Elevator CreateElevator(int id)
        {
            Elevator elevator = new Elevator(this) { ID = id };
            elevator.Queues = new Dictionary<Waypoint, List<Waypoint>>();
            Elevators.Add(elevator);
            _idToElevators[elevator.ID] = elevator;
            return elevator;
        }

        #endregion

        #region InputPalletStand

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _inputPalletStandID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterInputPalletStandID()
        {
            if (InputPalletStands.Any() && _inputPalletStandID <= InputPalletStands.Max(e => e.ID)) { _inputPalletStandID = InputPalletStands.Max(e => e.ID) + 1; }
            return _inputPalletStandID++;
        }
        /// <summary>
        /// All volative IDs used for input pallet stands so far.
        /// </summary>
        private HashSet<int> _volatileInputPalletStandIDs = new HashSet<int>();
        /// <summary>
        /// Creates a new input pallet stand
        /// </summary>
        /// <param name="id">The ID of the input pallet stand.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="x">The position (x-coordinate).</param>
        /// <param name="y">The position (y-coordinate).</param>
        /// <param name="radius">The radius of the stand.</param>
        /// <param name="activationOrderID">The order ID of the stand that defines the sequence in which the stands have to be activated.</param>
        /// <returns>The newly created input station.</returns>
        public InputPalletStand CreateInputPalletStand(int id, Tier tier, double x, double y, double radius, int activationOrderID)
        {
            InputPalletStand palletStand = new InputPalletStand(this)
            { ID = id, Tier = tier, Radius = radius, X = x, Y = y, ActivationOrderID = activationOrderID };
            palletStand.Queues = new Dictionary<Waypoint, List<Waypoint>>();
            InputPalletStands.Add(palletStand);
            InputStations.Add(palletStand);
            tier.AddInputPalletStand(palletStand);
            _idToInputPalletStands[palletStand.ID] = palletStand;
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileInputPalletStandIDs.Contains(volatileID)) { volatileID++; }
            palletStand.VolatileID = volatileID;
            _volatileInputPalletStandIDs.Add(palletStand.VolatileID);
            return palletStand;
        }

        #endregion

        #region OutputPalletStand

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _outputPalletStandID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterOutputPalletStandID()
        {
            if (OutputPalletStands.Any() && _outputPalletStandID <= OutputPalletStands.Max(e => e.ID)) { _outputPalletStandID = OutputPalletStands.Max(e => e.ID) + 1; }
            return _outputPalletStandID++;
        }
        /// <summary>
        /// All volative IDs used for output pallet stands so far.
        /// </summary>
        private HashSet<int> _volatileOutputPalletStandIDs = new HashSet<int>();
        /// <summary>
        /// Creates a new output pallet stand
        /// </summary>
        /// <param name="id">The ID of the output pallet stand.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="x">The position (x-coordinate).</param>
        /// <param name="y">The position (y-coordinate).</param>
        /// <param name="radius">The radius of the stand.</param>
        /// <param name="activationOrderID">The order ID of the stand that defines the sequence in which the stands have to be activated.</param>
        /// <returns>The newly created input station.</returns>
        public OutputPalletStand CreateOutputPalletStand(int id, Tier tier, double x, double y, double radius, int activationOrderID)
        {
            OutputPalletStand palletStand = new OutputPalletStand(this)
            { ID = id, Tier = tier, Radius = radius, X = x, Y = y, ActivationOrderID = activationOrderID };
            palletStand.Queues = new Dictionary<Waypoint, List<Waypoint>>();
            OutputPalletStands.Add(palletStand);
            OutputStations.Add(palletStand);
            tier.AddOutputPalletStand(palletStand);
            _idToOutputPalletStands[palletStand.ID] = palletStand;
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileOutputPalletStandIDs.Contains(volatileID)) { volatileID++; }
            palletStand.VolatileID = volatileID;
            _volatileOutputPalletStandIDs.Add(palletStand.VolatileID);
            return palletStand;
        }

        #endregion

        #region InputStation

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _inputStationID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterInputStationID()
        {
            if (InputStations.Any() && _inputStationID <= InputStations.Max(e => e.ID)) { _inputStationID = InputStations.Max(e => e.ID) + 1; }
            return _inputStationID++;
        }
        /// <summary>
        /// All volative IDs used for input-stations so far.
        /// </summary>
        private HashSet<int> _volatileInputStationIDs = new HashSet<int>();
        /// <summary>
        /// Creates a new input-station.
        /// </summary>
        /// <param name="id">The ID of the input station.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="x">The position (x-coordinate).</param>
        /// <param name="y">The position (y-coordinate).</param>
        /// <param name="radius">The radius of the station.</param>
        /// <param name="capacity">The capacity of the station.</param>
        /// <param name="itemBundleTransfertime">The time it takes to handle one bundle at the station.</param>
        /// <param name="activationOrderID">The order ID of the station that defines the sequence in which the stations have to be activated.</param>
        /// <returns>The newly created input station.</returns>
        public InputStation CreateInputStation(int id, Tier tier, double x, double y, double radius, double capacity, double itemBundleTransfertime, int activationOrderID)
        {
            // Consider override values
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverrideInputStationCapacity)
                capacity = SettingConfig.OverrideConfig.OverrideInputStationCapacityValue;
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverrideInputStationItemBundleTransferTime)
                itemBundleTransfertime = SettingConfig.OverrideConfig.OverrideInputStationItemBundleTransferTimeValue;
            // Init
            InputStation inputStation = new InputStation(this)
            { ID = id, Tier = tier, Radius = radius, X = x, Y = y, Capacity = capacity, ItemBundleTransferTime = itemBundleTransfertime, ActivationOrderID = activationOrderID };
            inputStation.Queues = new Dictionary<Waypoint, List<Waypoint>>();
            InputStations.Add(inputStation);
            tier.AddInputStation(inputStation);
            _idToInputStations[inputStation.ID] = inputStation;
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileInputStationIDs.Contains(volatileID)) { volatileID++; }
            inputStation.VolatileID = volatileID;
            _volatileInputStationIDs.Add(inputStation.VolatileID);
            return inputStation;
        }

        #endregion

        #region OutputStation

        /// <summary>
        /// Current ID to identify the corresponding instance element.
        /// </summary>
        private int _outputStationID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterOutputStationID()
        {
            if (OutputStations.Any() && _outputStationID <= OutputStations.Max(e => e.ID)) { _outputStationID = OutputStations.Max(e => e.ID) + 1; }
            return _outputStationID++;
        }
        /// <summary>
        /// All volative IDs used for output-stations so far.
        /// </summary>
        private HashSet<int> _volatileOutputStationIDs = new HashSet<int>();
        /// <summary>
        /// Creates a new output-station.
        /// </summary>
        /// <param name="id">The ID of the input station.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="x">The position (x-coordinate).</param>
        /// <param name="y">The position (y-coordinate).</param>
        /// <param name="radius">The radius of the station.</param>
        /// <param name="capacity">The capacity of the station.</param>
        /// <param name="itemTransferTime">The time it takes to handle one item at the station.</param>
        /// <param name="itemPickTime">The time it takes to pick the item from a pod (excluding other handling times).</param>
        /// <param name="activationOrderID">The order ID of the station that defines the sequence in which the stations have to be activated.</param>
        /// <returns>The newly created output station.</returns>
        public OutputStation CreateOutputStation(int id, Tier tier, double x, double y, double radius, int capacity, double itemTransferTime, double itemPickTime, int activationOrderID)
        {
            // Consider override values
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverrideOutputStationCapacity)
                capacity = SettingConfig.OverrideConfig.OverrideOutputStationCapacityValue;
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverrideOutputStationItemPickTime)
                itemPickTime = SettingConfig.OverrideConfig.OverrideOutputStationItemPickTimeValue;
            if (SettingConfig.OverrideConfig != null && SettingConfig.OverrideConfig.OverrideOutputStationItemTransferTime)
                itemTransferTime = SettingConfig.OverrideConfig.OverrideOutputStationItemTransferTimeValue;
            // Init
            OutputStation outputStation = new OutputStation(this)
            { ID = id, Radius = radius, X = x, Y = y, Capacity = capacity, ItemTransferTime = itemTransferTime, ItemPickTime = itemPickTime, ActivationOrderID = activationOrderID };
            outputStation.Queues = new Dictionary<Waypoint, List<Waypoint>>();
            OutputStations.Add(outputStation);
            tier.AddOutputStation(outputStation);
            _idToOutputStations[outputStation.ID] = outputStation;
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileOutputStationIDs.Contains(volatileID)) { volatileID++; }
            outputStation.VolatileID = volatileID;
            _volatileOutputStationIDs.Add(outputStation.VolatileID);
            return outputStation;
        }

        #endregion

        #region Waypoints

        private int _waypointID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterWaypointID()
        {
            if (Waypoints.Any() && _waypointID <= Waypoints.Max(e => e.ID)) { _waypointID = Waypoints.Max(e => e.ID) + 1; }
            return _waypointID++;
        }
        /// <summary>
        /// All volative IDs used for waypoints so far.
        /// </summary>
        private HashSet<int> _volatileWaypointIDs = new HashSet<int>();
        /// <summary>
        /// Determines and sets a volatile ID for the given waypoint. This must be called, if volatile IDs will be used.
        /// </summary>
        /// <param name="waypoint">The waypoint to determine the volatile ID for.</param>
        private void SetVolatileIDForWaypoint(Waypoint waypoint)
        {
            // Determine volatile ID
            int volatileID = 0;
            while (_volatileWaypointIDs.Contains(volatileID)) { volatileID++; }
            waypoint.VolatileID = volatileID;
            _volatileWaypointIDs.Add(waypoint.VolatileID);
        }

        /// <summary>
        /// Returns X,Y coordinates for row,col of the waypoint
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        public void RowColToXY(int row, int col, out double X, out double Y)
        {
            if (row >= MapRowCount || row < 0 || col >= MapColumnCount || col < 0)
            {
                X = double.NaN;
                Y = double.NaN;
                throw new ArgumentException("Invalid (row,col): " + row + ", " + col);
            }
            //X = lc.HorizontalWaypointDistance * col;
            //Y = lc.VerticalWaypointDistance * row;
            X = col * layoutConfiguration.HorizontalWaypointDistance + layoutConfiguration.HorizontalWaypointDistance / 2;
            Y = row * layoutConfiguration.VerticalWaypointDistance + layoutConfiguration.VerticalWaypointDistance / 2;
        }

        /// <summary>
        /// Returns row,col coordinates for X,Y of the waypoint
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        public bool XYToRowCol(double X, double Y, out int row, out int col)
        {
            X -= layoutConfiguration.HorizontalWaypointDistance / 2;
            Y -= layoutConfiguration.VerticalWaypointDistance / 2;
            if (X > MapHorizontalLength || X < 0.0 || (decimal)X % (decimal)layoutConfiguration.HorizontalWaypointDistance != (decimal)0.0 || 
                Y > MapVerticalLength   || Y < 0.0 || (decimal)Y % (decimal)layoutConfiguration.VerticalWaypointDistance != (decimal)0.0 )
            {
                row = -1;
                col = -1;
                throw new ArgumentException("Invalid (X,Y): " + X + ", " + Y);
            }
            row = (int)Math.Round(Y / layoutConfiguration.VerticalWaypointDistance);
            col = (int) Math.Round(X / layoutConfiguration.HorizontalWaypointDistance);

            return true;
        }

        /// <summary>
        /// Find the waypoint exactly at X,Y or closest to it
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        /// <returns></returns>
        public Waypoint FindWpFromXY(double X, double Y)
        {
            // find X,Y of the closest waypoint
            X = WaypointXs.BinaryFindClosest(X);
            Y = WaypointYs.BinaryFindClosest(Y);
            var wp = Waypoints_dict[X][Y];
            return wp;
        }

        /// <summary>
        /// Creates a new waypoint that serves as the handover point for an input station.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="station">The station.</param>
        /// <param name="isQueueWaypoint">Indicates whether this waypoint is also a queue waypoint.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, InputStation station, bool isQueueWaypoint)
        {
            Waypoint wp = new Waypoint(this) { ID = id, X = station.X, Y = station.Y, Radius = station.Radius, InputStation = station, IsQueueWaypoint = isQueueWaypoint };
            station.Waypoint = wp;
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates a new waypoint that serves as the handover point for an input pallet stand.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="stand">The pallet stand.</param>
        /// <param name="isQueueWaypoint">Indicates whether this waypoint is also a queue waypoint.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, InputPalletStand stand, bool isQueueWaypoint)
        {
            Waypoint wp = new Waypoint(this) { ID = id, X = stand.X, Y = stand.Y, Radius = stand.Radius, InputPalletStand = stand, IsQueueWaypoint = isQueueWaypoint };
            stand.Waypoint = wp;
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates a new waypoint that serves as the handover point for an output station.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="station">The station.</param>
        /// <param name="isQueueWaypoint">Indicates whether this waypoint is also a queue waypoint.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, OutputStation station, bool isQueueWaypoint)
        {
            Waypoint wp = new Waypoint(this) { ID = id, X = station.X, Y = station.Y, Radius = station.Radius, OutputStation = station, IsQueueWaypoint = isQueueWaypoint };
            station.Waypoint = wp;
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates a new waypoint that serves as the handover point for an output pallet stand.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="station">The station.</param>
        /// <param name="isQueueWaypoint">Indicates whether this waypoint is also a queue waypoint.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, OutputPalletStand stand, bool isQueueWaypoint)
        {
            Waypoint wp = new Waypoint(this) { ID = id, X = stand.X, Y = stand.Y, Radius = stand.Radius, OutputPalletStand = stand, IsQueueWaypoint = isQueueWaypoint };
            stand.Waypoint = wp;
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates a new waypoint that serves as the handover point for an elevator.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="x">The position (x-coordinate).</param>
        /// <param name="y">The position (y-coordinate).</param>
        /// <param name="elevator">The elevator.</param>
        /// <param name="isQueueWaypoint">Indicates whether this waypoint is also a queue waypoint.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, Elevator elevator, double x, double y, bool isQueueWaypoint)
        {
            Waypoint wp = new Waypoint(this) { ID = id, X = x, Y = y, Elevator = elevator, IsQueueWaypoint = isQueueWaypoint };
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates a new waypoint that serves as a storage location.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="pod">The pod currently stored at it.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, Pod pod)
        {
            Waypoint wp = new Waypoint(this) { ID = id, X = pod.X, Y = pod.Y, Radius = pod.Radius, PodStorageLocation = true, Pod = pod };
            pod.Waypoint = wp;
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates a typical waypoint.
        /// </summary>
        /// <param name="id">The ID of the waypoint.</param>
        /// <param name="tier">The position (tier).</param>
        /// <param name="x">The position (x-coordinate).</param>
        /// <param name="y">The position (y-coordinate).</param>
        /// <param name="podStorageLocation">Indicates whether the waypoint serves as a storage location.</param>
        /// <param name="isQueueWaypoint">Indicates whether the waypoint belongs to a queue.</param>
        /// <returns>The newly created waypoint.</returns>
        public Waypoint CreateWaypoint(int id, Tier tier, double x, double y, bool podStorageLocation, bool isQueueWaypoint)
        {
            Waypoint wp = new Waypoint(this) { ID = id, Tier = tier, X = x, Y = y, PodStorageLocation = podStorageLocation, IsQueueWaypoint = isQueueWaypoint };
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates waypoint with zone.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tier"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="podStorageLocation"></param>
        /// <param name="isQueueWaypoint"></param>
        /// <param name="zone"></param>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        public Waypoint CreateWaypoint(int id, Tier tier, double x, double y, bool podStorageLocation, bool isQueueWaypoint, string zone)
        {
            Waypoint wp = new Waypoint(this) { ID = id, Tier = tier, X = x, Y = y, PodStorageLocation = podStorageLocation, IsQueueWaypoint = isQueueWaypoint, Zone = zone};
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }
        /// <summary>
        /// Creates waypoint with address and zone.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tier"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="podStorageLocation"></param>
        /// <param name="isQueueWaypoint"></param>
        /// <param name="address"></param>
        /// <param name="zone"></param>
        /// <returns></returns>
        public Waypoint CreateWaypoint(int id, Tier tier, double x, double y, bool podStorageLocation, bool isQueueWaypoint, string address, string zone)
        {
            Waypoint wp = new Waypoint(this) { ID = id, Tier = tier, X = x, Y = y, PodStorageLocation = podStorageLocation, IsQueueWaypoint = isQueueWaypoint, Address = address, Zone = zone };
            tier.AddWaypoint(wp);
            Waypoints.Add(wp);
            WaypointGraph.Add(wp);
            _idToWaypoint[wp.ID] = wp;
            // Set volatile ID
            SetVolatileIDForWaypoint(wp);
            // Return
            return wp;
        }

        #endregion

        #region Semaphores

        private int _semaphoreID;
        /// <summary>
        /// Registers and returns a new ID for an object of the given type.
        /// </summary>
        /// <returns>A new unique ID that can be used to identify the object.</returns>
        public int RegisterSemaphoreID()
        {
            if (Semaphores.Any() && _semaphoreID <= Semaphores.Max(e => e.ID)) { _semaphoreID = Semaphores.Max(e => e.ID) + 1; }
            return _semaphoreID++;
        }
        /// <summary>
        /// Creates a new semaphore.
        /// </summary>
        /// <param name="id">The ID of the semaphore.</param>
        /// <param name="maximalCount">The maximal number of bots in the managed area.</param>
        /// <returns>The newly created semaphore.</returns>
        public QueueSemaphore CreateSemaphore(int id, int maximalCount)
        {
            QueueSemaphore semaphore = new QueueSemaphore(this, maximalCount) { ID = id };
            Semaphores.Add(semaphore);
            _idToSemaphore[semaphore.ID] = semaphore;
            return semaphore;
        }

        #endregion

        #endregion

        #region Finalizing

        /// <summary>
        /// Finalizes the instance.
        /// </summary>
        public void Flush()
        {
            // Set references to this object
            Compound.Instance = this;
            foreach (var instanceElement in
                Bots.AsEnumerable<InstanceElement>()
                .Concat(Pods.AsEnumerable<InstanceElement>())
                .Concat(Elevators.AsEnumerable<InstanceElement>())
                .Concat(InputStations.AsEnumerable<InstanceElement>())
                .Concat(OutputStations.AsEnumerable<InstanceElement>())
                .Concat(Waypoints.AsEnumerable<InstanceElement>())
                .Concat(ItemDescriptions.AsEnumerable<InstanceElement>())
                .Concat(ItemBundles.AsEnumerable<InstanceElement>()))
            {
                instanceElement.Instance = this;
            }
        }

        #endregion

        #region Late initialization hooks

        /// <summary>
        /// The event handler for the event that is raised when the simulation is just before starting and after all other managers were initialized.
        /// </summary>
        public delegate void LateInitEventHandler();
        /// <summary>
        /// The event that is raised when the simulation is just before starting and after all other managers were initialized.
        /// </summary>
        public event LateInitEventHandler LateInit;
        /// <summary>
        /// Notifies the instance that all previous initializations are done (all managers are available) and we are almost ready to start the simulation updates.
        /// </summary>
        internal void LateInitialize()
        {
            // Call all subscribers
            LateInit?.Invoke();

            if (SettingConfig.BotLocations == BotLocations.Random)
            {
                var writer = new StreamWriter($"{this.CreatedAtString}.bots");
                foreach (var bot in MovableStations)
                {
                    writer.WriteLine($"{bot.X},{bot.Y},{bot.Orientation}");
                }
                foreach (var bot in MateBots)
                {
                    writer.WriteLine($"{bot.X},{bot.Y},{bot.Orientation}");
                }
                writer.Close();
            }

            if (SettingConfig.InventoryConfiguration.OrderMode == OrderMode.Fill)
            {
                var writer = new StreamWriter($"{this.CreatedAtString}.orders");
                foreach (var order in ItemManager.AvailableOrders)
                {
                    bool isFirstItem = true;
                    foreach (var position in order.Positions)
                    {
                        var separator = isFirstItem ? "" : ",";

                        var item = position.Key;
                        var waypoint = GetWaypointByID(item.ID);
                        var pickupDuration = order.Times[item];

                        writer.Write($"{separator}{waypoint.X},{waypoint.Y},{pickupDuration}");

                        isFirstItem = false;
                    }
                    writer.WriteLine();
                }
                writer.Close();
            }

            {
                var writer = new StreamWriter($"{this.CreatedAtString}.seed");
                writer.WriteLine(this.Randomizer.Seed());
                writer.Close();
            }
        }

        #endregion
    }
}
