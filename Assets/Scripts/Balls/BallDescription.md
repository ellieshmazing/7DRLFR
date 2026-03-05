This folder will contain all scripts for the Ball class, which will serve as both the component parts of entities like the Centipede as well as the ammo for the player's projectiles. Balls will each be composed of a single GameObject that contains the sprite, a rigidbody, and a circular collider that are equivalently sized. 

The Ball class will take three arguments when being instantiated:
- The Ball's scale, which should apply in the same manner as on the player sprites (please make a note that this logic should be used across the project).
- The Ball's type, a value in an enumerated list described below.
- And a Boolean indicating whether the Ball has Centipede Mode enabled.

Balls should have two types of behavior, depending on whether they are in Centipede Mode. Centipede Mode applies for Balls that currently comprise a Centipede. In this state, they should behave exactly as the current visual component of the Centipede's segments, being pulled toward their linked SkeletonNode. When in this state, they should not be able to collide with other Balls in Centipede Mode. I was thinking of handling this by creating a Centipede layer that they are moved to when in this state, with Centipede x Centipede collision disabled. Please determine whether this is the best approach, otherwise decide on another path.

Outside of Centipede Mode, Balls should have the default behavior of a physical sphere object. They should be able to collide with all types of Balls, even if the other balls are in Centipede Mode.

The Ball's type will determine three factors:
- The Ball's sprite
- The Ball's default mass (which should be scaled according to its size when calculating movement)
- The Ball's movement equation, if overriding the default movement. Some Balls may, for example, be sticky, meaning they will require unique movement rules.
- Any unique effects (such as on collision). These will be in effect even in Centipede Mode.

I imagined placing the default behavior in one file, with a supplementary file containing the enumerated list of different types. Please determine if this is an ideal approach, otherwise alter it. The goal should be easy addition of new Ball types with a variety of movement and effects.