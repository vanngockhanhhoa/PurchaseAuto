namespace AutoPurchase;

using System;
using System.Windows.Forms;

public partial class Form1 : Form
    {
        private TextBox txtCoin;
        private ComboBox cmbCardType;
        private Button btnStart;
        private Label lblCountdown;
        private System.Windows.Forms.Timer _countdownTimer;
        private int _secondsLeft;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Automation Buy Tool";
            this.Width = 500;
            this.Height = 520;

            Label lblCoin = new Label()
            {
                Text = "Số coin:",
                Left = 30,
                Top = 30,
                Width = 100
            };

            txtCoin = new TextBox()
            {
                Left = 150,
                Top = 30,
                Width = 150
            };

            Label lblCard = new Label()
            {
                Text = "Loại thẻ:",
                Left = 30,
                Top = 70,
                Width = 100
            };

            cmbCardType = new ComboBox()
            {
                Left = 150,
                Top = 70,
                Width = 150
            };

            cmbCardType.Items.AddRange(new string[]
            {
                "Visa",
                "MasterCard",
                "Momo"
            });

            btnStart = new Button()
            {
                Text = "Start",
                Left = 150,
                Top = 120,
                Width = 100
            };

            lblCountdown = new Label()
            {
                Text = "",
                Left = 30,
                Top = 160,
                Width = 400,
                Height = 40,
                Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.DarkRed,
                Visible = false
            };

            btnStart.Click += btnStart_Click;

            this.Controls.Add(lblCoin);
            this.Controls.Add(txtCoin);
            this.Controls.Add(lblCard);
            this.Controls.Add(cmbCardType);
            this.Controls.Add(btnStart);
            this.Controls.Add(lblCountdown);
        }

        public void StartCountdown(int seconds = 60)
        {
            _secondsLeft = seconds;
            lblCountdown.Text = $"Scan QR code: {_secondsLeft}s";
            lblCountdown.Visible = true;

            _countdownTimer = new System.Windows.Forms.Timer();
            _countdownTimer.Interval = 1000;
            _countdownTimer.Tick += (s, e) =>
            {
                _secondsLeft--;
                if (_secondsLeft <= 0)
                {
                    _countdownTimer.Stop();
                    lblCountdown.Text = "Hết giờ, đang thử lại...";
                }
                else
                {
                    lblCountdown.Text = $"Scan QR code: {_secondsLeft}s";
                }
            };
            _countdownTimer.Start();
        }

        public void StopCountdown()
        {
            _countdownTimer?.Stop();
            lblCountdown.Visible = false;
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            string coin = txtCoin.Text;
            string cardType = cmbCardType.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(coin) || string.IsNullOrEmpty(cardType))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin");
                return;
            }

            var service = new AutomationService();
            _ = service.Run(coin, cardType, StartCountdown, StopCountdown)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        this.Invoke(() => MessageBox.Show("Lỗi: " + t.Exception!.InnerException?.Message));
                });
        }
    }