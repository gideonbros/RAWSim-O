using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using SpreadsheetLight;


namespace RAWSimO.Core.Management
{
    public class StatisticsManager: IUpdateable
    {
        /// <summary>
        /// Instance this object belongs to
        /// </summary>
        public Instance Instance;
        /// <summary>
        /// List of throughputs between time intervals
        /// </summary>
        public List<int> PeriodicThroughputs;
        /// <summary>
        /// Path to the statistics summary directory
        /// </summary>
        public string StatisticsSummaryDirectory { get; set; }
        /// <summary>
        /// Period used in periodic throughput analysis
        /// </summary>
        public double ThroughputPeriod => periodicThroughputInterval;

        public StatisticsManager(Instance instance)
        {
            Instance = instance;
            PeriodicThroughputs = new List<int>();
            periodicThroughputInterval = instance.SettingConfig.StatThroughputInterval;
            timeOfLastThroughputCalculation = Instance?.Controller?.CurrentTime ?? 0.0;
            lastHandledOrdersCount = 0;
        }
        /// <summary>
        /// Gets the next time this objects needs to act
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <returns>Time when next event triggers</returns>
        public double GetNextEventTime(double currentTime)
        {
            return timeOfLastThroughputCalculation + periodicThroughputInterval;
        }
        /// <summary>
        /// Updates this object
        /// </summary>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public void Update(double lastTime, double currentTime)
        {
            //if enough time passed do the calculation
            if (timeOfLastThroughputCalculation + periodicThroughputInterval <= currentTime)
            {
                PeriodicThroughputs.Add(Instance.StatOverallOrdersHandled - lastHandledOrdersCount);
                lastHandledOrdersCount = Instance.StatOverallOrdersHandled;
                timeOfLastThroughputCalculation = currentTime;
            }
        }
        /// <summary>
        /// Writes statistics summary in a given file path
        /// </summary>
        /// <param name="path">Path to the file where statistics summary shall be written</param>
        public void WriteStatisticsSummary(string path)
        {
            //thread synchronization event
            EventWaitHandle waitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, "SHARED_BY_ALL_PROCESSES");

            Instance.LogDefault(">>> Entering WriteStatisticsSummary() - extension and directory checking...");
            //null check
            if (string.IsNullOrEmpty(path))
                return;
            //extension check
            if(Path.GetExtension(path) != ".xlsx")
                throw new ArgumentException("file " + Path.GetFileName(path) + " is not of right type", path);

            string directory = Path.GetDirectoryName(path);

            //if directory does not exist, return error
            if (!Directory.Exists(directory)) throw new FileNotFoundException("Directory " + directory + " does not exist!");

            //write to statistics summary only once per simulation
            if (hasWrittenThisInstance) return;
            else hasWrittenThisInstance = true;

            Instance.LogDefault(">>> Passed extension and directory checking");
            SLDocument sl;

