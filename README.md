# Console Elevator Challenge

Had fun with this one, but only had a Sunday afternoon available. It's **not** a complete **Elevator Control System** by any stretch of the imagination, but does meet *some* of the requirements specified below.
***ToDo:***
 - [ ] Complete Unit, Integration, and End-to-End Tests
 - [ ] Add Elevator status-changed events (instead of IProgress for status updates)
 - [ ] DI for logger and elevator system config injection
 - [ ] Error handling
 - [ ] Thread-safe Floors for ElevatorManager
 - [ ] Implement interfaces for Elevator and ElevatorManager
 - [ ] Separate Controller interfaces for Elevator and Floor buttons
 - [ ] Elevator system setup and Elevator control via console user input
 - [ ] Propagate cancellationToken throughout

## Requirement
![alt text](https://github.com/ajamyburgh/DVT.AndreM.Elevator/blob/master/DVT.AndreM.Elevator/spec.png?raw=true)

## Assumptions & Comments
The requirement specifies an (over)simplified elevator control system, and some assumptions were made that affected the implementation:
 - All people are same size and we do not cater for wheel-chairs.
 - "Nearest available elevator" in requirement suggests we only cater for relative floor distance and ignore elevators already moving and/or their direction. (*i.e.* Elevators only have a destination and no scheduled stops along a route.)
 - Prioritise elevators that can service all people waiting. Else send nearest elevator with less capacity
 - Elevator system has computer vision to allow "setting the number of people waiting on each floor". Assuming this suggests the number of people that will try get into the elevator sent to that floor.
 - Loading passengers: Assume it will fill to capacity with all awaiting people (and nobody gets off ??)

## Output
![alt text](https://github.com/ajamyburgh/DVT.AndreM.Elevator/blob/master/DVT.AndreM.Elevator/results.png?raw=true)


