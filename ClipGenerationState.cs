using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iviewer
{
    internal class ClipGenerationState : BusinessObject
    {
        public static ClipGenerationState New()
        {
            var state = new ClipGenerationState();
            state.SetDefaultPropertyValues();

            return state;
        }

        public static ClipGenerationState Load(string sql)
        {
            var state = new ClipGenerationState();
            if (state.LoadFromSql(sql))
            {
                return state;
            }

            return null;
        }

        public override string TableName => "ClipGenerationStates";

        #region Persistent Properties

        public Guid VideoGenerationStatePK { get; set; }

        public string ImagePath { get; set; }

        public string VideoPath { get; set; }

        public string Prompt { get; set; }

        public string WorkflowPath { get; set; }

        public string Status { get; set; }

        public int OrderIndex { get; set; }

        #endregion

        #region Loading / Saving

        protected override void FillFromDictionary(Dictionary<string, object> dic)
        {
            base.FillFromDictionary(dic);
            ImagePath = dic["ImagePath"].ToString();
            VideoPath = dic["VideoPath"].ToString();
            Prompt = dic["Prompt"].ToString();
            WorkflowPath = dic["WorkflowPath"].ToString();
            Status = dic["Status"].ToString();
            OrderIndex = int.Parse(dic["OrderIndex"].ToString());
        }

        protected override Dictionary<string, object> FillDictionary()
        {
            var dic = base.FillDictionary();
            dic["ImagePath"] = ImagePath;
            dic["VideoPath"] = VideoPath;
            dic["Prompt"] = Prompt;
            dic["WorkflowPath"] = WorkflowPath;
            dic["Status"] = Status;
            dic["OrderIndex"] = OrderIndex;

            return dic;
        }

        #endregion
    }
}
