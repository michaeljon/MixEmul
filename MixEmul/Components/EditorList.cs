using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MixGui.Utils;
using MixLib.Type;

namespace MixGui.Components
{
	public class EditorList<T> : UserControl, IEnumerable<T> where T : IEditor
	{
        object mEditorsSyncRoot;
        VScrollBar mIndexScrollBar;
        int mFirstVisibleIndex;
        bool mReadOnly;
        List<T> mEditors;
        bool mResizeInProgress;
        bool mSizeAdaptationPending;
        int mMinIndex;
        int mMaxIndex;
        CreateEditorCallback mCreateEditor;
        LoadEditorCallback mLoadEditor;

        public bool IsReloading { get; private set; }

        public delegate T CreateEditorCallback(int index);
		public delegate void LoadEditorCallback(T editor, int index);
		public delegate void FirstVisibleIndexChangedHandler(EditorList<T> sender, FirstVisibleIndexChangedEventArgs e);

        public event FirstVisibleIndexChangedHandler FirstVisibleIndexChanged;

		public EditorList()
			: this(0, -1, null, null)
		{
		}

		public EditorList(int minIndex, int maxIndex, CreateEditorCallback createEditor, LoadEditorCallback loadEditor)
		{
			mMinIndex = minIndex;
			mMaxIndex = maxIndex;
			mCreateEditor = createEditor;
			mLoadEditor = loadEditor;
			mFirstVisibleIndex = 0;

			IsReloading = false;
			mReadOnly = false;
			mResizeInProgress = false;
			mSizeAdaptationPending = false;

			mEditorsSyncRoot = new object();
			mEditors = new List<T>();

			InitializeComponent();
		}

        public int EditorCount => mEditors.Count;

        public int MaxEditorCount => mMaxIndex - mMinIndex + 1;

        public T this[int index] => mEditors[index];

        public bool IsIndexVisible(int index) => index >= FirstVisibleIndex && index < (FirstVisibleIndex + VisibleEditorCount);

        public IEnumerator GetEnumerator() => mEditors.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => mEditors.GetEnumerator();

        protected void OnFirstVisibleIndexChanged(FirstVisibleIndexChangedEventArgs args) => FirstVisibleIndexChanged?.Invoke(this, args);

        public int ActiveEditorIndex
		{
			get
			{
				ContainerControl container = this;
				Control control = null;

				var editorControls = new List<Control>(mEditors.Count);
				foreach (T editor in mEditors) editorControls.Add(editor.EditorControl);

				int index = -1;

				while (container != null)
				{
					control = container.ActiveControl;

					index = editorControls.IndexOf(control);
					if (index >= 0) break;

					container = control as ContainerControl;
				}

				return index;
			}
		}

        void AdaptToSize()
        {
            if (MaxEditorCount <= 0 || mEditors.Count == 0) return;

            int visibleEditorCount = VisibleEditorCount;
            int editorsToAddCount = (visibleEditorCount - mEditors.Count) + 1;
            FirstVisibleIndex = mFirstVisibleIndex;

            lock (mEditorsSyncRoot)
            {
                IsReloading = true;

                for (int i = 0; i < mEditors.Count; i++)
                {
                    mEditors[i].EditorControl.Visible = FirstVisibleIndex + i <= MaxIndex;
                }

                if (editorsToAddCount > 0)
                {
                    for (int i = 0; i < editorsToAddCount; i++) AddEditor(FirstVisibleIndex + mEditors.Count);
                }

                IsReloading = false;
            }


            if (visibleEditorCount == MaxEditorCount)
            {
                mIndexScrollBar.Enabled = false;
            }
            else
            {
                mIndexScrollBar.Enabled = true;
                mIndexScrollBar.Value = mFirstVisibleIndex;
                mIndexScrollBar.LargeChange = visibleEditorCount;
                mIndexScrollBar.Refresh();
            }

            mSizeAdaptationPending = false;
        }

        void AddEditor(int index)
        {
            Control lastEditorControl = mEditors[mEditors.Count - 1].EditorControl;
            bool indexInItemRange = index <= mMaxIndex;

            SuspendLayout();

            var newEditor = mCreateEditor(indexInItemRange ? index : int.MinValue);
            Control newEditorControl = newEditor.EditorControl;
            newEditorControl.Anchor = AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Top;
            newEditorControl.Location = new Point(0, lastEditorControl.Bottom);
            newEditorControl.Size = lastEditorControl.Size;
            newEditorControl.TabIndex = mEditors.Count + 1;
            newEditorControl.Visible = indexInItemRange;
            newEditorControl.MouseWheel += MouseWheel_Handler;
            newEditor.ReadOnly = mReadOnly;
            if (newEditor is INavigableControl) ((INavigableControl)newEditor).NavigationKeyDown += This_KeyDown;

            mEditors.Add(newEditor);

            Controls.Add(newEditor.EditorControl);

            ResumeLayout(false);
        }

