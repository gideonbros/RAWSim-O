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

namespace RAWSimO.Core.Control.Defaults.OrderBatching
{
    class RemoteOrderManager : OrderManager
    {

        private HttpClient _client;
        private RemoteOrderSchedulerConfiguration _config;
        private int no_bots;

        public class OrderInfo
        {
            public int index { get; set; }
            public bool assigned { get; set; }
            public int deadline { get; set; }
            public List<string> items { get; set; }
        }

        public class InputOrderBatch
        {
            public InputOrderBatch(int no_bots_)
            {
                no_bots = no_bots_;
                orders = new List<OrderInfo>();
            }
            public int no_bots;
            public List<OrderInfo> orders { get; set; }

        }

        public class ItemList
        {
            public int order_id;
            public List<string> items;
        }

        public class OutputOrderSchedule
        {
            public List<int> orders { get; set; }
            public List<ItemList> items { get; set; }
        }

        public RemoteOrderManager(Instance instance) : base(instance)
        {
            _config = instance.ControllerConfig.OrderBatchingConfig as RemoteOrderSchedulerConfiguration;
            _client = new HttpClient();
            _client.BaseAddress = new Uri(_config.Host + ":" + _config.Port);
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(_config.GetBodyType()));
            no_bots = instance.layoutConfiguration.MovableStationCount;
        }

        public override void SignalCurrentTime(double currentTime) { }

        protected override void DecideAboutPendingOrders()
        {
            List<MovableStation> availableStations = Instance.MovableStations.Where(s => s.CapacityInUse == 0).ToList();
            if (availableStations.Count == 0 || _pendingOrders.Count == 0)
                return;

            InputOrderBatch orderBatch = new InputOrderBatch(no_bots);

            for (int i = 0; i < Instance.MovableStations.Count; i++)
            {
                MovableStation ms = Instance.MovableStations[i];
                if (ms.CurrentTask.Type.Equals(BotTaskType.MultiPointGatherTask))
                {
                    MultiPointGatherTask task = (MultiPointGatherTask)ms.CurrentTask;
                    Order order = task.Order;
                    OrderInfo orderInfo = new OrderInfo();
                    orderInfo.index = order.ID;
                    orderInfo.assigned = true;
                    orderInfo.deadline = 100000;
                    orderInfo.items = order.GetOpenLocationAddresses();
                    //Console.WriteLine("Partial " + orderInfo.index.ToString() + ", " + ms.ID + ": " + String.Join(", ", orderInfo.items));
                    orderBatch.orders.Add(orderInfo);
                }
            }

            for (int i = 0; i < Math.Min(_pendingOrders.Count, 30); ++i)
            {
                Order order = _orders[i];
                OrderInfo orderInfo = new OrderInfo();
                orderInfo.index = order.ID;
                orderInfo.assigned = false;
                orderInfo.deadline = 100000;
                orderInfo.items = order.GetLocationAddresses();
                //Console.WriteLine("Full " + orderInfo.index.ToString() + ": " + String.Join(", ", orderInfo.items));
                orderBatch.orders.Add(orderInfo);
            }

            var json = JsonConvert.SerializeObject(orderBatch);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync("optimize", data).Result;

            OutputOrderSchedule orderSchedule = JsonConvert.DeserializeObject<OutputOrderSchedule>(response.Content.ReadAsStringAsync().Result);
            //Console.WriteLine("Order priorities: " + String.Join(", ", orderSchedule.orders));
            for (int i = 0; i < Math.Min(availableStations.Count, orderSchedule.orders.Count); i++)
            //for (int i = 0; i < Math.Min(availableStations.Count, _pendingOrders.Count); i++)
            {
                int orderId = orderSchedule.orders[i];
                Order order = _pendingOrders.Where(x => x.ID == orderId).First();
                ItemList itemList = orderSchedule.items.Where(il => il.order_id == orderId).First();
                order.OrderedItemList.Sort(
                    (ItemDescription a, ItemDescription b) =>
                    {
                        int ia = itemList.items.FindIndex(adr => adr == (a as SimpleItemDescription).location);
                        int ib = itemList.items.FindIndex(adr => adr == (b as SimpleItemDescription).location);
                        return ia > ib ? 1 : ia < ib ? -1 : 0;
                    }
                    );
                AllocateOrder(order, availableStations[i]);
            }
        }
    }
}
