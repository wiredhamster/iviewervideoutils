using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iviewer
{
	public partial class Loras : UserControl
	{
		public Loras()
		{
			InitializeComponent();

			treeSelector.Height = listPreview.Height;
			listPreview.Columns[0].Width = listPreview.Width;
		}

		public event EventHandler LoraCopied;

		public void LoadData()
		{
			if (cboCompatibility.Items.Count > 0) return;

			var compatibilityList = new HashSet<string>();

			var sql = "SELECT DISTINCT Compatibility FROM Loras";
			var dataTable = DB.Select(sql);
			for (int i = 0; i < dataTable.Rows.Count; i++)
			{
				var models = dataTable.Rows[i].ItemArray[0].ToString().Split(',');
				foreach (var model in models)
				{
					var trimmedModel = model.Trim();
					if (!compatibilityList.Contains(trimmedModel))
					{
						compatibilityList.Add(trimmedModel);
					}
				}
			}

			cboCompatibility.Items.AddRange(compatibilityList.OrderBy(x => x).ToArray());
			cboCompatibility.Text = "Wan 2.2 I2V";

			LoadCategories(cboCompatibility.Text);
		}

		void LoadCategories(string compatibility)
		{
			treeSelector.Nodes.Clear();
			var loraNode = new TreeNode("Loras");
			treeSelector.Nodes.Add(loraNode);

			var sql = $"SELECT DISTINCT Category FROM Loras WHERE Compatibility LIKE {DB.FormatDBValue("%" + compatibility + "%")} ORDER BY Category";
			var lorasTable = DB.Select(sql);
			for (int i = 0; i < lorasTable.Rows.Count; i++)
			{
				var categoryEntry = lorasTable.Rows[i].ItemArray[0].ToString();
				var categoryArray = categoryEntry.Split("->");

				var parent = loraNode;
				foreach (var category in categoryArray)
				{
					if (!parent.Nodes.ContainsKey(category.ToString()))
					{

						var node = parent.Nodes.Add(category.ToString(), category);
						node.Tag = "loraCategory";

						parent = node;
					}
					else
					{
						parent = parent.Nodes[category.ToString()];
					}
				}
			}

			loraNode.Expand();
		}

		void LoadLoras()
		{
			var node = treeSelector.SelectedNode;
			if (node == null) return;

			var category = "";

			listPreview.Items.Clear();
			txtNotes.Text = "";
			linkURL.Text = "";
			listPrompts.Items.Clear();

			while (node.Parent != null)
			{
				if (category != "")
				{
					category = $"->{category}";
				}

				category = $"{node.Text}{category}";

				node = node.Parent;
			}

			if (category == "") return;

			var sql = $"SELECT * FROM Loras WHERE Category = {DB.FormatDBValue(category)} AND Compatibility LIKE {DB.FormatDBValue("%" + cboCompatibility.Text + "%")} ORDER BY [Key]";
			var dataTable = DB.Select(sql);
			for (var i = 0; i < dataTable.Rows.Count; i++)
			{
				var lora = new Lora();
				lora.LoadFromRow(dataTable.Rows[i]);

				var item = new ListViewItem(lora.ToString());
				item.Tag = lora;
				listPreview.Items.Add(item);
			}
		}

		void LoadLoraDetails()
		{
			if (listPreview.SelectedItems.Count == 0) return;

			var lora = listPreview.SelectedItems[0].Tag as Lora;
			if (lora == null) return;

			txtNotes.Text = lora.Notes
				.Replace("\r\n", Environment.NewLine)
				.Replace("\n", Environment.NewLine)
				.Replace("\r", Environment.NewLine);

			linkURL.Links.Clear();
			linkURL.Links.Add(0, lora.URL.Length, lora.URL);
			linkURL.Text = lora.URL;

			//var triggerWords = lora.TriggerWords.Split('|');
			var triggerWords = lora.TriggerWords.Split('\n').Where(w => !string.IsNullOrEmpty(w));
			listPrompts.Items.Clear();
			foreach (var triggerWord in triggerWords)
			{
				listPrompts.Items.Add(triggerWord.Trim());
			}
		}

		void CopyLoraDetails()
		{
			var lora = listPreview.SelectedItems[0]?.Tag as Lora;
			if (lora == null) return;

			var sb = new StringBuilder();
			foreach (var item in listPrompts.SelectedItems)
			{
				sb.Append(", " + item);
			}

			Clipboard.SetText($"{lora.ToString()}{sb.ToString()}");

			LoraCopied?.Invoke(this, EventArgs.Empty);
		}

		void SaveLora()
		{
			if (listPreview.SelectedItems.Count == 0) return;

			// Currently only Notes are editable
			var lora = listPreview.SelectedItems[0]?.Tag as Lora;
			if (lora == null) return;

			lora.Notes = txtNotes.Text;
			lora.Save();
		}
	}
}
