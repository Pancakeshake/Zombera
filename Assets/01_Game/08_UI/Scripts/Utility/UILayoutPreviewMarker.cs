using UnityEngine;

namespace Zombera.UI
{
    /// <summary>
    ///     Tags objects spawned only for editor scene-view layout. Stripped automatically when play mode starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UILayoutPreviewMarker : MonoBehaviour
    {
        [SerializeField] private string previewGroup = "Default";

        public string PreviewGroup => previewGroup;

        public void Configure(string group)
        {
            previewGroup = group;
        }
    }
}
