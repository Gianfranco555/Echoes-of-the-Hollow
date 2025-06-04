# NavMesh Baking Guide

This checklist explains how to bake a navigation mesh for `House_MainLevel.unity` once all static geometry and door obstacles are placed.

## 1. Mark Static Geometry
1. Select the foundation, walls, floors and any other non-moving geometry.
2. In the **Inspector** enable **Navigation Static** so they are included in the bake.

## 2. Configure NavMesh Agents
1. Add a **NavMeshAgent** component to each character that needs to navigate.
2. Set agent properties as required (the project defaults are radius **0.5**, height **2**, slope **45** and step height **0.75**).

## 3. Set Up Door Obstacles
1. For sliding or hinged doors that block movement when closed, add a **NavMeshObstacle** component.
2. Enable **Carve** so the obstacle removes its space from the NavMesh whenever the door is closed.

## 4. Bake the NavMesh
1. Open **Unity &gt; AI &gt; Navigation** to display the Navigation window.
2. On the **Bake** tab, review the agent settings and bake options.
3. Click **Bake** to generate the NavMesh for the scene.
