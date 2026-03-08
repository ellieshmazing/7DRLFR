# Ball Type Creation Guide

This guide explains how to design and implement a new Ball type in BallWorld. The Ball system is built for flexibility: you can create Balls that explode on impact, trail fire, home in on targets, or behave like sticky glue — all by composing a small number of building blocks without touching the core `Ball` class.

---

## How Balls Work

Every Ball in the game is a single circular GameObject with a sprite, physics body, and collider. Two things determine what makes a particular Ball type unique:

1. **A `BallDefinition` asset** — a data file that lives in your project and tells the engine what sprite to use, how heavy the ball is, and which behavior scripts are attached to it.
2. **Optional behavior scripts** — one for custom movement, one for effects at launch and on collision. These are also assets, not scene objects.

The `Ball` component reads from the `BallDefinition` at runtime and calls into the behavior scripts at the right moments.

---

## The Three Hooks

Every Ball type can respond to three moments in a ball's life:

| Hook | When It Fires | Available To |
|---|---|---|
| `OnLaunch` | The instant the ball starts moving freely (fired as a projectile, or ejected from a centipede) | Both `BallEffect` and `BallMovementOverride` |
| `OnFixedUpdate` | Every physics step while the ball is in free flight | `BallMovementOverride` only |
| `OnCollision` | When the ball physically touches anything | `BallEffect` only |

> **Centipede mode note:** When a ball is part of a living centipede, it uses its own spring-chase logic and `OnFixedUpdate` is not called. However, `OnCollision` and `OnLaunch` *do* still apply even to centipede segments. A fire ball will react to collisions regardless of whether it's flying free or riding a centipede chain.

---

## Step 1 — Register the Type Name

Open `BallDefinition.cs` and add your type to the `BallType` enum:

```
BallType {
    Standard,
    Fire,       // ← add your type here
}
```

This is just a label used to identify which definition to load. It doesn't do anything on its own.

---

## Step 2 — Create a BallDefinition Asset

In the Unity Project window:

**Right-click → Create → Ball → Ball Definition**

Name it something clear, like `FireBallDefinition`. This asset has four properties you set in the Inspector:

| Field | What It Does |
|---|---|
| `type` | Which entry in the `BallType` enum this represents |
| `sprite` | The ball's visual. Should be a circle sprite authored so that 1 world unit = the sprite's full width at scale 1. |
| `baseMass` | The weight of a ball with diameter 1. Balls scale their mass by `diameter²`, so bigger balls are proportionally heavier. |
| `movementOverride` | (Optional) An asset that controls how the ball moves. Leave empty for standard physics. |
| `effect` | (Optional) An asset that defines what happens at launch and on collision. Leave empty for no special effects. |

If you only need a visual change, you're done after setting `sprite`. Everything else is optional.

---

## Step 3 — Write a BallEffect (for launch and collision behavior)

Create a new C# script that extends `BallEffect`. This gives you two entry points:

### `OnLaunch(ball, rb, launchVelocity)`

Called once when the ball first enters free flight. Use this to:
- Spawn a particle system and parent it to the ball
- Play a launch sound
- Start a timer (e.g., a fuse before an explosion)
- Store the launch direction for later use

You have access to the ball's `Rigidbody2D` (`rb`) to read its velocity or mass, but you don't typically need to change physics here — the velocity is already set for you.

### `OnCollision(ball, collision)`

Called when the ball physically contacts anything — another ball, a wall, an enemy. Use this to:
- Trigger an explosion (spawn an explosion prefab, apply force to nearby objects)
- Freeze the ball in place (sticky behavior: set `ball.Rigidbody.bodyType = Kinematic`, zero out velocity)
- Apply a status effect to whatever was hit
- Spawn child balls (splitting)
- Destroy the ball

The `collision` parameter tells you what was hit (the other object, contact point, contact normal) so you can react directionally.

> **OnCollision fires in both centipede and free mode.** If a centipede segment with a fire effect grazes a wall, `OnCollision` runs. Design accordingly.

### Creating the Asset

After writing the script, create an instance of it in your project:

