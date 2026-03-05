The player character will be composed of four independent parts, with sprites loosely connected to nodes similar to the construction of the centipede. 

The player's largest section will be his torso and head. They will be the same sprite and anchored to the same point, so the torso and head are only visually distinct but otherwise the same object.

The second part is the player's hands which will hold a gun, following the mouse so the player may aim. The hands are able to rotate along a 360-degree circle, always pointing at the mouse. This circle will center on the torso sprite, so that the hands match the torso's movement when it wiggles off of its anchor node. The hands will be locked on that circular track. The track will always maintain the same position in relation to the torso piece.

Finally, the player will have two feet that are able to wiggle away from their respective nodes, similar to the centipede balls. Like the centipede balls, the collider and rigidbody will be placed on the visual feet. They will be circular. One situation to consider when programming the visual component's connection to the skeleton node is the player landing on the ground. The nodes should not interact with the ground, allowing them to push past it, but the feet should be stopped. The elastic force cannot simply apply to the feet, though, because it is impossible for them to move into the ground. The nodes should be pulled back up to the visual feet. Unless stopped by a physical obstacle like this, the visual component should move to the node as with the caterpillar.


The underlying Skeleton will have three SkeletonNodes: One for the torso, and one for each of the feet. The torso's should be the SkeletonRoot. The torso's SkeletonNode should be held at a constant vertical distance from the lowest foot SkeletonNode. For now, the foot SkeletonNodes should be locked into an isoceles triange with the torso, but I will eventually add movement that will change this.

Please develop an implementation plan for the player, following these steps:
- Review SkeletonNode.cs and SkeletonRoot.cs, then create new versions for the player. The torso's node should be the SkeletonRoot as well as the parent of each of the feet's SkeletonNode. Movement should currently have the feet match the velocity of the torso.
- Create a list of GameObjects necessary for the visual and physical components of the player.
- Determine if NodeWiggle.cs works for the movement as described for the feet and torso. If not, create a new version for whichever components require it.
- Determine the best approach for handling the arm component. Create a script that allows the behavior as described.
- Finally, create a script similar to CentipedeAssembler.cs that will instantiate all necessary components of the player character.

Once all scripts are made, re-read the PlayerDescription and determine if all design requirements are met. If not, make tweaks.
Finally, list any non-script components that will need to be configured and the steps to do so.