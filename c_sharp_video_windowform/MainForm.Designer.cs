using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using System.Xml.Linq;

namespace c_sharp_video_windowform
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Button btnSelectTxt;
        private System.Windows.Forms.Button btnSelectImage;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.Label lblStatus;

        private System.Windows.Forms.TextBox txtTextFile;
        private System.Windows.Forms.TextBox txtImageFile;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            btnSelectTxt = new Button();
            btnSelectImage = new Button();
            btnGenerate = new Button();
            lblStatus = new Label();
            txtTextFile = new TextBox();
            txtImageFile = new TextBox();

            SuspendLayout();

            // ---- txtTextFile ----
            txtTextFile.Location = new Point(190, 25);
            txtTextFile.Size = new Size(350, 27);
            txtTextFile.ReadOnly = true;

            // ---- txtImageFile ----
            txtImageFile.Location = new Point(190, 75);
            txtImageFile.Size = new Size(350, 27);
            txtImageFile.ReadOnly = true;

            // ---- btnSelectTxt ----
            btnSelectTxt.Location = new Point(20, 20);
            btnSelectTxt.Name = "btnSelectTxt";
            btnSelectTxt.Size = new Size(150, 40);
            btnSelectTxt.Text = "Select Text File";
            btnSelectTxt.Click += BtnSelectTxt_Click;

            // ---- btnSelectImage ----
            btnSelectImage.Location = new Point(20, 70);
            btnSelectImage.Name = "btnSelectImage";
            btnSelectImage.Size = new Size(150, 40);
            btnSelectImage.Text = "Select Image";
            btnSelectImage.Click += BtnSelectImage_Click;

            // ---- btnGenerate ----
            btnGenerate.Location = new Point(20, 130);
            btnGenerate.Name = "btnGenerate";
            btnGenerate.Size = new Size(150, 40);
            btnGenerate.Text = "Generate Video";
            btnGenerate.Click += BtnGenerate_Click;

            // ---- lblStatus ----
            lblStatus.Location = new Point(20, 190);
            lblStatus.Size = new Size(520, 40);
            lblStatus.Text = "Status: Idle";
            lblStatus.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Regular);
            lblStatus.ForeColor = Color.DarkBlue;

            // ---- Add controls
            Controls.Add(btnSelectTxt);
            Controls.Add(btnSelectImage);
            Controls.Add(btnGenerate);
            Controls.Add(lblStatus);
            Controls.Add(txtTextFile);
            Controls.Add(txtImageFile);

            // ---- Form settings
            ClientSize = new Size(600, 260);
            Name = "MainForm";
            Text = "Video Generator";
            StartPosition = FormStartPosition.CenterScreen;
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
