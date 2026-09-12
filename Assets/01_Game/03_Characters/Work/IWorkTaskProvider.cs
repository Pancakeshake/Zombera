using System.Collections.Generic;

namespace Zombera.Characters.Work
{
    public interface IWorkTaskProvider
    {
        void CollectAvailableTasks(List<WorkTaskDescriptor> buffer);
    }
}
