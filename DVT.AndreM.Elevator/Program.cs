// See https://aka.ms/new-console-template for more information
using DustInTheWind.ConsoleTools.Controls.Tables;
using DVT.AndreM.Elevator;
using static System.Net.Mime.MediaTypeNames;

Console.WriteLine("Elevator by André Myburgh");
Console.WriteLine("(Hack the Program.cs to Initialize a different Elevator simulation)");
Console.WriteLine("Any key to start");
Console.ReadLine();

var tokenS = new CancellationTokenSource();
var token = tokenS.Token;
var sim = new Simulator();

await sim.Start(token);
await sim.Inject(token);

tokenS.Cancel();

sim.StopSimulation();

Console.WriteLine("---------------------");
Console.WriteLine("Simulation completed!");
Console.WriteLine("---------------------");





