using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace iviewer
{
    internal class VideoQueueItem : BusinessObject
    {
        public static VideoQueueItem New()
        {
            var item = new VideoQueueItem();
            item.SetDefaultPropertyValues();
            return item;
        }

        public static VideoQueueItem New(Guid clipGenerationStatePK)
        {
            var item = new VideoQueueItem();
            item.SetDefaultPropertyValues();
            item.ClipGenerationStatePK = clipGenerationStatePK;

            return item;
        }

        public VideoQueueItem LoadFromSql(string sql)
        {
            var item = new VideoQueueItem();
            item.SetDefaultPropertyValues();
            base.LoadFromSql(sql);

            return item;
        }

        public static VideoQueueItem Load(Guid pk)
        {
            var item = New();
            item.LoadFromSql($"SELECT * FROM VideoGenerationQueue WHERE PK = {DB.FormatDBValue(pk)}");
            return item;
        }

        public override string TableName => "VideoGenerationQueue";

        #region Persistent Properties

        public Guid ClipGenerationStatePK 
        {
            get => clipGenerationStatePK;
            set
            {
                if (clipGenerationStatePK != value)
                {
                    clipGenerationStatePK = value;  
                    SetHasChanges();
                }
            }
        }
        Guid clipGenerationStatePK;

        public int OrderIndex
        {
            get => orderIndex;
            set
            {
                if (orderIndex != value)
                {
                    orderIndex = value;
                    SetHasChanges();
                }
            }
        }
        int orderIndex;

        public string Status 
        {
            get => status;
            set
            {
                if (status != value)
                {
                    status = value;
                    SetHasChanges();
                }
            }
        }
        string status = "";

        #endregion

        #region Loading / Saving

        protected override void FillFromDictionary(Dictionary<string, object> dic)
        {
            base.FillFromDictionary(dic);
            ClipGenerationStatePK = Guid.Parse(dic["ClipGenerationStatePK"].ToString());
            OrderIndex = int.Parse(dic["OrderIndex"].ToString());
            Status = dic["Status"].ToString();
        }

        protected override Dictionary<string, object> FillDictionary()
        {
            var dic = base.FillDictionary();
            dic["ClipGenerationStatePK"] = ClipGenerationStatePK;
            dic["OrderIndex"] = OrderIndex;
            dic["Status"] = Status;

            return dic;
        }

        protected override void SetDefaultPropertyValues()
        {
            base.SetDefaultPropertyValues();
            Status = "Queued";
        }

        #endregion
    }
}
