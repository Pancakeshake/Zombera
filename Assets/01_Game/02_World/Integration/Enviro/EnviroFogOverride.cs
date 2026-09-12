namespace Zombera.World.Enviro
{
    /// <summary>
    /// Editor/testing override for Enviro atmosphere fog.
    /// <para>
    /// The Development Hub generates the world in the open scene, and the authored
    /// <see cref="Zombera.World.CityPipeline.WorldBuilder.WorldEnvironmentProfile"/> fog density (0.034,
    /// against a 0.001–0.2 range) hides the terrain being built. The hub's "Suppress Fog While
    /// Generating" toggle raises this flag for the duration of a run.
    /// </para>
    /// <para>
    /// The Environment stages re-apply the profile mid-run, so suppressing fog once is not enough:
    /// <c>EnviroWorldEnvironmentBackend.ApplyFogSettings</c> must honour the flag on every apply or the
    /// fog comes straight back. Nothing in the runtime reads this outside an editor run; it defaults
    /// to false and is reset when the run finishes, fails, or is stopped.
    /// </para>
    /// </summary>
    public static class EnviroFogOverride
    {
        /// <summary>True while a world-build/test run wants Enviro fog suppressed.</summary>
        public static bool SuppressFog { get; set; }
    }
}
