# Welcome to...
  <img width="458" height="257" alt="Jubi_Logo" src="https://github.com/user-attachments/assets/0a9fa22f-1b69-4cb8-a3bb-d7a0a884e48f" />

A Kanban Solution to track the assembly of fog lamps in a factory setting.

How to Set up / Run The Jubi Fog Lamp Kanban Simulation
-
1. Run the Configuration Tool Program, edit and save the values in the confuration table to adjust the parameters for your simulation.
   - Recommended Configuration: Change SimulationTimeScale to 20.0 so that it takes about 3 seconds per lamp to be built in-simulation
2. Open up the Runner Display and the Assembly Line Kanban Display
3. Open one instance of the Workstation Simulation for each of the Workstations you have in your Assembly Area.
   a. Select the Worker working at that station from the worker drop down
   b. Select the Workstation that the worker is working at from the workstation drop down
   c. Press Start to start the lamp assembly at that station
   d. Repeat for each Workstation
4. Start the Runner Display by pressing the green start button
       - the runner will recieve red notifications from bins that need refilling with details like what part and which work station and when the runner refills a bin a green notification will appear in the runner display
5. Start the Workstation Andon Display - use the dropdown to select which Workstation stats to view (you can also open multiple instances of this program if you want to see multiple Workstation details at the same time
