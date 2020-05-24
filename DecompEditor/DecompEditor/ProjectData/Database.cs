using DecompEditor.Utils;
using System.Diagnostics;

namespace DecompEditor {
  public abstract class DatabaseBase : ObservableObject {
    /// <summary>
    /// If this database is currently dirty or not.
    /// </summary>
    public bool IsDirty { get; private set; } = false;
    public bool IsLoading { get; private set; } = false;

    public void load(ProjectDeserializer deserializer) {
      if (PropertyChangedHandler == null)
        PropertyChanged += (sender, e) => IsDirty = !Project.Instance.IsLoading;

      IsLoading = true;
      reset();
      deserialize(deserializer);
      IsDirty = false;
      IsLoading = false;
    }
    public void save(ProjectSerializer serializer) {
      if (IsDirty) {
        serialize(serializer);
        IsDirty = false;
      }
    }
    public void clear() {
      reset();
      IsDirty = false;
    }

    protected abstract void reset();
    protected abstract void deserialize(ProjectDeserializer deserializer);
    protected virtual void serialize(ProjectSerializer serializer) => Debug.Assert(IsDirty == false, "Dirty databases must override 'serialize'");
  }
}
