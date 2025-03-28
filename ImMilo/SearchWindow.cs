using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using IconFonts;
using ImGuiNET;
using MiloLib;
using MiloLib.Assets;

namespace ImMilo;

public class SearchWindow
{
    public String Query = "";
    private String Name;
    public MiloFile? TargetScene;
    public List<SearchResult> Results = new();
    public Mutex ResultsMutex = new();
    
    private Task? SearchTask;
    private CancellationTokenSource? SearchCancelTokenSource;

    public bool DoFocus = false;

    public SearchWindow(String name)
    {
        Name = name;
    }

    public void DrawWindow(ref bool open)
    {
        if (open)
        {
            ImGui.Begin(Name, ref open);
            Draw();
            ImGui.End();
        }
    }

    public void Draw(bool popup = false)
    {
        if (DoFocus)
        {
            DoFocus = false;
            ImGui.SetKeyboardFocusHere();
        }
        if (ImGui.InputText("##" + Name, ref Query, 1000))
        {
            UpdateQuery();
        }
        DrawResults(popup);
    }

    private void DrawResults(bool popup = false)
    {
        var size = ImGui.GetContentRegionAvail();
        if (popup)
        {
            size.Y = ImGui.GetFontSize() * 20;
        }
        ImGui.BeginChild("Results", size, ImGuiChildFlags.Borders);
        if (IsSearching())
        {
            ImGui.TextDisabled("Searching...");
        }
        else
        {
            if (Results.Count == 0 && Query.Length > 0)
            {
                ImGui.TextDisabled("No results found.");
            }

            ResultsMutex.WaitOne();
            for(int i = 0; i < Results.Count; i++)
            {
                var result = Results[i];
                if (ImGui.Selectable(result.ToString() + "##" + i))
                {
                    result.Result.Navigate();
                }

                if (i >= Settings.Editing.maxSearchResults)
                {
                    ImGui.TextDisabled($"Truncated to {Settings.Editing.maxSearchResults} results.");
                    ImGui.SameLine();
                    if (ImGui.TextLink("Adjust Limit"))
                    {
                        Program.NavigateObject(Settings.Editing);
                    }
                    break;
                }
            }
            ResultsMutex.ReleaseMutex();
        }
        ImGui.EndChild();
    }

    public bool MatchesQuery(String operand)
    {
        return operand.Contains(Query);
    }

    public bool ObjectMatches(object operand)
    {
        switch (operand)
        {
            case Symbol symbolValue:
                return MatchesQuery(symbolValue.value);
            case String stringValue:
                return MatchesQuery(stringValue);
            case DirectoryMeta directoryValue:
                return MatchesQuery(directoryValue.name);
            case DirectoryMeta.Entry entryValue:
                return MatchesQuery(entryValue.name);
            default:
                return false;
        }
    }

    public async void UpdateQuery()
    {
        if (IsSearching())
        {
            SearchCancelTokenSource?.Cancel();
            try
            {
                await SearchTask!;
            } catch (OperationCanceledException) {}
        }
        
        // Wait on the mutex to ensure the UI is finished drawing the results list before modifying it
        ResultsMutex.WaitOne();
        ResultsMutex.ReleaseMutex();
        Results.Clear();
        if (Query.Length == 0)
        {
            return;
        }

        if (TargetScene == null)
        {
            return;
        }
        SearchTask = PerformSearch();
    }

    public bool IsSearching()
    {
        if (SearchTask == null)
        {
            return false;
        }
        return SearchTask.Status == TaskStatus.Running || SearchTask.Status == TaskStatus.WaitingForActivation || SearchTask.Status == TaskStatus.WaitingToRun;
    }

    private async Task PerformSearch()
    {
        if (IsSearching())
        {
            Console.WriteLine("Search overlap");
        }
        SearchCancelTokenSource = new CancellationTokenSource();
        
        var task = Task.Run(() =>
        {
            SearchCancelTokenSource?.Token.ThrowIfCancellationRequested();
            SearchDirectory(TargetScene.dirMeta, ref Results, new List<SearchBreadcrumb>());
        });

        try
        {
            await task;
        }
        catch (OperationCanceledException e)
        {
            // Wait on the mutex to ensure the UI is finished drawing the results list before modifying it
            ResultsMutex.WaitOne();
            ResultsMutex.ReleaseMutex();
            Results.Clear();
        }
        finally
        {
            SearchCancelTokenSource?.Dispose();
        }

    }

