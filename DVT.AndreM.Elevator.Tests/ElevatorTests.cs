using System.Diagnostics;
using System.Xml.Linq;

namespace DVT.AndreM.Elevator.Tests
{
    public class ElevatorTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ConstructElevator_Test()
        {
            var el = new Elevator("bla", 1);
            Assert.IsNotNull(el);
            Assert.IsNotEmpty(el?.Name);
            //etc.
        }

        [Test]
        public async Task MoveOpenElevator_Test()
        {
            var el = new Elevator("bla", 1);
            await el.OpenDoorAsync();
            var progressIndicator = new Progress<ElevatorProgress>(ReportProgress);
            var resultTask = await el.MoveToDestinationFloorAsync(progressIndicator, new CancellationTokenSource().Token);
            Assert.IsFalse(resultTask.completed);
            Debug.WriteLine(resultTask.description);
        }

        //etc.

        private void ReportProgress(ElevatorProgress currentProgress)
        {
            Debug.WriteLine(currentProgress.movement);
        }
    }
}