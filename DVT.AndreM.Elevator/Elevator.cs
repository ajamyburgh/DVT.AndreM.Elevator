using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net.Http.Headers;

namespace DVT.AndreM.Elevator
{
    public enum MovementDirection
    {
        Up,
        Down
    }

    public enum DoorState
    {
        Open,
        Closed
    }

    public struct ElevatorTaskResult
    {
        public bool completed;
        public string description;

        public ElevatorTaskResult(bool completed, string description = "")
        {
            this.completed = completed;
            this.description = description;
        }
    }

    /// <summary>
    /// {elevatorName} {movement}
    /// </summary>
    public struct ElevatorProgress
    {
        public string movement;
        public string elevatorName;
        public int currentFloor;
    }

    public class Elevator
    {
        private readonly int _occupancyLimit; 
        private int _currentFloor;
        private int? _destinationFloor;
        private string _name;
        private DoorState _doorState;
        private bool _isMoving;
        //time to open/close door [sec]:
        private static int _secondsDoor = int.TryParse(ConfigurationManager.AppSettings["SecondsDoor"], out _secondsDoor) ? _secondsDoor : 3;
        //average speed per floor [sec]:
        private static int _secondsPerFloor = int.TryParse(ConfigurationManager.AppSettings["SecondsPerFloor"], out _secondsPerFloor) ? _secondsPerFloor : 5;
        private static int _secondsStopStart = int.TryParse(ConfigurationManager.AppSettings["SecondsStopStart"], out _secondsStopStart) ? _secondsStopStart : 1;

        #region Ctors

        public Elevator(string name, int occupancyLimit, int currentFloor = 0)
        {
            //Init a stopped, open elevator
            _occupancyLimit = occupancyLimit;
            _currentFloor = currentFloor;
            _destinationFloor = null;
            _isMoving = false;
            _doorState = DoorState.Open;   
            _name = name;
        }

        #endregion

        #region Properties

        public bool IsMoving
        {
            get { return _isMoving; }
            private set { _isMoving = value; }
        }

        public int CurrentFloor
        {
            get { return _currentFloor; }
            set { _currentFloor = value; }
        }

        public int? DestinationFloor
        {
            get { return _destinationFloor; }
            set { _destinationFloor = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public DoorState DoorStatus
        {
            get { return _doorState; }
            set { _doorState = value; }
        }

        public MovementDirection MovementDirection
        {
            get { return DestinationFloor < CurrentFloor ? MovementDirection.Down : MovementDirection.Up; }            
        }

        public int CurrentOccupancy { get; set; } //Set would typically be private (set by elevator's built-in scale)

        public int AvailableOccupancy { get { return (_occupancyLimit - CurrentOccupancy); } }

        public bool OccupancyLimitReached { get { return CurrentOccupancy >= _occupancyLimit; } }
        public int OccupancyLimit { get { return _occupancyLimit; } }

        #endregion

        #region Methods

        public async Task<bool> OpenDoorAsync()
        {
            if (DoorStatus == DoorState.Open) { return true; }

            if (IsMoving)
                throw new Exception($"Cannot open door. Elevator {Name} is moving.");

            await Task.Delay(_secondsDoor);

            DoorStatus = DoorState.Open;

            return true;
        }

        public async Task<bool> CloseDoorAsync()
        {
            if (DoorStatus == DoorState.Closed) { return true; }

            await Task.Delay(_secondsDoor * 1000);

            DoorStatus = DoorState.Closed;

            return true;
        }

        /// <summary>
        /// Stops the elevator and clears it's Destination
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StopAsync()
        {
            if(IsMoving)
                await Task.Delay(_secondsStopStart * 1000);
            IsMoving = false;
            DestinationFloor = null;
            return true;
        }

        /// <summary>
        /// Moves to destination and stops.
        /// </summary>
        /// <param name="progressFloor"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ElevatorTaskResult> MoveToDestinationFloorAsync(IProgress<ElevatorProgress> progressFloor, CancellationToken cancellationToken)
        {
            //safety checks
            if (IsMoving)
                return new ElevatorTaskResult(false, $"{Name} is moving already. Stop it first.");
            if (DestinationFloor == null)
                return new ElevatorTaskResult(false, $"{Name} has no destination set.");
            if (CurrentFloor != DestinationFloor && DoorStatus == DoorState.Open)
                return new ElevatorTaskResult(false, $"{Name} door is open. Close it first.");

            int startFloor = CurrentFloor;

            if (CurrentFloor != DestinationFloor)
            {
                IsMoving = true;                
                //Delayed start
                await Task.Delay(_secondsStopStart * 1000, cancellationToken);

                string movementDir = MovementDirection == MovementDirection.Up ? "up" : "down";
                var progress = new ElevatorProgress()
                {
                    currentFloor = CurrentFloor,
                    elevatorName = Name,
                    movement = $"{Name} started moving {movementDir} towards {HelperStatic.FloorName(DestinationFloor)} from {HelperStatic.FloorName(CurrentFloor)}"
                };
                progressFloor?.Report(progress);

                while (CurrentFloor != DestinationFloor) //(int f = startFloor; f <= DestinationFloor; f++)
                {
                    //Assume something went wrong - stop at current floor and don't pickup if cancelled:
                    if (cancellationToken.IsCancellationRequested)
                    {
                        //cancellationToken.ThrowIfCancellationRequested();
                        await StopAsync();
                        return new ElevatorTaskResult(false, $"{Name} {nameof(MoveToDestinationFloorAsync)} cancelled."); ;
                    }

                    if (CurrentFloor != startFloor)
                    {
                        await Task.Delay(_secondsPerFloor * 1000, cancellationToken); //Simulate movement per floor delay:
                        progress = new ElevatorProgress()
                        {
                            currentFloor = CurrentFloor,
                            elevatorName = Name,
                            movement = CurrentFloor != DestinationFloor ? $"{Name} moved {movementDir} past {HelperStatic.FloorName(CurrentFloor)} towards {HelperStatic.FloorName(DestinationFloor)}" : $"{Name} reached destination {HelperStatic.FloorName(CurrentFloor)}"
                        };
                        progressFloor?.Report(progress);
                    }

                    if (CurrentFloor > DestinationFloor)
                        CurrentFloor--;
                    else CurrentFloor++;

                }
            }

            //Destination reached:
            await StopAsync();
            return new ElevatorTaskResult(true, $"{Name} moved and stopped at {HelperStatic.FloorName(CurrentFloor)}.");

        }

        #endregion
    }
}
