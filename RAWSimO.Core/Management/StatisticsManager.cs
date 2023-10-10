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
        /// Containg accumulated statistics data
        /// </summary>
        public StatisticsSummaryTotal StatisticsSummaryTotal { get; set; }
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
            StatisticsSummaryTotal = new StatisticsSummaryTotal(instance);
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
        public void WriteStatisticsSummary(string path, bool write_partial_statistics = false)
        {
            //thread synchronization event
            EventWaitHandle waitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, "SHARED_BY_ALL_PROCESSES");
            Boolean thoroughSummary = false;
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
            if (Instance.SettingConfig.StatisticsSummaryOutputFrequency <= 0 && hasWrittenThisInstance) return;
            else hasWrittenThisInstance = true;

            Instance.LogDefault(">>> Passed extension and directory checking");
            SLDocument sl;

            //add lock
            waitHandle.WaitOne();
            //if file already exists, append
            if (File.Exists(path))
            {
                //open existing file on statistics summary worksheet
                var startTime = DateTime.Now;
                Instance.LogDefault(">>> File exists - trying to append...");

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
                if (write_partial_statistics || Instance.SettingConfig.StatisticsSummaryOutputFrequency <= 0)
                    WriteStatisticsSummaryEntry(sl, new StatisticsSummaryEntry(Instance), row, thoroughSummary);
                else
                    WriteStatisticsSummaryTotal(sl, StatisticsSummaryTotal, row, thoroughSummary);

                if (thoroughSummary)
                {
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
                }
                Instance.LogDefault(">>> Finished writing, saving...");
                sl.Save();
                Instance.LogDefault(">>> Saved!");
            }
            else //if file does not exist, create it
            {
                Instance.LogDefault(">>> Statistics Summary file does not exist, creating a new one");
                //create new workbook with default worksheet name
                sl = new SLDocument();

                Instance.LogDefault(">>> Created an empty worksheet, start writing...");
                //rename the worksheet
                sl.RenameWorksheet(SLDocument.DefaultFirstSheetName, "Statistics summary");
                //write statistics summary header
                WriteStatisticsSummaryHeader(sl, thoroughSummary);
                //write new entry on the second row
                if (write_partial_statistics || Instance.SettingConfig.StatisticsSummaryOutputFrequency <= 0)
                    WriteStatisticsSummaryEntry(sl, new StatisticsSummaryEntry(Instance), 2, thoroughSummary);
                else
                    WriteStatisticsSummaryTotal(sl, StatisticsSummaryTotal, 2, thoroughSummary);

                if (thoroughSummary)
                {
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
                }

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
        private void WriteStatisticsSummaryHeader(SLDocument sl, bool thoroughSummary)
        {
            //define header values
            if (thoroughSummary)
            {
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
                    "Orders handled","Lines","Printers","Simulation duration","Realtime duration","CLJ","CLJE","CR"
                };
                //write values on the first row
                for (int i = 0; i < headerValues.Count; ++i)
                    sl.SetCellValue(1, i + 1, headerValues[i]);
            }
            else
            {
                List<string> headerValues = new List<string>()
                {   "# Robots","# Pickers",
                    "Robot/Picker ratio","Picker assist time(%)",
                    "Picker move time (%)",
                    "Picker wait time (%)",
                    "Picker idle time (%)",
                    "Avg. picker distance", "Total picker distance", "Robot assist time (%)",
                    "Robot move time (%)",
                    "Robot wait time (%)",
                    "Robot idle time (%)", "Robot pallet time (%)",
                    "Avg. robot distance", "Total robot distance", "Total pallet distance", 
                    "Pallets","Lines","Printers","Simulation ","Realtime duration",
                    "Optimization", "Label", "OrderFile", "Quantity"
                };
                //write values on the first row
                for (int i = 0; i < headerValues.Count; ++i)
                    sl.SetCellValue(1, i + 1, headerValues[i]);
            }
        }


        public void WriteStatisticsSummaryTotal(SLDocument sl, StatisticsSummaryTotal total, int row, bool thoroughSummary)
        {
            if (thoroughSummary)
            {
                // Need to extend StatisticsSummaryTotal with all the values
            }
            else
            {
                List<object> solutionValues = new List<object>()
                {
                    (total.robotCount).ToString("0.##"),
                    (total.pickerCount).ToString("0.##"),
                    (total.robotCount / total.pickerCount).ToString("0.##"),
                    (100 * total.totalPickerAssistTime / total.totalPickerTime).ToString("0.##") + "%",
                    (100 * total.totalPickerMoveTime / total.totalPickerTime).ToString("0.##") + "%",
                    (100 * total.totalPickerWaitTime / total.totalPickerTime).ToString("0.##") + "%",
                    (100 * total.totalPickerIdleTime / total.totalPickerTime).ToString("0.##") + "%",
                    (total.totalPickerDistance / total.pickerCount).ToString("0.##"),
                    total.totalPickerDistance,
                    (100 * total.totalRobotAssistTime / total.totalRobotTime).ToString("0.##") + "%",
                    (100 * total.totalRobotMoveTime / total.totalRobotTime).ToString("0.##") + "%",
                    (100 * total.totalRobotWaitTime / total.totalRobotTime).ToString("0.##") + "%",
                    (100 * total.totalRobotIdleTime / total.totalRobotTime).ToString("0.##") + "%",
                    (100 * total.totalRobotPalletTime / total.totalRobotTime).ToString("0.##") + "%",
                    (total.totalRobotDistance / total.robotCount).ToString("0.##"),
                    (total.totalRobotDistance).ToString("0.##"),
                    (total.totalPalletDistance).ToString("0.##"),
                    total.totalPalletCount,
                    total.totalLinesHandled,
                    total.totalPrintersHandled,
                    total.simulationDuration,
                    total.realTimeDuration,
                    total.optimization,
                    total.label,
                    total.orderFile,
                    total.totalProcessedCapacityAllBots
            };

                //write values in the current row
                for (int i = 0; i < solutionValues.Count; ++i)
                {
                    sl.SetCellValue(row, i + 1, solutionValues[i].ToString());
                }
            }
        }
        /// <summary>
        /// Writes statistics summary entry of this instance
        /// </summary>
        /// <param name="sl"><see cref="SLDocument"/> object where header will be written</param>
        /// <param name="entry"><see cref="StatisticsSummaryEntry"/> object where data of this instance is stored</param>
        /// <param name="row">row on which the data will be written</param>
        private void WriteStatisticsSummaryEntry(SLDocument sl, StatisticsSummaryEntry entry, int row, bool thoroughSummary)
        {
            if (thoroughSummary)
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
                sl.SetCellValue(row, 39, entry.LinesHandled);
                sl.SetCellValue(row, 40, entry.PrintersHandled);
                sl.SetCellValue(row, 41, entry.SimulationDuration);
                sl.SetCellValue(row, 42, entry.RealtimeDuration);
                sl.SetCellValue(row, 43, entry.CC);
                sl.SetCellValue(row, 44, entry.CCE);
                sl.SetCellValue(row, 45, entry.CR);
            }
            else
            {

                List<object> solutionValues = new List<object>()
                {
                    entry.MovableStationCount,
                    entry.MateBotCount,
                    (entry.StationMateRatio).ToString("0.##"),
                    entry.MateBotAssistTimePercentage,
                    entry.MateBotPercentOfMoveTime,
                    entry.MateBotPercentOfWaitTime,
                    entry.MateBotPercentOfIdleTime,
                    (entry.AvgMateDistanceTraveled).ToString("0.##"),
                    (entry.MateBotDistanceTraveled).ToString("0.##"),
                    entry.MovableStationAssistTimePercentage,
                    entry.MovableStationPercentOfMoveTime,
                    entry.MovableStationPercentOfWaitTime,
                    entry.MovableStationPercentOfIdleTime,
                    entry.MovableStationPercentOfGetAndDropPalletTime,
                    (entry.AvgMSDistanceTraveled).ToString("0.##"),
                    (entry.MovableStationDistanceTraveled).ToString("0.##"),
                    entry.MovableStationPalletDistanceTraveled,
                    entry.OrderHandled,
                    entry.LinesHandled,
                    entry.PrintersHandled,
                    entry.SimulationDuration,
                    entry.RealtimeDuration,
                    entry.OptimizationType,
                    entry.NameLayout == "" ? entry.Label : entry.NameLayout,
                    entry.OrderFile,
                    entry.ProcessedCapacityAllBots
                };

                //write values in the current row
                for (int i = 0; i < solutionValues.Count; ++i)
                {
                    sl.SetCellValue(row, i + 1, solutionValues[i].ToString());
                }
            }
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

        public double ProcessedCapacityAllBots => Instance.MovableStations.Sum(mb => mb.ProcessedQuantity);
        public string OrderFile => Instance.layoutConfiguration.warehouse.GetOrderFileName();
        public string Label => Instance.layoutConfiguration.warehouse.agent_config;
        public string NameLayout => Instance.layoutConfiguration.NameLayout;
        public string OptimizationType => Instance.ControllerConfig.OrderBatchingConfig.GetMethodName() + "," + Instance.SettingConfig.orderShuffling.ToString() + "," + Instance.SettingConfig.botItemShuffling.ToString() + "," + Instance.SettingConfig.pickerReassignment.ToString();
        /// <summary>
        /// number of <see cref="MovableStation"/> objects in this instance
        /// </summary>
        public int MovableStationCount => Instance.MovableStations.Where(ms => ms.IsActive && !ms.IsRefill).Count();
        /// <summary>
        /// number of <see cref="MateBot"/> objects in this instance
        /// </summary>
        public int MateBotCount => Instance.MateBots.Where(mate => mate.IsActive).Count();
        /// <summary>
        /// Number of refilling bots.
        /// </summary>
        public int RefillingCount => Instance.MovableStations.Where(ms => ms.IsActive && ms.IsRefill).Count();
        /// <summary>
        /// <see cref="MovableStation"/> to <see cref="MateBot"/> count ratio
        /// </summary>
        public double StationMateRatio => MateBotCount == 0 ? 0 : (double)MovableStationCount / MateBotCount;
        /// <summary>
        /// Total time spent in assistance
        /// </summary>
        public double TotalAssistTime => MovableStation.StatAllAssistTimes + Instance.MateBots.Sum(mb => mb.StatTotalStateTimes[BotStateType.MoveToPickUpItem]);
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
        public double MateBotDistanceTraveled => Instance.MateBots.Sum(mb => mb.StatDistanceTraveled) + Instance.MovableStations.Sum(ms => ms.StatTotalPickerDistance);
        /// <summary>
        ///  Total time <see cref="MateBot"/> objects spent waiting
        /// </summary>
        public double MateBotWaitTime { get
            {
                double matebotsTotalWaitTime = 0;
                foreach (var mb in Instance.MateBots)
                    if (mb.IsActive)
                        matebotsTotalWaitTime += (mb.StatTotalStateTimes[BotStateType.WaitingForStation] - mb.StatTotalAssistTime);
                return matebotsTotalWaitTime;
            } }
        /// <summary>
        ///  Total time <see cref="MateBot"/> objects spent idle
        /// </summary>
        public double MateBotIdleTime
        {
            get
            {
                double matebotsTotalIdleTime = 0;
                foreach (var mb in Instance.MateBots)
                    if (mb.IsActive)
                        matebotsTotalIdleTime += (mb.StatTotalStateTimes[BotStateType.Rest]);
                return matebotsTotalIdleTime;
            }

        }
        /// <summary>
        /// Percentage of total time <see cref="MateBot"/> objects spent waiting
        /// </summary>
        public string MateBotPercentOfWaitTime => ((MateBotWaitTime / totalMateBotTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Percentage of total time <see cref="MateBot"/> objects spent idle
        /// </summary>
        public string MateBotPercentOfIdleTime => ((MateBotIdleTime / totalMateBotTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Time spent on assist by average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateAssistTime => MateBotCount == 0 ? 0 : TotalAssistTime / MateBotCount;
        /// <summary>
        /// Move time of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateMoveTime => MateBotCount == 0 ? 0 : MateBotMoveTime / MateBotCount;
        /// <summary>
        /// Distance traveled of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateDistanceTraveled => MateBotCount == 0 ? MateBotDistanceTraveled / MovableStationCount : MateBotDistanceTraveled / MateBotCount;
        /// <summary>
        /// Wait time of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateWaitTime => MateBotCount == 0 ? 0 : MateBotWaitTime / MateBotCount;
        /// <summary>
        /// Idle time of the average <see cref="MateBot"/>
        /// </summary>
        public double AvgMateIdleTime => MateBotCount == 0 ? 0 : MateBotIdleTime / MateBotCount;
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
        /// Total distance <see cref="MovableStation"/> objects traveled to/from pallets
        /// </summary>
        public double MovableStationPalletDistanceTraveled => Instance.MovableStations.Sum(ms => ms.StatPalletDistanceTraveled);
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent waiting
        /// </summary>
        public double MovableStationWaitTime
        {
            get
            {
                if (Instance.layoutConfiguration.warehouse.bots_as_pickers)
                {
                    return 0;
                }
                double movableStationsTotalWaitTime = 0;
                foreach (var ms in Instance.MovableStations)
                    if (ms.IsActive && (ms.StatTotalStateTimes[BotStateType.WaitingForMate] - ms.StatTotalAssistTime) > 0)
                        movableStationsTotalWaitTime += (ms.StatTotalStateTimes[BotStateType.WaitingForMate] - ms.StatTotalAssistTime);
                return movableStationsTotalWaitTime;   
            } }
        /// <summary>
        /// Total time <see cref="MovableStation"/> objects spent idle
        /// </summary>
        public double MovableStationIdleTime
        {
            get
            {
                if (Instance.layoutConfiguration.warehouse.bots_as_pickers)
                {
                    return 0;
                }
                double movableStationsTotalIdleTime = 0;
                foreach (var ms in Instance.MovableStations)
                    if (ms.IsActive)
                        movableStationsTotalIdleTime += (ms.StatTotalStateTimes[BotStateType.Rest] + ms.StatTotalStateTimes[BotStateType.WaitingForSeeOffAssistance]);
                return movableStationsTotalIdleTime;
            }
        }
        /// <summary>
        /// Percentage of total time <see cref="MovableStation"/> objects spent waiting
        /// </summary>
        public string MovableStationPercentOfWaitTime => ((MovableStationWaitTime / totalMovableStationTime) * 100).ToString("F") + "%";
        /// <summary>
        /// Percentage of total time <see cref="MovableStation"/> objects spent idle
        /// </summary>
        public string MovableStationPercentOfIdleTime => ((MovableStationIdleTime / totalMovableStationTime) * 100).ToString("F") + "%";
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
        /// Idle time of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSIdleTime => MovableStationIdleTime / MovableStationCount;
        /// <summary>
        /// Pallet time of average <see cref="MovableStation"/>
        /// </summary>
        public double AvgMSPalletTime => (MovableStationGetPalletTime + MovableStationDropPalletTime) / MovableStationCount;
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
        /// Amount of lines completed in this simulation
        /// </summary>
        public int LinesHandled => Instance.MovableStations.Sum(ms => ms.StatNumberOfPickups);
        /// <summary>
        /// Amount of printers completed in this simulation
        /// </summary>
        public int PrintersHandled => Instance.MovableStations.Sum(ms => ms.StatNumberOfPrinters);
        /// <summary>
        /// Amount of time simulated 
        /// </summary>
        public string SimulationDuration { get
            {
                var span = Instance.SettingConfig.StatisticsSummaryOutputFrequency > 0 ?
                    TimeSpan.FromSeconds(Instance.Controller.CurrentTime - Instance.Controller.StatisticsOutputsCount * 60 * Instance.SettingConfig.StatisticsSummaryOutputFrequency) : TimeSpan.FromSeconds(Instance.Controller.CurrentTime);
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
        public double totalMateBotTime => MateBotMoveTime + MateBotWaitTime + TotalAssistTime + MateBotIdleTime;
        /// <summary>
        /// total time <see cref="MovableStation"/> objects spent moving, waiting or assisting
        /// </summary>
        public double totalMovableStationTime => MovableStationMoveTime + MovableStationWaitTime + MovableStationIdleTime +
                            TotalAssistTime + MovableStationGetPalletTime + MovableStationDropPalletTime;

        private decimal Ccps => 0.0121527777777778M * MateBotCount; //7p7B28h
        private decimal Ccpse => Ccps * 2;
        private decimal Crps => 0.0118371212121212M * MovableStationCount; //15B16h
    }

    public class StatisticsSummaryTotal
    {
        public StatisticsSummaryTotal(Instance instance)
        {
            Instance = instance;
        }
        
        public double robotCount { get; set; } = 0;
        public double pickerCount { get; set; } = 0;
        public double refillingCount { get; set; } = 0;
        public double totalPickerWaitTime { get; set; } = 0.0;
        public double totalPickerIdleTime { get; set; } = 0.0;
        public double totalPickerMoveTime { get; set; } = 0.0;
        public double totalPickerDistance { get; set; } = 0.0;
        public double totalPickerAssistTime { get; set; } = 0.0;
        public double totalPickerTime { get; set; } = 0.0;
        public double totalRobotMoveTime { get; set; } = 0.0;
        public double totalRobotTime { get; set; } = 0.0;
        public double totalRobotWaitTime { get; set; } = 0.0;
        public double totalRobotIdleTime { get; set; } = 0.0;
        public double totalRobotPalletTime { get; set; } = 0.0;
        public double totalRobotDistance { get; set; } = 0.0;
        public double totalRobotAssistTime { get; set; } = 0.0;
        public double totalPalletDistance { get; set; } = 0.0;
        public int totalPalletCount { get; set; } = 0;
        public int totalLinesHandled { get; set; } = 0;
        public int totalPrintersHandled { get; set; } = 0;
        public double totalProcessedCapacityAllBots { get; set; } = 0.0;
        private Instance Instance { get; set; }
        public string simulationDuration { get; set; } = "0:00:00";
        public string realTimeDuration { get; set; } = "0:00:00";
        public string optimization { get; set; } = "";
        public string label { get; set; } = "";
        public string orderFile { get; set; } = "";
        public void Update(StatisticsSummaryEntry sse)
        {
            totalPickerWaitTime += sse.MateBotWaitTime;
            totalPickerIdleTime += sse.MateBotIdleTime;
            totalPickerMoveTime += sse.MateBotMoveTime;
            totalPickerDistance += sse.MateBotDistanceTraveled;
            totalPickerAssistTime += sse.TotalAssistTime;
            totalPickerTime += sse.totalMateBotTime;

            totalRobotWaitTime += sse.MovableStationWaitTime;
            totalRobotIdleTime += sse.MovableStationIdleTime;
            totalRobotPalletTime += sse.MovableStationDropPalletTime + sse.MovableStationGetPalletTime;
            totalRobotMoveTime += sse.MovableStationMoveTime;
            totalRobotTime += sse.totalMovableStationTime;
            totalRobotDistance += sse.MovableStationDistanceTraveled;
            totalRobotAssistTime += sse.TotalAssistTime;

            var span = TimeSpan.FromSeconds(Instance.Controller.CurrentTime);

            var partial_sim_time_in_hms = sse.SimulationDuration.Split(':');
            int partial_sim_time = 3600 * int.Parse(partial_sim_time_in_hms[0]) + 60 * int.Parse(partial_sim_time_in_hms[1]) + int.Parse(partial_sim_time_in_hms[2]);

            var last_total_sim_time_in_hms = simulationDuration.Split(':');
            int last_total_sim_time = 3600 * int.Parse(last_total_sim_time_in_hms[0]) + 60 * int.Parse(last_total_sim_time_in_hms[1]) + int.Parse(last_total_sim_time_in_hms[2]);

            if (Instance.layoutConfiguration.PickersPerPeriod.Count > 0)
                pickerCount = (pickerCount * last_total_sim_time + sse.MateBotCount * partial_sim_time) / Instance.Controller.CurrentTime;
            else
                pickerCount = sse.MateBotCount;

            if (Instance.layoutConfiguration.BotsPerPeriod.Count > 0)
                robotCount = (robotCount * last_total_sim_time + sse.MovableStationCount * partial_sim_time) / Instance.Controller.CurrentTime;
            else
                robotCount = sse.MovableStationCount;
            if (Instance.layoutConfiguration.RefillingPerPeriod.Count > 0)
                refillingCount = (refillingCount * last_total_sim_time + sse.RefillingCount * partial_sim_time) / Instance.Controller.CurrentTime;
            else
                refillingCount = sse.RefillingCount;

            simulationDuration = (24 * span.Days + span.Hours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
            realTimeDuration = (DateTime.Now - Instance.SettingConfig.StartTime).ToString("h\\:mm\\:ss");

            totalPalletDistance += sse.MovableStationPalletDistanceTraveled;
            totalPalletCount += sse.OrderHandled;
            totalLinesHandled += sse.LinesHandled;
            totalPrintersHandled += sse.PrintersHandled;
            totalProcessedCapacityAllBots += sse.ProcessedCapacityAllBots;

            optimization = sse.OptimizationType;
            label = sse.Label;
            orderFile = sse.OrderFile;
        }

    }
}
