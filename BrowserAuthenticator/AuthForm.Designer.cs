namespace BrowserAuthenticator;

partial class AuthForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AuthForm));
        ShowWindowDelay = new System.Windows.Forms.Timer(components);
        NobodyHomeTimer = new System.Windows.Forms.Timer(components);
        panel1 = new Panel();
        DescriptionText = new TextBox();
        Deny = new Button();
        UseIt = new Button();
        Back = new Button();
        GotoYandex = new LinkLabel();
        GotoMail = new LinkLabel();
        WebViewPanel = new Panel();
        panel1.SuspendLayout();
        SuspendLayout();
        // 
        // DelayTimer
        // 
        ShowWindowDelay.Interval = 3000;
        ShowWindowDelay.Tick += DelayTimer_Tick;
        // 
        // NobodyHomeTimer
        // 
        NobodyHomeTimer.Interval = 3000;
        NobodyHomeTimer.Tick += NobodyHomeTimer_Tick;
        // 
        // panel1
        // 
        panel1.Controls.Add(DescriptionText);
        panel1.Controls.Add(Deny);
        panel1.Controls.Add(UseIt);
        panel1.Controls.Add(Back);
        panel1.Controls.Add(GotoYandex);
        panel1.Controls.Add(GotoMail);
        panel1.Dock = DockStyle.Top;
        panel1.Location = new Point(0, 0);
        panel1.Name = "panel1";
        panel1.Size = new Size(1654, 151);
        panel1.TabIndex = 0;
        // 
        // DescriptionText
        // 
        DescriptionText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        DescriptionText.BackColor = SystemColors.Control;
        DescriptionText.BorderStyle = BorderStyle.None;
        DescriptionText.Location = new Point(545, 14);
        DescriptionText.Multiline = true;
        DescriptionText.Name = "DescriptionText";
        DescriptionText.ReadOnly = true;
        DescriptionText.ScrollBars = ScrollBars.Vertical;
        DescriptionText.Size = new Size(1084, 131);
        DescriptionText.TabIndex = 6;
        DescriptionText.Text = "Требуется вход для учетной записи ...";
        // 
        // Deny
        // 
        Deny.Location = new Point(396, 12);
        Deny.Name = "Deny";
        Deny.Size = new Size(120, 60);
        Deny.TabIndex = 4;
        Deny.Text = "Отказать";
        Deny.UseVisualStyleBackColor = true;
        Deny.Click += Deny_Click;
        // 
        // UseIt
        // 
        UseIt.Enabled = false;
        UseIt.Location = new Point(396, 78);
        UseIt.Name = "UseIt";
        UseIt.Size = new Size(120, 60);
        UseIt.TabIndex = 5;
        UseIt.Text = "Готово";
        UseIt.UseVisualStyleBackColor = true;
        UseIt.Click += UseIt_Click;
        // 
        // Back
        // 
        Back.Location = new Point(30, 12);
        Back.Name = "Back";
        Back.Size = new Size(120, 77);
        Back.TabIndex = 0;
        Back.Text = "← Back";
        Back.UseVisualStyleBackColor = true;
        Back.Click += Back_Click;
        // 
        // GotoYandex
        // 
        GotoYandex.AutoSize = true;
        GotoYandex.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
        GotoYandex.Location = new Point(178, 51);
        GotoYandex.Name = "GotoYandex";
        GotoYandex.Size = new Size(192, 38);
        GotoYandex.TabIndex = 2;
        GotoYandex.TabStop = true;
        GotoYandex.Text = "disk.yandex.ru";
        GotoYandex.LinkClicked += GotoYandex_LinkClicked;
        // 
        // GotoMail
        // 
        GotoMail.AutoSize = true;
        GotoMail.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
        GotoMail.Location = new Point(178, 12);
        GotoMail.Name = "GotoMail";
        GotoMail.Size = new Size(175, 38);
        GotoMail.TabIndex = 1;
        GotoMail.TabStop = true;
        GotoMail.Text = "cloud.mail.ru";
        GotoMail.LinkClicked += GotoMail_LinkClicked;
        // 
        // WebViewPanel
        // 
        WebViewPanel.BorderStyle = BorderStyle.FixedSingle;
        WebViewPanel.Dock = DockStyle.Fill;
        WebViewPanel.Location = new Point(0, 151);
        WebViewPanel.Name = "WebViewPanel";
        WebViewPanel.Size = new Size(1654, 881);
        WebViewPanel.TabIndex = 1;
        WebViewPanel.TabStop = true;
        // 
        // AuthForm
        // 
        AutoScaleDimensions = new SizeF(12F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1654, 1032);
        Controls.Add(WebViewPanel);
        Controls.Add(panel1);
        Icon = (Icon)resources.GetObject("$this.Icon");
        Name = "AuthForm";
        Text = "Сервис WebDavMailRuCloud запрашивает вход в облако";
        FormClosed += AuthForm_FormClosed;
        Load += AuthForm_Load;
        panel1.ResumeLayout(false);
        panel1.PerformLayout();
        ResumeLayout(false);
    }

    #endregion
    private System.Windows.Forms.Timer ShowWindowDelay;
    private System.Windows.Forms.Timer NobodyHomeTimer;
    private Panel panel1;
    private LinkLabel GotoYandex;
    private LinkLabel GotoMail;
    private Button Back;
    private Panel WebViewPanel;
    private Button UseIt;
    private Button Deny;
    private TextBox DescriptionText;
}