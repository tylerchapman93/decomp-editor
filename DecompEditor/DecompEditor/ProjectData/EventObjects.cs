using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Truncon.Collections;

namespace DecompEditor {
  public class GraphicsInfo : ObservableObject {
    private string cppVariable;
    private EventObjectPalette palette;
    private string reflectionPalette;
    private int width;
    private int height;
    private int paletteSlot;
    private string shadowSize;
    private bool inanimate;
    private bool enableReflectionPaletteLoad;
    private string tracks;
    private EventObjectAnimTable animations;
    private EventObjectPicTable picTable;
    private string affineAnimations;

    public string CppVariable { get => cppVariable; set => Set(ref cppVariable, value); }
    public EventObjectPalette Palette { get => palette; set => Set(ref palette, value); }
    public string ReflectionPalette { get => reflectionPalette; set => Set(ref reflectionPalette, value); }
    public int Width { get => width; set => Set(ref width, value); }
    public int Height { get => height; set => Set(ref height, value); }
    public int PaletteSlot { get => paletteSlot; set => Set(ref paletteSlot, value); }
    public string ShadowSize { get => shadowSize; set => Set(ref shadowSize, value); }
    public bool Inanimate { get => inanimate; set => Set(ref inanimate, value); }
    public bool EnableReflectionPaletteLoad { get => enableReflectionPaletteLoad; set => Set(ref enableReflectionPaletteLoad, value); }
    public string Tracks { get => tracks; set => Set(ref tracks, value); }
    public EventObjectAnimTable Animations { get => animations; set => Set(ref animations, value); }
    public EventObjectPicTable PicTable { get => picTable; set => Set(ref picTable, value); }
    public string AffineAnimations { get => affineAnimations; set => Set(ref affineAnimations, value); }
  }
  public class EventObject : ObservableObject {
    private string identifier;
    private GraphicsInfo info;

    public string Identifier {
      get => identifier;
      set {
        if (identifier != null) {
          Project.Instance.registerFileReplacement(
            "OBJ_EVENT_GFX_" + identifier, "OBJ_EVENT_GFX_" + value);
        }

        Set(ref identifier, value);
        if (Info != null) {
          string pascalId = identifier.fromSnakeToPascal();
          Info.CppVariable = "gObjectEventGraphicsInfo_" + pascalId;
          Info.PicTable.CppVar = "gObjectEventPicTable_" + pascalId;
        }
      }
    }

    public GraphicsInfo Info { get => info; set => Set(ref info, value); }
  }
  public class EventObjectAnimTable {
    public string Identifier { get; set; }
    public string PrettyName { get; set; }
  }
  public class EventObjectPalette {
    public static EventObjectPalette GenerateFromFileInst = new EventObjectPalette() {
      CppVariable = string.Empty,
      Identifier = "<Generate From Image>",
      FilePath = string.Empty
    };

    public string CppVariable { get; set; }
    public string Identifier { get; set; }
    public string FilePath { get; set; }

    public override string ToString() => Identifier;
  }
  public class EventObjectPic : ObservableObject {
    private string identifier;
    private string path;
    private string fullPath;

    public string Identifier { get => identifier; set => Set(ref identifier, value); }
    public string Path {
      get => path;
      set {
        if (path != null)
          Project.Instance.registerFileReplacement(path + ".4bpp", value + ".4bpp");
        Set(ref path, value);
      }
    }
    public string FullPath {
      get => fullPath;
      set => Set(ref fullPath, FileUtils.normalizePath(value));
    }
  }
  public class EventObjectPicTable : ObservableObject {
    public class Frame : ObservableObject {
      public Frame() { }
      public Frame(Frame other) {
        Pic = other.Pic;
        Index = other.Index;
      }
      private EventObjectPic pic;
      private int index = 0;

      public EventObjectPic Pic { get => pic; set => Set(ref pic, value); }
      public int Index { get => index; set => Set(ref index, value); }
    }

    private string cppVar;
    private ObservableCollection<Frame> frames;

    public string CppVar { get => cppVar; set => Set(ref cppVar, value); }
    public ObservableCollection<Frame> Frames {
      get => frames; set {
        Set(ref frames, value);
        frames.trackItemPropertyUpdates(this);
      }
    }

