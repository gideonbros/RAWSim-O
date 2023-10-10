using RAWSimO.Core;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Generator;
using RAWSimO.Core.Info;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.IO;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Randomization;
using RAWSimO.Core.Control;
using RAWSimO.Playground.Tests;
using RAWSimO.VisualToolbox;
using RAWSimO.Visualization.Rendering;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RAWSimO.Core.Configurations;
using System.Threading;
using System.Reflection;
using System.Globalization;
using RAWSimO.Core.Remote;
using RAWSimO.CommFramework;
using RAWSimO.Core.Statistics;
using RAWSimO.DataPreparation;
using RAWSimO.Toolbox;

namespace RAWSimO.Visualization
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Add key event listener
            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);

            // >>> Init
            // Init lights
            foreach (var light in _lights)
                Viewport3D.Lights.Children.Add(light);
            // Init dynamic button images
            _imageDisableInfoView = ConvertBitmapResource(Properties.Resources.info27);
            _imageEnableInfoView = ConvertBitmapResource(Properties.Resources.info22);
            // Init setting configuration ui-elements
            VisualParameterInputHelper.FillParameterItems(Dispatcher, WrapPanelSettingConfiguration.Children, _baseConfiguration);
            // Init control configuration ui-elements
            VisualParameterInputHelper.FillParameterItems(Dispatcher, WrapPanelControlConfiguration.Children, _controlConfiguration);
            // Init layout configuration ui-elements
            VisualParameterInputHelper.FillParameterItems(Dispatcher, WrapPanelInstanceLayout.Children, _layoutConfig);
            // Init simple item config param list
            VisualParameterInputHelper.FillParameterItems(Dispatcher, WrapPanelSimpleItemConfiguration.Children, _simpleItemGeneratorPreConfig);
            // Init heatmap rendering section ui-elements
            InitHeatmapUIElements();
            // Init some combobox content elements
            InitComboboxContent();
            // Set statistics output directory
            TextStatisicsOutputFolder.Text = @"";
            // Set current active remote button and image
            _remoteButton = ButtonConnectControl;
            _remoteImage = ImageConnectControl;
        }

        /// <summary>
        /// Used to log output to a file.
        /// </summary>
        private StreamWriter _logWriter;

        /// <summary>
        /// Indicates whether a log will be written to disk.
        /// </summary>
        private bool _logWritingEnabled = true;

        /// <summary>
        /// The current view-mode in use (2D/3D).
        /// </summary>
        ViewMode _viewMode = ViewMode.View2D;

        /// <summary>
        /// Denotes whether to draw additional information like a heatmap or not.
        /// </summary>
        bool _heatMode = false;

        /// <summary>
        /// The current mode of bot coloring.
        /// </summary>
        BotColorMode _botColorMode = BotColorMode.DefaultBotDefaultState;

        /// <summary>
        /// The control for the 2D animation.
        /// </summary>
        SimulationAnimation2D _animationControl2D;

        /// <summary>
        /// The control for the 3D animation.
        /// </summary>
        SimulationAnimation3D _animationControl3D;

        /// <summary>
        /// The control for the meta information of the objects.
        /// </summary>
        SimulationInfoManager _infoControl;

        /// <summary>
        /// The simulation wrapper.
        /// </summary>
        SimulationVisualizer _renderer;
        
        /// <summary>
        /// Indicated whether StartSimulation() has been called in this instance
        /// </summary>
        bool _firstCall = true;

        /// <summary>
        /// Indicates whether there is an active paused simulation.
        /// </summary>
        bool _paused = false;

        /// <summary>
        /// Indicates whether there is an active simulation.
        /// </summary>
        bool _running = false;

        /// <summary>
        /// The currently active instance.
        /// </summary>
        Instance _instance = null;

        /// <summary>
        /// Indicates whether the instance was invalidated by an operation such that it cannot execute anymore.
        /// </summary>
        private bool _instanceInvalidated = false;

        /// <summary>
        /// The lights to use in the 3D animation.
        /// </summary>
        List<Light> _lights = new List<Light> { new DirectionalLight(Colors.White, new Vector3D(-4, -5, -6)), new DirectionalLight(Colors.White, new Vector3D(5, 6, 4)), };

        /// <summary>
        /// The image to use for the enable image view button.
        /// </summary>
        BitmapImage _imageEnableInfoView;

        /// <summary>
        /// The image to use for the disable image view button.
        /// </summary>
        BitmapImage _imageDisableInfoView;

        /// <summary>
        /// A directory to save snapshots to.
        /// </summary>
        string _snapshotDir = "snapshots";

        /// <summary>
        /// The list of all tiers currently available for visualization.
        /// </summary>
        List<ITierInfo> _tiers;

        /// <summary>
        /// The index of the tier currently visualized.
        /// </summary>
        int _focusedTierIndex;

        /// <summary>
        /// The configuration to use when simple items shall be generated online. This object is used to automatically generate appropriate ui-elements and parse their content.
        /// </summary>
        private OrderGenerator.SimpleItemGeneratorPreConfiguration _simpleItemGeneratorPreConfig = new OrderGenerator.SimpleItemGeneratorPreConfiguration();
        /// <summary>
        /// The layout configuration to use for generating instances.
        /// </summary>
        private LayoutConfiguration _layoutConfig = new LayoutConfiguration();
        /// <summary>
        /// The configuration to attach to the instance when executing.
        /// </summary>
        private SettingConfiguration _baseConfiguration = new SettingConfiguration();
        /// <summary>
        /// The control configuration to attach to the instance when executing.
        /// </summary>
        private ControlConfiguration _controlConfiguration = new ControlConfiguration();

        #region Instance and configuration parsing

        private void ParseSetting()
        {
            // Parse the configuration from the gui elements
            VisualParameterInputHelper.ParseParameterItems(Dispatcher, WrapPanelSettingConfiguration.Children, _baseConfiguration);
            // Set appropriate default name
            _baseConfiguration.Name = _baseConfiguration.GetMetaInfoBasedConfigName();
        }
        private void ParseConfiguration()
        {
            // Parse the configuration from the gui elements
            VisualParameterInputHelper.ParseParameterItems(Dispatcher, WrapPanelControlConfiguration.Children, _controlConfiguration);
            // Set appropriate default name
            _controlConfiguration.Name = _controlConfiguration.GetMetaInfoBasedConfigName();
        }
        private void ParseLayoutConfiguration()
        {
            // Parse the configuration from the gui elements
            VisualParameterInputHelper.ParseParameterItems(Dispatcher, WrapPanelInstanceLayout.Children, _layoutConfig);
        }

        #endregion

        #region Helper methods

        private bool GetDrawMode3D() { return _viewMode == ViewMode.View3D; }
        private void To2DView()
        {
            // Handle visibility
            scroller.Visibility = Visibility.Visible;
            Viewport3D.Visibility = Visibility.Hidden;
            if (_animationControl3D != null)
                _animationControl3D.StopAnimation();
            _viewMode = ViewMode.View2D;
            // Refresh view
            if (_animationControl2D != null)
                _animationControl2D.Update(true);
        }
        private void To3DView()
        {
            // Handle visibility
            scroller.Visibility = Visibility.Hidden;
            Viewport3D.Visibility = Visibility.Visible;
            if (_animationControl2D != null)
                _animationControl2D.StopAnimation();
            _viewMode = ViewMode.View3D;
            // Refresh view
            if (_animationControl3D != null)
                _animationControl3D.Update(true);
        }
        void Log(string message)
        {
            // Init logger
            if (_logWritingEnabled)
                lock (this)
                    if (_logWriter == null)
                        try { _logWriter = new StreamWriter("controllog.txt", false) { AutoFlush = true }; }
                        catch (Exception) { _logWritingEnabled = false; }
            // Write to file
            _logWriter?.Write(message);
            // Write to GUI
            this.Dispatcher.InvokeAsync(() =>
            {
                TextBoxOutput.AppendText(message);
                TextBoxOutput.ScrollToEnd();
            });
        }
        void LogLine(string message)
        {
            // Init logger
            if (_logWritingEnabled)
                lock (this)
                    if (_logWriter == null)
                        try { _logWriter = new StreamWriter("controllog.txt", false) { AutoFlush = true }; }
                        catch (Exception) { _logWritingEnabled = false; }
            // Write to file
            _logWriter?.WriteLine(message);
            // Write to GUI
            this.Dispatcher.InvokeAsync(() =>
            {
                TextBoxOutput.AppendText(message + "\n");
                TextBoxOutput.ScrollToEnd();
            });
        }
        private void IncreaseSpeed()
        {
            double newSpeed = (double.Parse(TextBlockUpdateSpeed.Text, IOConstants.FORMATTER) + 1);
            if (newSpeed > 0 && newSpeed <= 400)
            {
                TextBlockUpdateSpeed.Text = newSpeed.ToString(IOConstants.FORMATTER);
                if (_renderer != null)
                    _renderer.SetUpdateRate(newSpeed);
            }
        }
        private void DecreaseSpeed()
        {
            double newSpeed = (double.Parse(TextBlockUpdateSpeed.Text, IOConstants.FORMATTER) - 1);
            if (newSpeed > 0 && newSpeed <= 400)
            {
                TextBlockUpdateSpeed.Text = newSpeed.ToString(IOConstants.FORMATTER);
                if (_renderer != null)
                    _renderer.SetUpdateRate(newSpeed);
            }
        }
        private void SetSpeed(int speed)
        {
            // Bound speed
            speed = Math.Max(Math.Min(speed, 400), 1);
            TextBlockUpdateSpeed.Text = speed.ToString(IOConstants.FORMATTER);
            if (_renderer != null)
                _renderer.SetUpdateRate(speed);
        }
        private void UpdateHeatmode()
        {
            if (_instance != null && ComboBoxHeatMode.SelectedIndex >= 0)
            {
                // Check whether heatmode drawing is desired at all
                HeatMode mode = Enum.GetValues(typeof(HeatMode)).Cast<HeatMode>().ElementAt(ComboBoxHeatMode.SelectedIndex);
                if (mode == HeatMode.None)
                {
                    // Disable heat rendering
                    _heatMode = false;
                }
                else
                {
                    // Set the corresponding heatmode
                    _heatMode = true;
                    _instance.SettingConfig.HeatMode = mode;
                }
            }
        }
        private void UpdateBotColoringMode()
        {
            // Set new coloring mode
            if (ComboBoxBotColoringMode.SelectedIndex >= 0)
                _botColorMode = Enum.GetValues(typeof(BotColorMode)).Cast<BotColorMode>().ElementAt(ComboBoxBotColoringMode.SelectedIndex);
        }
        private void UpdateFocusedTier()
        {
            if (_instance != null && _animationControl2D != null)
            {
                // Keep index in bounds
                if (ComboBoxTierSelection2D.SelectedIndex < 0 || ComboBoxTierSelection2D.SelectedIndex >= _tiers.Count)
                    _focusedTierIndex = 0;
                // Update the visual
                _animationControl2D.UpdateCurrentTier(_tiers[_focusedTierIndex]);
                // Update the info panel
                TextBlockTierInFocus.Text = _tiers[_focusedTierIndex].GetInfoID().ToString();
            }
        }
        private void FocusTierAbove() { if (_instance != null && _animationControl2D != null) { _focusedTierIndex = Math.Min(_tiers.Count - 1, _focusedTierIndex + 1); UpdateFocusedTier(); } }
        private void FocusTierBelow() { if (_instance != null && _animationControl2D != null) { _focusedTierIndex = Math.Max(0, _focusedTierIndex - 1); UpdateFocusedTier(); } }
        private void StartSimulation()
        {
            _firstCall = false;
            // Check whether there is an active simulation
            if (!_paused)
            {
                if (_instanceInvalidated)
                {
                    // Instance was invalidated
                    MessageBox.Show("Instance was invalidated by an operation and cannot execute anymore.\nIf you modified the instance, please save it to a file and reload it to make sure initialization was done correctly.", "Invalid instance", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (_instance != null)
                {
                    // Log start
                    LogLine(""); LogLine("");
                    LogLine("<<< Starting simulation >>>");
                    // Handle UI elements
                    DisableButtons();
                    // Execute
                    ExecuteInstance();
                }
            }
            else
            {
                // Handle controls
                FlipPausePlayButton();
                // Resume the simulation
                _renderer.ResumeSimulation();
                // Mark unpaused
                _paused = false;
            }
        }
        private void PauseSimulation()
        {
            _paused = true;
            FlipPausePlayButton();
            _renderer.PauseSimulation();
        }
        private void SetUpdaterate(double rate) { this.Dispatcher.Invoke(() => { TextBlockUpdateSpeed.Text = rate.ToString(IOConstants.FORMATTER); }); }
        private double GetUpdaterate() { double value = 1.0; this.Dispatcher.Invoke(() => { value = double.Parse(TextBlockUpdateSpeed.Text, IOConstants.FORMATTER); }); return value; }
        private void UpdateSimulationTime(double time) { TimeSpan simulationTime = TimeSpan.FromSeconds(time); this.Dispatcher.Invoke(() => { TextBlockSolutionTime.Text = simulationTime.ToString(IOConstants.TIMESPAN_FORMAT_HUMAN_READABLE_DAYS); }); }
        private void InitVisuals()
        {
            // Mark the attached visualization
            _instance.SettingConfig.VisualizationAttached = true;
            // Init info manager
            _infoControl = new SimulationInfoManager(
                TreeViewInfoPanel, // The panel hosting all the infos
                _instance // The instance to manage
                );
            // Update tier info
            _tiers = _instance.GetInfoTiers().ToList();
            ComboBoxTierSelection2D.Items.Clear();
            foreach (var tier in _tiers)
                ComboBoxTierSelection2D.Items.Add(tier.GetInfoID());
            ComboBoxTierSelection2D.SelectedIndex = 0;
            TextBlockTierInFocus.Text = _tiers[_focusedTierIndex].GetInfoID().ToString();
            // Init drawing - 2D
            _animationControl2D = new SimulationAnimation2D(
                _instance, // The instance to visualize
                this.Dispatcher, // The dispatcher handling the calls to the UI thread
                new SimulationAnimationConfig()
                {
                    // The level of detail for drawing
                    DetailLevel = (DetailLevel)Enum.Parse(typeof(DetailLevel), ComboBoxDrawMode2D.Text),
                    // Draw goal marker?
                    DrawGoal = CheckBoxVisDrawGoalMarker.IsChecked == true,
                    // Draw destination marker?
                    DrawDestination = CheckBoxVisDrawDestMarker.IsChecked == true,
                    // Draw path marker?
                    DrawPath = CheckBoxVisDrawPathMarker.IsChecked == true,
                },
                () => { return _heatMode; }, // The function declaring the current heat-mode
                () => { return _botColorMode; }, // The function passing the current bot coloring mode
                content, // The content element containing all the visual objects
                theGrid, // The parent of the content element
                Rectangle_MouseDown, // The event handler to register with all clickable objects
                _infoControl, // The manager of the different info objects
                _tiers[0] // The current tier to draw (begin with the base)
                );
            // Init drawing - 3D
            _animationControl3D = new SimulationAnimation3D(
                _instance, // The instance to visualize
                this.Dispatcher, // The dispatcher handling the calls to the UI thread
                new SimulationAnimationConfig()
                {
                    // The level of detail for drawing
                    DetailLevel = (DetailLevel)Enum.Parse(typeof(DetailLevel), ComboBoxDrawMode3D.Text),
                    // Draw goal marker?
                    DrawGoal = CheckBoxVisDrawGoalMarker.IsChecked == true,
                    // Draw destination marker?
                    DrawDestination = CheckBoxVisDrawDestMarker.IsChecked == true,
                    // Draw path marker?
                    DrawPath = CheckBoxVisDrawPathMarker.IsChecked == true,
                },
                () => { return _heatMode; }, // The function declaring the current heat-mode
                () => { return _botColorMode; }, // The function passing the current bot coloring mode
                Viewport3D, // The content element containing all the visual objects
                _infoControl // The manager of the different info objects
                );
            // Set the initial heatmode
            UpdateHeatmode();
            // Initialize
            _animationControl2D.Init();
            _animationControl3D.Init();
            _infoControl.Init();
        }
        private void InitComboboxContent()
        {
            // Init heat color mode enumeration in combobox
            ComboBoxHeatMode.Items.Clear();
            foreach (var item in Enum.GetNames(typeof(HeatMode)))
                ComboBoxHeatMode.Items.Add(item);
            ComboBoxHeatMode.SelectedIndex = 0;
            // Init bot color mode enumeration in combobox
            ComboBoxBotColoringMode.Items.Clear();
            foreach (var item in Enum.GetNames(typeof(BotColorMode)))
                ComboBoxBotColoringMode.Items.Add(item);
            ComboBoxBotColoringMode.SelectedIndex = 0;
        }
        private void DisableButtons()
        {
            this.Dispatcher.Invoke(() =>
            {
                ButtonStart.IsEnabled = false; ButtonStart.Visibility = Visibility.Collapsed; ImageStart.Visibility = Visibility.Collapsed;
                ButtonPause.IsEnabled = true; ButtonPause.Visibility = Visibility.Visible; ImagePause.Visibility = Visibility.Visible;
                ButtonStop.IsEnabled = true; ImageStop.Visibility = Visibility.Visible;
                ButtonLoadInstance.IsEnabled = false; ImageLoadInstance.Visibility = Visibility.Hidden;
                ButtonGenerateInstance.IsEnabled = false; ImageGenerateInstance.Visibility = Visibility.Hidden;
                ButtonSanityCheck.IsEnabled = false; ImageSanityCheck.Visibility = Visibility.Hidden;
                ButtonSaveInstance.IsEnabled = false; ImageSaveInstance.Visibility = Visibility.Hidden;
                ButtonSaveLayout.IsEnabled = false; ImageSaveLayout.Visibility = Visibility.Hidden;
                ButtonSaveSettingConfiguration.IsEnabled = false; ImageSaveSettingConfiguration.Visibility = Visibility.Hidden;
                ButtonSaveControlConfiguration.IsEnabled = false; ImageSaveControlConfiguration.Visibility = Visibility.Hidden;
                ButtonSaveOrderList.IsEnabled = false; ImageSaveOrderList.Visibility = Visibility.Hidden;
                ButtonLoadHeatInstance.IsEnabled = false; ImageLoadHeatInstance.Visibility = Visibility.Hidden;
                ButtonLoadHeatInfo.IsEnabled = false; ImageLoadHeatInfo.Visibility = Visibility.Hidden;
                ButtonHeatRedraw.IsEnabled = false; ImageHeatRedraw.Visibility = Visibility.Hidden;
                ButtonHeatDrawAll.IsEnabled = false; ImageHeatDrawAll.Visibility = Visibility.Hidden;
                ButtonHeatDrawTimeWindows.IsEnabled = false; ImageHeatDrawTimeWindows.Visibility = Visibility.Hidden;
                _remoteButton.IsEnabled = false; _remoteImage.Visibility = Visibility.Hidden;
            });
        }
        private void EnableButtons()
        {
            this.Dispatcher.Invoke(() =>
            {
                ButtonStart.IsEnabled = true; ButtonStart.Visibility = Visibility.Visible; ImageStart.Visibility = Visibility.Visible;
                ButtonPause.IsEnabled = false; ButtonPause.Visibility = Visibility.Collapsed; ImagePause.Visibility = Visibility.Collapsed;
                ButtonStop.IsEnabled = false; ImageStop.Visibility = Visibility.Hidden;
                ButtonLoadInstance.IsEnabled = true; ImageLoadInstance.Visibility = Visibility.Visible;
                ButtonGenerateInstance.IsEnabled = true; ImageGenerateInstance.Visibility = Visibility.Visible;
                ButtonSanityCheck.IsEnabled = true; ImageSanityCheck.Visibility = Visibility.Visible;
                ButtonSaveInstance.IsEnabled = true; ImageSaveInstance.Visibility = Visibility.Visible;
                ButtonSaveLayout.IsEnabled = true; ImageSaveLayout.Visibility = Visibility.Visible;
                ButtonSaveSettingConfiguration.IsEnabled = true; ImageSaveSettingConfiguration.Visibility = Visibility.Visible;
                ButtonSaveControlConfiguration.IsEnabled = true; ImageSaveControlConfiguration.Visibility = Visibility.Visible;
                ButtonSaveOrderList.IsEnabled = true; ImageSaveOrderList.Visibility = Visibility.Visible;
                ButtonLoadHeatInstance.IsEnabled = true; ImageLoadHeatInstance.Visibility = Visibility.Visible;
                ButtonLoadHeatInfo.IsEnabled = true; ImageLoadHeatInfo.Visibility = Visibility.Visible;
                ButtonHeatRedraw.IsEnabled = true; ImageHeatRedraw.Visibility = Visibility.Visible;
                ButtonHeatDrawAll.IsEnabled = true; ImageHeatDrawAll.Visibility = Visibility.Visible;
                ButtonHeatDrawTimeWindows.IsEnabled = true; ImageHeatDrawTimeWindows.Visibility = Visibility.Visible;
                _remoteButton.IsEnabled = true; _remoteImage.Visibility = Visibility.Visible;
            });
        }
        private void FlipPausePlayButton()
        {
            if (ButtonStart.IsEnabled == true)
            {
                ButtonStart.IsEnabled = false; ButtonStart.Visibility = Visibility.Collapsed; ImageStart.Visibility = Visibility.Collapsed;
                ButtonPause.IsEnabled = true; ButtonPause.Visibility = Visibility.Visible; ImagePause.Visibility = Visibility.Visible;
            }
            else
            {
                ButtonStart.IsEnabled = true; ButtonStart.Visibility = Visibility.Visible; ImageStart.Visibility = Visibility.Visible;
                ButtonPause.IsEnabled = false; ButtonPause.Visibility = Visibility.Collapsed; ImagePause.Visibility = Visibility.Collapsed;
            }
        }
        private void ResetView()
        {
            if (_instance != null)
            {
                // Look at the instance
                CameraHelper.LookAt(
                    Viewport3D.Camera,
                    new Point3D(_instance.GetInfoTiers().Average(t => (t.GetInfoTLX() + t.GetInfoLength()) / 2.0), _instance.GetInfoTiers().Average(t => (t.GetInfoTLY() + t.GetInfoWidth()) / 2.0), 0),
                    50,
                    0);
            }
            else
            {
                // Look at origin
                CameraHelper.LookAt(Viewport3D.Camera, new Point3D(0, 0, 0), 0);
            }
        }
        private void ExecuteInstance()
        {
            // Indicate execution
            _running = true;
            // Find the specified word-list file (just-in-case)
            if (_instance.SettingConfig.InventoryConfiguration.ColoredWordConfiguration != null)
                _instance.SettingConfig.InventoryConfiguration.ColoredWordConfiguration.WordFile =
                    IOHelper.FindResourceFile(_instance.SettingConfig.InventoryConfiguration.ColoredWordConfiguration.WordFile, ".");
            // Find the specified order-list file (just-in-case)
            if (_instance.SettingConfig.InventoryConfiguration.FixedInventoryConfiguration != null && _instance.SettingConfig.InventoryConfiguration.OrderMode == OrderMode.Fixed)
                _instance.SettingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderFile =
                    IOHelper.FindResourceFile(_instance.SettingConfig.InventoryConfiguration.FixedInventoryConfiguration.OrderFile, ".");
            // Attach log-action
            _instance.SettingConfig.LogAction = LogLine;
            // Init animation
            InitVisuals();
            // Execute the simulation
            _renderer = new SimulationVisualizer(_instance, _animationControl2D, _animationControl3D, _infoControl, GetDrawMode3D, SetUpdaterate, GetUpdaterate(), UpdateSimulationTime, LogLine, StopExecution);
            _renderer.VisualizeSimulation(_instance.SettingConfig.SimulationWarmupTime + _instance.SettingConfig.SimulationDuration);
        }
        private void StopExecution()
        {
            this.Dispatcher.Invoke(() =>
            {
                // Handle UI elements
                EnableButtons();
                _renderer.StopSimulation();
                // Stop animation
                if (_viewMode == ViewMode.View2D)
                    _animationControl2D.StopAnimation();
                else
                    _animationControl3D.StopAnimation();
                // Remove any possible pause-marker
                _paused = false;
                //write statistics
                if (!Directory.Exists(_instance.SettingConfig.StatisticsSummaryDirectory))
                    Directory.CreateDirectory(_instance.SettingConfig.StatisticsSummaryDirectory);

                if (CheckBoxWriteStatistics.IsChecked == true)
                {
                    _instance.SettingConfig.StatisticsDirectory = _instance.SettingConfig.StatisticsSummaryDirectory + "\\heatmap";
                    if (!Directory.Exists(_instance.SettingConfig.StatisticsDirectory))
                    {
                        Directory.CreateDirectory(_instance.SettingConfig.StatisticsDirectory);
                    } else
                    {
                        // If something exists, we should clear the statistics folder.
                        // Problem is that the data will be appended, so we need to clear recent data.
                        foreach (string file in Directory.GetFiles(_instance.SettingConfig.StatisticsDirectory))
                        {
                            File.Delete(file);
                        }
                    }
                    _instance.WriteStatistics();

                    // Save instance document
                    string filename = _instance.SettingConfig.StatisticsDirectory + "\\" + _instance.GetMetaInfoBasedInstanceName() + ".xinst";
                    _instance.Name = System.IO.Path.GetFileNameWithoutExtension(filename);
                    InstanceIO.WriteInstance(filename, _instance);

                    // Save it
                    filename = _instance.SettingConfig.StatisticsDirectory + "\\" + _instance.GetMetaInfoBasedInstanceName() + ".xsett";
                    _baseConfiguration.Name = System.IO.Path.GetFileNameWithoutExtension(filename);
                    InstanceIO.WriteSetting(filename, _baseConfiguration);
                }
                
                _instance.Controller.StatisticsManager.WriteStatisticsSummary(_instance.SettingConfig.StatisticsSummaryFile);
                // Indicate stop
                _running = false;
                //reset stop condition
                _instance.SettingConfig.StopCondition = false;
            });
        }
        private void SanityCheckAndShowInfo()
        {
            // Check whether we have an instance at all
            if (_instance != null)
            {
                try
                {
                    // Check for obvious errors
                    List<Tuple<Instance.SanityError, string>> errors = _instance.SanityCheck();
                    if (errors.Any())
                    {
                        // Show some info about the recognized errors
                        StringBuilder warningMsg = new StringBuilder("Sanity checking the instance returned negative!\nThe following errors were found:\n");
                        foreach (var errorType in errors.Select(e => e.Item1).Distinct())
                            warningMsg.AppendLine(errorType.ToString() + ": " + _instance.SanityGetDescription(errorType));
                        warningMsg.AppendLine("The detailed error list follows:");
                        foreach (var error in errors)
                            warningMsg.AppendLine(error.Item1.ToString() + ": " + error.Item2);
                        MessageBox.Show(warningMsg.ToString(), "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        // Show success
                        MessageBox.Show("No errors encountered!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    string msg =
                        "An exception occurred while sanity-checking - Something went horribly wrong!\n" +
                        "Here are some details:\n" +
                        "Message: " + ex.Message + "\n" +
                        "Stacktrace:\n" + ex.StackTrace + "\n" +
                        "Inner:\n" + ex.InnerException;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private BitmapImage ConvertBitmapResource(System.Drawing.Bitmap bitmap)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }

        #endregion

        #region Additional events

        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.OemMinus:
                case Key.Subtract: DecreaseSpeed(); break;
                case Key.OemPlus:
                case Key.Add: IncreaseSpeed(); break;
                case Key.D0:
                case Key.NumPad0: SetSpeed(1); break;
                case Key.D1:
                case Key.NumPad1: SetSpeed(50); break;
                case Key.D2:
                case Key.NumPad2: SetSpeed(100); break;
                case Key.D3:
                case Key.NumPad3: SetSpeed(200); break;
                case Key.D4:
                case Key.NumPad4: SetSpeed(400); break;
                case Key.Space: if (!_paused && _instance != null && !_firstCall) PauseSimulation(); else StartSimulation(); break;
                case Key.PageUp: FocusTierAbove(); break;
                case Key.PageDown: FocusTierBelow(); break;
                case Key.L: if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ButtonSaveLayout_Click(sender, e); break;
                case Key.S: if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ButtonSaveSettingConfiguration_Click(sender, e); break;
                case Key.C: if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ButtonSaveControlConfiguration_Click(sender, e); break;
                default: break;
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e) { }
        private void Window_Closed(object sender, EventArgs e)
        {
            if (_running)
            {
                StopExecution();
            }
        }
        private void ComboBoxHeatMode_SelectionChanged(object sender, SelectionChangedEventArgs e) { UpdateHeatmode(); UpdateFocusedTier(); }
        private void ComboBoxTierSelection2D_SelectionChanged(object sender, SelectionChangedEventArgs e) { _focusedTierIndex = ComboBoxTierSelection2D.SelectedIndex; UpdateFocusedTier(); }
        private void ComboBoxBotColoringMode_SelectionChanged(object sender, SelectionChangedEventArgs e) { UpdateBotColoringMode(); }

        #endregion

        #region Button clicks

        private void ButtonStart_Click(object sender, RoutedEventArgs e) { StartSimulation(); }

        private void ButtonPause_Click(object sender, RoutedEventArgs e) { PauseSimulation(); }

        private void ButtonSlowDown_Click(object sender, RoutedEventArgs e) { DecreaseSpeed(); }

        private void ButtonSpeedUp_Click(object sender, RoutedEventArgs e) { IncreaseSpeed(); }

        private void ButtonStop_Click(object sender, RoutedEventArgs e) { StopExecution(); }

        private void ButtonViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (_viewMode == ViewMode.View2D) To3DView();
            else To2DView();
        }

        private void ButtonLoadInstance_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog settingDialog = new OpenFileDialog();

            // Set filter options and filter index.
            settingDialog.Filter = "Setting files (.xsett)|*.xsett";
            settingDialog.FilterIndex = 1;
            settingDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? settClick = settingDialog.ShowDialog();

            // Only go on if user clicked OK
            if (settClick == true)
            {
                // Create an instance of the open file dialog box.
                OpenFileDialog configDialog = new OpenFileDialog();

                // Set filter options and filter index.
                configDialog.Filter = "Configuration files (.xconf)|*.xconf";
                configDialog.FilterIndex = 1;
                configDialog.Multiselect = false;

                // Call the ShowDialog method to show the dialog box.
                bool? confClick = configDialog.ShowDialog();

                // Only go on if user clicked OK
                if (confClick == true)
                {
                    // Create an instance of the open file dialog box.
                    OpenFileDialog instanceDialog = new OpenFileDialog();

                    // Set filter options and filter index.
                    instanceDialog.Filter = "Instance files (.xinst,xlayo)|*.xinst;*.xlayo;";
                    instanceDialog.FilterIndex = 1;
                    instanceDialog.Multiselect = false;

                    // Call the ShowDialog method to show the dialog box.
                    bool? instClick = instanceDialog.ShowDialog();

                    // Process input if the user clicked OK.
                    if (instClick == true)
                    {
                        // Parse the instance
                        _instance = InstanceIO.ReadInstance(instanceDialog.FileName, settingDialog.FileName, configDialog.FileName, true, logAction: LogLine);
                        if (_instance == null) return;
                        _instanceInvalidated = false;
                        // Show the instance
                        InitVisuals();
                        // Reset the view
                        ResetView();
                    }
                }
            }
        }

        private void ButtonSaveInstance_Click(object sender, RoutedEventArgs e)
        {
            if (_instance != null)
            {
                // Init save dialog
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.FileName = _instance.GetMetaInfoBasedInstanceName(); // Default file name
                dialog.DefaultExt = ".xinst"; // Default file extension
                dialog.Filter = "XINST Files (.xinst)|*.xinst"; // Filter files by extension

                // Show save file dialog box
                bool? userClickedOK = dialog.ShowDialog();

                // Process save file dialog box results
                if (userClickedOK == true)
                {
                    // Save document
                    string filename = dialog.FileName;
                    _instance.Name = System.IO.Path.GetFileNameWithoutExtension(filename);
                    InstanceIO.WriteInstance(filename, _instance);
                }
            }
            else
            {
                MessageBox.Show("Generate an instance first.");
            }
        }

        // overrides settings configuration with data
        private void OverrideSettingsConfigurationWithFile()
        {
            // overrides Fixed Inventory Configuration path to order file
            _baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile = _layoutConfig.warehouse.GetOrderFilePath();
            _baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.RefillingFile = _layoutConfig.warehouse.GetRefillingFilePath();
            _baseConfiguration.InventoryConfiguration.UseOrderBatching = _layoutConfig.warehouse.UseOrderBatching();
            _baseConfiguration.InventoryConfiguration.InitialBatchSize = _layoutConfig.warehouse.init_batch_size;
            _baseConfiguration.InventoryConfiguration.AverageNumberOfOrders = _layoutConfig.warehouse.avg_batch_size;
            _baseConfiguration.InventoryConfiguration.BatchingTimeInterval = _layoutConfig.warehouse.batch_time_interval;
            _baseConfiguration.InventoryConfiguration.UsePoissonBatching = _layoutConfig.warehouse.poisson;
            // few additional settings params
            _baseConfiguration.ZonesEnabled = _layoutConfig.warehouse.UseZones();
            _baseConfiguration.usingMapSortItems = _layoutConfig.warehouse.UsingPreferredCommissionOrder();
            _baseConfiguration.SimulationDuration = _layoutConfig.warehouse.time_limit;
            _baseConfiguration.UseConstantAssistDuration = _layoutConfig.warehouse.UseConstAssistTime();
            _baseConfiguration.AssistDuration = _layoutConfig.warehouse.const_assist_time;
            _baseConfiguration.SwitchPalletDuration = _layoutConfig.warehouse.switch_pallet_time;

            _baseConfiguration.BotLocations = _layoutConfig.warehouse.GetSpawnLocationsType();
            _baseConfiguration.LocationsFile = _layoutConfig.warehouse.GetSpawnLocationsFilePath();
            _baseConfiguration.BotsSelfAssist = _layoutConfig.warehouse.bots_as_pickers;
            //_baseConfiguration.ReserveSameAssistLocation = _layoutConfig.warehouse.reserve_same_loc;
            //_baseConfiguration.ReserveNextAssistLocation = _layoutConfig.warehouse.reserve_next_loc;
            // TODO add for zones
        }


        private void ButtonSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            // Parse the configuration
            ParseLayoutConfiguration();

            if (_layoutConfig.OverrideDataWithFile)
                // overrides layout configuration data
                _layoutConfig.OverrideData();

            // Init save dialog
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.FileName = _layoutConfig.GetMetaInfoBasedLayoutName(); // Default file name
            dialog.DefaultExt = ".xlayo"; // Default file extension
            dialog.Filter = "XLAYO Files (.xlayo)|*.xlayo"; // Filter files by extension

            // Show save file dialog box
            bool? userClickedOK = dialog.ShowDialog();

            // Process save file dialog box results
            if (userClickedOK == true)
            {
                // Save it
                string filename = dialog.FileName;
                _layoutConfig.NameLayout = System.IO.Path.GetFileNameWithoutExtension(filename);
                InstanceIO.WriteLayout(filename, _layoutConfig);
            }
        }

        private void ButtonSaveSettingConfiguration_Click(object sender, RoutedEventArgs e)
        {
            // Parse the configuration
            ParseSetting();

            if (_layoutConfig.OverrideDataWithFile)
                OverrideSettingsConfigurationWithFile();

            // Init save dialog
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.FileName = _baseConfiguration.Name; // Default file name
            dialog.DefaultExt = ".xsett"; // Default file extension
            dialog.Filter = "XSETT Files (.xsett)|*.xsett"; // Filter files by extension

            // Remove directory information of word-list file
            if (_baseConfiguration.InventoryConfiguration.ColoredWordConfiguration != null)
                _baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile =
                    System.IO.Path.GetFileName(_baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile);
            // Remove directory information of order-list file
            //if (_baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration != null)
            //    _baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile =
            //        System.IO.Path.GetFileName(_baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile);
            // Remove directory information of simple-item file
            if (_baseConfiguration.InventoryConfiguration.SimpleItemConfiguration != null)
                _baseConfiguration.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile =
                    System.IO.Path.GetFileName(_baseConfiguration.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile);

            // Show save file dialog box
            bool? userClickedOK = dialog.ShowDialog();

            // Process save file dialog box results
            if (userClickedOK == true)
            {
                // Save it
                string filename = dialog.FileName;
                _baseConfiguration.Name = System.IO.Path.GetFileNameWithoutExtension(filename);
                InstanceIO.WriteSetting(filename, _baseConfiguration);
            }
        }

        private void ButtonSaveControlConfiguration_Click(object sender, RoutedEventArgs e)
        {
            // Parse the configuration
            ParseConfiguration();

            // Init save dialog
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.FileName = _controlConfiguration.Name; // Default file name
            dialog.DefaultExt = ".xconf"; // Default file extension
            dialog.Filter = "XCONF Files (.xconf)|*.xconf"; // Filter files by extension

            // Show save file dialog box
            bool? userClickedOK = dialog.ShowDialog();

            // Process save file dialog box results
            if (userClickedOK == true)
            {
                // Save it
                string filename = dialog.FileName;
                _controlConfiguration.Name = System.IO.Path.GetFileNameWithoutExtension(filename);
                InstanceIO.WriteConfiguration(filename, _controlConfiguration);
            }
        }

        private void ButtonSaveOrderList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Fetch parameters
                string wordFile; double timeMin; double timeMax; int seed;
                int count; int positionMin; int positionMax;
                double relativeBundleAmount; int bundleSizeMin; int bundleSizeMax;
                double probR; double probG; double probB; double probY; double weightMin; double weightMax;
                wordFile = TextBoxOrderWordFile.Text;
                try { timeMin = double.Parse(TextBoxOrderTimeMin.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the minimal time!"); }
                try { timeMax = double.Parse(TextBoxOrderTimeMax.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the maximal time!"); }
                try { seed = int.Parse(TextBoxOrderSeed.Text); }
                catch (FormatException) { throw new FormatException("Error while parsing the seed!"); }
                try { count = int.Parse(TextBoxOrderCount.Text); }
                catch (FormatException) { throw new FormatException("Error while parsing the order count!"); }
                try { positionMin = int.Parse(TextBoxOrderPositionMin.Text); }
                catch (FormatException) { throw new FormatException("Error while parsing the minimal position count!"); }
                try { positionMax = int.Parse(TextBoxOrderPositionMax.Text); }
                catch (FormatException) { throw new FormatException("Error while parsing the maximal position count!"); }
                try { relativeBundleAmount = double.Parse(TextBoxOrderBundlesRelativeAmount.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the maximal weight!"); }
                try { bundleSizeMin = int.Parse(TextBoxOrderBundleSizeMin.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the maximal weight!"); }
                try { bundleSizeMax = int.Parse(TextBoxOrderBundleSizeMax.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the maximal weight!"); }
                try { probR = double.Parse(TextBoxOrderR.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the probability for red!"); }
                try { probG = double.Parse(TextBoxOrderG.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the probability for green!"); }
                try { probB = double.Parse(TextBoxOrderB.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the probability for blue!"); }
                try { probY = double.Parse(TextBoxOrderY.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the probability for yellow!"); }
                try { weightMin = double.Parse(TextBoxOrderWeightMin.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the minimal weight!"); }
                try { weightMax = double.Parse(TextBoxOrderWeightMax.Text, IOConstants.FORMATTER); }
                catch (FormatException) { throw new FormatException("Error while parsing the maximal weight!"); }
                Dictionary<LetterColors, double> colorProbs = new Dictionary<LetterColors, double>();
                if (probR > 0) { colorProbs[LetterColors.Red] = probR; }
                if (probG > 0) { colorProbs[LetterColors.Green] = probG; }
                if (probB > 0) { colorProbs[LetterColors.Blue] = probB; }
                if (probY > 0) { colorProbs[LetterColors.Yellow] = probY; }
                LetterColors[] colors = colorProbs.Keys.OrderBy(c => colorProbs[c]).ToArray();
                // Find the specified word-list file
                wordFile = IOHelper.FindResourceFile(wordFile, ".");
                // Generate list
                OrderList list = OrderGenerator.GenerateOrders(wordFile, colors, colorProbs, seed, timeMin, timeMax, count, weightMin, weightMax, positionMin, positionMax, bundleSizeMin, bundleSizeMax, relativeBundleAmount);

                // Init save dialog
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.FileName = list.GetMetaInfoBasedOrderListName(); // Default file name
                dialog.DefaultExt = ".xitem"; // Default file extension
                dialog.Filter = "XITEM Files (.xitem)|*.xitem"; // Filter files by extension

                // Show save file dialog box
                bool? userClickedOK = dialog.ShowDialog();

                // Process save file dialog box results
                if (userClickedOK == true)
                {
                    // Save document
                    string filename = dialog.FileName;
                    InstanceIO.WriteOrders(filename, list);
                }
            }
            catch (FormatException ex)
            {
                MessageBox.Show(this, "Failure while parsing the parameters:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonSaveSimpleItemGeneratorConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Fetch parameters
                VisualParameterInputHelper.ParseParameterItems(Dispatcher, WrapPanelSimpleItemConfiguration.Children, _simpleItemGeneratorPreConfig);
                SimpleItemGeneratorConfiguration config = OrderGenerator.GenerateSimpleItemConfiguration(_simpleItemGeneratorPreConfig);

                // Init save dialog
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.FileName = config.GetMetaInfoBasedName(); // Default file name
                dialog.DefaultExt = ".xgenc"; // Default file extension
                dialog.Filter = "XGENC Files (.xgenc)|*.xgenc"; // Filter files by extension

                // Show save file dialog box
                bool? userClickedOK = dialog.ShowDialog();

                // Process save file dialog box results
                if (userClickedOK == true)
                {
                    // Save document
                    string filename = dialog.FileName;
                    config.Name = System.IO.Path.GetFileNameWithoutExtension(filename);
                    InstanceIO.WriteSimpleItemGeneratorConfigFile(filename, config);
                    // Generate diagram
                    InventoryInfoProcessor.PlotSimpleInventoryFrequencies(filename);
                    // Log
                    LogLine("Saved item config to " + filename + " and generated a diagram for the file in the application directory!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failure while generating the config:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonGenerateInstance_Click(object sender, RoutedEventArgs e)
        {
            _firstCall = true;
            TextBoxOutput.Text = "";
            try
            {
                // Parse the base and layout configurations
                ParseSetting();
                ParseConfiguration();
                ParseLayoutConfiguration();

                // override data
                if (_layoutConfig.OverrideDataWithFile)
                {
                    _layoutConfig.OverrideData();
                    OverrideSettingsConfigurationWithFile();
                }

                // Find the specified word-list file
                if (_baseConfiguration.InventoryConfiguration.ColoredWordConfiguration != null)
                    _baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile =
                        IOHelper.FindResourceFile(_baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile, ".");
                // Find the specified order-list file
                if (_baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration != null && _baseConfiguration.InventoryConfiguration.OrderMode == OrderMode.Fixed)
                    _baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile =
                        IOHelper.FindResourceFile(_baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile, ".");
                // Find the specified generator-config file
                if (_baseConfiguration.InventoryConfiguration.SimpleItemConfiguration != null)
                    _baseConfiguration.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile =
                        IOHelper.FindResourceFile(_baseConfiguration.InventoryConfiguration.SimpleItemConfiguration.GeneratorConfigFile, ".");
                // Mark the attached visualization
                _baseConfiguration.VisualizationAttached = true;

                if (_baseConfiguration.Seed == -1) // Reset Randomizer when button for new instance is clicked and random seed is requested.
                {
                    RandomizerSimple.GlobalRandomSeed = -1;
                    _baseConfiguration.Seed = RandomizerSimple.GetRandomSeed();
                }
                Order.ResetIDCounter();

                //check validity of SettingsConfiguration
                var valid = _baseConfiguration.CheckValidityOfPaths(out var errorMessage);
                if(!valid)
                {
                    //write errorMessage
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Generate
                IRandomizer rand = new RandomizerSimple(_baseConfiguration.Seed);
                _instance = InstanceGenerator.GenerateLayout(_layoutConfig, rand, _baseConfiguration, _controlConfiguration);
                _instanceInvalidated = false;
                _instance.Name = _instance.GetMetaInfoBasedInstanceName();

                // Display
                InitVisuals();

                var tabControl = (TabControl) ((TabItem)((StackPanel)((StackPanel) ((Button) sender).Parent).Parent).Parent).Parent;
                tabControl.SelectedIndex = 0;
            }
            catch (FormatException ex)
            {
                MessageBox.Show(this, "Failure while parsing the parameters:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(this, "Failure while generating the instance:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonSanityCheck_Click(object sender, RoutedEventArgs e)
        {
            if (_instance != null)
                SanityCheckAndShowInfo();
            else
                MessageBox.Show("No instance to check!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ButtonShowInfo_Click(object sender, RoutedEventArgs e)
        {
            if (TreeViewInfoPanel.Visibility == Visibility.Hidden)
            {
                TreeViewInfoPanel.Visibility = Visibility.Visible;
                GridSplitterInfoPanel.Visibility = Visibility.Visible;
                TextBoxOutput.Visibility = Visibility.Visible;
                GridSplitterBoxOutput.Visibility = Visibility.Visible;
                ImageShowInfo.Source = _imageDisableInfoView;
            }
            else
            {
                TreeViewInfoPanel.Visibility = Visibility.Hidden;
                GridSplitterInfoPanel.Visibility = Visibility.Hidden;
                TextBoxOutput.Visibility = Visibility.Hidden;
                GridSplitterBoxOutput.Visibility = Visibility.Hidden;
                ImageShowInfo.Source = _imageEnableInfoView;
            }
        }

        private void ButtonSnapshot_Click(object sender, RoutedEventArgs e)
        {
            _snapshotDir = _instance.SettingConfig.StatisticsSummaryDirectory + "\\snapshots";
            
            if (!Directory.Exists(_snapshotDir))
                Directory.CreateDirectory(_snapshotDir);
            if (_viewMode == ViewMode.View2D)
            {
                if (_animationControl2D != null)
                    _animationControl2D.TakeSnapshot(_snapshotDir);
            }
            else
            {
                if (_animationControl3D != null)
                    _animationControl3D.TakeSnapshot(_snapshotDir);
            }
        }

        #endregion

        #region 2D Control

        /// <summary>
        /// Specifies the current state of the mouse handling logic.
        /// </summary>
        private MouseHandlingMode mouseHandlingMode = MouseHandlingMode.None;

        /// <summary>
        /// The point that was clicked relative to the ZoomAndPanControl.
        /// </summary>
        private Point origZoomAndPanControlMouseDownPoint;

        /// <summary>
        /// The point that was clicked relative to the content that is contained within the ZoomAndPanControl.
        /// </summary>
        private Point origContentMouseDownPoint;

        /// <summary>
        /// Records which mouse button clicked during mouse dragging.
        /// </summary>
        private MouseButton mouseButtonDown;

        /// <summary>
        /// Defines the current state of the mouse handling logic.
        /// </summary>
        private enum MouseHandlingMode
        {
            /// <summary>
            /// Not in any special mode.
            /// </summary>
            None,

            /// <summary>
            /// The user is drawing a rectangle to remove waypoints.
            /// </summary>
            WaypointRemoval,

            /// <summary>
            /// The user is left-mouse-button-dragging to pan the viewport.
            /// </summary>
            Panning,

            /// <summary>
            /// The user is holding down shift and left-clicking or right-clicking to zoom in or out.
            /// </summary>
            Zooming,

            /// <summary>
            /// The user is holding down shift and left-mouse-button-dragging to select a region to zoom to.
            /// </summary>
            DragZooming,
        }

        #region Event handlers

        private void ZoomControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            content.Focus();
            Keyboard.Focus(content);

            mouseButtonDown = e.ChangedButton;
            origZoomAndPanControlMouseDownPoint = e.GetPosition(zoomAndPanControl);
            origContentMouseDownPoint = e.GetPosition(content);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
                (e.ChangedButton == MouseButton.Left ||
                 e.ChangedButton == MouseButton.Right))
            {
                // Shift + left- or right-down initiates zooming mode.
                mouseHandlingMode = MouseHandlingMode.Zooming;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                mouseButtonDown == MouseButton.Left)
            {
                // Only allowed, if waypoints are visible
                if (!((DetailLevel)Enum.Parse(typeof(DetailLevel), ComboBoxDrawMode2D.Text) == DetailLevel.Full))
                {
                    MessageBox.Show("Cannot select invisible waypoints - enable drawing of waypoints first!", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                    e.Handled = true;
                    return;
                }
                // Ctrl + left-down initiates waypoint selection
                mouseHandlingMode = MouseHandlingMode.WaypointRemoval;
            }
            else if (mouseButtonDown == MouseButton.Left)
            {
                // Just a plain old left-down initiates panning mode.
                mouseHandlingMode = MouseHandlingMode.Panning;
            }

            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                // Capture the mouse so that we eventually receive the mouse up event.
                zoomAndPanControl.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ZoomControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                if (mouseHandlingMode == MouseHandlingMode.Zooming)
                {
                    if (mouseButtonDown == MouseButton.Left)
                    {
                        // Shift + left-click zooms in on the content.
                        ZoomIn(origContentMouseDownPoint);
                    }
                    else if (mouseButtonDown == MouseButton.Right)
                    {
                        // Shift + left-click zooms out from the content.
                        ZoomOut(origContentMouseDownPoint);
                    }
                }
                else if (mouseHandlingMode == MouseHandlingMode.DragZooming)
                {
                    // When drag-zooming has finished we zoom in on the rectangle that was highlighted by the user.
                    ZoomBySelectionRect();
                }
                else if (mouseHandlingMode == MouseHandlingMode.WaypointRemoval)
                {
                    // When selecting the waypoints has finished we remove them
                    RemoveWaypointsBySelectionRect();
                }

                zoomAndPanControl.ReleaseMouseCapture();
                mouseHandlingMode = MouseHandlingMode.None;
                e.Handled = true;
            }
        }

        private void ZoomControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseHandlingMode == MouseHandlingMode.Panning)
            {
                // The user is left-dragging the mouse.
                // Pan the viewport by the appropriate amount.
                Point curContentMousePoint = e.GetPosition(content);
                Vector dragOffset = curContentMousePoint - origContentMouseDownPoint;

                zoomAndPanControl.ContentOffsetX -= dragOffset.X;
                zoomAndPanControl.ContentOffsetY -= dragOffset.Y;

                e.Handled = true;
            }
            else if (mouseHandlingMode == MouseHandlingMode.Zooming)
            {
                Point curZoomAndPanControlMousePoint = e.GetPosition(zoomAndPanControl);
                Vector dragOffset = curZoomAndPanControlMousePoint - origZoomAndPanControlMouseDownPoint;
                double dragThreshold = 10;
                if (mouseButtonDown == MouseButton.Left &&
                    (Math.Abs(dragOffset.X) > dragThreshold ||
                     Math.Abs(dragOffset.Y) > dragThreshold))
                {
                    // When Shift + left-down zooming mode and the user drags beyond the drag threshold,
                    // initiate drag zooming mode where the user can drag out a rectangle to select the area
                    // to zoom in on.
                    mouseHandlingMode = MouseHandlingMode.DragZooming;
                    Point curContentMousePoint = e.GetPosition(content);
                    UpdateSelectionRect(origContentMouseDownPoint, curContentMousePoint);
                }

                e.Handled = true;
            }
            else if (mouseHandlingMode == MouseHandlingMode.DragZooming || mouseHandlingMode == MouseHandlingMode.WaypointRemoval)
            {
                // When in drag zooming mode continously update the position of the rectangle
                // that the user is dragging out.
                Point curContentMousePoint = e.GetPosition(content);
                UpdateSelectionRect(origContentMouseDownPoint, curContentMousePoint);

                e.Handled = true;
            }
        }

        private void ZoomControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (e.Delta > 0)
            {
                Point curContentMousePoint = e.GetPosition(content);
                ZoomIn(curContentMousePoint);
            }
            else if (e.Delta < 0)
            {
                Point curContentMousePoint = e.GetPosition(content);
                ZoomOut(curContentMousePoint);
            }
        }

        private void ZoomControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                Point doubleClickPoint = e.GetPosition(content);
                zoomAndPanControl.AnimatedSnapTo(doubleClickPoint);
            }
        }

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            content.Focus();
            Keyboard.Focus(content);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                // When the shift key is held down special zooming logic is executed in content_MouseDown,
                // so don't handle mouse input here.
                return;
            }

            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                // We are in some other mouse handling mode, don't do anything.
                return;
            }

            // Get the focussed object
            if (sender is SimulationVisual2D)
            {
                _infoControl.InitInfoObject(sender as SimulationVisual2D);
            }
            if (sender is SimulationVisual3D)
            {
                _infoControl.InitInfoObject(sender as SimulationVisual3D);
            }

            e.Handled = true;
        }

        private void SetStatisticalOutputFolder(object sender, RoutedEventArgs e)
        {
            //open folder browser dialog and set the path
            var folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
            var result = folderBrowser.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
                TextStatisicsOutputFolder.Text = folderBrowser.SelectedPath;
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Zoom the viewport out, centering on the specified point (in content coordinates).
        /// </summary>
        private void ZoomOut(Point contentZoomCenter)
        {
            zoomAndPanControl.ZoomAboutPoint(zoomAndPanControl.ContentScale - 0.1, contentZoomCenter);
        }

        /// <summary>
        /// Zoom the viewport in, centering on the specified point (in content coordinates).
        /// </summary>
        private void ZoomIn(Point contentZoomCenter)
        {
            zoomAndPanControl.ZoomAboutPoint(zoomAndPanControl.ContentScale + 0.1, contentZoomCenter);
        }

        /// <summary>
        /// Update the position and size of the rectangle that user is dragging out.
        /// </summary>
        private void UpdateSelectionRect(Point pt1, Point pt2)
        {
            double x, y, width, height;

            // Deterine x,y,width and height of the rect inverting the points if necessary.

            if (pt2.X < pt1.X)
            {
                x = pt2.X;
                width = pt1.X - pt2.X;
            }
            else
            {
                x = pt1.X;
                width = pt2.X - pt1.X;
            }

            if (pt2.Y < pt1.Y)
            {
                y = pt2.Y;
                height = pt1.Y - pt2.Y;
            }
            else
            {
                y = pt1.Y;
                height = pt2.Y - pt1.Y;
            }

            // Update the coordinates of the rectangle that is being dragged out by the user.
            // The we offset and rescale to convert from content coordinates.
            System.Windows.Controls.Canvas.SetLeft(dragZoomBorder, x);
            System.Windows.Controls.Canvas.SetTop(dragZoomBorder, y);
            dragZoomBorder.Width = width;
            dragZoomBorder.Height = height;

            // Make the rectangle visible, if not already done
            if (selectionCanvas.Visibility != Visibility.Visible)
            {
                selectionCanvas.Visibility = Visibility.Visible;
                dragZoomBorder.Opacity = 0.5;
            }
        }

        /// <summary>
        /// When the user has finished dragging out the rectangle the zoom operation is applied.
        /// </summary>
        private void ZoomBySelectionRect()
        {
            // Retreive the rectangle that the user draggged out and zoom in on it.
            double contentX = System.Windows.Controls.Canvas.GetLeft(dragZoomBorder);
            double contentY = System.Windows.Controls.Canvas.GetTop(dragZoomBorder);
            double contentWidth = dragZoomBorder.Width;
            double contentHeight = dragZoomBorder.Height;
            zoomAndPanControl.AnimatedZoomTo(new Rect(contentX, contentY, contentWidth, contentHeight));

            FadeOutSelectionRect();
        }

        /// <summary>
        /// When the user has finished dragging out the rectangle for selecting the waypoints to remove the removal is applied.
        /// </summary>
        private void RemoveWaypointsBySelectionRect()
        {
            // Retrieve the rectangle that the user draggged out
            double contentX = System.Windows.Controls.Canvas.GetLeft(dragZoomBorder);
            double contentY = System.Windows.Controls.Canvas.GetTop(dragZoomBorder);
            double contentWidth = dragZoomBorder.Width;
            double contentHeight = dragZoomBorder.Height;

            // If we do not have an instance, just quit here
            if (_instance == null)
            {
                FadeOutSelectionRect();
                return;
            }

            // If the instance is executing, just quit here
            if (_running)
            {
                MessageBox.Show("Cannot modify instance during execution!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                FadeOutSelectionRect();
                return;
            }

            // Invalidate instance
            _instanceInvalidated = true;

            // Init a transformer that helps obtaining the corner points of the rectangle in instance coordinates
            Transformation2D lengthTransformer = new Transformation2D(
                _tiers[_focusedTierIndex].GetInfoLength(),
                _tiers[_focusedTierIndex].GetInfoWidth(),
                theGrid.Width,
                theGrid.Height);

            // Obtain removal coordinates
            double selectionXMin = lengthTransformer.RevertX(contentX);
            double selectionXMax = lengthTransformer.RevertX(contentX) + lengthTransformer.RevertXLength(contentWidth);
            // Note: selection rectangle gives us the upper corner point - we have to make the transfer to the lower one here
            double selectionYMin = lengthTransformer.RevertY(contentY) - lengthTransformer.RevertYLength(contentHeight);
            double selectionYMax = lengthTransformer.RevertY(contentY);

            // Remove the waypoints that are contained in the selected area
            _instance.ModRemoveWaypoints(_tiers[_focusedTierIndex], selectionXMin, selectionYMin, selectionXMax, selectionYMax);

            // Update the visual
            _animationControl2D.UpdateCurrentTier(_tiers[_focusedTierIndex]);

            // Small sanity check
            SanityCheckAndShowInfo();

            // Fade out the selection rectangle
            FadeOutSelectionRect();
        }

        /// <summary>
        /// Fade out the drag zoom rectangle.
        /// </summary>
        private void FadeOutSelectionRect()
        {
            AnimationHelper.StartAnimation(dragZoomBorder, Border.OpacityProperty, 0.0, 0.1,
                delegate (object sender, EventArgs e)
                {
                    selectionCanvas.Visibility = Visibility.Collapsed;
                });
        }

        #endregion

        #endregion

        #region Remote features

        private void ButtonRemoteWaypointReached_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FireOnWaypointReached(null, null); } }
        private void ButtonRemotePickedUpPod_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FireOnPickedUpPodBot(null, null); } }
        private void ButtonRemoteSetDownPod_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FireOnSetDownPodBot(null, null); } }
        private void FireOnWaypointReached(object sender, RoutedEventArgs e)
        {
            var bot = _instance.GetBotByID(Int32.Parse(TextBoxEventBot.Text));
            var waypoint = _instance.GetWaypointByID(Int32.Parse(TextBoxEventWaypoint.Text));
            bot.OnReachedWaypoint(waypoint);
        }

        private void FireOnPickedUpPodBot(object sender, RoutedEventArgs e)
        {
            var bot = _instance.GetBotByID(Int32.Parse(TextBoxEventBot.Text));
            bot.OnPickedUpPod();
        }

        private void FireOnSetDownPodBot(object sender, RoutedEventArgs e)
        {
            var bot = _instance.GetBotByID(Int32.Parse(TextBoxEventBot.Text));
            bot.OnSetDownPod();
        }

        private Button _remoteButton;
        private Image _remoteImage;
        private ControlClient _controlClient;
        private void ConnectionErrorHandler(bool connected)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (connected)
                {
                    // Show disconnect button
                    ButtonConnectControl.IsEnabled = false; ButtonConnectControl.Visibility = Visibility.Collapsed; ImageConnectControl.Visibility = Visibility.Collapsed;
                    ButtonDisconnectControl.IsEnabled = true; ButtonDisconnectControl.Visibility = Visibility.Visible; ImageDisconnectControl.Visibility = Visibility.Visible;
                    // Set active remote button and image
                    _remoteButton = ButtonDisconnectControl;
                    _remoteImage = ImageDisconnectControl;
                }
                else
                {
                    // Show connect button
                    ButtonConnectControl.IsEnabled = true; ButtonConnectControl.Visibility = Visibility.Visible; ImageConnectControl.Visibility = Visibility.Visible;
                    ButtonDisconnectControl.IsEnabled = false; ButtonDisconnectControl.Visibility = Visibility.Collapsed; ImageDisconnectControl.Visibility = Visibility.Collapsed;
                    // Set active remote button and image
                    _remoteButton = ButtonConnectControl;
                    _remoteImage = ImageConnectControl;
                    // Deactivate at the instance
                    if (_instance != null)
                        _instance.DeactivateRemoteController();
                }
            });
        }
        private void ButtonConnectControl_Click(object sender, RoutedEventArgs e)
        {
            if (_instance != null)
            {
                // Init both sides and connect all callbacks
                RemoteControlAdapter remoteControlAdapter = _instance.ActivateRemoteController();
                _controlClient = new ControlClient(
                    remoteControlAdapter.RobotLocationUpdateCallback,
                    remoteControlAdapter.RobotPickupFinishedCallback,
                    remoteControlAdapter.RobotSetdownFinishedCallback,
                    LogLine,
                    ConnectionErrorHandler);
                remoteControlAdapter.PathSubmissionCallback = _controlClient.SubmitNewPath;
                remoteControlAdapter.PickupSubmissionCallback = _controlClient.SubmitPickupCommand;
                remoteControlAdapter.SetdownSubmissionCallback = _controlClient.SubmitSetdownCommand;
                remoteControlAdapter.RestSubmissionCallback = _controlClient.SubmitRestCommand;
                remoteControlAdapter.GetItemSubmissionCallback = _controlClient.SubmitGetItemNotification;
                remoteControlAdapter.PutItemSubmissionCallback = _controlClient.SubmitPutItemNotification;
                // Connect with the server
                _controlClient.Connect(TextBoxRemoteIP.Text, TextBoxRemotePort.Text);
                // Show disconnect button
                ButtonConnectControl.IsEnabled = false; ButtonConnectControl.Visibility = Visibility.Collapsed; ImageConnectControl.Visibility = Visibility.Collapsed;
                ButtonDisconnectControl.IsEnabled = true; ButtonDisconnectControl.Visibility = Visibility.Visible; ImageDisconnectControl.Visibility = Visibility.Visible;
                // Set active remote button and image
                _remoteButton = ButtonDisconnectControl;
                _remoteImage = ImageDisconnectControl;
            }
            else
            {
                LogLine("No instance initiated - load the instance first then connect to the server managing communication with the agents on the instance");
            }
        }

        private void ButtonDisconnectControl_Click(object sender, RoutedEventArgs e)
        {
            // Disconnect client
            if (_controlClient != null)
                _controlClient.Disconnect();
            // Show connect button
            ButtonConnectControl.IsEnabled = true; ButtonConnectControl.Visibility = Visibility.Visible; ImageConnectControl.Visibility = Visibility.Visible;
            ButtonDisconnectControl.IsEnabled = false; ButtonDisconnectControl.Visibility = Visibility.Collapsed; ImageDisconnectControl.Visibility = Visibility.Collapsed;
            // Set active remote button and image
            _remoteButton = ButtonConnectControl;
            _remoteImage = ImageConnectControl;
            // Deactivate at the instance
            if (_instance != null)
                _instance.DeactivateRemoteController();
        }

        #endregion

        #region Heatmap rendering

        /// <summary>
        /// The instance for which the heat data shall be visualized.
        /// </summary>
        private string _heatMapInstanceFile = null;
        /// <summary>
        /// The heat data that shall be visualized for the given instance.
        /// </summary>
        private string _heatMapDataFile = null;
        /// <summary>
        /// The config for which the heat data shall be visualized.
        /// </summary>
        private string _heatMapConfigFile = null;
        /// <summary>
        /// The index of the sub heat information.
        /// </summary>
        private int _heatMapSubDataIndex = 0;
        /// <summary>
        /// Stores the starting positions of the bots that can be used within heatmap generation.
        /// </summary>
        private List<Tuple<int, double, double>> _heatBotStartingPositions;
        /// <summary>
        /// The renderer used to draw heatmaps.
        /// </summary>
        private HeatMapRenderer _heatmapRenderer = null;

        private void ButtonLoadHeatInstance_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog instanceDialog = new OpenFileDialog();

            // Set filter options and filter index.
            instanceDialog.Filter = "XINST Files (.xinst)|*.xinst";
            instanceDialog.FilterIndex = 1;
            instanceDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? instClick = instanceDialog.ShowDialog();

            // Process input if the user clicked OK.
            if (instClick == true)
            {
                // Save file path (for rendering)
                _heatMapInstanceFile = instanceDialog.FileName;
                TextBlockHeatInstance.Text = "Instance: " + System.IO.Path.GetFileName(_heatMapInstanceFile);
                if (!string.IsNullOrWhiteSpace(_heatMapInstanceFile) && File.Exists(_heatMapInstanceFile))
                {
                    // Read and draw instance
                    ReadInstanceForHeatmapRendering();
                    TextBoxHeatTimeWindowLength.Text = ((int)_instance.SettingConfig.StatisticsSummaryOutputFrequency * 60).ToString(); // convert to seconds
                    SliderMinVelocityThreshold.Maximum = _instance.SettingConfig.StatVelocityCutoff;
                    SliderMinVelocityThreshold.Value = _instance.SettingConfig.StatVelocityCutoff;
                    SliderMaxVelocityThreshold.Maximum = _instance.SettingConfig.StatVelocityCutoff;
                    SliderMaxVelocityThreshold.Value = _instance.SettingConfig.StatVelocityCutoff;
                }
            }
        }

        private void ButtonLoadHeatInfo_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog heatDataDialog = new OpenFileDialog();

            // Set filter options and filter index.
            heatDataDialog.Filter = "HEAT Files (.heat)|*.heat";
            heatDataDialog.FilterIndex = 1;
            heatDataDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? heatClick = heatDataDialog.ShowDialog();

            // Process input if the user clicked OK.
            if (heatClick == true)
            {
                // Extract datatype information
                HeatDataType dataType = HeatMapRenderer.ParseHeatDataType(heatDataDialog.FileName);
                // Get sub data type choices
                string[] choices = HeatMapRenderer.GetSubDataChoices(dataType);
                // Let the user choose
                ComboBoxDialog choiceDialog = new ComboBoxDialog(choices, (int index) => { _heatMapSubDataIndex = index; });
                bool? choiceClick = choiceDialog.ShowDialog();
                // Process input if the user clicked OK
                if (choiceClick == true)
                {
                    // Save file path (for rendering)
                    _heatMapDataFile = heatDataDialog.FileName;
                    TextBlockHeatDataFile.Text = "Data: " + System.IO.Path.GetFileName(_heatMapDataFile) + " (" + choices[_heatMapSubDataIndex] + ")";
                }
            }
        }

        private void ButtonLoadHeatConfig_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog instanceDialog = new OpenFileDialog();

            // Set filter options and filter index.
            instanceDialog.Filter = "XSETT Files (.xsett)|*.xsett";
            instanceDialog.FilterIndex = 1;
            instanceDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? instClick = instanceDialog.ShowDialog();

            // Process input if the user clicked OK.
            if (instClick == true)
            {
                // Save file path (for rendering)
                _heatMapConfigFile = instanceDialog.FileName;
                TextBlockHeatConfig.Text = "Config: " + System.IO.Path.GetFileName(_heatMapConfigFile);
            }
        }

        private void ButtonHeatJustDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_heatMapInstanceFile) && File.Exists(_heatMapInstanceFile))
            {
                // Read and draw instance
                ReadInstanceForHeatmapRendering();
                // Show the instance
                InitVisuals();
                // Reset the view
                ResetView();
            }
        }

        private void ButtonHeatRedraw_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_heatMapInstanceFile) && File.Exists(_heatMapDataFile))
            {
                if (_instance != null)
                {
                    _snapshotDir = _instance.SettingConfig.StatisticsSummaryDirectory + "\\snapshots";
                }
                // Read instance
                ReadInstanceForHeatmapRendering();
                // Start rendering
                HeatMapRendererConfiguration config = ParseHeatmapConfiguration();
                config.DataFile = _heatMapDataFile;
                config.DataIndex = _heatMapSubDataIndex;
                config.DirectorySaveLocation = _snapshotDir;
                RenderHeatmap(config, false);
            }
            else
            {
                // Warn the user
                MessageBox.Show("No data files defined - load them first!");
            }
        }

        private void ButtonHeatDrawAll_Click(object sender, RoutedEventArgs e)
        {
            // Init heatmaps to generate
            List<Tuple<string, int>> heatmapFiles = new List<Tuple<string, int>>()
            {
                new Tuple<string, int>(IOConstants.StatFileNames[IOConstants.StatFile.HeatLocationPolling], 0),
                new Tuple<string, int>(IOConstants.StatFileNames[IOConstants.StatFile.HeatTrips], 0),
            };
            // Init parameters
            string instanceDir = null;
            string rootResultDir = null;
            // Select instance resource directory
            using (System.Windows.Forms.FolderBrowserDialog instanceDirDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                instanceDirDialog.Description = "Select directory containing all necessary instance files";
                if (instanceDirDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Save instance dir
                    instanceDir = instanceDirDialog.SelectedPath;
                    // Select result directory
                    using (System.Windows.Forms.FolderBrowserDialog rootResultDirDialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        rootResultDirDialog.Description = "Select root directory containing all result directories";
                        if (rootResultDirDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            // Save result dir
                            rootResultDir = rootResultDirDialog.SelectedPath;
                        }
                    }
                }
            }
            // Disable buttons
            DisableButtons();
            // Check preliminaries
            if (instanceDir != null && rootResultDir != null)
            {
                // Asynchronously execute
                ThreadPool.QueueUserWorkItem((object unused) =>
                {
                    // --> Generate all heatmaps
                    _heatMapInstanceFile = null;
                    string[] resultDirectories = Directory.EnumerateDirectories(rootResultDir).ToArray();
                    for (int i = 0; i < resultDirectories.Length; i++)
                    {
                        // Fetch instance name
                        string instanceName = null;
                        string path = System.IO.Path.Combine(resultDirectories[i], IOConstants.StatFileNames[IOConstants.StatFile.InstanceName]);
                        if (!File.Exists(path))
                            continue;
                        using (StreamReader sr = new StreamReader(path))
                            instanceName = sr.ReadToEnd().Trim();
                        // Log
                        LogLine("Generating heatmap(s) for instance " + instanceName + " - directory " + i + " / " + resultDirectories.Length);
                        // See whether we have to load a new instance for the current result
                        if (System.IO.Path.GetFileNameWithoutExtension(_heatMapInstanceFile) != instanceName)
                        {
                            _heatMapInstanceFile = System.IO.Path.Combine(instanceDir, instanceName + ".xinst");
                            Dispatcher.Invoke(() =>
                            {
                                ReadInstanceForHeatmapRendering();
                            });
                        }
                        // Generate the heatmaps
                        foreach (var heatFile in heatmapFiles)
                        {
                            // Parse the config to use for rendering
                            HeatMapRendererConfiguration config = null;
                            Dispatcher.Invoke(() =>
                            {
                                config = ParseHeatmapConfiguration();
                            });
                            config.FinishedCallback = null;
                            // Render heatmap
                            config.DataFile = System.IO.Path.Combine(resultDirectories[i], heatFile.Item1);
                            config.DataIndex = heatFile.Item2;
                            Dispatcher.Invoke(() =>
                            {
                                // Show the instance
                                InitVisuals();
                                // Reset the view
                                ResetView();
                                // Init the renderer with radius multiplier which is used to reduce the size of visualization marker.
                                _heatmapRenderer = new HeatMapRenderer(config, _instance, SliderRadiusMultiplier.Value);
                            });
                            // Render the heatmap
                            _heatmapRenderer.RenderSync();
                            // Wait a short time for the GUI to update
                            Thread.Sleep(5000);
                            // Save screenshot
                            if (_viewMode == ViewMode.View2D && _animationControl2D != null)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    _animationControl2D.TakeSnapshot(resultDirectories[i], System.IO.Path.GetFileNameWithoutExtension(heatFile.Item1));
                                });
                            }
                            else
                            {
                                MessageBox.Show("Heatmaps can only be generated in 2D view!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                    // Mark finished
                    HeatRenderingCallback();
                });
            }
        }

        /// <summary>
        /// Used for processing screenshot of the time window. The time window with lower and upper bounds.
        /// </summary>
        /// <param name="config">HeatMapRendererConfiguration</param>
        /// <param name="lowerBoundTimeWindow">Lower bound of the time window.</param>
        /// <param name="upperBoundTimeWindow">Upper bound of the time window.</param>
        /// <param name="botID">Robot's id.</param>
        private void processScreenshot(HeatMapRendererConfiguration config, double lowerBoundTimeWindow, double upperBoundTimeWindow, int botID = -1)
        {
            // saves screenshot
            if (config.VisualizePickedItems || config.VisualizeOrders)
            {
                _heatmapRenderer.BuildVisualizations();
            } else
            {
                config.FileName = lowerBoundTimeWindow.ToString() + "-" + upperBoundTimeWindow;
                _heatmapRenderer.BuildHeatmap();
            }
            if (_viewMode == ViewMode.View2D && _animationControl2D != null)
            {
                Dispatcher.Invoke(() =>
                {
                    if (!Directory.Exists(_snapshotDir))
                        Directory.CreateDirectory(_snapshotDir);

                    int day = TimeSpan.FromSeconds(lowerBoundTimeWindow).Days;
                    int hour = TimeSpan.FromSeconds(lowerBoundTimeWindow).Hours;
                    int minute = TimeSpan.FromSeconds(lowerBoundTimeWindow).Minutes;
                    _animationControl2D.TakeSnapshot(_snapshotDir, (botID > -1 ? "bot" + botID + "-" : "") + lowerBoundTimeWindow.ToString() + "-" + upperBoundTimeWindow);
                });

                _heatmapRenderer.RemoveLastChildInCanvas();
            }
            else
            {
                _heatmapRenderer.RemoveLastChildInCanvas();
                MessageBox.Show("Heatmaps can only be generated in 2D view!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Clear the renderer
            _heatmapRenderer.Clear();
        }

        private void ButtonHeatDrawTimeWindows_Click(object sender, RoutedEventArgs e)
        {
            if (_instance != null)
            {
                _snapshotDir = _instance.SettingConfig.StatisticsSummaryDirectory + "\\snapshots" + "\\" + DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss");
            }
            if (!Directory.Exists(_snapshotDir))
                Directory.CreateDirectory(_snapshotDir);

            if (File.Exists(_heatMapInstanceFile) && File.Exists(_heatMapDataFile))
            {
                // Read instance
                ReadInstanceForHeatmapRendering();
                // Prepare config
                HeatMapRendererConfiguration config = ParseHeatmapConfiguration();
                config.DataFile = _heatMapDataFile;
                config.DataIndex = _heatMapSubDataIndex;
                config.DirectorySaveLocation = _snapshotDir;
                config.FinishedCallback = null;

                double stepLength;
                try
                { stepLength = double.Parse(TextBoxHeatTimeWindowLength.Text, IOConstants.FORMATTER); }
                catch (FormatException) { MessageBox.Show("Cannot parse time-window length!", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                stepLength = Math.Max(SimulationObserver.STEP_LENGTH_POSITION_POLL * 5, stepLength); // At least 5 data points                
                // --> Start rendering
                // Disable buttons
                DisableButtons();
                // Show the instance
                InitVisuals();
                // Reset the view
                ResetView();
                // Init the renderer
                _heatmapRenderer = new HeatMapRenderer(config, _instance, SliderRadiusMultiplier.Value);

                _heatmapRenderer.ReadDataPoints(config.DataFile);
                // --> Generate all heatmaps
                bool success = true; 
                double lowerBoundTimeWindow = 0; 
                double upperBoundTimeWindow = stepLength;
                do
                {
                    // Log
                    LogLine("Generating heatmap for time-window: " + lowerBoundTimeWindow.ToString(IOConstants.FORMATTER) + " -> " + upperBoundTimeWindow.ToString(IOConstants.FORMATTER));
                    
                    
                    success = false;
                    if(config.VisualizePickedItems && CheckBoxDrawPickersSeparated.IsChecked == true)
                    {
                        for (int botID = _instance.GeneratedBotsCount; botID < _instance.GeneratedBotsCount + _instance.GeneratedMatesCount; botID++)
                        {
                            // Render the heatmap
                            bool currentSuccess = _heatmapRenderer.PrepareDataForTimeWindow(lowerBoundTimeWindow, upperBoundTimeWindow, botID);
                            success |= currentSuccess;
                            if (currentSuccess)
                            {
                                processScreenshot(config, lowerBoundTimeWindow, upperBoundTimeWindow, botID);
                            }
                        }
                    } else
                    {
                        success = _heatmapRenderer.PrepareDataForTimeWindow(lowerBoundTimeWindow, upperBoundTimeWindow);
                        if(success)
                        {
                            processScreenshot(config, lowerBoundTimeWindow, upperBoundTimeWindow);
                        }
                    }


                    // Update time window
                    lowerBoundTimeWindow += stepLength;
                    upperBoundTimeWindow += stepLength;
                    // Show the instance
                    InitVisuals();
                    // Reset the view
                    ResetView();
                } while (success);
                // Mark finished
                HeatRenderingCallback();
            }
            else
            {
                // Warn the user
                MessageBox.Show("No data files defined - load them first!");
            }
        }
       
        private void SliderMinVelocityThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Check whether a heatmap currently exists
            if (SliderMinVelocityThreshold?.Value > SliderMaxVelocityThreshold?.Value)
            {
                SliderMaxVelocityThreshold.Value = SliderMinVelocityThreshold.Value;
            }
        }

        private void SliderMaxVelocityThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Check whether a heatmap currently exists
            if (SliderMinVelocityThreshold?.Value > SliderMaxVelocityThreshold?.Value)
            {
                SliderMinVelocityThreshold.Value = SliderMaxVelocityThreshold.Value;
            }
        }

        private void SliderHeatOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Check whether a heatmap currently exists
            if (_heatmapRenderer != null && _heatmapRenderer.ResultImage != null)
            {
                // Set the new opacity value
                _heatmapRenderer.ResultImage.Opacity = SliderHeatOpacity.Value;
            }
        }


        private void CheckBoxHeatTransparentCanvas_Checked(object sender, RoutedEventArgs e)
        {
            if (CheckBoxHeatTransparentCanvas.IsChecked == true) { content.Background = Brushes.Transparent; }
            else { content.Background = Brushes.White; }
        }

        /// <summary>
        /// Initializes the dynamic UI-elements for the heatmap rendering section for Tasks and States.
        /// </summary>
        private void InitHeatmapUIElements()
        {
            _heatmapBotTaskFilterCheckboxes = Enum.GetValues(typeof(BotTaskType)).Cast<BotTaskType>().ToDictionary(
                // Bot task type is the key
                k => k,
                // Create a new checkbox for the corresponding task
                v =>
                {
                    bool check = false;
                    CheckBox cb = new CheckBox() { Content = v.ToString(), IsChecked = check };
                    StackPanelHeatmapBotTasks.Children.Add(cb);
                    return cb;
                });

            _heatmapBotStateFilterCheckboxes = Enum.GetValues(typeof(BotStateType)).Cast<BotStateType>().ToDictionary(
                // Bot state type is the key
                k => k,
                // Create a new checkbox for the corresponding state
                v =>
                {
                    bool check = false;
                    // This is the most used option for heatmap visualization, so it is checked.
                    if (v.ToString() == "WaitingForMate")
                    {
                        check = true;
                    }

                    CheckBox cb = new CheckBox() { Content = v.ToString(), IsChecked = check };
                    StackPanelHeatmapBotStates.Children.Add(cb);
                    return cb;
                });
        }
        /// <summary>
        /// Contains the checkboxes used for filtering position heat-data by the corresponding bot task.
        /// </summary>
        private Dictionary<BotTaskType, CheckBox> _heatmapBotTaskFilterCheckboxes;
        /// <summary>
        /// Contains the checkboxes used for filtering position heat-data by the corresponding bot states.
        /// </summary>
        private Dictionary<BotStateType, CheckBox> _heatmapBotStateFilterCheckboxes;
        /// <summary>
        /// Parses the current heatmap setting from the GUI and returns a new configuration according to these.
        /// </summary>
        /// <returns>The new config.</returns>
        private HeatMapRendererConfiguration ParseHeatmapConfiguration()
        {
            // Init 
            HeatMapRendererConfiguration config = null;
            // Parse the parameters
            bool drawInBackground = CheckBoxHeatDrawInBackground.IsChecked == true;
            bool logarithmic = CheckBoxHeatLogarithmic.IsChecked == true;
            bool saveLegend = CheckBoxHeatSaveLegend.IsChecked == true;
            bool bichromaticColoring = CheckBoxHeatBichromaticColoring.IsChecked == true;
            bool visualizeCongestion = VisualizeCongestion.IsChecked == true;
            bool visualizeTasksAndStates = VisualizeTasksAndStates.IsChecked == true;
            bool visualizePickedItems = VisualizePickedItems.IsChecked == true;
            bool visualizeOrders = VisualizeOrders.IsChecked == true;
            double minVelocityThreshold = SliderMinVelocityThreshold.Value;
            double maxVelocityThreshold = SliderMaxVelocityThreshold.Value;
            Color firstColor; Color secondColor;
            HashSet<BotTaskType> botTaskFilter = _heatmapBotTaskFilterCheckboxes.Where(kvp => kvp.Value.IsChecked == true).Select(kvp => kvp.Key).ToHashSet();
            HashSet<BotStateType> botStateFilter = _heatmapBotStateFilterCheckboxes.Where(kvp => kvp.Value.IsChecked == true).Select(kvp => kvp.Key).ToHashSet();
            try
            {
                // Parse
                firstColor = (Color)ColorConverter.ConvertFromString(ComboBoxHeatmapColoringFirstColor.SelectedValue.ToString());
                secondColor = (Color)ColorConverter.ConvertFromString(ComboBoxHeatmapColoringSecondColor.SelectedValue.ToString());
                // Create it
                config = new HeatMapRendererConfiguration()
                {
                    ContentControl = content, // The canvas to draw on
                    ContentHost = theGrid, // The host of the canvas (for size information)
                    FinishedCallback = HeatRenderingCallback, // The callback
                    Logger = LogLine, // The logger - this can take a while
                    BotTaskFilter = botTaskFilter, // Only consider data for the given tasks
                    BotStateFilter = botStateFilter, // Only consider data for the given states
                    Tier = _instance.GetInfoTiers().First(), // Draw first tier
                    VisualizeCongestion = visualizeCongestion,
                    VisualizeTasksAndStates = visualizeTasksAndStates,
                    VisualizePickedItems = visualizePickedItems,
                    VisualizeOrders = visualizeOrders,
                    MinVelocityThreshold = minVelocityThreshold,
                    MaxVelocityThreshold = maxVelocityThreshold,
                    DrawInBackground = drawInBackground, // Indicates whether the heatmap will be drawn behind everything else or in front of it
                    Logarithmic = logarithmic, // Indicates that the values are transformed by applying the logarithm
                    SaveLegend = saveLegend,
                    FileName = (string)"",
                    BichromaticColoring = bichromaticColoring, // Specifies whether to use bichromatic coloring
                    BichromaticColorOne = firstColor, // The first color for bichromatic coloring
                    BichromaticColorTwo = secondColor, // The second color for bichromatic coloring
                };
            }
            catch (Exception) { MessageBox.Show("Cannot parse the parameters"); return null; }
            return config;
        }

        private void ReadInstanceForHeatmapRendering()
        {
            // Parse the instance
            _instance = InstanceIO.ReadInstance(_heatMapInstanceFile, _heatMapConfigFile, null, true, true);
            // Invalidate the instance (the instance should not execute in this state)
            _instanceInvalidated = true;
            // Store bot starting positions before removing the bots
            _heatBotStartingPositions = _instance.GetInfoBots().Select(b => new Tuple<int, double, double>(b.GetInfoCurrentTier().GetInfoID(), b.GetInfoCenterX(), b.GetInfoCenterY())).ToList();
            // Remove unecessary objects in case of heatmap visualization
            if (!(CheckBoxHeatDrawBots.IsChecked == true))
                _instance.VisClearBots();
            if (!(CheckBoxHeatDrawPods.IsChecked == true))
                _instance.VisClearPods();
            if (!(CheckBoxHeatDrawStations.IsChecked == true))
                _instance.VisClearStations();
            if (!(CheckBoxHeatDrawSemaphores.IsChecked == true))
                _instance.VisClearSemaphores();
        }

        private void HeatRenderingCallback()
        {
            this.Dispatcher.Invoke(() =>
            {
                // Set opacity just in case it is not the default anymore
                if (_heatmapRenderer.ResultImage != null)
                    _heatmapRenderer.ResultImage.Opacity = SliderHeatOpacity.Value;
            });
            // Enable the buttons again
            EnableButtons();
        }

        private void RenderHeatmap(HeatMapRendererConfiguration config, bool asynchronously)
        {

            // Disable the buttons
            DisableButtons();
            // Show the instance
            InitVisuals();
            // Reset the view
            ResetView();
            // Init the renderer
            _heatmapRenderer = new HeatMapRenderer(config, _instance, SliderRadiusMultiplier.Value);

            // Render the heatmap
            if (asynchronously)
                _heatmapRenderer.RenderAsync();
            else
                _heatmapRenderer.RenderSync();
        }

        #endregion

        #region Flip book debugging

        private void ButtonLoadFlipBookDebuggingMaterial_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog instanceDialog = new OpenFileDialog();

            // Set filter options and filter index.
            instanceDialog.Filter = "XINST Files (.xinst)|*.xinst";
            instanceDialog.FilterIndex = 1;
            instanceDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? instClick = instanceDialog.ShowDialog();

            // Process input if the user clicked OK.
            if (instClick == true)
            {
                // Create an instance of the open file dialog box.
                OpenFileDialog locationDataDialog = new OpenFileDialog();

                // Set filter options and filter index.
                locationDataDialog.Filter = "LP HEAT Files (locationspolled.heat)|locationspolled.heat";
                locationDataDialog.FilterIndex = 1;
                locationDataDialog.Multiselect = false;

                // Call the ShowDialog method to show the dialog box.
                bool? locationDataClick = locationDataDialog.ShowDialog();

                // Process input if the user clicked OK.
                if (locationDataClick == true)
                {
                    // Parse the instance
                    _instance = InstanceIO.ReadInstance(instanceDialog.FileName, null, null, true, true);
                    // TODO what about invalidation?
                    _instanceInvalidated = false;
                    // Remove unecessary objects in case of heatmap visualization
                    _instance.VisClearBots();
                    _instance.VisClearPods();
                    _instance.VisClearSemaphores();

                    // TODO go on
                    throw new NotImplementedException();
                }
            }
        }

        #endregion

        #region Onboard camera

        /// <summary>
        /// The last robot for which the onboard camera was activated.
        /// </summary>
        private SimulationVisualBot3D _onboardCameraBot;

        private void Button3DOnboardCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_onboardCameraBot == null)
            {
                // Choose a new bot and attach the camera to it
                SimulationVisualBot3D[] botVisuals = Viewport3D.Children.OfType<SimulationVisualBot3D>().ToArray();
                if (botVisuals.Any())
                {
                    Random random = new Random();
                    _onboardCameraBot = botVisuals[random.Next(botVisuals.Length)];
                    _onboardCameraBot.StartOnboardCamera(Viewport3D.Camera);
                }
            }
            else
            {
                // Deactivate onboard camera
                _onboardCameraBot.StopOnboardCamera();
                _onboardCameraBot = null;
            }
        }

        #endregion
    }
}
