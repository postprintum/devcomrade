// Copyright (C) 2020 by Postprintum Pty Ltd (https://www.postprintum.com),
// which licenses this file to you under Apache License 2.0,
// see the LICENSE file in the project root for more information. 
// Author: Andrew Nosenko (@noseratio)

#nullable enable

using AppLogic.Helpers;
using System.Drawing;
using System.Windows.Forms;

namespace AppLogic.Presenter
{
    [System.ComponentModel.DesignerCategory("")]
    internal class Notepad : Form
    {
        public TextBox TextBox { get; }

        public Notepad()
        {
            this.TextBox = new TextBox()
            {
                Multiline = true,
                AcceptsReturn = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Both,
                Font = new Font(
                    familyName: "Consolas", 
                    9,
                    FontStyle.Regular,
                    GraphicsUnit.Point)
            };

            this.Text = $"{Application.ProductName} Notepad";
            this.ShowInTaskbar = false;
            this.Padding = new Padding(3);
            this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Diagnostics.GetExecutablePath());

            var workingArea = Screen.PrimaryScreen.WorkingArea;
            this.StartPosition = FormStartPosition.Manual;
            this.Width = workingArea.Width / 2;
            this.Height = workingArea.Height / 2;
            this.Left = workingArea.Left + (workingArea.Width - this.Width) / 2;
            this.Top = workingArea.Top + (workingArea.Height - this.Height) / 2;

            this.Controls.Add(this.TextBox);

            this.GotFocus += (s, e) => this.TextBox.Focus();

            this.KeyPreview = true;

            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Hide();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
