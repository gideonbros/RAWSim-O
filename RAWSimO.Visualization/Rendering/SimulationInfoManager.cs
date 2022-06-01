using RAWSimO.Core.Info;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RAWSimO.Visualization.Rendering
{
    public class SimulationInfoManager
    {
        public SimulationInfoManager(TreeView infoHost, IInstanceInfo instance)
        {
            _instance = instance;
            _infoHost = infoHost;
            _instanceInfoObject = new SimulationInfoInstance(infoHost, instance);
        }

        IInstanceInfo _instance;
        SimulationInfoInstance _instanceInfoObject;

        Dictionary<IGeneralObjectInfo, SimulationInfoObject> _managedInfoObjects = new Dictionary<IGeneralObjectInfo, SimulationInfoObject>();
        Dictionary<SimulationVisual2D, SimulationInfoObject> _managed2DVisuals = new Dictionary<SimulationVisual2D, SimulationInfoObject>();
        Dictionary<SimulationVisual3D, SimulationInfoObject> _managed3DVisuals = new Dictionary<SimulationVisual3D, SimulationInfoObject>();

        SimulationInfoObject _currentInfoObject;

        TreeView _infoHost;

        public void Register(IBotInfo bot, SimulationVisualBot2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(bot))
                _managedInfoObjects[bot] = new SimulationInfoBot(_infoHost, bot);
            _managed2DVisuals[visual] = _managedInfoObjects[bot];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IPodInfo pod, SimulationVisualPod2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(pod))
                _managedInfoObjects[pod] = new SimulationInfoPod(_infoHost, pod);
            _managed2DVisuals[visual] = _managedInfoObjects[pod];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IInputStationInfo iStation, SimulationVisualInputStation2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(iStation))
                _managedInfoObjects[iStation] = new SimulationInfoInputStation(_infoHost, iStation);
            _managed2DVisuals[visual] = _managedInfoObjects[iStation];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IOutputStationInfo oStation, SimulationVisualOutputStation2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(oStation))
                _managedInfoObjects[oStation] = new SimulationInfoOutputStation(_infoHost, oStation);
            _managed2DVisuals[visual] = _managedInfoObjects[oStation];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IInputStationInfo iStation, SimulationVisualInputPalletStand2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(iStation))
                _managedInfoObjects[iStation] = new SimulationInfoInputStation(_infoHost, iStation);
            _managed2DVisuals[visual] = _managedInfoObjects[iStation];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IOutputStationInfo oStation, SimulationVisualOutputPalletStand2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(oStation))
                _managedInfoObjects[oStation] = new SimulationInfoOutputStation(_infoHost, oStation);
            _managed2DVisuals[visual] = _managedInfoObjects[oStation];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IWaypointInfo waypoint, SimulationVisualWaypoint2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(waypoint))
                _managedInfoObjects[waypoint] = new SimulationInfoWaypoint(_infoHost, waypoint);
            _managed2DVisuals[visual] = _managedInfoObjects[waypoint];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IWaypointInfo waypoint, SimulationVisualUnavailablePod2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(waypoint))
                _managedInfoObjects[waypoint] = new SimulationInfoWaypoint(_infoHost, waypoint);
            _managed2DVisuals[visual] = _managedInfoObjects[waypoint];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IGuardInfo guard, SimulationVisualGuard2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(guard))
                _managedInfoObjects[guard] = new SimulationInfoGuard(_infoHost, guard);
            _managed2DVisuals[visual] = _managedInfoObjects[guard];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IElevatorInfo elevator, SimulationVisualElevatorEntrance2D visual)
        {
            if (!_managedInfoObjects.ContainsKey(elevator))
                _managedInfoObjects[elevator] = new SimulationInfoElevatorEntrance(_infoHost, elevator);
            _managed2DVisuals[visual] = _managedInfoObjects[elevator];
            _managed2DVisuals[visual].ManagedVisual2D = visual;
        }

        public void Register(IBotInfo bot, SimulationVisualBot3D visual)
        {
            if (!_managedInfoObjects.ContainsKey(bot))
                _managedInfoObjects[bot] = new SimulationInfoBot(_infoHost, bot);
            _managed3DVisuals[visual] = _managedInfoObjects[bot];
            _managed3DVisuals[visual].ManagedVisual3D = visual;
        }

        public void Register(IPodInfo pod, SimulationVisualPod3D visual)
        {
            if (!_managedInfoObjects.ContainsKey(pod))
                _managedInfoObjects[pod] = new SimulationInfoPod(_infoHost, pod);
            _managed3DVisuals[visual] = _managedInfoObjects[pod];
            _managed3DVisuals[visual].ManagedVisual3D = visual;
        }

        public void Register(IInputStationInfo iStation, SimulationVisualInputStation3D visual)
        {
            if (!_managedInfoObjects.ContainsKey(iStation))
                _managedInfoObjects[iStation] = new SimulationInfoInputStation(_infoHost, iStation);
            _managed3DVisuals[visual] = _managedInfoObjects[iStation];
            _managed3DVisuals[visual].ManagedVisual3D = visual;
        }

        public void Register(IOutputStationInfo oStation, SimulationVisualOutputStation3D visual)
        {
            if (!_managedInfoObjects.ContainsKey(oStation))
                _managedInfoObjects[oStation] = new SimulationInfoOutputStation(_infoHost, oStation);
            _managed3DVisuals[visual] = _managedInfoObjects[oStation];
            _managed3DVisuals[visual].ManagedVisual3D = visual;
        }

        public void Register(IWaypointInfo waypoint, SimulationVisualOutputStation3D visual)
        {
            if (!_managedInfoObjects.ContainsKey(waypoint))
                _managedInfoObjects[waypoint] = new SimulationInfoWaypoint(_infoHost, waypoint);
            _managed3DVisuals[visual] = _managedInfoObjects[waypoint];
            _managed3DVisuals[visual].ManagedVisual3D = visual;
        }

        public void Register(IElevatorInfo elevator, SimulationVisualElevatorEntrance3D visual)
        {
            if (!_managedInfoObjects.ContainsKey(elevator))
                _managedInfoObjects[elevator] = new SimulationInfoElevatorEntrance(_infoHost, elevator);
            _managed3DVisuals[visual] = _managedInfoObjects[elevator];
            _managed3DVisuals[visual].ManagedVisual3D = visual;
        }

        public void InitInfoObject(SimulationVisual2D visual)
        {
            SimulationInfoObject newInfoObject = _managed2DVisuals.ContainsKey(visual) ? _managed2DVisuals[visual] : null;
            if (newInfoObject != _currentInfoObject)
            {
                // Select the new object and unselect the old one
                if (_currentInfoObject != null)
                    _currentInfoObject.InfoPanelLeave();
                _currentInfoObject = newInfoObject;
                if (_currentInfoObject != null)
                    _currentInfoObject.InfoPanelInit();
            }
            else
            {
                // Unselect the already selected object
                if (_currentInfoObject != null)
                    _currentInfoObject.InfoPanelLeave();
                _currentInfoObject = null;
                HandleInfoPanelNoSelection();
            }
        }

        public void InitInfoObject(SimulationVisual3D visual)
        {
            SimulationInfoObject newInfoObject = _managed3DVisuals.ContainsKey(visual) ? _managed3DVisuals[visual] : null;
            if (newInfoObject != _currentInfoObject)
            {
                // Select the new object and unselect the old one
                if (_currentInfoObject != null)
                    _currentInfoObject.InfoPanelLeave();
                _currentInfoObject = newInfoObject;
                if (_currentInfoObject != null)
                    _currentInfoObject.InfoPanelInit();
            }
            else
            {
                // Unselect the already selected object
                if (_currentInfoObject != null)
                    _currentInfoObject.InfoPanelLeave();
                _currentInfoObject = null;
                HandleInfoPanelNoSelection();
            }
        }

        public void Update()
        {
            _infoHost.Dispatcher.Invoke(() =>
            {
                if (_infoHost.Visibility == Visibility.Visible)
                {
                    if (_currentInfoObject != null)
                    {
                        _currentInfoObject.InfoPanelUpdate();
                    }
                    else
                    {
                        HandleInfoPanelNoSelection();
                    }
                }
            });
        }

        public void Init()
        {
            // Clear info panel
            _infoHost.Items.Clear();
        }

        protected void HandleInfoPanelNoSelection()
        {
            // Add controls in case the info panel is empty
            if (!_infoHost.HasItems)
            {
                _instanceInfoObject.InfoPanelInit();
            }

            // Use info of the instance
            _instanceInfoObject.InfoPanelUpdate();
        }
    }
    #region Bot location manager
    public class SimulationVisualBotLocationManager
    {
        // Root with the open/close triangle
        public TreeViewItem RootBotLocationTable { get; private set; }
        public IInstanceInfo _instanceInfo;

        // Connects each bot to the corresponding list of blocks
        private Dictionary<IBotInfo, WrapPanel> tableRows = new Dictionary<IBotInfo, WrapPanel>();

        private Dictionary<IBotInfo, TextBlock> robotStates = new Dictionary<IBotInfo, TextBlock>();
        private Dictionary<IBotInfo, TextBlock> robotCurrentRowCol = new Dictionary<IBotInfo, TextBlock>();
        private Dictionary<IBotInfo, TextBlock> robotTargetRowCol = new Dictionary<IBotInfo, TextBlock>();
        private Dictionary<IBotInfo, TextBlock> robotGoalRowCol = new Dictionary<IBotInfo, TextBlock>();
        private Dictionary<IBotInfo, TextBlock> robotCurrAddresses = new Dictionary<IBotInfo, TextBlock>();
        private Dictionary<IBotInfo, TextBlock> robotCurrMates = new Dictionary<IBotInfo, TextBlock>();

        private Dictionary<IBotInfo, int> botOrder = new Dictionary<IBotInfo, int>();
        private Dictionary<int, List<Tuple<TextBlock, TextBlock>>> order = new Dictionary<int, List<Tuple<TextBlock, TextBlock>>>();
        public SimulationVisualBotLocationManager(TreeViewItem rootBotLocationTable, IInstanceInfo instanceInfo)
        {
            RootBotLocationTable = rootBotLocationTable;
            RootBotLocationTable.Header = "Bot location table";
            _instanceInfo = instanceInfo;

            // column names
            WrapPanel columnNames = new WrapPanel();
            columnNames.Children.Add(CreateTextBlock("Bot", Brushes.White, Brushes.Black, 6));
            columnNames.Children.Add(CreateTextBlock("Status", Brushes.White, Brushes.Black, 15));
            columnNames.Children.Add(CreateTextBlock("Curr. Row/Col", Brushes.White, Brushes.Black, 15));
            columnNames.Children.Add(CreateTextBlock("Targ. Row/Col", Brushes.White, Brushes.Black, 15));
            columnNames.Children.Add(CreateTextBlock("Goal Row/Col", Brushes.White, Brushes.Black, 15));
            columnNames.Children.Add(CreateTextBlock("Curr. Address", Brushes.White, Brushes.Black, 15));
            columnNames.Children.Add(CreateTextBlock("Mate", Brushes.White, Brushes.Black, 5));

            RootBotLocationTable.Items.Add(columnNames);
            
            order.Add(-1, new List<Tuple<TextBlock, TextBlock>>());
            foreach (IBotInfo bot in _instanceInfo.GetInfoMovableStations())
            {
                // allocate rows of the table
                tableRows.Add(bot, new WrapPanel() { Orientation = Orientation.Horizontal });
                RootBotLocationTable.Items.Add(tableRows[bot]);

                botOrder[bot] = -1;

                // add row names
                TextBlock rowName = CreateTextBlock("Bot " + bot.GetInfoID().ToString(),
                    ColorManager.GenerateHueBrush(bot.GetInfoHue()), Brushes.Black, 6);
                tableRows[bot].Children.Add(rowName);

                // add state in the current row
                robotStates.Add(bot, CreateTextBlock("None", Brushes.White, Brushes.Black, 15));
                robotCurrentRowCol.Add(bot, CreateTextBlock("None", Brushes.White, Brushes.Black, 15));
                robotTargetRowCol.Add(bot, CreateTextBlock("None", Brushes.White, Brushes.Black, 15));
                robotGoalRowCol.Add(bot, CreateTextBlock("None", Brushes.White, Brushes.Black, 15));
                robotCurrAddresses.Add(bot, CreateTextBlock("-----", Brushes.White, Brushes.Black, 15));
                robotCurrMates.Add(bot, CreateTextBlock("None", Brushes.White, Brushes.Black, 5));
                tableRows[bot].Children.Add(robotStates[bot]);
                tableRows[bot].Children.Add(robotCurrentRowCol[bot]);
                tableRows[bot].Children.Add(robotTargetRowCol[bot]);
                tableRows[bot].Children.Add(robotGoalRowCol[bot]);
                tableRows[bot].Children.Add(robotCurrAddresses[bot]);
                tableRows[bot].Children.Add(robotCurrMates[bot]);
            }
        }
        private TextBlock CreateTextBlock(string text, Brush back, Brush fore, int padRight)
        {
            TextBlock textBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
            textBlock.Background = back;
            textBlock.Foreground = fore;
            textBlock.Text = text.PadRight(padRight).Substring(0, padRight);
            return textBlock;
        }
        public void Update()
        {
            foreach (var bot in _instanceInfo.GetInfoMovableStations())
            {
                robotStates[bot].Text = bot.GetInfoState() == null ? ("None").PadRight(15).Substring(0, 15) : bot.GetInfoState().PadRight(15).Substring(0, 15);
                
                robotCurrentRowCol[bot].Text = bot.GetInfoCurrentWaypoint() == null ? ("None").PadRight(15).Substring(0, 15) : bot.GetInfoCurrentWaypoint().GetInfoRowColumn().PadRight(15).Substring(0, 15);
                robotTargetRowCol[bot].Text = bot.GetInfoDestinationWaypoint() == null ? ("None").PadRight(15).Substring(0, 15) : bot.GetInfoDestinationWaypoint().GetInfoRowColumn().PadRight(15).Substring(0, 15);
                robotGoalRowCol[bot].Text = bot.GetInfoGoalWaypoint() == null ? ("None").PadRight(15).Substring(0, 15) : bot.GetInfoGoalWaypoint().GetInfoRowColumn().PadRight(15).Substring(0, 15);

                Tuple<string, int> addressAndMate = bot.GetCurrentItemAddressAndMate();

                //List<Tuple<string, bool, int, bool>> statusTable = bot.GetStatus();
                robotCurrAddresses[bot].Text = addressAndMate.Item1.PadRight(15);
                //robotCurrAddresses[bot].Background = bot.GetCurrentItemAddress(); 
                robotCurrMates[bot].Text = addressAndMate.Item2 == -1 ? ("None").PadLeft(5) : addressAndMate.Item2.ToString().PadLeft(5);
            }
        }
    }
    #endregion

    #region Status table manager

    public class SimulationVisualStatusTableManager
    {
        // Root with the open/close triangle
        public TreeViewItem RootStatusTable { get; private set; }
        private IInstanceInfo _instaceInfo;

        // Connects each bot to the corresponding list of blocks
        private Dictionary<IBotInfo, WrapPanel> tableRows = new Dictionary<IBotInfo, WrapPanel>();

        private Dictionary<IBotInfo, int> botOrder = new Dictionary<IBotInfo, int>();
        private Dictionary<int, List<Tuple<TextBlock,TextBlock>>> order = new Dictionary<int, List<Tuple<TextBlock, TextBlock>>>();

        public SimulationVisualStatusTableManager(TreeViewItem rootStatusTable, IInstanceInfo instanceInfo)
        {
            RootStatusTable = rootStatusTable;
            RootStatusTable.Header = "Status table";
            _instaceInfo = instanceInfo;

            // column names
            WrapPanel columnNames = new WrapPanel();
            columnNames.Children.Add(CreateTextBlock("Bot", Brushes.White, Brushes.Black, 6));
            
            RootStatusTable.Items.Add(columnNames);

            order.Add(-1, new List<Tuple<TextBlock, TextBlock>>());
            foreach (IBotInfo bot in _instaceInfo.GetInfoMovableStations())
            {
                // allocate rows of the table
                tableRows.Add(bot, new WrapPanel() { Orientation = Orientation.Horizontal });
                RootStatusTable.Items.Add(tableRows[bot]);

                botOrder[bot] = -1;

                // add row names
                TextBlock rowName = CreateTextBlock("Bot " + bot.GetInfoID().ToString(),
                    ColorManager.GenerateHueBrush(bot.GetInfoHue()), Brushes.Black, 6);
                tableRows[bot].Children.Add(rowName);

            }
        }

        private TextBlock CreateTextBlock(string text, Brush back, Brush fore, int padRight)
        {
            TextBlock textBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
            textBlock.Background = back;
            textBlock.Foreground = fore;
            textBlock.Text = text.PadRight(padRight).Substring(0,padRight);
            return textBlock;
        }

        public void Update()
        {
            //  Update states
            foreach (var bot in _instaceInfo.GetInfoMovableStations())
            {
                // if bot has order
                int newOrderID = _instaceInfo.GetStatusTableOrderID(bot.GetInfoID());
                int oldOrderID = botOrder[bot];
                if (newOrderID != oldOrderID) 
                {
                    // clear previos values
                    foreach(var am in order[oldOrderID])
                    {
                        tableRows[bot].Children.Remove(am.Item2);
                        tableRows[bot].Children.Remove(am.Item1);
                    }
                    if (tableRows[bot].Children.Count > 1)
                        tableRows[bot].Children.RemoveAt(1);
                    // remove also from order if it is not dummy order (-1)
                    if (oldOrderID != -1) order.Remove(oldOrderID);
                    order.Add(newOrderID, new List<Tuple<TextBlock, TextBlock>>());
                    botOrder[bot] = newOrderID;

                    List<string> addresses = _instaceInfo.GetStatusTableOrderAddresses(bot.GetInfoID());

                    TextBlock orderIDblock = CreateTextBlock(newOrderID.ToString(), Brushes.DarkGray, Brushes.White, 4);
                    tableRows[bot].Children.Add(orderIDblock);

                    for (int i = 0; i < addresses.Count; ++i)
                    {
                        order[newOrderID].Add(
                            new Tuple<TextBlock, TextBlock>(
                                CreateTextBlock(addresses[i], Brushes.White, Brushes.Black, 5),
                                CreateTextBlock("-1", Brushes.LightGray, Brushes.Black, 2)
                            )
                        );
                        tableRows[bot].Children.Add(order[newOrderID][i].Item1);
                        tableRows[bot].Children.Add(order[newOrderID][i].Item2);
                    }
                }    
                else
                {
                    for (int i = 0; i < order[botOrder[bot]].Count; ++i)
                    {
                        Tuple<bool, int, bool, bool> info = _instaceInfo.GetStatusTableInfoOnItem(bot.GetInfoID(), i);
                        bool opened = info.Item1;
                        bool completed = info.Item3;
                        int mateID = info.Item2;
                        bool locked = info.Item4;
                        order[botOrder[bot]][i].Item2.Text = mateID.ToString().PadLeft(2);

                        order[botOrder[bot]][i].Item2.Foreground = completed ? Brushes.Gray : Brushes.Black;
                        order[botOrder[bot]][i].Item2.Background = completed ? Brushes.LightGray : (mateID == -1 ? Brushes.LightGray : ColorManager.GenerateHueBrush(_instaceInfo.GetInfoBotHue(mateID)));
                        order[botOrder[bot]][i].Item1.Background = completed ? Brushes.Gray : (opened ? (locked ? Brushes.Black : Brushes.DarkBlue) : Brushes.White);
                        order[botOrder[bot]][i].Item1.Foreground = completed ? Brushes.Black : (opened ? Brushes.White : Brushes.Black);
                    }
                }
            }
        }

    }

    #endregion

    #region Order manager

    public class SimulationVisualOrderManager
    {
        public SimulationVisualOrderManager(TreeViewItem rootOpenOrders, string headerOpenOrders, TreeViewItem rootCompletedOrders, string headerCompletedOrders, int orderCount)
        {
            RootOpenOrders = rootOpenOrders;
            RootCompletedOrders = rootCompletedOrders;
            _headerOpenOrders = headerOpenOrders;
            _headerCompletedOrders = headerCompletedOrders;
            _orderCount = orderCount;
        }
        public SimulationVisualOrderManager(TreeViewItem rootOpenOrders, string headerOpenOrders, TreeViewItem rootCompletedOrders, string headerCompletedOrders, TreeViewItem rootAvailableOrders, string headerAvailableOrders, int orderCount)
        {
            RootOpenOrders = rootOpenOrders;
            RootCompletedOrders = rootCompletedOrders;
            RootAvailableOrders = rootAvailableOrders;
            _headerOpenOrders = headerOpenOrders;
            _headerCompletedOrders = headerCompletedOrders;
            _headerAvailableOrders = headerAvailableOrders;
            _orderCount = orderCount;
        }

        public TreeViewItem RootOpenOrders { get; private set; }
        public TreeViewItem RootCompletedOrders { get; private set; }
        public TreeViewItem RootAvailableOrders { get; private set; }

        private int _orderCount;
        private int _maxDisplayedAvailableOrders = 10;
        private List<IOrderInfo> _droppedOrders = new List<IOrderInfo>();
        private List<IOrderInfo> _completedOrders = new List<IOrderInfo>();
        private List<IOrderInfo> _openOrders = new List<IOrderInfo>();
        private List<IOrderInfo> _availableOrdersDisplayed = new List<IOrderInfo>();
        private int _openOrderCount;
        private int _completedOrderCount;
        private int _availableOrderCount;
        private string _headerOpenOrders;
        private string _headerCompletedOrders;
        private string _headerAvailableOrders;
        private Dictionary<IOrderInfo, bool> _orderStatusOpen = new Dictionary<IOrderInfo, bool>();
        private Dictionary<IOrderInfo, bool> _orderStatusAvailable = new Dictionary<IOrderInfo, bool>();
        private Dictionary<IOrderInfo, WrapPanel> _orderControls = new Dictionary<IOrderInfo, WrapPanel>();
        private Dictionary<IOrderInfo, WrapPanel> _availableOrderControls = new Dictionary<IOrderInfo, WrapPanel>();
        private Dictionary<IOrderInfo, TextBlock> _botControls = new Dictionary<IOrderInfo, TextBlock>();
        private Dictionary<IOrderInfo, Dictionary<IItemDescriptionInfo, TextBlock>> _itemControls = new Dictionary<IOrderInfo, Dictionary<IItemDescriptionInfo, TextBlock>>();

        private TextBlock CreateTextBlock(string text, Brush back, Brush fore, int padRight)
        {
            TextBlock textBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
            textBlock.Background = back;
            textBlock.Foreground = fore;
            textBlock.Text = text.PadRight(padRight).Substring(0,padRight);
            return textBlock;
        }
        public void Update(IEnumerable<IOrderInfo> openOrders, IEnumerable<IOrderInfo> completedOrders)
        {
            // Check open orders if there is a new one.
            // If yes, create a new control for the order.
            foreach (var order in openOrders)
            {
                if (!_orderStatusOpen.ContainsKey(order))
                {
                    // Create a container for this order
                    _orderControls[order] = new WrapPanel() { Orientation = Orientation.Horizontal };
                    RootOpenOrders.Items.Add(_orderControls[order]);
                    // Create the controls for every element of the order
                    _itemControls[order] = new Dictionary<IItemDescriptionInfo, TextBlock>();

                    TextBlock botBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
                    botBlock.Background = ColorManager.GenerateHueBrush(order.GetAssignedMovableStationHue());
                    botBlock.Foreground = Brushes.Black;
                    botBlock.Text = ("Bot " + order.GetAssignedMovableStationID().ToString()).PadLeft(4);
                    _botControls[order] = botBlock;
                    _orderControls[order].Children.Add(botBlock);
                    TextBlock orderIDblock = CreateTextBlock(order.ID.ToString(), Brushes.DarkGray, Brushes.White, 4);
                    _orderControls[order].Children.Add(orderIDblock);
                    foreach (var position in order.GetInfoPositions())
                    {
                        TextBlock positionBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
                        if (position is IColoredLetterDescriptionInfo)
                        {
                            IColoredLetterDescriptionInfo itemDescription = position as IColoredLetterDescriptionInfo;
                            positionBlock.Background = VisualizationConstants.LetterColorBackgroundBrushes[itemDescription.GetInfoColor()];
                            positionBlock.Foreground = VisualizationConstants.LetterColorIncomplete;
                            positionBlock.Text = itemDescription.GetInfoLetter() + "(0/" + order.GetInfoDemandCount(position) + ")";
                        }
                        else
                        {
                            if (position is ISimpleItemDescriptionInfo)
                            {
                                ISimpleItemDescriptionInfo itemDescription = position as ISimpleItemDescriptionInfo;
                                positionBlock.Background = Brushes.White;
                                positionBlock.Foreground = Brushes.Gray;
                                positionBlock.Text = itemDescription.GetLocation();
                            }
                            else
                            {
                                positionBlock.Text = position.GetInfoDescription() + "(0/" + order.GetInfoDemandCount(position) + ")";
                            }
                        }
                        _itemControls[order][position] = positionBlock;
                        _orderControls[order].Children.Add(positionBlock);
                    }
                    // Add the order to the list and mark it as open
                    _openOrders.Add(order);
                    _openOrderCount++;
                    _orderStatusOpen[order] = true;
                }
            }

            // Update served quantities of existing orders
            foreach (var order in _openOrders)
            {
                // Refresh order status
                if (order.GetInfoIsCompleted())
                    _orderStatusOpen[order] = false;

                // Refresh available order status
                if (_orderStatusAvailable.ContainsKey(order) && _orderStatusAvailable[order] == true)
                {
                    RootAvailableOrders.Items.Remove(_availableOrderControls[order]);
                    _availableOrderControls.Remove(order);
                    _availableOrdersDisplayed.Remove(order);
                    _orderStatusAvailable[order] = false;
                    continue;
                }

                // Update all positions
                foreach (var position in order.GetInfoPositions())
                {
                    if (position is IColoredLetterDescriptionInfo)
                    {
                        // Update the position's text (use the coloring too)
                        IColoredLetterDescriptionInfo itemDescription = position as IColoredLetterDescriptionInfo;
                        _itemControls[order][position].Text = itemDescription.GetInfoLetter() + "(" + order.GetInfoServedCount(position).ToString() + "/" + order.GetInfoDemandCount(position).ToString() + ")";
                    }
                    else
                    {
                        if (position is ISimpleItemDescriptionInfo)
                        {
                            _botControls[order].Text = ("Bot " + order.GetAssignedMovableStationID().ToString()).PadLeft(4);
                            _botControls[order].Background = ColorManager.GenerateHueBrush(order.GetAssignedMovableStationHue());
                            ISimpleItemDescriptionInfo itemDescription = position as ISimpleItemDescriptionInfo;
                            _itemControls[order][position].Text = itemDescription.GetLocation();
                        }
                        else
                        {
                            // Update the position's text
                            _itemControls[order][position].Text = position.GetInfoDescription() + "(" + order.GetInfoServedCount(position).ToString() + "/" + order.GetInfoDemandCount(position).ToString() + ")";
                        }
                    }
                    // Set color according to completed position
                    _itemControls[order][position].Foreground =
                        order.GetInfoServedCount(position) == order.GetInfoDemandCount(position) ? // Check whether position is complete
                        Brushes.Black : Brushes.Black;
                    _itemControls[order][position].Background =
                        order.GetInfoServedCount(position) == order.GetInfoDemandCount(position) ? // Check whether position is complete
                        Brushes.LightGray : Brushes.White;
                }
            }

            // Move newly completed orders from open to complete node
            foreach (var order in _openOrders.Where(o => !_orderStatusOpen[o]).ToList())
            {
                _openOrders.Remove(order);
                _completedOrders.Add(order);
                RootOpenOrders.Items.Remove(_orderControls[order]);
                RootCompletedOrders.Items.Insert(0, _orderControls[order]);
                _openOrderCount--;
                _completedOrderCount++;
            }

            // Remove completed orders if list gets too long
            if (_completedOrders.Count > _orderCount)
            {
                // Manage the list of the currently displayed orders
                List<IOrderInfo> removedOrders = _completedOrders.Take(_completedOrders.Count - _orderCount).ToList();
                _droppedOrders.AddRange(removedOrders);
                _completedOrders.RemoveRange(0, _completedOrders.Count - _orderCount);
                foreach (var removedOrder in removedOrders)
                    RootCompletedOrders.Items.Remove(_orderControls[removedOrder]);
                // Remove the controls
                foreach (var removedOrder in removedOrders)
                {
                    foreach (var position in removedOrder.GetInfoPositions())
                        _itemControls[removedOrder].Remove(position);
                    _itemControls.Remove(removedOrder);
                    _orderControls.Remove(removedOrder);
                }
            }

            // Update order count info
            RootOpenOrders.Header = _headerOpenOrders + " (" + _openOrderCount + ")";
            RootCompletedOrders.Header = _headerCompletedOrders + " (" + _completedOrderCount + ")";

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="openOrders"></param>
        /// <param name="completedOrders"></param>
        /// <param name="availableOrders"></param>
        /// <param name="tot"></param>
        public void Update(IEnumerable<IOrderInfo> openOrders, IEnumerable<IOrderInfo> completedOrders, IEnumerable<IOrderInfo> availableOrders)
        {
            #region AvailableOrders
            // Add new controls for the available orders
            foreach (var order in availableOrders.Take(_maxDisplayedAvailableOrders))
            {
                if (!_orderStatusAvailable.ContainsKey(order) && _availableOrdersDisplayed.Count() < _maxDisplayedAvailableOrders)
                {
                    // Create a container for this order
                    _availableOrderControls[order] = new WrapPanel() { Orientation = Orientation.Horizontal };
                    RootAvailableOrders.Items.Add(_availableOrderControls[order]);
                    // Create the controls for every element of the order
                    _itemControls[order] = new Dictionary<IItemDescriptionInfo, TextBlock>();

                    TextBlock botBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
                    botBlock.Background = Brushes.White;
                    botBlock.Foreground = Brushes.Black;
                    botBlock.Text = "     ";
                    _botControls[order] = botBlock;
                    _availableOrderControls[order].Children.Add(botBlock);
                    TextBlock orderIDblock = CreateTextBlock(order.ID.ToString(), Brushes.DarkGray, Brushes.White, 4);
                    _availableOrderControls[order].Children.Add(orderIDblock);
                    foreach (var position in order.GetInfoPositions())
                    {
                        TextBlock positionBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
                        if (position is IColoredLetterDescriptionInfo)
                        {
                            IColoredLetterDescriptionInfo itemDescription = position as IColoredLetterDescriptionInfo;
                            positionBlock.Background = Brushes.White;
                            positionBlock.Foreground = Brushes.Black;
                            positionBlock.Text = itemDescription.GetInfoLetter() + "(0/" + order.GetInfoDemandCount(position) + ")";
                        }
                        else
                        {
                            if (position is ISimpleItemDescriptionInfo)
                            {
                                ISimpleItemDescriptionInfo itemDescription = position as ISimpleItemDescriptionInfo;
                                positionBlock.Background = Brushes.White;
                                positionBlock.Foreground = Brushes.Black;
                                positionBlock.Text = itemDescription.GetLocation();
                            }
                            else
                            {
                                positionBlock.Text = position.GetInfoDescription() + "(0/" + order.GetInfoDemandCount(position) + ")";
                            }
                        }
                        _itemControls[order][position] = positionBlock;
                        _availableOrderControls[order].Children.Add(positionBlock);
                    }
                    // Add the order to the list and mark it as open
                    _availableOrdersDisplayed.Add(order);
                    _orderStatusAvailable[order] = true;
                }
            }

            _availableOrderCount = availableOrders.Count();
            RootAvailableOrders.Header = _headerAvailableOrders + " (" + _availableOrderCount + ")";

            #endregion AvailableOrders

            Update(openOrders, completedOrders);
        }

    }

    #endregion

    #region Bundle manager

    public class SimulationVisualBundleManager
    {
        public SimulationVisualBundleManager(TreeViewItem contentHost) { _contentHost = contentHost; }
        private TreeViewItem _contentHost;
        private Dictionary<IItemDescriptionInfo, TextBlock> _blocksContent = new Dictionary<IItemDescriptionInfo, TextBlock>();
        private Dictionary<IItemDescriptionInfo, int> _contentCount = new Dictionary<IItemDescriptionInfo, int>();

        public void UpdateContentInfo(IItemBundleInfo[] bundles)
        {
            // Update content info
            _contentCount.Clear();
            // Update item-count information
            foreach (var item in bundles)
            {
                if (!_contentCount.ContainsKey(item.GetInfoItemDescription()))
                    _contentCount[item.GetInfoItemDescription()] = item.GetInfoItemCount();
                else
                    _contentCount[item.GetInfoItemDescription()] += item.GetInfoItemCount();
            }
            // Update the visual controls
            foreach (var item in bundles)
            {
                // Check whether it is a new item
                if (!_blocksContent.ContainsKey(item.GetInfoItemDescription()))
                {
                    // New item - init visual control
                    TextBlock textBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
                    if (item.GetInfoItemDescription() is IColoredLetterDescriptionInfo)
                    {
                        IColoredLetterDescriptionInfo itemDescription = item.GetInfoItemDescription() as IColoredLetterDescriptionInfo;
                        textBlock.Background = VisualizationConstants.LetterColorBackgroundBrushes[itemDescription.GetInfoColor()];
                        textBlock.Foreground = VisualizationConstants.LetterColorComplete;
                        textBlock.Text = itemDescription.GetInfoLetter() + "/" + _contentCount[itemDescription];
                    }
                    else
                    {
                        if (item.GetInfoItemDescription() is ISimpleItemDescriptionInfo)
                        {
                            ISimpleItemDescriptionInfo itemDescription = item.GetInfoItemDescription() as ISimpleItemDescriptionInfo;
                            textBlock.Background = ColorManager.GenerateHueBrush(itemDescription.GetInfoHue());
                            textBlock.Foreground = VisualizationConstants.SimpleItemColorComplete;
                            textBlock.Text =
                                (itemDescription.GetInfoID().ToString() + "(" + _contentCount[itemDescription] + ")").PadBoth(VisualizationConstants.SIMPLE_ITEM_BUNDLE_MIN_CHAR_COUNT);
                        }
                        else
                        {
                            textBlock.Text = item.GetInfoItemDescription().GetInfoDescription();
                        }
                    }
                    // Add the control
                    _blocksContent[item.GetInfoItemDescription()] = textBlock;
                    _contentHost.Items.Add(textBlock);
                }
                else
                {
                    // Visual control already exists - grab and update it
                    if (item.GetInfoItemDescription() is IColoredLetterDescriptionInfo)
                    {
                        IColoredLetterDescriptionInfo itemDescription = item.GetInfoItemDescription() as IColoredLetterDescriptionInfo;
                        _blocksContent[item.GetInfoItemDescription()].Text = itemDescription.GetInfoLetter() + "/" + _contentCount[itemDescription];
                    }
                    else
                    {
                        if (item.GetInfoItemDescription() is ISimpleItemDescriptionInfo)
                        {
                            ISimpleItemDescriptionInfo itemDescription = item.GetInfoItemDescription() as ISimpleItemDescriptionInfo;
                            _blocksContent[item.GetInfoItemDescription()].Text =
                                (itemDescription.GetInfoID().ToString() + "(" + _contentCount[itemDescription] + ")").PadBoth(VisualizationConstants.SIMPLE_ITEM_BUNDLE_MIN_CHAR_COUNT);
                        }
                        else
                        {
                            _blocksContent[item.GetInfoItemDescription()].Text = item.GetInfoItemDescription().GetInfoDescription();
                        }
                    }
                }
            }
            // Remove controls showing items not present in the pod anymore
            foreach (var itemDescription in _blocksContent.Keys.Except(bundles.Select(b => b.GetInfoItemDescription())).ToArray())
            {
                _contentHost.Items.Remove(_blocksContent[itemDescription]);
                _blocksContent.Remove(itemDescription);
            }
        }
    }

    #endregion

    #region Content manager

    public class SimulationVisualContentManager
    {
        public SimulationVisualContentManager(TreeViewItem contentHost, IPodInfo pod) { _contentHost = contentHost; _pod = pod; }
        private IPodInfo _pod;
        private TreeViewItem _contentHost;
        private Dictionary<IItemDescriptionInfo, TextBlock> _blocksContent = new Dictionary<IItemDescriptionInfo, TextBlock>();

        public void UpdateContentInfo()
        {
            // Update the visual controls
            foreach (var item in _pod.GetInfoInstance().GetInfoItemDescriptions())
            {
                // Check whether the item is contained in the pod
                if (_pod.GetInfoContent(item) > 0)
                {
                    // Check whether it is a new item
                    if (!_blocksContent.ContainsKey(item))
                    {
                        // New item - init visual control
                        TextBlock textBlock = new TextBlock() { Padding = new Thickness(3), FontFamily = VisualizationConstants.ItemFont };
                        if (item is IColoredLetterDescriptionInfo)
                        {
                            IColoredLetterDescriptionInfo itemDescription = item as IColoredLetterDescriptionInfo;
                            textBlock.Background = VisualizationConstants.LetterColorBackgroundBrushes[itemDescription.GetInfoColor()];
                            textBlock.Foreground = VisualizationConstants.LetterColorComplete;
                            textBlock.Text = itemDescription.GetInfoLetter() + "/" + _pod.GetInfoContent(itemDescription);
                        }
                        else
                        {
                            if (item is ISimpleItemDescriptionInfo)
                            {
                                ISimpleItemDescriptionInfo itemDescription = item as ISimpleItemDescriptionInfo;
                                textBlock.Background = ColorManager.GenerateHueBrush(itemDescription.GetInfoHue());
                                textBlock.Foreground = VisualizationConstants.SimpleItemColorComplete;
                                textBlock.Text =
                                    (itemDescription.GetInfoID().ToString() + "(" + _pod.GetInfoContent(itemDescription) + ")").PadBoth(VisualizationConstants.SIMPLE_ITEM_BUNDLE_MIN_CHAR_COUNT);
                            }
                            else
                            {
                                textBlock.Text = item.GetInfoDescription();
                            }
                        }
                        // Add the control
                        _blocksContent[item] = textBlock;
                        _contentHost.Items.Add(textBlock);
                    }
                    else
                    {
                        // Visual control already exists - grab and update it
                        if (item is IColoredLetterDescriptionInfo)
                        {
                            IColoredLetterDescriptionInfo itemDescription = item as IColoredLetterDescriptionInfo;
                            _blocksContent[item].Text = itemDescription.GetInfoLetter() + "/" + _pod.GetInfoContent(itemDescription);
                        }
                        else
                        {
                            if (item is ISimpleItemDescriptionInfo)
                            {
                                ISimpleItemDescriptionInfo itemDescription = item as ISimpleItemDescriptionInfo;
                                _blocksContent[item].Text =
                                    (itemDescription.GetInfoID().ToString() + "(" + _pod.GetInfoContent(itemDescription) + ")").PadBoth(VisualizationConstants.SIMPLE_ITEM_BUNDLE_MIN_CHAR_COUNT);
                            }
                            else
                            {
                                _blocksContent[item].Text = item.GetInfoDescription();
                            }
                        }
                    }
                }
            }
            // Remove controls showing items not present in the pod anymore
            foreach (var item in _pod.GetInfoInstance().GetInfoItemDescriptions())
            {
                if (_pod.GetInfoContent(item) <= 0 && _blocksContent.ContainsKey(item))
                {
                    _contentHost.Items.Remove(_blocksContent[item]);
                    _blocksContent.Remove(item);
                }
            }
        }
    }

    #endregion
}
