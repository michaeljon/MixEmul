namespace MixGui.Components
{
	using System;
	using System.Drawing;
	using System.Windows.Forms;
	using MixGui.Events;
	using MixGui.Settings;
	using MixGui.Utils;
	using MixLib.Type;

	public class DeviceMixByteCollectionEditor : UserControl, IMixByteCollectionEditor, INavigableControl
	{
		private Label mMixByteCollectionIndexLabel;
		private MixByteCollectionCharTextBox mCharTextBox;
		private int mMixByteCollectionIndex;
		private IMixByteCollection mDeviceMixByteCollection;
		private int mIndexCharCount = 2;

		public event KeyEventHandler NavigationKeyDown;
		public event MixByteCollectionEditorValueChangedEventHandler ValueChanged;

		public DeviceMixByteCollectionEditor()
			: this(null)
		{
		}

		public DeviceMixByteCollectionEditor(IMixByteCollection mixByteCollection)
		{
			mDeviceMixByteCollection = mixByteCollection;
			if (mDeviceMixByteCollection == null)
			{
				mDeviceMixByteCollection = new MixByteCollection(FullWord.ByteCount);
			}

			mCharTextBox = new MixByteCollectionCharTextBox(mDeviceMixByteCollection);
			mMixByteCollectionIndexLabel = new Label();
			initializeComponent();
		}

		public bool Focus(FieldTypes? field, int? index)
		{
			return mCharTextBox.FocusWithIndex(index);
		}

		private void initializeComponent()
		{
			base.SuspendLayout();

			mMixByteCollectionIndexLabel.Font = GuiSettings.GetFont(GuiSettings.FixedWidth);
			mMixByteCollectionIndexLabel.ForeColor = GuiSettings.GetColor(GuiSettings.AddressText);
			mMixByteCollectionIndexLabel.Location = new Point(0, 0);
			mMixByteCollectionIndexLabel.Name = "mMixByteCollectionIndexLabel";
			mMixByteCollectionIndexLabel.Size = new Size(30, 13);
			mMixByteCollectionIndexLabel.AutoSize = true;
			mMixByteCollectionIndexLabel.TabIndex = 0;
			mMixByteCollectionIndexLabel.Text = "00:";

			mCharTextBox.Location = new Point(mMixByteCollectionIndexLabel.Right, 0);
			mCharTextBox.Name = "mCharTextBox";
			mCharTextBox.TabIndex = 1;
			mCharTextBox.ValueChanged += new MixByteCollectionEditorValueChangedEventHandler(mMixByteCollectionEditor_ValueChanged);
			mCharTextBox.NavigationKeyDown += new KeyEventHandler(keyDown);
			mCharTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			mCharTextBox.BorderStyle = BorderStyle.None;
			mCharTextBox.Height = 13;

			base.Controls.Add(mMixByteCollectionIndexLabel);
			base.Controls.Add(mCharTextBox);
			base.Name = "DeviceMixByteCollectionEditor";
			base.Size = new Size(mCharTextBox.Right, mCharTextBox.Height);
			base.KeyDown += new KeyEventHandler(keyDown);
			base.ResumeLayout(false);
		}

		private void keyDown(object sender, KeyEventArgs e)
		{
			if (e.Modifiers != Keys.None)
			{
				return;
			}

			switch (e.KeyCode)
			{
				case Keys.Prior:
				case Keys.Next:
				case Keys.Up:
				case Keys.Down:
					if (NavigationKeyDown != null)
					{
						NavigationKeyDown(this, e);
					}
					break;
			}
		}

		private void mMixByteCollectionEditor_ValueChanged(IMixByteCollectionEditor sender, MixByteCollectionEditorValueChangedEventArgs args)
		{
			OnValueChanged(args);
		}

		protected virtual void OnValueChanged(MixByteCollectionEditorValueChangedEventArgs args)
		{
			if (ValueChanged != null)
			{
				ValueChanged(this, args);
			}
		}

		public new void Update()
		{
			mCharTextBox.Update();
			base.Update();
		}

		public void UpdateLayout()
		{
			mMixByteCollectionIndexLabel.Font = GuiSettings.GetFont(GuiSettings.FixedWidth);
			mMixByteCollectionIndexLabel.ForeColor = GuiSettings.GetColor(GuiSettings.AddressText);
			mCharTextBox.UpdateLayout();
		}

		public int MixByteCollectionIndex
		{
			get
			{
				return mMixByteCollectionIndex;
			}
			set
			{
				if (value < 0)
				{
					value = 0;
				}

				mMixByteCollectionIndex = value;
				setIndexLabelText();
			}
		}

		private void setIndexLabelText()
		{
			mMixByteCollectionIndexLabel.Text = mMixByteCollectionIndex.ToString("D" + mIndexCharCount) + ":";
		}

		public int IndexCharCount
		{
			get
			{
				return mIndexCharCount;
			}
			set
			{
				if (mIndexCharCount == value)
				{
					return;
				}

				mIndexCharCount = value;
				setIndexLabelText();

				int oldCharBoxLeft = mCharTextBox.Left;
				mCharTextBox.Left = mMixByteCollectionIndexLabel.Right + 3;
				mCharTextBox.Width += oldCharBoxLeft - mCharTextBox.Left;
			}
		}

		public IMixByteCollection DeviceMixByteCollection
		{
			get
			{
				return mDeviceMixByteCollection;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value", "DeviceMixByteCollection may not be set to null");
				}

				mDeviceMixByteCollection = value;
				mCharTextBox.MixByteCollectionValue = mDeviceMixByteCollection;
				mCharTextBox.Select(0, 0);
			}
		}

		public bool ReadOnly
		{
			get
			{
				return mCharTextBox.ReadOnly;
			}
			set
			{
				mCharTextBox.ReadOnly = value;
			}
		}

		public IMixByteCollection MixByteCollectionValue
		{
			get
			{
				return DeviceMixByteCollection;
			}
			set
			{
				DeviceMixByteCollection = value;
			}
		}

		public Control EditorControl
		{
			get
			{
				return this;
			}
		}

		public FieldTypes? FocusedField
		{
			get
			{
				return mCharTextBox.Focused ? FieldTypes.Chars : (FieldTypes?)null;
			}
		}

		public int? CaretIndex
		{
			get
			{
				return mCharTextBox.Focused ? mCharTextBox.SelectionStart + mCharTextBox.SelectionLength : (int?)null;
			}
		}

		public void Select(int start, int length)
		{
			mCharTextBox.Select(start, length);
		}
	}
}