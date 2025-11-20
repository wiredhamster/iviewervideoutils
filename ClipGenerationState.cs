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
        public ClipGenerationState()
        {
            debug = new StringBuilder();
            debug.AppendLine(Environment.StackTrace);
        }

        public StringBuilder debug;

        public static ClipGenerationState New()
        {
            var state = new ClipGenerationState();
            state.SetDefaultPropertyValues();

            return state;
        }

        public static ClipGenerationState Load(string sql)
        {
            var state = new ClipGenerationState();
            state.SetDefaultPropertyValues();
            if (state.LoadFromSql(sql))
            {
                return state;
            }

            return null;
        }

        public static ClipGenerationState Load(Guid pk)
        {
            return Load($"SELECT * FROM ClipGenerationStates WHERE PK = {DB.FormatDBValue(pk)}");
        }

        public override string TableName => "ClipGenerationStates";

        #region Persistent Properties

        public Guid VideoGenerationStatePK 
        {
            get => videoGenerationStatePK;
            set
            {
                if (videoGenerationStatePK != value)
                {
                    videoGenerationStatePK = value;
                    SetHasChanges();
                }
            }
        }
        Guid videoGenerationStatePK;

        public string ImagePath 
        {
            get => imagePath;
            set
            {
                if (imagePath != value)
                {
                    imagePath = value;
                    SetHasChanges();
                }
            }
        }
        string imagePath = "";

        public string VideoPath 
        {
            get => videoPath;
            set
            {
                if (videoPath != value)
                {
                    videoPath = value;
                    SetHasChanges();
                }
            }
        }
        string videoPath = "";

        public string Prompt 
        {
            get => prompt;
            set
            {
                if (prompt != value)
                {
                    prompt = value;
                    SetHasChanges();
                }
            }
        }
        string prompt;

        public string WorkflowPath 
        {
            get => workflowPath;
            set
            {
                if (workflowPath != value)
                {
                    workflowPath = value;
                    SetHasChanges();
                }
            }
        }
        string workflowPath = "";

        public string WorkflowJson 
        {
            get => workflowJson;
            set
            {
                if (workflowJson != value)
                {
                    workflowJson = value;
                    SetHasChanges();
                }
            }
        }
        string workflowJson = "";

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

        public string TransitionType
        {
            get => transitionType;
            set
            {
                if (transitionType != value)
                {
                    transitionType = value;
                    SetHasChanges();
                }
            }
        }
        string transitionType;

        public double TransitionDuration 
        {
            get => transitionDuration;
            set
            {
                if (transitionDuration != value)
                {
                    transitionDuration = value;
                    SetHasChanges();
                }
            }
        }
        double transitionDuration;

        public int TransitionDropLastFrames 
        {
            get => transitionDropLastFrames;
            set
            {
                if (transitionDropLastFrames != value)
                {
                    transitionDropLastFrames = value;
                    SetHasChanges();
                }
            }
        }
        int transitionDropLastFrames;

        public int TransitionAddFrames 
        {
            get => transitionAddFrames;
            set
            {
                if (transitionAddFrames != value)
                {
                    transitionAddFrames = value;
                    SetHasChanges();
                }
            }
        }
        public int transitionAddFrames;

		public int TransitionDropFirstFrames
		{
			get => transitionDropFirstFrames;
			set
			{
				if (transitionDropFirstFrames != value)
				{
					transitionDropFirstFrames = value;
					SetHasChanges();
				}
			}
		}
		int transitionDropFirstFrames;

		public double ClipSpeed 
        {
            get => clipSpeed;
            set
            {
                if (clipSpeed != value)
                {
                    clipSpeed = value;
                    SetHasChanges();
                }
            }
        }
        double clipSpeed;

        #endregion

        #region Non-persistent properties

        public string TempDir
        {
            get
            {
                if (tempDir == null)
                {
                    tempDir = Path.Combine(VideoGenerationConfig.TempFileDir, VideoGenerationStatePK.ToString());
                    Directory.CreateDirectory(tempDir);
                }

                return tempDir;
            }
        }
        string tempDir;

        public string WorkingDir
        {
            get
            {
                if (workingDir == null)
                {
                    workingDir = Path.Combine(TempDir, "working");
                    Directory.CreateDirectory(workingDir);
                }

                return workingDir;
            }
        }
        string workingDir;

		#endregion

		#region Loading / Saving

		protected override void FillFromDictionary(Dictionary<string, object> dic)
        {
            base.FillFromDictionary(dic);
            ImagePath = dic["ImagePath"].ToString();
            VideoPath = dic["VideoPath"].ToString();
            Prompt = dic["Prompt"].ToString();
            WorkflowPath = dic["WorkflowPath"].ToString();
            WorkflowJson = dic["WorkflowJson"].ToString();
            Status = dic["Status"].ToString();
            OrderIndex = int.Parse(dic["OrderIndex"].ToString());
            VideoGenerationStatePK = Guid.Parse(dic["VideoGenerationStatePK"].ToString());
            TransitionType = dic["TransitionType"].ToString();
            TransitionDuration = double.Parse(dic["TransitionDuration"].ToString());
            TransitionDropLastFrames = int.Parse(dic["TransitionDropLastFrames"].ToString());
			TransitionDropFirstFrames = int.Parse(dic["TransitionDropFirstFrames"].ToString());
			TransitionAddFrames = int.Parse(dic["TransitionAddFrames"].ToString());
            ClipSpeed = double.Parse(dic["ClipSpeed"].ToString());
        }

        protected override Dictionary<string, object> FillDictionary()
        {
            var dic = base.FillDictionary();
            dic["ImagePath"] = ImagePath;
            dic["VideoPath"] = VideoPath;
            dic["Prompt"] = Prompt;
            dic["WorkflowPath"] = WorkflowPath;
            dic["WorkflowJson"] = WorkflowJson;
            dic["Status"] = Status;
            dic["OrderIndex"] = OrderIndex;
            dic["VideoGenerationStatePK"] = VideoGenerationStatePK;
            dic["TransitionType"] = TransitionType;
            dic["TransitionDuration"] = TransitionDuration;
            dic["TransitionDropLastFrames"] = TransitionDropLastFrames;
			dic["TransitionDropFirstFrames"] = TransitionDropFirstFrames;
			dic["TransitionAddFrames"] = TransitionAddFrames;
            dic["ClipSpeed"] = ClipSpeed;

            return dic;
        }

        protected override void SetDefaultPropertyValues()
        {
            base.SetDefaultPropertyValues();

            ImagePath = string.Empty;
            VideoPath = string.Empty;
            Prompt = string.Empty;
            WorkflowPath = string.Empty;
            WorkflowJson = string.Empty;
            TransitionType = "Interpolate";
            TransitionDuration = 0.15;
            TransitionDropFirstFrames = 1;
            TransitionDropLastFrames = 1;
            TransitionAddFrames = 1;
            ClipSpeed = 1;
        }

        #endregion
    }
}
