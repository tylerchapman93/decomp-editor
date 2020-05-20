using DecompEditor.Utils;
using System.Collections.Generic;
using System.IO;
using Truncon.Collections;

namespace DecompEditor {
  public class Item : ObservableObject {
    private string name;
    private string identifier;

    public string Name { get => name; set => Set(ref name, value); }
    public string Identifier { get => identifier; set => Set(ref identifier, value); }
  }

  public class ItemDatabase {
    readonly OrderedDictionary<string, Item> nameToItem = new OrderedDictionary<string, Item>();

    public IEnumerable<Item> Items => nameToItem.Values;

    public Item getFromId(string id) => nameToItem[id];

    public void reset() => nameToItem.Clear();

    public void load(string projectDir) {
      reset();
      Deserializer.deserialize(projectDir, this);
    }

    class Deserializer {
      class ItemStruct : CParser.Struct {
        public Item currentItem;

        public ItemStruct() => addString("name", (name) => currentItem.Name = name);// TODO://u16 price;//u8 holdEffect;//u8 holdEffectParam;//const u8* description;//u8 importance;//u8 unk19;//u8 pocket;//u8 type;//ItemUseFunc fieldUseFunc;//u8 battleUsage;//ItemUseFunc battleUseFunc;//u8 secondaryId;
      }
      static readonly ItemStruct itemSerializer = new ItemStruct();

      public static void deserialize(string projectDir, ItemDatabase database) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "items.h"));
        reader.ReadLine();
        reader.ReadLine();

        while (!reader.EndOfStream) {
          if (!reader.ReadLine().tryExtractPrefix("    [", "]", out string itemId))
            continue;
          reader.ReadLine();

          Item newItem = itemSerializer.currentItem = new Item() {
            Identifier = itemId
          };
          itemSerializer.deserialize(reader);
          database.nameToItem.Add(newItem.Identifier, newItem);
        }
      }
    }
  }
}
