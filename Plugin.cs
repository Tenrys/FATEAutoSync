using System;
using Dalamud.Plugin;
using Dalamud.Game.Internal.Gui;
using FateAutoSync.Attributes;
using System.Runtime.InteropServices;

namespace FateAutoSync
{
  public class Plugin : IDalamudPlugin
  {
    public string Name => "FATE AutoSync";

    private DalamudPluginInterface pluginInterface;
    private PluginCommandManager<Plugin> commandManager;
    private Configuration config;
    // private PluginUI ui;

    // Command execution
    private delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
    private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
    private ProcessChatBoxDelegate ProcessChatBox;
    private IntPtr uiModule = IntPtr.Zero;

    // Our specific stuff
    private IntPtr inFatePtr = (IntPtr)0x7FF70E25CC51;
    private bool inFate = false;
    private bool firstRun = true;
    private ChatGui chat;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
      this.pluginInterface = pluginInterface;
      chat = pluginInterface.Framework.Gui.Chat;

      config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
      config.Initialize(this.pluginInterface);

      // this.ui = new PluginUI();
      // this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

      pluginInterface.Framework.OnUpdateEvent += Update;

      commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);

      InitializePointers();
    }

    private void Update(Dalamud.Game.Internal.Framework framework)
    {
      if (!this.config.enabled) return;

      var oldInFate = inFate;
      inFate = Marshal.ReadByte(inFatePtr) == 1;
      if (oldInFate != inFate)
      {
        if (inFate)
        {
          if (firstRun)
          {
            chat.Print("FATE Auto Sync ran for the first time in this session (/fateautosync to toggle)");
            firstRun = false;
          }
          ExecuteCommand("/levelsync on");
        }
      }
    }

    [Command("/fateautosync")]
    [HelpMessage("Toggles the plug-in on and off.")]
    public void ToggleCommand(string command, string args)
    {
      this.config.enabled = !this.config.enabled;
      chat.Print($"Toggled FATE Auto Sync {(this.config.enabled ? "on" : "off")}");
    }

    // Courtesy of https://github.com/UnknownX7/QoLBar
    private unsafe void InitializePointers()
    {
      try
      {
        var getUIModulePtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
        var easierProcessChatBoxPtr = pluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
        var uiModulePtr = pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8");

        var GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);

        uiModule = GetUIModule(*(IntPtr*)uiModulePtr);
        ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(easierProcessChatBoxPtr);
      }
      catch { PluginLog.Error("Failed loading ExecuteCommand"); }
    }

    public void ExecuteCommand(string command)
    {
      var chat = this.pluginInterface.Framework.Gui.Chat;
      try
      {
        var bytes = System.Text.Encoding.UTF8.GetBytes(command);

        var mem1 = Marshal.AllocHGlobal(400);
        var mem2 = Marshal.AllocHGlobal(bytes.Length + 30);

        Marshal.Copy(bytes, 0, mem2, bytes.Length);
        Marshal.WriteByte(mem2 + bytes.Length, 0);
        Marshal.WriteInt64(mem1, mem2.ToInt64());
        Marshal.WriteInt64(mem1 + 8, 64);
        Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
        Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);

        ProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);

        Marshal.FreeHGlobal(mem1);
        Marshal.FreeHGlobal(mem2);
      }
      catch (Exception err) { chat.PrintError(err.Message); }
    }

    #region IDisposable Support
    protected virtual void Dispose(bool disposing)
    {
      if (!disposing) return;

      this.commandManager.Dispose();

      this.pluginInterface.SavePluginConfig(this.config);

      // this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

      this.pluginInterface.Framework.OnUpdateEvent -= Update;

      this.pluginInterface.Dispose();
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
