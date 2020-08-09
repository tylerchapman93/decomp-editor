using DecompEditor.Utils;
using System.Diagnostics;

namespace DecompEditor {
  public abstract class DatabaseBase : ObservableObject {
    /// <summary>
    /// The name of the database.
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// If this database is currently dirty or not.
    /// </summary>
    public bool IsDirty { get; private set; } = false;

    /// <summary>
    /// If this database is currently loading or not.
    /// </summary>
    public bool IsLoading { get; private set; } = false;

    /// <summary>
    /// Load this database from the project directory.
    /// </summary>
    public void load(ProjectDeserializer deserializer) {
      if (PropertyChangedHandler == null)
        PropertyChanged += (sender, e) => IsDirty = !Project.Instance.IsLoading;
      IsLoading = true;
      reset();

      Debug.Assert(!needsUpgrade(), "database needs to upgrade");
      Project.Logger.Info("Loading {Database}", Name);
      deserialize(deserializer);
      IsDirty = false;
      IsLoading = false;
    }

    /// <summary>
    /// Save the project changes held by this database if this project is dirty.
    /// If no changes are pending, this is a noop.
    /// </summary>
    public void save(ProjectSerializer serializer) {
      if (IsDirty) {
        Project.Logger.Info("Saving {Database}", Name);
        serialize(serializer);
        IsDirty = false;
      }
    }

    /// <summary>
    /// Clear out any data held by this database and drop any pending
    /// project changes.
    /// </summary>
    public void clear() {
      reset();
      IsDirty = false;
    }

    /// <summary>
    /// Reset the data held by this database.
    /// </summary>
    protected abstract void reset();

    /// <summary>
    /// Deserialize the project data into this database.
    /// </summary>
    protected abstract void deserialize(ProjectDeserializer deserializer);

    /// <summary>
    /// Serialize the project data held within this database.
    /// </summary>
    protected virtual void serialize(ProjectSerializer serializer) => Debug.Assert(IsDirty == false, "Dirty databases must override 'serialize'");

    /// <summary>
    /// Ask the database to check if the it needs a new format than what is currently
    /// in the project.
    /// </summary>
    public virtual bool needsUpgrade() { return false; }

    /// <summary>
    /// Upgrade the current format of the project to the new format. This
    /// is called on databases that returned true for `needsUpgrade`.
    /// </summary>
    protected virtual void upgradeFormat(ProjectDeserializer deserializer, ProjectSerializer serializer) {
      Debug.Fail("Databases must override 'upgrade' if 'needsUpgrade' returns true");
    }

    /// <summary>
    /// Upgrade the current format of the project to the new format. This
    /// is called on databases that returned true for `needsUpgrade`.
    /// </summary>
    public void upgrade(ProjectDeserializer deserializer, ProjectSerializer serializer) {
      Debug.Assert(needsUpgrade(), "expected database to need upgrade");
      Project.Logger.Info("Upgrading {Database}", Name);
      IsLoading = true;
      reset();
      upgradeFormat(deserializer, serializer);
      IsDirty = false;
      IsLoading = false;
    }
  }
}
