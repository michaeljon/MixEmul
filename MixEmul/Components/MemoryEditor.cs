using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using MixGui.Events;
using MixGui.Properties;
using MixGui.Settings;
using MixGui.Utils;
using MixLib;
using MixLib.Misc;
using MixLib.Type;

namespace MixGui.Components
{
	public class MemoryEditor : UserControl, IBreakpointManager
	{
		private VScrollBar mAddressScrollBar;
		private SortedList mBreakpoints;
		private Label mFirstAddressLabel;
		private Panel mFirstAddressPanel;
		private LongValueTextBox mFirstAddressTextBox;
		private int mMarkedAddress;
		private IMemory mMemory;
		private Label mNoMemoryLabel;
		private bool mReadOnly;
		private Label mSetClipboardLabel;
		private ToolTip mToolTip;
		private WordEditorList mWordEditorList;
		private IndexedAddressCalculatorCallback mIndexedAddressCalculatorCallback = null;
		private SymbolCollection mSymbols;
		private MixCharClipboardButtonControl mMixCharButtons;
		private Button mExportButton;
		private Button mUpButton;
		private Button mDownButton;
		private LinkedItemsSelectorControl<EditorListViewInfo> mAddressHistorySelector;
		private SaveFileDialog mSaveExportFileDialog = null;
		private int? mPrevDataIndex = null;
		private int? mNextDataIndex = null;
		private long[] mProfilingMaxCounts;

		public event AddressSelectedHandler AddressSelected;

		public MemoryEditor()
			: this(null)
		{
		}

		public MemoryEditor(IMemory memory)
		{
			mReadOnly = false;
			mMarkedAddress = -1;

			mProfilingMaxCounts = new long[Enum.GetValues(typeof(GuiSettings.ProfilingInfoType)).Length];

			mBreakpoints = SortedList.Synchronized(new SortedList());

			mNoMemoryLabel = new Label();
			mNoMemoryLabel.Location = new Point(0, 0);
			mNoMemoryLabel.Name = "mNoMemoryLabel";
			mNoMemoryLabel.Size = new Size(120, 16);
			mNoMemoryLabel.TabIndex = 0;
			mNoMemoryLabel.Text = "No memory connected";
			mNoMemoryLabel.TextAlign = ContentAlignment.MiddleCenter;

			Memory = memory;
		}

		private void addressSelected(object sender, AddressSelectedEventArgs args)
		{
			MakeAddressVisible(args.SelectedAddress);
		}

		private void addressDoubleClick(object sender, EventArgs e)
		{
			OnAddressSelected(new AddressSelectedEventArgs(((MemoryWordEditor)sender).MemoryWord.Index));
		}

		public ToolTip ToolTip
		{
			get
			{
				return mToolTip;
			}
			set
			{
				mToolTip = value;

				if (mWordEditorList != null)
				{
					foreach (MemoryWordEditor editor in mWordEditorList)
					{
						editor.ToolTip = mToolTip;
					}
				}
			}
		}

		private long getMaxProfilingCount(GuiSettings.ProfilingInfoType infoType)
		{
			return mProfilingMaxCounts[(int)infoType];
		}

		private IWordEditor createWordEditor(int address)
		{
			MemoryWordEditor editor = new MemoryWordEditor(address >= mMemory.MinWordIndex ? mMemory[address] : (IMemoryFullWord)new MemoryFullWord(address));
			editor.GetMaxProfilingCount = getMaxProfilingCount;
			editor.MemoryMinIndex = mMemory.MinWordIndex;
			editor.MemoryMaxIndex = mMemory.MaxWordIndex;
			editor.IndexedAddressCalculatorCallback = mIndexedAddressCalculatorCallback;
			editor.BreakPointChecked = mBreakpoints.Contains(address);
			editor.Marked = mMarkedAddress == address;
			editor.ToolTip = mToolTip;
			editor.Symbols = mSymbols;
			editor.BreakpointCheckedChanged += new EventHandler(breakPointCheckedChanged);
			editor.AddressDoubleClick += new EventHandler(addressDoubleClick);
			editor.AddressSelected += new AddressSelectedHandler(addressSelected);

			return editor;
		}

