namespace iviewer
{
	partial class Loras
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
			groupSelector = new GroupBox();
			listPreview = new ListView();
			Entry = new ColumnHeader();
			treeSelector = new TreeView();
			listPrompts = new ListBox();
			linkURL = new LinkLabel();
			lblCompatibility = new Label();
			cboCompatibility = new ComboBox();
			btnCopy = new Button();
			txtNotes = new TextBox();
			btnSave = new Button();
			groupSelector.SuspendLayout();
			SuspendLayout();
			// 
			// groupSelector
			// 
			groupSelector.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			groupSelector.Controls.Add(listPreview);
			groupSelector.Controls.Add(treeSelector);
			groupSelector.Location = new Point(1, 1);
			groupSelector.Margin = new Padding(1);
			groupSelector.Name = "groupSelector";
			groupSelector.Padding = new Padding(1);
			groupSelector.Size = new Size(838, 191);
			groupSelector.TabIndex = 5;
			groupSelector.TabStop = false;
			groupSelector.Text = "Selector";
			// 
			// listPreview
			// 
			listPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			listPreview.Columns.AddRange(new ColumnHeader[] { Entry });
			listPreview.FullRowSelect = true;
			listPreview.HeaderStyle = ColumnHeaderStyle.None;
			listPreview.LabelWrap = false;
			listPreview.Location = new Point(267, 14);
			listPreview.Margin = new Padding(1);
			listPreview.MultiSelect = false;
			listPreview.Name = "listPreview";
			listPreview.Size = new Size(569, 177);
			listPreview.TabIndex = 6;
			listPreview.UseCompatibleStateImageBehavior = false;
			listPreview.View = View.Details;
			listPreview.SelectedIndexChanged += ListPreview_SelectedIndexChanged;
			// 
			// Entry
			// 
			Entry.Width = 500;
			// 
			// treeSelector
			// 
			treeSelector.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			treeSelector.Location = new Point(2, 14);
			treeSelector.Margin = new Padding(1);
			treeSelector.Name = "treeSelector";
			treeSelector.Size = new Size(265, 177);
			treeSelector.TabIndex = 5;
			treeSelector.AfterSelect += TreeSelector_AfterSelect;
			// 
			// listPrompts
			// 
			listPrompts.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			listPrompts.FormattingEnabled = true;
			listPrompts.ItemHeight = 15;
			listPrompts.Location = new Point(3, 196);
			listPrompts.Name = "listPrompts";
			listPrompts.Size = new Size(834, 109);
			listPrompts.TabIndex = 6;
			listPrompts.SelectionMode = SelectionMode.MultiExtended;
			// 
			// linkURL
			// 
			linkURL.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			linkURL.AutoSize = true;
			linkURL.Location = new Point(3, 390);
			linkURL.Name = "linkURL";
			linkURL.Size = new Size(0, 15);
			linkURL.TabIndex = 8;
			// 
			// lblCompatibility
			// 
			lblCompatibility.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			lblCompatibility.AutoSize = true;
			lblCompatibility.Location = new Point(3, 414);
			lblCompatibility.Name = "lblCompatibility";
			lblCompatibility.Size = new Size(79, 15);
			lblCompatibility.TabIndex = 9;
			lblCompatibility.Text = "Compatibility";
			// 
			// cboCompatibility
			// 
			cboCompatibility.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			cboCompatibility.FormattingEnabled = true;
			cboCompatibility.Location = new Point(88, 411);
			cboCompatibility.Name = "cboCompatibility";
			cboCompatibility.Size = new Size(180, 23);
			cboCompatibility.TabIndex = 10;
			cboCompatibility.SelectedIndexChanged += CboCompatibility_SelectedIndexChanged;
			// 
			// btnCopy
			// 
			btnCopy.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnCopy.Location = new Point(762, 411);
			btnCopy.Name = "btnCopy";
			btnCopy.Size = new Size(75, 23);
			btnCopy.TabIndex = 13;
			btnCopy.Text = "Copy";
			btnCopy.UseVisualStyleBackColor = true;
			btnCopy.Click += BtnCopy_Click;
			// 
			// txtNotes
			// 
			txtNotes.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			txtNotes.Location = new Point(3, 311);
			txtNotes.Multiline = true;
			txtNotes.Name = "txtNotes";
			txtNotes.ScrollBars = ScrollBars.Vertical;
			txtNotes.Size = new Size(834, 75);
			txtNotes.TabIndex = 12;
			// 
			// btnSave
			// 
			btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnSave.Location = new Point(681, 412);
			btnSave.Name = "btnSave";
			btnSave.Size = new Size(75, 23);
			btnSave.TabIndex = 12;
			btnSave.Text = "Save";
			btnSave.UseVisualStyleBackColor = true;
			btnSave.Click += btnSave_Click;
			// 
			// Loras
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add(btnSave);
			Controls.Add(txtNotes);
			Controls.Add(btnCopy);
			Controls.Add(cboCompatibility);
			Controls.Add(lblCompatibility);
			Controls.Add(linkURL);
			Controls.Add(listPrompts);
			Controls.Add(groupSelector);
			Name = "Loras";
			Size = new Size(840, 438);
			groupSelector.ResumeLayout(false);
			ResumeLayout(false);
			PerformLayout();
		}

		private void BtnCopy_Click(object sender, EventArgs e)
		{
			CopyLoraDetails();
		}

		private void ListPreview_SelectedIndexChanged(object sender, EventArgs e)
		{
			LoadLoraDetails();
		}

		private void TreeSelector_AfterSelect(object sender, TreeViewEventArgs e)
		{
			LoadLoras();
		}

		private void CboCompatibility_SelectedIndexChanged(object sender, EventArgs e)
		{
			LoadCategories(cboCompatibility.Text);
		}

		private void btnSave_Click(object sender, EventArgs e)
		{
			SaveLora();
		}

		#endregion

		private GroupBox groupSelector;
		private TreeView treeSelector;
		private ListBox listPrompts;
		private LinkLabel linkURL;
		private Label lblCompatibility;
		private ComboBox cboCompatibility;
		private Button btnCopy;
		private TextBox txtNotes;
		private ListView listPreview;
		private ColumnHeader Entry;
		private Button btnSave;
	}
}