    public abstract class SearchBreadcrumb
    {
        
        public virtual String Delimiter => "/";

        /// <summary>
        /// Navigates to the breadcrumb target in the editor.
        /// </summary>
        /// <returns>True if the navigation was successful. If false is returned, navigate to the previous breadcrumb.</returns>
        public virtual bool Navigate()
        {
            return true;
        }
    }

    public class SearchBreadcrumb<T> : SearchBreadcrumb
    {
        public T Target;
        public override string ToString()
        {
            return Target.ToString();
        }
    }

    public class DirectoryBreadcrumb : SearchBreadcrumb<DirectoryMeta>
    {
        public DirectoryBreadcrumb(DirectoryMeta meta)
        {
            Target = meta;
        }
        
        public override string ToString()
        {
            if (Target == Program.currentScene.dirMeta)
            {
                return "root";
            }
            return Target.name.value;
        }

        public override bool Navigate()
        {
            Program.NavigateObject(Target.directory);
            return true;
        }
    }

    public class InlineDirBreadcrumb : DirectoryBreadcrumb
    {
        public InlineDirBreadcrumb(DirectoryMeta meta) : base(meta) {}

        public override string Delimiter => "\\";
    }

    public class EntryBreadcrumb : SearchBreadcrumb<DirectoryMeta.Entry>
    {
        public EntryBreadcrumb(DirectoryMeta.Entry entry)
        {
            Target = entry;
        }
        
        public override string ToString()
        {
            return Target.name.value;
        }

        public override bool Navigate()
        {
            Program.NavigateObject(Target.obj);
            return true;
        }
    }

    public class FieldBreadcrumb : SearchBreadcrumb<FieldInfo>
    {
        public object Parent;

        public FieldBreadcrumb(object parent, FieldInfo field)
        {
            Parent = parent;
            Target = field;
        }
        
        public override string ToString()
        {
            return Target.Name;
        }
        public override String Delimiter => "->";
        public override bool Navigate()
        {
            Program.NavigateObject(Parent);
            // TODO: Focus the field itself

            return true;
        }
    }

    public class ListItemBreadcrumb : SearchBreadcrumb<int>
    {
        public ListItemBreadcrumb(int index)
        {
            Target = index;
        }

        public override string ToString()
        {
            return "[" + Target + "]";
        }

        public override bool Navigate()
        {
            return false;
        }

        public override string Delimiter => "";
    }

    /// <summary>
    /// A search result is a list of "breadcrumbs" describing the data path to the matching object.
    /// The final breadcrumb is what matched the search filter.
    /// </summary>
    public class SearchResult(List<SearchBreadcrumb> breadcrumbs)
    {
        public readonly List<SearchBreadcrumb> Breadcrumbs = breadcrumbs;

        public SearchBreadcrumb Result => Breadcrumbs.Last();
        
        public object? GetContainer()
        {
            for (int i = Breadcrumbs.Count - 1; i >= 0; i--)
            {
                var b = Breadcrumbs[i];
                if (Breadcrumbs[i] is DirectoryBreadcrumb dir)
                {
                    return dir.Target;
                }

                if (Breadcrumbs[i] is EntryBreadcrumb entry)
                {
                    return entry.Target;
                } 
            }
            return null;
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            for (int i = 0; i < Breadcrumbs.Count; i++)
            {
                var b = Breadcrumbs[i];
                if (i > 0)
                {
                    builder.Append(b.Delimiter);
                }
                builder.Append(b);
            }
            return builder.ToString();
        }
    }

    public static List<T> CopyAndAdd<T>(List<T> list, T item)
    {
        if (list.Count == 0)
        {
            list.Add(item);
            return list;
        }
        var copy = new List<T>(list);
        copy.Add(item);
        return copy;
    }

