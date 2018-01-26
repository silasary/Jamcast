namespace Controller
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
            this.controllerStatus.Size = new System.Drawing.Size(519, 239);
            this.controllerStatus.TabIndex = 0;
            // 
            // logMessages
            // 
            this.logMessages.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.logMessages.Location = new System.Drawing.Point(0, 245);
            this.logMessages.Multiline = true;
            this.logMessages.Name = "logMessages";
            this.logMessages.ReadOnly = true;
            this.logMessages.Size = new System.Drawing.Size(519, 156);
            this.logMessages.TabIndex = 1;
            // 
            // ControllerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(519, 401);
            this.Controls.Add(this.logMessages);
            this.Controls.Add(this.controllerStatus);
            this.Name = "ControllerForm";
            this.Text = "JamCast Controller";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox controllerStatus;
        private System.Windows.Forms.TextBox logMessages;
    }
}

