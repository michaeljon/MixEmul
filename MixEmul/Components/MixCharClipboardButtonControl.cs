using System;
using System.Drawing;
using System.Windows.Forms;
using MixGui.Settings;

namespace MixGui.Components
{
	public class MixCharClipboardButtonControl : UserControl
	{
        readonly Button mDeltaButton;
        readonly Button mSigmaButton;
        readonly Button mPiButton;

        public MixCharClipboardButtonControl()
		{
			mDeltaButton = new Button();
			mSigmaButton = new Button();
			mPiButton = new Button();

			SuspendLayout();

			var font = GuiSettings.GetFont(GuiSettings.FixedWidth);
			mDeltaButton.FlatStyle = FlatStyle.Flat;
			mDeltaButton.Font = font;
			mDeltaButton.Location = new Point(0, 0);
			mDeltaButton.Name = "mDeltaButton";
			mDeltaButton.Size = new Size(21, 21);
			mDeltaButton.TabIndex = 0;
			mDeltaButton.Text = "Δ";
			mDeltaButton.Click += ClipboardButton_Click;

			mSigmaButton.FlatStyle = FlatStyle.Flat;
			mSigmaButton.Font = font;
			mSigmaButton.Location = new Point(mDeltaButton.Right - 1, 0);
			mSigmaButton.Name = "mSigmaButton";
			mSigmaButton.Size = mDeltaButton.Size;
			mSigmaButton.TabIndex = 1;
			mSigmaButton.Text = "Σ";
			mSigmaButton.Click += ClipboardButton_Click;

			mPiButton.FlatStyle = FlatStyle.Flat;
			mPiButton.Font = font;
			mPiButton.Location = new Point(mSigmaButton.Right - 1, 0);
			mPiButton.Name = "mPiButton";
			mPiButton.Size = mDeltaButton.Size;
			mPiButton.TabIndex = 2;
			mPiButton.Text = "Π";
			mPiButton.Click += ClipboardButton_Click;

			Controls.Add(mDeltaButton);
			Controls.Add(mSigmaButton);
			Controls.Add(mPiButton);
			Size = new Size(mPiButton.Right, mPiButton.Height);
			Name = "MemoryEditor";
			ResumeLayout(false);
		}

        void ClipboardButton_Click(object sender, EventArgs e) => Clipboard.SetDataObject(((Button)sender).Text);

        public void UpdateLayout()
		{
			var font = GuiSettings.GetFont(GuiSettings.FixedWidth);
			mDeltaButton.Font = font;
			mSigmaButton.Font = font;
			mPiButton.Font = font;
		}
	}
}
