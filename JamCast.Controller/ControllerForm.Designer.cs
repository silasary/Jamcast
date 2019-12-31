namespace JamCast.Controller
{
    partial class ControllerForm
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
            this.controllerStatus = new System.Windows.Forms.TextBox();
            this.logMessages = new System.Windows.Forms.TextBox();
            this.wsLogs = new System.Windows.Forms.TextBox();
            this.excludeBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // controllerStatus
            // 
            this.controllerStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.controllerStatus.Location = new System.Drawing.Point(0, 0);
            this.controllerStatus.Multiline = true;
            this.controllerStatus.Name = "controllerStatus";
            this.controllerStatus.ReadOnly = true;
            this.controllerStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.controllerStatus.Size = new System.Drawing.Size(568, 239);
            this.controllerStatus.TabIndex = 0;
            // 
            // logMessages
            // 
            this.logMessages.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logMessages.Location = new System.Drawing.Point(0, 245);
            this.logMessages.Multiline = true;
            this.logMessages.Name = "logMessages";
            this.logMessages.ReadOnly = true;
            this.logMessages.Size = new System.Drawing.Size(568, 395);
            this.logMessages.TabIndex = 1;
            // 
            // wsLogs
            // 
            this.wsLogs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.wsLogs.Location = new System.Drawing.Point(574, 245);
            this.wsLogs.Multiline = true;
            this.wsLogs.Name = "wsLogs";
            this.wsLogs.ReadOnly = true;
            this.wsLogs.Size = new System.Drawing.Size(410, 395);
            this.wsLogs.TabIndex = 2;
            // 
            // excludeBox
            // 
            this.excludeBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.excludeBox.Location = new System.Drawing.Point(574, 0);
            this.excludeBox.Multiline = true;
            this.excludeBox.Name = "excludeBox";
            this.excludeBox.Size = new System.Drawing.Size(410, 239);
            this.excludeBox.TabIndex = 3;
            this.excludeBox.TextChanged += new System.EventHandler(this.excludeBox_TextChanged);
            // 
            // ControllerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 640);
            this.Controls.Add(this.excludeBox);
            this.Controls.Add(this.wsLogs);
            this.Controls.Add(this.logMessages);
            this.Controls.Add(this.controllerStatus);
            this.DoubleBuffered = true;
            this.Name = "ControllerForm";
            this.Text = "JamCast Controller";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox controllerStatus;
        private System.Windows.Forms.TextBox logMessages;
        private System.Windows.Forms.TextBox wsLogs;
        private System.Windows.Forms.TextBox excludeBox;
    }
}

