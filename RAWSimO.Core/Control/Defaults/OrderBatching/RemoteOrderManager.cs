using System;
using System.Collections.Generic;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Items;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using RAWSimO.Core.Configurations;
using System.IO;

namespace RAWSimO.Core.Control.Defaults.OrderBatching
{
    class RemoteOrderManager : OrderManager
    {

        private HttpClient _client;
        private RemoteOrderSchedulerConfiguration _config;
        private int no_bots;
        private int no_pickers;
        private int bot_item_shuffling;
        private int order_shuffling;
        private int picker_reassignment;
        public class Warehouse
        {
            public List<int> orders = new List<int>();
            public List<int> bots = new List<int>();
        }

        public class AddressToAccess
        {
            public string adr { get; set; }
            public string access_point{ get; set; }
        }

        public class InputOrderBatch
        {
            public InputOrderBatch(int no_bots_, int no_pickers_, int order_shuffling_, int bot_item_shuffling_, int picker_reassignment_)
            {
                no_pickers = no_pickers_;
                no_bots = no_bots_;
                orders = new List<OrderInfo>();
                pickers = new List<PickerInfo>();
                bots = new List<BotInfo>();
                // temporary info until we include complete bot info
                availableBots = new List<int>();
                bot_item_shuffling = bot_item_shuffling_;
                order_shuffling = order_shuffling_;
                picker_reassignment = picker_reassignment_;
                caller_id = -1;
            }
            public int no_bots;
            public int no_pickers;
            public int bot_item_shuffling;
            public int order_shuffling;
            public int picker_reassignment;
            public int caller_id;
            public List<OrderInfo> orders { get; set; }
            public List<PickerInfo> pickers { get; set; }
            public List<BotInfo> bots { get; set; }
            public List<int> availableBots { get; set; }
        }

        public class ItemList
        {
            public int order_id;
            public List<string> items;
        }

        public class OutputOrderSchedule
        {
            public List<int> orders { get; set; }
            public List<OrderInfo> items { get; set; }
            public List<OrderInfo> assigned { get; set; }
            public List<PickerSchedule> picker_schedules { get; set; }

        }
        /// <summary>
        /// Logger path
        /// </summary>
        public string loggerPath;
        /// <summary>
        /// Logger object defined by loggerPath.
        /// </summary>
        public static StreamWriter Logger = null;

        public RemoteOrderManager(Instance instance) : base(instance)
        {
            _config = instance.ControllerConfig.OrderBatchingConfig as RemoteOrderSchedulerConfiguration;
            _client = new HttpClient();
            _client.BaseAddress = new Uri(_config.Host + ":" + _config.Port);
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(_config.GetBodyType()));
            no_bots = instance.layoutConfiguration.MovableStationCount;
            no_pickers = instance.layoutConfiguration.MateBotCount;
            order_shuffling = instance.SettingConfig.orderShuffling == true ? 1 : 0;
            bot_item_shuffling = instance.SettingConfig.botItemShuffling == true ? 1 : 0;
            picker_reassignment = instance.SettingConfig.pickerReassignment == true ? 1 : 0;
            Instance = instance;
            if (Logger != null)
                Logger.Close();
            loggerPath = instance.layoutConfiguration.InitSwarmLog("Optimization", "Opt", out Logger);
            InitOptimization();
        }
        public class OptInit
        {
            public OptInit(string working_dir_, Instance inst)
            {
                working_dir = working_dir_;
                addressToAccesses = new List<AddressToAccess>();
                foreach (string adr in inst.addressToAccessPoint.Keys)
                {
                    AddressToAccess aTa = new AddressToAccess();
                    aTa.adr = adr;
                    int wpID = inst.addressToAccessPoint[adr];
                    Waypoints.Waypoint wp = inst.GetWaypointByID(wpID);
                    aTa.access_point = inst.AccessPointsArray[wp.Row][wp.Column];
                    addressToAccesses.Add(aTa);
                }
                robots = new List<int>();
                foreach (var bot in inst.MovableStations)
                {
                    robots.Add(bot.ID);
                }
                
                max_robot_velocity = inst.layoutConfiguration.MaxVelocity;
                max_robot_acceleration = inst.layoutConfiguration.MaxAcceleration;
                max_robot_decelaration = inst.layoutConfiguration.MaxDeceleration;
                max_robot_angular_velocity = inst.layoutConfiguration.TurnSpeed;

                pickers = new List<int>();
                foreach (var picker in inst.MateBots)
                {
                    pickers.Add(picker.ID);
                }
                max_picker_velocity = inst.layoutConfiguration.MaxMateVelocity;
            }
            public string working_dir;
            public List<AddressToAccess> addressToAccesses { get; set; }
            public List<int> pickers;
            public double max_picker_velocity;

