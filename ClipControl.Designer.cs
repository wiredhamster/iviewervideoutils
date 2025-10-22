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
			txtDropLastFrames = new TextBox();
			txtAddFrames = new TextBox();
			txtDropFirstFrames = new TextBox();
			lblDropLast = new Label();
			lblDropFirst = new Label();
			lblAdd = new Label();
			lblLength = new Label();
			txtLength = new TextBox();
			flowPanel = new FlowLayoutPanel();
			flowPanel.SuspendLayout();
			SuspendLayout();
			// 
			// btnClip
			// 
			btnClip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			btnClip.Location = new Point(3, 3);
			btnClip.Name = "btnClip";
			btnClip.Size = new Size(164, 23);
			btnClip.TabIndex = 0;
			btnClip.Text = "Clip";
			btnClip.UseVisualStyleBackColor = true;
			// 
			// lblSpeed
			// 
			lblSpeed.Location = new Point(3, 29);
			lblSpeed.Name = "lblSpeed";
			lblSpeed.Size = new Size(75, 26);
			lblSpeed.TabIndex = 1;
			lblSpeed.Text = "Speed";
			lblSpeed.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// txtSpeed
			// 
			txtSpeed.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtSpeed.Location = new Point(84, 32);
			txtSpeed.Name = "txtSpeed";
			txtSpeed.Size = new Size(83, 23);
			txtSpeed.TabIndex = 2;
			txtSpeed.Text = "1";
			// 
			// lblEffect
			// 
			lblEffect.Location = new Point(3, 58);
			lblEffect.Name = "lblEffect";
			lblEffect.Size = new Size(75, 26);
			lblEffect.TabIndex = 3;
			lblEffect.Text = "Effect";
			lblEffect.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// cboEffect
			// 
			cboEffect.FormattingEnabled = true;
			cboEffect.Items.AddRange(new object[] { "None", "Interpolate", "Fade", "Redirect" });
			cboEffect.Location = new Point(84, 61);
			cboEffect.Name = "cboEffect";
			cboEffect.Size = new Size(83, 23);
			cboEffect.TabIndex = 4;
			cboEffect.Text = "Interpolate";
			// 
			// txtDropLastFrames
			// 
			txtDropLastFrames.Location = new Point(84, 148);
			txtDropLastFrames.Name = "txtDropLastFrames";
			txtDropLastFrames.Size = new Size(83, 23);
			txtDropLastFrames.TabIndex = 11;
			txtDropLastFrames.Text = "2";
			// 
			// txtAddFrames
			// 
			txtAddFrames.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtAddFrames.Location = new Point(84, 177);
			txtAddFrames.Name = "txtAddFrames";
			txtAddFrames.Size = new Size(83, 23);
			txtAddFrames.TabIndex = 13;
			txtAddFrames.Text = "2";
			// 
			// txtDropFirstFrames
			// 
			txtDropFirstFrames.Location = new Point(84, 119);
			txtDropFirstFrames.Name = "txtDropFirstFrames";
			txtDropFirstFrames.Size = new Size(83, 23);
			txtDropFirstFrames.TabIndex = 9;
			txtDropFirstFrames.Text = "2";
			// 
			// lblDropLast
			// 
			lblDropLast.Location = new Point(3, 145);
			lblDropLast.Name = "lblDropLast";
			lblDropLast.Size = new Size(75, 26);
			lblDropLast.TabIndex = 10;
			lblDropLast.Text = "Drop Last";
			lblDropLast.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// lblDropFirst
			// 
			lblDropFirst.Location = new Point(3, 116);
			lblDropFirst.Name = "lblDropFirst";
			lblDropFirst.Size = new Size(75, 26);
			lblDropFirst.TabIndex = 8;
			lblDropFirst.Text = "Drop First";
			lblDropFirst.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// lblAdd
			// 
			lblAdd.Location = new Point(3, 174);
			lblAdd.Name = "lblAdd";
			lblAdd.Size = new Size(75, 26);
			lblAdd.TabIndex = 12;
			lblAdd.Text = "Add";
			lblAdd.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// lblLength
			// 
			lblLength.Location = new Point(3, 87);
			lblLength.Name = "lblLength";
			lblLength.Size = new Size(75, 26);
			lblLength.TabIndex = 14;
			lblLength.Text = "Length";
			lblLength.TextAlign = ContentAlignment.MiddleLeft;
			lblLength.Visible = false;
			// 
			// txtLength
			// 
			txtLength.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtLength.Location = new Point(84, 90);
			txtLength.Name = "txtLength";
			txtLength.Size = new Size(83, 23);
			txtLength.TabIndex = 15;
			txtLength.Text = "0.1";
			txtLength.Visible = false;
			// 
			// flowPanel
			// 
			flowPanel.Controls.Add(btnClip);
			flowPanel.Controls.Add(lblSpeed);
			flowPanel.Controls.Add(txtSpeed);
			flowPanel.Controls.Add(lblEffect);
			flowPanel.Controls.Add(cboEffect);
			flowPanel.Controls.Add(lblLength);
			flowPanel.Controls.Add(txtLength);
			flowPanel.Controls.Add(lblDropFirst);
			flowPanel.Controls.Add(txtDropFirstFrames);
			flowPanel.Controls.Add(lblDropLast);
			flowPanel.Controls.Add(txtDropLastFrames);
			flowPanel.Controls.Add(lblAdd);
			flowPanel.Controls.Add(txtAddFrames);
			flowPanel.Dock = DockStyle.Fill;
			flowPanel.Location = new Point(0, 0);
			flowPanel.Name = "flowPanel";
			flowPanel.Size = new Size(170, 206);
			flowPanel.TabIndex = 0;
			// 
			// ClipControl
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add(flowPanel);
			Name = "ClipControl";
			Size = new Size(170, 206);
			flowPanel.ResumeLayout(false);
			flowPanel.PerformLayout();
			ResumeLayout(false);
		}

		#endregion

		public Button btnClip;
        private Label lblSpeed;
        public TextBox txtSpeed;
        private Label lblEffect;
        public ComboBox cboEffect;
        public TextBox txtDropFirstFrames;
		public TextBox txtDropLastFrames;
        public TextBox txtAddFrames;
        private Label lblDropLast;
		private Label lblDropFirst;
        private Label lblAdd;
        private Label lblLength;
        public TextBox txtLength;
		private FlowLayoutPanel flowPanel;
	}
}
