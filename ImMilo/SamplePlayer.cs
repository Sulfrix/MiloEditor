using System.Numerics;
using IconFonts;
using ImGuiNET;
using ManagedBass;
using MiloLib.Assets;

namespace ImMilo;

public class SamplePlayer
{
    private static Dictionary<SynthSample, SamplePlayer> players = new();

    private const int DataPadding = 0;

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

    public SamplePlayer(SynthSample sample)
    {
        thisSample = sample;
        LoadData();
    }

    public async void LoadData()
    {
        switch (thisSample.sampleData.encoding)
        {
            case SynthSample.SampleData.Encoding.kBigEndPCM:
            case SynthSample.SampleData.Encoding.kPCM:
                sampleData = new short[(thisSample.sampleData.sampleCount / 2) + DataPadding];
                var rawData = thisSample.sampleData.samples;
                for (int i = 0; i < thisSample.sampleData.sampleCount/2; i++)
                {
                    var byteIndex = i * 2;
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
        
        bassChannel = Bass.SampleGetChannel(bassSample, 0);
        Bass.SampleSetData(bassSample, sampleData);
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

    public void Render()
    {
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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        ImGui.BeginChild("##sampleData", new Vector2(contentSize.X, 30), ImGuiChildFlags.Borders);
        
        if (ImGui.Button(channelStatus == PlaybackState.Playing ? FontAwesome5.StepBackward : FontAwesome5.Play, new Vector2(30, 30)))
        {
            Bass.ChannelPlay(bassChannel, true);
        }

        ImGui.SameLine();
        contentSize = ImGui.GetContentRegionAvail();
        var drawList = ImGui.GetWindowDrawList();
        var progress = (float)Bass.ChannelGetPosition(bassChannel) / Bass.ChannelGetLength(bassChannel);
        var playheadXPos = (contentSize.X * progress);
        if (channelStatus == PlaybackState.Playing)
        {
            drawList.AddRectFilled(ImGui.GetCursorScreenPos()+new Vector2(playheadXPos, 0), ImGui.GetCursorScreenPos()+new Vector2(playheadXPos+2, 30), 0xffffffff);
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
                drawList.AddLine(ImGui.GetCursorScreenPos()+new Vector2(xPos, 15-(maxAmpFloat*15)), ImGui.GetCursorScreenPos()+new Vector2(xPos, 15+(maxAmpFloat*15)), 0xff0000ff);
            }
            else
            {
                drawList.AddLine(ImGui.GetCursorScreenPos()+new Vector2(xPos, 15-(maxAmpFloat*15)), ImGui.GetCursorScreenPos()+new Vector2(xPos, 15+(maxAmpFloat*15)), 0xffffffff);
            }
            

        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleVar();
    }
}