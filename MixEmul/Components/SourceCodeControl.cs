using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MixAssembler;
using MixAssembler.Finding;
using MixAssembler.Instruction;
using MixGui.Settings;
using MixLib.Misc;

namespace MixGui.Components
{
	public class SourceCodeControl : UserControl
	{
		private const string lineNumberSeparator = " │ ";

		private static readonly Color[] findingColors = new Color[] 
    {
      GuiSettings.GetColor(GuiSettings.DebugText), 
      GuiSettings.GetColor(GuiSettings.InfoText), 
      GuiSettings.GetColor(GuiSettings.WarningText), 
      GuiSettings.GetColor(GuiSettings.ErrorText)
    };

		private Color mAddressColor;
		private Color mCommentColor;
		private bool mFindingsColored;
		private List<processedSourceLine> mInstructions;
		private Color mLineNumberColor;
		private int mLineNumberLength;
		private Color mLineNumberSeparatorColor;
		private Color mLocColor;
		private AssemblyFinding mMarkedFinding;
		private Color mOpColor;
		private RichTextBox mSourceBox = new RichTextBox();

		public SourceCodeControl()
		{
			base.SuspendLayout();

			mSourceBox.Anchor = AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top;
			mSourceBox.Location = new Point(0, 0);
			mSourceBox.Name = "mSourceBox";
			mSourceBox.ReadOnly = true;
			mSourceBox.Size = base.Size;
			mSourceBox.TabIndex = 0;
			mSourceBox.Text = "";
			mSourceBox.DetectUrls = false;

			base.Controls.Add(mSourceBox);
			base.Name = "SourceCodeControl";
			base.ResumeLayout(false);

			UpdateLayout();

			mInstructions = new List<processedSourceLine>();
			mLineNumberLength = 0;
			mFindingsColored = false;
		}

		private void addLine(ParsedSourceLine sourceLine)
		{
			int count = mInstructions.Count;
			if (mSourceBox.TextLength != 0)
			{
				mSourceBox.AppendText(Environment.NewLine);
			}

			string lineNumberText = (count + 1).ToString();
			mSourceBox.AppendText(new string(' ', mLineNumberLength - lineNumberText.Length) + lineNumberText + lineNumberSeparator);

			processedSourceLine processedLine = new processedSourceLine(sourceLine, mSourceBox.TextLength);
			mInstructions.Add(processedLine);

			if (sourceLine.IsCommentLine)
			{
				if (sourceLine.Comment.Length > 0)
				{
					mSourceBox.AppendText(sourceLine.Comment);
				}
			}
			else
			{
				mSourceBox.AppendText(sourceLine.LocationField + new string(' ', (processedLine.LocTextLength - sourceLine.LocationField.Length) + Parser.FieldSpacing));
				mSourceBox.AppendText(sourceLine.OpField + new string(' ', (processedLine.OpTextLength - sourceLine.OpField.Length) + Parser.FieldSpacing));
				mSourceBox.AppendText(sourceLine.AddressField + new string(' ', (processedLine.AddressTextLength - sourceLine.AddressField.Length) + Parser.FieldSpacing));
				if (sourceLine.Comment.Length > 0)
				{
					mSourceBox.AppendText(sourceLine.Comment);
				}
			}

			applySyntaxColoring(processedLine);
		}

