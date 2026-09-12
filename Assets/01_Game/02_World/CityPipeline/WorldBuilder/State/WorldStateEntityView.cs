using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public abstract class WorldStateEntityView : MonoBehaviour
    {
        [SerializeField] private WorldEntityKind _worldEntityKind = WorldEntityKind.None;
        [SerializeField] private string _worldEntityId = string.Empty;

        public string WorldEntityIdHex => _worldEntityId;
        public WorldEntityKind EntityKind => _worldEntityKind;
        public bool HasWorldEntityId =>
            WorldStableIdFactory.TryCreateFromHexString(_worldEntityKind, _worldEntityId, out _);

        public WorldEntityId WorldEntityId
        {
            get
            {
                return HasWorldEntityId
                    ? WorldStableIdFactory.CreateFromHexString(_worldEntityKind, _worldEntityId)
                    : new WorldEntityId(WorldEntityKind.None, string.Empty);
            }
            set
            {
                _worldEntityKind = value.kind;
                _worldEntityId = value.value ?? string.Empty;
            }
        }

        public virtual void Bind(WorldEntityId id)
        {
            WorldEntityId = id;
        }

        public virtual void ClearBinding()
        {
            _worldEntityKind = WorldEntityKind.None;
            _worldEntityId = string.Empty;
        }
    }
}
