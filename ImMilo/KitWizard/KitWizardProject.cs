using System.Text.Json;
using System.Text.Json.Serialization;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using MiloLib;
using MiloLib.Assets;
using MiloLib.Assets.Synth;

namespace ImMilo;

public class KitWizardProject
{

    [JsonIgnore]
    public Dictionary<string, SynthSample[]> sampleCache = new();
    
    [JsonIgnore]
    public string folder;

    public string KitName;
    public int SampleRate = 32000;
    
    private static JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

    [JsonIgnore] public FileSystemWatcher watcher;
    [JsonIgnore] public MiloFile miloFile;
    
    public KitWizardProject(string folder)
    {
        this.folder = folder;
    }
    
    public KitWizardProject() {}

    public string GetJsonPath()
    {
        return Path.Join(folder, "kitwizard.json");
    }

    public void InitWatches()
    {
        watcher = new FileSystemWatcher(folder);

        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName;

        watcher.Changed += WatcherOnChanged;
        watcher.Created += WatcherOnChanged;
        watcher.Deleted += WatcherOnChanged;
        watcher.Renamed += WatcherOnChanged;

        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        UpdateKit();
    }

    public void UpdateKit()
    {
        miloFile = new MiloFile(DirectoryMeta.New("ObjectDir", KitName, 28, 27));
        foreach (var drum in KitWizard.drums)
        {
            var randomGroupSeq = new RandomGroupSeq();
            randomGroupSeq.revision = 2;
            randomGroupSeq.numSimultaneous = 1;
            randomGroupSeq.allowRepeats = true;
            miloFile.dirMeta.entries.Add(new DirectoryMeta.Entry("RandomGroupSeq", drum + ".cue", randomGroupSeq));

            foreach (var file in Directory.GetFiles(Path.Join(folder, drum)))
            {
                GetCachedSamples(file);
            } 
        }
    }

    public async Task<SynthSample[]> GetCachedSamples(string path)
    {
        path = Path.GetFullPath(path);
        if (sampleCache.ContainsKey(path))
        {
            return sampleCache[path];
        }
        else
        {
            var mediaInfo = await FFProbe.AnalyseAsync(path);
            
            var samples = new List<SynthSample>();
            for (int i = 0; i < mediaInfo.AudioStreams.First().Channels; i++)
            {
                var outStream = new MemoryStream();
                FFMpegArguments.FromFileInput(path).OutputToPipe(new StreamPipeSink(outStream), options => options
                    .WithAudioCodec("pcm_s16be")
                    .WithAudioSamplingRate(SampleRate)
                    .SelectStream(mediaInfo.AudioStreams.First().Index, 0, Channel.Audio)
                    .WithAudioFilters(filterOptions => filterOptions.Pan(1, ["c0=c" + i])))
                    .ProcessSynchronously();
                var sample = new SynthSample();
                sample.revision = 6;
                sample.sampleData.revision = 14;
                sample.sampleData.samples = new List<byte>(outStream.ToArray());
                sample.sampleData.sampleCount = (uint)(outStream.Length / 2);
                sample.sampleData.sampleRate = (uint)SampleRate;
                sample.sampleData.encoding = SynthSample.SampleData.Encoding.kBigEndPCM;
                samples.Add(sample);
            }
            
            sampleCache[path] = samples.ToArray();
            return sampleCache[path];
        }
    }

    public void Save()
    {
        File.WriteAllText(GetJsonPath(), JsonSerializer.Serialize(this, options));
        foreach (var drum in KitWizard.drums)
        {
            EnsureDirectory(drum);
        }
    }

    public static KitWizardProject Load(string path)
    {
        var loaded = JsonSerializer.Deserialize<KitWizardProject>(File.ReadAllText(path), options);
        loaded.folder = Path.GetDirectoryName(path);
        loaded.UpdateKit();
        loaded.InitWatches();
        return loaded;
    }

    void EnsureDirectory(string folderName)
    {
        var path = Path.Join(folder, folderName);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}