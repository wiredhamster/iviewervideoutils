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
            textBox1 = new TextBox();
            lblEffect = new Label();
            cboEffect = new ComboBox();
            txtDropFrames = new TextBox();
            txtAddFrames = new TextBox();
            lblDrop = new Label();
            lblAdd = new Label();
            SuspendLayout();
            // 
            // btnClip
            // 
            btnClip.Location = new Point(5, 0);
            btnClip.Name = "btnClip";
            btnClip.Size = new Size(142, 23);
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
            // textBox1
            // 
            textBox1.Location = new Point(48, 29);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(99, 23);
            textBox1.TabIndex = 2;
            textBox1.Text = "1";
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
            cboEffect.FormattingEnabled = true;
            cboEffect.Items.AddRange(new object[] { "None", "Interpolate", "Fade" });
            cboEffect.Location = new Point(48, 58);
            cboEffect.Name = "cboEffect";
            cboEffect.Size = new Size(99, 23);
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
            txtAddFrames.Location = new Point(48, 116);
            txtAddFrames.Name = "txtAddFrames";
            txtAddFrames.Size = new Size(99, 23);
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
            // ClipControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(lblAdd);
            Controls.Add(lblDrop);
            Controls.Add(txtAddFrames);
            Controls.Add(txtDropFrames);
            Controls.Add(cboEffect);
            Controls.Add(lblEffect);
            Controls.Add(textBox1);
            Controls.Add(lblSpeed);
            Controls.Add(btnClip);
            Name = "ClipControl";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnClip;
        private Label lblSpeed;
        private TextBox textBox1;
        private Label lblEffect;
        private ComboBox cboEffect;
        private TextBox txtDropFrames;
        private TextBox txtAddFrames;
        private Label lblDrop;
        private Label lblAdd;
    }
}
