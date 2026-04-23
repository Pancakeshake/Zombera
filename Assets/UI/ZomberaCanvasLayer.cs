namespace Zombera.UI
{
    /// <summary>
    /// Shared sorting-order constants for all runtime canvases in Zombera.
    ///
    /// Layout (lowest → highest):
    ///   Hud      (10)  — WorldHUD bottom bar, time controls, damage popups
    ///   Screens  (50)  — Squad management, Inventory, Crafting, Map, Missions
    ///   Overlays (100) — Alerts, notifications (always above screens)
    ///   Loading  (32767) — Loading screen (always on top of everything)
    /// </summary>
    public static class ZomberaCanvasLayer
    {
        public const int Hud      = 10;
        public const int Screens  = 50;
        public const int Overlays = 100;
        public const int Loading  = short.MaxValue;
    }
}
