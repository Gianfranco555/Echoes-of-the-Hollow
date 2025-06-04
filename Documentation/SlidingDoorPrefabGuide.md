# Sliding Door Prefab Guide

This guide describes how to create the `Door_Sliding.prefab` used for the Dining Room east wall (Blueprint Section 6.2).

## Using the Editor Script
1. Open the Unity project.
2. In the menu bar select **Tools > Create Sliding Door Prefab**.
3. The script generates `Assets/Prefabs/Door_Sliding.prefab` containing:
   - A root GameObject named `Door_Sliding`.
   - `Panel_Left` and `Panel_Right` cube meshes sized `0.91 x 2.03 x 0.05` meters.
   - `SlidingDoorController` on the root with the right panel assigned as the sliding panel.
4. The right panel will slide left behind the static left panel when `ToggleDoorState` is called.

## Manual Setup
If you prefer to create the prefab manually:
1. Create an empty GameObject named `Door_Sliding` at the origin.
2. Add two cube children:
   - **Panel_Left** at local position `(-0.455, 1.015, 0)`.
   - **Panel_Right** at local position `(0.455, 1.015, 0)`.
   Each cube should have a local scale of `0.91, 2.03, 0.05`.
3. Attach `SlidingDoorController` to the root and assign **Panel_Right** as the sliding panel. Ensure **Slides Left** is checked so the panel moves behind the left panel when opening.
4. Drag the root into the `Assets/Prefabs` folder to create `Door_Sliding.prefab`.
