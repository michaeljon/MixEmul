
namespace MixGui.Components
{
    public class MixByteCollectionEditorList : EditorList<IMixByteCollectionEditor>
    {
        public MixByteCollectionEditorList() : this(0, null, null) { }

        public MixByteCollectionEditorList(int maxWordCount, CreateEditorCallback createEditor, LoadEditorCallback loadEditor)
            : base(0, maxWordCount - 1, createEditor, loadEditor) { }
    }
}