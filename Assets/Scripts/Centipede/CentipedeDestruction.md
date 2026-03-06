The next step is to implement destructibility for the centipedes, which the player accomplishes by detaching the centipede's Balls. Centipedes must be at least 2 Balls long. If a Ball detaches in a way that multiple chains meet this criteria (such as if the middle Ball in a centipede of length 5 detaches), then each chain becomes a separate centipede.

A Ball should detach from the centipede if it reaches a certain distance away from its associated SkeletonNode. This distance should be set by a detachDistance variable in the CentipedeConfig.

When a Ball reaches or exceeds its detachDistance, it should trigger a function that updates all SkeletonNodes and Balls associated with the Centipede with the following process:
- First, mark the triggering Ball as "detached"
- Then, check whether any of the Balls has sufficient force already applied (such as if multiple Balls were hit by an explosion) to pass its own detachDistance. Accomplish this by calculating the velocity needed to detach a Ball of that size, with its NodeWiggle variables, and its current displacement. If its velocity exceeds that number, mark them as detached as well. This is so that their detachments can be handled with a single event, instead of each detachment triggering its own call of this function.
- Iterate through the remaining Balls to determine their outcome, with the following rules:
- Start from the SkeletonRoot. Check whether its immediate child node is detached. If so, it also becomes an independent Ball object. Otherwise, it remains the SkeletonRoot of the Centipede.
- For each subsequent attached node, if its parent is still part of a Centipede, it should remain connected to its parent.
- Whenever you reach a detached Ball, remove it from the hierarchy of its parent SkeletonNode.
- For the first attached Ball after a detachment, run the same check as for the SkeletonRoot: If it does not have a still-attached child, it becomes an independent Ball.
- Otherwise, it will become a new centipede. Check how many of its child nodes are still attached without a break in the chain. These Balls will become a new centipede in the reverse order as before. The last attached child Ball will become the new SkeletonRoot. This will require reversing the hierarchy of children, but do not do that until you are certain that all Balls can be resolved.
- Any further chains of 2+ Balls should be handled in the same fashion.
- Once the outcome for all Balls is determined, then resolve their outcomes. All detached Balls or Balls without any attached neighbors will become independent Balls. All others will form centipedes. Create new centipede object wrappers for these.
- All independent Balls should have Centipede Mode disabled, and their associated SkeletonNodes should be deleted.

Read through this process, then determine what object is best to carry this function. Make any changes necessary for this to perform as described. Write the function, then verify that all Balls will be resolved correctly. Think through how the function would handle this situation as a test: A 9-length centipede has its 4th and 7th Balls detach. Make any tweaks necessary.