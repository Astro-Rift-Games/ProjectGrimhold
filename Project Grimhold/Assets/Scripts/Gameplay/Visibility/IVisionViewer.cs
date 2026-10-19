namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// The single local producer of vision queries registered with <see cref="EntityVisibilitySystem"/>.
    /// </summary>
    public interface IVisionViewer
    {
        /// <summary>
        /// Builds this frame's vision query. Returns false when the viewer cannot see
        /// (missing dependencies, not spawned), which the system treats as "no viewer".
        /// </summary>
        bool TryBuildQuery(out VisionQuery query);
    }
}
