// Importing necessary modules
module Program

open Akka
open System
open Gossip
open Schedulers
open Akka.Actor
open Akka.Actor.Scheduler
open Akka.FSharp
open System.Diagnostics
open System.Collections.Generic
open Akka.Configuration


// Configuration for Akka system
let config = 
    ConfigurationFactory.ParseString(
        @"akka {            
            stdout-loglevel : ERROR
            loglevel : ERROR
            log-dead-letters = 0
            log-dead-letters-during-shutdown = off
        }")

// Setting the parameters for pushsum algo
let PushSumC = 20
let PushSumR = 70.0
// Create Akka actor system
let Gsystem = ActorSystem.Create("Gsystem", config)
let mutable mainActorRef = null

// Define custom message types for communication between actors
type MainCommands =
    | SearchNeighbour of (list<IActorRef> * string)
    | Topology of (string)
    | StopTrans of (string)
    | AlgoStart of (string)
    | GossipNote of (int * string)
    | Cycle
    | Ireceived of (int)
    | ImmediateN of (int)
    | PSMessage of (int * float * float)
    | StartPS
    | ActorConvereged of (int * float * float)

// Globals
let mutable N = 100
let mutable topo = "3D"
let mutable advanceAlgo = "pushsum"
let mutable findActor = []
let mutable IMP = false
let TimeCheck = Stopwatch()

// For 3D grid, rounding off the total nodes to the next nearest cube
let Node3D (N:int) =
    let sides = float N |> Math.Cbrt |> ceil |> int
    pown sides 3

// For 2D grid, rounding off the total nodes to the next nearest square
let Node2D (N:int) =
    let sides = float N |> sqrt |> ceil |> int
    pown sides 2



// Function to find neighbors based on the specified topo
let searchNodeNeighbour (pool: list<IActorRef>, topo: string, actorIxd: int) = 
    // Function to retrieve neighbors based on different network topologies
    let mutable myNeighbours = []
    match topo with 
    | "line" ->
        myNeighbours <- Gossip.findLineNeighboursFor(pool, actorIxd, N)
    | "2D" | "2d" ->
        let side = N |> float |> sqrt |> int
        myNeighbours <- Gossip.find2DNeighboursFor(pool, actorIxd, side, N, IMP)
    |"imp3D" | "imp3d" ->
        let side = N |> float |> Math.Cbrt |> int
        myNeighbours <- Gossip.find3DNeighboursFor(pool, actorIxd, side, (side * side), N, IMP)
    | "full" ->
        myNeighbours <- Gossip.findFullNeighboursFor(pool, actorIxd, N)
    | _ -> ()
    myNeighbours

// Gossip actor behavior
let ActorGos (id: int) (mailbox: Actor<_>) =
    // Function to simulate the behavior of Gossip actors
    let mutable arrayNeighbour = []
    let mutable rCount = 0
    let actorIxd = id
    let mutable active = 1

    let rec loop () = actor {
        if active = 1 then
            let! (message) = mailbox.Receive()

            match message with 
            // Handling different types of messages for Gossip actors
            | SearchNeighbour(pool, topo) ->
                arrayNeighbour <- searchNodeNeighbour(pool, topo, actorIxd)
                mainActorRef <! ImmediateN(actorIxd)

            | GossipNote(fromNode, message) ->
                // Handling received gossip messages
                if rCount = 0 then
                    mainActorRef <! Ireceived(actorIxd)
                    mailbox.Self <! Cycle

                rCount <- rCount + 1

                if rCount > 10 then
                    active <- 0
                    mainActorRef <! StopTrans(mailbox.Self.Path.Name)

            | Cycle(_) ->
                // Sending Gossip messages to neighbors
                // Find a random neighbor from the neighbor list
                let ranNeigh = Random().Next(arrayNeighbour.Length)
                let ranAct = arrayNeighbour.[ranNeigh]
                // Send GossipNote to it
                ranAct <! GossipNote(actorIxd, "rumour")
                Gsystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(10.0), mailbox.Self, Cycle, mailbox.Self)

            | _ -> ()

            return! loop()
        }
    loop()

