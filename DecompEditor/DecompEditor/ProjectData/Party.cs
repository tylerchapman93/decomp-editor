using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DecompEditor {
  /// <summary>
  /// A specific pokemon from within a trainer's party.
  /// </summary>
  public class Pokemon : ObservableObject {
    private int iv = 0;
    private int level = 5;
    private PokemonSpecies species;
    private Item heldItem;
    private ObservableCollection<Move> moves;

    public int Iv { get => iv; set => Set(ref iv, value); }
    public int Level { get => level; set => Set(ref level, value); }
    public PokemonSpecies Species { get => species; set => Set(ref species, value); }
    public Item HeldItem { get => heldItem; set => Set(ref heldItem, value); }
    public ObservableCollection<Move> Moves {
      get => moves;
      set => SetAndTrack(ref moves, value);
    }

    public static Pokemon createDefault() {
      return new Pokemon {
        HeldItem = Project.Instance.Items.getFromId("ITEM_NONE"),
        Moves = new ObservableCollection<Move>(Enumerable.Repeat(Project.Instance.Moves.getFromId("MOVE_NONE"), 4)),
        Species = Project.Instance.Species.getFromId("SPECIES_NONE")
      };
    }
  }

  /// <summary>
  /// The part of a trainer defined within the project.
  /// </summary>
  public class TrainerParty : ObservableObject {
    private ObservableCollection<Pokemon> pokemon;

    /// <summary>
    /// The pokemon within the party.
    /// </summary>
    public ObservableCollection<Pokemon> Pokemon {
      get => pokemon;
      set => SetAndTrackItemUpdates(ref pokemon, value, this);
    }

    /// <summary>
    /// The c++ variable of the party.
    /// </summary>
    public string CppVariable { get; set; }

    /// <summary>
    /// Returns whether any of the pokemon in the party have items.
    /// </summary>
    public bool HasItems => Pokemon.Any(pokemon => pokemon.HeldItem.Identifier != "ITEM_NONE");

    /// <summary>
    /// Returns whether any of the pokemon in the party have moves explicitly set.
    /// </summary>
    public bool HasMoves => Pokemon.Any(pokemon => Enumerable.Any(pokemon.Moves, move => move.Identifier != "MOVE_NONE"));

    TrainerParty() => Pokemon = new ObservableCollection<Pokemon>();

    internal class Deserializer : ArrayDeserializer {
      class PokemonDeserializer : StructDeserializer<Pokemon> {
        public PokemonDeserializer(Action<Pokemon> handler) : base(handler) {
          addInteger("iv", (iv) => current.Iv = iv);
          addInteger("lvl", (lvl) => current.Level = lvl);
          addEnum("species", (species) => current.Species = Project.Instance.Species.getFromId(species));
          addEnum("heldItem", (heldItem) => current.HeldItem = Project.Instance.Items.getFromId(heldItem));
          addEnumList("moves", (moves) => current.Moves = new ObservableCollection<Move>(moves.Select(move => Project.Instance.Moves.getFromId(move))));
        }
      }

      public Deserializer(Dictionary<string, TrainerParty> cppToParty) {
        TrainerParty current = null;
        var pkmDeserializer = new PokemonDeserializer((pkm) => {
          if (pkm.HeldItem == null)
            pkm.HeldItem = Project.Instance.Items.getFromId("ITEM_NONE");
          if (pkm.Moves == null)
            pkm.Moves = new ObservableCollection<Move>(Enumerable.Repeat(Project.Instance.Moves.getFromId("MOVE_NONE"), 4));
          current.Pokemon.Add(pkm);
        });
        initialize(pkmDeserializer, "sParty_", (name) => {
          current = new TrainerParty() { CppVariable = name };
          cppToParty.Add(name, current);
        });
      }
    }
    internal class Serializer {
      public static void serialize(TrainerParty party, StreamWriter stream) {
        bool hasItems = party.HasItems;
        bool hasMoves = party.HasMoves;

        stream.Write("static const struct TrainerMon");
        stream.Write(hasItems ? "Item" : "NoItem");
        stream.Write(hasMoves ? "CustomMoves" : "DefaultMoves");
        stream.WriteLine(" " + party.CppVariable + "[] = {");
        for (int i = 0, e = party.Pokemon.Count; i != e; ++i) {
          Pokemon pkm = party.Pokemon[i];

          stream.WriteLine("    {");
          stream.WriteLine("    .iv = " + pkm.Iv + ",");
          stream.WriteLine("    .lvl = " + pkm.Level + ",");
          stream.WriteLine("    .species = " + pkm.Species.Identifier + ",");
          if (hasItems)
            stream.WriteLine("    .heldItem = " + pkm.HeldItem.Identifier + (hasMoves ? "," : ""));
          if (hasMoves)
            stream.WriteLine("    .moves = {" + string.Join(", ", pkm.Moves.Select(move => move.Identifier)) + "}");
          stream.Write("    }");

          if (i != e - 1)
            stream.Write(',');
          stream.Write('\n');
        }
        stream.WriteLine("};\n");
      }
    }
  }
}
