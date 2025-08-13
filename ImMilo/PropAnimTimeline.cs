using System.Numerics;
using System.Text;
using ImGuiNET;
using MiloLib.Assets.Rnd;

namespace ImMilo;

public class PropAnimTimeline
{
    // key is the hashcode, so that it doesn't hold a reference
    public static Dictionary<int, PropAnimTimeline> Timelines = new();

    public WeakReference<RndPropAnim> target;
    public readonly int id;

    public float viewStart;
    public float viewEnd;
    public bool timelineHovered;

    private float rowHeight;

    public float keyframeRadius = 6;
    

    public static void DrawForPropAnim(RndPropAnim anim)
    {
        var code = anim.GetHashCode();
        if (!Timelines.TryGetValue(code, out var timeline))
        {
            timeline = new PropAnimTimeline(anim);
            Timelines.Add(code, timeline);
        }
        timeline.Draw();
    }

    public PropAnimTimeline(RndPropAnim target)
    {
        this.target = new WeakReference<RndPropAnim>(target);
        this.id = target.GetHashCode();
        RecalculateView();
    }

    public void RecalculateView()
    {
        viewStart = 0;
        viewEnd = 0;
        if (!target.TryGetTarget(out var anim))
        {
            return;
        }

        foreach (var propkey in anim.propKeys)
        {
            foreach (var ev in propkey.keys)
            {
                if (ev.Pos > viewEnd)
                {
                    viewEnd = ev.Pos;
                }
            }
        }

        viewEnd += 10;
    }
    
    // time -> timeline frac
    public float TransformKey(float pos)
    {
        var wide = viewEnd - viewStart;
        return (pos - viewStart) / wide;
    }

    public float TransformKey(RndPropAnim.PropKey.IAnimEvent key)
    {
        return TransformKey(key.Pos);
    }

    // timeline frac -> time
    public float UntransformPos(float pos)
    {
        return float.Lerp(viewStart, viewEnd, pos);
    }

    string GetTargetPath(RndPropAnim.PropKey key)
    {
        StringBuilder builder = new();
        builder.Append(key.target);
        foreach (var node in key.dtb.children)
        {
            if (node.value is Symbol sym)
            {
                builder.Append("->");
                builder.Append(sym);
            }
        }
        return builder.ToString();
    }
    