            public List<int> robots;
            public double max_robot_velocity;
            public double max_robot_acceleration;
            public double max_robot_decelaration;
            public double max_robot_angular_velocity;
        }

        public class OptInitResponse
        {
            public string STATUS;
        }

        public void InitOptimization()
        {
            string working_dir = Instance.layoutConfiguration.warehouse.GetChosenMapDirectory();
            log("Initialize optimization");
            log(String.Format("Working directory: {0}", working_dir));
            OptInit optInit = new OptInit(working_dir, Instance);
            var json = JsonConvert.SerializeObject(optInit);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            _client.Timeout = TimeSpan.FromMinutes(20);
            
            var response = _client.PostAsync("init", data).Result;
            OptInitResponse initStatus = JsonConvert.DeserializeObject<OptInitResponse>(response.Content.ReadAsStringAsync().Result);
            log(initStatus.STATUS);
            log("");
            log("");
        }

        public override void SignalCurrentTime(double currentTime) { }

        protected override void DecideAboutPendingOrders()
        {
            if (!optimizationInfo.reoptimizationFlag && _pendingOrders.Count == 0)
                return;

            foreach (var mate in Instance.MateBots)
            {
                Instance.Controller.OptimizationClient.schedule.UpdatePickerXY(mate.ID, mate.X, mate.Y);
            }

            foreach (var bot in Instance.MovableStations)
            {
                Instance.Controller.OptimizationClient.schedule.UpdateBotXY(bot.ID, bot.X, bot.Y);
                // TODO: check the origin of the predicted arrival time
                if (Instance.Controller.MateScheduler.AssistInfo.AssistanceLocations.ContainsKey(bot as Bot))
                {
                    if (Instance.SettingConfig.ExcludePalletStands) continue;
                    double predicted_time = Instance.Controller.PathManager.PredictArrivalTime(
                        bot, Instance.Controller.MateScheduler.AssistInfo.AssistanceLocations[bot].First.Value.Item1, false);
                    Instance.Controller.OptimizationClient.schedule.UpdateBotPredictedTime(
                        bot.ID, predicted_time - Instance.Controller.CurrentTime);
                }
            }

            List<MovableStation> availableStations = Instance.MovableStations.Where(s => (s.doneWithInputStation && s.AssignedOrders.Count() == 0)).ToList();
            if (availableStations.Count() == 0 && PendingOrdersCount > 0)
            {
                availableStations = Instance.MovableStations.Where(s => s.CapacityInUse == 0).ToList();
            }

            List<int> availableBots = new List<int>(availableStations.Select(x => x.ID).ToList());

            if (availableBots.Count == 0) GetItem();
            else
            {
                GetOrder(availableStations, availableBots);
                // TODO after getting order we need to call get item to get first item of that robot, but currently GetItem won't
                // work because original data has not been updated
            }
            
            Instance.Controller.OptimizationClient.isRecentlyUpdated = true;
        }

        public void GetItem()
        {
            var orderBatch = GetOrderBatch();

            // orders contain both pending and assigned orders
            // this if is true only when the last order is completed
            if (orderBatch.orders.Count == 0) return;

            orderBatch.availableBots = new List<int>();

            var json = JsonConvert.SerializeObject(orderBatch);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync("get_item", data).Result;

            var itemGetter = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content.ReadAsStringAsync().Result);
            if (itemGetter["item"] != "")
                Instance.Controller.OptimizationClient.schedule.Update(
                    optimizationInfo.botID, Instance.Controller.OptimizationClient.schedule.botOrderID[optimizationInfo.botID], itemGetter["item"]);

            var pickerSchedules = GetPickerSchedules(json);
            Instance.Controller.OptimizationClient.schedule.Update(pickerSchedules["picker_schedules"]);

