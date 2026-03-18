using System;
using System.Drawing;
using System.Windows.Forms;

namespace LockboxQr
{
    /// <summary>
    /// Kilitli kasa durumu için modern uyarı diyaloğu.
    /// QR tarama ekranına geçmeden önce kullanıcıya bilgi verir.
    /// </summary>
    public class LockerLockedDialog : Form
    {
        private readonly string _lockerDisplayName;

        private Label _lblTitle = null!;
        private Label _lblMessage = null!;
        private Button _btnScan = null!;
        private Button _btnClose = null!;

        public LockerLockedDialog(string lockerDisplayName)
        {
            _lockerDisplayName = lockerDisplayName;
            InitializeUi();
        }

        private void InitializeUi()
        {
            this.Text = "Kasa Kilitli";
            this.Size = new Size(420, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            var iconLabel = new Label
            {
                Text = "🔒",
                Font = new Font("Segoe UI Emoji", 24, FontStyle.Regular), // daha küçük ikon
                AutoSize = true,
                Location = new Point(20, 35)
            };

            _lblTitle = new Label
            {
                Text = $"{_lockerDisplayName} Kilitli",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(90, 35)
            };

            _lblMessage = new Label
            {
                Text = "Bu kasa şu anda kilitli.\nAçmak için geçerli QR kodu kasaya gösterip\n\"QR Tara\" butonuna basabilirsiniz.",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(90, 70)
            };

            _btnScan = new Button
            {
                Text = "QR Tara",
                DialogResult = DialogResult.OK,
                Size = new Size(100, 32),
                Location = new Point(90, 140)
            };

            _btnClose = new Button
            {
                Text = "İptal",
                DialogResult = DialogResult.Cancel,
                Size = new Size(100, 32),
                Location = new Point(210, 140)
            };

            this.AcceptButton = _btnScan;
            this.CancelButton = _btnClose;

            Controls.Add(iconLabel);
            Controls.Add(_lblTitle);
            Controls.Add(_lblMessage);
            Controls.Add(_btnScan);
            Controls.Add(_btnClose);
        }
    }
}


