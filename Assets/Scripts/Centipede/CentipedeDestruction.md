The next step is to implement destructibility for the centipedes, which the player accomplishes by detaching the centipede's Balls.

A Ball should detach from the centipede if it reaches a certain distance away from its associated SkeletonNode. This distance should be set by a detachDistance variable in the CentipedeConfig. 

When a Ball reaches or exceeds its detachDistance, it should trigger a function 