        void InitializeComponent()
        {
            SuspendLayout();
            Controls.Clear();

            mIndexScrollBar = new VScrollBar
            {
                Location = new Point(Width - 16, 0),
                Size = new Size(16, Height),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom | AnchorStyles.Top,
                Minimum = mMinIndex,
                Maximum = mMaxIndex,
                Name = "mAddressScrollBar",
                LargeChange = 1,
                TabIndex = 0,
                Enabled = !mReadOnly
            };
            mIndexScrollBar.Scroll += MIndexScrollBar_Scroll;

            Control editorControl = mCreateEditor != null ? CreateFirstEditor() : null;

            Controls.Add(mIndexScrollBar);

            if (editorControl != null) Controls.Add(editorControl);

            Name = "WordEditorList";

            BorderStyle = BorderStyle.Fixed3D;

            ResumeLayout(false);

            AdaptToSize();

            SizeChanged += This_SizeChanged;
            KeyDown += This_KeyDown;
            MouseWheel += MouseWheel_Handler;
        }

        void MouseWheel_Handler(object sender, MouseEventArgs e)
		{
			FirstVisibleIndex -= (int)((e.Delta / 120.0F) * SystemInformation.MouseWheelScrollLines);
		}

        Control CreateFirstEditor()
        {
            var editor = mCreateEditor(0);
            Control editorControl = editor.EditorControl;
            editorControl.Anchor = AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Top;
            editorControl.Location = new Point(0, 0);
            editorControl.Width = mIndexScrollBar.Left;
            editorControl.TabIndex = 1;
            editorControl.MouseWheel += MouseWheel_Handler;
            editor.ReadOnly = mReadOnly;
            if (editor is INavigableControl)
            {
                ((INavigableControl)editor).NavigationKeyDown += This_KeyDown;
            }

            mEditors.Add(editor);

            return editorControl;
        }

        void This_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers != Keys.None) return;

            FieldTypes? editorField = null;
            int? index = null;

            if (e is IndexKeyEventArgs)
            {
                index = ((IndexKeyEventArgs)e).Index;
            }

            if (e is FieldKeyEventArgs)
            {
                editorField = ((FieldKeyEventArgs)e).Field;
            }

