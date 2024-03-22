                                                    COP5615- Distributed Operating System Principles
                                                                    Fall 2023
                                                            Programming Assignment #3
                                                                    Team 4

                                    Ansh Khatri		   Abhinav Aryal 	        Nandan Dave		Manvi Gupta
                                    5754-5908		   3327-1507		        5364-2074		9050-7900
                            ansh.khatri@ufl.edu	   abhinavaryal@ufl.edu     nandandave@ufl.edu  manvigupta@ufl.edu

How to run:

Use the command “dotnet run numberOfNodes topologyName AlgorithmName” after opening the terminal in the directory of the file. 
dotnet run 5 line pushsum
Here, 5 is the number of nodes line is the name of the Topology and pushsum is the Name of the algorithm.

For running

Topology Name:
line, full, 2D, imp3D

Algorithm name: 
Gossip, pushsum






What is working

Actor Implementation

 To mimic a distributed system, the project effectively uses F# actors inside the Akka framework. Actors exchange messages to engage asynchronously as participants in the network.

Gossip Algorithm

When the gossip algorithm is used, actors disseminate rumours by choosing at random which of their neighbours to contact with information. The actors count how many times they have heard the rumour, cutting the feed after a set number of times (10 by default).

Push-Sum Algorithm

 The use of the Push-Sum method, in which each actor keeps track of two values (s and w), interacts with neighbours, and computes the sum using ratio convergence criteria, is another functional feature.

Topology Creation
Different network topologies (Line, Imperfect 3D Grid, Full Network, and 2D Grid) are created, and their arrangement determines how the actors interact.

Main Process & Termination
 When all players converge or the rumour spreads sufficiently, the main process ends the system. It also initiates the start of algorithms and records convergence.

Command-Line Input
 To define the number of actors, the topology to be used, and the method (e.g., dotnet run 20-line gossip), the project allows command-line input.








What is the largest network you managed to deal with for each type of topology and algorithm?

For all the topologies using Gossip Algorithm, we were able to implement up to 10000 nodes,
But while implementing the Line topology took significantly more time when compared to the other topologies.

For the PushSum we were also able to implement up to 10000 nodes but we were only able to implement 5000 nodes for the PushSum algorithm.






The Graphs can be seen in the report for both the algorithms.