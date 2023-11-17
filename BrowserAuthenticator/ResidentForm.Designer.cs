namespace BrowserAuthenticator;

partial class ResidentForm
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

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ResidentForm));
        NotifyIcon = new NotifyIcon(components);
        HideButton = new Button();
        HideTimer = new System.Windows.Forms.Timer(components);
        TestButton = new Button();
        SaveConfigTimer = new System.Windows.Forms.Timer(components);
        label3 = new Label();
        Counter = new Label();
        groupBox1 = new GroupBox();
        Lock = new CheckBox();
        CopyPasswordPic = new PictureBox();
        GeneratePassword = new LinkLabel();
        Password = new TextBox();
        label2 = new Label();
        CopyPortPic = new PictureBox();
        Port = new TextBox();
        label1 = new Label();
        ToolTip = new ToolTip(components);
        groupBox2 = new GroupBox();
        ManualCommit = new CheckBox();
        TestLogin = new ComboBox();
        label4 = new Label();
        groupBox1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)CopyPasswordPic).BeginInit();
        ((System.ComponentModel.ISupportInitialize)CopyPortPic).BeginInit();
        groupBox2.SuspendLayout();
        SuspendLayout();
        // 
        // NotifyIcon
        // 
        NotifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        NotifyIcon.BalloonTipText = "WebDAVCloud для Яндекс.Диск";
        NotifyIcon.BalloonTipTitle = "Резидентная часть для отображения на десктопе окна браузера для входа на Яндекс.Диск";
        NotifyIcon.Icon = (Icon)resources.GetObject("NotifyIcon.Icon");
        NotifyIcon.Text = "WebDAVCloud";
        NotifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
        // 
        // HideButton
        // 
        HideButton.Location = new Point(18, 492);
        HideButton.Name = "HideButton";
        HideButton.Size = new Size(630, 40);
        HideButton.TabIndex = 13;
        HideButton.Text = "Свернуть программу в System Tray";
        HideButton.UseVisualStyleBackColor = true;
        HideButton.Click += HideButton_Click;
        // 
        // HideTimer
        // 
        HideTimer.Tick += HideTimer_Tick;
        // 
        // TestButton
        // 
        TestButton.Location = new Point(531, 72);
        TestButton.Name = "TestButton";
        TestButton.Size = new Size(81, 40);
        TestButton.TabIndex = 11;
        TestButton.Text = "Test";
        TestButton.UseVisualStyleBackColor = true;
        TestButton.Click += TestButton_Click;
        // 
        // SaveConfigTimer
        // 
        SaveConfigTimer.Tick += SaveConfigTimer_Tick;
        // 
        // label3
        // 
        label3.AutoSize = true;
        label3.Location = new Point(53, 535);
        label3.Name = "label3";
        label3.Size = new Size(564, 30);
        label3.TabIndex = 14;
        label3.Text = "Для выхода используйте меню иконки в системном трее";
        // 
        // Counter
        // 
        Counter.AutoSize = true;
        Counter.Location = new Point(18, 448);
        Counter.Name = "Counter";
        Counter.Size = new Size(29, 30);
        Counter.TabIndex = 12;
        Counter.Text = "--";
        // 
        // groupBox1
        // 
        groupBox1.Controls.Add(Lock);
        groupBox1.Controls.Add(CopyPasswordPic);
        groupBox1.Controls.Add(GeneratePassword);
        groupBox1.Controls.Add(Password);
        groupBox1.Controls.Add(label2);
        groupBox1.Controls.Add(CopyPortPic);
        groupBox1.Controls.Add(Port);
        groupBox1.Controls.Add(label1);
        groupBox1.Location = new Point(18, 12);
        groupBox1.Name = "groupBox1";
        groupBox1.Size = new Size(630, 250);
        groupBox1.TabIndex = 1;
        groupBox1.TabStop = false;
        // 
        // Lock
        // 
        Lock.AutoSize = true;
        Lock.Checked = true;
        Lock.CheckState = CheckState.Checked;
        Lock.Cursor = Cursors.Hand;
        Lock.Location = new Point(15, 0);
        Lock.Name = "Lock";
        Lock.Size = new Size(188, 34);
        Lock.TabIndex = 0;
        Lock.Text = "Заблокировано";
        ToolTip.SetToolTip(Lock, "Для изменения порта или пароля снимите блокировку");
        Lock.UseVisualStyleBackColor = true;
        Lock.CheckedChanged += Lock_CheckedChanged;
        // 
        // CopyPasswordPic
        // 
        CopyPasswordPic.Cursor = Cursors.Hand;
        CopyPasswordPic.Image = (Image)resources.GetObject("CopyPasswordPic.Image");
        CopyPasswordPic.InitialImage = (Image)resources.GetObject("CopyPasswordPic.InitialImage");
        CopyPasswordPic.Location = new Point(15, 154);
        CopyPasswordPic.Name = "CopyPasswordPic";
        CopyPasswordPic.Size = new Size(35, 35);
        CopyPasswordPic.SizeMode = PictureBoxSizeMode.Zoom;
        CopyPasswordPic.TabIndex = 16;
        CopyPasswordPic.TabStop = false;
        CopyPasswordPic.Click += CopyPasswordPic_Click;
        // 
        // GeneratePassword
        // 
        GeneratePassword.AutoSize = true;
        GeneratePassword.Location = new Point(56, 192);
        GeneratePassword.Name = "GeneratePassword";
        GeneratePassword.Size = new Size(429, 30);
        GeneratePassword.TabIndex = 6;
        GeneratePassword.TabStop = true;
        GeneratePassword.Text = "Сгенерировать пароль в виде нового GUID";
        GeneratePassword.LinkClicked += GeneratePassword_LinkClicked;
        // 
        // Password
        // 
        Password.Enabled = false;
        Password.Location = new Point(56, 154);
        Password.Name = "Password";
        Password.Size = new Size(556, 35);
        Password.TabIndex = 5;
        // 
        // label2
        // 
        label2.AutoSize = true;
        label2.Location = new Point(56, 121);
        label2.Name = "label2";
        label2.Size = new Size(358, 30);
        label2.TabIndex = 4;
        label2.Text = "Пароль для входящего соединения:";
        // 
        // CopyPortPic
        // 
        CopyPortPic.Cursor = Cursors.Hand;
        CopyPortPic.Image = (Image)resources.GetObject("CopyPortPic.Image");
        CopyPortPic.InitialImage = (Image)resources.GetObject("CopyPortPic.InitialImage");
        CopyPortPic.Location = new Point(15, 75);
        CopyPortPic.Name = "CopyPortPic";
        CopyPortPic.Size = new Size(35, 35);
        CopyPortPic.SizeMode = PictureBoxSizeMode.Zoom;
        CopyPortPic.TabIndex = 12;
        CopyPortPic.TabStop = false;
        CopyPortPic.Click += CopyPortPic_Click;
        // 
        // Port
        // 
        Port.Enabled = false;
        Port.Location = new Point(56, 75);
        Port.Name = "Port";
        Port.Size = new Size(116, 35);
        Port.TabIndex = 3;
        Port.Text = "54321";
        Port.Validating += Port_Validating;
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.Location = new Point(56, 42);
        label1.Name = "label1";
        label1.Size = new Size(334, 30);
        label1.TabIndex = 2;
        label1.Text = "Порт для входящего соединения:";
        // 
        // groupBox2
        // 
        groupBox2.Controls.Add(ManualCommit);
        groupBox2.Controls.Add(TestLogin);
        groupBox2.Controls.Add(label4);
        groupBox2.Controls.Add(TestButton);
        groupBox2.Location = new Point(18, 268);
        groupBox2.Name = "groupBox2";
        groupBox2.Size = new Size(630, 170);
        groupBox2.TabIndex = 7;
        groupBox2.TabStop = false;
        groupBox2.Text = "Проверка";
        ToolTip.SetToolTip(groupBox2, "Задайте логин вида \nlogin@yandex.ru для старта со страницы Яндекс.Диска, \nlogin@mail.ru для старта со страницы Облако Mail.Ru, \n? для общей стартовой страницы.");
        // 
        // ManualCommit
        // 
        ManualCommit.AutoSize = true;
        ManualCommit.Location = new Point(15, 118);
        ManualCommit.Name = "ManualCommit";
        ManualCommit.Size = new Size(593, 34);
        ManualCommit.TabIndex = 10;
        ManualCommit.Text = "Только по кнопке «Готов» считать вход осуществлённым";
        ManualCommit.UseVisualStyleBackColor = true;
        // 
        // TestLogin
        // 
        TestLogin.FormattingEnabled = true;
        TestLogin.Location = new Point(15, 74);
        TestLogin.Name = "TestLogin";
        TestLogin.Size = new Size(470, 38);
        TestLogin.TabIndex = 9;
        TestLogin.Text = "?";
        // 
        // label4
        // 
        label4.AutoSize = true;
        label4.Location = new Point(15, 41);
        label4.Name = "label4";
        label4.Size = new Size(331, 30);
        label4.TabIndex = 8;
        label4.Text = "Login (email или учетная запись):";
        // 
        // ResidentForm
        // 
        AutoScaleDimensions = new SizeF(12F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(666, 576);
        Controls.Add(groupBox2);
        Controls.Add(groupBox1);
        Controls.Add(HideButton);
        Controls.Add(Counter);
        Controls.Add(label3);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ResidentForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "WebDavMailRuCloud Bowser Authenticator";
        FormClosing += ResidentForm_FormClosing;
        Load += ResidentForm_Load;
        Move += ResidentForm_Move;
        groupBox1.ResumeLayout(false);
        groupBox1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)CopyPasswordPic).EndInit();
        ((System.ComponentModel.ISupportInitialize)CopyPortPic).EndInit();
        groupBox2.ResumeLayout(false);
        groupBox2.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
    private NotifyIcon NotifyIcon;
    private Button HideButton;
    private System.Windows.Forms.Timer HideTimer;
    private Button TestButton;
    private System.Windows.Forms.Timer SaveConfigTimer;
    private Label label3;
    private Label Counter;
    private GroupBox groupBox1;
    private PictureBox CopyPasswordPic;
    private LinkLabel GeneratePassword;
    private TextBox Password;
    private Label label2;
    private PictureBox CopyPortPic;
    private TextBox Port;
    private Label label1;
    private CheckBox Lock;
    private ToolTip ToolTip;
    private GroupBox groupBox2;
    private Label label4;
    private ComboBox TestLogin;
    private CheckBox ManualCommit;
}