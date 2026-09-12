namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
        private void EnsureHudDevPlaceholderData()
        {
            if (!IsHudDevScene()) return;

            HudDevPlaceholderBootstrap.EnsureForWorldHud(this);
            RefreshDevPlaceholderTabs();
        }

        private void RefreshDevPlaceholderTabs()
        {
            _formationsTab?.RefreshFromRuntime();
            _jobsTab?.RefreshFromRuntime();
            _factionsTab?.RefreshFromRuntime();
        }
    }
}
