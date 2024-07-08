using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVT.AndreM.Elevator
{
    public class ElevatorManager
    {
        //Threadsafe for future:
        private ConcurrentDictionary<int, Elevator> _elevators;

        //TODO: Threadsafe for future:
        private Dictionary<int, int> _floors = new Dictionary<int, int>(); //Only "people waiting" at this point. 
        private static int _secondsPickup = int.TryParse(ConfigurationManager.AppSettings["SecondsPickup"], out _secondsPickup) ? _secondsPickup : 10;

        /// <summary>
        /// Constroller for elevator system.
        /// </summary>
        /// <param name="elevators"></param>
        /// <param name="minFloor"></param>
        /// <param name="maxFloor"></param>
        /// <param name="elevatorMaxOccupancy"></param>
        public ElevatorManager(ConcurrentDictionary<int, Elevator> elevators, int minFloor, int maxFloor, int elevatorMaxOccupancy) 
        {
            _elevators = elevators;
            Initialize(minFloor, maxFloor, elevatorMaxOccupancy);
        }

        /// <summary>
        /// Assume all start on ground floor with zero occupants
        /// </summary>
        /// <param name="elevatorCount"></param>
        /// <param name="minFloor">Lowest floor number (one basement = -1</param>
        /// <param name="maxFloor">Highest floor number (ground floor = 1)</param>
        /// <param name="elevatorMaxOccupancy"></param>
        public ElevatorManager(int elevatorCount, int minFloor, int maxFloor, int elevatorMaxOccupancy)
        {
            _elevators = new ConcurrentDictionary<int, Elevator>();
            for (int i = 1; i <= elevatorCount; i++) 
            {
                _elevators.TryAdd(i, new Elevator($"Elevator{i}", elevatorMaxOccupancy));
            };
            Initialize(minFloor, maxFloor, elevatorMaxOccupancy);
        }

        private void Initialize(int minFloor, int maxFloor, int elevatorMaxOccupancy)
        {
            if (_elevators?.Count < 0) throw new ArgumentNullException($"You need elevators. elevatorCount ({_elevators?.Count})");
            if (elevatorMaxOccupancy <= 0) throw new ArgumentNullException($"You need bigger elevators. elevatorMaxOccupancy ({elevatorMaxOccupancy})");
            if (minFloor == maxFloor) throw new ArgumentOutOfRangeException($"Only one floor. You don't need elevators ...");
            if (minFloor > maxFloor) throw new ArgumentOutOfRangeException($"minFloor ({minFloor}) must be lower than maxFloor ({maxFloor})");

            //Assume all floors are elevator stops
            for (int i = minFloor; i <= maxFloor; i++)
            {
                _floors.Add(i, 0); //Default to zero people waiting for now.
            }
        }

        /// <summary>
        /// This requirement is a bit misleading. Average elevator systems would not have this input (without computer vision)
        /// Assuming this suggests the number of people that will try get into the elevator sent to that floor
        /// </summary>
        /// <param name="floorNumber"></param>
        /// <param name="peopleCount"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void SetPeopleWaiting(int floorNumber, int peopleCount)
        {
            if (!_floors.ContainsKey(floorNumber))
                throw new ArgumentOutOfRangeException($"No such floor");
            if (peopleCount < 0)
                throw new ArgumentOutOfRangeException($"peopleCount ({peopleCount}) invalid");

            _floors[floorNumber] = peopleCount;
        }

        public int GetPeopleWaiting(int floorNumber)
        {
            if (!_floors.ContainsKey(floorNumber))
                throw new ArgumentOutOfRangeException($"No such floor");
            
            return _floors[floorNumber];
        }

        public ConcurrentDictionary<int, Elevator> Elevators
        {
            get { return _elevators; }
            set { _elevators = value; }
        }

        /// <summary>
        /// Finds the nearest avalaible elevator (!e.DestinationFloor.HasValue  && !e.IsMoving && !e.OccupancyLimitReached)
        /// Prioritises elevators with enough capacity to take all people waiting on floor
        /// </summary>
        /// <param name="floorNumber"></param>
        /// <param name="peopleCountWaiting"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Elevator NearestElevator(int floorNumber, int? peopleCountWaiting = null)
        {
            if (!_floors.ContainsKey(floorNumber))
                throw new ArgumentOutOfRangeException($"No such floor");
            if (peopleCountWaiting.HasValue && peopleCountWaiting.Value < 0)
                throw new ArgumentOutOfRangeException($"peopleCount ({peopleCountWaiting}) invalid");

            //Not clear from requirement at which point this should be controlled. Allowing both
            if(peopleCountWaiting.HasValue)
                SetPeopleWaiting(floorNumber, peopleCountWaiting.Value);

            //Assumptions:
            //  Prioritise elevators that can service all people waiting. Else send nearest elevator with less capacity (ignore _floor[floorNumber].Value)
            //  All people are same size and we do not cater for wheel-chairs..
            //  "Nearest" in requirement suggests we only cater for relative floor distance and ignore elevators already moving and/or their direction.
            //      i.e. elevators only have a destination and no scheduled stops along a route.

            var nearestQ = _elevators.Values.Where(e => !e.DestinationFloor.HasValue
                                                    && !e.IsMoving
                                                    && !e.OccupancyLimitReached);
            var nearest = nearestQ.Where(e => e.AvailableOccupancy >= _floors[floorNumber])
                                    .OrderBy(e => Math.Abs(e.CurrentFloor - floorNumber)).FirstOrDefault();

            if (nearest == null)
                nearest = nearestQ.OrderBy(e => Math.Abs(e.CurrentFloor - floorNumber)).FirstOrDefault();

            return nearest;
        }

        /// <summary>
        /// Stop, close door, moves elevator to destination floor, stop, opens door and load awaiting passengers.
        /// ToDo: Refactor this crude implementation
        /// </summary>
        /// <param name="destinationFloor">Floor to go to</param>
        /// <param name="progressFloor">Option to show progress. TODO: expand to include MovementStatus with Elevator name, movement, current floor, etc</param>
        /// <param name="cancellationToken">Allows elevator to be stopped at floors along the movement direction or be redirected</param>
        /// <returns></returns>
        public async Task<ElevatorTaskResult> MoveToFloorAsync(Elevator elevator, int destinationFloor, IProgress<ElevatorProgress> progressFloor, CancellationToken cancellationToken)
        {
            //Validation
            if (elevator == null)
                throw new ArgumentNullException(nameof(elevator));

            if (!_floors.ContainsKey(destinationFloor))
                throw new ArgumentOutOfRangeException(nameof(destinationFloor), destinationFloor,
                    $"Should be between {_floors.Keys.Min()} and {_floors.Keys.Max()}");

            await elevator.StopAsync();            

            elevator.DestinationFloor = destinationFloor;

            if (destinationFloor != elevator.CurrentFloor) //Close door and move
            {
                await elevator.CloseDoorAsync();
                var moveTask = await elevator.MoveToDestinationFloorAsync(progressFloor, cancellationToken);
                if(!moveTask.completed)
                    return moveTask;
            }

            await elevator.OpenDoorAsync();
            await LoadElevatorAsync(elevator);

            return new ElevatorTaskResult(true, $"{elevator.Name} at {HelperStatic.FloorName(destinationFloor)}. Stopped and door opened. Loaded to {elevator.CurrentOccupancy} person capacity.");

        }

        /// <summary>
        /// Load passengers. Assume it will fill to capacity with all awaiting people (and nobody gets off ??)
        /// </summary>
        public async Task<bool> LoadElevatorAsync(Elevator elevator)
        {
            //TODO: Validation of elevator status:
            
            var floorWaiting = _floors[elevator.CurrentFloor];

            //Only open and load if someone is waiting? Some people might want to get off?
            //if (floorWaiting > 0) 
            
            //Simulate pickup delay:
            await Task.Delay(_secondsPickup * 1000);
            //Assumption: fill to capacity
            int newPassengers = Math.Min(floorWaiting, elevator.AvailableOccupancy);
            elevator.CurrentOccupancy += newPassengers;
            _floors[elevator.CurrentFloor] -= newPassengers;

            return true;
        }        
    }
}
