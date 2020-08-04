using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Truncon.Collections;

namespace DecompEditor.ProjectData.OldFormat {
  class EventObjects {
    public class OldGraphicsInfo {
      public string CppVariable { get; set; }
      public EventObjectPalette Palette { get; set; }
      public string ReflectionPalette { get; set; }
      public int Width { get; set; }
      public int Height { get; set; }
      public int PaletteSlot { get; set; }
      public string ShadowSize { get; set; }
      public bool Inanimate { get; set; }
      public bool EnableReflectionPaletteLoad { get; set; }
      public string Tracks { get; set; }
      public EventObjectAnimTable Animations { get; set; }
      public OldEventObjectPicTable PicTable { get; set; }
      public string AffineAnimations { get; set; }
    }
    public class OldEventObject {
      public string Identifier { get; set; }
      public OldGraphicsInfo Info { get; set; }
    }
    public class OldEventObjectPicTable {
      public string CppVar { get; set; }
      public List<EventObject.Frame> Frames { get; set; } = new List<EventObject.Frame>();
    }
    class Deserializer {
      class PicTableMap {
        readonly Dictionary<string, OldEventObjectPicTable> varToPicTable = new Dictionary<string, OldEventObjectPicTable>();
        readonly Dictionary<string, int> picVarCount = new Dictionary<string, int>();
        public void Add(OldEventObjectPicTable table) => varToPicTable.Add(table.CppVar, table);
        public OldEventObjectPicTable GetValue(string cppVar) {
          if (!varToPicTable.TryGetValue(cppVar, out OldEventObjectPicTable table))
            return new OldEventObjectPicTable() { CppVar = cppVar };
          if (picVarCount.TryGetValue(cppVar, out int picCount)) {
            // If the table has already been accessed, clone it so that each info gets its own
            // table. This just makes it easier to edit.
            picVarCount[cppVar] = ++picCount;
            table = new OldEventObjectPicTable() {
              CppVar = cppVar + "_" + picCount,
              Frames = table.Frames.Select(frame => new EventObject.Frame(frame)).ToList()
            };
          } else {
            picVarCount.Add(cppVar, ++picCount);
          }

          return table;
        }
      };
      readonly Dictionary<string, OldGraphicsInfo> cppVarToGraphicsInfo = new Dictionary<string, OldGraphicsInfo>();
      readonly Dictionary<string, EventObjectAnimTable> varToAnimTable = new Dictionary<string, EventObjectAnimTable>();
      readonly Dictionary<string, EventObjectPalette> idToPalette = new Dictionary<string, EventObjectPalette>();
      readonly Dictionary<string, EventObjectPic> idToPic = new Dictionary<string, EventObjectPic>();
      readonly PicTableMap varToPicTable = new PicTableMap();

      List<EventObjectAnimTable> animTables;
      List<OldGraphicsInfo> nonObjectGraphicsInfos;
      List<OldEventObject> objects;
      ObservableCollection<EventObjectPic> pics;
      List<string> shadowSizes;
      List<string> trackTypes;
      OrderedDictionary<string, EventObjectPalette> varToPalette;

