using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
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
    public EventObjectPalette Palette { get => palette; set => SetAndTrack(ref palette, value); }
    public string ReflectionPalette { get => reflectionPalette; set => Set(ref reflectionPalette, value); }
    public int Width { get => width; set => Set(ref width, value); }
    public int Height { get => height; set => Set(ref height, value); }
    public int PaletteSlot { get => paletteSlot; set => Set(ref paletteSlot, value); }
    public string ShadowSize { get => shadowSize; set => Set(ref shadowSize, value); }
    public bool Inanimate { get => inanimate; set => Set(ref inanimate, value); }
    public bool EnableReflectionPaletteLoad { get => enableReflectionPaletteLoad; set => Set(ref enableReflectionPaletteLoad, value); }
    public string Tracks { get => tracks; set => Set(ref tracks, value); }
    public EventObjectAnimTable Animations { get => animations; set => SetAndTrack(ref animations, value); }
    public EventObjectPicTable PicTable { get => picTable; set => SetAndTrack(ref picTable, value); }
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

    public GraphicsInfo Info { get => info; set => SetAndTrack(ref info, value); }
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

      public EventObjectPic Pic { get => pic; set => SetAndTrack(ref pic, value); }
      public int Index { get => index; set => Set(ref index, value); }
    }

    private string cppVar;
    private ObservableCollection<Frame> frames;

    public string CppVar { get => cppVar; set => Set(ref cppVar, value); }
    public ObservableCollection<Frame> Frames {
      get => frames; set => SetAndTrackItemUpdates(ref frames, value, this);
    }

    public EventObjectPicTable() => Frames = new ObservableCollection<Frame>();
  }
  public class EventObjectDatabase : DatabaseBase {
    ObservableCollection<EventObjectPic> pics;
    readonly OrderedDictionary<string, EventObjectPalette> varToPalette = new OrderedDictionary<string, EventObjectPalette>();
    readonly List<GraphicsInfo> nonObjectGraphicsInfos = new List<GraphicsInfo>();
    readonly List<EventObjectAnimTable> animTables = new List<EventObjectAnimTable>();
    private ObservableCollection<EventObject> objects;

    public ObservableCollection<EventObject> Objects { get => objects; set => SetAndTrackItemUpdates(ref objects, value, this); }

    public IEnumerable<EventObjectAnimTable> AnimTables => animTables;

    public List<EventObjectPalette> Palettes => varToPalette.Values.Concat(Enumerable.Repeat(EventObjectPalette.GenerateFromFileInst, 1)).ToList();

    public ObservableCollection<EventObjectPic> Pics { get => pics; set => SetAndTrackItemUpdates(ref pics, value, this); }
    public List<string> ShadowSizes { get; set; } = new List<string>();
    public List<string> TrackTypes { get; set; } = new List<string>();

    public EventObjectDatabase() {
      Objects = new ObservableCollection<EventObject>();
      Pics = new ObservableCollection<EventObjectPic>();
    }

    protected override void reset() {
      animTables.Clear();
      nonObjectGraphicsInfos.Clear();
      Objects.Clear();
      Pics.Clear();
      ShadowSizes.Clear();
      TrackTypes.Clear();
      varToPalette.Clear();
    }

    protected override void deserialize(ProjectDeserializer serializer)
      => new Deserializer(this).deserialize(serializer);
    protected override void serialize(ProjectSerializer serializer)
      => new Serializer(this).serialize(serializer);

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
      readonly EventObjectDatabase database;
      readonly Dictionary<string, GraphicsInfo> cppVarToGraphicsInfo = new Dictionary<string, GraphicsInfo>();
      readonly Dictionary<string, EventObjectAnimTable> varToAnimTable = new Dictionary<string, EventObjectAnimTable>();
      readonly Dictionary<string, EventObjectPalette> idToPalette = new Dictionary<string, EventObjectPalette>();
      readonly Dictionary<string, EventObjectPic> idToPic = new Dictionary<string, EventObjectPic>();
      readonly PicTableMap varToPicTable = new PicTableMap();

      public Deserializer(EventObjectDatabase database) => this.database = database;

      public void deserialize(ProjectDeserializer deserializer) {
        loadPicsAndPalettes(deserializer);
        loadPaletteIDs(deserializer);
        loadAnimTables(deserializer);
        loadPicTables(deserializer);
        loadGraphicsInfos(deserializer);
        loadTracksAndShadowSizes(deserializer);
        loadObjects(deserializer);
      }

      void loadPicsAndPalettes(ProjectDeserializer deserializer) {
        var picPaths = new HashSet<string>();
        var fileDeserializer = new FileDeserializer();
        fileDeserializer.add(new IncBinDeserializer("gObjectEventPic_", "u32", (cppVar, fileName) => {
          string path = Path.ChangeExtension(fileName, ".png");
          var pic = new EventObjectPic() {
            Identifier = cppVar,
            Path = Path.ChangeExtension(path.Remove(0, "graphics/object_events/pics/".Length), null),
            FullPath = Path.Combine(deserializer.project.ProjectDir, path)
          };
          picPaths.Add(path);
          idToPic.Add(cppVar, pic);
          database.Pics.Add(pic);
        }));
        fileDeserializer.add(new IncBinDeserializer("gObjectEventPalette", "u16", (cppVar, fileName) => {
          cppVar = "gObjectEventPalette" + cppVar;
          database.varToPalette.Add(cppVar, new EventObjectPalette() {
            CppVariable = cppVar,
            FilePath = Path.ChangeExtension(fileName, ".pal")
          });
        }));
        deserializer.deserializeFile(fileDeserializer, "src", "data", "object_events", "object_event_graphics.h");

        // Remove all of the palettes whose file paths match a present picture, these
        // palettes will be generated from that image.
        var toRemove = new List<string>();
        foreach (KeyValuePair<string, EventObjectPalette> kv in database.varToPalette) {
          if (picPaths.Contains(Path.ChangeExtension(kv.Value.FilePath, ".png")))
            toRemove.Add(kv.Key);
        }
        foreach (string key in toRemove)
          database.varToPalette.Remove(key);
      }
      void loadPaletteIDs(ProjectDeserializer deserializer) {
        var paletteDeserializer = new InlineStructDeserializer((vals) => {
          string cppVar = vals[0], fullId = vals[1];
          if (!database.varToPalette.TryGetValue(cppVar, out EventObjectPalette pal)) {
            idToPalette.Add(fullId, EventObjectPalette.GenerateFromFileInst);
          } else {
            pal.Identifier = fullId.Remove(0, "OBJ_EVENT_PAL_TAG_".Length);
            idToPalette.Add(fullId, pal);
          }
        });
        var palArrayDeserializer = new ArrayDeserializer(paletteDeserializer, "sObjectEventSpritePalettes");
        deserializer.deserializeFile(palArrayDeserializer, "src", "event_object_movement.c");
      }
      void loadObjects(ProjectDeserializer deserializer) {
        var usedInfos = new HashSet<string>();
        var fileDeserializer = new FileDeserializer();
        fileDeserializer.add((currentLine, _) => {
          if (!currentLine.tryExtractPrefix("const struct ObjectEventGraphicsInfo ", ";", out string cppVar))
            return false;
          if (!cppVarToGraphicsInfo.ContainsKey(cppVar)) {
            cppVarToGraphicsInfo.Add(cppVar, new GraphicsInfo() {
              CppVariable = cppVar
            });
          }
          return true;
        });
        fileDeserializer.add((currentLine, _) => {
          if (!currentLine.tryExtractPrefix("[OBJ_EVENT_GFX_", "]", out string objectID))
            return false;
          int skipLen = currentLine.LastIndexOf('&') + 1;
          string infoVar = currentLine.Substring(skipLen, currentLine.Length - skipLen - 1);
          database.Objects.Add(new EventObject() {
            Identifier = objectID,
            Info = cppVarToGraphicsInfo[infoVar]
          });
          usedInfos.Add(infoVar);
          return true;
        });
        deserializer.deserializeFile(fileDeserializer, "src", "data", "object_events", "object_event_graphics_info_pointers.h");

        // Check for any graphics info objects that aren't used by event objects.
        foreach (GraphicsInfo info in cppVarToGraphicsInfo.Values) {
          if (!usedInfos.Contains(info.CppVariable))
            database.nonObjectGraphicsInfos.Add(info);
        }
      }
      void loadAnimTables(ProjectDeserializer deserializer) {
        deserializer.deserializeFile((reader) => {
          if (!reader.ReadLine().tryExtractPrefix("const union AnimCmd *const gObjectEventImageAnimTable_", "[", out string name))
            return;
          var animTable = new EventObjectAnimTable() {
            Identifier = "gObjectEventImageAnimTable_" + name,
            PrettyName = name.fromPascalToSentence()
          };
          varToAnimTable.Add(animTable.Identifier, animTable);
          database.animTables.Add(animTable);
        }, "src", "data", "object_events", "object_event_anims.h");
      }
      void loadPicTables(ProjectDeserializer deserializer) {
        deserializer.deserializeFile((stream) => {
          if (!stream.ReadLine().tryExtractPrefix("const struct SpriteFrameImage gObjectEventPicTable_", "[", out string cppVar))
            return;
          var table = new EventObjectPicTable() {
            CppVar = "gObjectEventPicTable_" + cppVar
          };
          do {
            string line = stream.ReadLine().Trim();
            if (line.StartsWith("}"))
              break;
            if (line.StartsWith("obj_frame_tiles")) {
              int skipLen = line.IndexOf('(') + "(gObjectEventPic_".Length;
              table.Frames.Add(new EventObjectPicTable.Frame() {
                Pic = idToPic[line.Substring(skipLen, line.Length - skipLen - 2)],
              });
              continue;
            }

            line = line.Remove(0, "overworld_frame(gObjectEventPic_".Length);
            string[] elements = line.Remove(line.Length - "),".Length).Split(", ");
            table.Frames.Add(new EventObjectPicTable.Frame() {
              Pic = idToPic[elements[0]],
              Index = int.Parse(elements[3])
            });
          } while (true);
          varToPicTable.Add(table);
        }, "src", "data", "object_events", "object_event_pic_tables.h");
      }
      void loadGraphicsInfos(ProjectDeserializer deserializer) {
        deserializer.deserializeFile(new CustomDeserializer((line, _) => {
          if (!line.tryExtractPrefix("const struct ObjectEventGraphicsInfo ", " ", out string cppVar))
            return false;
          var info = new GraphicsInfo() {
            CppVariable = cppVar
          };
          cppVarToGraphicsInfo.Add(cppVar, info);

          string[] elements = line[(line.IndexOf('{') + 1)..(line.Length - 2)].Split(", ");
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
          return true;
        }), "src", "data", "object_events", "object_event_graphics_info.h");
      }
      void loadTracksAndShadowSizes(ProjectDeserializer deserializer) {
        deserializer.deserializeFile((stream) => {
          string line = stream.ReadLine();
          if (line.tryExtractPrefix("#define SHADOW_SIZE_", " ", out string enumName))
            database.ShadowSizes.Add(enumName);
          else if (line.tryExtractPrefix("#define TRACKS_", " ", out enumName))
            database.TrackTypes.Add(enumName);
        }, "include", "constants", "event_objects.h");
      }
    }
    class Serializer {
      /// A set of palettes generated from sprite images.
      readonly OrderedDictionary<EventObjectPic, EventObjectPalette> picPalettes = new OrderedDictionary<EventObjectPic, EventObjectPalette>();
      readonly HashSet<EventObjectPicTable.Frame> framesWithoutSpriteSheets = new HashSet<EventObjectPicTable.Frame>();
      readonly EventObjectDatabase database;

      public Serializer(EventObjectDatabase database) => this.database = database;

      public void serialize(ProjectSerializer serializer) {
        populatePalettesGeneratedFromSprites();
        updateSpriteSheetMakeRules(serializer);
        savePaletteIDs(serializer);
        saveObjectIds(serializer);
        saveGraphicsInfoPointers(serializer);
        savePicTables(serializer);
        saveGraphicsInfos(serializer);
        savePicsAndPalettes(serializer);
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

      void updateSpriteSheetMakeRules(ProjectSerializer serializer) {
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
        string[] curLines = File.ReadAllLines(Path.Combine(serializer.project.ProjectDir, "spritesheet_rules.mk"));
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

        if (updatedExistingSheet)
          File.WriteAllLines(Path.Combine(serializer.project.ProjectDir, "spritesheet_rules.mk"), curLines);
        if (needSprites.Count != 0) {
          var writer = new StreamWriter(Path.Combine(serializer.project.ProjectDir, "spritesheet_rules.mk"), true);
          foreach (KeyValuePair<string, Tuple<int, int>> pathAndDims in needSprites) {
            writer.WriteLine();
            writer.WriteLine("$(OBJEVENTGFXDIR)/" + pathAndDims.Key + ".4bpp: %.4bpp: %.png");
            writer.WriteLine(string.Format("\t$(GFX) $< $@ -mwidth {0} -mheight {1}", pathAndDims.Value.Item1, pathAndDims.Value.Item2));
          }
          writer.Close();
        }
      }
      void savePaletteIDs(ProjectSerializer serializer) {
        IEnumerable<EventObjectPalette> palettes = database.varToPalette.Values.Concat(picPalettes.Values);
        int longestPalID = 4, longestPalVar = 4;

        Func<string, bool> palTagCheck = (str) => str.StartsWith("#define OBJ_EVENT_PAL_TAG_");
        Action<StreamWriter> palTagHandler = (stream) => {
          foreach (EventObjectPalette pal in palettes) {
            longestPalID = Math.Max(longestPalID, pal.Identifier.Length);
            longestPalVar = Math.Max(longestPalVar, pal.CppVariable.Length);
          }
          int palIndex = 0x1103;
          foreach (EventObjectPalette pal in palettes) {
            stream.WriteLine("#define OBJ_EVENT_PAL_TAG_" + pal.Identifier.PadRight(longestPalID) +
                             " 0x" + string.Format("{0:X}", palIndex++));
          }
          stream.WriteLine("#define OBJ_EVENT_PAL_TAG_" + "NONE".PadRight(longestPalID) + " 0x11FF");
          stream.WriteLine();
        };
        Func<string, bool> spritePalCheck = (str) => str.EndsWith("sObjectEventSpritePalettes[] = {");
        Action<StreamWriter> spritePalHandler = (stream) => {
          stream.WriteLine("const struct SpritePalette sObjectEventSpritePalettes[] = {");
          foreach (EventObjectPalette pal in palettes) {
            stream.WriteLine("    {" + (pal.CppVariable + ", ").PadRight(longestPalVar + 2) +
                             "OBJ_EVENT_PAL_TAG_" + pal.Identifier + "},");
          }
          stream.WriteLine("    {" + "NULL, ".PadRight(longestPalVar + 2) + "0x0000},");
          stream.WriteLine("};\n");
        };
        Func<string, bool> sectionEnd = (str) => str.Length == 0;
        serializer.serializePartialFile(new[] { palTagCheck, spritePalCheck }, new[] { sectionEnd, sectionEnd },
                                        new[] { palTagHandler, spritePalHandler }, "src", "event_object_movement.c");
      }
      void saveObjectIds(ProjectSerializer serializer) {
        Action<StreamWriter> handler = stream => {
          int longestedID = 0;
          for (int i = 0; i != database.Objects.Count; ++i)
            longestedID = Math.Max(longestedID, database.Objects[i].Identifier.Length);
          longestedID += 3;
          for (int i = 0; i != database.Objects.Count; ++i)
            stream.WriteLine("#define OBJ_EVENT_GFX_" + database.Objects[i].Identifier.PadRight(longestedID + 1) + i);

          stream.WriteLine();
          stream.WriteLine("#define NUM_OBJ_EVENT_GFX" + "".PadRight(longestedID - 2) + database.Objects.Count.ToString());
        };
        serializer.serializePartialFile(str => str.StartsWith("#define OBJ_EVENT_GFX_"),
                                        str => str.StartsWith("#define NUM_OBJ"),
                                        handler, "include", "constants", "event_objects.h");
      }
      void saveGraphicsInfoPointers(ProjectSerializer serializer) {
        serializer.serializePartialFile(str => true, str => str.StartsWith("}"), stream => {
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
        }, "src", "data", "object_events", "object_event_graphics_info_pointers.h");
      }
      void savePicTables(ProjectSerializer serializer) {
        IEnumerable<GraphicsInfo> infos = database.Objects.Select(obj => obj.Info).Concat(database.nonObjectGraphicsInfos);
        serializer.serializeFile(stream => {
          foreach (GraphicsInfo info in infos) {
            if (info.PicTable.Frames.Count == 0)
              continue;
            stream.WriteLine("const struct SpriteFrameImage " + info.PicTable.CppVar + "[] = {");
            int width = info.Width / 8, height = info.Height / 8;
            foreach (EventObjectPicTable.Frame frame in info.PicTable.Frames) {
              if (framesWithoutSpriteSheets.Contains(frame)) {
                stream.WriteLine("    obj_frame_tiles(gObjectEventPic_" + frame.Pic.Identifier + "),");
              } else {
                stream.WriteLine("    overworld_frame(gObjectEventPic_" + frame.Pic.Identifier + ", " + width +
                                 ", " + height + ", " + frame.Index + "),");
              }
            }
            stream.WriteLine("};\n");
          }
        }, "src", "data", "object_events", "object_event_pic_tables.h");
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
      void saveGraphicsInfos(ProjectSerializer serializer) {
        serializer.serializeFile(stream => {
          foreach (GraphicsInfo info in database.Objects.Select(obj => obj.Info).Concat(database.nonObjectGraphicsInfos)) {
            string palId;
            if (info.Palette == EventObjectPalette.GenerateFromFileInst)
              palId = picPalettes[info.PicTable.Frames[0].Pic].Identifier;
            else
              palId = info.Palette.Identifier;

            // TODO: It would be nice to print the struct form, but PoryMap can't load that in...
            stream.Write("const struct ObjectEventGraphicsInfo " + info.CppVariable + " = {");
            // .tileTag
            stream.Write("0xFFFF, ");
            // .paletteTag1
            stream.Write("OBJ_EVENT_PAL_TAG_" + palId + ", ");
            // .paletteTag2
            stream.Write(info.ReflectionPalette + ", ");
            // .size
            stream.Write(((info.Width * info.Height * 4) / 8) + ", ");
            // .width
            stream.Write(info.Width + ", ");
            // .height
            stream.Write(info.Height + ", ");
            // .paletteSlot
            stream.Write(info.PaletteSlot + ", ");
            // .shadowSize
            stream.Write("SHADOW_SIZE_" + info.ShadowSize + ", ");
            // .inanimate
            stream.Write((info.Inanimate ? "TRUE" : "FALSE") + ", ");
            // .disableReflectionPaletteLoad
            stream.Write((info.EnableReflectionPaletteLoad ? "FALSE" : "TRUE") + ", ");
            // .tracks
            stream.Write("TRACKS_" + info.Tracks + ", ");
            // .oam
            stream.Write("&gObjectEventBaseOam_" + roundOamSize(info.Width) + "x" + roundOamSize(info.Height) + ", ");
            // .subspriteTables
            stream.Write("gObjectEventSpriteOamTables_" + info.Width + "x" + info.Height + ", ");
            // .anims
            stream.Write(info.Animations.Identifier + ", ");
            // .images
            stream.Write(info.PicTable.CppVar + ", ");
            // .affineAnims
            stream.Write(info.AffineAnimations);
            stream.WriteLine("};\n");
          }
        }, "src", "data", "object_events", "object_event_graphics_info.h");
      }
      void savePicsAndPalettes(ProjectSerializer serializer) {
        string[] curLines = File.ReadAllLines(Path.Combine(serializer.project.ProjectDir, "src", "data", "object_events", "object_event_graphics.h"));
        serializer.serializeFile(stream => {
          // Copy the existing lines unrelated to even palettes and pictures.
          foreach (string str in curLines) {
            if (!str.StartsWith("const u32 gObjectEventPic_") && !str.StartsWith("const u16 gObjectEventPalette"))
              stream.WriteLine(str);
          }

          foreach (EventObjectPic pic in database.pics) {
            stream.WriteLine("const u32 gObjectEventPic_" + pic.Identifier +
                             "[] = INCBIN_U32(\"graphics/object_events/pics/" + pic.Path + ".4bpp\");");
          }

          foreach (KeyValuePair<string, EventObjectPalette> palAndPath in database.varToPalette) {
            stream.WriteLine("const u16 " + palAndPath.Key + "[] = INCBIN_U16(\"" +
                             Path.ChangeExtension(palAndPath.Value.FilePath, ".gbapal") + "\");");
          }
          foreach (KeyValuePair<EventObjectPic, EventObjectPalette> picAndPal in picPalettes) {
            stream.WriteLine("const u16 " + picAndPal.Value.CppVariable + "[] = INCBIN_U16(\"" +
                             Path.ChangeExtension(picAndPal.Value.FilePath, ".gbapal") + "\");");
          }
        }, "src", "data", "object_events", "object_event_graphics.h");

        // Check to see if any of the pics changed location.
        foreach (EventObjectPic pic in database.pics) {
          string fullPrettyPath = Path.Combine(serializer.project.ProjectDir, "graphics/object_events/pics/", pic.Path + ".png");
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