            //if file already exists, append
            if (File.Exists(path))
            {
                //open existing file on statistics summary worksheet
                var startTime = DateTime.Now;
                Instance.LogDefault(">>> File exists - trying to append...");

                //add lock
                waitHandle.WaitOne();

                while (true)
                {
                    try
                    {   //try to open document
                        sl = new SLDocument(path, "Statistics summary");
                        Instance.LogDefault(">>> Successfully opened the file");
                        //if successful, continue with execution
                        break;
                    }catch(Exception)
                    {
                        Instance.LogDefault(">>> Unable to open - keep trying for 60 seconds...");
                        //if you could not open documnt within one minute, stop trying
                        if ((DateTime.Now - startTime).TotalSeconds > 60)
                        {
                            sl = new SLDocument();
                            Instance.LogDefault(">>> Unable to open for 60 seconds, creating a new SLDocument()");
                            break;
                        }
                            
                        //if you still have time to try, do it again
                        continue;
                    }
                }

                Instance.LogDefault(">>> Start writing...");
                //get all the used cells so far
                var usedCells = sl.GetCells();
                //get the row on which you will write
                int row = usedCells.Count + 1;
                WriteStatisticsSummaryEntry(sl, new StatisticsSummaryEntry(Instance), row);

                //append periodic data
                //change worksheet
                sl.SelectWorksheet("Periodic throughput");
                //get all the used cells so far
                usedCells = sl.GetCells();
                //get the row on which you will write
                row = usedCells.Count + 1;
                //get max column used in case you need to change the header
                int maxColumns = usedCells.Values.Select(rowDict => rowDict.Count).Max();
                //if max column used is less than what we currently need, write new header
                if (maxColumns < PeriodicThroughputs.Count + 1)
                    WritePeriodicThroughputHeader(sl);
                //write new values in the row
                WritePeriodicThroughputEntry(sl, row);

                //append mate switching throuput
                sl.SelectWorksheet("Mate switching throughput");
                WriteMateSwitchingThrouput(sl);

                //add mate assist order history
                sl.SelectWorksheet("Mate assist order selection");
                WriteMateAssistOrderSelection(sl);

                //write extra parameters/values/statistics which do not belong in the other worksheets
                sl.SelectWorksheet("Extras");
                WriteExtras(sl);

                Instance.LogDefault(">>> Finished writing, saving...");
                sl.Save();
                Instance.LogDefault(">>> Saved!");
            }
            else //if file does not exist, create it
            {
                //add lock
                waitHandle.WaitOne();

                Instance.LogDefault(">>> Statistics Summary file does not exist, creating a new one");
                //create new workbook with default worksheet name
                sl = new SLDocument();

                Instance.LogDefault(">>> Created an empty worksheet, start writing...");
                //rename the worksheet
                sl.RenameWorksheet(SLDocument.DefaultFirstSheetName, "Statistics summary");
                //write statistics summary header
                WriteStatisticsSummaryHeader(sl);
                //write new entry on the second row
                WriteStatisticsSummaryEntry(sl, new StatisticsSummaryEntry(Instance), 2);

                //add new worksheet for periodic throughput 
                sl.AddWorksheet("Periodic throughput");

                //write Periodic throughput header
                WritePeriodicThroughputHeader(sl);
                //write periodic throughput entry on the second row
                WritePeriodicThroughputEntry(sl, 2);

                //add mate switching throughput
                sl.AddWorksheet("Mate switching throughput");
                WriteMateSwitchingThrouput(sl);

                //add mate assist order history
                sl.AddWorksheet("Mate assist order selection");
                WriteMateAssistOrderSelection(sl);

                //write extra parameters/values/statistics which do not belong in the other worksheets
                sl.AddWorksheet("Extras");
                WriteExtras(sl);

                Instance.LogDefault(">>> Finished writing, saving the file...");
                //save the workbook
                sl.SaveAs(path);
            }
            sl.Dispose();

            //unlock
            waitHandle.Set();
        }


        private void WriteExtras(SLDocument sl)
        {
            var cells = sl.GetCells();
            var row = cells.Count + 1;
            //write entry delimiter in the first available row
            var style = sl.CreateStyle();
            style.Fill.SetPattern(DocumentFormat.OpenXml.Spreadsheet.PatternValues.DarkGray, SLThemeColorIndexValues.Dark1Color, SLThemeColorIndexValues.Dark1Color);
            for (int i = 1; i <= 15; ++i)
                sl.SetCellStyle(row, i, style);
            row++;

            //write instance identifying data
            sl.SetCellValue(row, 1, "MovableStation count:"); sl.SetCellValue(row, 2, Instance.MovableStations.Count);
            sl.SetCellValue(row, 3, "MateBot count:"); sl.SetCellValue(row++, 4, Instance.MateBots.Count);

            //write extras
            if (Instance.SettingConfig.DetectPassByEvents)
            {
                sl.SetCellValue(row, 1, "Total Pass-By events:");
                sl.SetCellValue(row++, 2, BotNormal.TotalPassByEvents);

                sl.SetCellValue(row, 1, "Pass by events by hour:");

                int col = 2;
                foreach (var hour in BotNormal.PassByEvents.Keys)
                {
                    sl.SetCellValue(row++, col, hour);
                    sl.SetCellValue(row--, col++, BotNormal.PassByEvents[hour]);
                }

                row += 2;
            }
            
        }

