using RAWSimO.Core.IO;
using RAWSimO.Core.Management;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace RAWSimO.Core.Configurations
{
    /// <summary>
    /// The base configuration.
    /// </summary>
    public class SettingConfiguration
    {
        #region Naming

        /// <summary>
        /// A name identifying the configuration.
        /// </summary>
        public string Name = "default";
        /// <summary>
        /// Creates a name for the setting supplying basic information.
        /// </summary>
        /// <returns>The name of the scenario.</returns>
        public string GetMetaInfoBasedConfigName()
        {
            string name = "";
            if (InventoryConfiguration != null)
            {
                // Add item type and order mode
                name += InventoryConfiguration.ItemType.ToString() + "-" + InventoryConfiguration.OrderMode.ToString();
                // Add further info depending on mode
                switch (InventoryConfiguration.OrderMode)
                {
                    case OrderMode.Fill:
                        if (InventoryConfiguration.DemandInventoryConfiguration != null)
                            name += "-" + InventoryConfiguration.DemandInventoryConfiguration.BundleCount + "-" + InventoryConfiguration.DemandInventoryConfiguration.OrderCount;
                        break;
                    case OrderMode.Poisson:
                        if (InventoryConfiguration.PoissonInventoryConfiguration != null)
                            name += "-" + InventoryConfiguration.PoissonInventoryConfiguration.PoissonMode;
                        break;
                    case OrderMode.Fixed:
                        break;
                    default:
                        break;
                }
            }
            // Add the simulation time
            name += (string.IsNullOrWhiteSpace(name) ? "" : "-") + Math.Round(SimulationDuration).ToString(IOConstants.FORMATTER);
            // Return it
            return name;
        }

        #endregion

        #region Basic parameters

        /// <summary>
        /// Both picker and robot come to the same location to picke the item
        /// </summary>
        public bool SameAssistLocation = true;

        /// <summary>
        /// Enables picking position queuing through the location manager
        /// </summary>
        public bool UsingLocationManager = true;

        /// <summary>
        /// New sort using the excel file
        /// </summary>
        public bool usingMapSortItems = true;

        /// <summary>
        /// Indicates whether Reserve same assist location will be enabled
        /// </summary>
        public bool ReserveSameAssistLocation = false;
        /// <summary>
        /// Indicates whether Reserve assist locations for mates will be enabled
        /// </summary>
        public bool ReserveNextAssistLocation = false;

        /// <summary>
        /// Cost value of neutral directions
        /// </summary>
        public double NeutralCost = 1.0;

        /// <summary>
        /// Cost value of preferred directions
        /// </summary>
        public double PreferredCost = 0.1;

        /// <summary>
        /// Cost value of unpreferred directions
        /// </summary>
        public double UnpreferredCost = 10;

        /// <summary>
        /// Rate at which simulation step is executed.
        /// </summary>
        public double SimulationUpdateRate = 50; // In Hz.

        /// <summary>
        /// The warmup-time for the simulation in seconds.
        /// </summary>
        public double SimulationWarmupTime = 0;

        /// <summary>
        /// The duration of the simulation in seconds.
        /// </summary>
        // 2 days
        //public double SimulationDuration = 172800;
        // 40 days
        public double SimulationDuration = 172800 * 20;

        /// <summary>
        /// The duration in seconds of the thouput interval used in statistics
        /// </summary>
        public double StatThroughputInterval = 3600;

        /// <summary>
        /// The random seed to use.
        /// </summary>
        public int Seed = 0;

        /// <summary>
        /// Path to statisics summary .xlsx file
        /// </summary>
        public string StatisticsSummaryFile = "StatisticsSummary.xlsx";

        /// <summary>
        /// Indicates usage of constant assist duration regardless of the item.
        /// The duration is given below.
        /// </summary>
        public bool UseConstantAssistDuration = false;

        /// <summary>
        /// Time it takes for MateBot to assist
        /// </summary>
        public double AssistDuration = 10;

        /// <summary>
        /// Time it takes to switch pallets
        /// </summary>
        public double SwitchPalletDuration = 20;

        /// <summary>
        /// Breaking condition
        /// </summary>
        public int OrderCountStopCondition = -1;

        /// <summary>
        /// Bool which indicates that OrderCountStopCondotion
        /// </summary>
        public bool ManualOrderCountStopCondition { get; set; } = false;

        /// <summary>
        /// No. items mate scheduler will search into the future
        /// </summary>
        public int MateSchedulerPredictionDepth = 1;

        /// <summary>
        /// No. times matebot objects are allowed to switch assist locations
        /// </summary>
        public int MaxNumberOfMateSwitches = 3;

        /// <summary>
        /// For how many seconds does the new location have to be quicker in order for MateScheduler to order switching
        /// </summary>
        public double MateSwitchingThreshold = 30;

        /// <summary>
        /// Path to a file where MateScheduler will log it's data
        /// </summary>
        // C:\Users\Rene Marusevec\Documents\raw_fleet_simulator\logger.txt
        public string MateSchedulerLoggerPath = @"";
        public string LocationManagerLoggerPath = @""; 
        public string TempLoggerPath = @""; 

        /// <summary>
        /// The log level used to filter output messages.
        /// </summary>
        public LogLevel LogLevel = LogLevel.Info;

        /// <summary>
        /// The log level that indicates which output files will be written.
        /// </summary>
        public LogFileLevel LogFileLevel = LogFileLevel.All;

        /// <summary>
        /// Indicates how are the bot locations chosen.
        /// </summary>
        public BotLocations BotLocations = BotLocations.Fixed;

        /// <summary>
        /// File containing bot locations.
        /// If empty, Moveable Stations will be spawned in parking lots.
        /// </summary>
        public string LocationsFile = "";

        /// <summary>
        /// Indicates whether MovableStations are performing assisting by themselves
        /// </summary>
        public bool BotsSelfAssist = false;

        /// <summary>
        /// If true the bots can drive through each other and perform path planning individually
        /// </summary>
        public bool DimensionlessBots = false;

        /// <summary>
        /// Inidcates that Mate Scheduler will follow SeeOff strategy
        /// </summary>
        public bool SeeOffMateScheduling = false;

        /// <summary>
        /// Indicate whether Hungarian scheduling will be used
        /// </summary>
        public bool HungarianMateScheduling = false;

        /// <summary>
        /// Indicates whether zone picking will be enabled
        /// </summary>
        public bool ZonesEnabled = false;
        
        /// <summary>
        /// Indicates whether Wave filter will be enabled
        /// </summary>
        public bool WaveEnabled = false;
        
        /// <summary>
        /// How many rows should the upper bound of the wave be removed from the bottom + MaxWaveHight
        /// </summary>
        public int waveAuxiliaryStoppingShift = 3;

        /// <summary>
        /// Indicates height of Wave filter in number of rows
        /// </summary>
        public int WaveHeight = 5;
        /// <summary>
        /// Indicates maximum height of the waves in number of rows
        /// </summary>
        public int MaxWaveHeight = 13;
        /// <summary>
        /// Indicates that the simulator will detect events of two bots passing by each other
        /// </summary>
        public bool DetectPassByEvents = false;

        /// <summary>
        /// Indicates whether well-sortedness will be tracked (computationally intense).
        /// </summary>
        public bool MonitorWellSortedness = false;

        /// <summary>
        /// Indicates whether all fixed orders have been completed
        /// </summary>
        public bool StopCondition { get; set; } = false;


        /// <summary>
        /// Indicates whether fix orders will be sorted
        /// </summary
        public bool SortOrders = false;
		/// <summary>
        /// Indicates whether fix orders will be sorted by length first and then by location
        /// </summary
        public bool SortOrdersByLenghtFirst = false;

        /// <summary>
        /// Indicates the current debug mode.
        /// </summary>
        public DebugMode DebugMode = DebugMode.RealTimeAndMemory;
        /// <summary>
        /// Numerical tolerance
        /// </summary>
        public readonly double NumericalTolerance = 0.0001;

        #endregion

        #region Movement related parameters

        /// <summary>
        /// Distance between a pod and a station which is considered close enough.
        /// </summary>
        public double Tolerance = 0.2;

        /// <summary>
        /// Indicates whether to simulate the acceleration or use top-speed instantly.
        /// </summary>
        public bool UseAcceleration = false;

        /// <summary>
        /// Indicates whether to simulate the rotation or use oritaion instantly.
        /// </summary>
        public bool UseTurnDelay = false;

        /// <summary>
        /// Indicates whether to rotate pods while the bot is rotating. This actually only results in different visual feedback.
        /// </summary>
        public bool RotatePods = false;

        /// <summary>
        /// Indicates whether to use or ignore queues in the waypoint-system.
        /// </summary>
        public bool QueueHandlingEnabled = true;

        #endregion

        #region Entity related parameters

        /// <summary>
        /// The idle-time after which a station is considered resting / shutdown.
        /// </summary>
        public double StationShutdownThresholdTime = 600;

        #endregion

        #region Statistics related

        /// <summary>
        /// Enables / disables the tracking of correlative frequencies between item descriptions.
        /// </summary>
        public bool CorrelativeFrequencyTracking = false;
        /// <summary>
        /// Indicates whether locations of the robots are polled alot more frequently in order to get more precise statistical feedback (note: this may cause huge output files).
        /// </summary>
        public bool IntenseLocationPolling = false;

        #endregion

        #region Inventory related parameters

        /// <summary>
        /// All configuration settings for the generation or input of inventory.
        /// </summary>
        public InventoryConfiguration InventoryConfiguration = new InventoryConfiguration();

        #endregion

        #region Override parameters

        /// <summary>
        /// Exposes values that override and replace others given by the instance file.
        /// </summary>
        public OverrideConfiguration OverrideConfig;

        #endregion

        #region Comment tags

        /// <summary>
        /// Some optional comment tag that will be written to the footprint.
        /// </summary>
        public string CommentTag1 = "";
        /// <summary>
        /// Some optional comment tag that will be written to the footprint.
        /// </summary>
        public string CommentTag2 = "";
        /// <summary>
        /// Some optional comment tag that will be written to the footprint.
        /// </summary>
        public string CommentTag3 = "";

        #endregion

        #region Live parameters

        /// <summary>
        /// The heat mode to use when visualizing the simulation.
        /// </summary>
        [Live]
        [XmlIgnore]
        public HeatMode HeatMode;

        /// <summary>
        /// The action that is called when something is written to the output.
        /// </summary>
        [Live]
        [XmlIgnore]
        public Action<string> LogAction;

        /// <summary>
        /// The timestamp of the start of the execution.
        /// </summary>
        [Live]
        [XmlIgnore]
        public DateTime StartTime;

        /// <summary>
        /// The timestamp of the finish of the execution.
        /// </summary>
        [Live]
        [XmlIgnore]
        public DateTime StopTime;

        /// <summary>
        /// The directory to write all statistics file to.
        /// </summary>
        [Live]
        [XmlIgnore]
        public string StatisticsDirectory;

        /// <summary>
        /// Indicates whether a visualization is attached.
        /// </summary>
        [Live]
        [XmlIgnore]
        public bool VisualizationAttached;

        /// <summary>
        /// Indicates that the instance will only be drawn and can not be executed.
        /// </summary>
        [Live]
        [XmlIgnore]
        public bool VisualizationOnly;

        /// <summary>
        /// Real world commands will be printed
        /// </summary>
        [Live]
        [XmlIgnore]
        public bool RealWorldIntegrationCommandOutput = false;

        /// <summary>
        /// Real world events determine the end of a state
        /// </summary>
        [Live]
        [XmlIgnore]
        public bool RealWorldIntegrationEventDriven = false;

        #endregion

        #region Self check
        public bool CheckValidityOfPaths(out string message )
        {
            StringBuilder errorMessage = new StringBuilder("Error:\n");
            bool success = true;

            //check statistics summary file path
            if (!string.IsNullOrEmpty(StatisticsSummaryFile))
            {
                var StatisticsSummaryDirectory = Path.GetDirectoryName(StatisticsSummaryFile);
                if (!string.IsNullOrEmpty(StatisticsSummaryDirectory) && !Directory.Exists(StatisticsSummaryDirectory))
                {
                    errorMessage.Append("Directory " + StatisticsSummaryDirectory + " does not exist!\n");
                    success = false;
                }
                if (string.IsNullOrEmpty(StatisticsSummaryDirectory))
                {
                    StatisticsSummaryDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "..", "..", "..", "statistics");
                    StatisticsSummaryFile = Path.Combine(StatisticsSummaryDirectory, StatisticsSummaryFile);
                }
                if (Path.GetExtension(StatisticsSummaryFile) != ".xlsx")
                {
                    errorMessage.Append(StatisticsSummaryFile + " is not an .xlsx file!\n");
                    success = false;
                }
            }

            //check Mate scheduler logger
            if(!string.IsNullOrEmpty(MateSchedulerLoggerPath))
            {
                var MateSchedulerLoggerDirectory = Path.GetDirectoryName(MateSchedulerLoggerPath);
                if (!string.IsNullOrEmpty(MateSchedulerLoggerDirectory) && !Directory.Exists(MateSchedulerLoggerDirectory))
                {
                    errorMessage.Append("Directory " + MateSchedulerLoggerDirectory + " does not exist!\n");
                    success = false;
                }
                if (Path.GetExtension(MateSchedulerLoggerPath) != ".txt")
                {
                    errorMessage.Append(MateSchedulerLoggerPath + " is not an .txt file!\n");
                    success = false;
                }
            }
            
            //create return message
            message = errorMessage.ToString();
            return success;
        }
        #endregion
    }
}
