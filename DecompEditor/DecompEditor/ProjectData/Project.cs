using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DecompEditor {
  public class Project {
    readonly Dictionary<string, string> fileReplacements = new Dictionary<string, string>();
    readonly List<DatabaseBase> databases = new List<DatabaseBase>();
    private readonly BattleAIDatabase battleAI = new BattleAIDatabase();
    private readonly EventObjectDatabase eventObjects = new EventObjectDatabase();
    private readonly ItemDatabase items = new ItemDatabase();
    private readonly MoveDatabase moves = new MoveDatabase();
    private readonly PokemonSpeciesDatabase species = new PokemonSpeciesDatabase();
    private readonly TrainerClassDatabase trainerClasses = new TrainerClassDatabase();
    private readonly TrainerEncounterMusicDatabase trainerEncounterMusic = new TrainerEncounterMusicDatabase();
    private readonly TrainerPicDatabase trainerPics = new TrainerPicDatabase();
    private readonly TrainerDatabase trainers = new TrainerDatabase();
    private string projectDir;

    internal BattleAIDatabase BattleAI => getSafe(battleAI);
    internal EventObjectDatabase EventObjects => getSafe(eventObjects);
    internal ItemDatabase Items => getSafe(items);
    internal MoveDatabase Moves => getSafe(moves);
    internal PokemonSpeciesDatabase Species => getSafe(species);
    internal TrainerClassDatabase TrainerClasses => getSafe(trainerClasses);
    internal TrainerEncounterMusicDatabase TrainerEncounterMusic => getSafe(trainerEncounterMusic); 
    internal TrainerPicDatabase TrainerPics => getSafe(trainerPics);
    internal TrainerDatabase Trainers => getSafe(trainers);
    internal string ProjectDir { get => projectDir; private set => projectDir = FileUtils.normalizePath(value); }

    internal bool IsDirty => databases.Any(db => db.IsDirty);
    internal bool IsLoading { get; private set; } = false;

    public static Project Instance { get; private set; } = new Project();

    private Project() {
      registerDatabases(BattleAI, EventObjects, Items, Moves, Species,
                        TrainerClasses, TrainerEncounterMusic, TrainerPics,
                        Trainers);
    }
    void registerDatabases(params DatabaseBase[] databases) => this.databases.AddRange(databases);
    T getSafe<T>(T database) where T: DatabaseBase {
      while (database.IsLoading)
        Thread.Yield();
      return database;
    }

    /// <summary>
    /// Event for when a project is loaded.
    /// </summary>
    public delegate void LoadEventHandler();
    public event LoadEventHandler Loaded;

    public void load(string projectDir) {
      IsLoading = true;
      ProjectDir = projectDir;

      // Load the different project databases.
      fileReplacements.Clear();
      try {
        var deserializer = new ProjectDeserializer(this);
        Parallel.ForEach(databases, db => db.load(deserializer));
      } catch (Exception) {
        foreach (DatabaseBase database in databases)
          database.clear();
      }

      // Signal to all of the listeners that the project is loaded.
      Loaded?.Invoke();
      IsLoading = false;
    }

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

    /// Request a file replacement within the project.
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
