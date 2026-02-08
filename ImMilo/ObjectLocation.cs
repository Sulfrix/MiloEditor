using MiloLib.Assets;
using Object = MiloLib.Assets.Object;

namespace ImMilo;

public abstract class ObjectLocation
{
    public static T? FindObject<T>(string name, Object thisObj) where T : Object
    {
        // Step 1: find what directory contains thisObj
        var thisObjFind = FindEntryRecurse(thisObj, Program.currentScene.dirMeta);
        if (thisObjFind == null)
        {
            return null;
        }

        foreach (var dir in thisObjFind.Value.Item2)
        {
            foreach (var entry in dir.entries)
            {
                if (entry.name == name)
                {
                    if (entry.obj is T)
                    {
                        return entry.obj as T;
                    }
                }
            }
        }

        return null;

    }

    public static (DirectoryMeta.Entry, DirectoryMeta)? FindEntryRecurse(string name, DirectoryMeta thisDir)
    {
        foreach (var entry in thisDir.entries)
        {
            if (entry.name == name)
            {
                return (entry, thisDir);
            }

            if (entry.dir != null)
            {
                var recurseResult = FindEntryRecurse(name, entry.dir);
                if (recurseResult != null)
                {
                    return recurseResult;
                }
            }
        }

        return null;
    }

    public static (DirectoryMeta.Entry, List<DirectoryMeta>)? FindEntryRecurse(Object obj, DirectoryMeta thisDir)
    {
        foreach (var entry in thisDir.entries)
        {
            if (entry.obj == obj)
            {
                return (entry, [thisDir]);
            }

            if (entry.dir != null)
            {
                var recurseResult = FindEntryRecurse(obj, entry.dir);
                if (recurseResult != null)
                {
                    recurseResult.Value.Item2.Add(thisDir);
                    return recurseResult;
                }
            }
        }

        return null;
    }
}