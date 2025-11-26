using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iviewer
{
    public static class EventBus
    {
        //public static event Action<Guid> ClipQueued; // Param: new Clip PK
        public static event Action<Guid, Guid> ClipDeleted; // Param: Clip PK, Video PK
        public static event Action<Guid, Guid, string> ClipStatusChanged; // Params: Clip PK, Video PK, new Status (e.g., "Generating", "Generated")
        public static event Action<Guid, string> VideoStatusChanged; // Params: Video PK, new Status
        public static event Action<Guid, HashSet<Guid>> VideoDeleted; // Param: Video PK, Clip PKs

        //public static void RaiseClipQueued(Guid clipPK)
        //{
        //    ClipQueued?.Invoke(clipPK);
        //}

        public static void RaiseClipDeleted(Guid clipPK, Guid videoPK)
        {
            ClipDeleted?.Invoke(clipPK, videoPK);
        }

        public static void RaiseClipStatusChanged(Guid clipPK, Guid videoPK, string newStatus)
        {
            ClipStatusChanged?.Invoke(clipPK, videoPK, newStatus);
        }

        public static void RaiseVideoStatusChanged(Guid videoPK, string newStatus)
        {
            VideoStatusChanged?.Invoke(videoPK, newStatus);
        }

        public static void RaiseVideoDeleted(Guid videoPK, HashSet<Guid> clipPKs)
        {
            VideoDeleted?.Invoke(videoPK, clipPKs);
        }
    }
}
