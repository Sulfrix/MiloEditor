using System.Numerics;
using System.Runtime.InteropServices;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using IconFonts;
using ImGuiNET;
using ManagedBass;
using MiloLib.Assets;
using TinyDialogsNet;

namespace ImMilo;

public class SamplePlayer
{
    
    public class CuePlayer
    {
        public List<SamplePlayer> players = new();
        public Dictionary<SamplePlayer, Sfx.SfxMap> playerToMap = new();
        public Sfx sfx;

        public uint longestSample;

        public CuePlayer(Sfx sfx)
        {
            this.sfx = sfx;
            LoadSamples();

        }

        [DllImport("bass", EntryPoint = "BASS_ChannelStart")]
        static extern bool BassChannelStart(int handle);

        public void LoadSamples()
        {
            players.Clear();
            playerToMap.Clear();
            longestSample = 0;
            var loadTasks = new List<Task>();
            foreach (var map in sfx.sfxMaps)
            {
                var sample = ObjectLocation.FindObject<SynthSample>(map.sampleName, sfx);
                if (sample != null)
                {
                    var player = new SamplePlayer(sample, false);
                    players.Add(player);
                    playerToMap[player] = map;
                    player.LoadData();
                    if (sample.sampleData.sampleCount > longestSample)
                    {
                        longestSample = sample.sampleData.sampleCount;
                    }
                }
            }
            
            foreach (var player in players)
            {
                if (player.dataState != State.Loaded)
                {
                    throw new Exception("Sample is not loaded yet!");
                }
                Bass.ChannelSetAttribute(player.bassChannel, ChannelAttribute.Pan, playerToMap[player].pan);
                if (player != players.First())
                {
                    Console.WriteLine($"Linking {players.First().bassChannel} and {player.bassChannel}");
                    if (!Bass.ChannelSetLink(players.First().bassChannel, player.bassChannel))
                    {
                        Console.WriteLine("Channel link failed: " + Bass.LastError);
                    }
                }
            }
        }

        public void Render()
        {
            if (players.Count > 0)
            {
                if (ImGui.Button("Play"))
                {
                    foreach (var player in players)
                    {
                        Bass.ChannelSetPosition(player.bassChannel, 0);
                    }
                    BassChannelStart(players.First().bassChannel);
                }

                ImGui.SameLine();
                if (ImGui.Button("Reload"))
                {
                    LoadSamples();
                }
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                ImGui.BeginChild("samples", new Vector2(ImGui.GetContentRegionAvail().X, players.Count * SamplePlayerHeight), ImGuiChildFlags.Borders);
                foreach (var player in players)
                {
                    player.DrawWaveform((float)player.thisSample.sampleData.sampleCount / longestSample);
                    ImGui.Dummy(new Vector2(20, SamplePlayerHeight));
                }
                ImGui.EndChild();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.Dummy(ImGui.GetStyle().ItemSpacing/2);
            }
        }
    }
    
    private static Dictionary<SynthSample, SamplePlayer> players = new();
    private static Dictionary<Sfx, CuePlayer> cuePlayers = new();

    private const int DataPadding = 0;
    private const int SamplePlayerHeight = 30;

    private SynthSample thisSample;
    private short[] sampleData;

    enum State
    {
        NotLoaded,
        Loaded,
        Failed
    }
    
    private State dataState = State.NotLoaded;
    private int bassSample;
    private int bassChannel;

    public SamplePlayer(SynthSample sample, bool loadData = true)
    {
        thisSample = sample;
        if (loadData)
        {
            Task.Run(LoadData);
        }
    }

