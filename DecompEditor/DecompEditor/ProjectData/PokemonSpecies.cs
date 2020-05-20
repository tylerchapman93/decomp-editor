using DecompEditor.Utils;
using System.Collections.Generic;
using System.IO;
using Truncon.Collections;

namespace DecompEditor {
  public class PokemonSpecies {
    public string Identifier { get; set; }
    public string Name { get; set; }
  }
  public class PokemonSpeciesDatabase {
    readonly OrderedDictionary<string, PokemonSpecies> enumToSpecies = new OrderedDictionary<string, PokemonSpecies>();

    public PokemonSpecies getFromId(string name) => enumToSpecies[name];

    public IEnumerable<PokemonSpecies> Species => enumToSpecies.Values;

    public void reset() => enumToSpecies.Clear();

    public void load(string projectDir) {
      reset();

      StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "text", "species_names.h"));
      reader.ReadLine();
      while (!reader.EndOfStream) {
        if (CParser.Element.tryDeserializeBracketString(reader.ReadLine(), out string speciesEnum, out string speciesName)) {
          enumToSpecies.Add(speciesEnum, new PokemonSpecies() {
            Identifier = speciesEnum,
            Name = speciesName
          });
        }
      }
    }
  }
}
