namespace CoverShooter
{
    /// <summary>
    /// Stores object layers.
    /// </summary>
    public static class Layers
    {
        /// <summary>
        /// Layer for the cover markers.
        /// </summary>
        public static int Cover = 8;

        /// <summary>
        /// Layer for objects to be hidden when using scope (usually the player renderer).
        /// </summary>
        public static int Scope = 9;

        /// <summary>
        /// Layer for all human characters.
        /// </summary>
        public static int Character = 10;

        /// <summary>
        /// Layer for zones.
        /// </summary>
        public static int Zones = 11;
    }
}
