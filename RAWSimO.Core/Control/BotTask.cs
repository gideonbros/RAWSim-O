using RAWSimO.Core.Elements;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Waypoints;
using RAWSimO.Core.Bots;
using RAWSimO.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// Defines the task a robot can execute.
    /// </summary>
    public abstract class BotTask
    {
        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance in which the task is executed.</param>
        /// <param name="bot">The bot that shall execute the task.</param>
        public BotTask(Instance instance, Bot bot) { Instance = instance; Bot = bot; }
        /// <summary>
        /// The instance this task belongs to.
        /// </summary>
        public Instance Instance { get; private set; }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public abstract BotTaskType Type { get; }
        /// <summary>
        /// The bot that executes the task.
        /// </summary>
        public Bot Bot { get; private set; }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public abstract void Prepare();
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public abstract void Cancel();
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public abstract void Finish();
    }
    /// <summary>
    /// Class represents a task that is assigned onto bot only for the 
    /// duration of aborting a task(breaking to the nearest waypoint
    /// </summary>
    public class AbortingTask : BotTask
    {
        /// <summary>
        /// Constructs new AbortingTask
        /// </summary>
        /// <param name="instance">This instance</param>
        /// <param name="bot"><see cref="Bot"/> that will execute this task</param>
        public AbortingTask(Instance instance, Bot bot) : base(instance, bot)
        {
        }
        /// <summary>
        /// Type of this Task
        /// </summary>
        public override BotTaskType Type => BotTaskType.AbortingTask;
        /// <summary>
        /// Cancel this task
        /// </summary>
        public override void Cancel()
        {
            //Not implemented for now
        }
        /// <summary>
        /// Finish this task by notifying needed managers
        /// </summary>
        public override void Finish()
        {
            //Not implemented for now
        }
        /// <summary>
        /// Prepare this task for execution
        /// </summary>
        public override void Prepare()
        {
            //Not implemented for now
        }
    }
    /// <summary>
    /// this class represents a task of assisting at a waypoint
    /// </summary>
    public class AssistTask : BotTask
    {
        /// <summary>
        /// Constructs new AssistTask for a waypoint
        /// </summary>
        /// <param name="instance">current instance</param>
        /// <param name="assistant">Bot which will execute the assist task</param>
        /// <param name="waypoint">Waypoint on which assist is needed</param>
        /// <param name="botToAssist">Bot which needs assistance</param>
        public AssistTask(Instance instance, Bot assistant, Waypoint waypoint, Bot botToAssist) 
            : base(instance, assistant)
        {
            Waypoint = waypoint;
            BotToAssist = botToAssist;
            BotToAssistArrived = false;
            AssistantArrived = false;
        }
        /// <summary>
        /// Construct new AssistTask for a location on idx
        /// </summary>
        /// <param name="instance">current instance</param>
        /// <param name="assistant">Bot which will execute the assist task</param>
        /// <param name="idx">Index at which the assist is needed </param>
        /// <param name="botToAssist">Bot which needs assistance</param>
        public AssistTask(Instance instance, Bot assistant, int idx, Bot botToAssist) 
            :this(instance, assistant, instance.Waypoints[idx], botToAssist ) { }

        /// <summary>
        /// returns whether Bot that needs assistance has arrived
        /// </summary>
        public bool BotToAssistArrived { get; set; }
        /// <summary>
        /// returns whether Bot that offers assistance has arrived
        /// </summary>
        public bool AssistantArrived { get; set; }
        /// <summary>
        /// Bot which needs assistance
        /// </summary>
        public Bot BotToAssist { get; private set; }
        /// <summary>
        /// Waypoint at which the assist is needed
        /// </summary>
        public Waypoint Waypoint { get; private set; }
        /// <summary>
        /// Type of the Task
        /// </summary>
        public override BotTaskType Type => BotTaskType.AssistTask;
        /// <summary>
        /// Cancel this task
        /// </summary>
        public override void Cancel()
        {
            //Not implemented for now
        }
        /// <summary>
        /// finish this task
        /// </summary>
        public override void Finish()
        {
            //Not implemented for now
        }
        /// <summary>
        /// prepare this task
        /// </summary>
        public override void Prepare()
        {
            //Not implemented for now
        }
    }
    ///<summary>
    /// This class represents a task of gathering multiple items 
    ///</summary>
    public class MultiPointGatherTask : BotTask
    {   
         /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance this task belongs to.</param>
        /// <param name="bot">The bot that shall execute the task.</param>
        /// <param name="order">DummyOrder which will be transformed to task</param>
        public MultiPointGatherTask(Instance instance, Bot bot, Order order)
            : base(instance, bot)
        {
            // clear the previous order tracking and give new order
            Instance.Controller.MateScheduler.NewOrderInItemTable(bot.ID, order.ID);
            Locations = new List<Waypoint>();
            PodLocations = new List<Waypoint>();
            PodItems = new List<SimpleItemDescription>();
            Times = new List<double>();
            LocationItemDictionary = new Dictionary<Waypoint, List<ItemDescription>>();
            //foreach (var position in order.Positions)
            // Use the ordered item list to suggest the preferred item order as determined by the optmization
            foreach (var item in order.OrderedItemList)
            {
                Waypoint point = instance.GetWaypointByID(item.ID);
                // locations where robot will come
                Locations.Add(point);
                // true locations of the pod so that the mapping of the
                // bot location and the pod location is tracked
                PodLocations.Add(point);
                PodItems.Add(item as SimpleItemDescription);
                Instance.Controller.MateScheduler.itemTable[bot.ID].AddAddress((item as SimpleItemDescription).location);
                Times.Add(order.Times[item]);
                if (!LocationItemDictionary.ContainsKey(point))
                    LocationItemDictionary.Add(point, new List<ItemDescription>());
                LocationItemDictionary[point].Add(item);
            }

            Order = order;

            // updated the ID of the order
            order.movableStationID = bot.GetInfoID();
            order.movableStationHue = bot.GetInfoHue();
            DropWaypoint = null; //currently DropWaypoint is ignored (from OrderFile)
        }
        /// <summary>n
        /// List of locations that the robot will visit
        /// </summary>
        public List<Waypoint> Locations {get; private set;}
        public List<Waypoint> PodLocations { get; private set; }
        public List<SimpleItemDescription> PodItems { get; private set; }

        // this dictionary is only used for visualization
        // if there are two items on the same location, visualization will not recognize that
        // but the location will still be visited two times
        public Dictionary<Waypoint, List<ItemDescription>> LocationItemDictionary;
        /// <summary>
        /// order from which the task was created
        /// </summary>
        public Order Order { get; set; }
        /// <summary>
        /// Represents a list of times needed for each item
        /// </summary>
        public List<double> Times { get; set; }
        /// <summary>
        /// Item drop waypoint
        /// </summary>
        public Waypoint DropWaypoint { get; set; }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type => BotTaskType.MultiPointGatherTask;
        public override void Cancel()
        {
            //Not implemented for now
        }
        /// <summary>
        /// Finish the task and signal OrderManager
        /// </summary>
        public override void Finish()
        {
            MovableStation ms = Bot as MovableStation;
            ms.AssignedOrders.First().Completed = true;
            ms.RemoveAnyCompletedOrder(Instance.Controller.CurrentTime);
        }
        /// <summary>
        /// Prepares the task by adjusting waypoints so that they are not blocked by pods
        /// </summary>
        public override void Prepare()
        {
            if (Locations == null) throw new TaskCanceledException("DummyTask Location was unitialized but Prepare() was called");
            // go through all task locations
            for (int i = 0; i < Locations.Count; ++i)
            {
                bool found = false;
                // check if this is the location with the pod
                if (!Locations[i].HasPod)
                {
                    bool _found = false;
                    foreach(var wp in Locations[i].Paths)
                    {
                        if(wp.HasPod)
                        {
                            _found = true;
                            Locations[i] = wp;
                            break;
                        }
                    }
                    if(!_found)
                        throw new Exception("Locatons should have pods!" + Locations[i].ToString() + " does not have pod");
                }
                
                // find the neighbouring location waypoint which is clear
                foreach (Waypoint newWaypoint in Locations[i].Paths)
                {
                    Waypoint location = null;
                    // if it doesn't have a pod
                    if (!newWaypoint.HasPod)
                    {
                        // find the closest (BFS) access point (queue position)
                        // this also covers the case when
                        location = path_BFS(newWaypoint);
                        // if found, this is the queue point
                        if (location != null)
                        {
                            Locations[i] = location;
                            found = true;
                            break;
                        }
                    }
                }
                //if this part is reached then no available position was found, throw error
                if(!found)
                    throw new Exception("there was no available position next to " + Locations[i].ToString());
            }
        }

        /// <summary>
        /// Finding the closes access point via BFS for a single waypoint.
        /// </summary>
        /// <param name="startWp">Starting waypoint, usally with pod</param>
        /// <returns></returns>
        public Waypoint path_BFS(Waypoint startWp)
        {
            Waypoint location = null;
            Queue<Waypoint> wps = new Queue<Waypoint>();
            List<Waypoint> visited = new List<Waypoint>();
            wps.Enqueue(startWp);
            while (wps.Count > 0)
            {
                Waypoint currWp = wps.Dequeue();
                visited.Add(currWp);
                if (!currWp.HasPod)
                {
                    if (currWp.isAccessPoint)
                    {
                        location = currWp;
                        break;
                    }
                    else
                    {
                        foreach(var wp in currWp.Paths)
                            if (!visited.Contains(wp))
                                wps.Enqueue(wp);
                    }
                }
            }
            return location;
        }

    }
    /// <summary>
    /// This class represents a park pod task.
    /// </summary>
    public class ParkPodTask : BotTask
    {
        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance this task belongs to.</param>
        /// <param name="bot">The bot that shall execute the task.</param>
        /// <param name="pod">The pod that the robot shall park.</param>
        /// <param name="storageLocation">The location at which the pod shall be parked.</param>
        public ParkPodTask(Instance instance, Bot bot, Pod pod, Waypoint storageLocation)
            : base(instance, bot)
        {
            Pod = pod;
            StorageLocation = storageLocation;
        }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type { get { return BotTaskType.ParkPod; } }
        /// <summary>
        /// The storage location to use for the pod.
        /// </summary>
        public Waypoint StorageLocation { get; private set; }
        /// <summary>
        /// The pod to store.
        /// </summary>
        public Pod Pod { get; private set; }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public override void Prepare()
        {
            Instance.ResourceManager.ClaimPod(Pod, Bot, BotTaskType.ParkPod);
            Instance.ResourceManager.ClaimStorageLocation(StorageLocation);
        }
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public override void Finish()
        {
        }
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public override void Cancel()
        {
            Instance.ResourceManager.ReleaseStorageLocation(StorageLocation);
        }
    }
    /// <summary>
    /// This class represents a park pod task.
    /// </summary>
    public class RepositionPodTask : BotTask
    {
        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance this task belongs to.</param>
        /// <param name="bot">The bot that shall execute the task.</param>
        /// <param name="pod">The pod that the robot shall park.</param>
        /// <param name="storageLocation">The location to bring the pod to.</param>
        public RepositionPodTask(Instance instance, Bot bot, Pod pod, Waypoint storageLocation)
            : base(instance, bot)
        {
            Pod = pod;
            StorageLocation = storageLocation;
        }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type { get { return BotTaskType.RepositionPod; } }
        /// <summary>
        /// The location to bring the pod to.
        /// </summary>
        public Waypoint StorageLocation { get; private set; }
        /// <summary>
        /// The pod to store.
        /// </summary>
        public Pod Pod { get; private set; }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public override void Prepare()
        {
            Instance.ResourceManager.ClaimPod(Pod, Bot, BotTaskType.RepositionPod);
            Instance.ResourceManager.ClaimStorageLocation(StorageLocation);
        }
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public override void Finish()
        {
        }
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public override void Cancel()
        {
            Instance.ResourceManager.ReleaseStorageLocation(StorageLocation);
        }
    }
    /// <summary>
    /// Defines an extraction task.
    /// </summary>
    public class ExtractTask : BotTask
    {
        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance this task belongs to.</param>
        /// <param name="bot">The robot that shall execute the task.</param>
        /// <param name="reservedPod">The pod to use for executing the task.</param>
        /// <param name="outputStation">The output station to bring the pod to.</param>
        /// <param name="requests">The requests to handle with this task.</param>
        public ExtractTask(Instance instance, Bot bot, Pod reservedPod, OutputStation outputStation, List<ExtractRequest> requests)
            : base(instance, bot)
        {
            ReservedPod = reservedPod;
            OutputStation = outputStation;
            Requests = requests;
            foreach (var request in requests)
                request.StatInjected = false;
        }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type { get { return BotTaskType.Extract; } }
        /// <summary>
        /// The output station to bring the pod to.
        /// </summary>
        public OutputStation OutputStation { get; private set; }
        /// <summary>
        /// The requests to finish by executing this task.
        /// </summary>
        public List<ExtractRequest> Requests { get; private set; }
        /// <summary>
        /// The pod to use for this task.
        /// </summary>
        public Pod ReservedPod { get; private set; }
        /// <summary>
        /// Marks the first request handled.
        /// </summary>
        public void FirstPicked() { Requests[0].Finish(); Requests.RemoveAt(0); }
        /// <summary>
        /// Marks the first request aborted and re-inserts it into the pool of available requests.
        /// </summary>
        public void FirstAborted() { Requests[0].Abort(); Instance.ResourceManager.ReInsertExtractRequest(Requests[0]); Requests.RemoveAt(0); }
        /// <summary>
        /// Adds another request to this task on-the-fly.
        /// </summary>
        /// <param name="request">The request that shall also be completed by this task.</param>
        public void AddRequest(ExtractRequest request)
        {
            Instance.ResourceManager.RemoveExtractRequest(request);
            if (!ReservedPod.IsContained(request.Item))
                throw new InvalidOperationException("Cannot add a request for an item that is not available!");
            ReservedPod.RegisterItem(request.Item, request);
            Requests.Add(request);
            request.StatInjected = true;
        }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public override void Prepare()
        {
            Instance.ResourceManager.ClaimPod(ReservedPod, Bot, BotTaskType.Extract);
            OutputStation.RegisterInboundPod(ReservedPod);
            OutputStation.RegisterExtractTask(this);
            for (int i = 0; i < Requests.Count; i++)
            {
                Instance.ResourceManager.RemoveExtractRequest(Requests[i]);
                ReservedPod.RegisterItem(Requests[i].Item, Requests[i]);
            }
        }
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public override void Cancel()
        {
            if (Bot.Pod == null)
                Instance.ResourceManager.ReleasePod(ReservedPod);
            OutputStation.UnregisterInboundPod(ReservedPod);
            OutputStation.UnregisterExtractTask(this);
            for (int i = 0; i < Requests.Count; i++)
            {
                Instance.ResourceManager.ReInsertExtractRequest(Requests[i]);
                ReservedPod.UnregisterItem(Requests[i].Item, Requests[i]);
            }
        }
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public override void Finish()
        {
            if (Requests.Any())
                throw new InvalidOperationException("An unfinished request cannot be marked as finished!");
            OutputStation.UnregisterInboundPod(ReservedPod);
            OutputStation.UnregisterExtractTask(this);
        }
    }
    /// <summary>
    /// Defines a store task.
    /// </summary>
    public class InsertTask : BotTask
    {
        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance the task belongs to.</param>
        /// <param name="bot">The bot that shall carry out the task.</param>
        /// <param name="reservedPod">The pod to use for the task.</param>
        /// <param name="inputStation">The input station to bring the pod to.</param>
        /// <param name="requests">The requests that shall be finished after successful execution of the task.</param>
        public InsertTask(Instance instance, Bot bot, Pod reservedPod, InputStation inputStation, List<InsertRequest> requests)
            : base(instance, bot)
        {
            InputStation = inputStation;
            Requests = requests;
            ReservedPod = reservedPod;
            foreach (var request in requests)
                request.StatInjected = false;
        }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type { get { return BotTaskType.Insert; } }
        /// <summary>
        /// The input station at which the task is carried out.
        /// </summary>
        public InputStation InputStation { get; private set; }
        /// <summary>
        /// The requests to finish by executing this task.
        /// </summary>
        public List<InsertRequest> Requests { get; private set; }
        /// <summary>
        /// The pod used for storing the bundles.
        /// </summary>
        public Pod ReservedPod { get; private set; }
        /// <summary>
        /// Marks the first request handled.
        /// </summary>
        public void FirstStored() { Requests[0].Finish(); Requests.RemoveAt(0); }
        /// <summary>
        /// Marks the first request aborted and re-inserts it into the pool of available requests.
        /// </summary>
        public void FirstAborted() { Requests[0].Abort(); Instance.ResourceManager.ReInsertStoreRequest(Requests[0]); Requests.RemoveAt(0); }
        /// <summary>
        /// Adds another request to this task on-the-fly.
        /// </summary>
        /// <param name="request">The request that shall also be completed by this task.</param>
        public void AddRequest(InsertRequest request)
        {
            Instance.ResourceManager.RemoveStoreRequest(request);
            if (!ReservedPod.Fits(request.Bundle))
                throw new InvalidOperationException("Cannot add a request for a bundle not fitting the pod!");
            Requests.Add(request);
            request.StatInjected = true;
        }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public override void Prepare()
        {
            Instance.ResourceManager.ClaimPod(ReservedPod, Bot, BotTaskType.Insert);
            for (int i = 0; i < Requests.Count; i++)
                Instance.ResourceManager.RemoveStoreRequest(Requests[i]);
        }
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public override void Cancel()
        {
            if (Bot.Pod == null)
                Instance.ResourceManager.ReleasePod(ReservedPod);
            for (int i = 0; i < Requests.Count; i++)
                Instance.ResourceManager.ReInsertStoreRequest(Requests[i]);
        }
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public override void Finish() { }
    }
    /// <summary>
    /// Defines a rest task.
    /// </summary>
    public class RestTask : BotTask
    {
        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <param name="instance">The instance this task belongs to.</param>
        /// <param name="bot">The bot that shall rest.</param>
        /// <param name="restingLocation">The location at which the robot shall rest.</param>
        public RestTask(Instance instance, Bot bot, Waypoint restingLocation)
            : base(instance, bot)
        {
            RestingLocation = restingLocation;
        }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type { get { return BotTaskType.Rest; } }
        /// <summary>
        /// The location at which the robot shall rest.
        /// </summary>
        public Waypoint RestingLocation { get; private set; }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public override void Prepare()
        {
            // TODO check for resting location?
            //if (RestingLocation.PodStorageLocation)
            Instance.ResourceManager.ClaimRestingLocation(RestingLocation);
        }
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public override void Finish()
        {
            // TODO check for resting location?
            //if (RestingLocation.PodStorageLocation)
            Instance.ResourceManager.ReleaseRestingLocation(RestingLocation);
        }
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public override void Cancel()
        {
            // TODO check for resting location?
            //if (RestingLocation.PodStorageLocation)
            Instance.ResourceManager.ReleaseRestingLocation(RestingLocation);
        }
    }
    /// <summary>
    /// Defines a dummy task that basically does nothing.
    /// </summary>
    public class DummyTask : BotTask
    {
        /// <summary>
        /// Creates a new placeholder task.
        /// </summary>
        /// <param name="instance">The instance the task belongs to.</param>
        /// <param name="bot">The bot for which the placeholder task shall be generated.</param>
        public DummyTask(Instance instance, Bot bot) : base(instance, bot) { }
        /// <summary>
        /// The type of the task.
        /// </summary>
        public override BotTaskType Type { get { return BotTaskType.None; } }
        /// <summary>
        /// Prepares everything for executing the task (claiming resources and similar).
        /// </summary>
        public override void Prepare() { /* Nothing to do */ }
        /// <summary>
        /// Cleans up a cancelled task.
        /// </summary>
        public override void Cancel() { /* Nothing to do */ }
        /// <summary>
        /// Cleans up after a task was successfully executed.
        /// </summary>
        public override void Finish() { /* Nothing to do */ }
    }
}
