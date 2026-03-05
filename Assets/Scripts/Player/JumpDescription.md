The next step is to configure a jumping mechanic for the player character. Jump should be controlled by the Space Bar, but configurable in a PlayerConfig file.

The jump velocity will be determined according to the springy behavior of the player's feet. The further crouched down the player is (meaning the PlayerHipNode is below the lowest foot, therefore pulling the torso closer as well) indicates greater potential energy. 

The player should have a short, baseline jump velocity with the crouch force added on top. If the PlayerHipNode is level with the lowest foot, then it should jump at exactly the baseline velocity. Otherwise, additional velocity should be added based on the offset. Determine what kind of calculation would give this a realistic feel (i.e. whether the offset should have a linear or exponential effect on velocity, or whatever equation applies).

The player should only be able to jump when the lowest foot (considered the grounding foot) is in contact with the ground.

Please implement this jump. Include variables on the PlayerAssembler that can be tweaked for the baseline jump velocity as well as the factor to multiply the hip offset by for additional velocity. This should be adjustable while the game is live.