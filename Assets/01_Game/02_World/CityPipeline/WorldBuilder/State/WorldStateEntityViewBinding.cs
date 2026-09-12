using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStateEntityViewBinding
    {
        public static WorldStateEntityView Bind(GameObject target, WorldEntityId id)
        {
            if (target == null)
                return null;

            var view = target.GetComponent<WorldStateEntityView>();
            if (view == null)
                view = target.AddComponent<WorldStateGeneratedEntityView>();

            view.Bind(id);
            return view;
        }

        public static bool TryGetBoundId(Component source, WorldEntityKind expectedKind, out WorldEntityId id)
        {
            id = default;
            if (source == null)
                return false;

            return TryGetBoundId(source.gameObject, expectedKind, out id);
        }

        public static bool TryGetBoundId(GameObject source, WorldEntityKind expectedKind, out WorldEntityId id)
        {
            id = default;
            var view = source != null ? source.GetComponent<WorldStateEntityView>() : null;
            if (view == null || !view.HasWorldEntityId)
                return false;

            id = view.WorldEntityId;
            return expectedKind == WorldEntityKind.None || id.kind == expectedKind;
        }
    }
}
