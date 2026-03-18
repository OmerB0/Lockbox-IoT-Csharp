namespace LockboxQr
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageGenerate;
        private System.Windows.Forms.TabPage tabPageScan;
        
        // QR Generate Tab Controls
        private System.Windows.Forms.Label lblLockerId;
        private System.Windows.Forms.TextBox txtLockerId;
        private System.Windows.Forms.Label lblValidity;
        private System.Windows.Forms.NumericUpDown numValidity;
        private System.Windows.Forms.CheckBox chkOneTime;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.PictureBox picQRCode;
        private System.Windows.Forms.TextBox txtToken;
        private System.Windows.Forms.Label lblInfo;
        
        // QR Scan Tab Controls
        private System.Windows.Forms.ComboBox cmbCameras;
        private System.Windows.Forms.Label lblCamera;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblSuccessOverlay;
        
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageGenerate = new System.Windows.Forms.TabPage();
            this.tabPageScan = new System.Windows.Forms.TabPage();
            
            // Generate Tab
            this.lblLockerId = new System.Windows.Forms.Label();
            this.txtLockerId = new System.Windows.Forms.TextBox();
            this.lblValidity = new System.Windows.Forms.Label();
            this.numValidity = new System.Windows.Forms.NumericUpDown();
            this.chkOneTime = new System.Windows.Forms.CheckBox();
            this.btnGenerate = new System.Windows.Forms.Button();
            this.picQRCode = new System.Windows.Forms.PictureBox();
            this.txtToken = new System.Windows.Forms.TextBox();
            this.lblInfo = new System.Windows.Forms.Label();
            
            // Scan Tab
            this.lblCamera = new System.Windows.Forms.Label();
            this.cmbCameras = new System.Windows.Forms.ComboBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblSuccessOverlay = new System.Windows.Forms.Label();
            
            this.tabControl.SuspendLayout();
            this.tabPageGenerate.SuspendLayout();
            this.tabPageScan.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numValidity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picQRCode)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.SuspendLayout();
            
            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 700);
            this.Text = "Akıllı Kasa - QR Yönetimi";
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            
            // TabControl
            this.tabControl.Controls.Add(this.tabPageScan);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(900, 700);
            this.tabControl.TabIndex = 0;
            
            // Generate Tab (artık kullanılmıyor, sadece designer alanı için tutuluyor)
            
            // Scan Tab
            this.tabPageScan.Controls.Add(this.lblSuccessOverlay);
            this.tabPageScan.Controls.Add(this.lblStatus);
            this.tabPageScan.Controls.Add(this.picPreview);
            this.tabPageScan.Controls.Add(this.btnStop);
            this.tabPageScan.Controls.Add(this.btnStart);
            this.tabPageScan.Controls.Add(this.cmbCameras);
            this.tabPageScan.Controls.Add(this.lblCamera);
            this.tabPageScan.Location = new System.Drawing.Point(4, 24);
            this.tabPageScan.Name = "tabPageScan";
            this.tabPageScan.Padding = new System.Windows.Forms.Padding(10);
            this.tabPageScan.Size = new System.Drawing.Size(892, 672);
            this.tabPageScan.TabIndex = 1;
            this.tabPageScan.Text = "QR Tara";
            this.tabPageScan.UseVisualStyleBackColor = true;
            
            // Camera label
            this.lblCamera.AutoSize = true;
            this.lblCamera.Location = new System.Drawing.Point(13, 20);
            this.lblCamera.Name = "lblCamera";
            this.lblCamera.Size = new System.Drawing.Size(50, 15);
            this.lblCamera.TabIndex = 0;
            this.lblCamera.Text = "Kamera:";
            
            // Camera ComboBox
            this.cmbCameras.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCameras.Location = new System.Drawing.Point(100, 17);
            this.cmbCameras.Name = "cmbCameras";
            this.cmbCameras.Size = new System.Drawing.Size(300, 23);
            this.cmbCameras.TabIndex = 1;
            
            // Start button
            this.btnStart.Location = new System.Drawing.Point(420, 15);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(100, 30);
            this.btnStart.TabIndex = 2;
            this.btnStart.Text = "Başlat";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            
            // Stop button
            this.btnStop.Location = new System.Drawing.Point(530, 15);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(100, 30);
            this.btnStop.TabIndex = 3;
            this.btnStop.Text = "Durdur";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Enabled = false;
            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
            
            // Preview PictureBox
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Location = new System.Drawing.Point(13, 60);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(640, 480);
            this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPreview.TabIndex = 4;
            this.picPreview.TabStop = false;
            
            // Status label
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblStatus.Location = new System.Drawing.Point(13, 550);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(85, 21);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "Beklemede";
            
            // Success overlay
            this.lblSuccessOverlay.AutoSize = false;
            this.lblSuccessOverlay.BackColor = System.Drawing.Color.FromArgb(200, 0, 200, 0);
            this.lblSuccessOverlay.Font = new System.Drawing.Font("Segoe UI", 48F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblSuccessOverlay.ForeColor = System.Drawing.Color.White;
            this.lblSuccessOverlay.Location = new System.Drawing.Point(200, 200);
            this.lblSuccessOverlay.Name = "lblSuccessOverlay";
            this.lblSuccessOverlay.Size = new System.Drawing.Size(500, 200);
            this.lblSuccessOverlay.TabIndex = 6;
            this.lblSuccessOverlay.Text = "AÇILDI ✅";
            this.lblSuccessOverlay.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblSuccessOverlay.Visible = false;
            
            this.Controls.Add(this.tabControl);
            
            this.tabControl.ResumeLayout(false);
            this.tabPageGenerate.ResumeLayout(false);
            this.tabPageGenerate.PerformLayout();
            this.tabPageScan.ResumeLayout(false);
            this.tabPageScan.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numValidity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picQRCode)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