**Right-click → Create → [your script's menu path]**

You define this menu path in your script using the `[CreateAssetMenu]` attribute (at the top of the class, before the class declaration). Then drag the created asset into the `effect` slot on your `BallDefinition`.

---

## Step 4 — Write a BallMovementOverride (for custom physics)

Create a new C# script that extends `BallMovementOverride`. This gives you two entry points:

### `OnLaunch(ball, rb, launchVelocity)`

Same timing as `BallEffect.OnLaunch`. Use this to initialize movement state:
- Lock onto a target (for homing)
- Save the initial heading (for a boomerang that needs to return)
- Set up a timer for phased movement (e.g., "accelerate for 0.5s, then curve")

### `OnFixedUpdate(ball, rb, dt)`

Called every physics step while the ball is in free flight. `dt` is the elapsed time since the last physics step. Use this to:
- Apply a steering force toward a target (homing)
- Curve the trajectory
- Add drag or lift in specific directions

You can directly set `rb.linearVelocity` to override the current motion entirely, or use `rb.AddForce` to nudge it. For full positional control (e.g., a scripted path), set `rb.bodyType = Kinematic` and drive `rb.position` directly with `rb.MovePosition`.

> Movement overrides only run in **free mode**. Centipede segments use their own spring logic and `OnFixedUpdate` is never called for them.

---

## Composing Effects and Movement Together

A single `BallDefinition` can have both an effect and a movement override at the same time. They are independent and both get `OnLaunch` called when the ball fires.

**Example — Homing Fire Ball:**
- `movementOverride`: a homing script that acquires the nearest enemy at launch and steers toward it each step
- `effect`: a fire script that spawns a flame particle trail at launch, and triggers an explosion on collision

The two scripts don't know about each other — they both just respond to the ball's events.

---

## Practical Examples

### Explosive Ball
- No movement override (flies straight under normal physics)
- `BallEffect.OnLaunch`: arm a state flag
- `BallEffect.OnCollision`: if armed, spawn explosion prefab at contact point, destroy self

### Sticky Ball
- No movement override
- `BallEffect.OnCollision`: set `ball.Rigidbody.bodyType = Kinematic`, zero velocity, parent ball to the hit surface

### Homing Ball
- `BallMovementOverride.OnLaunch`: find nearest enemy, store reference
- `BallMovementOverride.OnFixedUpdate`: compute direction to target, apply steering force; if target is gone, fly straight
- No effect (or combine with explosive for homing + explode)

### Bouncy Ball
- No movement override (physics handles bouncing — just set a `PhysicsMaterial2D` with high bounciness on the ball's collider)
- No effect needed, unless you want visual or audio feedback on each bounce (use `OnCollision`)

### Split Ball
- `BallEffect.OnCollision`: spawn two or three smaller balls at the contact point with velocities fanned outward, then destroy self

---

## Quick Checklist

When adding a new Ball type:

- [ ] Add entry to `BallType` enum in `BallDefinition.cs`
- [ ] Create a `BallDefinition` asset (Assets → Create → Ball → Ball Definition)
- [ ] Assign `type`, `sprite`, and `baseMass` in the Inspector
- [ ] If you need collision or launch effects: write a `BallEffect` subclass, create its asset, assign to `effect`
- [ ] If you need custom movement: write a `BallMovementOverride` subclass, create its asset, assign to `movementOverride`
- [ ] Assign the `BallDefinition` asset wherever balls of this type are spawned (e.g. `ProjectileGun.projectileDef`, `CentipedeConfig.ballDefinition`)

---

## What the Ball Exposes to Your Scripts

When your effect or override receives a `Ball` parameter, you have access to:

| Property / Method | What It Is |
|---|---|
| `ball.Rigidbody` | The `Rigidbody2D` — read velocity, change body type, apply force |
| `ball.SpringVelocity` | The internal spring velocity (only meaningful for centipede segments) |
| `ball.gameObject` | The ball's GameObject — attach particle systems, get other components |
| `ball.transform` | Position, rotation, scale |
| `ball.SetTint(color)` | Change the sprite tint at runtime |
| `ball.Launch(velocity)` | Fire the ball and trigger `OnLaunch` (used by the gun, not typically called from within effects) |

You should not call `ball.Init()` or `ball.SetCentipedeMode()` from within effects — those are lifecycle methods managed by the assemblers.
