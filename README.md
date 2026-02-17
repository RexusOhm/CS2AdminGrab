# Admin Grab

CounterStrikeSharp plugin for CS2 that allows server administrators to grab and move players and physics objects using telekinesis-style mechanics.

## Features

- Grab players and physics objects (props, weapons, grenades, chickens)
- Spring-damper physics for smooth movement
- Automatic collision detection with distance correction
- Visual feedback: cyan beam to target + wireframe bounding box
- Distance control via mouse buttons and commands
- Separate permission flags for players and physics objects
- All physics parameters configurable via JSON

## Requirements

- CounterStrikeSharp **v1.0.362+**
- .NET 8.0

## Installation

1. Build the project or download the release
2. Copy the plugin folder to `csgo/addons/counterstrikesharp/plugins/CS2_Admin_Grab/`
3. Restart the server or hot-reload the plugin

## Commands

| Command | Description |
|---------|-------------|
| `css_grab` | Toggle grab (pick up / release target) |
| `css_grab_in` | Increase grab distance |
| `css_grab_out` | Decrease grab distance |

While holding a target, `LMB` / `RMB` also adjust distance.

## Configuration

File: `csgo/addons/counterstrikesharp/configs/plugins/CS2_Admin_Grab/CS2_Admin_Grab.json`

```json
{
  "Allow grab physics objects": true,
  "Permission Flag Players": "@css/ban",
  "Permission Flag Physics": "@css/vip",
  "Gain": 25.0,
  "Damping": 0.6,
  "Max Velocity": 1500.0,
  "Stuck Threshold": 0.5,
  "Min Distance": 60.0,
  "Max Distance": 2000.0,
  "Scroll Step": 25.0,
  "Button Step": 10.0
}
```

### Permission Flags

Players and physics objects have separate permission flags, allowing fine-grained access control.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Permission Flag Players` | `@css/ban` | Permission required to grab players |
| `Permission Flag Physics` | `@css/vip` | Permission required to grab physics objects |

### Physics Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Allow grab physics objects` | `true` | Master switch for physics object grabbing |
| `Gain` | `25.0` | Spring force constant (higher = faster pull) |
| `Damping` | `0.6` | Velocity damping (lower = more momentum) |
| `Max Velocity` | `1500.0` | Maximum target velocity (units/sec) |
| `Stuck Threshold` | `0.5` | Collision detection threshold (units) |
| `Min Distance` | `60.0` | Minimum grab distance |
| `Max Distance` | `2000.0` | Maximum grab distance |
| `Scroll Step` | `25.0` | Distance change per `css_grab_in`/`css_grab_out` |
| `Button Step` | `10.0` | Distance change per tick when holding LMB/RMB |

## Grabbable Entity Types

- Players
- `prop_physics`, `prop_physics_override`, `prop_physics_multiplayer`
- `chicken`
- Weapons (`weapon_*`)
- Projectiles (`*_projectile`)
- Items (`item_*`)

## Project Structure

```
CS2_Admin_Grab/
  CS2_Admin_Grab.cs           Entry point (Composition Root)
  Config/
    AdminGrabConfig.cs         Configuration model
  Models/
    GrabTarget.cs              Polymorphic target (Player | Entity)
    GrabSession.cs             Session state
    TraceResult.cs             Ray trace result
  Services/
    IRayTraceService.cs        Ray trace interface
    OBBRayTraceService.cs      OBB ray casting implementation
    IGrabPhysics.cs            Physics interface
    SpringDamperPhysics.cs     Spring-damper implementation
    IGrabVisualizer.cs         Visualization interface
    BeamVisualizer.cs          Beam/wireframe visualization
  Core/
    GrabManager.cs             Orchestrator
    GrabSessionManager.cs      Session state management
  Utils/
    VectorMath.cs              Vector/angle math utilities
```

## Author

Rexus Ohm
