using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    ///     Optional callback for units that need custom damage presentation without gameplay→AI coupling.
    /// </summary>
    public interface IDamageNotifiable
    {
        void OnDamagedBy(GameObject source);
    }
}
