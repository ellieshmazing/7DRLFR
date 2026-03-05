This folder contains the scripts for the centipede enemy in a game called Ball World.

Every entity in Ball World is comprised of a series of magically connected balls. A centipede is a linked chain of balls, whose head drives movement with all other balls following. The head will be a SkeletonRoot object with an associated SkeletonNode. All other balls will only contain a SkeletonNode.

Currently, only the SkeletonRoot moves independently. All SkeletonNodes have an offset value that they will jump to, keeping the overall structure constant. 

I would like to update this. Instead of a strict offset, SkeletonNodes should have a distance value (defaulted to 0.3) that controls how far it can be from its parent SkeletonNode. This distance is only the magnitude and the SkeletonNodes can be at any angle relative to one another.

Additionally, SkeletonNodes should follow the path of their parent, instead of moving according to its current velocity. A SkeletonNode should occupy its parent's position after the centipede moves the distance of their offset. 