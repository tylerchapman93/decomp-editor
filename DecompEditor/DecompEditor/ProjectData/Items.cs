using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using Truncon.Collections;

namespace DecompEditor {
  public class Item : ObservableObject {
    private string name;
    private string identifier;

    public string Name { get => name; set => Set(ref name, value); }
    public string Identifier { get => identifier; set => Set(ref identifier, value); }
  }

  public class ItemDatabase : DatabaseBase {
    readonly OrderedDictionary<string, Item> nameToItem = new OrderedDictionary<string, Item>();

    public IEnumerable<Item> Items => nameToItem.Values;

    public Item getFromId(string id) => nameToItem[id];

    protected override void reset() => nameToItem.Clear();

    protected override void deserialize(ProjectDeserializer deserializer)
      => Deserializer.deserialize(deserializer, this);

    class Deserializer {
      class ItemStruct : StructDeserializer<Item> {
        public ItemStruct(Action<string, Item> handler) : base(handler)
          => addString("name", (name) => current.Name = name);
        // TODO:
        //u16 price;
        //u8 holdEffect;
        //u8 holdEffectParam;
        //const u8* description;
        //u8 importance;
        //u8 unk19;
        //u8 pocket;
        //u8 type;
        //ItemUseFunc fieldUseFunc;
        //u8 battleUsage;
        //ItemUseFunc battleUseFunc;
        //u8 secondaryId;
      }

      public static void deserialize(ProjectDeserializer deserializer, ItemDatabase database) {
        var itemDeserializer = new ItemStruct((id, item) => {
          item.Identifier = id;
          database.nameToItem.Add(id, item);
        });
        var arrayDeserializer = new ArrayDeserializer(itemDeserializer, "gItems");
        deserializer.deserializeFile(arrayDeserializer, "src", "data", "items.h");
      }
    }
  }
}
