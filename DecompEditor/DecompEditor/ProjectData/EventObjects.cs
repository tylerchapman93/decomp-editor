using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Truncon.Collections;
using DecompEditor.ProjectData;
using System.Drawing;
using System.Windows.Controls.Primitives;
using System.Text.Json;

namespace DecompEditor {
  public class EventObject : ObservableObject {
    private string identifier;
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
    private string affineAnimations;
    private ObservableCollection<Frame> frames;

    public EventObject() => Frames = new ObservableCollection<Frame>();

    /// <summary>
    /// This class represents a single frame of an object event pic table.
    /// </summary>
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

    public string Identifier {
      get => identifier;
      set {
        if (identifier != null) {
          Project.Instance.registerFileReplacement(
            "OBJ_EVENT_GFX_" + identifier, "OBJ_EVENT_GFX_" + value);
        }

        Set(ref identifier, value);
      }
    }
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
    public string AffineAnimations { get => affineAnimations; set => Set(ref affineAnimations, value); }
    public ObservableCollection<Frame> Frames {
      get => frames; set => SetAndTrackItemUpdates(ref frames, value, this);
    }
  }
  public class EventObjectAnimTable {
    public string Identifier { get; set; }
    public string PrettyName { get; set; }
  }
  public class EventObjectPalette {
    public static EventObjectPalette GenerateFromFileInst = new EventObjectPalette() {
      Identifier = "<Generate From Image>",
      FilePath = string.Empty
    };

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
  public class EventObjectDatabase : DatabaseBase {
    ObservableCollection<EventObjectPic> pics;
    readonly OrderedDictionary<string, EventObjectPalette> varToPalette = new OrderedDictionary<string, EventObjectPalette>();
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
      Objects.Clear();
      Pics.Clear();
      ShadowSizes.Clear();
      TrackTypes.Clear();
      varToPalette.Clear();
    }

    protected override void deserialize(ProjectDeserializer serializer) {

    }
    protected override void serialize(ProjectSerializer serializer) {
      Serializer.serialize(serializer, this);
    }

    protected override bool checkAndUpgrade(ProjectDeserializer deserializer) {
      // If the json file exists, this project has the new format.
      bool hasNewFormat = File.Exists(Path.Combine(Project.Instance.ProjectDir, "src", "data", "event_objects.json"));
      if (hasNewFormat)
        return false;

      // Otherwise, convert to the new format.
      ProjectData.OldFormat.EventObjects.Converter converter = new ProjectData.OldFormat.EventObjects.Converter(animTables, pics, ShadowSizes, TrackTypes, varToPalette);
      converter.convert(deserializer, Objects);
      return true;
    }

    static class Serializer {
      public class JSONDatabase {
        public JSONDatabase(EventObjectDatabase database) {
          int paletteTag = 0;
          Palettes = database.varToPalette.Values.Select(palette => new JSONEventObjectPalette() {
            Identifier = palette.Identifier,
            Path = palette.FilePath,
            Tag = paletteTag++
          }).ToArray();

          Dictionary<EventObjectPic, JSONEventObjectPic> idenToPic = new Dictionary<EventObjectPic, JSONEventObjectPic>();
          foreach (var pic in database.Pics) {
            var jsonPic = new JSONEventObjectPic() {
              Identifier = pic.Identifier,
              Path = pic.Path
            };
            idenToPic.Add(pic, jsonPic);
          }
          Pics = idenToPic.Values.ToArray();

          EventObjects = new JSONEventObject[database.Objects.Count];
          for (int i = 0; i != EventObjects.Length; ++i) {
            EventObject obj = database.Objects[i];
            EventObjects[i] = new JSONEventObject(obj);

            // If this object generates its palette, inform the first picture generate a palette.
            if (obj.Palette == EventObjectPalette.GenerateFromFileInst)
              idenToPic[obj.Frames[0].Pic].PaletteTag = paletteTag++;
          }
        }

        public class JSONEventObjectPic {
          public string Identifier { get; set; }
          public string Path { get; set; }
          public int? PaletteTag { get; set; }
        }
        public class JSONEventObjectPalette {
          public string Identifier { get; set; }
          public string Path { get; set; }
          public int Tag { get; set; }
        }
        public class JSONEventObject {
          public class JSONFrame {
            public JSONFrame(EventObject.Frame frame) {
              Identifier = frame.Pic.Identifier;
              StartIndex = frame.Index == 0 ? null : new int?(frame.Index);
              Count = null;
            }

            public string Identifier { get; set; }
            public int? StartIndex { get; set; } = 0;
            public int? Count { get; set; } = 1;
          }

