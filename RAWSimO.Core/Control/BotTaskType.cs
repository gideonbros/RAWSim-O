using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// Enumerates all tasks a robot can carry out.
    /// </summary>
    public enum BotTaskType
    {
        /// <summary>
        /// An empty task.
        /// </summary>
        None,

        /// <summary>
        /// Tells the bot to store the specified pod at the destination location.
        /// </summary>
        ParkPod,

        /// <summary>
        /// Tells the bot to move a pod to a new location.
        /// </summary>
        RepositionPod,

        /// <summary>
        /// Tells the bot to carry the specified pod to the desired <code>InputStation</code>.
        /// </summary>
        Insert,

        /// <summary>
        /// Tells the bot to carry the specified pod to the desired <code>OutputStation</code>.
        /// </summary>
        Extract,
        
        /// <summary>
        /// Tells the bot to rest.
        /// </summary>
        Rest,

        /// <summary>
        /// Tells the bot to visit given locations and gather items
        /// </summary>
        MultiPointGatherTask,

        /// <summary>
        /// Tells the bot to visit given locations and refill items
        /// </summary>
        RefillingTask,

        /// <summary>
        /// Tells the bot to assist another bot at a given location
        /// </summary>
        AssistTask,

        /// <summary>
        /// An empty task which is given to bot for the duration of aborting previous task
        /// </summary>
        AbortingTask
    }
}
