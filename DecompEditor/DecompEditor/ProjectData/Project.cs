using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DecompEditor {
  public class Project {
    readonly Dictionary<string, string> fileReplacements = new Dictionary<string, string>();
    private string projectDir;

    internal BattleAIDatabase BattleAI { get; } = new BattleAIDatabase();
    internal EventObjectDatabase EventObjects { get; } = new EventObjectDatabase();
    internal ItemDatabase Items { get; } = new ItemDatabase();
    internal MoveDatabase Moves { get; } = new MoveDatabase();
    internal PokemonSpeciesDatabase Species { get; } = new PokemonSpeciesDatabase();
    internal TrainerClassDatabase TrainerClasses { get; } = new TrainerClassDatabase();
    internal TrainerEncounterMusicDatabase TrainerEncounterMusic { get; } = new TrainerEncounterMusicDatabase();
    internal TrainerDatabase Trainers { get; } = new TrainerDatabase();
    internal TrainerPicDatabase TrainerPics { get; } = new TrainerPicDatabase();
    internal string ProjectDir { get => projectDir; private set => projectDir = FileUtils.normalizePath(value); }

    internal bool IsDirty => EventObjects.IsDirty || Trainers.IsDirty || TrainerClasses.IsDirty;

    public static Project Instance { get; private set; } = new Project();

    /// <summary>
    /// Event for when a project is loaded.
    /// </summary>
    public delegate void LoadEventHandler();
    public event LoadEventHandler Loaded;

    public void load(string projectDir) {
      ProjectDir = projectDir;

      // Load the different project databases.
      fileReplacements.Clear();
      try {
        BattleAI.load(projectDir);
        EventObjects.load(projectDir);
        Items.load(projectDir);
        Moves.load(projectDir);
        Species.load(projectDir);
        TrainerClasses.load(projectDir);
        TrainerEncounterMusic.load(projectDir);
        TrainerPics.load(projectDir);
        Trainers.load(projectDir, BattleAI, Items, Moves, Species,
                      TrainerClasses, TrainerPics);
      } catch (Exception) {
        BattleAI.reset();
        EventObjects.reset();
        Items.reset();
        Moves.reset();
        Species.reset();
        TrainerClasses.reset();
        TrainerEncounterMusic.reset();
        TrainerPics.reset();
        Trainers.reset();
      }

      // Signal to all of the listeners that the project is loaded.
      Loaded?.Invoke();
    }

    /// Saving Projects
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

      // Trainer editor saving.
      Trainers.save(ProjectDir, BattleAI);
      TrainerClasses.save(ProjectDir);

      // Overworld editor saving.
      EventObjects.save(ProjectDir);
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