    public EventObjectPicTable() => Frames = new ObservableCollection<Frame>();
  }
  public class EventObjectDatabase : ObservableObject {
    ObservableCollection<EventObjectPic> pics;
    readonly OrderedDictionary<string, EventObjectPalette> varToPalette = new OrderedDictionary<string, EventObjectPalette>();
    readonly List<GraphicsInfo> nonObjectGraphicsInfos = new List<GraphicsInfo>();
    readonly List<EventObjectAnimTable> animTables = new List<EventObjectAnimTable>();
    private ObservableCollection<EventObject> objects;

    public bool IsDirty { get; private set; }
    public ObservableCollection<EventObject> Objects {
      get => objects;
      set {
        Set(ref objects, value);
        objects.trackItemPropertyUpdates(this);
      }
    }

    public IEnumerable<EventObjectAnimTable> AnimTables => animTables;

    public List<EventObjectPalette> Palettes => varToPalette.Values.Concat(Enumerable.Repeat(EventObjectPalette.GenerateFromFileInst, 1)).ToList();

    public ObservableCollection<EventObjectPic> Pics {
      get => pics;
      set {
        Set(ref pics, value);
        pics.trackItemPropertyUpdates(this);
      }
    }
    public List<string> ShadowSizes { get; set; } = new List<string>();
    public List<string> TrackTypes { get; set; } = new List<string>();

    public EventObjectDatabase() {
      Objects = new ObservableCollection<EventObject>();
      Pics = new ObservableCollection<EventObjectPic>();
      PropertyChanged += (sender, e) => IsDirty = true;
    }

    public void reset() {
      animTables.Clear();
      nonObjectGraphicsInfos.Clear();
      Objects.Clear();
      Pics.Clear();
      ShadowSizes.Clear();
      TrackTypes.Clear();
      varToPalette.Clear();
      IsDirty = false;
    }

    public void load(string projectDir) {
      reset();
      new Deserializer(this).deserialize(projectDir);
      IsDirty = false;
    }
    public void save(string projectDir) {
      if (!IsDirty)
        return;
      new Serializer(this).serialize(projectDir);
      IsDirty = false;
    }

    class Deserializer {
      class PicTableMap {
        readonly Dictionary<string, EventObjectPicTable> varToPicTable = new Dictionary<string, EventObjectPicTable>();
        readonly Dictionary<string, int> picVarCount = new Dictionary<string, int>();
        public void Add(EventObjectPicTable table) => varToPicTable.Add(table.CppVar, table);
        public EventObjectPicTable GetValue(string cppVar) {
          if (!varToPicTable.TryGetValue(cppVar, out EventObjectPicTable table))
            return new EventObjectPicTable() { CppVar = cppVar };
          if (picVarCount.TryGetValue(cppVar, out int picCount)) {
            // If the table has already been accessed, clone it so that each info gets its own
            // table. This just makes it easier to edit.
            picVarCount[cppVar] = ++picCount;
            table = new EventObjectPicTable() {
              CppVar = cppVar + "_" + picCount,
              Frames = new ObservableCollection<EventObjectPicTable.Frame>(table.Frames.Select(frame => new EventObjectPicTable.Frame(frame)))
            };
          } else {
            picVarCount.Add(cppVar, ++picCount);
          }

          return table;
        }
      };
      class GraphicsInfoStruct : CParser.Struct {
        public GraphicsInfo currentInfo;
        public PicTableMap varToPicTable;
        public Dictionary<string, EventObjectAnimTable> varToAnimTable;
        public Dictionary<string, EventObjectPalette> idToPalette;

