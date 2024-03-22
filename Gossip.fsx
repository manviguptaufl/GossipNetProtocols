module Gossip

open System
open Akka.Actor
open Akka.FSharp

// Full/Mesh Topology
// All nodes are connected to all other nodes.
let findFullNeighboursFor (actors: list<IActorRef>, currentIndex: int, totalNodes: int) =
    // Enumerate the list of actors, filter out the current actor, and extract only the actor references to form the neighbors.
    let neighbours = actors |> List.indexed |> List.filter (fun (i, _) -> i <> currentIndex) |> List.map snd
    neighbours


// Line Topology
// Single-dimensional line. Max 2 neighbors possible - X-axis: left & right.
let findLineNeighboursFor (actors: list<IActorRef>, currentIndex: int, totalNodes: int) =
    let mutable neighbours = []
    // X-axis neighbors
    if currentIndex <> 0 then
        neighbours <- actors.[currentIndex - 1] :: neighbours // Add left neighbor
    if currentIndex <> totalNodes - 1 then
        neighbours <- actors.[currentIndex + 1] :: neighbours // Add right neighbor
    neighbours


// 2D Grid Topology
// Simple square topology. Max 4 neighbors possible - X-axis: left & right | Y-axis: top & bottom.
let find2DNeighboursFor (actors: list<IActorRef>, currentIndex: int, side: int, totalNodes: int, isImproper: bool) =
    let mutable neighbours = []
    // X-axis neighbors
    if currentIndex % side <> 0 then
        neighbours <- actors.[currentIndex - 1] :: neighbours // Add left neighbor
    if currentIndex % side <> side - 1 then
        neighbours <- actors.[currentIndex + 1] :: neighbours // Add right neighbor
    // Y-axis neighbors
    if currentIndex - side >= 0 then
        neighbours <- actors.[currentIndex - side] :: neighbours // Add top neighbor
    if currentIndex + side <= totalNodes - 1 then
        neighbours <- actors.[currentIndex + side] :: neighbours // Add bottom neighbor

    // If it's an improper topology, add one additional random node
    if isImproper then
        let random = System.Random()
        let additionalNeighbour =
            actors
            |> List.filter (fun x -> (x <> actors.[currentIndex] && not (List.contains x neighbours)))
            |> fun filteredActors -> filteredActors.[random.Next(filteredActors.Length - 1)]
        neighbours <- additionalNeighbour :: neighbours

    neighbours


// 3D Grid Topology
// Simple cube topology. Max 6 neighbors possible - X-axis: left & right | Y-axis: top & bottom | Z-axis: front & back
let find3DNeighboursFor (actors: list<IActorRef>, currentIndex: int, side: int, sidesquare: int, totalNodes: int, isImproper: bool) =
    let mutable neighbours = []
    // X-axis neighbors
    if currentIndex % side <> 0 then
        neighbours <- actors.[currentIndex - 1] :: neighbours // Add left neighbor
    if currentIndex % side <> side - 1 then
        neighbours <- actors.[currentIndex + 1] :: neighbours // Add right neighbor
    // Y-axis neighbors
    if currentIndex % sidesquare >= side then
        neighbours <- actors.[currentIndex - side] :: neighbours // Add top neighbor
    if sidesquare - (currentIndex % sidesquare) > side then
        neighbours <- actors.[currentIndex + side] :: neighbours // Add bottom neighbor
    // Z-axis
    if currentIndex >= sidesquare then
        neighbours <- actors.[currentIndex - sidesquare] :: neighbours // Add front neighbor
    if (totalNodes - currentIndex) > sidesquare then
        neighbours <- actors.[currentIndex + sidesquare] :: neighbours // Add back neighbor

    // If the topology is improper, add one additional random node
    if isImproper then
        let random = System.Random()
        let additionalNeighbour =
            actors
            |> List.filter (fun x -> (x <> actors.[currentIndex] && not (List.contains x neighbours)))
            |> fun filteredActors -> filteredActors.[random.Next(filteredActors.Length - 1)]
        neighbours <- additionalNeighbour :: neighbours

    neighbours
