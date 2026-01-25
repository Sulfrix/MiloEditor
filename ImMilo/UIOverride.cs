using Veldrid.Sdl2;

namespace ImMilo;

public interface UIOverride
{
    public void Draw();
    public void OnDragDrop(DragDropEvent evt);
}