        public GraphicsInfoStruct() {
          addEnum("paletteTag1", (val) => currentInfo.Palette = idToPalette[val]);
          addEnum("paletteTag2", (val) => currentInfo.ReflectionPalette = val);
          addInteger("width", (val) => currentInfo.Width = val);
          addInteger("height", (val) => currentInfo.Height = val);
          addInteger("paletteSlot", (val) => currentInfo.PaletteSlot = val);
          addEnum("shadowSize", (val) => currentInfo.ShadowSize = val.Remove(0, "SHADOW_SIZE_".Length));
          addEnum("inanimate", (val) => currentInfo.Inanimate = val[0] != 'F');
          addEnum("disableReflectionPaletteLoad", (val) => currentInfo.EnableReflectionPaletteLoad = val[0] == 'F');
          addEnum("tracks", (val) => currentInfo.Tracks = val.Remove(0, "TRACKS_".Length));
          addEnum("anims", (val) => currentInfo.Animations = varToAnimTable[val]);
          addEnum("images", (val) => currentInfo.PicTable = varToPicTable.GetValue(val));
          addEnum("affineAnims", (val) => currentInfo.AffineAnimations = val);
        }
      }
      static readonly GraphicsInfoStruct infoSerializer = new GraphicsInfoStruct();
      readonly EventObjectDatabase database;
      readonly Dictionary<string, GraphicsInfo> cppVarToGraphicsInfo = new Dictionary<string, GraphicsInfo>();
      readonly Dictionary<string, EventObjectAnimTable> varToAnimTable = new Dictionary<string, EventObjectAnimTable>();
      readonly Dictionary<string, EventObjectPalette> idToPalette = new Dictionary<string, EventObjectPalette>();
      readonly Dictionary<string, EventObjectPic> idToPic = new Dictionary<string, EventObjectPic>();
      readonly PicTableMap varToPicTable = new PicTableMap();

      public Deserializer(EventObjectDatabase database) {
        this.database = database;
        infoSerializer.varToAnimTable = varToAnimTable;
        infoSerializer.idToPalette = idToPalette;
        infoSerializer.varToPicTable = varToPicTable;
      }

      public void deserialize(string projectDir) {
        loadPicsAndPalettes(projectDir);
        loadPaletteIDs(projectDir);
        loadAnimTables(projectDir);
        loadPicTables(projectDir);
        loadGraphicsInfos(projectDir);
        loadTracksAndShadowSizes(projectDir);
        loadObjects(projectDir);
      }

      void loadPicsAndPalettes(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics.h"));
        var picPaths = new HashSet<string>();
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (line.StartsWith("const u32 gObjectEventPic_")) {
            int skipLen = "const u32 gObjectEventPic_".Length;
            string cppVar = line.Substring(skipLen, line.IndexOf('[') - skipLen);
            string filename = string.Empty;
            CParser.Element.deserializeValue(line, CParser.ValueKind.String, (value) => filename = value as string);
            string path = Path.ChangeExtension(filename, ".png");
            var pic = new EventObjectPic() {
              Identifier = cppVar,
              Path = Path.ChangeExtension(path.Remove(0, "graphics/object_events/pics/".Length), null),
              FullPath = Path.Combine(projectDir, path)
            };
            picPaths.Add(path);
            idToPic.Add(cppVar, pic);
            database.Pics.Add(pic);
            continue;
          }
          if (line.StartsWith("const u16 gObjectEventPalette")) {
            if (line.EndsWith("{};"))
              continue;

            int skipLen = "const u16 ".Length;
            string cppVar = line.Substring(skipLen, line.IndexOf('[') - skipLen);
            string filename = string.Empty;
            CParser.Element.deserializeValue(line, CParser.ValueKind.String, (value) => filename = value as string);
            database.varToPalette.Add(cppVar, new EventObjectPalette() {
              CppVariable = cppVar,
              FilePath = Path.ChangeExtension(filename, ".pal")
            });
            continue;
          }
        }

        // Remove all of the palettes whose file paths match a present picture, these
        // palettes will be generated from that image.
        var toRemove = new List<string>();
        foreach (KeyValuePair<string, EventObjectPalette> kv in database.varToPalette) {
          if (picPaths.Contains(Path.ChangeExtension(kv.Value.FilePath, ".png")))
            toRemove.Add(kv.Key);
        }

