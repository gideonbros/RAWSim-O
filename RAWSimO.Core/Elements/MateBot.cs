using RAWSimO.Core.Control;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Info;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Waypoints;
using RAWSimO.MultiAgentPathFinding;
using System;
using System.Collections.Generic;
using System.Linq;
using RAWSimO.Core.Control.Defaults.PathPlanning;
using RAWSimO.MultiAgentPathFinding.Methods;

namespace RAWSimO.Core.Elements
{
    /// <summary>
    /// class representing robot's pal
    /// </summary>
    public class MateBot : BotNormal 
    {
        /// <summary>
        /// Static constructor is called only once, it initializes random number generator parameters 
        /// which will be used in this class
        /// </summary>
        static MateBot()
        {
            kParam = 0.63764;
            thetaParam = 15.35307;
            kParamBig = 0.741566;
            thetaParamBig = 227.992358;
            StatAllAssistTimes = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MateBot"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="instance">The instance.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="acceleration">The maximum acceleration.</param>
        /// <param name="deceleration">The maximum deceleration.</param>
        /// <param name="maxVelocity">The maximum velocity.</param>
        /// <param name="turnSpeed">The turn speed.</param>
        /// <param name="collisionPenaltyTime">The collision penalty time.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public MateBot(int id, Instance instance, double radius, double acceleration, double deceleration, double maxVelocity, double turnSpeed, double collisionPenaltyTime, double x = 0.0, double y = 0.0)
            : base(id, instance, radius, 99999, acceleration, deceleration, maxVelocity, turnSpeed, collisionPenaltyTime, x, y)
        {
            MateID = ++maxID;
        }
        #region Core
        /// <summary>
        /// Sets the assist duration to the given value
        /// </summary>
        public void SetAssistDuration(double duration)
        {
            AssistDuration = duration;
            StatTotalAssistTime += AssistDuration;
            StatAllAssistTimes += AssistDuration;
        }
        /// <summary>
        /// Aborts assist
        /// </summary>
        internal void AbortAssist()
        {
            //assigning new task will clear state queue if needed
            AssignTask(new AbortingTask(Instance, this));
        }
        /// <summary>
        /// Saves last assist order to AssistOrderHistory container
        /// </summary>
        private void SaveLastAssistDepth()
        {
            //in case LastAssistOrder was not saved (maybe some strategy) ignore;
            if (LastAssistOrder == null) return;
            //if AssistOrderHistory exists, append to it
            if (AssistOrderHistory != null)
            {
                AssistOrderHistory.Add(LastAssistOrder.GetValueOrDefault());
            } //if it is first time, create new list and add LastAssistOrder
            else
            {
                AssistOrderHistory = new List<int>() { LastAssistOrder.GetValueOrDefault() };
            }
        }
        /// <summary>
        /// Returns object identification as a string
        /// </summary>
        /// <returns>string representation</returns>
        public override string ToString()
        {
            return "Mate" + MateID.ToString();
        }
        #endregion

        #region Events
        /// <summary>
        /// Reacts on assignment of <see cref="AbortingTask"/>
        /// </summary>
        public override void OnAbortingTaskAsigned()
        {
            Instance.Controller.MateScheduler.NotifyMateAbortingTaskAssigned(this);
        }
        /// <summary>
        /// Saves <paramref name="assistOrder"/> as last assist order
        /// </summary>
        /// <param name="assistOrder">Which position is assigned location currently</param>
        public virtual void OnBeingAssigned(int assistOrder)
        {
            LastAssistOrder = assistOrder;
        }
        /// <summary>
        /// Reacts on assist end pseudoevent
        /// </summary>
        public virtual void OnAssistEnded()
        {
            AssistDuration = double.NaN;
            SaveLastAssistDepth();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Time it takes for this particular MateBot to perform the assist
        /// </summary>
        public double AssistDuration { get; set; }
        /// <summary>
        /// Gets BotType
        /// </summary>
        public override BotType Type => BotType.MateBot;
        /// <summary>
        /// Statistic for total assist time of this matebot
        /// </summary>
        public double StatTotalAssistTime = 0;
        /// <summary>
        /// statistic for total assist times for all matebots
        /// </summary>
        public static double StatAllAssistTimes;
        /// <summary>
        /// Gets the number of states in the state queue 
        /// </summary>
        new public int StateQueueCount => base.StateQueueCount;
        /// <summary>
        /// The type of state this bot is currently in
        /// </summary>
        new public BotStateType CurrentBotStateType => base.CurrentBotStateType;
        /// <summary>
        /// Average matebot assist time
        /// </summary>
        public static double AverageAssistTime { get; } = 10.0;
        /// <summary>
        /// Indicates if MateBot is breaking
        /// </summary>
        public new bool IsBreaking => base.IsBreaking;
        /// <summary>
        /// Indicates number of times this <see cref="MateBot"/> assist location was switched before he began assist process
        /// </summary>
        public int SwitchesThisAssist { get; set; } = 0;
        /// <summary>
        /// List of number of switches per all assists done by this MateBot
        /// </summary>
        public LinkedList<int> SwitchesPerAssists { get; set; } = new LinkedList<int>();
        /// <summary>
        /// Holds history of LastAssistOrders for this <see cref="MateBot"/>
        /// </summary>
        public List<int> AssistOrderHistory { get; set; }
        /// <summary>
        /// Holds info about which depth was the last assist that this mate was assigned
        /// </summary>
        private int? LastAssistOrder = null;
        #endregion

        #region Private variables
        /// <summary>
        /// id of this MateBot instance
        /// </summary>
        private int MateID;
        #endregion

        #region Static variables
        /// <summary>
        /// Max ID of all MateBots
        /// </summary>
        private static int maxID = 0;
        /// <summary>
        /// k parameter used for sampling of Gamma distribution
        /// </summary>
        public static readonly double kParam;
        /// <summary>
        /// theta parameter used for sampling of Gamma distribution
        /// </summary>
        public static readonly double thetaParam;
        /// <summary>
        /// k parameter used for sampling of Gamma distribution for long assist times
        /// </summary>
        public static readonly double kParamBig;
        /// <summary>
        /// theta parameter used for sampling of Gamma distribution for long assist times
        /// </summary>
        public static readonly double thetaParamBig;
        #endregion
    }
}
