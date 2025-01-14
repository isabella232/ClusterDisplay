namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Represent something that can be launched on a launchpad.
    /// </summary>
    /// <remarks><see cref="Asset"/> for a family portrait of <see cref="Launchable"/> and its relation to an
    /// <see cref="Asset"/>.</remarks>
    public class Launchable: LaunchCatalog.LaunchableBase, IEquatable<Launchable>
    {
        /// <summary>
        /// Name of Payloads forming the list of all the files needed by this <see cref="LaunchCatalog.LaunchableBase"/>.
        /// </summary>
        public IEnumerable<Guid> Payloads { get; set; } = Enumerable.Empty<Guid>();

        /// <summary>
        /// Create a shallow copy of from.
        /// </summary>
        /// <param name="from">To copy from.</param>
        public void ShallowCopyFrom(Launchable from)
        {
            base.ShallowCopyFrom(from);
            Payloads = from.Payloads;
        }

        public bool Equals(Launchable? other)
        {
            return other != null &&
                ArePropertiesEqual(other) &&
                Payloads.SequenceEqual(other.Payloads);
        }
    }
}
