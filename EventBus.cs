using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iviewer
{
    public static class EventBus
    {
        public static event Action<Guid> ItemQueued; // Param: new Clip PK
        public static event Action<Guid, string> ClipStatusChanged; // Params: Clip PK, new Status (e.g., "Generating", "Generated")

        public static void RaiseItemQueued(Guid clipPK)
        {
            ItemQueued?.Invoke(clipPK);
        }

        public static void RaiseClipStatusChanged(Guid clipPK, string newStatus)
        {
            ClipStatusChanged?.Invoke(clipPK, newStatus);
        }
    }
}
