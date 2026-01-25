using ImGuiNET;
using TinyDialogsNet;
using Veldrid.Sdl2;

namespace ImMilo;

public class KitWizard : UIOverride
{

    public KitWizardProject? Project;
    public static string[] drums = { "snare", "tom1", "tom2", "tom3", "hat", "hatopen", "ride", "crash", "kick" };
    
    public void MenuBar()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Project"))
                {
                    var (cancelled, path) = TinyDialogs.SelectFolderDialog("Select a folder to create a project in.");
                    if (!cancelled)
                    {
                        NewProject(path);
                    }
                }
                
                if (ImGui.MenuItem("Open Project"))
                {
                    var (cancelled, path) = TinyDialogs.OpenFileDialog("Select a kitwizard.json file.", "", false,
                        new FileFilter("KitWizard project files", []));
                    if (!cancelled)
                    {
                        Project = KitWizardProject.Load(path.First());
                    }
                }

                if (ImGui.MenuItem("Close"))
                {
                    Program.uiOverride = null;
                }

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }

    public async void NewProject(string path)
    {
        var kitName = await Program.ShowTextPrompt("Enter drum kit name", "Kit Name", "custom_kit");
        Project = new KitWizardProject(path);
        Project.KitName = kitName;
        Project.Save();
    }

    public void Draw()
    {
        MenuBar();
        if (Project != null)
        {
            ImGui.Text(Project.KitName);
        }
    }

    public void OnDragDrop(DragDropEvent evt)
    {
        Project = KitWizardProject.Load(evt.File);
    }
}