// Pushsum actor behavior
let ActorPS (actorIxd: int) (mailbox: Actor<_>) =
    // Function to simulate the behavior of PushSum actors
    // Similar structure to the Gossip actor behavior with differences in message handling
    // Mutable variables to hold actor-specific state and data
    let mutable arrayNeighbour = []          // List to store neighbors of the current actor
    let mutable s = actorIxd + 1 |> float   // Variable 's' for sum value initialized with actor index + 1
    let mutable w = 1.0                     // Variable 'w' for weight initialized to 1.0
    let mutable prevRatio = s / w            // Previous ratio of sum/weight initialized
    let mutable currRatio = s / w            // Current ratio of sum/weight
    let mutable towardsEnd = 0               // Counter for tracking convergence criteria
    let mutable active = 1                   // Flag to indicate if the actor is active
    let mutable incomingsList = []           // List to accumulate incoming sums from neighbors
    let mutable incomingwList = []           // List to accumulate incoming weights from neighbors
    let mutable s_aggregateTminus1 = 0.0     // Variable for storing the aggregated sum from the previous round
    let mutable w_aggregateTminus1 = 0.0     // Variable for storing the aggregated weight from the previous round
    let mutable count = 0                    // Counter to track incoming messages

    let rec loop () = actor {
        if active = 1 then
            // Await incoming messages from the mailbox
            let! (message) = mailbox.Receive()

            // Match the received messages to perform corresponding actions
            match message with 
            | SearchNeighbour(pool, topo) ->
                // Update neighbor list based on the provided topology
                arrayNeighbour <- searchNodeNeighbour(pool, topo, actorIxd)
                // Notify the main actor about immediate neighbor discovery
                mainActorRef <! ImmediateN(actorIxd)

            | Cycle(_) ->
                
                // Step 2: s <- Σ(incomingsList) and w <- Σ(incomingwList)
                s <- s_aggregateTminus1
                w <- w_aggregateTminus1

                // Step 3: Choose a random neighbor
                let ranNeigh = Random().Next(0, arrayNeighbour.Length)
                let ranAct = arrayNeighbour.[ranNeigh]

                // Step 4: Send the pair ( ½s , ½w ) to ranNeigh and self
                ranAct <! PSMessage(actorIxd, (s / 2.0), (w / 2.0))
                mailbox.Self <! PSMessage(actorIxd, (s / 2.0), (w / 2.0))

                // Check for convergence
                currRatio <- s / w
                if (abs(currRatio - prevRatio)) < (pown 10.0 -10) then 
                    towardsEnd <- towardsEnd + 1
                else 
                    towardsEnd <- 0
                // If the convergence criteria are met, notify the main actor and stop the actor
                if towardsEnd = PushSumC then 
                    mainActorRef <! ActorConvereged(actorIxd, s, w)
                    active <- 0

                prevRatio <- currRatio

                // Reset the aggregate back to 0 after each round
                s_aggregateTminus1 <- 0.0
                w_aggregateTminus1 <- 0.0
                count <- 0
                       
            | PSMessage (fromIndex, incomings, incomingw) ->
                // Process incoming sum and weight values from neighbors
                count <- count + 1
                s_aggregateTminus1 <- s_aggregateTminus1 + incomings
                w_aggregateTminus1 <- w_aggregateTminus1 + incomingw

            | StartPS ->
                // Trigger the starting message to initialize the PushSum algorithm
                mailbox.Self <! PSMessage(actorIxd, s, w)
                // Schedule the recurring message to trigger 'Cycle'
                Gsystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.0), TimeSpan.FromMilliseconds(PushSumR), mailbox.Self, Cycle)
            | _ -> ()

            return! loop()
        }
    loop()

// Main actor behavior
let MainActor (mailbox: Actor<_>) =    
    let mutable actorsDone = 0
    let mutable actorsThatKnow = 0
    let mutable topologyBuilt = 0

    let rec loop () = 
        actor {
            let! (message) = mailbox.Receive()

            match message with 
            | Topology(_) ->
                if advanceAlgo = "gossip" then    
                    findActor <-
                        [0 .. N - 1]
                        |> List.map(fun id -> spawn Gsystem (sprintf "ActorGos_%d" id) (ActorGos id))
                else
                    findActor <- 
                        [0 .. N - 1]
                        |> List.map(fun id -> spawn Gsystem (sprintf "ActorPS_%d" id) (ActorPS id))

                findActor |> List.iter (fun item -> 
                    item <! SearchNeighbour(findActor, topo))

            | ImmediateN(index) ->
                topologyBuilt <- topologyBuilt + 1
                if topologyBuilt = N then 
                    printfn "Topology completely built! \n"
                    TimeCheck.Start()
                    mainActorRef <! AlgoStart(advanceAlgo)

            | AlgoStart(advanceAlgo) ->
                // Start the specified advanceAlgo
                match advanceAlgo with 
                | "gossip" ->
                    // Randomly select a neighbor
                    let ranNeigh = Random().Next(findActor.Length)
                    let ranAct = findActor.[ranNeigh]
                    // Send GossipNote to it
                    ranAct <! GossipNote(0, "theRumour")
                | "pushsum" ->
                    // Initialize all actors for pushsum advanceAlgo
                    findActor |> List.iter (fun item -> 
                        item <! StartPS)
                | _ -> ()

            | StopTrans(actorName) ->
                actorsDone <- actorsDone + 1


            | Ireceived(actorIndex) ->
                actorsThatKnow <- actorsThatKnow + 1
                if actorsThatKnow = N then 
                    TimeCheck.Stop()
                    printfn "Total time = %d ms " TimeCheck.ElapsedMilliseconds
                    Environment.Exit(0)

            | ActorConvereged (index, s, w) ->
                actorsDone <- actorsDone + 1
                if actorsDone = N then 
                    printfn "\nConverged, Done for all nodes!!"
                    TimeCheck.Stop()
                    printfn "Total time = %dms" TimeCheck.ElapsedMilliseconds
                    Environment.Exit(0)

            | _ -> ()

            return! loop()
        }
    loop()

// Entry point of the program
[<EntryPoint>]
let main argv =
    // Check for command-line arguments
    if (argv.Length <> 3) then 
        printfn "You didn't give any value so using default value" 
    else 
        // Parse command-line arguments
        topo <-  argv.[1]
        advanceAlgo <- argv.[2]
        if topo = "2D" || topo = "imp2D" || topo = "imp2d" || topo = "2d"  then N <-  argv.[0] |> int |> Node2D
        else if topo = "3D" || topo = "imp3D" || topo = "imp3d" || topo = "3d"  then N <-  argv.[0] |> int |> Node3D
        else N <- argv.[0] |> int

        if topo = "imp2D" || topo = "imp2d" || topo = "imp3D" || topo = "imp3d" then IMP <- true
   
    // Create main actor
    mainActorRef <- spawn Gsystem "MainActor" MainActor
    mainActorRef <! Topology("start")

    // Wait for the Akka system to terminate
    Gsystem.WhenTerminated.Wait()

    0