		private void loadWordEditor(IWordEditor editor, int address)
		{
			MemoryWordEditor memoryEditor = (MemoryWordEditor)editor;

			if (address == memoryEditor.MemoryWord.Index && address >= mMemory.MinWordIndex && memoryEditor.MemoryWord.Equals(mMemory[address]))
			{
				memoryEditor.Update();
			}
			else
			{
				memoryEditor.MemoryWord = address >= mMemory.MinWordIndex ? mMemory[address] : (IMemoryFullWord)new MemoryFullWord(address);
			}

			memoryEditor.BreakPointChecked = mBreakpoints.Contains(address);
			memoryEditor.Marked = mMarkedAddress == address;
		}

		private void breakPointCheckedChanged(object sender, EventArgs e)
		{
			if (!mWordEditorList.IsReloading)
			{
				MemoryWordEditor editor = (MemoryWordEditor)sender;
				int memoryAddress = editor.MemoryWord.Index;

				if (editor.BreakPointChecked)
				{
					mBreakpoints.Add(memoryAddress, memoryAddress);
				}
				else
				{
					mBreakpoints.Remove(memoryAddress);
				}
			}
		}

		public void ClearBreakpoints()
		{
			mBreakpoints.Clear();
			foreach (MemoryWordEditor editor in mWordEditorList)
			{
				editor.BreakPointChecked = false;
			}
		}

		private void initializeComponent()
		{
			base.SuspendLayout();
			base.Controls.Clear();

			if (mMemory == null)
			{
				base.Controls.Add(mNoMemoryLabel);
				mNoMemoryLabel.Size = base.Size;

				base.ResumeLayout(false);
			}
			else
			{
				mFirstAddressPanel = new Panel();
				mFirstAddressLabel = new Label();
				mFirstAddressTextBox = new LongValueTextBox(true, mMemory.MinWordIndex, mMemory.MaxWordIndex);
				mSetClipboardLabel = new Label();
				mAddressScrollBar = new VScrollBar();
				mAddressHistorySelector = new LinkedItemsSelectorControl<EditorListViewInfo>();
				mAddressHistorySelector.SetCurrentItemCallback(GetCurrentListViewInfo);
				mMixCharButtons = new MixCharClipboardButtonControl();
				mExportButton = new Button();
				mUpButton = new Button();
				mDownButton = new Button();

				mFirstAddressPanel.SuspendLayout();
				mFirstAddressLabel.Location = new Point(0, 0);
				mFirstAddressLabel.Name = "mFirstAddressLabel";
				mFirstAddressLabel.Size = new Size(110, 21);
				mFirstAddressLabel.TabIndex = 0;
				mFirstAddressLabel.Text = "First visible address:";
				mFirstAddressLabel.TextAlign = ContentAlignment.MiddleLeft;

				mFirstAddressTextBox.Location = new Point(mFirstAddressLabel.Width, 0);
				mFirstAddressTextBox.Name = "mFirstAddressTextBox";
				mFirstAddressTextBox.Size = new Size(32, 20);
				mFirstAddressTextBox.TabIndex = 1;
				mFirstAddressTextBox.ClearZero = false;
				mFirstAddressTextBox.ValueChanged += mFirstAddressTextBox_ValueChanged;

				mAddressHistorySelector.Location = new Point(mFirstAddressTextBox.Right + 4, 0);
				mAddressHistorySelector.Name = "mAddressHistorySelector";
				mAddressHistorySelector.TabIndex = 2;
				mAddressHistorySelector.ItemSelected += mAddressHistorySelector_ItemSelected;

				mSetClipboardLabel.Location = new Point(mAddressHistorySelector.Right + 16, 0);
				mSetClipboardLabel.Name = "mSetClipboardLabel";
				mSetClipboardLabel.Size = new Size(88, mFirstAddressLabel.Height);
				mSetClipboardLabel.TabIndex = 3;
				mSetClipboardLabel.Text = "Set clipboard to:";
				mSetClipboardLabel.TextAlign = ContentAlignment.MiddleLeft;

				mMixCharButtons.Location = new Point(mSetClipboardLabel.Right, 0);
				mMixCharButtons.Name = "mMixCharButtons";
				mMixCharButtons.TabIndex = 4;

				mExportButton.Location = new Point(mMixCharButtons.Right + 16, 0);
				mExportButton.Name = "mExportButton";
				mExportButton.Size = new System.Drawing.Size(62, 21);
				mExportButton.TabIndex = 5;
				mExportButton.Text = "&Export...";
				mExportButton.Click += this.mExportButton_Click;

				mDownButton.Visible = mMemory is Memory;
				mDownButton.Enabled = false;
				mDownButton.FlatStyle = FlatStyle.Flat;
				mDownButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
				mDownButton.Image = Resources.Symbols_Down_16xLG;
				mDownButton.Name = "mDownButton";
				mDownButton.Size = new Size(21, 21);
				mDownButton.Location = new Point(Width - mDownButton.Width, 0);
				mDownButton.TabIndex = 7;
				mDownButton.Click += mDownButton_Click;

				mUpButton.Visible = mMemory is Memory;
				mUpButton.Enabled = false;
				mUpButton.FlatStyle = FlatStyle.Flat;
				mUpButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
				mUpButton.Image = Resources.Symbols_Up_16xLG___kopie;
				mUpButton.Name = "mUpButton";
				mUpButton.Size = new Size(21, 21);
				mUpButton.Location = new Point(mDownButton.Left - mUpButton.Width, 0);
				mUpButton.TabIndex = 6;
				mUpButton.Click += mUpButton_Click;

				mFirstAddressPanel.Controls.Add(mFirstAddressLabel);
				mFirstAddressPanel.Controls.Add(mFirstAddressTextBox);
				mFirstAddressPanel.Controls.Add(mAddressHistorySelector);
				mFirstAddressPanel.Controls.Add(mSetClipboardLabel);
				mFirstAddressPanel.Controls.Add(mMixCharButtons);
				mFirstAddressPanel.Controls.Add(mExportButton);
				mFirstAddressPanel.Controls.Add(mUpButton);
				mFirstAddressPanel.Controls.Add(mDownButton);
				mFirstAddressPanel.Dock = DockStyle.Top;
				mFirstAddressPanel.Name = "mFirstAddressPanel";
				mFirstAddressPanel.TabIndex = 0;
				mFirstAddressPanel.Size = new Size(Width, mFirstAddressTextBox.Height + 2);

				mWordEditorList = new WordEditorList(mMemory.MinWordIndex, mMemory.MaxWordIndex, createWordEditor, loadWordEditor);
				mWordEditorList.Dock = DockStyle.Fill;
				mWordEditorList.ReadOnly = mReadOnly;
				mWordEditorList.FirstVisibleIndexChanged += mWordEditorList_FirstVisibleIndexChanged;

				base.Controls.Add(mWordEditorList);
				base.Controls.Add(mFirstAddressPanel);
				base.Name = "MemoryEditor";
				base.SizeChanged += MemoryEditor_SizeChanged;

				mFirstAddressPanel.ResumeLayout(false);
				base.ResumeLayout(false);

				setUpDownButtonStates(navigationDirection.None);
			}
		}