    public unsafe void Draw()
    {
        if (!target.TryGetTarget(out var anim))
        {
            Timelines.Remove(id);
            return;
        }

        float padding = 4;
        float textHeight = ImGui.GetTextLineHeight();
        rowHeight = textHeight*2 + padding;
        var keyCol = ImGui.GetColorU32(ImGuiCol.ButtonActive) | 0xff000000;
        var keyBorderCol = ImGui.GetColorU32(ImGuiCol.Border) | 0xff000000;
        var rowBorderCol = ImGui.GetColorU32(ImGuiCol.Border);
        var mainBGCol = ImGui.GetColorU32(ImGuiCol.MenuBarBg);
        var splitBGCol = ImGui.GetColorU32(ImGuiCol.WindowBg) | 0xff000000;
        var textCol = ImGui.GetColorU32(ImGuiCol.Text);
        var textAltCol = ImGui.GetColorU32(ImGuiCol.Text) & 0x80ffffff;
        RecalculateView();
        var avail = ImGui.GetContentRegionAvail();
        
        var mainWindowDrawList = ImGui.GetWindowDrawList();
        var headerPos = ImGui.GetCursorScreenPos();
        Vector2 headerSize = Vector2.Zero;
        
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        // Splitter methods need to be used through a pointer object, don't ask me
        ImDrawListSplitter splitter = new ImDrawListSplitter();
        ImDrawListSplitterPtr splitterPtr = new ImDrawListSplitterPtr(&splitter);
        // Main timeline code
        if (anim.propKeys.Any()) 
        {
            
            // Draw the header background
            {
                avail = ImGui.GetContentRegionAvail();
                headerSize = new Vector2(avail.X, textHeight + padding);
                ImGui.InvisibleButton("timelineHeader##" + id, headerSize);
                mainWindowDrawList.AddRectFilled(headerPos, headerPos + headerSize, splitBGCol);
                var headerHeight = headerSize with { X = 0 };
                //mainWindowDrawList.AddLine(headerPos + headerHeight, headerPos + headerSize, rowBorderCol);
            }
            
            ImGui.BeginChild("Timeline##" + id, new Vector2(avail.X, avail.Y-200), ImGuiChildFlags.Borders);
            var drawlist = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            avail = ImGui.GetContentRegionAvail();

            splitterPtr.Clear();
            splitterPtr.Split(drawlist.NativePtr, 3);
            splitterPtr.SetCurrentChannel(drawlist.NativePtr, 0);

            var rowPos = cursorPos;
            float splitWidth = 0;
            // Initial pass to calculate sizing
            foreach (var propkey in anim.propKeys)
            {
                string targetpath = GetTargetPath(propkey);
                string typeindicator = propkey.type1.ToString();
                rowPos.Y += rowHeight;
                float rowSplitWidth = MathF.Max(ImGui.CalcTextSize(targetpath).X, ImGui.CalcTextSize(typeindicator).X);
                if (rowSplitWidth > splitWidth)
                {
                    splitWidth = rowSplitWidth;
                }
            }
            splitWidth += padding*2;

            var totalHeight = rowPos.Y - cursorPos.Y;
            var endPos = new Vector2(rowPos.X, MathF.Max(cursorPos.Y + avail.Y, rowPos.Y));
            
            // Second pass to draw timeline labels and split panels
            drawlist.AddRectFilled(cursorPos, endPos + new Vector2(avail.X, 0), mainBGCol);
            drawlist.AddRectFilled(cursorPos, endPos + new Vector2(splitWidth, 0), splitBGCol);
            drawlist.AddLine(cursorPos + new Vector2(splitWidth, 0), endPos + new Vector2(splitWidth, 0), rowBorderCol);
            rowPos = cursorPos;
            foreach (var propkey in anim.propKeys)
            {
                string targetpath = GetTargetPath(propkey);
                string typeindicator = propkey.type1.ToString();
                drawlist.AddText(rowPos + new Vector2(padding, padding/2f), textCol, targetpath);
                drawlist.AddText(rowPos + new Vector2(padding, padding/2f+textHeight), textAltCol, typeindicator);
                rowPos.Y += rowHeight;
                drawlist.AddLine(rowPos + new Vector2(0, 0), rowPos + new Vector2(avail.X, 0), rowBorderCol);
            }
            rowPos = cursorPos;
            // Final pass to draw keyframes
            var scrollY = ImGui.GetScrollY();
            splitterPtr.SetCurrentChannel(drawlist.NativePtr, 1);
            drawlist.PushClipRect(cursorPos + new Vector2(splitWidth, scrollY), cursorPos + avail + new Vector2(0, scrollY));
            splitterPtr.SetCurrentChannel(drawlist.NativePtr, 2);
            drawlist.PushClipRect(cursorPos + new Vector2(splitWidth, scrollY), cursorPos + avail + new Vector2(0, scrollY));
            foreach (var propkey in anim.propKeys)
            {
                var keyStartPos = rowPos + new Vector2(splitWidth, rowHeight / 2f);
                var keyEndPos = rowPos + new Vector2(avail.X, rowHeight / 2f);
                for (int i = 0; i < propkey.keys.Count; i++)
                {
                    RndPropAnim.PropKey.IAnimEvent ev = propkey.keys[i];
                    RndPropAnim.PropKey.IAnimEvent? prev = null;
                    var equal = false;
                    if (i > 0)
                    {
                        prev = propkey.keys[i - 1];
                        equal = ev.IsEqual(prev);
                    }
                    var transformed = TransformKey(ev);
                    var keyPos = Vector2.Lerp(keyStartPos, keyEndPos, transformed);
                    drawlist.AddCircleFilled(keyPos, keyframeRadius, keyCol, 4);
                    drawlist.AddCircle(keyPos, keyframeRadius, textCol, 4);
                    if (equal)
                    {
                        var prevTransformed = TransformKey(prev);
                        var prevKeyPos = Vector2.Lerp(keyStartPos, keyEndPos, prevTransformed);
                        splitterPtr.SetCurrentChannel(drawlist.NativePtr, 1);
                        drawlist.AddLine(prevKeyPos, keyPos, rowBorderCol, keyframeRadius*2f);
                        splitterPtr.SetCurrentChannel(drawlist.NativePtr, 2);
                    }
                }
                rowPos.Y += rowHeight;
            }
            
            splitterPtr.SetCurrentChannel(drawlist.NativePtr, 2);
            drawlist.PopClipRect();
            splitterPtr.SetCurrentChannel(drawlist.NativePtr, 1);
            drawlist.PopClipRect();
            splitterPtr.Merge(drawlist.NativePtr);
            ImGui.InvisibleButton("timelineInput", new Vector2(avail.X, totalHeight));
            ImGui.EndChild();
        }
        else
        {
            ImGui.Text("Animation is empty");
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleVar();
        {
            ImGui.BeginChild("TimelineControls##" + id, ImGui.GetContentRegionAvail());
            
            ImGui.EndChild();
        }
        splitterPtr.ClearFreeMemory();
    }
}