        /// <summary>
        /// Writes AssistOrderHistory of each <see cref="MateBot"/> in it's column
        /// </summary>
        /// <param name="sl">Spreadsheed document to write to</param>
        private void WriteMateAssistOrderSelection(SLDocument sl)
        {
            var cells = sl.GetCells();
            var column = cells.Count != 0 ? cells[1].Count : 0;

            if (column > 0) //we are appending, add extra column space for readbility
            {
                column++;
                for (int i = 1; i <= 10; ++i)
                    sl.SetCellValue(i, column, "||||");
            }
            
            //write data for each MateBot
            foreach (var mate in Instance.MateBots)
            {
                var row = 1;

                //write mate name in header
                sl.SetCellValue(row++, ++column, mate.ToString());

                if (mate.AssistOrderHistory == null) continue;

                //write mate data in column
                foreach (var entry in mate.AssistOrderHistory)
                {
                    sl.SetCellValue(row++, column, entry);
                }
            }
        }

        /// <summary>
        /// Writes mate switching throuput to statisticsSummary
        /// </summary>
        /// <param name="sl"><see cref="SLDocument"/> to write to</param>
        /// <param name="row">row at which writing will start</param>
        private void WriteMateSwitchingThrouput(SLDocument sl)
        {
            var cells = sl.GetCells();
            var column = cells.Count != 0 ? cells[1].Count : 0;

            if (column > 0) //we are appending, add extra column space for readbility
            {
                column++;
                for (int i = 1; i <= 10; ++i)
                    sl.SetCellValue(i, column, "||||");
            }

            //write data for each MateBot
            foreach (var mate in Instance.MateBots)
            {
                var row = 1;

                //write mate name in header
                sl.SetCellValue(row++, ++column, mate.ToString());

                //write mate data in column
                foreach (var entry in mate.SwitchesPerAssists)
                {
                    sl.SetCellValue(row++, column, entry);
                }
            }

        }
        /// <summary>
        /// Writes Periodic throughput header
        /// </summary>
        /// <param name="sl"><see cref="SLDocument"/> where the header will be written</param>
        private void WritePeriodicThroughputHeader(SLDocument sl)
        {
            for (int i = 1; i <= PeriodicThroughputs.Count; ++i)
            {
                sl.SetCellValue(1, i, TimeSpan.FromSeconds(i * ThroughputPeriod).ToString("d\\:hh\\:mm\\:ss"));
            }
            sl.SetCellValue(1, PeriodicThroughputs.Count + 1, TimeSpan.FromSeconds(Instance.Controller.CurrentTime).ToString("d\\:hh\\:mm\\:ss"));
        }
        /// <summary>
        /// Writes Periodic throughput entry line for this instance 
        /// </summary>
        /// <param name="sl"><see cref="SLDocument"/> where the entry will be written</param>
        /// <param name="row">row on which the entry will be written</param>
        private void WritePeriodicThroughputEntry(SLDocument sl, int row)
        {
            for (int i = 0; i < PeriodicThroughputs.Count; ++i)
            {
                sl.SetCellValue(row, i + 1, PeriodicThroughputs[i]);
            }
            sl.SetCellValue(row, PeriodicThroughputs.Count + 1, Instance.StatOverallOrdersHandled - PeriodicThroughputs.Sum());
        }
        /// <summary>
        /// Writes Statistics summary header
        /// </summary>
        /// <param name="sl"><see cref="SLDocument"/> object where header will be written</param>
        private void WriteStatisticsSummaryHeader(SLDocument sl)
        {
            //define header values
            List<string> headerValues = new List<string>()
            {   "Movable station count","MateBot count",
                "Station/mate ratio","Total assist time","MateBot assist time(%)","MateBots move time",
                "MateBots move time (%)","MateBots distance traveled","MateBots wait time",
                "MateBots wait time (%)","Avg. Mate assist time", "Avg. Mate move time",
                "Avg. Mate distance traveled","Avg. Mate wait time","Movable station assist time (%)",
                "Movable stations move time","Movable stations move time (%)",
                "Movable stations distance traveled","Movable stations wait time",
                "Movable stations wait time (%)","MS total time rotating","MS total time queuing",
                "MS total time waiting in traffic","Movable stations get pallet time",
                "Movable stations drop pallet time","MS get/drop pallet time percentage",
                "MS Time spent waiting for locations to open","Avg. MS assist time",
                "Avg. MS move time","Avg. MS distance traveled","Avg. MS wait time",
                "Avg. MS time rotating", "Avg. MS time queuing", "Avg. MS time waiting in traffic",
                "Avg. MS get pallet time","Avg. MS put pallet time","Avg. MS time spent waiting for location",
                "Orders handled","Simulation duration","Realtime duration","CLJ","CLJE","CR"
            };
            //write values on the first row
            for (int i = 0; i < headerValues.Count; ++i)
                sl.SetCellValue(1, i + 1, headerValues[i]);
        }
        /// <summary>
        /// Writes statistics summary entry of this instance
        /// </summary>
        /// <param name="sl"><see cref="SLDocument"/> object where header will be written</param>
        /// <param name="entry"><see cref="StatisticsSummaryEntry"/> object where data of this instance is stored</param>
        /// <param name="row">row on which the data will be written</param>
        private void WriteStatisticsSummaryEntry(SLDocument sl, StatisticsSummaryEntry entry, int row)
        {
            sl.SetCellValue(row, 1, entry.MovableStationCount);
            sl.SetCellValue(row, 2, entry.MateBotCount);
            sl.SetCellValue(row, 3, entry.StationMateRatio);
            sl.SetCellValue(row, 4, entry.TotalAssistTime);
            sl.SetCellValue(row, 5, entry.MateBotAssistTimePercentage);
            sl.SetCellValue(row, 6, entry.MateBotMoveTime);
            sl.SetCellValue(row, 7, entry.MateBotPercentOfMoveTime);
            sl.SetCellValue(row, 8, entry.MateBotDistanceTraveled);
            sl.SetCellValue(row, 9, entry.MateBotWaitTime);
            sl.SetCellValue(row, 10, entry.MateBotPercentOfWaitTime);
            sl.SetCellValue(row, 11, entry.AvgMateAssistTime);
            sl.SetCellValue(row, 12, entry.AvgMateMoveTime);
            sl.SetCellValue(row, 13, entry.AvgMateDistanceTraveled);
            sl.SetCellValue(row, 14, entry.AvgMateWaitTime);
            sl.SetCellValue(row, 15, entry.MovableStationAssistTimePercentage);
            sl.SetCellValue(row, 16, entry.MovableStationMoveTime);
            sl.SetCellValue(row, 17, entry.MovableStationPercentOfMoveTime);
            sl.SetCellValue(row, 18, entry.MovableStationDistanceTraveled);
            sl.SetCellValue(row, 19, entry.MovableStationWaitTime);
            sl.SetCellValue(row, 20, entry.MovableStationPercentOfWaitTime);
            sl.SetCellValue(row, 21, entry.MovableStationTotalTimeRotating);
            sl.SetCellValue(row, 22, entry.MovableStationTotalTimeQueuing);
            sl.SetCellValue(row, 23, entry.MovableStationTotalTimeWaitingInTraffic);
            sl.SetCellValue(row, 24, entry.MovableStationGetPalletTime);
            sl.SetCellValue(row, 25, entry.MovableStationDropPalletTime);
            sl.SetCellValue(row, 26, entry.MovableStationPercentOfGetAndDropPalletTime);
            sl.SetCellValue(row, 27, entry.MovableStationWaitingForLocationToOpen);
            sl.SetCellValue(row, 28, entry.AvgMSAssistTime);
            sl.SetCellValue(row, 29, entry.AvgMSMoveTime);
            sl.SetCellValue(row, 30, entry.AvgMSDistanceTraveled);
            sl.SetCellValue(row, 31, entry.AvgMSWaitTime);
            sl.SetCellValue(row, 32, entry.AvgMSRotateTime);
            sl.SetCellValue(row, 33, entry.AvgMSQueuingTime);
            sl.SetCellValue(row, 34, entry.AvgMSWaitingInTraffic);
            sl.SetCellValue(row, 35, entry.AvgMSGetPalletTime);
            sl.SetCellValue(row, 36, entry.AvgMSPutPalletTime);
            sl.SetCellValue(row, 37, entry.AvgMSWaitForLocatons);
            sl.SetCellValue(row, 38, entry.OrderHandled);
            sl.SetCellValue(row, 39, entry.SimulationDuration);
            sl.SetCellValue(row, 40, entry.RealtimeDuration);
            sl.SetCellValue(row, 41, entry.CC);
            sl.SetCellValue(row, 42, entry.CCE);
            sl.SetCellValue(row, 43, entry.CR);
        }
        /// <summary>
        /// Time span of one interval
        /// </summary>
        private double periodicThroughputInterval { get; set; }
        /// <summary>
        /// Last time periodic throughput was calculated
        /// </summary>
        private double timeOfLastThroughputCalculation { get; set; }
        /// <summary>
        /// Number of orders handled when last throughput calculation was dome
        /// </summary>
        private int lastHandledOrdersCount { get; set; }
        /// <summary>
        /// Bool indicating if Statistics manager has already written in the statistics summary
        /// </summary>
        private bool hasWrittenThisInstance = false;
    }

    /// <summary>
    /// This class represents one entry in the statistics summary file
    /// </summary>
    public class StatisticsSummaryEntry
    {
        public StatisticsSummaryEntry(Instance instance)
        {
            Instance = instance;
        }
        /// <summary>
        /// number of <see cref="MovableStation"/> objects in this instance
        /// </summary>
        public int MovableStationCount => Instance.MovableStations.Count;
        /// <summary>
        /// number of <see cref="MateBot"/> objects in this instance
        /// </summary>
        public int MateBotCount => Instance.MateBots.Count;
        /// <summary>
        /// <see cref="MovableStation"/> to <see cref="MateBot"/> count ratio
        /// </summary>
        public double StationMateRatio => (double)MovableStationCount / MateBotCount;
        /// <summary>
        /// Total time spent in assistance
        /// </summary>
        public double TotalAssistTime => MovableStation.StatAllAssistTimes;
        /// <summary>
        /// Percentage of total time <see cref="MateBot"/> objects spent assisting
        /// </summary>
        public string MateBotAssistTimePercentage => ((TotalAssistTime / totalMateBotTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Total time <see cref="MateBot"/> objects spent moving
        /// </summary>
        public double MateBotMoveTime => Instance.MateBots.Sum(mb => mb.StatTotalStateTimes[BotStateType.MoveToAssist]);
        /// <summary>
        /// Percentage of total time <see cref="MateBot"/> objects spent moving
        /// </summary>
        public string MateBotPercentOfMoveTime => ((MateBotMoveTime/totalMateBotTime) * 100).ToString("F") + "%";
        /// <summary>
        ///  Total distance <see cref="MateBot"/> objects traveled
        /// </summary>
        public double MateBotDistanceTraveled => Instance.MateBots.Sum(mb => mb.StatDistanceTraveled);
        /// <summary>
        ///  Total time <see cref="MateBot"/> objects spent waiting
        /// </summary>
        public double MateBotWaitTime { get
            {
                double matebotsTotalWaitTime = 0;
                foreach (var mb in Instance.MateBots)
                    matebotsTotalWaitTime += (mb.StatTotalStateTimes[BotStateType.WaitingForStation] - mb.StatTotalAssistTime + mb.StatTotalStateTimes[BotStateType.Rest]);
                return matebotsTotalWaitTime;
            } }
        /// <summary>
        /// Percentage of total time <see cref="MateBot"/> objects spent waiting
        /// </summary>
        public string MateBotPercentOfWaitTime => ((MateBotWaitTime / totalMateBotTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Time spent on assist by average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateAssistTime => TotalAssistTime / MateBotCount;
        /// <summary>
        /// Move time of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateMoveTime => MateBotMoveTime / MateBotCount;
        /// <summary>
        /// Distance traveled of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateDistanceTraveled => MateBotDistanceTraveled / MateBotCount;
        /// <summary>
        /// Wait time of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateWaitTime => MateBotWaitTime / MateBotCount;
        /// <summary>
        /// Percentage of total time <see cref="MateBot"/> objects spent assisting
        /// </summary>
        public string MovableStationAssistTimePercentage => ((TotalAssistTime / totalMovableStationTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent moving
        /// </summary>
        public double MovableStationMoveTime => Instance.MovableStations.Sum(ms => ms.StatTotalStateTimes[BotStateType.Move]);
        /// <summary>
        /// Percentage of total time <see cref="MovableStation"/> objects spent moving
        /// </summary>
        public string MovableStationPercentOfMoveTime => ((MovableStationMoveTime / totalMovableStationTime) * 100).ToString("F") + "%";
        /// <summary>
        ///  Total distance <see cref="MateBot"/> objects traveled
        /// </summary>
        public double MovableStationDistanceTraveled => Instance.MovableStations.Sum(ms => ms.StatDistanceTraveled);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent waiting
        /// </summary>
        public double MovableStationWaitTime
        {
            get
            {
                double movableStationsTotalWaitTime = 0;
                foreach (var ms in Instance.MovableStations)
                    movableStationsTotalWaitTime += (ms.StatTotalStateTimes[BotStateType.WaitingForMate] - ms.StatTotalAssistTime 
                                                        + ms.StatTotalStateTimes[BotStateType.Rest] + ms.StatTotalStateTimes[BotStateType.WaitingForSeeOffAssistance]);
                return movableStationsTotalWaitTime;   
            } }
        /// <summary>
        /// Percentage of total time <see cref="MovableStation"/> objects spent waiting
        /// </summary>
        public string MovableStationPercentOfWaitTime => ((MovableStationWaitTime / totalMovableStationTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent rotating 
        /// </summary>
        public double MovableStationTotalTimeRotating => Instance.MovableStations.Sum(ms => ms.StatTotalTimeRotating);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent in queue 
        /// </summary>
        public double MovableStationTotalTimeQueuing=> Instance.MovableStations.Sum(ms => ms.StatTotalTimeQueuing);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent waiting in traffic (linear and angular velocity 0 but in <see cref="BotStateType.Move"/> and not in queue 
        /// </summary>
        public double MovableStationTotalTimeWaitingInTraffic=> Instance.MovableStations.Sum(ms => ms.StatTotalTimeIdleMoving);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent in <see cref="BotStateType.PreparePartialTask"/> state
        /// </summary>
        public double MovableStationWaitingForLocationToOpen => Instance.MovableStations.Sum(ms => ms.StatTotalStateTimes[BotStateType.PreparePartialTask]);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent in <see cref="BotStateType.GetPallet"/> state
        /// </summary>
        public double MovableStationGetPalletTime => Instance.MovableStations.Sum(ms => ms.StatTotalStateTimes[BotStateType.GetPallet]);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent in <see cref="BotStateType.PutPallet"/> state
        /// </summary>
        public double MovableStationDropPalletTime => Instance.MovableStations.Sum(ms => ms.StatTotalStateTimes[BotStateType.PutPallet]);
        /// <summary>
        /// Percentage of total time <see cref="MovableStation"/> objects spent in <see cref="BotStateType.GetPallet"/> and  <see cref="BotStateType.PutPallet"/> states
        /// </summary>
        public string MovableStationPercentOfGetAndDropPalletTime => (((MovableStationGetPalletTime + MovableStationDropPalletTime) / totalMovableStationTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Time spent on getting assistance of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSAssistTime => TotalAssistTime / MovableStationCount;
        /// <summary>
        /// Move time of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSMoveTime => MovableStationMoveTime / MovableStationCount;
        /// <summary>
        /// Distance traveled of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSDistanceTraveled => MovableStationDistanceTraveled / MovableStationCount;
        /// <summary>
        /// Wait time of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSWaitTime => MovableStationWaitTime / MovableStationCount;
        /// <summary>
        /// Rotate time of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSRotateTime => MovableStationTotalTimeRotating / MovableStationCount;
        /// <summary>
        /// Queuing time of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSQueuingTime => MovableStationTotalTimeQueuing / MovableStationCount;
        /// <summary>
        /// The time average <see cref="MovableStation"/> spent waiting in traffic
        /// </summary>
        public double AvgMSWaitingInTraffic => MovableStationTotalTimeWaitingInTraffic / MovableStationCount;
        /// <summary>
        /// Time average <see cref="MovableStation"/> spent in <see cref="BotStateType.GetPallet"/> state
        /// </summary>
        public double AvgMSGetPalletTime => MovableStationGetPalletTime / MovableStationCount;
        /// <summary>
        /// Time average <see cref="MovableStation"/> spent in <see cref="BotStateType.PutPallet"/> state
        /// </summary>
        public double AvgMSPutPalletTime => MovableStationDropPalletTime / MovableStationCount;
        /// <summary>
        /// Time average <see cref="MovableStation"/> spent in <see cref="BotStateType.PreparePartialTask"/> state
        /// </summary>
        public double AvgMSWaitForLocatons => MovableStationWaitingForLocationToOpen / MovableStationCount;
        /// <summary>
        /// Amount of orders completed in this simulation
        /// </summary>
        public int OrderHandled => Instance.StatOverallOrdersHandled;
        /// <summary>
        /// Amount of time simulated 
        /// </summary>
        public string SimulationDuration { get
            {
                var span = TimeSpan.FromSeconds(Instance.Controller.CurrentTime);
                return (24 * span.Days + span.Hours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
            } }
        /// <summary>
        /// Duration of this simulation in real time 
        /// </summary>
        public string RealtimeDuration => (Instance.SettingConfig.StartTime != DateTime.MinValue ?
                                          (DateTime.Now - Instance.SettingConfig.StartTime).ToString("h\\:mm\\:ss") :
                                          DateTime.Now.ToString("HH:mm:ss"));
        public decimal CC => ((decimal)Instance.Controller.CurrentTime) * Ccps;
        public decimal CCE => ((decimal)Instance.Controller.CurrentTime) * Ccpse;
        public decimal CR => ((decimal)Instance.Controller.CurrentTime) * Crps;
        /// <summary>
        /// instance this object belongs to
        /// </summary>
        private Instance Instance { get; set; }
        /// <summary>
        /// total time <see cref="MateBot"/> objects spent moving, waiting or assisting
        /// </summary>
        private double totalMateBotTime => MateBotMoveTime + MateBotWaitTime + TotalAssistTime;
        /// <summary>
        /// total time <see cref="MovableStation"/> objects spent moving, waiting or assisting
        /// </summary>
        private double totalMovableStationTime => MovableStationMoveTime + MovableStationWaitTime + 
                            TotalAssistTime + MovableStationGetPalletTime + MovableStationDropPalletTime;

        private decimal Ccps => 0.0121527777777778M * MateBotCount; //7p7B28h
        private decimal Ccpse => Ccps * 2;
        private decimal Crps => 0.0118371212121212M * MovableStationCount; //15B16h
    }
}