		private void applyFindingColoring(AssemblyFinding finding, markOperation mark)
		{
			if (finding != null && finding.LineNumber != int.MinValue && finding.LineNumber >= 0 && finding.LineNumber < mInstructions.Count)
			{
				processedSourceLine processedLine = mInstructions[finding.LineNumber];
				int lineTextIndex = processedLine.LineTextIndex;
				int length = 0;

				switch (finding.LineSection)
				{
					case LineSection.LocationField:
						if (finding.Length <= 0)
						{
							lineTextIndex = processedLine.LocTextIndex;
							length = processedLine.SourceLine.LocationField.Length;

							break;
						}

						lineTextIndex = processedLine.LocTextIndex + finding.StartCharIndex;
						length = finding.Length;

						break;

					case LineSection.OpField:
						if (finding.Length <= 0)
						{
							lineTextIndex = processedLine.OpTextIndex;
							length = processedLine.SourceLine.OpField.Length;

							break;
						}

						lineTextIndex = processedLine.OpTextIndex + finding.StartCharIndex;
						length = finding.Length;

						break;

					case LineSection.AddressField:
						if (finding.Length <= 0)
						{
							lineTextIndex = processedLine.AddressTextIndex;
							length = processedLine.SourceLine.AddressField.Length;

							break;
						}

						lineTextIndex = processedLine.AddressTextIndex + finding.StartCharIndex;
						length = finding.Length;

						break;

					case LineSection.CommentField:
						if (finding.Length <= 0)
						{
							lineTextIndex = processedLine.CommentTextIndex;
							length = processedLine.SourceLine.Comment.Length;

							break;
						}

						lineTextIndex = processedLine.CommentTextIndex + finding.StartCharIndex;
						length = finding.Length;

						break;

					case LineSection.EntireLine:
						length = processedLine.LineTextLength;

						break;
				}

				mSourceBox.Select(lineTextIndex, length);

				if (length != 0)
				{
					if (mark == markOperation.Mark)
					{
						Font font = mSourceBox.Font;

						mSourceBox.SelectionFont = new Font(font.Name, font.Size, FontStyle.Underline, font.Unit, font.GdiCharSet);
						mSourceBox.Focus();
						mSourceBox.ScrollToCaret();
					}
					else if (mark == markOperation.Unmark)
					{
						mSourceBox.SelectionFont = mSourceBox.Font;
					}

					mSourceBox.SelectionColor = findingColors[(int)finding.Severity];
					mSourceBox.SelectionLength = 0;
				}
			}
		}

		private void applyFindingColoring(AssemblyFindingCollection findings, Severity severity)
		{
			foreach (AssemblyFinding finding in findings)
			{
				if (finding.Severity == severity)
				{
					applyFindingColoring(finding, markOperation.None);
				}
			}
		}

		private void applySyntaxColoring(processedSourceLine processedLine)
		{
			mSourceBox.SelectionStart = (processedLine.LineTextIndex - lineNumberSeparator.Length) - mLineNumberLength;
			mSourceBox.SelectionLength = mLineNumberLength;
			mSourceBox.SelectionColor = mLineNumberColor;
			mSourceBox.SelectionStart += mLineNumberLength;
			mSourceBox.SelectionLength = lineNumberSeparator.Length;
			mSourceBox.SelectionColor = mLineNumberSeparatorColor;

			if (!processedLine.SourceLine.IsCommentLine)
			{
				if (processedLine.SourceLine.LocationField.Length > 0)
				{
					mSourceBox.SelectionStart = processedLine.LocTextIndex;
					mSourceBox.SelectionLength = processedLine.SourceLine.LocationField.Length;
					mSourceBox.SelectionColor = mLocColor;
				}

				if (processedLine.SourceLine.OpField.Length > 0)
				{
					mSourceBox.SelectionStart = processedLine.OpTextIndex;
					mSourceBox.SelectionLength = processedLine.SourceLine.OpField.Length;
					mSourceBox.SelectionColor = mOpColor;
				}

				if (processedLine.SourceLine.AddressField.Length > 0)
				{
					mSourceBox.SelectionStart = processedLine.AddressTextIndex;
					mSourceBox.SelectionLength = processedLine.SourceLine.AddressField.Length;
					mSourceBox.SelectionColor = mAddressColor;
				}
			}

			if (processedLine.SourceLine.Comment.Length > 0)
			{
				mSourceBox.SelectionStart = processedLine.CommentTextIndex;
				mSourceBox.SelectionLength = processedLine.SourceLine.Comment.Length;
				mSourceBox.SelectionColor = mCommentColor;
			}
		}

		public void UpdateLayout()
		{
			mSourceBox.Font = GuiSettings.GetFont(GuiSettings.FixedWidth);
			mSourceBox.BackColor = GuiSettings.GetColor(GuiSettings.EditorBackground);
			mLineNumberColor = GuiSettings.GetColor(GuiSettings.LineNumberText);
			mLineNumberSeparatorColor = GuiSettings.GetColor(GuiSettings.LineNumberSeparator);
			mLocColor = GuiSettings.GetColor(GuiSettings.LocationFieldText);
			mOpColor = GuiSettings.GetColor(GuiSettings.OpFieldText);
			mAddressColor = GuiSettings.GetColor(GuiSettings.AddressFieldText);
			mCommentColor = GuiSettings.GetColor(GuiSettings.CommentFieldText);
		}