		void MemoryEditor_SizeChanged(object sender, EventArgs e)
		{
			setUpDownButtonStates(navigationDirection.None);
		}

		private void setUpButtonState(Memory memory)
		{
			mPrevDataIndex = null;

			if (mWordEditorList.FirstVisibleIndex != mMemory.MinWordIndex)
			{
				int firstIndex = Math.Max(mMemory.MinWordIndex, mWordEditorList.FirstVisibleIndex - mWordEditorList.VisibleEditorCount);

				for (int index = firstIndex; index < mWordEditorList.FirstVisibleIndex; index++)
				{
					if (memory.HasContents(index))
					{
						mPrevDataIndex = index;
						break;
					}
				}

				if (mPrevDataIndex == null)
				{
					int? lastPreviousDataIndex = memory.LastAddressWithContentsBefore(firstIndex);
					if (lastPreviousDataIndex != null)
					{
						mPrevDataIndex = lastPreviousDataIndex.Value;

						for (int index = lastPreviousDataIndex.Value - 1; index >= Math.Max(mMemory.MinWordIndex, lastPreviousDataIndex.Value - mWordEditorList.VisibleEditorCount + 1); index--)
						{
							if (memory.HasContents(index))
							{
								mPrevDataIndex = index;
							}
						}
					}
				}
			}

			mUpButton.Enabled = mPrevDataIndex.HasValue;
		}

