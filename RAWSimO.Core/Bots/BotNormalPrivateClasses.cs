using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Bots
{
    //////////////////////////////////////////////////////////////////////////////////
    /// This file contains implementations of all the private classes of BotNormal ///
    ////////////////////////////////////////////////////////////////////////////////// 
    public partial class BotNormal : Bot
    {
        /// <summary>
        /// Class represents one pass by event. It is used only by BotNormal, hence it is private class
        /// </summary>
        private class PassByEvent : IEquatable<PassByEvent>
        {
            public PassByEvent(Bot firstBot, Bot secondBot, Waypoint firstWp, Waypoint secondWp, double time)
            {
                Bot1 = firstBot;
                Bot2 = secondBot;
                Bot1Wp = firstWp;
                Bot2Wp = secondWp;
                Time = time;
            }

            /// <summary>
            /// Logs <paramref name="Event"/> as a current event if it is not currently registered. Checks by value equality instead of reference equality
            /// </summary>
            /// <param name="Event">Pass by event that is going to be logged</param>
            /// <returns><see langword="true"/> if event has not yet been logged, <see langword="false"/> otherwise</returns>
            public static bool Log(PassByEvent Event, double time)
            {
                if (!CurrentEvents.Contains(Event)) //uses custom defined value equality instead of reference equality
                {
                    //new event, log it
                    CurrentEvents.Add(Event);
                    //count total number of events
                    TotalPassByEvents++;
                    //count event by hour in which it happened
                    var hour = (int)Math.Floor(time / 3600) + 1;
                    if(PassByEvents.ContainsKey(hour))
                    {
                        PassByEvents[hour]++;
                    }
                    else
                    {
                        PassByEvents.Add(hour, 1);
                    }
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Removes <paramref name="Event"/> from loged current pass by events if <paramref name="Event"/> is registered as current. Search is done by value comparison instead of reference
            /// </summary>
            /// <param name="Event">Pass by event that will be removed</param>
            /// <returns><see langword="true"/> if <paramref name="Event"/> was sucessfully removed, <see langword="false"/> if it wasn't registered</returns>
            public static bool Remove(PassByEvent Event)
            {
                if (CurrentEvents.Contains(Event))
                {
                    CurrentEvents.Remove(Event);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Removes any logged pass by event in which <paramref name="bot"/> was logged on <paramref name="waypoint"/>
            /// </summary>
            /// <param name="bot">bot whose event will be removed</param>
            /// <param name="waypoint">location on which <paramref name="bot"/> was logged</param>
            /// <returns><see langword="true"/> if event was sucessfully removed, <see langword="false"/> if such event is not registered</returns>
            public static bool Remove(Bot bot, Waypoint waypoint)
            {
                return Remove(CurrentEvents.FirstOrDefault(e =>
                        (e.Bot1 == bot && e.Bot1Wp == waypoint) || (e.Bot2 == bot && e.Bot2Wp == waypoint)));
            }

            /// <summary>
            /// Property which returns the two bots which were in a pass by event
            /// </summary>
            public HashSet<Bot> Bots => new HashSet<Bot>() { Bot1, Bot2 };
            /// <summary>
            /// Property which returns the two <see cref="Waypoint"/>s at which Bots were staying when pass by event was registered 
            /// </summary>
            public HashSet<Waypoint> Locations => new HashSet<Waypoint>() { Bot1Wp, Bot2Wp };
            /// <summary>
            /// Time at which pass by event was registered
            /// </summary>
            public double Time { get; private set; }
            /// <summary>
            /// Counter of total number of pass by events that happened in this instance
            /// </summary>
            public static int TotalPassByEvents { get; set; } = 0;
            /// <summary>
            /// Counter of total number of pass by events by hour. Keys are hours, values are number of events in a given hour
            /// </summary>
            public static Dictionary<int, int> PassByEvents { get; set; } = new Dictionary<int, int>();
            /// <summary>
            /// Container holding all the PassBy events that are currently happening
            /// </summary>
            private static HashSet<PassByEvent> CurrentEvents = new HashSet<PassByEvent>();

            public readonly Bot Bot1;
            public readonly Bot Bot2;
            public readonly Waypoint Bot1Wp;
            public readonly Waypoint Bot2Wp;

            #region IEquatable implementation
            /// <summary>
            /// generic equals. Calls type specific equals
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                return Equals(obj as PassByEvent);
            }
            /// <summary>
            /// Type specific equals
            /// </summary>
            /// <param name="other">Other object</param>
            /// <returns>true if positions and bots are the same</returns>
            public bool Equals(PassByEvent other)
            {
                // If parameter is null, return false.
                if (other is null)
                {
                    return false;
                }

                // Optimization for a common success case.
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                // If run-time types are not exactly the same, return false.
                if (this.GetType() != other.GetType())
                {
                    return false;
                }

                //HashSet checks for performance
                if (other.Locations.Contains(Bot1Wp) && other.Locations.Contains(Bot2Wp) &&
                   other.Bots.Contains(Bot1) && other.Bots.Contains(Bot2)) //if passed, then the same bots and locations were present in the event, but maybe the same bot did not stand in the same location ((x1,y2),(x2,y1))
                    if ((Bot1 == other.Bot1 && Bot1Wp == other.Bot1Wp && Bot2 == other.Bot2 && Bot2Wp == other.Bot2Wp) ||
                        (Bot1 == other.Bot2 && Bot1Wp == other.Bot2Wp && Bot2 == other.Bot1 && Bot2Wp == other.Bot1Wp)) //pairwise check
                        return true;

                return false;
            }

            public static bool operator ==(PassByEvent lhs, PassByEvent rhs)
            {
                // Check for null on left side.
                if (lhs is null)
                {
                    if (rhs is null)
                    {
                        // null == null = true.
                        return true;
                    }

                    // Only the left side is null.
                    return false;
                }
                // Equals handles case of null on right side.
                return lhs.Equals(rhs);
            }

            public static bool operator !=(PassByEvent lhs, PassByEvent rhs)
            {
                return !(lhs == rhs);
            }

            public override int GetHashCode()
            {   //if one event was caused by (bot1,wp1) and (bot2,wp2) and was saved in that order, we want that event which was saved in 
                //different order produces the same hash code, hence why we calculate hash in two different ways and then return min
                
                unchecked //Overflow is fine, just wrap
                {
                    int hash1 = 17; //prime number, as well as 23

                    hash1 = hash1 * 19 + Bot1?.GetHashCode() ?? 1;
                    hash1 = hash1 * 23 + Bot2?.GetHashCode() ?? 1;
                    hash1 = hash1 * 29 + Bot1Wp?.GetHashCode() ?? 1;
                    hash1 = hash1 * 31 + Bot2Wp?.GetHashCode() ?? 1;

                    int hash2 = 17;

                    hash2 = hash2 * 19 + Bot2?.GetHashCode() ?? 1;
                    hash2 = hash2 * 23 + Bot1?.GetHashCode() ?? 1;
                    hash2 = hash2 * 29 + Bot2Wp?.GetHashCode() ?? 1;
                    hash2 = hash2 * 31 + Bot1Wp?.GetHashCode() ?? 1;

                    return Math.Min(hash1, hash2);  //returning min insures that symetric events are treated the same
                }
            }
            #endregion

        }
    }
}