    public void LoadData()
    {
        Bass.SampleFree(bassSample);
        sampleData = [];
        switch (thisSample.sampleData.encoding)
        {
            case SynthSample.SampleData.Encoding.kBigEndPCM:
            case SynthSample.SampleData.Encoding.kPCM:
                sampleData = new short[(thisSample.sampleData.sampleCount) + DataPadding];
                var rawData = thisSample.sampleData.samples;
                for (int i = 0; i < thisSample.sampleData.sampleCount; i++)
                {
                    var byteIndex = i * 2;
                    Console.WriteLine(byteIndex);
                    short sample = 0;
                    switch (thisSample.sampleData.encoding)
                    {
                        case SynthSample.SampleData.Encoding.kBigEndPCM:
                            sample = BitConverter.ToInt16([rawData[byteIndex + 1], rawData[byteIndex]]);
                            break;
                        case SynthSample.SampleData.Encoding.kPCM:
                            sample = BitConverter.ToInt16([rawData[byteIndex], rawData[byteIndex + 1]]);
                            break;
                    }
                    sampleData[i] = sample;
                }
                bassSample = Bass.CreateSample(sampleData.Length * 2, (int)thisSample.sampleData.sampleRate, 1, 1, 0);
                break;
            case SynthSample.SampleData.Encoding.kMP3:
                var rawMp3Data = thisSample.sampleData.samples.ToArray();
                bassSample = Bass.SampleLoad(rawMp3Data, 0, rawMp3Data.Length, 1, BassFlags.Mono);
                if (bassSample == 0)
                {
                    Console.WriteLine("Sample failed to load: " + Bass.LastError.ToString());
                    dataState = State.Failed;
                    return;
                }
                var info = Bass.SampleGetInfo(bassSample);
                var samples = new byte[info.Length];
                Bass.SampleGetData(bassSample, samples);
                sampleData = new short[info.Length / 2];
                for (int i = 0; i < info.Length / 2; i++)
                {
                    var byteIndex = i * 2;
                    sampleData[i] = BitConverter.ToInt16([samples[byteIndex], samples[byteIndex + 1]]);
                }
                break;
        }
        
        bassChannel = Bass.SampleGetChannel(bassSample, BassFlags.SampleChannelStream);
        Bass.SampleSetData(bassSample, sampleData);
        Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.SampleRateConversion, 0);
        dataState = State.Loaded;
    }
    
    public static void Draw(SynthSample sample)
    {
        if (!players.TryGetValue(sample, out var player))
        {
            players[sample] = new SamplePlayer(sample);
            player = players[sample];
        }
        player.Render();
    }

    public static void Draw(Sfx sfx)
    {
        if (!cuePlayers.TryGetValue(sfx, out var player))
        {
            cuePlayers[sfx] = new CuePlayer(sfx);
            player = cuePlayers[sfx];
        }

        player.Render();
    }

    public void Render()
    {
        ImGui.Button(FontAwesome5.EllipsisH, new Vector2(SamplePlayerHeight, SamplePlayerHeight));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().FramePadding);
        if (ImGui.BeginPopupContextItem("SampleActions", ImGuiPopupFlags.MouseButtonLeft))
        {
            if (ImGui.MenuItem(FontAwesome5.FileImport + "  Import Audio File"))
            {
                var (cancelled, path) = TinyDialogs.OpenFileDialog("Select an audio file.");
                if (!cancelled)
                {
                    
                    var probe = FFProbe.Analyse(path.First());
                    var rate = probe.AudioStreams.First().SampleRateHz;
                    var memoryStream = new MemoryStream();
                    FFMpegArguments.FromFileInput(path.First())
                        .OutputToPipe(new StreamPipeSink(memoryStream), options => options
                            .WithAudioCodec("pcm_s16be").ForceFormat("s16be").WithAudioSamplingRate(rate).WithCustomArgument("-ac 1")).WithLogLevel(FFMpegLogLevel.Info).ProcessSynchronously();
                    
                    Console.WriteLine($"Using sample rate {rate} from file");

                    var data = memoryStream.ToArray();
                    thisSample.sampleData.samples = [..data];
                    thisSample.sampleData.sampleRate = (uint)rate;
                    thisSample.sampleData.sampleCount = (uint)data.Length / 2;
                    thisSample.sampleData.encoding = SynthSample.SampleData.Encoding.kBigEndPCM;
                    LoadData();
                }
            }

            if (ImGui.MenuItem("\uf01e  Reload"))
            {
                LoadData();
            }
            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(3, 0));

        ImGui.SameLine();
        ImGui.PopStyleVar();
        switch (dataState)
        {
            case State.NotLoaded:
                ImGui.Text("Loading sample data...");
                return;
            case State.Failed:
                ImGui.Text("Failed to load sample data.");
                return;
        }

        var channelStatus = Bass.ChannelIsActive(bassChannel);
        var contentSize = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.BeginChild("##sampleData", new Vector2(contentSize.X, SamplePlayerHeight), ImGuiChildFlags.Borders);

        if (ImGui.Button(channelStatus == PlaybackState.Playing ? FontAwesome5.StepBackward : FontAwesome5.Play, new Vector2(SamplePlayerHeight, SamplePlayerHeight)))
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                Bass.ChannelStop(bassChannel);
            }
            else
            {
                Bass.ChannelPlay(bassChannel, true);
            }
        }

        ImGui.SameLine();
        DrawWaveform();
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleVar();
    }

    public void DrawWaveform(float widthMult = 1)
    {
        var channelStatus = Bass.ChannelIsActive(bassChannel);
        var contentSize = ImGui.GetContentRegionAvail();
        contentSize.X *= widthMult;
        contentSize.X = MathF.Floor(contentSize.X);
        var drawList = ImGui.GetWindowDrawList();
        var progress = (float)Bass.ChannelGetPosition(bassChannel) / Bass.ChannelGetLength(bassChannel);
        var playheadXPos = (contentSize.X * progress);
        if (channelStatus == PlaybackState.Playing)
        {
            drawList.AddRectFilled(ImGui.GetCursorScreenPos()+new Vector2(playheadXPos, 0), ImGui.GetCursorScreenPos()+new Vector2(playheadXPos+2, SamplePlayerHeight), ImGui.GetColorU32(ImGuiCol.Text));
        }
        
        for (int i = 0; i < contentSize.X; i++)
        {
            var fracStart = i / contentSize.X;
            var fracEnd = (i + 1) / contentSize.X;
            var xPos = (contentSize.X * fracStart);
            int indexStart = (int)(sampleData.Length * fracStart);
            int indexEnd = (int)(sampleData.Length * fracEnd);
            var maxAmp = 0;
            for (int j = indexStart; j < indexEnd; j++)
            {
                if (Math.Abs((int)sampleData[j]) > maxAmp)
                {
                    maxAmp = sampleData[j];
                }
            }

            var maxAmpFloat = (float)maxAmp / short.MaxValue;
            if (fracStart < progress)
            {
                drawList.AddLine(ImGui.GetCursorScreenPos()+new Vector2(xPos, (SamplePlayerHeight/2)-(maxAmpFloat*(SamplePlayerHeight/2))), ImGui.GetCursorScreenPos()+new Vector2(xPos, (SamplePlayerHeight/2)+(maxAmpFloat*(SamplePlayerHeight/2))), 0xff0000ff);
            }
            else
            {
                drawList.AddLine(ImGui.GetCursorScreenPos()+new Vector2(xPos, (SamplePlayerHeight/2)-(maxAmpFloat*(SamplePlayerHeight/2))), ImGui.GetCursorScreenPos()+new Vector2(xPos, (SamplePlayerHeight/2)+(maxAmpFloat*(SamplePlayerHeight/2))), ImGui.GetColorU32(ImGuiCol.Text));
            }
            

        }
    }
}