		private void setDownButtonState(Memory memory)
		{
			mNextDataIndex = null;

			if (mWordEditorList.FirstVisibleIndex + mWordEditorList.VisibleEditorCount <= mMemory.MaxWordIndex)
			{
				int lastIndex = Math.Min(mMemory.MaxWordIndex, mWordEditorList.FirstVisibleIndex + (2 * mWordEditorList.VisibleEditorCount) - 1);

				for (int index = mWordEditorList.FirstVisibleIndex + mWordEditorList.VisibleEditorCount; index <= lastIndex; index++)
				{
					if (memory.HasContents(index))
					{
						mNextDataIndex = index;
						break;
					}
				}

				if (mNextDataIndex == null)
				{
					mNextDataIndex = memory.FirstAddressWithContentsAfter(lastIndex);
				}
			}

			mDownButton.Enabled = mNextDataIndex.HasValue;
		}

		private void setUpDownButtonStates(navigationDirection direction)
		{
			Memory memory = mMemory as Memory;

			if (memory == null || mWordEditorList == null)
			{
				return;
			}

			if (direction != navigationDirection.Up || (mPrevDataIndex.HasValue && mPrevDataIndex.Value >= mWordEditorList.FirstVisibleIndex))
			{
				setUpButtonState(memory);
			}

			if (direction != navigationDirection.Down || (mNextDataIndex.HasValue && mNextDataIndex.Value <= mWordEditorList.FirstVisibleIndex + mWordEditorList.VisibleEditorCount))
			{
				setDownButtonState(memory);
			}

		}

		public EditorListViewInfo GetCurrentListViewInfo()
		{
			EditorListViewInfo viewInfo = new EditorListViewInfo() { FirstVisibleIndex = mWordEditorList.FirstVisibleIndex };
			int selectedEditorIndex = mWordEditorList.ActiveEditorIndex;
			if (selectedEditorIndex >= 0)
			{
				viewInfo.SelectedIndex = viewInfo.FirstVisibleIndex + selectedEditorIndex;
				viewInfo.FocusedField = mWordEditorList[selectedEditorIndex].FocusedField;
			}

			return viewInfo;
		}

		private void mAddressHistorySelector_ItemSelected(object sender, ItemSelectedEventArgs<EditorListViewInfo> e)
		{
			EditorListViewInfo viewInfo = e.SelectedItem;

			if (viewInfo.SelectedIndex.HasValue)
			{
				int selectedIndex = viewInfo.SelectedIndex.Value;

				if (selectedIndex < viewInfo.FirstVisibleIndex + mWordEditorList.VisibleEditorCount)
				{
					mWordEditorList.FirstVisibleIndex = viewInfo.FirstVisibleIndex;
					mWordEditorList[viewInfo.SelectedIndex.Value - viewInfo.FirstVisibleIndex].Focus(viewInfo.FocusedField, null);
				}
				else
				{
					mWordEditorList.MakeIndexVisible(selectedIndex);
				}
			}
			else
			{
				mWordEditorList.FirstVisibleIndex = viewInfo.FirstVisibleIndex;
			}
		}

		void mWordEditorList_FirstVisibleIndexChanged(EditorList<IWordEditor> sender, WordEditorList.FirstVisibleIndexChangedEventArgs args)
		{
			if (args.FirstVisibleIndex < mFirstAddressTextBox.LongValue)
			{
				setUpDownButtonStates(navigationDirection.Up);
			}
			else if (args.FirstVisibleIndex > mFirstAddressTextBox.LongValue)
			{
				setUpDownButtonStates(navigationDirection.Down);
			}

			mFirstAddressTextBox.LongValue = args.FirstVisibleIndex;
		}

		public bool IsBreakpointSet(int address)
		{
			return mBreakpoints.Contains(address);
		}

		public void MakeAddressVisible(int address)
		{
			MakeAddressVisible(address, true);
		}

		public void MakeAddressVisible(int address, bool trackChange)
		{
			if (trackChange)
			{
				EditorListViewInfo oldViewInfo = GetCurrentListViewInfo();

				mWordEditorList.MakeIndexVisible(address);

				EditorListViewInfo newViewInfo = new EditorListViewInfo() { FirstVisibleIndex = mWordEditorList.FirstVisibleIndex, SelectedIndex = address };
				int selectedEditorIndex = mWordEditorList.ActiveEditorIndex;
				if (selectedEditorIndex >= 0)
				{
					newViewInfo.FocusedField = mWordEditorList[selectedEditorIndex].FocusedField;
				}

				mAddressHistorySelector.AddItem(oldViewInfo, newViewInfo);
			}
			else
			{
				mWordEditorList.MakeIndexVisible(address);
			}
		}

