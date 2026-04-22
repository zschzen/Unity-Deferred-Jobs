
# Unity Deferred Jobs

A basic Unity prototype for **deferred visibility queries** using the C# Job System, Burst, batched raycasts and time slicing.

<img width="888" height="957" alt="screenshot" src="https://github.com/user-attachments/assets/0faa627a-d95f-4e55-836a-7318da2068f2" />

Built after studying and implementing ideas from Allen Chou's game programming series:

- [Delayed Result Gathering](https://allenchou.net/2021/05/delayed-result-gathering/)
- [Time Slicing](https://allenchou.net/2021/05/time-slicing/)

> [!NOTE]
> The goal is not to build a production exposure-map updater. The exposure map is only a test case for experimenting with these optimization techniques.

## Overview


The project simulates an actor scanning a grid while a runner tries to stay hidden.

```mermaid
flowchart LR
    Actor["Actor position"] --> Slice["Select ray slice"]
    Slice --> Setup["RaycastSetupJob"]
    Setup --> Batch["RaycastCommand.ScheduleBatch"]
    Batch --> Gather["RaycastGatherJob"]
    Gather --> Map["Exposure map"]
    Map --> Grid["Grid colors<br/>red = visible<br/>green = hidden"]
    Map --> Runner["Runner finds nearest hidden tile"]
    Runner --> NavMesh["NavMeshAgent moves runner"]
```

Visibility work is processed in small slices. Completed job results update the exposure map, color the grid, and guide the runner toward the nearest hidden tile.

## Features

- Time-sliced raycast batches
- Delayed result gathering across frames
- Burst-compiled Unity jobs
- Grid exposure visualization
- Runtime ray-budget slider

## Run

1. Open with Unity `2022.3.62f3`
2. Open `Assets/Scenes/SampleScene.unity`
3. Press Play
4. Left click a tile to move the actor
5. Use the slider to change rays processed per frame

## Packages

- Unity Burst
- Unity Jobs
- Unity AI Navigation
- [Graphy stats monitor](https://github.com/Tayx94/graphy)
