using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iviewer.Video
{
    public partial class ClipControl : UserControl
    {
        public ClipControl()
        {
            InitializeComponent();
            this.AutoScaleMode = AutoScaleMode.Dpi;  // Or AutoScaleMode.Font if font changes are the trigger
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);  // Base on 100% scaling (96 DPI)

            EnableTransitionControls();
        }

        public new bool Enabled
        {
            get
            {
                return base.Enabled;
            }
            set
            {
                if (base.Enabled != value)
                {
                    btnClip.Enabled = value;
                    txtSpeed.Enabled = value;

                    base.Enabled = value;

                    EnableTransitionControls();
                }
            }
        }

        public bool LastClip { get; set; }

        public void EnableTransitionControls()
        {
           if (Enabled && !LastClip && cboEffect.Text == "Fade")
           {
                btnClip.Enabled = true;
                txtSpeed.Visible = true;
                lblSpeed.Visible = true;
                cboEffect.Visible = true;
                lblEffect.Visible = true;
                lblAdd.Visible = false;
                txtAddFrames.Visible = false;
                lblDrop.Visible = false;
                txtDropFrames.Visible = false;
                lblLength.Visible = true;
                txtLength.Visible = true;
            }
            else if (Enabled && !LastClip && cboEffect.Text == "Interpolate")
            {
                btnClip.Enabled = true;
                txtSpeed.Visible = true;
                lblSpeed.Visible = true;
                cboEffect.Visible = true;
                lblEffect.Visible = true;
                txtAddFrames.Visible = true;
                lblAdd.Visible = true;
                txtDropFrames.Visible = true;
                lblDrop.Visible = true;
                txtLength.Visible = false;
                lblLength.Visible = false;
            }
            else if (Enabled && LastClip)
            {
                btnClip.Enabled = true;
                txtSpeed.Visible = true;
                lblSpeed.Visible = true;
                cboEffect.Visible = false;
                lblEffect.Visible = false;
                txtAddFrames.Visible = false;
                lblAdd.Visible = false;
                txtDropFrames.Visible = false;
                lblDrop.Visible = false;
                txtLength.Visible = false;
                lblLength.Visible = false;
            }
            else
            {
                btnClip.Enabled = false;
                txtSpeed.Visible = true;
                lblSpeed.Visible = true;
                cboEffect.Visible = true;
                lblEffect.Visible = true;
                txtAddFrames.Visible = false;
                lblAdd.Visible = false;
                txtDropFrames.Visible = false;
                lblDrop.Visible = false;
                txtLength.Visible = false;
                lblLength.Visible = false;
            }
        }
    }
}