		private void mFirstAddressTextBox_ValueChanged(LongValueTextBox source, LongValueTextBox.ValueChangedEventArgs args)
		{
			EditorListViewInfo oldViewInfo = new EditorListViewInfo() { FirstVisibleIndex = mWordEditorList.FirstVisibleIndex };
			int selectedEditorIndex = mWordEditorList.ActiveEditorIndex;
			if (selectedEditorIndex >= 0)
			{
				oldViewInfo.SelectedIndex = oldViewInfo.FirstVisibleIndex + selectedEditorIndex;
				oldViewInfo.FocusedField = mWordEditorList[selectedEditorIndex].FocusedField;
			}

			mWordEditorList.FirstVisibleIndex = (int)args.NewValue;

			mAddressHistorySelector.AddItem(oldViewInfo, new EditorListViewInfo() { FirstVisibleIndex = mWordEditorList.FirstVisibleIndex });
		}

		protected virtual void OnAddressSelected(AddressSelectedEventArgs args)
		{
			if (AddressSelected != null)
			{
				AddressSelected(this, args);
			}
		}

		public IndexedAddressCalculatorCallback IndexedAddressCalculatorCallback
		{
			get
			{
				return mIndexedAddressCalculatorCallback;
			}
			set
			{
				mIndexedAddressCalculatorCallback = value;

				if (mWordEditorList != null)
				{
					foreach (MemoryWordEditor wordEditor in mWordEditorList)
					{
						wordEditor.IndexedAddressCalculatorCallback = mIndexedAddressCalculatorCallback;
					}
				}
			}
		}

		public bool ResizeInProgress
		{
			get
			{
				return mWordEditorList != null ? mWordEditorList.ResizeInProgress : false;
			}

			set
			{
				if (mWordEditorList != null)
				{
					mWordEditorList.ResizeInProgress = value;
				}
			}
		}

		public new void Update()
		{
			mProfilingMaxCounts[(int)GuiSettings.ProfilingInfoType.Execution] = mMemory.MaxProfilingExecutionCount;
			mProfilingMaxCounts[(int)GuiSettings.ProfilingInfoType.Tick] = mMemory.MaxProfilingTickCount;
			mWordEditorList.Update();
			setUpDownButtonStates(navigationDirection.None);

			base.Update();
		}

		public void UpdateLayout()
		{
			base.SuspendLayout();

			mFirstAddressTextBox.UpdateLayout();

			mMixCharButtons.UpdateLayout();

			mWordEditorList.UpdateLayout();

			base.ResumeLayout();
		}

		public ICollection Breakpoints
		{
			get
			{
				return mBreakpoints.Values;
			}
		}

		public int FirstVisibleAddress
		{
			get
			{
				return mWordEditorList != null ? mWordEditorList.FirstVisibleIndex : 0;
			}
			set
			{
				if (mWordEditorList != null)
				{
					mWordEditorList.FirstVisibleIndex = value;
				}
			}
		}