        foreach (string key in toRemove)
          database.varToPalette.Remove(key);
        reader.Close();
      }
      void loadPaletteIDs(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "event_object_movement.c"));
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (!line.StartsWith("const struct SpritePalette sObjectEventSpritePalettes[]"))
            continue;
          do {
            line = reader.ReadLine().Remove(0, "    {".Length);
            if (line.StartsWith("NULL"))
              break;
            string cppVar = line.Substring(0, line.IndexOf(','));
            string fullId = line.Substring(cppVar.Length + 2, line.Length - cppVar.Length - 4).TrimStart();

            if (!database.varToPalette.TryGetValue(cppVar, out EventObjectPalette pal)) {
              idToPalette.Add(fullId, EventObjectPalette.GenerateFromFileInst);
            } else {
              pal.Identifier = fullId.Remove(0, "OBJ_EVENT_PAL_TAG_".Length);
              idToPalette.Add(fullId, pal);
            }
          } while (true);
          break;
        }
        reader.Close();
      }
      void loadObjects(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info_pointers.h"));
        var usedInfos = new HashSet<string>();
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (line.StartsWith("const struct ObjectEventGraphicsInfo g")) {
            int skipLen = "const struct ObjectEventGraphicsInfo ".Length;
            string cppVar = line.Substring(skipLen, line.Length - skipLen - 1);
            if (!cppVarToGraphicsInfo.ContainsKey(cppVar)) {
              cppVarToGraphicsInfo.Add(cppVar, new GraphicsInfo() {
                CppVariable = cppVar
              });
            }
            continue;
          }
          if (line.StartsWith("    [OBJ_EVENT_GFX")) {
            int skipLen = "    [OBJ_EVENT_GFX_".Length;
            string objectID = line.Substring(skipLen, line.IndexOf(']') - skipLen);
            skipLen = line.LastIndexOf('&') + 1;
            string infoVar = line.Substring(skipLen, line.Length - skipLen - 1);
            database.Objects.Add(new EventObject() {
              Identifier = objectID,
              Info = cppVarToGraphicsInfo[infoVar]
            });
            usedInfos.Add(infoVar);
            continue;
          }
        }
        foreach (GraphicsInfo info in cppVarToGraphicsInfo.Values) {
          if (!usedInfos.Contains(info.CppVariable))
            database.nonObjectGraphicsInfos.Add(info);
        }

        reader.Close();
      }
      void loadAnimTables(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "object_events", "object_event_anims.h"));
        while (!reader.EndOfStream) {
          if (!reader.ReadLine().tryExtractPrefix("const union AnimCmd *const gObjectEventImageAnimTable_", "[", out string name))
            continue;
          var animTable = new EventObjectAnimTable() {
            Identifier = "gObjectEventImageAnimTable_" + name,
            PrettyName = name.fromPascalToSentence()
          };
          varToAnimTable.Add(animTable.Identifier, animTable);
          database.animTables.Add(animTable);
        }
        reader.Close();
      }
      void loadPicTables(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "object_events", "object_event_pic_tables.h"));
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (!line.StartsWith("const struct SpriteFrameImage gObjectEventPicTable_"))
            continue;
          int skipLen = "const struct SpriteFrameImage ".Length;
          string cppVar = line.Substring(skipLen, line.Length - skipLen - 6);
          var table = new EventObjectPicTable() {
            CppVar = cppVar
          };
          do {
            line = reader.ReadLine().Trim();
            if (line[0] == '}')
              break;
            if (line.StartsWith("obj_frame_tiles")) {
              skipLen = line.IndexOf('(') + 1 + "gObjectEventPic_".Length;
              table.Frames.Add(new EventObjectPicTable.Frame() {
                Pic = idToPic[line.Substring(skipLen, line.Length - skipLen - 2)],
              });
              continue;
            }

            line = line.Remove(0, "overworld_frame(".Length);
            string[] elements = line.Remove(line.Length - 2).Split(", ");
            table.Frames.Add(new EventObjectPicTable.Frame() {
              Pic = idToPic[elements[0].Remove(0, "gObjectEventPic_".Length)],
              Index = int.Parse(elements[3])
            });
          } while (true);
          varToPicTable.Add(table);
        }
        reader.Close();
      }
      void loadGraphicsInfos(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info.h"));
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (!line.StartsWith("const struct ObjectEventGraphicsInfo g"))
            continue;
          int skipLen = "const struct ObjectEventGraphicsInfo ".Length;
          string cppVar = line.Substring(skipLen, line.IndexOf(' ', skipLen) - skipLen);
          var info = new GraphicsInfo() {
            CppVariable = cppVar
          };

          // Parse the inline variant.
          if (line.EndsWith(";")) {
            skipLen += cppVar.Length + 4;
            string[] elements = line.Substring(skipLen, line.Length - skipLen - 2).Split(", ");
            info.Palette = idToPalette[elements[1]];
            info.ReflectionPalette = elements[2];
            info.Width = int.Parse(elements[4]);
            info.Height = int.Parse(elements[5]);
            info.PaletteSlot = int.Parse(elements[6]);
            info.ShadowSize = elements[7].Remove(0, "SHADOW_SIZE_".Length);
            info.Inanimate = elements[8][0] != 'F';
            info.EnableReflectionPaletteLoad = elements[9][0] == 'F';
            info.Tracks = elements[10].Remove(0, "TRACKS_".Length);
            info.Animations = varToAnimTable[elements[13]];
            info.PicTable = varToPicTable.GetValue(elements[14]);
            info.AffineAnimations = elements[15];
          } else {
            infoSerializer.currentInfo = info;
            infoSerializer.deserialize(reader);
          }
          cppVarToGraphicsInfo.Add(cppVar, info);
        }
        reader.Close();
      }
      void loadTracksAndShadowSizes(string projectDir) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "include", "constants", "event_objects.h"));
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (line.tryExtractPrefix("#define SHADOW_SIZE_", " ", out string enumName))
            database.ShadowSizes.Add(enumName);
          else if (line.tryExtractPrefix("#define TRACKS_", " ", out enumName))
            database.TrackTypes.Add(enumName);
        }
        reader.Close();
      }
    }
    class Serializer {
      /// A set of palettes generated from sprite images.
      readonly OrderedDictionary<EventObjectPic, EventObjectPalette> picPalettes = new OrderedDictionary<EventObjectPic, EventObjectPalette>();
      readonly HashSet<EventObjectPicTable.Frame> framesWithoutSpriteSheets = new HashSet<EventObjectPicTable.Frame>();
      readonly EventObjectDatabase database;

      public Serializer(EventObjectDatabase database) => this.database = database;

      public void serialize(string projectDir) {
        populatePalettesGeneratedFromSprites();
        updateSpriteSheetMakeRules(projectDir);
        savePaletteIDs(projectDir);
        saveObjectIds(projectDir);
        saveGraphicsInfoPointers(projectDir);
        savePicTables(projectDir);
        saveGraphicsInfos(projectDir);
        savePicsAndPalettes(projectDir);
      }

      void populatePalettesGeneratedFromSprites() {
        foreach (EventObject obj in database.Objects) {
          if (obj.Info.Palette != EventObjectPalette.GenerateFromFileInst)
            continue;
          // Use the palette from the first image.
          EventObjectPic pic = obj.Info.PicTable.Frames[0].Pic;
          picPalettes.Add(pic, new EventObjectPalette() {
            CppVariable = "gObjectEventPalette_" + pic.Identifier,
            FilePath = "graphics/object_events/pics/" + pic.Path,
            Identifier = pic.Identifier.fromPascalToSnake()
          });
        }
      }

      void updateSpriteSheetMakeRules(string projectDir) {
        var checkedFiles = new HashSet<EventObjectPic>();
        var needSprites = new Dictionary<string, Tuple<int, int>>();
        foreach (EventObject obj in database.Objects) {
          int objWidth = obj.Info.Width, objHeight = obj.Info.Height;

          foreach (EventObjectPicTable.Frame frame in obj.Info.PicTable.Frames) {
            if (!checkedFiles.Add(frame.Pic))
              continue;
            BitmapImage file = FileUtils.loadBitmapImage(frame.Pic.FullPath);
            if (file.PixelWidth != objWidth || file.PixelHeight != objHeight)
              needSprites.Add(frame.Pic.Path, new Tuple<int, int>(objWidth / 8, objHeight / 8));
            else
              framesWithoutSpriteSheets.Add(frame);
          }
        }

        if (needSprites.Count == 0)
          return;
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "spritesheet_rules.mk"));
        bool updatedExistingSheet = false;
        for (int i = 0, e = curLines.Length; i != e; ++i) {
          if (!curLines[i].tryExtractPrefix("$(OBJEVENTGFXDIR)/", ".", out string linePath))
            continue;
          if (!needSprites.TryGetValue(linePath, out Tuple<int, int> widthHeight))
            continue;
          needSprites.Remove(linePath);

          string widthHeightLine = curLines[++i];
          int widthIndex = widthHeightLine.IndexOf("-mwidth") + "-mwidth ".Length;
          int widthIndexEnd = widthHeightLine.IndexOf(' ', widthIndex);

          int heightIndex = widthIndex + " -mheight ".Length;
          bool changed = widthHeight.Item1 != int.Parse(widthHeightLine.Substring(widthIndex, widthIndexEnd - widthIndex)) ||
                         widthHeight.Item2 != int.Parse(widthHeightLine.Substring(heightIndex));
          if (changed) {
            curLines[i] = string.Format("\t$(GFX) $< $@ -mwidth {0} -mheight {1}", widthHeight.Item1, widthHeight.Item2);
            updatedExistingSheet = true;
          }
        }

        if (updatedExistingSheet) {
          var writer = new StreamWriter(Path.Combine(projectDir, "spritesheet_rules.mk"), false);
          foreach (string line in curLines)
            writer.WriteLine(line);
          writer.Close();
        }
        if (needSprites.Count != 0) {
          var writer = new StreamWriter(Path.Combine(projectDir, "spritesheet_rules.mk"), true);
          foreach (KeyValuePair<string, Tuple<int, int>> pathAndDims in needSprites) {
            writer.WriteLine();
            writer.WriteLine("$(OBJEVENTGFXDIR)/" + pathAndDims.Key + ".4bpp: %.4bpp: %.png");
            writer.WriteLine(string.Format("\t$(GFX) $< $@ -mwidth {0} -mheight {1}", pathAndDims.Value.Item1, pathAndDims.Value.Item2));
          }
          writer.Close();
        }
      }
      void savePaletteIDs(string projectDir) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "src", "event_object_movement.c"));
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "event_object_movement.c"), false);

        // Keep the prefix lines as-is.
        int index = 0;
        while (!curLines[index].StartsWith("#define OBJ_EVENT_PAL_TAG_"))
          writer.WriteLine(curLines[index++]);
        while (curLines[index++].Length != 0)
          continue;

        IEnumerable<EventObjectPalette> palettes = database.varToPalette.Values.Concat(picPalettes.Values);

        int longestPalID = 4, longestPalVar = 4;
        foreach (EventObjectPalette pal in palettes) {
          longestPalID = Math.Max(longestPalID, pal.Identifier.Length);
          longestPalVar = Math.Max(longestPalVar, pal.CppVariable.Length);
        }
        int palIndex = 0x1103;
        foreach (EventObjectPalette pal in palettes) {
          writer.WriteLine("#define OBJ_EVENT_PAL_TAG_" + pal.Identifier.PadRight(longestPalID) +
                           " 0x" + string.Format("{0:X}", palIndex++));
        }
        writer.WriteLine("#define OBJ_EVENT_PAL_TAG_" + "NONE".PadRight(longestPalID) + " 0x11FF");
        writer.WriteLine();

        while (!curLines[index].StartsWith("const struct SpritePalette sObjectEventSpritePalettes[]"))
          writer.WriteLine(curLines[index++]);
        while (curLines[index++].Length != 0)
          continue;

        writer.WriteLine("const struct SpritePalette sObjectEventSpritePalettes[] = {");
        foreach (EventObjectPalette pal in palettes) {
          writer.WriteLine("    {" + (pal.CppVariable + ", ").PadRight(longestPalVar + 2) +
                           "OBJ_EVENT_PAL_TAG_" + pal.Identifier + "},");
        }
        writer.WriteLine("    {" + "NULL, ".PadRight(longestPalVar + 2) + "0x0000},");
        writer.WriteLine("};\n");

        while (index != curLines.Length)
          writer.WriteLine(curLines[index++]);
        writer.Close();
      }
      void saveObjectIds(string projectDir) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "include", "constants", "event_objects.h"));
        var writer = new StreamWriter(Path.Combine(projectDir, "include", "constants", "event_objects.h"), false);

        // Keep the prefix lines as-is.
        int index = 0;
        while (!curLines[index].StartsWith("#define OBJ_EVENT_GFX_"))
          writer.WriteLine(curLines[index++]);
        while (!curLines[index++].StartsWith("#define NUM_OBJ_EVENT_GFX"))
          continue;

        int longestedID = 0;
        for (int i = 0; i != database.Objects.Count; ++i)
          longestedID = Math.Max(longestedID, database.Objects[i].Identifier.Length);
        longestedID += 3;
        for (int i = 0; i != database.Objects.Count; ++i)
          writer.WriteLine("#define OBJ_EVENT_GFX_" + database.Objects[i].Identifier.PadRight(longestedID + 1) + i);

        writer.WriteLine();
        writer.WriteLine("#define NUM_OBJ_EVENT_GFX" + "".PadRight(longestedID - 2) + database.Objects.Count.ToString());

        while (index != curLines.Length)
          writer.WriteLine(curLines[index++]);
        writer.Close();
      }
      void saveGraphicsInfoPointers(string projectDir) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info_pointers.h"));
        var stream = new StreamWriter(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info_pointers.h"), false);
        foreach (GraphicsInfo info in database.Objects.Select(obj => obj.Info).Concat(database.nonObjectGraphicsInfos))
          stream.WriteLine("const struct ObjectEventGraphicsInfo " + info.CppVariable + ";");
        stream.WriteLine();

        stream.WriteLine("const struct ObjectEventGraphicsInfo *const gObjectEventGraphicsInfoPointers[NUM_OBJ_EVENT_GFX] = {");
        int longestedObjId = 0;
        foreach (EventObject obj in database.Objects)
          longestedObjId = Math.Max(longestedObjId, obj.Identifier.Length);

        foreach (EventObject obj in database.Objects) {
          stream.WriteLine("    [OBJ_EVENT_GFX_" + obj.Identifier + "] = ".PadRight(longestedObjId) +
                           "&" + obj.Info.CppVariable + ",");
        }
        stream.WriteLine("};");

        int skipLineIndex = 0;
        while (!curLines[skipLineIndex++].StartsWith("}"))
          continue;
        while (skipLineIndex != curLines.Length)
          stream.WriteLine(curLines[skipLineIndex++]);
        stream.Close();
      }
      void savePicTables(string projectDir) {
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "object_events", "object_event_pic_tables.h"), false);
        foreach (GraphicsInfo info in database.Objects.Select(obj => obj.Info)
                                             .Concat(database.nonObjectGraphicsInfos)) {
          if (info.PicTable.Frames.Count == 0)
            continue;
          writer.WriteLine("const struct SpriteFrameImage " + info.PicTable.CppVar + "[] = {");
          int width = info.Width / 8, height = info.Height / 8;
          foreach (EventObjectPicTable.Frame frame in info.PicTable.Frames) {
            if (framesWithoutSpriteSheets.Contains(frame)) {
              writer.WriteLine("    obj_frame_tiles(gObjectEventPic_" + frame.Pic.Identifier + "),");
            } else {
              writer.WriteLine("    overworld_frame(gObjectEventPic_" + frame.Pic.Identifier + ", " + width +
                               ", " + height + ", " + frame.Index + "),");
            }
          }
          writer.WriteLine("};\n");
        }
        writer.Close();
      }
      int roundOamSize(int size) {
        if (size <= 8)
          return 8;
        if (size <= 16)
          return 16;
        if (size <= 32)
          return 32;
        return 64;
      }
      void saveGraphicsInfos(string projectDir) {
        var stream = new StreamWriter(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info.h"), false);
        foreach (GraphicsInfo info in database.Objects.Select(obj => obj.Info).Concat(database.nonObjectGraphicsInfos)) {
          string palId;
          if (info.Palette == EventObjectPalette.GenerateFromFileInst)
            palId = picPalettes[info.PicTable.Frames[0].Pic].Identifier;
          else
            palId = info.Palette.Identifier;

          stream.WriteLine("const struct ObjectEventGraphicsInfo " + info.CppVariable + " = {");
          stream.WriteLine("    .tileTag = 0xFFFF,");
          stream.WriteLine("    .paletteTag1 = OBJ_EVENT_PAL_TAG_" + palId + ",");
          stream.WriteLine("    .paletteTag2 = " + info.ReflectionPalette + ",");
          stream.WriteLine("    .size = " + ((info.Width * info.Height * 4) / 8) + ",");
          stream.WriteLine("    .width = " + info.Width + ",");
          stream.WriteLine("    .height = " + info.Height + ",");
          stream.WriteLine("    .paletteSlot = " + info.PaletteSlot + ",");
          stream.WriteLine("    .shadowSize = SHADOW_SIZE_" + info.ShadowSize + ",");
          stream.WriteLine("    .inanimate = " + (info.Inanimate ? "TRUE" : "FALSE") + ",");
          stream.WriteLine("    .disableReflectionPaletteLoad = " + (info.EnableReflectionPaletteLoad ? "FALSE" : "TRUE") + ",");
          stream.WriteLine("    .tracks = TRACKS_" + info.Tracks + ",");
          stream.WriteLine("    .oam = &gObjectEventBaseOam_" + roundOamSize(info.Width) + "x" + roundOamSize(info.Height) + ",");
          stream.WriteLine("    .subspriteTables = gObjectEventSpriteOamTables_" + info.Width + "x" + info.Height + ",");
          stream.WriteLine("    .anims = " + info.Animations.Identifier + ",");
          stream.WriteLine("    .images = " + info.PicTable.CppVar + ",");
          stream.WriteLine("    .affineAnims = " + info.AffineAnimations + ",");
          stream.WriteLine("};\n");
        }
        stream.Close();
      }
      void savePicsAndPalettes(string projectDir) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics.h"));
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics.h"), false);

        // Copy the existing lines unrelated to even palettes and pictures.
        foreach (string str in curLines) {
          if (!str.StartsWith("const u32 gObjectEventPic_") && !str.StartsWith("const u16 gObjectEventPalette"))
            writer.WriteLine(str);
        }

        foreach (EventObjectPic pic in database.pics) {
          writer.WriteLine("const u32 gObjectEventPic_" + pic.Identifier +
                           "[] = INCBIN_U32(\"graphics/object_events/pics/" + pic.Path + ".4bpp\");");
        }

        foreach (KeyValuePair<string, EventObjectPalette> palAndPath in database.varToPalette) {
          writer.WriteLine("const u16 " + palAndPath.Key + "[] = INCBIN_U16(\"" +
                           Path.ChangeExtension(palAndPath.Value.FilePath, ".gbapal") + "\");");
        }
        foreach (KeyValuePair<EventObjectPic, EventObjectPalette> picAndPal in picPalettes) {
          writer.WriteLine("const u16 " + picAndPal.Value.CppVariable + "[] = INCBIN_U16(\"" +
                           Path.ChangeExtension(picAndPal.Value.FilePath, ".gbapal") + "\");");
        }
        writer.Close();

        // Check to see if any of the pics changed location.
        foreach (EventObjectPic pic in database.pics) {
          string fullPrettyPath = Path.Combine(projectDir, "graphics/object_events/pics/", pic.Path + ".png");
          string normalizedPath = FileUtils.normalizePath(fullPrettyPath);
          if (pic.FullPath == normalizedPath)
            continue;
          File.Copy(pic.FullPath, fullPrettyPath, true);
          pic.FullPath = normalizedPath;

          // Delete any existing bpp/pal files to force a rebuild.
          File.Delete(Path.ChangeExtension(normalizedPath, ".4bpp"));
          File.Delete(Path.ChangeExtension(normalizedPath, ".gbapal"));
        }
      }
    }
  }
}
