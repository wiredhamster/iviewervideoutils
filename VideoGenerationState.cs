using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iviewer
{
    internal class VideoGenerationState : BusinessObject
    {
        public static VideoGenerationState New()
        {
            var state = new VideoGenerationState();
            state.SetDefaultPropertyValues();

            return state;
        }

        public static VideoGenerationState Load(string sql)
        {
            var state = new VideoGenerationState();
            if (state.LoadFromSql(sql))
            {
                return state;
            }

            return null;
        }

        public override string TableName => "VideoGenerationStates";

        #region Persistent Properties

        public string ImagePath { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public string PreviewPath { get; private set; }

        public string TempFiles { get; private set; }

        public string Status { get; private set; }

        #endregion

        #region Loading / Saving

        protected override void FillFromDictionary(Dictionary<string, object> dic)
        {
            base.FillFromDictionary(dic);
            ImagePath = dic["ImagePath"].ToString();
            Width = int.Parse(dic["Width"].ToString());
            Height = int.Parse(dic["Height"].ToString());
            PreviewPath = dic["PreviewPath"].ToString();
            TempFiles = dic["TempFiles"].ToString();
            Status = dic["Status"].ToString();
        }

        protected override Dictionary<string, object> FillDictionary()
        {
            var dic = base.FillDictionary();
            dic["ImagePath"] = ImagePath;
            dic["Width"] = Width;
            dic["Height"] = Height;
            dic["PreviewPath"] = PreviewPath;
            dic["TempFiles"] = TempFiles;
            dic["Status"] = Status;

            return dic;
        }

        #endregion
    }
}