		public void FindMatch(SearchParameters options)
		{
			int activeEditorIndex = mWordEditorList.ActiveEditorIndex;
			IWordEditor activeEditor = activeEditorIndex >= 0 ? mWordEditorList[activeEditorIndex] : null;

			if (activeEditor != null)
			{
				options.SearchFromWordIndex = ((IMemoryFullWord)activeEditor.WordValue).Index;
				options.SearchFromField = activeEditor.FocusedField ?? FieldTypes.None;
				options.SearchFromFieldIndex = options.SearchFromField != FieldTypes.None ? activeEditor.CaretIndex ?? 0 : 0;
			}
			else
			{
				if (mWordEditorList.FirstVisibleIndex > options.SearchFromWordIndex || mWordEditorList.FirstVisibleIndex + mWordEditorList.VisibleEditorCount <= options.SearchFromWordIndex)
				{
					options.SearchFromWordIndex = mWordEditorList.FirstVisibleIndex;
				}
				options.SearchFromField = FieldTypes.None;
				options.SearchFromFieldIndex = 0;
			}

			SearchResult result = mMemory.FindMatch(options);

			if (result != null)
			{
				MakeAddressVisible(result.WordIndex);
				activeEditor = mWordEditorList[result.WordIndex - mWordEditorList.FirstVisibleIndex];
				activeEditor.Focus(result.Field, result.FieldIndex);
				activeEditor.Select(result.FieldIndex, options.SearchText.Length);
			}
			else
			{
				MessageBox.Show(this, string.Format("Search text \"{0}\" could not be found.", options.SearchText), "Text not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		public int MarkedAddress
		{
			get
			{
				return mMarkedAddress;
			}
			set
			{
				if (mMarkedAddress != value)
				{
					int firstVisibleIndex = mWordEditorList.FirstVisibleIndex;

					if (mMarkedAddress >= firstVisibleIndex && mMarkedAddress < firstVisibleIndex + mWordEditorList.EditorCount)
					{
						((MemoryWordEditor)mWordEditorList[mMarkedAddress - firstVisibleIndex]).Marked = false;
					}

					mMarkedAddress = value;

					if (mMarkedAddress >= firstVisibleIndex && mMarkedAddress < firstVisibleIndex + mWordEditorList.EditorCount)
					{
						((MemoryWordEditor)mWordEditorList[mMarkedAddress - firstVisibleIndex]).Marked = true;
					}
				}
			}
		}

		public IMemory Memory
		{
			get
			{
				return mMemory;
			}
			set
			{
				if (mMemory == null && value != null)
				{
					mMemory = value;
					initializeComponent();
					mProfilingMaxCounts[(int)GuiSettings.ProfilingInfoType.Execution] = mMemory.MaxProfilingExecutionCount;
					mProfilingMaxCounts[(int)GuiSettings.ProfilingInfoType.Tick] = mMemory.MaxProfilingTickCount;
				}
			}
		}

		public bool ReadOnly
		{
			get
			{
				return mReadOnly;
			}
			set
			{
				if (mReadOnly != value)
				{
					mReadOnly = value;
					if (mWordEditorList != null)
					{
						mWordEditorList.ReadOnly = mReadOnly;
					}
				}
			}
		}

		public SymbolCollection Symbols
		{
			get
			{
				return mSymbols;
			}
			set
			{
				mSymbols = value;

				if (mWordEditorList != null)
				{
					foreach (MemoryWordEditor editor in mWordEditorList)
					{
						editor.Symbols = mSymbols;
					}
				}
			}
		}

		public bool IsAddressVisible(int address)
		{
			return mWordEditorList.IsIndexVisible(address);
		}

		public void ClearHistory()
		{
			mAddressHistorySelector.Clear();
		}

		private void mUpButton_Click(object sender, EventArgs args)
		{
			if (mPrevDataIndex.HasValue)
			{
				FirstVisibleAddress = mPrevDataIndex.Value;
			}
		}

		private void mDownButton_Click(object sender, EventArgs args)
		{
			if (mNextDataIndex.HasValue)
			{
				FirstVisibleAddress = mNextDataIndex.Value;
			}
		}

		private void mExportButton_Click(object sender, EventArgs args)
		{
			MemoryExportDialog exportDialog = new MemoryExportDialog();
			exportDialog.MinMemoryIndex = mMemory.MinWordIndex;
			exportDialog.MaxMemoryIndex = mMemory.MaxWordIndex;
			exportDialog.FromAddress = mWordEditorList.FirstVisibleIndex;
			exportDialog.ToAddress = mWordEditorList.FirstVisibleIndex + mWordEditorList.VisibleEditorCount - 1;
			exportDialog.ProgramCounter = mMarkedAddress;

			if (exportDialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			if (mSaveExportFileDialog == null)
			{
				mSaveExportFileDialog = new SaveFileDialog();
				mSaveExportFileDialog.DefaultExt = "mixdeck";
				mSaveExportFileDialog.Filter = "MixEmul card deck files|*.mixdeck|All files|*.*";
				mSaveExportFileDialog.Title = "Specify export file name";
			}

			if (mSaveExportFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				IFullWord[] memoryWords = new IFullWord[exportDialog.ToAddress - exportDialog.FromAddress + 1];

				int fromAddressOffset = exportDialog.FromAddress;
				for (int index = 0; index < memoryWords.Length; index++)
				{
					memoryWords[index] = mMemory[fromAddressOffset + index];
				}

				try
				{
					CardDeckExporter.ExportFullWords(mSaveExportFileDialog.FileName, memoryWords, fromAddressOffset, exportDialog.ProgramCounter);
					MessageBox.Show(this, "Memory successfully exported.", "Export successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "Error while exporting memory: " + ex.Message, "Error while exporting", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
			}
		}

		private enum navigationDirection
		{
			None,
			Up,
			Down
		}
	}
}