            switch (e.KeyCode)
            {
                case Keys.Prior:
                    FirstVisibleIndex -= VisibleEditorCount;
                    break;

                case Keys.Next:
                    FirstVisibleIndex += VisibleEditorCount;
                    return;

                case Keys.Up:
                    if (sender is INavigableControl)
                    {
                        var editor = (T)sender;

                        if (editor.Equals(mEditors[0]))
                        {
                            mIndexScrollBar.Focus();
                            FirstVisibleIndex--;
                            editor.Focus(editorField, index);

                            return;
                        }

                        mEditors[mEditors.IndexOf(editor) - 1].Focus(editorField, index);

                        return;
                    }

                    FirstVisibleIndex--;

                    return;

                case Keys.Right:
                    break;

                case Keys.Down:
                    if (sender is INavigableControl)
                    {
                        var editor = (T)sender;

                        if (mEditors.IndexOf(editor) >= VisibleEditorCount - 1)
                        {
                            mIndexScrollBar.Focus();
                            FirstVisibleIndex++;
                            mEditors[VisibleEditorCount - 1].Focus(editorField, index);

                            return;
                        }

                        mEditors[mEditors.IndexOf(editor) + 1].Focus(editorField, index);

                        return;
                    }

                    FirstVisibleIndex++;

                    return;
            }
        }

        void MIndexScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            FirstVisibleIndex = mIndexScrollBar.Value;
        }

        public void MakeIndexVisible(int index)
		{
			if (!IsIndexVisible(index))
			{
				FirstVisibleIndex = index;
			}
		}

		public LoadEditorCallback LoadEditor
		{
			get
			{
				return mLoadEditor;
			}
			set
			{
				if (value == null) return;

				if (mLoadEditor != null)
				{
					throw new InvalidOperationException("value may only be set once");
				}

				mLoadEditor = value;
			}
		}

		public CreateEditorCallback CreateEditor
		{
			get
			{
				return mCreateEditor;
			}
			set
			{
				if (value == null) return;

				if (mCreateEditor != null)
				{
					throw new InvalidOperationException("value may only be set once");
				}

				mCreateEditor = value;

				Controls.Add(CreateFirstEditor());
				AdaptToSize();
			}
		}

		public bool ResizeInProgress
		{
			get
			{
				return mResizeInProgress;
			}

			set
			{
				if (mResizeInProgress == value) return;

				mResizeInProgress = value;

				if (mResizeInProgress)
				{
					for (int i = 1; i < mEditors.Count; i++) mEditors[i].EditorControl.SuspendDrawing();
				}
				else
				{
					for (int i = 1; i < mEditors.Count; i++) mEditors[i].EditorControl.ResumeDrawing();

					if (mSizeAdaptationPending) AdaptToSize();

					Invalidate(true);
				}
			}
		}

        void This_SizeChanged(object sender, EventArgs e)
        {
            if (!mResizeInProgress)
            {
                AdaptToSize();
            }
            else
            {
                mSizeAdaptationPending = true;
            }
        }

        public new void Update()
		{
			lock (mEditorsSyncRoot)
			{
				for (int i = 0; i < mEditors.Count; i++)
				{
					int index = mFirstVisibleIndex + i;
					mLoadEditor(mEditors[i], index <= mMaxIndex ? index : int.MinValue);
				}
			}

			base.Update();
		}

		public void UpdateLayout()
		{
			SuspendLayout();

			lock (mEditorsSyncRoot)
			{
				foreach (T editor in mEditors) editor.UpdateLayout();
			}

			ResumeLayout();
		}

		public int FirstVisibleIndex
		{
			get
			{
				return mFirstVisibleIndex;
			}
			set
			{
				if (value < mMinIndex)
				{
					value = mMinIndex;
				}

				if (value + VisibleEditorCount > mMaxIndex + 1)
				{
					value = mMaxIndex - VisibleEditorCount + 1;
				}

				if (value != mFirstVisibleIndex)
				{
					int indexDelta = value - mFirstVisibleIndex;
					int selectedEditorIndex = ActiveEditorIndex;
					FieldTypes? field = selectedEditorIndex >= 0 ? mEditors[selectedEditorIndex].FocusedField : null;
					int? caretIndex = selectedEditorIndex >= 0 ? mEditors[selectedEditorIndex].CaretIndex : null;

					mFirstVisibleIndex = value;

					lock (mEditorsSyncRoot)
					{
						IsReloading = true;

						for (int i = 0; i < mEditors.Count; i++)
						{
							int index = mFirstVisibleIndex + i;
							bool indexInItemRange = index <= mMaxIndex;

							T editor = mEditors[i];
                            mLoadEditor?.Invoke(editor, indexInItemRange ? index : int.MinValue);

                            editor.EditorControl.Visible = indexInItemRange;
						}

						if (selectedEditorIndex != -1)
						{
							selectedEditorIndex -= indexDelta;
							if (selectedEditorIndex < 0)
							{
								selectedEditorIndex = 0;
							}
							else if (selectedEditorIndex >= VisibleEditorCount)
							{
								selectedEditorIndex = VisibleEditorCount - 1;
							}

							mEditors[selectedEditorIndex].Focus(field, caretIndex);
						}

						IsReloading = false;
					}

					mIndexScrollBar.Value = mFirstVisibleIndex;
					OnFirstVisibleIndexChanged(new FirstVisibleIndexChangedEventArgs(mFirstVisibleIndex));
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
					lock (mEditorsSyncRoot)
					{
						foreach (T editor in mEditors)
						{
							editor.ReadOnly = mReadOnly;
						}
					}
				}
			}
		}

		public int VisibleEditorCount
		{
			get
			{
				if (mEditors.Count == 0) return 0;

				int editorAreaHeight = Height;

				if (editorAreaHeight < 0)
				{
					editorAreaHeight = 0;
				}

				return Math.Min(editorAreaHeight / mEditors[0].EditorControl.Height, MaxEditorCount);
			}
		}

		public int MaxIndex
		{
			get
			{
				return mMaxIndex;
			}
			set
			{
				mMaxIndex = value;

				mIndexScrollBar.Maximum = mMaxIndex;

				AdaptToSize();
			}
		}

		public int MinIndex
		{
			get
			{
				return mMinIndex;
			}
			set
			{
				mMaxIndex = value;

				mIndexScrollBar.Minimum = mMinIndex;

				AdaptToSize();
			}
		}

        public class FirstVisibleIndexChangedEventArgs : EventArgs
        {
            public int FirstVisibleIndex { get; private set; }

            public FirstVisibleIndexChangedEventArgs(int firstVisibleIndex)
            {
                FirstVisibleIndex = firstVisibleIndex;
            }
        }

    }
}