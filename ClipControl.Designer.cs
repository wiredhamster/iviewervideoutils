namespace iviewer.Video
{
    partial class ClipControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnClip = new Button();
            lblSpeed = new Label();
            txtSpeed = new TextBox();
            lblEffect = new Label();
            cboEffect = new ComboBox();
            txtDropFrames = new TextBox();
            txtAddFrames = new TextBox();
            lblDrop = new Label();
            lblAdd = new Label();
            lblLength = new Label();
            txtLength = new TextBox();
            SuspendLayout();
            // 
            // btnClip
            // 
            btnClip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnClip.Location = new Point(5, 0);
            btnClip.Name = "btnClip";
            btnClip.Size = new Size(126, 23);
            btnClip.TabIndex = 0;
            btnClip.Text = "Clip";
            btnClip.UseVisualStyleBackColor = true;
            // 
            // lblSpeed
            // 
            lblSpeed.AutoSize = true;
            lblSpeed.Location = new Point(3, 31);
            lblSpeed.Name = "lblSpeed";
            lblSpeed.Size = new Size(39, 15);
            lblSpeed.TabIndex = 1;
            lblSpeed.Text = "Speed";
            // 
            // txtSpeed
            // 
            txtSpeed.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSpeed.Location = new Point(48, 29);
            txtSpeed.Name = "txtSpeed";
            txtSpeed.Size = new Size(83, 23);
            txtSpeed.TabIndex = 2;
            txtSpeed.Text = "1";
            // 
            // lblEffect
            // 
            lblEffect.AutoSize = true;
            lblEffect.Location = new Point(4, 61);
            lblEffect.Name = "lblEffect";
            lblEffect.Size = new Size(37, 15);
            lblEffect.TabIndex = 3;
            lblEffect.Text = "Effect";
            // 
            // cboEffect
            // 
            cboEffect.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cboEffect.FormattingEnabled = true;
            cboEffect.Items.AddRange(new object[] { "None", "Interpolate", "Fade" });
            cboEffect.Location = new Point(48, 58);
            cboEffect.Name = "cboEffect";
            cboEffect.Size = new Size(83, 23);
            cboEffect.TabIndex = 4;
            cboEffect.Text = "Interpolate";
            // 
            // txtDropFrames
            // 
            txtDropFrames.Location = new Point(48, 87);
            txtDropFrames.Name = "txtDropFrames";
            txtDropFrames.Size = new Size(99, 23);
            txtDropFrames.TabIndex = 5;
            txtDropFrames.Text = "2";
            // 
            // txtAddFrames
            // 
            txtAddFrames.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtAddFrames.Location = new Point(48, 116);
            txtAddFrames.Name = "txtAddFrames";
            txtAddFrames.Size = new Size(83, 23);
            txtAddFrames.TabIndex = 6;
            txtAddFrames.Text = "2";
            // 
            // lblDrop
            // 
            lblDrop.AutoSize = true;
            lblDrop.Location = new Point(5, 90);
            lblDrop.Name = "lblDrop";
            lblDrop.Size = new Size(33, 15);
            lblDrop.TabIndex = 7;
            lblDrop.Text = "Drop";
            // 
            // lblAdd
            // 
            lblAdd.AutoSize = true;
            lblAdd.Location = new Point(5, 119);
            lblAdd.Name = "lblAdd";
            lblAdd.Size = new Size(29, 15);
            lblAdd.TabIndex = 8;
            lblAdd.Text = "Add";
            // 
            // lblLength
            // 
            lblLength.AutoSize = true;
            lblLength.Location = new Point(5, 90);
            lblLength.Name = "lblLength";
            lblLength.Size = new Size(44, 15);
            lblLength.TabIndex = 9;
            lblLength.Text = "Length";
            lblLength.Visible = false;
            // 
            // txtLength
            // 
            txtLength.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtLength.Location = new Point(48, 87);
            txtLength.Name = "txtLength";
            txtLength.Size = new Size(83, 23);
            txtLength.TabIndex = 10;
            txtLength.Text = "0.1";
            txtLength.Visible = false;
            // 
            // ClipControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(txtLength);
            Controls.Add(lblLength);
            Controls.Add(lblAdd);
            Controls.Add(lblDrop);
            Controls.Add(txtAddFrames);
            Controls.Add(txtDropFrames);
            Controls.Add(cboEffect);
            Controls.Add(lblEffect);
            Controls.Add(txtSpeed);
            Controls.Add(lblSpeed);
            Controls.Add(btnClip);
            Name = "ClipControl";
            Size = new Size(134, 145);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        public Button btnClip;
        private Label lblSpeed;
        public TextBox txtSpeed;
        private Label lblEffect;
        public ComboBox cboEffect;
        public TextBox txtDropFrames;
        public TextBox txtAddFrames;
        private Label lblDrop;
        private Label lblAdd;
        private Label lblLength;
        public TextBox txtLength;
    }
}
