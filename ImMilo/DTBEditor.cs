using System.Numerics;
using IconFonts;
using ImGuiNET;
using MiloLib.Assets;

namespace ImMilo;

using static ObjectFields;

public class DTBEditor
{

    public static void NodeSetType(ref DTBNode node, NodeType type)
    {
        node.type = type;
        switch (type)
        {
            case NodeType.Int:
                node.value = 0;
                break;
            case NodeType.Float:
                node.value = 0;
                break;
            case NodeType.Variable:
            case NodeType.Object:
            case NodeType.Symbol:
            case NodeType.Unhandled:
            case NodeType.IfDef:
            case NodeType.Else:
            case NodeType.EndIf:
            case NodeType.String:
            case NodeType.Define:
            case NodeType.Include:
            case NodeType.Merge:
            case NodeType.IfNDef:
            case NodeType.Autorun:
            case NodeType.Undef:
                node.value = new Symbol(0, "");
                break;
            case NodeType.Array:
            case NodeType.Command:
            case NodeType.Property:
                DTBArrayParent parent = new DTBArrayParent();
                parent.children = new();
                node.value = parent;
                break;
            default:
                node.type = NodeType.Unhandled;
                node.value = new Symbol(0, "");
                break;
        }
    }


    
    public static void EditDTBNode(ref DTBNode node, int index)
    {
        var nodeTypes = EditorPanel.GetCachedEnumValues(typeof(NodeType));
        var comboArray = nodeTypes.ToArray();
        
        var type = nodeTypes.IndexOf(node.type.ToString());
        ImGui.SetNextItemWidth(120f);
        if (ImGui.Combo("##nodetype", ref type, comboArray, comboArray.Length))
        {
            NodeSetType(ref node, (NodeType)Enum.Parse(typeof(NodeType), nodeTypes[type]));
        }
        ImGui.SameLine();

        ImGui.SetNextItemWidth(-3f);
        switch (node.value)
        {
            case int intValue:
                if (ImGui.InputInt("##nodevalue", ref intValue))
                {
                    node.value = intValue;
                };
                break;
            case float floatValue:
                if (ImGui.InputFloat("##nodevalue", ref floatValue))
                {
                    node.value = floatValue;
                }
                break;
            case string stringValue:
                if (ImGui.InputText("##nodevalue", ref stringValue, 128))
                {
                    node.value = stringValue;
                }
                break;
            case Symbol symbolValue:
                var val = symbolValue.value;
                if (ImGui.InputText("##nodevalue", ref val, 128))
                {
                    node.value = new Symbol((uint)val.Length, val);
                }

                break;
            case DTBArrayParent parent:
                if (ImGui.CollapsingHeader(parent.children.Count + " items###nodevalue"))
                {
                    ImGui.Indent();
                    EditDTBNodes(parent.children);
                    ImGui.Unindent();
                }
                break;
        }
    }

    public static void EditDTBNodes(List<DTBNode> nodes)
    {
        var collectionButtonSize = new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
        int toDelete = -1;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.Y, ImGui.GetStyle().ItemSpacing.Y));
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            ImGui.PushID(i);
            if (ImGui.Button(FontAwesome5.Minus, collectionButtonSize))
            {
                toDelete = i;
            }
            ImGui.SameLine();
            EditDTBNode(ref node, i);
            if (node.value != nodes[i].value || node.type != nodes[i].type)
            {
                nodes[i] = node;
            }
            ImGui.PopID();
        }

        if (toDelete != -1)
        {
            nodes.RemoveAt(toDelete);
        }
        
        if (ImGui.Button(FontAwesome5.Plus, collectionButtonSize))
        {
            var newNode = new DTBNode();
            newNode.type = NodeType.Int;
            newNode.value = 0;
            nodes.Add(newNode);
        }
        ImGui.PopStyleVar();
    }
}