            // reorders the OrderedItemList
            foreach (var ms in Instance.MovableStations)
            {
                if (ms.CurrentTask.Type.Equals(BotTaskType.MultiPointGatherTask) &&
                    ms.CapacityInUse != 0 && Instance.Controller.OptimizationClient.schedule.botOrder[ms.ID].Count() > 0)
                {
                    MultiPointGatherTask task = (MultiPointGatherTask)ms.CurrentTask;
                    task.Order.ReorderItemList(Instance.Controller.OptimizationClient.schedule.botOrder[ms.ID]);
                }
            }
        }

        private void GetOrder(List<MovableStation> availableStations, List<int> availableBots)
        {
            foreach (int botID in availableBots)
            {
                var orderBatch = GetOrderBatch();

                // orders contain both pending and assigned orders
                // this if is true only when the last order is completed
                if (orderBatch.orders.Count == 0) continue;

                orderBatch.availableBots = new List<int>() { botID };

                var json = JsonConvert.SerializeObject(orderBatch);
                var data = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = _client.PostAsync("get_order", data).Result;

                int orderId = int.Parse(JsonConvert.DeserializeObject<string>(response.Content.ReadAsStringAsync().Result));


                if (orderId >= 0)
                {
                    Order order = _pendingOrders.Where(x => x.ID == orderId).First();
                    List<string> addresses = new List<string>();
                    foreach (var item in order.GetLocationAddresses())
                    {
                        addresses.Add(item.GetAddress());
                    }

                    AllocateOrder(order, availableStations.Where(ms => ms.ID == botID).First());
                    Instance.Controller.OptimizationClient.schedule.Update(botID, orderId, addresses);
                }

                var pickerSchedules = GetPickerSchedules(json);
                Instance.Controller.OptimizationClient.schedule.Update(pickerSchedules["picker_schedules"]);
                // reorders the OrderedItemList
                foreach (var ms in Instance.MovableStations)
                {
                    if (ms.CurrentTask.Type.Equals(BotTaskType.MultiPointGatherTask) &&
                        ms.CapacityInUse != 0 && Instance.Controller.OptimizationClient.schedule.botOrder[ms.ID].Count() > 0)
                    {
                        MultiPointGatherTask task = (MultiPointGatherTask)ms.CurrentTask;
                        task.Order.ReorderItemList(Instance.Controller.OptimizationClient.schedule.botOrder[ms.ID]);
                    }
                }
            }
        }

        private InputOrderBatch GetOrderBatch()
        {
            InputOrderBatch orderBatch = new InputOrderBatch(no_bots, no_pickers, order_shuffling, bot_item_shuffling, picker_reassignment);

            orderBatch.pickers = Instance.Controller.OptimizationClient.schedule.pickers.Values.ToList();
            orderBatch.bots = Instance.Controller.OptimizationClient.schedule.bots.Values.ToList();

            for (int i = 0; i < Instance.MovableStations.Count; i++)
            {
                MovableStation ms = Instance.MovableStations[i];
                if (ms.CurrentTask.Type.Equals(BotTaskType.MultiPointGatherTask) &&
                    ((MultiPointGatherTask)ms.CurrentTask).Locations.Count > 0)
                // NOTE: this will exclude an item that was opened but still not assistance requested
                // that item is not present in botOrder, but is still not completed
                // it was removed when robot executed PPT
                //&&
                //Instance.Controller.OptimizationClient.schedule.botOrder[ms.ID].Count() > 0)
                {
                    MultiPointGatherTask task = (MultiPointGatherTask)ms.CurrentTask;
                    Order order = task.Order;
                    OrderInfo orderInfo = new OrderInfo();
                    orderInfo.order_id = order.ID;
                    orderInfo.bot_id = ms.ID;
                    orderInfo.deadline = 100000;
                    orderInfo.items = order.GetOpenLocationAddresses().Select(sid => sid.GetAddress()).ToList();
                    orderInfo.times = order.GetOpenLocationTimes();
                    //if (orderInfo.times == null)
                    //{
                    //    orderInfo.times = new List<double>();
                    //}
                    //Console.WriteLine("Partial " + orderInfo.index.ToString() + ", " + ms.ID + ": " + String.Join(", ", orderInfo.items));
                    if (orderInfo.items.Count > 0)
                        orderBatch.orders.Add(orderInfo);
                }
                else if (Instance.Controller.OptimizationClient.schedule.botOrder.ContainsKey(ms.ID) &&
                    ms.doneWithInputStation)
                // this happens only when optimization has just returned new order and another optimization is being called
                // just a few miliseconds after new order was given because simulator still hasnt given bot a multipointgathertask
                {
                    OrderInfo orderInfo = new OrderInfo();

                    orderInfo.order_id = Instance.Controller.OptimizationClient.schedule.botOrderID[ms.ID];
                    orderInfo.bot_id = ms.ID;
                    orderInfo.deadline = 100000;
                    orderInfo.items = Instance.Controller.OptimizationClient.schedule.botOrder[ms.ID];
                    orderInfo.times = new List<double>(new List<double>(new double[orderInfo.items.Count]));
                    //if (orderInfo.times == null)
                    //{
                    //    orderInfo.times = new List<double>();
                    //}
                    //Console.WriteLine("Partial " + orderInfo.index.ToString() + ", " + ms.ID + ": " + String.Join(", ", orderInfo.items));
                    if (orderInfo.items.Count > 0)
                        orderBatch.orders.Add(orderInfo);
                }
            }

            for (int i = 0; i < Math.Min(_pendingOrders.Count, 100); ++i)
            {
                Order order = _orders[i];
                OrderInfo orderInfo = new OrderInfo();
                orderInfo.order_id = order.ID;
                orderInfo.bot_id = -1;
                orderInfo.deadline = 100000;
                orderInfo.items = order.GetLocationAddresses().Select(sid => sid.GetAddress()).ToList();
                orderInfo.times = order.GetLocationTimes();
                //Console.WriteLine("Full " + orderInfo.index.ToString() + ": " + String.Join(", ", orderInfo.items));
                orderBatch.orders.Add(orderInfo);
            }
            orderBatch.caller_id = optimizationInfo.botID;
            return orderBatch;
        }

        private Dictionary<string, List<PickerSchedule>> GetPickerSchedules(string json)
        {
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response_pickers = _client.PostAsync("get_picker_schedules", data).Result;
            return JsonConvert.DeserializeObject<Dictionary<string, List<PickerSchedule>>>(response_pickers.Content.ReadAsStringAsync().Result);
        }
        public class CommData
        {
            public Dictionary<int, List<string>> assigned;
            public Dictionary<int, List<string>> new_orders;
            public Dictionary<int, PickerInfo> picker_info;
            public Dictionary<int, BotInfo> bot_info;
            public Dictionary<int, List<PickerAssignment>> picker_schedule;
            public List<int> availableBots;


            public CommData()
            {
                assigned = new Dictionary<int, List<string>>();
                picker_info = new Dictionary<int, PickerInfo>();
                bot_info = new Dictionary<int, BotInfo>();
                picker_schedule = new Dictionary<int, List<PickerAssignment>>();
                new_orders = new Dictionary<int, List<string>>();
            }
        }
        public bool OptimizationCheck(Instance inst, InputOrderBatch req, OutputOrderSchedule resp)
        {
            CommData inputData = new CommData();
            CommData outputData = new CommData();
            inputData.availableBots = req.availableBots;
            foreach (var oi in req.orders)
            {
                if (oi.bot_id != -1)
                    inputData.assigned.Add(oi.bot_id, oi.items);
            }
            foreach (var oi in resp.assigned)
            {
                //if (oi.bot_id != -1)
                outputData.assigned.Add(oi.bot_id, oi.items);
            }
            foreach (var oi in resp.items)
            {
                //// TODO: remove when check is included
                //if (oi.bot_id != -1)
                    outputData.new_orders.Add(oi.bot_id, oi.items);
            }
            foreach (var p in req.pickers)
            {
                inputData.picker_info.Add(p.picker_id, p);
            }
            foreach (var b in req.bots)
            {
                inputData.bot_info.Add(b.bot_id, b);
            }
            foreach (var ps in resp.picker_schedules)
            {
                outputData.picker_schedule.Add(ps.picker_id, ps.schedule);
            }
            PrintLog(inst, inputData, outputData);
            // check first item is the same
            foreach (int id in inputData.assigned.Keys)
            {
                List<string> input_items = inputData.assigned[id];
                if (!outputData.assigned.ContainsKey(id))
                    ThrowException(String.Format("Robot {0} had assignment in request, but no assignment in response", id));
                List<string> output_items = outputData.assigned[id];
                if (input_items.Count() == 0)
                    ThrowException(String.Format("Robot {0} has 0 items in input", id));
                if (output_items.Count() == 0)
                    ThrowException(String.Format("Robot {0} has 0 items in output", id));
                string input_first_item = input_items.First();
                string output_first_item = output_items.First();
                // if robot opened an item but optimization changed the order (this excludes when robot is still in INPUT state)
                if (inputData.bot_info[id].item == input_first_item && input_first_item != output_first_item)
                    ThrowException(String.Format("Robot {0} has different first item: input({1}) != output({2})", id, input_first_item, output_first_item));
                List<string> io_items_difference = input_items.Except(output_items).Concat(output_items.Except(input_items)).ToList();
                if (io_items_difference.Count() > 0)
                    ThrowException(String.Format("Robot {0} has different elements in the list: {1}", id, String.Join(", ", io_items_difference)));
            }
            foreach (int id in inputData.picker_info.Keys)
            {
                string input_picker_item = inputData.picker_info[id].item;
                double input_picker_item_status = inputData.picker_info[id].item_status;
                int input_picker_bot = inputData.picker_info[id].bot_id;
                if (!outputData.picker_schedule.ContainsKey(id) || outputData.picker_schedule[id].Count() == 0)
                    continue;
                string output_picker_first_item = outputData.picker_schedule[id].First().item;
                int output_picker_bot = outputData.picker_schedule[id].First().bot_id;
                if (input_picker_item_status >= 0 && input_picker_item_status < 1)
                {
                    if (input_picker_item != output_picker_first_item || input_picker_bot != output_picker_bot)
                        ThrowException(String.Format(
                            "Picker {0} started item ({1},{2}) but was changed to ({3,4}) without completing first.",
                            id, input_picker_item, input_picker_bot, output_picker_first_item, output_picker_bot));
                }

            }
            // TODO: check current picking item in picker schedule is ok
            // TODO: check that order in picker schedule corresponds to the order in robots (order of items)
            return true;
        }

        public void ThrowException(string msg)
        {
            log("");
            log(msg);
            Logger.Flush();
            throw new NotSupportedException(msg);

        } 

        public void PrintLog(Instance inst, CommData input, CommData output)
        {
            DateTime currentTime = DateTime.Now;
            string date = currentTime.ToString(new System.Globalization.CultureInfo("de-DE"));
            Logger.Write("################################################################################\n");
            log("");
            log(String.Format("< REQUEST > [{0}]", date));
            log("");
            log(String.Format("bots: {0}", String.Join(", ", input.availableBots)));
            log("");
            log("BOTS:");
            foreach (int id in inst.MovableStations.Select(ms => ms.ID))
            {
                if (!input.bot_info.ContainsKey(id))
                {
                    log(String.Format("{0} | ------  -", id));
                    continue;
                }
                BotInfo bi = input.bot_info[id];
                string item = bi.item != "" ? String.Format("{0,6}", bi.item) : "------";
                string item_status = bi.item_status >= 0 ? String.Format("{0:0.00}", bi.item_status) : "-   ";
                string predicted_time = bi.predicted_time >= 0 ? String.Format("{0:0.00}", bi.predicted_time) : " -";
                log(String.Format("{0} | {1}  {2}  {3}", id, item, item_status, predicted_time));
            }
            log("++++");
            foreach (int id in inst.MovableStations.Select(ms => ms.ID))
            {
                string orders = "X";
                if (input.assigned.ContainsKey(id))
                {
                    orders = String.Join("  ", input.assigned[id]);
                }
                log(String.Format("{0} | {1}", id, orders));
            }
            log("");
            log("PICKERS:");
            foreach (int id in inst.MateBots.Select(mb => mb.ID))
            {
                if (!input.picker_info.ContainsKey(id))
                {
                    log(String.Format("{0} | ----  --  -", id));
                    continue;
                }
                PickerInfo pi = input.picker_info[id];
                string robot_id = pi.bot_id == -1 ? "--" : String.Format("{0}", pi.bot_id);
                string item_status = pi.item_status >= 0 ? String.Format("{0:0.00}", pi.item_status) : "-";
                string item = pi.item != "" ? pi.item : "----";
                log(String.Format("{0} | {1:0,4}  {2}  {3:0.00}", id, item, robot_id, item_status));
            }
            log("");
            Logger.Write("#-------------------------------------------------------------------------------\n");
            log("");
            log("< RESPONSE >");
            log("");
            log("BOTS:");
            foreach (int id in inst.MovableStations.Select(ms => ms.ID))
            {
                string orders = "X";
                if (output.assigned.ContainsKey(id))
                {
                    orders = String.Join("  ", output.assigned[id]);
                }
                else if (output.new_orders.ContainsKey(id))
                {
                    orders = String.Join("  ", output.new_orders[id]);
                }
                log(String.Format("{0} | {1}", id, orders));
            }
            log("");
            log("PICKERS:");
            foreach (int id in inst.MateBots.Select(mb => mb.ID))
            {
                if (!output.picker_schedule.ContainsKey(id))
                {
                    log(String.Format("{0} | -", id));
                    continue;
                }
                List<string> schedule = new List<string>();
                foreach (var pa in output.picker_schedule[id])
                {
                    schedule.Add(String.Format("{0}-{1}", pa.item, pa.bot_id));
                }
                string sch = String.Join("  ", schedule);
                if (schedule.Count == 0)
                    sch = "X";
                log(String.Format("{0} | {1}", id, sch));
            }
            log("");
            Logger.Write("################################################################################\n");
            Logger.Flush();
        }
        public void log(string line)
        {
            Logger.Write(String.Format("#  {0}\n", line));
        }
    }
}