          public JSONEventObject(EventObject obj) {
            Identifier = obj.Identifier;
            if (obj.Palette != EventObjectPalette.GenerateFromFileInst)
              Palette = obj.Palette.Identifier;
            ReflectionPalette = getOrNullClass(ReflectionPalette, obj.ReflectionPalette);

            Width = obj.Width;
            Height = obj.Height;
            PaletteSlot = getOrNullValue((int)PaletteSlot, obj.PaletteSlot);
            ShadowSize = getOrNullClass(ShadowSize, obj.ShadowSize);
            Inanimate = getOrNullValue((bool)Inanimate, obj.Inanimate);
            EnableReflectionPaletteLoad = getOrNullValue((bool)EnableReflectionPaletteLoad, obj.EnableReflectionPaletteLoad);
            Tracks = getOrNullClass(Tracks, obj.Tracks);
            Animations = obj.Animations.Identifier;
            AffineAnimations = getOrNullClass(AffineAnimations, obj.AffineAnimations);

            for (int i = 0; i != obj.Frames.Count; ++i) {
              if (i != 0) {
                if (obj.Frames[i - 1].Pic == obj.Frames[i].Pic &&
                    obj.Frames[i - 1].Index == obj.Frames[i].Index - 1) {
                  if (Frames[^1].Count == null)
                    Frames[^1].Count = 2;
                  else
                    ++Frames[^1].Count;
                  continue;
                }
              }
              Frames.Add(new JSONFrame(obj.Frames[i]));
            }
          }

          /// <summary>
          /// If the given values are equal this method returns null, otherwise returns the new value.
          /// </summary>
          Nullable<T> getOrNullValue<T>(T value, T newValue) where T : struct {
            return EqualityComparer<T>.Default.Equals(value, newValue) ? null : new T?(newValue);
          }
          T getOrNullClass<T>(T value, T newValue) where T : class => EqualityComparer<T>.Default.Equals(value, newValue) ? null : newValue;

          public string Identifier { get; set; }
          public string Palette { get; set; }
          public string ReflectionPalette { get; set; } = "OBJ_EVENT_PAL_TAG_NONE";
          public int Width { get; set; }
          public int Height { get; set; }
          public int? PaletteSlot { get; set; } = 0;
          public string ShadowSize { get; set; } = "M";
          public bool? Inanimate { get; set; } = false;
          public bool? EnableReflectionPaletteLoad { get; set; } = false;
          public string Tracks { get; set; } = "FOOT";
          public string Animations { get; set; }
          public string AffineAnimations { get; set; } = "gDummySpriteAffineAnimTable";
          public List<JSONFrame> Frames { get; set; } = new List<JSONFrame>();
        }
        public JSONEventObject[] EventObjects { get; set; }
        public JSONEventObjectPalette[] Palettes { get; set; }
        public JSONEventObjectPic[] Pics { get; set; }
      }

      public static void serialize(ProjectSerializer serializer, EventObjectDatabase database) {
        updateSpriteSheetMakeRules(serializer, database);
        moveGraphicsPics(serializer, database);

        // Generate and save a JSON database.
        JSONDatabase jsonDatabase = new JSONDatabase(database);
        string json = JsonSerializer.Serialize(jsonDatabase, new JsonSerializerOptions() {
          IgnoreNullValues = true,
          WriteIndented = true
        });
        File.WriteAllText(Path.Combine(serializer.project.ProjectDir, "src", "data", "object_events", "event_objects.json"), json);
      }

      static void updateSpriteSheetMakeRules(ProjectSerializer serializer, EventObjectDatabase database) {
        var checkedFiles = new HashSet<EventObjectPic>();
        var checkedFilePaths = new HashSet<string>();
        var needSprites = new Dictionary<string, Tuple<int, int>>();
        foreach (EventObject obj in database.Objects) {
          int objWidth = obj.Width, objHeight = obj.Height;
          foreach (EventObject.Frame frame in obj.Frames) {
            if (!checkedFiles.Add(frame.Pic))
              continue;
            checkedFilePaths.Add(frame.Pic.Path);

            BitmapImage file = FileUtils.loadBitmapImage(frame.Pic.FullPath);
            if (file.PixelWidth != objWidth || file.PixelHeight != objHeight)
              needSprites.Add(frame.Pic.Path, new Tuple<int, int>(objWidth / 8, objHeight / 8));
          }
        }
        string[] curLines = File.ReadAllLines(Path.Combine(serializer.project.ProjectDir, "spritesheet_rules.mk"));
        bool updatedExistingSheet = false;
        for (int i = 0, e = curLines.Length; i != e; ++i) {
          if (!curLines[i].tryExtractPrefix("$(OBJEVENTGFXDIR)/", ".", out string linePath) ||
              !checkedFilePaths.Contains(linePath))
            continue;

          // If the sprite doesn't need a sprite sheet, remove it.
          if (!needSprites.TryGetValue(linePath, out Tuple<int, int> widthHeight)) {
            // TODO: We could also just remove the lines completely, i.e. null them out.
            curLines[i] = curLines[++i] = string.Empty;
            updatedExistingSheet = true;
            continue;
          }
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
      static void moveGraphicsPics(ProjectSerializer serializer, EventObjectDatabase database) {
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
