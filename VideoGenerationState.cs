using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.Design.AxImporter;

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
                state.clipGenerationStates?.Clear();
                return state;
            }

            return null;
        }

        public static VideoGenerationState Load(Guid pk)
        {
            return Load($"SELECT * FROM VideoGenerationStates WHERE PK = {DB.FormatDBValue(pk)}");
        }

        public override string TableName => "VideoGenerationStates";

        #region Persistent Properties

        public string ImagePath 
        {
            get => imagePath;
            set
            {
                if (imagePath != value)
                {
                    SetHasChanges();
                    imagePath = value;
                }
            }
        }
        string imagePath = "";

        public int Width 
        {
            get => width;
            set
            {
                if (width != value)
                {
                    width = value;
                    SetHasChanges();
                }
            }
        }
        int width = 0;

        public int Height 
        {
            get => height;
            set
            {
                if (height != value)
                {
                    height = value; 
                    SetHasChanges();
                }
            }
        }
        int height = 0;

        public string PreviewPath 
        {
            get => previewPath;
            set
            {
                if (previewPath != value)
                {
                    SetHasChanges();
                    previewPath = value;
                }
            }
        }
        string previewPath = "";

        public string TempFiles 
        {
            get => tempFiles;
            set
            {
                if (tempFiles != value)
                {
                    tempFiles = value;
                    SetHasChanges();
                }
            }
        }
        string tempFiles = "";

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

        #region Non-persistent properties

        public string TempDir
        {
            get
            {
                if (tempDir == null)
                {
                    tempDir = Path.Combine(VideoGenerationConfig.TempFileDir, PK.ToString());
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

        #region Clips

        public List<ClipGenerationState> ClipGenerationStates
        {
            get
            {
                if (clipGenerationStates == null)
                {
                    clipGenerationStates = new List<ClipGenerationState>();
                    if (IsInDatabase)
                    {
                        var sql = $"SELECT c.* FROM ClipGenerationStates c WITH (NOLOCK) WHERE VideoGenerationStatePK = {DB.FormatDBValue(PK)} ORDER BY OrderIndex";
                        var table = DB.Select(sql);
                        for (int i = 0; i < table.Rows.Count; i++)
                        {
                            var clip = new ClipGenerationState();
                            clip.LoadFromRow(table.Rows[i]);
                            clipGenerationStates.Add(clip);
                        }

                        // Sort in-place once after loading (ensures order without recreating lists)
                        clipGenerationStates.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
                    }
                }

                // Return the backing list without resorting into a new list.
                return clipGenerationStates;
            }
        }
        List<ClipGenerationState> clipGenerationStates;

        public void ResetClips()
        {
            clipGenerationStates = null;
        }

        #endregion

        #region Loading / Saving

        protected override void FillFromDictionary(Dictionary<string, object> dic)
        {
            base.FillFromDictionary(dic);
            ImagePath = dic["ImagePath"].ToString();
            Width = int.Parse(dic["Width"].ToString());
            Height = int.Parse(dic["Height"].ToString());
            PreviewPath = dic["PreviewVideoPath"].ToString();
            TempFiles = dic["TempFiles"].ToString();
            Status = dic["Status"].ToString();
        }

        protected override Dictionary<string, object> FillDictionary()
        {
            var dic = base.FillDictionary();
            dic["ImagePath"] = ImagePath;
            dic["Width"] = Width;
            dic["Height"] = Height;
            dic["PreviewVideoPath"] = PreviewPath;
            dic["TempFiles"] = TempFiles;
            dic["Status"] = Status;

            return dic;
        }

        protected override void SaveRelated()
        {
            base.SaveRelated();

            foreach (var clip in ClipGenerationStates)
            {
                clip.Save();
            }
        }

        #endregion
    }
}
