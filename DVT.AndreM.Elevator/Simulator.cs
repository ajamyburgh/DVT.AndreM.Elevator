using DustInTheWind.ConsoleTools.Controls.Tables;
using DustInTheWind.ConsoleTools.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Collections.Concurrent;

namespace DVT.AndreM.Elevator
{
    internal class Simulator
    {
        private ElevatorManager _elevatorManager;
        private int _elevatorCount = 3;
        private int _minFloor = -1;
        private int _maxFloor = 5;
        private int _elevatorMaxOccupancy = 5;
        private static List<string> _movements = new List<string>();
        private static Progress<ElevatorProgress> _progressIndicator;
        private static System.Threading.Timer _stateTimer;

        internal Simulator()
        {
            int elevatorCount = 3;
            int minFloor = -1;
            int maxFloor = 5;
            int elevatorMaxOccupancy = 5;
            _progressIndicator = new Progress<ElevatorProgress>(ReportProgress);
            _stateTimer = new System.Threading.Timer(RedrawTable, new AutoResetEvent(false), 0, 1000);
            _elevatorManager = new ElevatorManager(elevatorCount, minFloor, maxFloor, elevatorMaxOccupancy);
            _movements.Add($"{NowString()} - Starting simulation");
        }

        private string NowString()
        {
            return DateTime.Now.ToString("HH:mm:ss.f");
        }

        internal async Task Start(CancellationToken cancellationToken)
        {
            List<Task> tasks = new List<Task>();

            //Set people waiting:
            _elevatorManager.SetPeopleWaiting(floorNumber: 4, peopleCount: 5);
            _elevatorManager.SetPeopleWaiting(floorNumber: -1, peopleCount: 2);

            //Set elevator start positions and current occupants
            _elevatorManager.Elevators[1].CurrentFloor = 2;
            _elevatorManager.Elevators[1].CurrentOccupancy = _elevatorMaxOccupancy; //This one is full, so should not move

            _elevatorManager.Elevators[2].CurrentFloor = 1; //Further from 4 but not full
            _elevatorManager.Elevators[2].CurrentOccupancy = 1;

            _elevatorManager.Elevators[3].CurrentFloor = 0; //Nearest to basement
            _elevatorManager.Elevators[3].CurrentOccupancy = 2;

            List<Elevator> moveElevators = new List<Elevator>();  
            for (int f = _minFloor; f <= _maxFloor; f++) 
            {
                int waiting = _elevatorManager.GetPeopleWaiting(f);
                if(waiting > 0)//Send elevators to nearest waiting people
                {
                    Elevator nearestElevator = _elevatorManager.NearestElevator(f);
                    if (nearestElevator != null)
                    {
                        nearestElevator.DestinationFloor = f;
                        moveElevators.Add(nearestElevator);                        
                    }
                }
            }            
            
            //Start move all:
            foreach (var elevator in moveElevators)
            {
                
                var moveTask = _elevatorManager.MoveToFloorAsync(elevator, elevator.DestinationFloor.Value, _progressIndicator, cancellationToken);
                tasks.Add(moveTask);
            }

            //StartMoving
            //await Task.WhenAll(tasks);
            //Hacking in an extra move before the last one completed
            await Task.WhenAny(tasks);
                        
            await Task.Delay(2000);            
        }


        private void ReportProgress(ElevatorProgress currentProgress)
        {
            Debug.WriteLine(currentProgress.movement);
            _movements.Add($"{NowString()} - {currentProgress.movement}"); ;
        }

        internal void StopSimulation()
        {
            _stateTimer.Dispose();
        }

        private void RedrawTable(object source)
        {
            //Redraw entire window:
            Console.Clear();
            Console.WriteLine("\x1b[3J");
            Console.SetCursorPosition(0, 0);

            DataGrid dataGrid = new DataGrid("Elevator by André Myburgh");
            dataGrid.DisplayBorderBetweenRows = true;
            dataGrid.HeaderRow.IsVisible = true;
            dataGrid.Border.Template = BorderTemplate.DoubleLineBorderTemplate;

            //dataGrid.Columns.Add("One");
            dataGrid.Columns.Add(""); // "Floor");
            foreach (var e in _elevatorManager.Elevators.Values)
            {
                dataGrid.Columns.Add(e.Name);
            }
            dataGrid.Columns.Add("People waiting");

            //dataGrid.Rows.Add(i, i, "1,3", "1,4");
            for (int f = _maxFloor; f >= _minFloor; f--)
            {
                int waiting = _elevatorManager.GetPeopleWaiting(f);
                List<ContentCell> cells = new List<ContentCell>();
                cells.Add(HelperStatic.FloorName(f));
                foreach (var e in _elevatorManager.Elevators.Values)
                {
                    string doorSymbol = e.DoorStatus == DoorState.Open ? "|" : "X";
                    string cellText = e.CurrentFloor == f ? $"{doorSymbol} {e.CurrentOccupancy} {doorSymbol}" : string.Empty;
                    ContentCell cell = new ContentCell(cellText);
                    cell.HorizontalAlignment = HorizontalAlignment.Center;
                    cells.Add(cell);
                }
                cells.Add(waiting.ToString());

                dataGrid.Rows.Add(cells);
            }

            dataGrid.Display();

            Console.WriteLine($"Last movement:");
            var reversed = _movements.AsEnumerable().Reverse();
            foreach (var movement in reversed)
            {
                Console.WriteLine(movement);
            }
            Console.WriteLine();
            DataGrid dataGridLegend = new DataGrid("Legend");
            dataGridLegend.DisplayBorderBetweenRows = true;
            dataGridLegend.HeaderRow.IsVisible = false;
            dataGridLegend.Border.Template = BorderTemplate.SingleLineBorderTemplate;
            dataGridLegend.Columns.Add("Symbol");
            dataGridLegend.Columns.Add("Description");
            dataGridLegend.Rows.Add("| 3 |", "Open elevator with 3 occupants");
            dataGridLegend.Rows.Add("X 1 X", "Closed elevator with 1 occupant");
            dataGridLegend.Display();
            //Console.ReadLine();

            //Console.SetCursorPosition(0, 0);
            Console.SetWindowPosition(0, 0);
        }

        internal async Task Inject(CancellationToken token)
        {
            //Some new people arrived:
            int destFloor = 0;
            _elevatorManager.SetPeopleWaiting(floorNumber: destFloor, peopleCount: 1);
            Elevator elevator = _elevatorManager.NearestElevator(destFloor);
            if (elevator != null) 
                await _elevatorManager.MoveToFloorAsync(elevator, destFloor, _progressIndicator, token);

            await Task.Delay(2000);
        }
    }
}
