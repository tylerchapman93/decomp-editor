using DecompEditor.ParserUtils;
using System.Collections.Generic;
using Truncon.Collections;

namespace DecompEditor {
  public class PokemonSpecies {
    public string Identifier { get; set; }
    public string Name { get; set; }
  }
  public class PokemonSpeciesDatabase : DatabaseBase {
    readonly OrderedDictionary<string, PokemonSpecies> enumToSpecies = new OrderedDictionary<string, PokemonSpecies>();

    public PokemonSpecies getFromId(string name) => enumToSpecies[name];

    public IEnumerable<PokemonSpecies> Species => enumToSpecies.Values;

    protected override void reset() => enumToSpecies.Clear();

    protected override void deserialize(ProjectDeserializer deserializer) {
      deserializer.deserializeFile((stream) => {
        if (StructBodyDeserializer.Element.tryDeserializeBracketString(stream.ReadLine(), out string speciesEnum, out string speciesName)) {
          enumToSpecies.Add(speciesEnum, new PokemonSpecies() {
            Identifier = speciesEnum,
            Name = speciesName
          });
        }
      }, "src", "data", "text", "species_names.h");
    }
  }
}