		public AssemblyFindingCollection Findings
		{
			set
			{
				applyFindingColoring(mMarkedFinding, markOperation.Unmark);
				mMarkedFinding = null;

				if (mFindingsColored)
				{
					foreach (processedSourceLine line in mInstructions)
					{
						applySyntaxColoring(line);
					}
				}

				if (value.ContainsDebugs)
				{
					applyFindingColoring(value, Severity.Debug);
				}

				if (value.ContainsInfos)
				{
					applyFindingColoring(value, Severity.Info);
				}

				if (value.ContainsWarnings)
				{
					applyFindingColoring(value, Severity.Warning);
				}

				if (value.ContainsErrors)
				{
					applyFindingColoring(value, Severity.Error);
				}

				mFindingsColored = true;
			}
		}

		public PreInstruction[] Instructions
		{
			set
			{
				mMarkedFinding = null;
				mSourceBox.Text = "";
				mInstructions.Clear();
				mFindingsColored = false;

				if (value == null)
				{
					mLineNumberLength = 0;
				}
				else
				{
					int lineCount = value.Length;
					mLineNumberLength = lineCount.ToString().Length;

					SortedList<int, ParsedSourceLine> parsedLines = new SortedList<int, ParsedSourceLine>();

					foreach (PreInstruction instruction in value)
					{
						if (instruction is ParsedSourceLine)
						{
							ParsedSourceLine parsedLine = (ParsedSourceLine)instruction;
							parsedLines.Add(parsedLine.LineNumber, parsedLine);
						}
					}

					foreach (ParsedSourceLine parsedLine in parsedLines.Values)
					{
						while (parsedLine.LineNumber > mInstructions.Count)
						{
							addLine(new ParsedSourceLine(mInstructions.Count, ""));
						}

						addLine(parsedLine);
					}

					mSourceBox.SelectionStart = 0;
					mSourceBox.SelectionLength = 0;
				}
			}
		}

		public AssemblyFinding MarkedFinding
		{
			get
			{
				return mMarkedFinding;
			}
			set
			{
				applyFindingColoring(mMarkedFinding, markOperation.Unmark);

				mMarkedFinding = value;

				applyFindingColoring(mMarkedFinding, markOperation.Mark);
			}
		}

		private enum markOperation
		{
			Unmark,
			None,
			Mark
		}

		private class processedSourceLine
		{
			private int mLineTextIndex;
			private ParsedSourceLine mSourceLine;

			public processedSourceLine(ParsedSourceLine sourceLine, int lineTextIndex)
			{
				mSourceLine = sourceLine;
				mLineTextIndex = lineTextIndex;
			}

			public int AddressTextIndex
			{
				get
				{
					return !SourceLine.IsCommentLine ? OpTextIndex + OpTextLength + Parser.FieldSpacing : LineTextIndex;
				}
			}

			public int AddressTextLength
			{
				get
				{
					if (SourceLine.IsCommentLine)
					{
						return 0;
					}

					if (mSourceLine.Comment != "")
					{
						return Math.Max(Parser.MinAddressLength, SourceLine.AddressField.Length);
					}

					return SourceLine.AddressField.Length;
				}
			}

			public int CommentTextIndex
			{
				get
				{
					return !SourceLine.IsCommentLine ? AddressTextIndex + AddressTextLength + Parser.FieldSpacing : LineTextIndex;
				}
			}

			public int LineTextIndex
			{
				get
				{
					return mLineTextIndex;
				}
			}

			public int LineTextLength
			{
				get
				{
					return (SourceLine.Comment == "" ? AddressTextIndex + AddressTextLength : CommentTextIndex + SourceLine.Comment.Length) - LineTextIndex;
				}
			}

			public int LocTextIndex
			{
				get
				{
					return LineTextIndex;
				}
			}

			public int LocTextLength
			{
				get
				{
					return !SourceLine.IsCommentLine ? Math.Max(Parser.MinLocLength, SourceLine.LocationField.Length) : 0;
				}
			}

			public int OpTextIndex
			{
				get
				{
					return !SourceLine.IsCommentLine ? LocTextIndex + LocTextLength + Parser.FieldSpacing : LineTextIndex;
				}
			}

			public int OpTextLength
			{
				get
				{
					return !SourceLine.IsCommentLine ? Math.Max(Parser.MinOpLength, SourceLine.OpField.Length) : 0;
				}
			}

			public ParsedSourceLine SourceLine
			{
				get
				{
					return mSourceLine;
				}
			}
		}
	}
}