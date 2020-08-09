using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DecompEditor {
  public class Project {
    readonly Dictionary<string, string> fileReplacements = new Dictionary<string, string>();
    readonly List<DatabaseBase> databases = new List<DatabaseBase>();
    private string projectDir;
    internal BattleAIDatabase BattleAI { get; } = new BattleAIDatabase();
    internal EventObjectDatabase EventObjects { get; } = new EventObjectDatabase();
    internal ItemDatabase Items { get; } = new ItemDatabase();
    internal MoveDatabase Moves { get; } = new MoveDatabase();
    internal PokemonSpeciesDatabase Species { get; } = new PokemonSpeciesDatabase();
    internal TrainerClassDatabase TrainerClasses { get; } = new TrainerClassDatabase();
    internal TrainerEncounterMusicDatabase TrainerEncounterMusic { get; } = new TrainerEncounterMusicDatabase();
    internal TrainerPicDatabase TrainerPics { get; } = new TrainerPicDatabase();
    internal TrainerDatabase Trainers { get; } = new TrainerDatabase();
    internal string ProjectDir { get => projectDir; private set => projectDir = FileUtils.normalizePath(value); }

    internal bool IsDirty => databases.Any(db => db.IsDirty);
    internal bool IsLoading { get; private set; } = false;

    /// <summary>
    /// The main instance of the project being transformed.
    /// </summary>
    public static Project Instance { get; private set; } = new Project();

    /// <summary>
    /// The main logger instance of the project.
    /// </summary>
    public static NLog.Logger Logger { get; private set; } = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The file containing the editor logs.
    /// </summary>
    public static string LogFileName 
    => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DecompEditor", "decompEditor.log");

    private Project() {
      registerDatabases(BattleAI, EventObjects, Items, Moves, Species,
                        TrainerClasses, TrainerEncounterMusic, TrainerPics,
                        Trainers);
    }
    void registerDatabases(params DatabaseBase[] databases) => this.databases.AddRange(databases);

    /// <summary>
    /// Event for when a project is loaded.
    /// </summary>
    public delegate void LoadEventHandler();
    public event LoadEventHandler Loaded;

    /// <summary>
    /// Load the project at the provided project directory.
    /// </summary>
    public void load(string projectDir) {
      IsLoading = true;
      ProjectDir = projectDir;
      fileReplacements.Clear();
      var deserializer = new ProjectDeserializer(this);

      Logger.Info("Loading project located at: {ProjectDir}", projectDir);

      // Check for any necessary upgrades.
      IEnumerable<DatabaseBase> databasesToUpgrade = databases.Where(db => db.needsUpgrade());
      if (databasesToUpgrade.Any()) {
        MessageBoxResult result = MessageBox.Show("This project contains an unsupported format, would you like to try and auto-upgrade?\n" +
                                                  "Note: On failure this may leave the project in an inconsistent state.",
                                                  "Upgrade Project", MessageBoxButton.YesNo);
        if (result == MessageBoxResult.No) {
          ProjectDir = "";
          Loaded?.Invoke();
          IsLoading = false;
          return;
        }

        var serializer = new ProjectSerializer(this);
        foreach (DatabaseBase db in databasesToUpgrade) {
          try {
            db.upgrade(deserializer, serializer);
          } catch (Exception e) {
            MessageBox.Show($"Failed to upgrade project to new format, see {LogFileName} for more details.");
            Logger.Error(e, "Failed to upgrade {Database} to its expected format", db.Name);

            foreach (DatabaseBase database in databases)
              database.clear();
            Loaded?.Invoke();
            IsLoading = false;
            return;
          }
        }
      }

      // Load the databases.
      foreach (var db in databases) {
        try {
          db.load(deserializer);
        } catch (Exception e) {
          MessageBox.Show($"Failed to load project, see {LogFileName} for more details");
          Logger.Error(e, "Failed to load {Database} from the project directory", db.Name);

          foreach (DatabaseBase database in databases)
            database.clear();
        }
      }

      // Signal to all of the listeners that the project is loaded.
      Loaded?.Invoke();
      IsLoading = false;
    }

    /// <summary>
    /// Save the current project.
    /// </summary>
    public void save() {
      // Process any requested file replacements.
      if (fileReplacements.Count != 0) {
        string identifierRegex = "(^|[^0-9_a-zA-Z]){0}([^0-9_a-zA-Z]|$)", identifierRepl = "$1{0}$2";
        FileUtils.replaceInFiles(
          ProjectDir,
          fileReplacements.Select(kv => new KeyValuePair<string, string>(string.Format(identifierRegex, kv.Key),
                                                                         string.Format(identifierRepl, kv.Value))).ToList(),
          "*.c|*.h|*.inc|*.json|*.mk");
        fileReplacements.Clear();
      }

      var serializer = new ProjectSerializer(this);
      foreach (DatabaseBase database in databases)
        database.save(serializer);
    }

    /// <summary>
    /// Request a string replacement within the files of the project.
    /// </summary>
    public void registerFileReplacement(string from, string to) {
      foreach (KeyValuePair<string, string> kv in fileReplacements) {
        if (kv.Value == from) {
          if (kv.Key == to) {
            fileReplacements.Remove(to);
            return;
          }

          from = kv.Key;
          break;
        }
      }
      fileReplacements[from] = to;
    }
  }
}
