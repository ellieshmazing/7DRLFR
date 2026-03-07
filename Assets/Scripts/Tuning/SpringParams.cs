/// <summary>
/// Static helpers for second-order spring parameterization.
/// Converts perceptually meaningful (frequency, dampingRatio) pairs
/// into the raw (stiffness, damping) values consumed by spring simulations.
/// </summary>
public static class SpringParams
{
    /// <summary>k = ω² × m</summary>
    public static float ComputeStiffness(float frequency, float mass)
    {
        return frequency * frequency * mass;
    }

    /// <summary>c = 2ζωm</summary>
    public static float ComputeDamping(float frequency, float dampingRatio, float mass)
    {
        return 2f * dampingRatio * frequency * mass;
    }
}
