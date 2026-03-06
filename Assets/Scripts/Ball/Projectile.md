The next step is to implement firing functionality for the player's gun. 

The player will fire Balls out of their arm (whose two sprites are a combination of the arm and the player's gun). The arm object should have a firingPoint, which is the coordinate from which the Ball objects will launch. 

When fired, the Balls should initially be scaled down to the size of the firingPoint (allowing the illusion that they are emerging from the firingPoint). Create a variable attached to the arm to set this initialScale, which will be shared across all fired Balls.

The Balls should then quickly grow to their actual scale. Also include a variable that controls how quickly this growth will happen.

The firing mechanism should accept a diameter and type that will be used as initial values for the created Ball. The Ball will never be in Centipede Mode when firing. 
Eventually, there will be a queue of Balls that will feed to this firing mechanism. For now, allow unlimited shots of Balls at random sizes within a range. This will be temporary functionality, so ensure it is implemented in an easily removeable way.

The projectiles should fire away from the firingPoint at the same angle as the arm is to the torso at the moment of firing. Include a variable on the arm that can configure the firingSpeed. This should be changeable on the Character Config.

The gun will fire when the player left clicks. Create a time variable that controls how often the player may shoot. 