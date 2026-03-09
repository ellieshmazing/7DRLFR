"""
BallWorld -- Step Tuning Calculator
Derives step constraint values so feet never limit movement speed.

Physics terminal velocity: v = moveForce / (groundDamping * torsoMass)
This is the speed the torso reaches under sustained input, ignoring the
hard maxSpeed clamp. Use it as the tuning target so feet keep up with
what the physics actually allows.

Edit the INPUTS section and run: python step_tuning.py
"""

# -----------------------------------------------------------------------------
# INPUTS
# -----------------------------------------------------------------------------

# Physics (from PlayerConfig)
moveForce       = 17.0    # wu force applied to torso
groundDamping   = 5.0     # torso linearDamping while grounded
baseTorsoMass   = 1.0     # torso RB mass

# Pixel space (from PlayerConfig)
playerScale           = 1.0    # world units per 16px sprite
strideTriggerDistance = 6.442921    # px -- how far foot lags before stepping (s)
strideProjectionTime  = 0.27574   # s  -- how far ahead step target is projected (p)

# Step shape (from PlayerConfig) -- used for validation and k calculation
baseStepDuration = 1   # s -- step duration at zero speed
minStepDuration  = .27574   # s -- floor on step duration at sprint
stepSpeedScale   = 1   # -- speed-to-duration compression factor (k)

# -----------------------------------------------------------------------------
# DERIVED
# -----------------------------------------------------------------------------

ptw = playerScale / 16.0                           # pixelToWorld
s   = strideTriggerDistance * ptw                  # stride trigger (wu)
p   = strideProjectionTime                         # projection time (s)
v   = moveForce / (groundDamping * baseTorsoMass)  # physics terminal velocity (wu/s)

# -----------------------------------------------------------------------------
# CONSTRAINTS
# -----------------------------------------------------------------------------

# 1. minStepDuration ceiling -- feet must be able to cycle at v
T_min_max = s / v

# 2. stepSpeedScale minimum -- duration must actually reach T_min at v
#    From: baseStepDuration / (1 + v * k) = T_min  =>  k = (base/T_min - 1) / v
k_min = (baseStepDuration / T_min_max - 1) / v

# 3. Balanced strideProjectionTime -- foot center stays on torso at v (delta = 0)
p_balanced = s / v

# 4. Foot centre offset at current p
#    During walking: back foot ~(torsoX - s), front foot ~(torsoX + p*v)
#    Centre = torsoX + (p*v - s) / 2
delta    = (p * v - s) / 2   # wu  (+) leads torso, (-) lags into leash territory
delta_px = delta / ptw

# 5. leashSoftRadius minimum to absorb offset (px)
leash_min_px = abs(delta_px)

# Step duration at v with current settings
T_at_v = max(minStepDuration, baseStepDuration / (1.0 + v * stepSpeedScale))

# -----------------------------------------------------------------------------
# OUTPUT
# -----------------------------------------------------------------------------

W = 48

def ok(condition): return "OK" if condition else "!!"
def hr(): print("-" * W)

hr()
print("  DERIVED BASE VALUES")
hr()
print(f"  pixelToWorld            {ptw:.5f} wu/px")
print(f"  s  (stride trigger)     {s:.5f} wu  ({strideTriggerDistance} px)")
print(f"  v  (physics top speed)  {v:.3f} wu/s")
print(f"     = moveForce({moveForce}) / (groundDamping({groundDamping}) * mass({baseTorsoMass}))")

hr()
print("  CONSTRAINT 1 -- minStepDuration <= s / v")
hr()
print(f"  Required:  <= {T_min_max:.5f} s")
print(f"  Current:      {minStepDuration:.5f} s  [{ok(minStepDuration <= T_min_max)}]")
if minStepDuration > T_min_max:
    print(f"  !! Feet cannot cycle fast enough at v = {v:.2f}.")
    print(f"     Reduce minStepDuration to <= {T_min_max:.4f} s.")

hr()
print("  CONSTRAINT 2 -- stepSpeedScale >= (baseStepDuration / T_min - 1) / v")
hr()
print(f"  Required:  >= {k_min:.5f}")
print(f"  Current:      {stepSpeedScale:.5f}  [{ok(stepSpeedScale >= k_min)}]")
print(f"  (ensures duration reaches T_min exactly at v)")
if stepSpeedScale < k_min:
    print(f"  !! Duration never reaches minStepDuration at v = {v:.2f}.")
    print(f"     Raise stepSpeedScale to >= {k_min:.4f}.")
print(f"  Step duration at v:     {T_at_v:.5f} s  (target: {T_min_max:.5f} s)")

hr()
print("  CONSTRAINT 3 -- strideProjectionTime for balanced foot centre")
hr()
print(f"  Balanced p (delta=0):   {p_balanced:.5f} s")
print(f"  Current p:              {p:.5f} s")
sign = "leads" if delta >= 0 else "LAGS"
print(f"  Foot centre offset:     {delta_px:+.3f} px at v = {v:.2f}  (feet {sign} torso)")
if delta < 0:
    print(f"  !! Feet lag torso -- leash may activate during normal running.")

hr()
print("  CONSTRAINT 4 -- leashSoftRadius > |delta| / pixelToWorld")
hr()
print(f"  Minimum leashSoftRadius:  {leash_min_px:.3f} px")
print(f"  (hard radius should be larger still)")

hr()
print("  RECOMMENDED VALUES")
hr()
print(f"  minStepDuration      {T_min_max:.4f} s  (ceiling)")
print(f"  stepSpeedScale       {k_min:.4f}    (min to hit ceiling at v)")
print(f"  strideProjectionTime {p_balanced:.4f} s  (balanced centre)")
print(f"  leashSoftRadius      > {leash_min_px:.2f} px")
hr()
