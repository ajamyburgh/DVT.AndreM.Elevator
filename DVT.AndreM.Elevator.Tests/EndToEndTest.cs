using System.Diagnostics;
using System.Xml.Linq;

namespace DVT.AndreM.Elevator.Tests
{
    public class EndToEndTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task SingleElevator_TestAsync()
        {
            var testParams = new TestParams();
            testParams.ElevatorCount = 1;
            testParams.TestElevatorKey = 1;
            testParams.MinFloor = -1;
            testParams.MaxFloor = 5;
            testParams.ElevatorMaxOccupancy = 5;
            testParams.TestElevatorStartFloor = 1;
            testParams.TestElevatorStartCapacity = 1;
            testParams.DestinationFloor = 4;
            testParams.PeopleAtDestination = 3;

            var elevatorManager = new ElevatorManager(testParams.ElevatorCount, testParams.MinFloor, testParams.MaxFloor, testParams.ElevatorMaxOccupancy);

            Assert.IsTrue(elevatorManager.Elevators.ContainsKey(testParams.TestElevatorKey));
            
            //Set test elevator
            var elevator = elevatorManager.Elevators[testParams.TestElevatorKey];
            Assert.IsNotNull(elevator);

            elevator.Name = $"Elevator{testParams.TestElevatorKey}";
            elevator.CurrentFloor = testParams.TestElevatorStartFloor;
            elevator.CurrentOccupancy = testParams.TestElevatorStartCapacity;

            //nearest = test
            var nearestElevator = elevatorManager.NearestElevator(testParams.DestinationFloor, testParams.PeopleAtDestination);
            Assert.That(nearestElevator, Is.SameAs(elevator));

            //Move up 
            await MoveElevatorAsync(elevatorManager, elevator, testParams.DestinationFloor);

            //Move down and fill to max
            testParams.DestinationFloor = -1;
            testParams.PeopleAtDestination = 2;
            elevatorManager.SetPeopleWaiting(testParams.DestinationFloor, testParams.PeopleAtDestination);
            await MoveElevatorAsync(elevatorManager, elevator, testParams.DestinationFloor);

            //Move up?
            testParams.DestinationFloor = 0;
            testParams.PeopleAtDestination = 2;
            elevatorManager.SetPeopleWaiting(testParams.DestinationFloor, testParams.PeopleAtDestination);
            await MoveElevatorAsync(elevatorManager, elevator, testParams.DestinationFloor);
        }

        [Test]
        public async Task ParallelElevator_TestAsync()
        {
            var testParams = new TestParams();
            testParams.ElevatorCount = 3;
            testParams.MinFloor = -1;
            testParams.MaxFloor = 5;
            testParams.ElevatorMaxOccupancy = 5;

            var elevatorManager = new ElevatorManager(testParams.ElevatorCount, testParams.MinFloor, testParams.MaxFloor, testParams.ElevatorMaxOccupancy);

            Assert.IsTrue(elevatorManager.Elevators.Count == testParams.ElevatorCount);

            //Set UP test elevator
            testParams.DestinationFloor = 4;
            var elevatorFull = elevatorManager.Elevators[1];
            elevatorFull.CurrentFloor = 2;
            elevatorFull.CurrentOccupancy = testParams.ElevatorMaxOccupancy;
            elevatorFull.Name = "FullElevator";
            var elevatorUp = elevatorManager.Elevators[2];
            elevatorUp.CurrentFloor = 2;
            elevatorUp.CurrentOccupancy = 0;
            elevatorFull.Name = "UpElevator";
            var nearestElevator = elevatorManager.NearestElevator(testParams.DestinationFloor, 5);
            //Don't send full elevator
            Assert.That(nearestElevator, Is.SameAs(elevatorUp));

            //Move up 
            var UpTask = MoveElevatorAsync(elevatorManager, nearestElevator, testParams.DestinationFloor);

            //Set DOWN test elevator
            testParams.DestinationFloor = -1;
            var elevatorDown = elevatorManager.Elevators[3];
            elevatorDown.CurrentFloor = 1;
            elevatorDown.CurrentOccupancy = 1;
            elevatorDown.Name = "DownElevator";
            nearestElevator = elevatorManager.NearestElevator(testParams.DestinationFloor, 5);
            //Don't send full elevator
            Assert.That(nearestElevator, Is.SameAs(elevatorDown));

            //Move down 
            var DownTask = MoveElevatorAsync(elevatorManager, nearestElevator, testParams.DestinationFloor);

            await Task.WhenAll(UpTask, DownTask);
        }

        private async Task MoveElevatorAsync(ElevatorManager elevatorManager, Elevator nearestElevator, int destinationFloor)
        {
            var progressIndicator = new Progress<ElevatorProgress>(ReportProgress);
            var resultTask = await elevatorManager.MoveToFloorAsync(nearestElevator, destinationFloor, progressIndicator, new CancellationTokenSource().Token);
            
            Assert.IsTrue(resultTask.completed);
            Debug.WriteLine(resultTask.description);
            Assert.IsTrue(nearestElevator.CurrentFloor == destinationFloor);
            Assert.IsTrue(nearestElevator.IsMoving == false);
            Assert.IsTrue(nearestElevator.DoorStatus == DoorState.Open);
            Assert.IsTrue(nearestElevator.CurrentOccupancy <= nearestElevator.OccupancyLimit);
        }

        private void ReportProgress(ElevatorProgress currentProgress)
        {
            Debug.WriteLine(currentProgress.movement);
        }
    }
}