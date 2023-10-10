using RAWSimO.Core.Bots;
using RAWSimO.Core.Control;
using RAWSimO.Core.Info;
using RAWSimO.Core.IO;
using RAWSimO.Core.Statistics;
using RAWSimO.Visualization.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSimO.Visualization.Rendering
{
    /// <summary>
    /// Supplies a number of settings for generating heatmaps.
    /// </summary>
    public class HeatMapRendererConfiguration
    {
        /// <summary>
        /// The canvas to draw on.
        /// </summary>
        public Canvas ContentControl { get; set; }
        /// <summary>
        /// The control holding the canvas.
        /// </summary>
        public Grid ContentHost { get; set; }
        /// <summary>
        /// An action to log output to.
        /// </summary>
        public Action<string> Logger { get; set; }
        /// <summary>
        /// An action that is invoked as soon as rendering is completed.
        /// </summary>
        public Action FinishedCallback { get; set; }
        /// <summary>
        /// The tier for which the heatmap shall be rendered.
        /// </summary>
        public ITierInfo Tier { get; set; }
        /// <summary>
        /// Only snapshots belonging to the tasks contained in this hashset will be considered.
        /// </summary>
        public HashSet<BotTaskType> BotTaskFilter { get; set; } = new HashSet<BotTaskType>(Enum.GetValues(typeof(BotTaskType)).Cast<BotTaskType>());
        /// <summary>
        /// Only snapshots belonging to the states contained in this hashset will be considered.
        /// </summary>
        public HashSet<BotStateType> BotStateFilter{ get; set; } = new HashSet<BotStateType>(Enum.GetValues(typeof(BotStateType)).Cast<BotStateType>());
        /// <summary>
        /// Should the congestion (slow movement) be visualized.
        /// </summary>
        public bool VisualizeCongestion { get; set; }
        /// <summary>
        /// Should tasks and states be visualized.
        /// </summary>
        public bool VisualizeTasksAndStates { get; set; }
        /// <summary>
        /// Should picked items be visualized.
        /// </summary>
        public bool VisualizePickedItems { get; set; }
        /// <summary>
        /// Should orders be visualized.
        /// </summary>
        public bool VisualizeOrders { get; set; }
        /// <summary>
        /// Lower bound velocity threshold.
        /// </summary>
        public double MinVelocityThreshold { get; set; }
        /// <summary>
        /// Upper bound velocity threshold.
        /// </summary>
        public double MaxVelocityThreshold { get; set; }
        /// <summary>
        /// Indicates whether the heatmap will be rendered beneath all other visual objects.
        /// </summary>
        public bool DrawInBackground { get; set; }
        /// <summary>
        /// Indicates whether heat-values will be scaled logarithmically.
        /// </summary>
        public bool Logarithmic { get; set; }
        /// <summary>
        /// Indicates whether to save legend.
        /// </summary>
        public bool SaveLegend { get; set; }
        /// <summary>
        /// Indicates whether bichromatic coloring will be used instead of the default one.
        /// </summary>
        public bool BichromaticColoring { get; set; }
        /// <summary>
        /// The first color used for bichromatic coloring.
        /// </summary>
        public Color BichromaticColorOne { get; set; }
        /// <summary>
        /// The second color used for bichromatic coloring.
        /// </summary>
        public Color BichromaticColorTwo { get; set; }
        /// <summary>
        /// The file storing the heat data.
        /// </summary>
        public string DataFile { get; set; }
        /// <summary>
        /// Where to save legend.
        /// </summary>
        public string DirectorySaveLocation { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// The index of the heat sub data to visualize.
        /// </summary>
        public int DataIndex { get; set; }
        /// <summary>
        /// This is used to override heat data usage. If not null initial bot positions indicated by the instance will be used instead.
        /// </summary>
        public List<Tuple<int, double, double>> InitialBotPositions { get; set; }
        /// <summary>
        /// Defines the lower bound for filtering datapoints regarding their time stamp (is only used when the data hast time-correspondence).
        /// </summary>
        public double DataTimeFilterLow { get; set; }
        /// <summary>
        /// Defines the upper bound for filtering datapoints regarding their time stamp (is only used when the data hast time-correspondence).
        /// </summary>
        public double DataTimeFilterHigh { get; set; }
    }

    /// <summary>
    /// Exposes functionality to render heatmaps.
    /// </summary>
    public class HeatMapRenderer
    {
        /// <summary>
        /// Creates a new renderer.
        /// </summary>
        /// <param name="config">Contains all necessary information.</param>
        /// <param name="instance">The instance.</param>
        /// <param name="radiusMultiplier">Radius multiplier for changing the size of marker used for visualization.</param>
        public HeatMapRenderer(HeatMapRendererConfiguration config, Core.Instance instance, double radiusMultiplier = 1)
        {
            if (instance == null) throw new ArgumentNullException("Instance is not initalized.");

            _instance = instance;           
            _heatmap = new double[instance.MapColumnCount, instance.MapRowCount];
            _config = config;
            _dataFile = config.DataFile;
            _tier = config.Tier;
            _contentControl = config.ContentControl;
            _transformer = new Transformation2D(_heatmap.GetLength(0), _heatmap.GetLength(1), config.ContentHost.Width, config.ContentHost.Height);
            _finishCallback = config.FinishedCallback;
            _logger = config.Logger;
            _radius = (Math.Min(instance.MapHorizontalLength / instance.MapColumnCount, instance.MapVerticalLength / instance.MapRowCount) - 0.02) / 2 * radiusMultiplier;
            _selectedDataPoints = new Dictionary<(int x, int y), LocationDatapoint>();
        }

        /// <summary>
        /// Clears for new time window.
        /// </summary>
        public void Clear()
        {
            _heatmap = new double[_instance.MapColumnCount, _instance.MapRowCount];
            _anyPointAddedToHeatmap = false;
            _selectedDataPoints.Clear();
        }

        /// <summary>
        /// Prepares all data for heatmap generation.
        /// </summary>
        public void PrepareAllData()
        {
            foreach(LocationDatapoint data in _dataPoints)
            {
                ProcessDatapoint(data);
            }
        }

        /// <summary>
        /// Prepares data for specific time window.
        /// </summary>
        /// <param name="lowerBound">Lower bound where to start with time window.</param>
        /// <param name="upperBound">Upper bound where to ends with time window.</param>
        /// <returns></returns>
        public bool PrepareDataForTimeWindow(double lowerBound, double upperBound, int botId = -1)
        {
            bool foundData = false;     

            Func<LocationDatapoint, bool> filter;
            // if botId was given lambda will filter only specific bot
            if (botId > -1)
            {
                filter = (data) => lowerBound <= data.TimeStamp && data.TimeStamp < upperBound && data.BotId == botId;
            }
            else // filter by timestamp
            {
                filter = (data) => lowerBound <= data.TimeStamp && data.TimeStamp < upperBound;
            }

            if (_config.VisualizeOrders)
            {
                _orderColors = new Dictionary<int, Color>();
                foreach (LocationDatapoint data in _dataPoints)
                {
                    if(filter(data))
                    {
                        if(!_orderColors.ContainsKey(data.OrderId))
                        {
                            _orderColors.Add(data.OrderId, Color.FromRgb(0, 0, 0));
                        }
                    }
                }
                if (_orderColors.Count() == 0)
                {
                    return false;
                }
                int idx = 0, baseColorId = 360/_orderColors.Count();
                foreach (var key in _orderColors.Keys.ToList())
                {
                    if(key > -1)
                    {
                        _orderColors[key] = ColorManager.GenerateHueColor(baseColorId * idx++);                        
                    }
                }
            }

            foreach (LocationDatapoint data in _dataPoints)
            {
                if (filter(data))
                {
                    foundData = true;
                    ProcessDatapoint(data);
                }
            }
            return foundData;
        }

        #region Core members

        /// <summary>
        /// The config specifying certain settings.
        /// </summary>
        private HeatMapRendererConfiguration _config;
        /// <summary>
        /// The canvas to draw on.
        /// </summary>
        private Canvas _contentControl;
        /// <summary>
        /// The file containing the heat data.
        /// </summary>
        private string _dataFile;
        /// <summary>
        /// The type of the heat data.
        /// </summary>
        private HeatDataType _dataType;
        /// <summary>
        /// The datapoints.
        /// </summary>
        private List<HeatDatapoint> _dataPoints;
        /// <summary>
        /// The actual heatmap.
        /// </summary>
        private double[,] _heatmap;
        /// <summary>
        /// The selected data points for post-processing.
        /// </summary>
        private Dictionary<(int x, int y), LocationDatapoint> _selectedDataPoints;
        /// <summary>
        /// The tier we are looking at.
        /// </summary>
        private ITierInfo _tier;
        /// <summary>
        /// The transformer object used to transform the coordinates between instance and canvas lengths.
        /// </summary>
        private Transformation2D _transformer;
        /// <summary>
        /// The method to call after the operation finishes.
        /// </summary>
        private Action _finishCallback;
        /// <summary>
        /// A logger used to output progress information.
        /// </summary>
        private Action<string> _logger;
        /// <summary>
        /// Instance
        /// </summary>
        private Core.Instance _instance;
        /// <summary>
        /// Flag if any point has been added to heatmap
        /// </summary>
        private bool _anyPointAddedToHeatmap = false;
        /// <summary>
        /// Radius of marker.
        /// </summary>
        private double _radius;
        /// <summary>
        /// 
        /// </summary>
        private Dictionary<int, Color> _orderColors;

        #endregion

        #region Meta information

        /// <summary>
        /// The resulting image. (This is set after renering is done)
        /// </summary>
        public Image ResultImage { get; private set; }

        #endregion

        /// <summary>
        /// Reads the data and renders the heatmap (synchronously).
        /// </summary>
        public bool RenderSync()
        {
            ReadDataPoints(_dataFile);
            PrepareAllData();
            if (_config.VisualizePickedItems)
            {
                BuildVisualizations();
                _finishCallback?.Invoke();
                return _dataPoints.Count() > 0;
            } else if (_config.VisualizeOrders) {
                // Should not be implemented
                return true;
            } else if (_anyPointAddedToHeatmap)
                BuildHeatmap();
            _finishCallback?.Invoke();
            return _anyPointAddedToHeatmap;
        }
        /// <summary>
        /// Reads the data and renders the heatmap (asynchronously).
        /// </summary>
        public void RenderAsync()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(Render));
        }
        /// <summary>
        /// Reads the data and renders the heatmap - also a wrapper to fit <code>ThreadPool</code>
        /// </summary>
        /// <param name="dummy">Unused data.</param>
        private void Render(object dummy)
        {
            ReadDataPoints(_dataFile);
            PrepareAllData();
            if (_config.VisualizePickedItems)
            {
                BuildVisualizations();
                _finishCallback?.Invoke();
                return;
            } else if (_config.VisualizeOrders)
            {
                // Should not be implemented:
                // Visualizing Orders should only be able with Time windows. Not with the all of the datapoints.
                return;
            }
            else if (_anyPointAddedToHeatmap)
                BuildHeatmap();
            _finishCallback?.Invoke();
        }

        /// <summary>
        /// Extracts the datatype information from a heat statistics file.
        /// </summary>
        /// <param name="file">The file to determine the datatype for.</param>
        /// <returns>The data contained in the heat file.</returns>
        public static HeatDataType ParseHeatDataType(string file)
        {
            HeatDataType dataType = HeatDataType.PolledLocation;
            using (StreamReader sr = new StreamReader(file))
            {
                string content = sr.ReadToEnd();
                int tagStart = content.IndexOf(IOConstants.STAT_HEAT_TAG_START);
                int tagEnd = content.IndexOf(IOConstants.STAT_HEAT_TAG_END);
                if (tagStart < 0 || tagEnd < 0)
                    throw new FormatException("Could not find heat data type identifier!");
                string ident = content.Substring(tagStart, tagEnd - tagStart).Replace(IOConstants.STAT_HEAT_TAG_START, "").Replace(IOConstants.STAT_HEAT_TAG_END, "");
                bool parseSuccess = Enum.TryParse(ident, out dataType);
                if (!parseSuccess)
                    throw new FormatException("Could not recognize heat data type of file: " + ident);
            }
            return dataType;
        }

        /// <summary>
        /// Gets the available sub data choices for the given data type.
        /// </summary>
        /// <param name="dataType">The data type to get the sub data choices for.</param>
        /// <returns>All available sub information.</returns>
        public static string[] GetSubDataChoices(HeatDataType dataType)
        {
            switch (dataType)
            {
                case HeatDataType.PolledLocation: return Enum.GetNames(typeof(LocationDatapoint.LocationDataType)).ToArray();
                case HeatDataType.TimeIndependentTripData: return Enum.GetNames(typeof(TimeIndependentTripDataPoint.TripDataType)).ToArray();
                case HeatDataType.StorageLocationInfo: return Enum.GetNames(typeof(StorageLocationInfoDatapoint.StorageLocationInfoType)).ToArray();
                default: throw new ArgumentException("Unknown data-type: " + dataType);
            }
        }

        /// <summary>
        /// Converts 2D points to row (Y) and column (X).
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <exception cref="ArgumentException"></exception>
        private void XYToRowCol(double X, double Y, out int row, out int col)
        {
            decimal horizontalWaypointDistance = (decimal) (_instance.MapHorizontalLength / _instance.MapColumnCount);
            decimal verticalWaypointDistance = (decimal) (_instance.MapVerticalLength / _instance.MapRowCount);
            X -= (double) horizontalWaypointDistance / 2;
            Y -= (double) verticalWaypointDistance / 2;
            if (X > _instance.MapHorizontalLength || X < 0.0 || (decimal)X % (decimal)horizontalWaypointDistance != (decimal)0.0 ||
                Y > _instance.MapVerticalLength || Y < 0.0 || (decimal)Y % (decimal)verticalWaypointDistance != (decimal)0.0)
            {
                row = -1;
                col = -1;
                throw new ArgumentException("Invalid (X,Y): " + X + ", " + Y);
            }
            row = (int)Math.Round(Y / (double)verticalWaypointDistance);
            col = (int)Math.Round(X / (double)horizontalWaypointDistance);
        }

        /// <summary>
        /// Adds point to heatmap.
        /// </summary>
        /// <param name="x">X point.</param>
        /// <param name="y">Y point.</param>
        private void AddPointToHeatmap(double x, double y)
        {
            int row;
            int col;
            XYToRowCol(x, y, out row, out col);
            _heatmap[col, _heatmap.GetLength(1) - row - 1]++;
        }

        /// <summary>
        /// Add location data point (X,Y + robot ID) to the collection. 
        /// If there was a robot on (X, Y) before, that point will be treated as point with multiple robots and set to ID = -1.
        /// </summary>
        /// <param name="datapoint">Location datapoint that will be added.</param>
        private void AddPointToSelectedDataPointsForPickers(LocationDatapoint datapoint)
        {
            int row;
            int col;
            XYToRowCol(datapoint.X, datapoint.Y, out row, out col);
            var key = (x: col, y: _heatmap.GetLength(1) - row - 1);
            if (_selectedDataPoints.ContainsKey(key)){
                // If two different bots visited the same place, set BotId to -1. (Later will be colored black.)
                if (_selectedDataPoints[key].BotId != datapoint.BotId)
                { 
                    _selectedDataPoints[key].BotId = -1;
                }
                
            } else
            {
                _selectedDataPoints.Add(key, datapoint);
            }
        }

        /// <summary>
        /// Add location data point (X,Y + order ID) to the collection. 
        /// If there was a order on (X, Y) before, that point will be treated as point with multiple orders and set to ID = -1.
        /// </summary>
        /// <param name="datapoint">Location datapoint that will be added.</param>
        private void AddPointToSelectedDataPointsForOrders(LocationDatapoint datapoint)
        {
            int row;
            int col;
            XYToRowCol(datapoint.X, datapoint.Y, out row, out col);
            var key = (x: col, y: _heatmap.GetLength(1) - row - 1);
            if (_selectedDataPoints.ContainsKey(key))
            {
                if (_selectedDataPoints[key].OrderId != datapoint.OrderId)
                {
                    _selectedDataPoints[key].OrderId = -1;
                }
            }
            else
            {
                _selectedDataPoints.Add(key, datapoint);
            }
        }

        /// <summary>
        /// Processes one datapoint.
        /// </summary>
        /// <param name="datapoint">Datapoint to be processed.</param>
        private void ProcessDatapoint(LocationDatapoint datapoint)
        {

            // Only add datapoint, if it belongs to the set of task types that shall be considered
            if (_config.VisualizeTasksAndStates && (_config.BotTaskFilter.Contains(datapoint.BotTask) || _config.BotStateFilter.Contains(datapoint.BotState)))
            {
                _anyPointAddedToHeatmap = true;
                AddPointToHeatmap(datapoint.X, datapoint.Y);
            } else if(_config.VisualizeCongestion)
            {
                foreach((double x, double y, double velocity) position in datapoint.MovingSlowlyPositions)
                {
                    if(_config.MinVelocityThreshold <= position.velocity && position.velocity <= _config.MaxVelocityThreshold)
                    {
                        _anyPointAddedToHeatmap = true;
                        AddPointToHeatmap(position.x, position.y);
                    }
                }
            } else if (_config.VisualizePickedItems && datapoint.BotState == BotStateType.WaitingForStation)
            {
                AddPointToSelectedDataPointsForPickers(datapoint);
            } else if(_config.VisualizeOrders && (datapoint.BotState == BotStateType.WaitingForMate || datapoint.BotState == BotStateType.BotAssist))
            {
                AddPointToSelectedDataPointsForOrders(datapoint);
            }      
        }

        /// <summary>
        /// Read the datapoints from the location file.
        /// </summary>
        /// <param name="file">The path to the data-file.</param>
        public void ReadDataPoints(string file)
        {
            // Identify data type
            _dataType = ParseHeatDataType(file);
            // Init data
            _dataPoints = new List<HeatDatapoint>();
            if (_config.InitialBotPositions != null)
            {
                // Only use the initial bot positions
                _dataPoints.AddRange(_config.InitialBotPositions.Where(t => t.Item1 == _tier.GetInfoID())
                    .Select(t => new LocationDatapoint() { Tier = _tier.GetInfoID(), TimeStamp = 0, X = t.Item2, Y = t.Item3 }));
            }
            else
            {
                // Read data
                using (StreamReader sr = new StreamReader(file))
                {
                    string line = "";
                    // Prepare datapoints for a bit more efficient calculation
                    _logger("Preparing dataset ...");
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();
                        // Skip empty and comment lines
                        if (line.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(line))
                            continue;
                        
                        // Parse data
                        switch (_dataType)
                        {
                            case HeatDataType.PolledLocation:
                                {
                                    LocationDatapoint datapoint = LocationDatapoint.FromCSV(line);
                                    _dataPoints.Add(datapoint);
                                }
                                break;
                            case HeatDataType.TimeIndependentTripData:
                                {
                                    _dataPoints.Add(TimeIndependentTripDataPoint.FromCSV(line));
                                }
                                break;
                            case HeatDataType.StorageLocationInfo:
                                {
                                    StorageLocationInfoDatapoint datapoint = StorageLocationInfoDatapoint.FromCSV(line);
                                    // Only add datapoint, if it belongs to the set time window (if a window is specified)
                                    if (_config.DataTimeFilterLow == _config.DataTimeFilterHigh || _config.DataTimeFilterLow <= datapoint.TimeStamp && datapoint.TimeStamp < _config.DataTimeFilterHigh)
                                        // The datapoint made it through all filters - add it
                                        _dataPoints.Add(datapoint);
                                }
                                break;
                            default: throw new ArgumentException("Unknown data type: " + _dataType.ToString());
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Builds and renders the heatmap.
        /// </summary>
        public void BuildHeatmap()
        {
            if (!_anyPointAddedToHeatmap)
            {
                return; // nothing to update
            }

            double offsetForLog = -1;
            // Handle logarithmic transformation, if desired
            if (_config.Logarithmic)
            {
                // If logarithmic values are desired, shift all values to numbers greater or equal to 1 first
                double minValue = _heatmap.Cast<double>().Min(v => v);
                offsetForLog = minValue < 1 ? 1 - minValue : 0;
                if (_config.Logarithmic && offsetForLog > 0)
                    for (int x = 0; x < _heatmap.GetLength(0); x++)
                        for (int y = 0; y < _heatmap.GetLength(1); y++)
                            _heatmap[x, y] += offsetForLog;
                // Transform to logarithmic values if desired
                for (int x = 0; x < _heatmap.GetLength(0); x++)
                    for (int y = 0; y < _heatmap.GetLength(1); y++)
                        _heatmap[x, y] = _heatmap[x, y] <= 0 ? 0 : Math.Log10(_heatmap[x, y]);
            }
            // Normalize the heat to [0,1]
            _logger("Normalizing heatmap ...");
            double maxHeat = double.MinValue;
            for (int x = 0; x < _heatmap.GetLength(0); x++)
                for (int y = 0; y < _heatmap.GetLength(1); y++)
                    maxHeat = Math.Max(maxHeat, _heatmap[x, y]);
            for (int x = 0; x < _heatmap.GetLength(0); x++)
                for (int y = 0; y < _heatmap.GetLength(1); y++)
                    _heatmap[x, y] /= maxHeat;

            if(_config.SaveLegend)
            {
                // Colors of the legend are defined as middlepoints of intervals.
                // The interval [0.4, 0.6] will be represented with color of 0.5, the middlepoint.
                double[] listOfIntervals = new double[] { 0.2, 0.4, 0.6, 0.8, 1.0 };
                for (int i = 0; i < listOfIntervals.Length; i++)
                {
                    listOfIntervals[i] = listOfIntervals[i] * maxHeat;
                }

                if (_config.Logarithmic)
                {
                    for (int i = 0; i < listOfIntervals.Length; i++)
                    {
                        listOfIntervals[i] = (int)Math.Pow(10.0, listOfIntervals[i]) - offsetForLog;
                        Console.WriteLine(i + " => " + listOfIntervals[i]);
                    }
                }
                GenerateLegend(listOfIntervals);
            }
            
            // Render the heat overlay
            _logger("Rendering heatmap ...");
            _contentControl.Dispatcher.Invoke(() =>
            {
                // Init image
                Image image = new Image();
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); // Changed from NearestNeighbor (which was faster) but we wanted high quality.
                image.Opacity = 0.7;

                int bitmapWidth = (int)_transformer.ProjectXLength(_heatmap.GetLength(0));
                int bitmapHeight = (int)_transformer.ProjectYLength(_heatmap.GetLength(1));
                WriteableBitmap writeableBitmap = BitmapFactory.New(bitmapWidth, bitmapHeight); // TODO hotfixing the missing 1-pixel column and row by increasing the size of the bitmap by 1 in each direction

                // Draw all tiles
                for (int x = 0; x < _heatmap.GetLength(0); x++)
                {
                    for (int y = 0; y < _heatmap.GetLength(1); y++)
                    {
                        int xCenter = (int)_transformer.ProjectXLength((x + 0.5));
                        int yCenter = (int)_transformer.ProjectYLength((y + 0.5));
                        int radius = (int)_transformer.ProjectXLength(_radius);
                        Color color = _config.BichromaticColoring ?
                            HeatVisualizer.GenerateBiChromaticHeatColor(_config.BichromaticColorOne, _config.BichromaticColorTwo, _heatmap[x, y]) :
                            HeatVisualizer.GenerateHeatColor(_heatmap[x, y]);
                        writeableBitmap.FillEllipseCentered(xCenter, yCenter, radius, radius, color);
                    }
                }
                image.Source = writeableBitmap;
                ResultImage = image; 
                // Add the image to the canvas (in background, if desired)
                if (_config.DrawInBackground)
                    _contentControl.Children.Insert(0, image);
                else
                    _contentControl.Children.Add(image);
                _contentControl.Children[_contentControl.Children.Count - 1].UpdateLayout();
            });
            
            // Finished
            _logger("Heatmap done!");
        }


        public void SaveCanvas(string snapshotDir, Canvas canvas, string snapshotFilename = null)
        {
            // Get the bounds of the stuff to render
            Rect bounds = System.Windows.Media.VisualTreeHelper.GetDescendantBounds(canvas);
            // Scale dimensions from 96 dpi to 300 dpi.
            double scale = 300 / 96;
            // Init the image
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)(scale * (bounds.Width + 1)), (int)(scale * (bounds.Height + 1)), scale * 96, scale * 96, System.Windows.Media.PixelFormats.Default);
            // Render the complete control
            bitmap.Render(canvas);
            // Init encoder
            BitmapEncoder pngEncoder = new PngBitmapEncoder();
            // Add the frame
            pngEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            // Write the image to the given export location using the instance name and a timestamp
            using (System.IO.FileStream stream = System.IO.File.Create(System.IO.Path.Combine(snapshotDir,
                // If a filename is given, use it
                snapshotFilename != null ?
                    // If the filename does not already end with .png, append it
                    snapshotFilename.EndsWith(".png") ?
                        snapshotFilename :
                        snapshotFilename + ".png" : "legend" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png"
                // No filename given, use a date timestamp instead
                )))
            {
                // Actually save the picture
                pngEncoder.Save(stream);
            }
        }


        private double GenerateLabel(Canvas canvas, double colorValue, int fontSize, double startWidth)
        {
            TextBlock tb = new TextBlock();
            tb.Background = new SolidColorBrush(Colors.White);
            tb.Foreground= new SolidColorBrush(_config.BichromaticColoring ?
                            HeatVisualizer.GenerateBiChromaticHeatColor(_config.BichromaticColorOne, _config.BichromaticColorTwo, colorValue) :
                            HeatVisualizer.GenerateHeatColor(colorValue));
            tb.Text = "   ∎ ";
            tb.FontSize = fontSize;
            tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            tb.Arrange(new Rect(new Point(startWidth, 0), new Point(startWidth + tb.DesiredSize.Width, tb.DesiredSize.Height)));
            startWidth += tb.DesiredSize.Width;
            canvas.Children.Add(tb);
            return startWidth;
        }
        private double GenerateText(Canvas canvas, int fontSize, double startWidth, string text)
        {
            TextBlock tb = new TextBlock();
            tb.Background = new SolidColorBrush(Colors.White);
            tb.Foreground = new SolidColorBrush(Colors.Black);
            tb.Text = text;
            tb.FontSize = fontSize;
            tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            tb.Arrange(new Rect(new Point(startWidth-1, 0), new Point(startWidth + tb.DesiredSize.Width, tb.DesiredSize.Height)));
            startWidth += tb.DesiredSize.Width;
            canvas.Children.Add(tb);
            return startWidth;
        }


        private void GenerateLegend(double[] listOfIntervals)
        {
            Canvas canvas = new Canvas();
            
            double startWidth = 0.0;
            int fontSize = 40;

            startWidth = GenerateLabel(canvas, 0.3, fontSize, startWidth);
            startWidth = GenerateText(canvas, fontSize, startWidth, "" + (int)listOfIntervals[0] + " - " + (int)listOfIntervals[1]);

            startWidth = GenerateLabel(canvas, 0.5, fontSize, startWidth-1);
            startWidth = GenerateText(canvas, fontSize, startWidth, "" + (int)(listOfIntervals[1] + 1) + " - " + (int)listOfIntervals[2]);

            startWidth = GenerateLabel(canvas, 0.7, fontSize, startWidth-1);
            startWidth = GenerateText(canvas, fontSize, startWidth, "" + (int)(listOfIntervals[2] + 1) + " - " + (int)listOfIntervals[3]);

            startWidth = GenerateLabel(canvas, 0.9, fontSize, startWidth-1);
            startWidth = GenerateText(canvas, fontSize, startWidth, "" + (int)(listOfIntervals[3] + 1) + " - " + (int)listOfIntervals[4] + "   ");

            Console.WriteLine("dirr : " + _config.DirectorySaveLocation);
            if(_config.FileName.Length == 0)
            {
                _config.FileName = "legend" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";
            } else
            {
                _config.FileName = "legend" + "-" + _config.FileName;
            }
            Console.WriteLine(_config.DirectorySaveLocation + "  " + _config.FileName);
            SaveCanvas(_config.DirectorySaveLocation, canvas, _config.FileName);
        }

        /// <summary>
        /// Computes color for mate.
        /// </summary>
        /// <param name="mateID">Mate ID</param>
        /// <returns>color</returns>
        private Color ComputeColorForMate(int mateID)
        {
            return ColorManager.GenerateHueColor(_instance.GetBotByID(mateID).botHue);
        }

        /// <summary>
        /// 
        /// </summary>
        /// Computes color for order or mate.
        /// <param name="datapoint">Datapoint used for computation.</param>
        /// <returns>The color.</returns>
        private Color ComputeColor(LocationDatapoint datapoint)
        {
            Color color = Color.FromRgb(0, 0, 0);
            if (_config.VisualizeOrders)
            {
                color = _orderColors[datapoint.OrderId];
            } else if (_config.VisualizePickedItems && datapoint.BotId > -1)
            {
                color = ComputeColorForMate(datapoint.BotId);
            }
            return color;
        }

        /// <summary>
        /// Builds and renders the pickers and orders visualizations.
        /// </summary>
        public void BuildVisualizations()
        {
            if (_selectedDataPoints.Count() == 0)
            {
                return; // nothing to update
            }

            // Render the heat overlay
            _logger("Rendering pickers visualizations ...");
            _contentControl.Dispatcher.Invoke(() =>
            {
                // Init image
                Image image = new Image();
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); // Changed from NearestNeighbor (which was faster) but we wanted high quality.
                image.Opacity = 0.7;

                int bitmapWidth = (int)_transformer.ProjectXLength(_heatmap.GetLength(0));
                int bitmapHeight = (int)_transformer.ProjectYLength(_heatmap.GetLength(1));
                WriteableBitmap writeableBitmap = BitmapFactory.New(bitmapWidth, bitmapHeight); // TODO hotfixing the missing 1-pixel column and row by increasing the size of the bitmap by 1 in each direction

                foreach(var point in _selectedDataPoints)
                {
                    int xCenter = (int)_transformer.ProjectXLength((point.Key.x + 0.5));
                    int yCenter = (int)_transformer.ProjectYLength((point.Key.y + 0.5));
                    int radius = (int)_transformer.ProjectXLength(_radius);

                    writeableBitmap.FillEllipseCentered(xCenter, yCenter, radius, radius, ComputeColor(point.Value));
                }
                image.Source = writeableBitmap;
                ResultImage = image;
                // Add the image to the canvas (in background, if desired)
                if (_config.DrawInBackground)
                    _contentControl.Children.Insert(0, image);
                else
                    _contentControl.Children.Add(image);
                _contentControl.Children[_contentControl.Children.Count - 1].UpdateLayout();
            });

            // Finished
            _logger("Heatmap done!");
        }

        /// <summary>
        /// Removes last child in canvas for time window.
        /// </summary>
        public void RemoveLastChildInCanvas()
        {
            _contentControl.Children.RemoveAt(_contentControl.Children.Count - 1);
        }
    }
}