    public void SearchDirectory(DirectoryMeta dir, ref List<SearchResult> results, List<SearchBreadcrumb> breadcrumbs, bool inline = false)
    {
        SearchCancelTokenSource?.Token.ThrowIfCancellationRequested();
        if (inline)
        {
            breadcrumbs = CopyAndAdd(breadcrumbs, new InlineDirBreadcrumb(dir));
        }
        else
        {
            breadcrumbs = CopyAndAdd(breadcrumbs, new DirectoryBreadcrumb(dir));
        }
        

        if (ObjectMatches(dir))
        {
            results.Add(new SearchResult(breadcrumbs));
        }
        
        // Match fields in the directory object
        SearchObject(dir.directory, ref results, breadcrumbs);
        
        
        // Scan directory children
        for (int i = 0; i < dir.entries.Count; i++)
        {
            SearchCancelTokenSource?.Token.ThrowIfCancellationRequested();
            var entry = dir.entries[i];

            if (entry.dir != null)
            {
                if (Settings.Editing.fastSearch)
                {
                    Task.Run(() => SearchDirectory(entry.dir, ref Results, breadcrumbs));
                }
                else
                {
                    SearchDirectory(entry.dir, ref results, breadcrumbs);
                }
            }
            else
            {
                var entryBreadcrumb = CopyAndAdd(breadcrumbs, new EntryBreadcrumb(entry));
                if (ObjectMatches(entry))
                {
                    results.Add(new SearchResult(entryBreadcrumb));
                }
                SearchObject(entry.obj, ref results, entryBreadcrumb);
            }
        }

        if (dir.directory is ObjectDir objDir)
        {
            // Scan inline subdirs
            for (int i = 0; i < objDir.inlineSubDirs.Count; i++)
            {
                var entry = objDir.inlineSubDirs[i];

                SearchDirectory(entry, ref results, breadcrumbs, true);
            }
        }
        
        
    }

    public void SearchObject(object obj, ref List<SearchResult> results, List<SearchBreadcrumb> breadcrumbs)
    {
        SearchCancelTokenSource?.Token.ThrowIfCancellationRequested();
        if (obj == null)
        {
            return;
        }
        if (breadcrumbs.Count > 15)
        {
            // Failsafe to make sure circular references don't get traversed.
            // Checking for breadcrumb count is probably more efficient than checking for duplicate objects.
            return;
        }

        // Mooch off of the EditorPanel's field caching.
        var fields = EditorPanel.GetCachedFields(obj.GetType());

        foreach (var field in fields)
        {
            SearchCancelTokenSource?.Token.ThrowIfCancellationRequested();
            // Hack: Don't iterate through inline subdirs through the reference, rather do it through a dedicated method.
            if (field.Name == "inlineSubDirs")
            {
                continue;
            }
            var fieldValue = field.GetValue(obj);
            switch (fieldValue)
            {
                case Symbol symbolValue:
                    if (MatchesQuery(symbolValue.value))
                    {
                        results.Add(new SearchResult(CopyAndAdd(breadcrumbs, new FieldBreadcrumb(obj, field))));
                    }
                    break;
                case String stringValue:
                    if (MatchesQuery(stringValue))
                    {
                        results.Add(new SearchResult(CopyAndAdd(breadcrumbs, new FieldBreadcrumb(obj, field))));
                    }
                    break;
                case IList list:
                    var fieldCrumbs = CopyAndAdd(breadcrumbs, new FieldBreadcrumb(obj, field));
                    for (int i = 0; i < list.Count; i++)
                    {
                        var listObj = list[i];
                        if (ObjectMatches(listObj))
                        {
                            results.Add(new SearchResult(CopyAndAdd(fieldCrumbs, new ListItemBreadcrumb(i))));
                        }
                        else
                        {
                            SearchObject(listObj, ref results, CopyAndAdd(fieldCrumbs, new ListItemBreadcrumb(i)));
                        }
                    }

                    break;
                case object nestedObject when fieldValue != null:
                    SearchObject(nestedObject, ref results, CopyAndAdd(breadcrumbs, new FieldBreadcrumb(obj, field)));
                    break;
            }
        }
    }

    public static SearchWindow mainWindow = new("Search Scene");
    public static bool mainWindowOpen;

    public static void FocusMainWindow()
    {
        mainWindowOpen = true;
        mainWindow.DoFocus = true;
    }
}