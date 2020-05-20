using DecompEditor.Utils;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DecompEditor {
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
      set {
        moves = value;
        moves.trackItemPropertyUpdates(this);
      }
    }

    public static Pokemon createDefault() {
      return new Pokemon {
        HeldItem = Project.Instance.Items.getFromId("ITEM_NONE"),
        Moves = new ObservableCollection<Move>(Enumerable.Repeat(Project.Instance.Moves.getFromId("MOVE_NONE"), 4)),
        Species = Project.Instance.Species.getFromId("SPECIES_NONE")
      };
    }
  }
  public class TrainerParty : ObservableObject {
    private ObservableCollection<Pokemon> pokemon;

    public ObservableCollection<Pokemon> Pokemon {
      get => pokemon;
      set {
        pokemon = value;
        pokemon.trackItemPropertyUpdates(this);
      }
    }
    public string CppVariable { get; set; }

    public bool HasItems => Pokemon.Any(pokemon => pokemon.HeldItem.Identifier != "ITEM_NONE");
    public bool HasMoves => Pokemon.Any(pokemon => Enumerable.Any(pokemon.Moves, move => move.Identifier != "MOVE_NONE"));

    TrainerParty() => Pokemon = new ObservableCollection<Pokemon>();

    internal class Deserializer {
      class PokemonStruct : CParser.Struct {
        public ItemDatabase itemDatabase;
        public MoveDatabase moveDatabase;
        public PokemonSpeciesDatabase speciesDatabase;
        public Pokemon currentPokemon;

        public PokemonStruct() {
          addInteger("iv", (iv) => currentPokemon.Iv = iv);
          addInteger("lvl", (lvl) => currentPokemon.Level = lvl);
          addEnum("species", (species) => currentPokemon.Species = speciesDatabase.getFromId(species));
          addEnum("heldItem", (heldItem) => currentPokemon.HeldItem = itemDatabase.getFromId(heldItem));
          addEnumList("moves", (moves) => currentPokemon.Moves = new ObservableCollection<Move>(moves.Select(move => moveDatabase.getFromId(move))));
        }
      }
      static readonly PokemonStruct pokemonSerializer = new PokemonStruct();

      public static TrainerParty deserialize(StreamReader stream, ItemDatabase itemDatabase,
                                             MoveDatabase moveDatabase, PokemonSpeciesDatabase speciesDatabase) {
        pokemonSerializer.itemDatabase = itemDatabase;
        pokemonSerializer.moveDatabase = moveDatabase;
        pokemonSerializer.speciesDatabase = speciesDatabase;

        string defLine = stream.ReadLine();
        defLine = defLine.Remove(defLine.Length - "[] = {".Length);

        var party = new TrainerParty {
          CppVariable = defLine.Substring(defLine.LastIndexOf(' ') + 1)
        };
        while (stream.ReadLine().Trim().StartsWith("{")) {
          var pkm = new Pokemon();
          pokemonSerializer.currentPokemon = pkm;
          pokemonSerializer.deserialize(stream);
          if (pkm.HeldItem == null)
            pkm.HeldItem = itemDatabase.getFromId("ITEM_NONE");
          if (pkm.Moves == null)
            pkm.Moves = new ObservableCollection<Move>(Enumerable.Repeat(moveDatabase.getFromId("MOVE_NONE"), 4));
          party.Pokemon.Add(pkm);
        }

        // Read the last line after the party definition.
        stream.ReadLine();
        return party;
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
