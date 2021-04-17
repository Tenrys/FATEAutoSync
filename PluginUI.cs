using ImGuiNET;

namespace FateAutoSync
{
  public class PluginUI
  {
    public bool IsVisible { get; set; }

    public void Draw()
    {
      if (!IsVisible)
        return;
    }
  }
}
