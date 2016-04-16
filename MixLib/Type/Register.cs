namespace MixLib.Type
{

	public abstract class Register : Word
	{
		private int mPaddingByteCount;

		public Register(int byteCount, int paddingByteCount)
			: base(byteCount)
		{
			mPaddingByteCount = paddingByteCount;
		}

		public MixByte GetByteWithPadding(int index)
		{
			return index < mPaddingByteCount ? (MixByte)0 : base[index - mPaddingByteCount];
		}

		public int ByteCountWithPadding
		{
			get
			{
				return (mPaddingByteCount + base.ByteCount);
			}
		}

		public FullWord FullWordValue
		{
			get
			{
				FullWord word = new FullWord();

				for (int i = 0; (i < mPaddingByteCount) && (i < FullWord.ByteCount); i++)
				{
					word[i] = 0;
				}

				for (int i = 0; (i < base.ByteCount) && (i < FullWord.ByteCount); i++)
				{
					word[mPaddingByteCount + i] = base[i];
				}

				word.Sign = base.Sign;

				return word;
			}
		}
	}
}