      public Deserializer(List<EventObjectAnimTable> animTables,
                          List<OldGraphicsInfo> nonObjectGraphicsInfos,
                          List<OldEventObject> objects,
                          ObservableCollection<EventObjectPic> pics,
                          List<string> shadowSizes,
                          List<string> trackTypes,
                          OrderedDictionary<string, EventObjectPalette> varToPalette) {
        this.animTables = animTables;
        this.nonObjectGraphicsInfos = nonObjectGraphicsInfos;
        this.objects = objects;
        this.pics = pics;
        this.shadowSizes = shadowSizes;
        this.trackTypes = trackTypes;
        this.varToPalette = varToPalette;
      }

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
          pics.Add(pic);
        }));
        fileDeserializer.add(new IncBinDeserializer("gObjectEventPalette", "u16", (cppVar, fileName) => {
          cppVar = "gObjectEventPalette" + cppVar;
          varToPalette.Add(cppVar, new EventObjectPalette() {
            Identifier = cppVar.fromPascalToSnake(),
            FilePath = fileName
          });
        }));
        deserializer.deserializeFile(fileDeserializer, "src", "data", "object_events", "object_event_graphics.h");

        // Remove all of the palettes whose file paths match a present picture, these
        // palettes will be generated from that image.
        var toRemove = new List<string>();
        foreach (KeyValuePair<string, EventObjectPalette> kv in varToPalette) {
          if (picPaths.Contains(kv.Value.FilePath + ".png"))
            toRemove.Add(kv.Key);
        }
        foreach (string key in toRemove)
          varToPalette.Remove(key);
      }
      void loadPaletteIDs(ProjectDeserializer deserializer) {
        var paletteDeserializer = new InlineStructDeserializer((vals) => {
          string cppVar = vals[0], fullId = vals[1];
          if (!varToPalette.TryGetValue(cppVar, out EventObjectPalette pal)) {
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
            cppVarToGraphicsInfo.Add(cppVar, new OldGraphicsInfo() {
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
          objects.Add(new OldEventObject() {
            Identifier = objectID,
            Info = cppVarToGraphicsInfo[infoVar]
          });
          usedInfos.Add(infoVar);
          return true;
        });
        deserializer.deserializeFile(fileDeserializer, "src", "data", "object_events", "object_event_graphics_info_pointers.h");

        // Check for any graphics info objects that aren't used by event objects.
        foreach (OldGraphicsInfo info in cppVarToGraphicsInfo.Values) {
          if (!usedInfos.Contains(info.CppVariable))
            nonObjectGraphicsInfos.Add(info);
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
          animTables.Add(animTable);
        }, "src", "data", "object_events", "object_event_anims.h");
      }
      void loadPicTables(ProjectDeserializer deserializer) {
        deserializer.deserializeFile((stream) => {
          if (!stream.ReadLine().tryExtractPrefix("const struct SpriteFrameImage gObjectEventPicTable_", "[", out string cppVar))
            return;
          var table = new OldEventObjectPicTable() {
            CppVar = "gObjectEventPicTable_" + cppVar
          };
          do {
            string line = stream.ReadLine().Trim();
            if (line.StartsWith("}"))
              break;
            if (line.StartsWith("obj_frame_tiles")) {
              int skipLen = line.IndexOf('(') + "(gObjectEventPic_".Length;
              table.Frames.Add(new EventObject.Frame() {
                Pic = idToPic[line.Substring(skipLen, line.Length - skipLen - 2)],
              });
              continue;
            }

            line = line.Remove(0, "overworld_frame(gObjectEventPic_".Length);
            string[] elements = line.Remove(line.Length - "),".Length).Split(", ");
            table.Frames.Add(new EventObject.Frame() {
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
          var info = new OldGraphicsInfo() {
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
            shadowSizes.Add(enumName);
          else if (line.tryExtractPrefix("#define TRACKS_", " ", out enumName))
            trackTypes.Add(enumName);
        }, "include", "constants", "event_objects.h");
      }
    }

    public class Converter {
      List<EventObjectAnimTable> animTables;
      List<OldGraphicsInfo> nonObjectGraphicsInfos = new List<OldGraphicsInfo>();
      ObservableCollection<EventObjectPic> pics;
      List<string> shadowSizes;
      List<string> trackTypes;
      OrderedDictionary<string, EventObjectPalette> varToPalette;

      public Converter(List<EventObjectAnimTable> animTables,
                       ObservableCollection<EventObjectPic> pics,
                       List<string> shadowSizes,
                       List<string> trackTypes,
                       OrderedDictionary<string, EventObjectPalette> varToPalette) {
        this.animTables = animTables;
        this.pics = pics;
        this.shadowSizes = shadowSizes;
        this.trackTypes = trackTypes;
        this.varToPalette = varToPalette;
      }

      public void convert(ProjectDeserializer projectDeserializer, ObservableCollection<EventObject> objects) {
        List<OldEventObject> oldObjects = new List<OldEventObject>();

        // Deserialize the project in the old format.
        Deserializer deserializer = new Deserializer(animTables, nonObjectGraphicsInfos, oldObjects, pics, shadowSizes, trackTypes, varToPalette);
        deserializer.deserialize(projectDeserializer);

        // Populate the project with the new format.
        populateWithNewFormat();

        // Convert the old object event format to the proper format.
        foreach (OldEventObject oldObject in oldObjects) {
          objects.Add(new EventObject() {
            Identifier = oldObject.Identifier,
            Palette = oldObject.Info.Palette,
            ReflectionPalette = oldObject.Info.ReflectionPalette,
            Width = oldObject.Info.Width,
            Height = oldObject.Info.Height,
            PaletteSlot = oldObject.Info.PaletteSlot,
            ShadowSize = oldObject.Info.ShadowSize,
            Inanimate = oldObject.Info.Inanimate,
            EnableReflectionPaletteLoad = oldObject.Info.EnableReflectionPaletteLoad,
            Tracks = oldObject.Info.Tracks,
            Animations = oldObject.Info.Animations,
            AffineAnimations = oldObject.Info.AffineAnimations,
            Frames = new ObservableCollection<EventObject.Frame>(oldObject.Info.PicTable.Frames)
          });
        }
      }

      /// <summary>
      /// Overwrite the existing game mechanisms and use the new format for event objects instead.
      /// </summary>
      void populateWithNewFormat() {
        string projectDir = Project.Instance.ProjectDir;

        // Remove the direct use of Palette IDs.
        string eventObjectMovementPath = Path.Combine(projectDir, "src", "event_object_movement.c");
        List<string> curLines = File.ReadAllLines(eventObjectMovementPath).ToList();
        curLines.RemoveAll(line => line.StartsWith("#define OBJ_EVENT_PAL_TAG_") && !line.Contains("OBJ_EVENT_PAL_TAG_NONE"));
        int paletteArrayIndex = curLines.FindIndex(line => line.EndsWith("sObjectEventSpritePalettes[] = {"));
        curLines.RemoveRange(paletteArrayIndex, (curLines.FindIndex(paletteArrayIndex, line => line.StartsWith("}")) + 1) - paletteArrayIndex);
        File.WriteAllLines(eventObjectMovementPath, curLines);

        // Remove the direct use of object graphics ids.
        string eventObjectConstantsPath = Path.Combine(projectDir, "include", "constants", "event_objects.h");
        curLines = File.ReadAllLines(eventObjectConstantsPath).ToList();
        curLines.RemoveRange(2, curLines.FindIndex(str => str.Contains("NUM_OBJ_EVENT_GFX")) - 1);
        curLines.InsertRange(3, new string[] {
          "#include \"event_objects.h.inc\""
        });
        File.WriteAllLines(eventObjectConstantsPath, curLines);

        // Generate the file for object_event_graphics_info_pointers.h.inc
        using (StreamWriter stream = new StreamWriter(eventObjectConstantsPath + ".json.txt")) {
          stream.NewLine = "\n";
          stream.Write(FileUtils.readResource("event_objects.json.txt"));
        }

        // Remove the direct use of graphics info pointers.
        string eventObjectInfoPointersPath = Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info_pointers.h");
        curLines = File.ReadAllLines(eventObjectInfoPointersPath).ToList();
        using (StreamWriter stream = new StreamWriter(eventObjectInfoPointersPath)) {
          stream.NewLine = "\n";
          stream.WriteLine("#include \"object_event_graphics_info_pointers.h.inc\"\n");

          // Write back the graphics infos that don't relate to objects.
          foreach (OldGraphicsInfo info in nonObjectGraphicsInfos)
            stream.WriteLine("const struct ObjectEventGraphicsInfo " + info.CppVariable + ";");
          foreach (string str in curLines.Skip(curLines.FindIndex(str => str.StartsWith("}")) + 1))
            stream.WriteLine(str);
        }

        // Generate the file for object_event_graphics_info_pointers.h.inc
        using (StreamWriter stream = new StreamWriter(eventObjectInfoPointersPath + ".json.txt")) {
          stream.NewLine = "\n";
          stream.Write(FileUtils.readResource("object_event_graphics_info_pointers.json.txt"));
        }

        // Remove the use of object event pic tables.
        string eventObjectInfoPicTablePath = Path.Combine(projectDir, "src", "data", "object_events", "object_event_pic_tables.h");
        using (StreamWriter stream = new StreamWriter(eventObjectInfoPicTablePath)) {
          stream.NewLine = "\n";
          stream.WriteLine("#include \"object_event_pic_tables.h.inc\"\n");

          // Write out the non object infos.
          foreach (OldGraphicsInfo info in nonObjectGraphicsInfos) {
            if (info.PicTable.Frames.Count == 0)
              continue;
            stream.WriteLine("const struct SpriteFrameImage " + info.PicTable.CppVar + "[] = {");
            int width = info.Width / 8, height = info.Height / 8;
            foreach (EventObject.Frame frame in info.PicTable.Frames) {
              if (info.PicTable.Frames.Count == 1) {
                stream.WriteLine("    obj_frame_tiles(gObjectEventPic_" + frame.Pic.Identifier + "),");
              } else {
                stream.WriteLine("    overworld_frame(gObjectEventPic_" + frame.Pic.Identifier + ", " + width +
                                 ", " + height + ", " + frame.Index + "),");
              }
            }
            stream.WriteLine("};\n");
          }
        }

        // Generate the file for object_event_graphics_info_pointers.h.inc
        using (StreamWriter stream = new StreamWriter(eventObjectInfoPicTablePath + ".json.txt")) {
          stream.NewLine = "\n";
          stream.Write(FileUtils.readResource("object_event_pic_tables.json.txt"));
        }

        // Remove the use of object event graphics infos.
        string eventObjectInfoGraphicsInfoPath = Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics_info.h");
        curLines = File.ReadAllLines(eventObjectInfoGraphicsInfoPath).ToList();
        curLines.RemoveAll(str => !nonObjectGraphicsInfos.Any(info => str.Contains(info.CppVariable)));
        curLines.InsertRange(0, new string[] {
          "#include \"object_event_graphics_info.h.inc\"\n"
        });
        File.WriteAllLines(eventObjectInfoGraphicsInfoPath, curLines);

        // Generate the file for object_event_graphics_info_pointers.h.inc
        using (StreamWriter stream = new StreamWriter(eventObjectInfoGraphicsInfoPath + ".json.txt")) {
          stream.NewLine = "\n";
          stream.Write(FileUtils.readResource("object_event_graphics_info.json.txt"));
        }

        // Remove the use of object event graphics pics and palettes.
        string eventObjectInfoGraphicsPath = Path.Combine(projectDir, "src", "data", "object_events", "object_event_graphics.h");
        curLines = File.ReadAllLines(eventObjectInfoGraphicsPath).ToList();
        curLines.InsertRange(0, new string[] {
          "#include \"object_event_graphics.h.inc\"\n"
        });
        curLines.RemoveAll(str => str.StartsWith("const u32 gObjectEventPic_") || str.StartsWith("const u16 gObjectEventPalette"));
        File.WriteAllLines(eventObjectInfoGraphicsPath, curLines);

        // Generate the file for object_event_graphics_info_pointers.h.inc
        using (StreamWriter stream = new StreamWriter(eventObjectInfoGraphicsPath + ".json.txt")) {
          stream.NewLine = "\n";
          stream.Write(FileUtils.readResource("object_event_graphics.json.txt"));
        }

        // Update the json_data_rules makefile.
        string jsonDataRulesPath = Path.Combine(projectDir, "json_data_rules.mk");
        using (StreamWriter stream = new StreamWriter(jsonDataRulesPath, /*append=*/true)) {
          stream.Write(FileUtils.readResource("json_data_rules.txt"));
        }
      }
    }
  }
}
