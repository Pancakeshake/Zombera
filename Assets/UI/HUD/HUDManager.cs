using UnityEngine;

namespace Zombera.UI
{
    /// <summary>
    /// Initializes and coordinates all HUD panel controllers.
    /// </summary>
    public sealed class HUDManager : MonoBehaviour
    {
        [Header("HUD Root")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private RectTransform hudRoot;

        [Header("Panel Prefabs")]
        [SerializeField] private GameObject squadPanelPrefab;
        [SerializeField] private GameObject commandPanelPrefab;
        [SerializeField] private GameObject minimapPrefab;
        [SerializeField] private GameObject playerStatusPrefab;
        [SerializeField] private GameObject hotbarPrefab;
        [SerializeField] private GameObject alertPanelPrefab;

        [Header("Panel Instances")]
        [SerializeField] private SquadPanelController squadPanel;
        [SerializeField] private CommandPanelController commandPanel;
        [SerializeField] private MinimapController minimap;
        [SerializeField] private PlayerStatusController playerStatus;
        [SerializeField] private HotbarController hotbar;
        [SerializeField] private AlertController alertPanel;

        public bool IsInitialized { get; private set; }

        public SquadPanelController SquadPanel => squadPanel;
        public CommandPanelController CommandPanel => commandPanel;
        public MinimapController Minimap => minimap;
        public PlayerStatusController PlayerStatus => playerStatus;
        public HotbarController Hotbar => hotbar;
        public AlertController AlertPanel => alertPanel;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            if (hudRoot == null)
            {
                if (hudCanvas != null)
                {
                    hudRoot = hudCanvas.transform as RectTransform;
                }
                else
                {
                    hudRoot = transform as RectTransform;
                }
            }

            squadPanel = EnsurePanelInstance(squadPanelPrefab, squadPanel, "SquadPanel");
            commandPanel = EnsurePanelInstance(commandPanelPrefab, commandPanel, "CommandPanel");
            minimap = EnsurePanelInstance(minimapPrefab, minimap, "Minimap");
            playerStatus = EnsurePanelInstance(playerStatusPrefab, playerStatus, "PlayerStatus");
            hotbar = EnsurePanelInstance(hotbarPrefab, hotbar, "Hotbar");
            alertPanel = EnsurePanelInstance(alertPanelPrefab, alertPanel, "AlertPanel");

            squadPanel?.Initialize(this);
            commandPanel?.Initialize(this);
            minimap?.Initialize(this);
            playerStatus?.Initialize(this);
            hotbar?.Initialize(this);
            alertPanel?.Initialize(this);

            IsInitialized = true;

            // TODO: Connect gameplay event subscriptions in a dedicated UI binding layer.
        }

        public void SetVisible(bool visible)
        {
            if (hudCanvas != null)
            {
                hudCanvas.enabled = visible;
            }

            squadPanel?.SetVisible(visible);
            commandPanel?.SetVisible(visible);
            minimap?.SetVisible(visible);
            playerStatus?.SetVisible(visible);
            hotbar?.SetVisible(visible);
            alertPanel?.SetVisible(visible);
        }

        public void ShowAlert(AlertViewData alertData)
        {
            alertPanel?.ShowAlert(alertData);
        }

        public void ClearAlert()
        {
            alertPanel?.ClearAlert();
        }

        private T EnsurePanelInstance<T>(GameObject prefab, T existing, string fallbackName) where T : MonoBehaviour
        {
            if (existing != null)
            {
                return existing;
            }

            GameObject panelObject;

            if (prefab != null)
            {
                panelObject = Instantiate(prefab, hudRoot);
            }
            else
            {
                panelObject = new GameObject(fallbackName, typeof(RectTransform));
                panelObject.transform.SetParent(hudRoot, false);
            }

            T panelController = panelObject.GetComponent<T>();

            if (panelController == null)
            {
                panelController = panelObject.AddComponent<T>();
            }

            return panelController;
        }
    }
}